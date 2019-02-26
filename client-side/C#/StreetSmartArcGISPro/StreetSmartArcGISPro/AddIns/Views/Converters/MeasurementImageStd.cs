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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using StreetSmartArcGISPro.CycloMediaLayers;

using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MeasurementImageStd : IValueConverter
  {
    #region IValueConverter Members

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      string result = string.Empty;
      string imageId = value as string;
      ModulestreetSmart streetSmart = ModulestreetSmart.Current;
      CycloMediaGroupLayer groupLayer = streetSmart.CycloMediaGroupLayer;
      AutoResetEvent taskWaiter = new AutoResetEvent(false);
      const int timeOut = 3000;

      Task.Run(async () =>
      {
        Recording recording = await groupLayer.GetRecordingAsync(imageId);
        double stdX = recording == null ? 0 : (recording.LongitudePrecision ?? 0);
        double stdY = recording == null ? 0 : (recording.LatitudePrecision ?? 0);
        double stdZ = recording == null ? 0 : (recording.HeightPrecision ?? 0);
        result = $"{stdX:0.00} {stdY:0.00} {stdZ:0.00}";
        taskWaiter.Set();
      });

      taskWaiter.WaitOne(timeOut);
      return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
