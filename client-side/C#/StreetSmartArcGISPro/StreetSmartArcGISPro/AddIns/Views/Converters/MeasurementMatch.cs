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
using System.Drawing;
using System.Globalization;
using System.Windows.Data;

using StreetSmartArcGISPro.Utilities;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementMatch : IValueConverter
  {
    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (!(value is Bitmap bitmap))
      {
        bitmap = new Bitmap(64, 64);

        using (Graphics g = Graphics.FromImage(bitmap))
        {
          g.Clear(Color.Transparent);
        }
      }

      using (Graphics g = Graphics.FromImage(bitmap))
      {
        var pen = new Pen(Brushes.Black, 1);
        int hHeight = bitmap.Height/2;
        int hWidth = bitmap.Width/2;
        g.DrawLine(pen, 0, hHeight, bitmap.Width, hHeight);
        g.DrawLine(pen, hWidth, 0, hWidth, bitmap.Height);
      }

      return bitmap.ToBitmapSource();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
