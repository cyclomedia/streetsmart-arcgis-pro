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

using System;
using System.Globalization;
using System.Windows.Data;

using ApiMeasurementPoint = GlobeSpotterAPI.MeasurementPoint;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementPosition : IValueConverter
  {
    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      CultureInfo ci = CultureInfo.InvariantCulture;
      ApiMeasurementPoint apiPoint = value as ApiMeasurementPoint;

      string x = apiPoint == null || double.IsNaN(apiPoint.x) ? "---" : apiPoint.x.ToString("#0.00", ci);
      string y = apiPoint == null || double.IsNaN(apiPoint.y) ? "---" : apiPoint.y.ToString("#0.00", ci);
      string z = apiPoint == null || double.IsNaN(apiPoint.z) ? "---" : apiPoint.z.ToString("#0.00", ci);
      return string.Format(ci, "{0}, {1}, {2}", x, y, z);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
