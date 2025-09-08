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

using System.Windows.Input;

using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for streetSmart.xaml
  /// </summary>
  public partial class StreetSmart
  {
    #region Constructor

    public StreetSmart()
    {
      InitializeComponent();
    }

    #endregion

    private void StreetSmartApi_MouseEnter(object sender, MouseEventArgs e)
    {
      ModulestreetSmart.Current.InsideViewer = true; // Set the flag in
    }

    private void StreetSmartApi_MouseLeave(object sender, MouseEventArgs e)
    {
      ModulestreetSmart.Current.InsideViewer = false; // Set the flag in
    }
  }
}
