﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  public class EnvironmentConverter : IValueConverter
  {
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return Environment.Is64BitProcess ? "x64" : "x86";
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return Binding.DoNothing;
    }
  }
}
