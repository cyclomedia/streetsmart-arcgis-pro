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
using System.Linq;
using System.Windows.Data;

using SystConvert = System.Convert;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class CombineBoolean : IMultiValueConverter
  {
    #region IMultiValueConverter Members

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      return values.Aggregate(true, (current, t) => current && (t as bool? ?? SystConvert.ToBoolean(t)));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
