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

using System.Collections.Generic;

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.VectorLayers;

using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class ShowInCyclorama : Button
  {
    #region Members

    private CycloMediaLayer _cycloMediaLayer;
    private VectorLayer _vectorLayer;

    #endregion

    #region Overrides

    protected override void OnClick()
    {
      IsChecked = !IsChecked;

      if (_cycloMediaLayer != null)
      {
        _cycloMediaLayer.IsVisibleInstreetSmart = IsChecked;
      }

      if (_vectorLayer != null)
      {
        _vectorLayer.IsVisibleInstreetSmart = IsChecked;
      }
    }

    protected override async void OnUpdate()
    {
      MapView mapView = MapView.Active;
      IReadOnlyList<Layer> layers = mapView?.GetSelectedLayers();

      if (layers?.Count == 1)
      {
        Layer layer = layers[0];
        ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;

        CycloMediaGroupLayer groupLayer = streetSmart.CycloMediaGroupLayer;
        _cycloMediaLayer = groupLayer?.GetLayer(layer);

        VectorLayerList vectorLayerList = await streetSmart.GetVectorLayerListAsync();
        _vectorLayer = vectorLayerList.GetLayer(layer);

        if (_cycloMediaLayer != null)
        {
          IsChecked = _cycloMediaLayer.IsVisibleInstreetSmart;
          Enabled = _cycloMediaLayer.IsVisible;
        }
        else if (_vectorLayer != null)
        {
          IsChecked = _vectorLayer.IsVisibleInstreetSmart;
          Enabled = _vectorLayer.IsVisible;
        }
        else
        {
          IsChecked = false;
          Enabled = false;
        }
      }
      else
      {
        Enabled = false;
      }

      base.OnUpdate();
    }

    #endregion
  }
}
