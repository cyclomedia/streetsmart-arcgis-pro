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

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for Configuration.xaml
  /// </summary>
  public partial class Configuration
  {
    #region Constructors

    public Configuration()
    {
      InitializeComponent();
    }

    #endregion

    #region Event handlers

    private void OnNumberValidation(object sender, TextCompositionEventArgs e)
    {
      var regex = new Regex("[^0-9]+");
      e.Handled = regex.IsMatch(e.Text);
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        if (passwordBox.IsEnabled)
        {
          ((dynamic) DataContext).ProxyPassword = passwordBox.Password;
        }
      }
    }

    private void OnPasswordLoaded(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        passwordBox.Password = passwordBox.IsEnabled ? ((dynamic) DataContext).ProxyPassword : string.Empty;
      }
    }

    private void OnPasswordEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        passwordBox.Password = passwordBox.IsEnabled ? ((dynamic) DataContext).ProxyPassword : string.Empty;
      }
    }

    #endregion
  }
}
