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

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

using StreetSmartArcGISPro.Overlays;

using DrawingColor = System.Drawing.Color;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementColor : IValueConverter
  {
    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      Viewer thisViewer = value as Viewer;
      DrawingColor color = thisViewer?.Color ?? DrawingColor.Gray;
      Color mediaColor = Color.FromArgb(255, color.R, color.G, color.B);
      return new SolidColorBrush(mediaColor);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
