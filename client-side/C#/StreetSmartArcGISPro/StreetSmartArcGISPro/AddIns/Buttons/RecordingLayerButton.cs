/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018 - 2019, CycloMedia, All rights reserved.
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

using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.CycloMediaLayers;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Xml.Linq;
using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class RecordingLayerButton : Button
  {
    #region Properties

    public const string Id = "streetSmartArcGISPro_recordingLayerButton";

    private string RecordingLayerName
    {
      get
      {
        ResourceManager resourceManager = Properties.Resources.ResourceManager;
        LanguageSettings language = LanguageSettings.Instance;
        return resourceManager.GetString("RecordingLayerName", language.CultureInfo);
      }
    }

    #endregion

    #region Constructors

    protected RecordingLayerButton()
    {
      //IsChecked = false;
      //_mapView = MapView.Active;

      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);


      var groupLayer = ModuleStreetSmart.Current.GetOrAddCycloMediaGroupLayer(MapView.Active);
      if (groupLayer != null)
      {
        IsChecked = groupLayer.Any(layer => layer.Name == RecordingLayerName);
        groupLayer.CollectionChanged += OnGroupLayerChanged;
      }
     

      //ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      //CycloMediaGroupLayer groupLayer = streetSmart.GetCycloMediaGroupLayer(_mapView);

      //if (groupLayer != null)
      //{
      //  CheckCheckedState(groupLayer);
      //  groupLayer.PropertyChanged += OnLayerPropertyChanged;
      //}
    }

    #endregion

    #region Overrides

    protected override async void OnClick()
    {
      var cycloMediaGroupLayer = ModuleStreetSmart.Current.GetOrAddCycloMediaGroupLayer(MapView.Active);
      if (IsChecked)
      {
        await cycloMediaGroupLayer.RemoveLayerAsync(RecordingLayerName, true);

        IsChecked = false;
      }
      else
      {
        if (cycloMediaGroupLayer.Count == 0)
        {
          await cycloMediaGroupLayer.InitializeAsync();
        }

        if (!string.IsNullOrEmpty(RecordingLayerName))
        {
          await cycloMediaGroupLayer.AddAcceptableLayerAsync(RecordingLayerName);
        }

        IsChecked = true;
      }
    }

    #endregion

    #region Event handlers

    private void OnGroupLayerChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
      if(args.Action == NotifyCollectionChangedAction.Remove)
      {
        IsChecked = ModuleStreetSmart.Current.GetOrAddCycloMediaGroupLayer(MapView.Active).Any(layer => layer.Name == RecordingLayerName);
      }
    }

    private void OnLayerChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
      if (args.Action == NotifyCollectionChangedAction.Remove)
      {
        IsChecked = ModuleStreetSmart.Current.GetOrAddCycloMediaGroupLayer(MapView.Active).Any(layer => layer.Name == RecordingLayerName);
      }
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      //_mapView = args.IncomingView;
      //ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;

      //CycloMediaGroupLayer outGroupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(args.OutgoingView);

      //if (outGroupLayer != null)
      //{
      //  outGroupLayer.PropertyChanged -= OnLayerPropertyChanged;
      //}

      //if (args.IncomingView != null)
      //{
      //  CycloMediaGroupLayer inGroupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(args.IncomingView);

      //  if (inGroupLayer != null)
      //  {
      //    CheckCheckedState(inGroupLayer);
      //    inGroupLayer.PropertyChanged += OnLayerPropertyChanged;
      //  }
      //}

      if (args.IncomingView != null)
      {
        var inGroupLayer = ModuleStreetSmart.Current.GetOrAddCycloMediaGroupLayer(args.IncomingView);
        if (inGroupLayer != null && inGroupLayer.Any())
        {
          IsChecked = true;
          return;
        }
      }

      IsChecked = false;
    }

    #endregion

    #region Functions

    //private void CheckCheckedState(CycloMediaGroupLayer groupLayer)
    //{
    //  IsChecked = false;

    //  foreach (var layer in groupLayer)
    //  {
    //    if (layer.IsRemoved)
    //    {
    //      IsChecked = layer.Name != RecordingLayerName && IsChecked;
    //    }
    //    else
    //    {
    //      IsChecked = layer.Name == RecordingLayerName || IsChecked;
    //    }
    //  }
    //}

    #endregion
  }
}
