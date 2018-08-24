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

using System.Windows.Navigation;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for About.xaml
  /// </summary>
  public partial class About
  {
    #region Constructors

    public About()
    {
      InitializeComponent();
    }

    #endregion

    #region Event handlers

    private void OnNavigateUri(object sender, RequestNavigateEventArgs e)
    {
      var window = new NavigationWindow {Source = e.Uri};
      window.Show();
    }

    #endregion
  }
}
