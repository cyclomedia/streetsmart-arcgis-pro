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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmart.Common.Interfaces.API;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;

using FileSettings = StreetSmartArcGISPro.Configuration.File.Settings;
using FileLogin = StreetSmartArcGISPro.Configuration.File.Login;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;
using ThisResources = StreetSmartArcGISPro.Properties.Resources;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for streetSmart.xaml
  /// </summary>
  public partial class StreetSmart
  {
    #region Members

    private readonly FileSettings _settings;
    private readonly FileConfiguration _configuration;
    private readonly ConstantsViewer _constants;
    private readonly FileLogin _login;
    private readonly HistoricalRecordings _historicalRecordings;
    private readonly List<string> _openNearest;
    private readonly List<CycloMediaLayer> _layers;
    private readonly ViewerList _viewerList;
    private readonly MeasurementList _measurementList;
    private readonly CycloMediaGroupLayer _cycloMediaGroupLayer;

    private CrossCheck _crossCheck;
    private SpatialReference _lastSpatialReference;
    private bool _startOpenNearest;
    private VectorLayerList _vectorLayerList;

    #endregion

    #region Constructor

    public StreetSmart()
    {
      InitializeComponent();
      _settings = FileSettings.Instance;
      _constants = ConstantsViewer.Instance;
      _historicalRecordings = HistoricalRecordings.Instance;

      _login = FileLogin.Instance;

      _configuration = FileConfiguration.Instance;

      _openNearest = new List<string>();
      _crossCheck = null;
      _lastSpatialReference = null;
      _layers = new List<CycloMediaLayer>();
      _startOpenNearest = false;

      GetVectorLayerListAsync();
      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _viewerList = streetSmartModule.ViewerList;
      _measurementList = streetSmartModule.MeasurementList;
      _cycloMediaGroupLayer = streetSmartModule.CycloMediaGroupLayer;
    }

    #endregion

    #region Events API

    public async void OnAPIReady()
    {
      // ToDo: _measurementList.Api = _api;

      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        // ToDo: layer wfs
      }

      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // ToDo: measure permissions
      }

      if (GlobeSpotterConfiguration.MeasureSmartClick)
      {
        // ToDo: measure smart click
      }

      _settings.PropertyChanged += OnSettingsPropertyChanged;
      _cycloMediaGroupLayer.PropertyChanged += OnGroupLayerPropertyChanged;

      foreach (CycloMediaLayer layer in _cycloMediaGroupLayer)
      {
        if (!_layers.Contains(layer))
        {
          _layers.Add(layer);
          UpdateRecordingLayer(layer);
          layer.PropertyChanged += OnLayerPropertyChanged;
        }
      }

      _vectorLayerList.LayerAdded += OnAddVectorLayer;
      _vectorLayerList.LayerRemoved += OnRemoveVectorLayer;
      _vectorLayerList.LayerUpdated += OnUpdateVectorLayer;

      foreach (var vectorLayer in _vectorLayerList)
      {
        vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
      }

      await _vectorLayerList.LoadMeasurementsAsync();
    }

    public void OnOpenImageResult(string input, bool opened, string imageId)
    {
      if (!string.IsNullOrEmpty(input) && input.Contains("Vector3D") && opened)
      {
        input = input.Remove(0, 9);
        input = input.Remove(input.Length - 1, 1);
        var seperator = new[] {", "};
        string[] split = input.Split(seperator, StringSplitOptions.None);

        if (split.Length == 3)
        {
          // ToDo: Set point 3D
          // ToDo: set viewerID
          CultureInfo ci = CultureInfo.InvariantCulture;
          object point3D = null;
          int viewerId = 0;

          if (viewerId != -1)
          {
            OnShowLocationRequested((uint) viewerId, point3D);
          }
        }
      }
    }

    public async void OnImagePreviewCompleted(IPanoramaViewer panoramaViewer)
    {
      Viewer viewer = _viewerList.GetViewer(panoramaViewer);

      if (viewer != null)
      {
        if (GlobeSpotterConfiguration.AddLayerWfs)
        {
          await UpdateVectorLayerAsync();

          MapView mapView = MapView.Active;
          Map map = mapView?.Map;
          SpatialReference spatRef = map?.SpatialReference;
          Unit unit = spatRef?.Unit;
          double factor = unit?.ConversionFactor ?? 1;
          double overlayDrawDistance = _constants.OverlayDrawDistance / factor;
          // ToDo: set overlay draw distance to api
        }
      }

      await MoveToLocationAsync(panoramaViewer);
    }

    public void OnFeatureClicked(Dictionary<string, string> feature)
    {
      MapView mapView = MapView.Active;
      Map map = mapView?.Map;

      string uri = feature[VectorLayer.FieldUri];
      FeatureLayer layer = map?.FindLayer(uri) as FeatureLayer;

      string objectIdStr = feature[VectorLayer.FieldObjectId];
      long objectId = long.Parse(objectIdStr);
      mapView?.FlashFeature(layer, objectId);
      mapView?.ShowPopup(layer, objectId);
    }

    public async void OnImageDistanceSliderChanged(IPanoramaViewer panoramaViewer, double distance)
    {
      double e = 0.0;
      MapView mapView = MapView.Active;
      Map map = mapView?.Map;
      SpatialReference spatRef = map?.SpatialReference;
      Unit unit = spatRef?.Unit;
      double factor = unit?.ConversionFactor ?? 1;
      double overlayDrawDistance = distance * factor;

      if (Math.Abs(overlayDrawDistance - _constants.OverlayDrawDistance) > e)
      {
        _constants.OverlayDrawDistance = overlayDrawDistance;
        _constants.Save();
      }

      await UpdateVectorLayerAsync();
    }

    public void OnMaxViewers()
    {
      MessageBox.Show(ThisResources.streetSmart_OnMaxViewers_Failed);
    }

    public async void OnShowLocationRequested(uint viewerId, object point3D)
    {
      MapView thisView = MapView.Active;
      Envelope envelope = thisView?.Extent;

      if (envelope != null && point3D != null)
      {
        // ToDo: Move to Cyclorama map position
        double x = 0, y = 0, z = 0;
        MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);

        if (point != null)
        {
          Camera camera = new Camera
          {
            X = point.X,
            Y = point.Y,
            Z = point.Z,
            SpatialReference = point.SpatialReference
          };

          await QueuedTask.Run(() =>
          {
            thisView.PanTo(camera);
          });
        }
      }
    }

    public void OnDetailImagesVisibilityChanged(bool value)
    {
      _settings.ShowDetailImages = value;
      _settings.Save();
    }

    #endregion

    #region Events Properties

    private void OnGroupLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaGroupLayer groupLayer && args.PropertyName == "Count")
      {
        foreach (CycloMediaLayer layer in groupLayer)
        {
          if (!_layers.Contains(layer))
          {
            _layers.Add(layer);
            layer.PropertyChanged += OnLayerPropertyChanged;
          }
        }
      }
    }

    private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaLayer layer)
      {
        switch (args.PropertyName)
        {
          case "Visible":
          case "IsVisibleInstreetSmart":
            UpdateRecordingLayer(layer);
            break;
        }
      }
    }

    private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      switch (args.PropertyName)
      {
        case "CtrlClickHashTag":
          // ToDo: set multi window count
          break;
        case "CtrlClickDelta":
          // ToDo: set window spread
          break;
        case "ShowDetailImages":
          // ToDo: set viewer detail images visible
          break;
        case "EnableSmartClickMeasurement":
          if (GlobeSpotterConfiguration.MeasureSmartClick)
          {
            // ToDo: set measurement smart click mode enabled
          }

          break;
        case "CycloramaViewerCoordinateSystem":
          RestartstreetSmart();
          break;
      }
    }

    private async void OnVectorLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        VectorLayer vectorLayer = sender as VectorLayer;

        if (vectorLayer?.IsVisibleInstreetSmart ?? false)
        {
          switch (args.PropertyName)
          {
            case "IsVisibleInstreetSmart":
              await vectorLayer.GenerateGmlAsync();
              break;
            case "Gml":
              if (vectorLayer.LayerId == null || vectorLayer.GmlChanged)
              {
                RemoveVectorLayer(vectorLayer);
                AddVectorLayer(vectorLayer);
              }

              break;
          }
        }
        else
        {
          RemoveVectorLayer(vectorLayer);
        }
      }
    }

    #endregion

    #region Functions

    private async void GetVectorLayerListAsync()
    {
      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _vectorLayerList = await streetSmartModule.GetVectorLayerListAsync();
    }

    private void Initialize()
    {
    }

    private void RestartstreetSmart()
    {
      // Todo: Check API ready state
      _measurementList.Api = null;
      DockPanestreetSmart streetSmart = (dynamic) DataContext;
      _settings.PropertyChanged -= OnSettingsPropertyChanged;
      _cycloMediaGroupLayer.PropertyChanged -= OnGroupLayerPropertyChanged;
      _measurementList.RemoveAll();

      _vectorLayerList.LayerAdded -= OnAddVectorLayer;
      _vectorLayerList.LayerRemoved -= OnRemoveVectorLayer;
      _vectorLayerList.LayerUpdated -= OnUpdateVectorLayer;

      foreach (var vectorLayer in _vectorLayerList)
      {
        vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
      }

      foreach (var vectorLayer in _vectorLayerList)
      {
        uint? vectorLayerId = vectorLayer.LayerId;

        if (vectorLayerId != null)
        {
          // Todo: Remove layer api vector: vectorLayerId
          vectorLayer.LayerId = null;
        }
      }

      _viewerList.RemoveViewers();

      // Todo: check api ready state
      // Todo: get viewer Ids from api
      int[] viewerIds = new int[0];

      foreach (int viewerId in viewerIds)
      {
        // Todo: close image from api
      }

      Initialize();
    }

    #endregion

    #region Vector layer events

    private async void OnUpdateVectorLayer()
    {
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        await UpdateVectorLayerAsync();
      }
    }

    private async void OnAddVectorLayer(VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
        await UpdateVectorLayerAsync(vectorLayer);
      }
    }

    private void OnRemoveVectorLayer(VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
        RemoveVectorLayer(vectorLayer);
      }
    }

    #endregion

    #region vector layer functions

    private async Task UpdateVectorLayerAsync()
    {
      // ReSharper disable once ForCanBeConvertedToForeach
      for(int i = 0; i < _vectorLayerList.Count; i++)
      {
        VectorLayer vectorLayer = _vectorLayerList[i];
        await UpdateVectorLayerAsync(vectorLayer);
      }
    }

    private async Task UpdateVectorLayerAsync(VectorLayer vectorLayer)
    {
      if (vectorLayer.IsVisibleInstreetSmart)
      {
        await vectorLayer.GenerateGmlAsync();
      }
      else
      {
        RemoveVectorLayer(vectorLayer);
      }
    }

    private void AddVectorLayer(VectorLayer vectorLayer)
    {
      int minZoomLevel = _constants.MinVectorLayerZoomLevel;

      MySpatialReference cyclSpatRel = _settings.CycloramaViewerCoordinateSystem;
      string srsName = cyclSpatRel.SRSName;

      string layerName = vectorLayer.Name;
      string gml = vectorLayer.Gml;
      Color color = vectorLayer.Color;

      uint? layerId = 0;
      // ToDo: Add GML layer: _api?.AddGMLLayer(layerName, gml, srsName, color, true, false, minZoomLevel);
      vectorLayer.LayerId = layerId;
    }

    private void RemoveVectorLayer(VectorLayer vectorLayer)
    {
      uint? layerId = vectorLayer?.LayerId;

      if (layerId != null)
      {
        // Todo: Remove vector layer, layerId
        vectorLayer.LayerId = null;
      }
    }

    #endregion
  }
}
