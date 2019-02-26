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

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.CycloMediaLayers;

using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class RecordingLayer : Button
  {
    #region Members

    private readonly ConstantsRecordingLayer _constantsRecordingLayer;

    #endregion

    #region Constructors

    protected RecordingLayer()
    {
      IsChecked = false;
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      CycloMediaGroupLayer groupLayer = streetSmart.CycloMediaGroupLayer;
      _constantsRecordingLayer = ConstantsRecordingLayer.Instance;

      if (groupLayer != null)
      {
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
        await streetSmart.RemoveLayerAsync(_constantsRecordingLayer.RecordingLayerName);
      }
      else
      {
        await streetSmart.AddLayersAsync(_constantsRecordingLayer.RecordingLayerName);
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

    #endregion
  }
}
