﻿/*
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

using CefSharp.DevTools.CSS;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static StreetSmartArcGISPro.Configuration.File.Login;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class SignInEnabled : IMultiValueConverter
  {
    #region IValueConverter Members

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values[0] != null && (((OAuthStatus)values[0]) == OAuthStatus.SignedOut || ((OAuthStatus)values[0]) == OAuthStatus.None) && values[1] != null && !(bool)values[1];
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
