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

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementPositionStd : IValueConverter
  {
    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      CultureInfo ci = CultureInfo.InvariantCulture;
      // Todo: Convert api measurement point
      object apiPoint = value;
      double axStd = 0, ayStd = 0, azStd = 0;

      string stdx = apiPoint == null || double.IsNaN(axStd) ? "---" : axStd.ToString("#0.00", ci);
      string stdy = apiPoint == null || double.IsNaN(ayStd) ? "---" : ayStd.ToString("#0.00", ci);
      string stdz = apiPoint == null || double.IsNaN(azStd) ? "---" : azStd.ToString("#0.00", ci);
      return string.Format(ci, "{0}, {1}, {2}", stdx, stdy, stdz);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
