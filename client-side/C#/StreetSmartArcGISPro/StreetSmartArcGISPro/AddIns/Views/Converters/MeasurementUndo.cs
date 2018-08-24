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

using ArcGIS.Core.Geometry;

using StreetSmartArcGISPro.Overlays.Measurement;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementUndo : IMultiValueConverter
  {
    #region IValueConverter Members

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      MeasurementPoint point = values.Length >= 1 ? values[0] as MeasurementPoint : null;
      bool isOpen = values.Length >= 2 && values[1] is bool && (bool) values[1];
      MapPoint mapPoint = values.Length >= 3 ? values[2] as MapPoint : null;
      Measurement measurement = point?.Measurement;
      bool update = measurement?.ObjectId == null && mapPoint != null && !double.IsNaN(mapPoint.X) && !double.IsNaN(mapPoint.Y) && !double.IsNaN(mapPoint.Z)
                    || point?.LastPoint != null && point.LastPoint != mapPoint;
      return ((!measurement?.IsPointMeasurement ?? false) || isOpen) && update;
    }

    public object[] ConvertBack(object value, Type []targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
