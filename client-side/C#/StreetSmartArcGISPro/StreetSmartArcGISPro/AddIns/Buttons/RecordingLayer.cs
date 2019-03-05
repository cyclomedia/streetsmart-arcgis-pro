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

using System.ComponentModel;
using System.Linq;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.CycloMediaLayers;

using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class RecordingLayer : Button
  {
    #region Members

    private readonly ConstantsRecordingLayer _constantsRecordingLayer;
    private MapView _mapView;

    #endregion

    #region Constructors

    protected RecordingLayer()
    {
      IsChecked = false;
      _mapView = MapView.Active;
      _constantsRecordingLayer = ConstantsRecordingLayer.Instance;

      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);

      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      CycloMediaGroupLayer groupLayer = streetSmart.GetCycloMediaGroupLayer(_mapView);

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
        await streetSmart.RemoveLayerAsync(_constantsRecordingLayer.RecordingLayerName, _mapView);
      }
      else
      {
        await streetSmart.AddLayersAsync(_constantsRecordingLayer.RecordingLayerName, _mapView);
      }
    }

    #endregion

    #region Event handlers

    private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaGroupLayer groupLayer && args.PropertyName == "Count")
      {
        IsChecked = groupLayer.Aggregate(false, (current, layer) => layer.Name == _constantsRecordingLayer.RecordingLayerName || current);
      }
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      _mapView = args.IncomingView;
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;

      CycloMediaGroupLayer outGroupLayer = streetSmart.GetCycloMediaGroupLayer(args.OutgoingView);

      if (outGroupLayer != null)
      {
        outGroupLayer.PropertyChanged -= OnLayerPropertyChanged;
      }

      if (args.IncomingView != null)
      {
        CycloMediaGroupLayer inGroupLayer = streetSmart.GetCycloMediaGroupLayer(args.IncomingView);

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
          IsChecked = layer.Name != _constantsRecordingLayer.RecordingLayerName && IsChecked;
        }
        else
        {
          IsChecked = layer.Name == _constantsRecordingLayer.RecordingLayerName || IsChecked;
        }
      }
    }

    #endregion
  }
}
