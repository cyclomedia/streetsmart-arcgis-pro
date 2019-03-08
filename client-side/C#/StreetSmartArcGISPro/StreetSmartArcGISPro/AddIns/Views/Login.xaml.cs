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

using System.Windows;
using System.Windows.Controls;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for Login.xaml
  /// </summary>
  public partial class Login
  {
    #region Constructors

    public Login()
    {
      InitializeComponent();
    }

    #endregion

    #region Event handlers

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        ((dynamic) DataContext).Password = passwordBox.Password;
      }
    }

    private void OnPasswordLoaded(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        passwordBox.Password = ((dynamic) DataContext).Password;
      }
    }

    private void OnCheckButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        ((dynamic) DataContext).Save();
      }
    }

    #endregion
  }
}
