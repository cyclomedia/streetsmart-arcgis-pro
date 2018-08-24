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

using ArcGIS.Desktop.Framework.Contracts;

using MeasurementDetailPane = StreetSmartArcGISPro.AddIns.DockPanes.MeasurementDetail;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class MeasurementDetail : Button
  {
    #region Members

    private readonly MeasurementDetailPane _pane;

    #endregion

    #region Constructor

    public MeasurementDetail()
    {
      _pane = MeasurementDetailPane.Get();
    }

    #endregion

    #region Overrides

    protected override void OnClick()
    {
      if (_pane.IsVisible)
      {
        _pane.Hide();
      }
      else
      {
        _pane.Activate();
      }
    }

    protected override void OnUpdate()
    {
      IsChecked = _pane.IsVisible;
      base.OnUpdate();
    }

    #endregion
  }
}
