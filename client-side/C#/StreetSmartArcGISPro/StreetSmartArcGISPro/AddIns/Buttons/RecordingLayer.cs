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

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.CycloMediaLayers;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class RecordingLayer : Button
  {
    #region Members

    private MapView _mapView;

    #endregion

    #region Properties

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

    protected RecordingLayer()
    {
      IsChecked = false;
      _mapView = MapView.Active;

      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);

      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      CycloMediaGroupLayer groupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(_mapView);

      if (groupLayer != null)
      {
        CheckCheckedState(groupLayer);
        groupLayer.PropertyChanged += OnLayerPropertyChanged;
      }
    }

    #endregion

    #region Overrides

    protected override async void OnClick()
    {
      OnUpdate();
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;

      if (IsChecked)
      {
        await streetSmart.RemoveLayerAsync(RecordingLayerName, _mapView);
      }
      else
      {
        await streetSmart.AddLayersAsync(RecordingLayerName, _mapView);
      }
    }

    #endregion

    #region Event handlers

    private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaGroupLayer groupLayer && args.PropertyName == "Count")
      {
        IsChecked = groupLayer.Any(layer => layer.Name == RecordingLayerName);
      }
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      _mapView = args.IncomingView;
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;

      CycloMediaGroupLayer outGroupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(args.OutgoingView);

      if (outGroupLayer != null)
      {
        outGroupLayer.PropertyChanged -= OnLayerPropertyChanged;
      }

      if (args.IncomingView != null)
      {
        CycloMediaGroupLayer inGroupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(args.IncomingView);

        if (inGroupLayer != null)
        {
          CheckCheckedState(inGroupLayer);
          inGroupLayer.PropertyChanged += OnLayerPropertyChanged;
        }
      }
    }

    #endregion

    #region Functions

    private void CheckCheckedState(CycloMediaGroupLayer groupLayer)
    {
      IsChecked = false;

      foreach (var layer in groupLayer)
      {
        if (layer.IsRemoved)
        {
          IsChecked = layer.Name != RecordingLayerName && IsChecked;
        }
        else
        {
          IsChecked = layer.Name == RecordingLayerName || IsChecked;
        }
      }
    }

    #endregion
  }
}
