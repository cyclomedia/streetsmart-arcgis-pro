/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018, CycloMedia, All rights reserved.
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3.0 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library.
 */

using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;

namespace StreetSmartArcGISPro.AddIns.Modules
{
  internal class StreetSmart : Module
  {
    #region Members

    private static StreetSmart _streetSmart;

    private CycloMediaGroupLayer _cycloMediaGroupLayer;
    private VectorLayerList _vectorLayerList;
    private ViewerList _viewerList;
    private MeasurementList _measurementList;
    private readonly Agreement _agreement;

    #endregion

    #region Properties

    /// <summary>
    /// Retrieve the singleton instance to this module here
    /// </summary>
    public static StreetSmart Current
      =>
        _streetSmart ??
        (_streetSmart = (StreetSmart) FrameworkApplication.FindModule("StreetSmartArcGISPro_module"));

    public CycloMediaGroupLayer CycloMediaGroupLayer
    {
      get
      {
        if (_cycloMediaGroupLayer == null)
        {
          _cycloMediaGroupLayer = new CycloMediaGroupLayer();
          _cycloMediaGroupLayer.PropertyChanged += OnLayerPropertyChanged;
        }

        return _cycloMediaGroupLayer;
      }
    }

    public ViewerList ViewerList => _viewerList ?? (_viewerList = new ViewerList());

    public MeasurementList MeasurementList => _measurementList ?? (_measurementList = new MeasurementList());

    #endregion

    #region Constructor

    public StreetSmart()
    {
      _agreement = Agreement.Instance;

      if (_agreement.Value)
      {
        FrameworkApplication.State.Activate("StreetSmartArcGISPro_agreementAcceptedState");
      }

      Login login = Login.Instance;
      login.Check();
      MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
      MapClosedEvent.Subscribe(OnMapClosedDocument);
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Called by Framework when ArcGIS Pro is closing
    /// </summary>
    /// <returns>False to prevent Pro from closing, otherwise True</returns>
    protected override bool CanUnload()
    {
      return true;
    }

    protected override async void Uninitialize()
    {
      await RemoveLayersAsync(true);
      MapViewInitializedEvent.Unsubscribe(OnMapViewInitialized);
      MapClosedEvent.Unsubscribe(OnMapClosedDocument);
      base.Uninitialize();
    }

    #endregion

    #region Functions

    internal bool InsideScale()
    {
      return CycloMediaGroupLayer.InsideScale;
    }

    private bool ContainsCycloMediaLayer(MapView mapView = null)
    {
      return
        (mapView ?? MapView.Active)?.Map?.Layers.Aggregate(false,
          (current, layer) => (CycloMediaGroupLayer?.IsKnownName(layer.Name) ?? layer.Name == "CycloMedia") || current) ??
        false;
    }

    private async Task CloseCycloMediaLayerAsync(bool closeMap)
    {
      if (!ContainsCycloMediaLayer() || closeMap)
      {
        await RemoveLayersAsync(false);
      }

      if (closeMap)
      {
        Settings settings = Settings.Instance;
        Login login = Login.Instance;
        LayersRemovedEvent.Unsubscribe(OnLayerRemoved);
        DrawCompleteEvent.Unsubscribe(OnDrawComplete);
        settings.PropertyChanged -= OnSettingsPropertyChanged;
        login.PropertyChanged -= OnLoginPropertyChanged;
      }
    }

    public async Task<VectorLayerList> GetVectorLayerListAsync()
    {
      if (_vectorLayerList == null)
      {
        _vectorLayerList = new VectorLayerList();
        await _vectorLayerList.DetectVectorLayersAsync();
      }

      return _vectorLayerList;
    }

    public async Task AddLayersAsync(MapView mapView)
    {
      await AddLayersAsync(null, mapView);
    }

    public async Task AddLayersAsync(string name, MapView mapView = null)
    {
      if (CycloMediaGroupLayer.Count == 0)
      {
        await CycloMediaGroupLayer.InitializeAsync(mapView);
      }

      if (!string.IsNullOrEmpty(name))
      {
        await CycloMediaGroupLayer.AddLayerAsync(name, mapView);
      }
    }

    public async Task RemoveLayerAsync(string name)
    {
      await CycloMediaGroupLayer.RemoveLayerAsync(name, true);
    }

    public async Task RemoveLayersAsync(bool fromMap)
    {
      await CycloMediaGroupLayer.DisposeAsync(fromMap);
    }

    #endregion

    #region Event handlers

    private async void OnMapViewInitialized(MapViewEventArgs args)
    {
      CycloMediaLayer.ResetYears();
      LayersRemovedEvent.Subscribe(OnLayerRemoved);
      DrawCompleteEvent.Subscribe(OnDrawComplete);

      if (ContainsCycloMediaLayer(args.MapView))
      {
        await AddLayersAsync(args.MapView);
      }

      Settings settings = Settings.Instance;
      Login login = Login.Instance;
      settings.PropertyChanged += OnSettingsPropertyChanged;
      login.PropertyChanged += OnLoginPropertyChanged;

      if (settings.CycloramaViewerCoordinateSystem != null)
      {
        await CoordSystemUtils.CheckInAreaCycloramaSpatialReferenceAsync();
      }

      if (!_agreement.Value)
      {
        PropertySheet.ShowDialog("streetSmartArcGISPro_optionsPropertySheet", "streetSmartArcGISPro_agreementPage");
      }
    }

    private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "RecordingLayerCoordinateSystem")
      {
        foreach (var layer in CycloMediaGroupLayer)
        {
          await layer.UpdateLayerAsync();
        }
      }
    }

    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Credentials")
      {
        Login login = Login.Instance;

        foreach (var layer in CycloMediaGroupLayer)
        {
          if (login.Credentials)
          {
            await layer.RefreshAsync();
          }
          else
          {
            await layer.MakeEmptyAsync();
            Project project = Project.Current;
            await project.SaveEditsAsync();
          }
        }
      }
    }

    private async void OnMapClosedDocument(MapClosedEventArgs args)
    {
      await CloseCycloMediaLayerAsync(true);
    }

    private async void OnLayerRemoved(LayerEventsArgs args)
    {
      await CloseCycloMediaLayerAsync(false);
    }

    private void OnDrawComplete(MapViewEventArgs args)
    {
      // toDo: is this function necessary?
    }

    private async void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Count")
      {
        if (!CycloMediaGroupLayer.ContainsLayers)
        {
          await RemoveLayersAsync(true);
        }
      }
    }

    #endregion
  }
}
