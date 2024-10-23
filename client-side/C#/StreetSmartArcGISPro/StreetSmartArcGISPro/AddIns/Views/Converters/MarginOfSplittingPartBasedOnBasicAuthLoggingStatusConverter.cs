using System;
using System.Globalization;
using System.Windows.Data;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class MarginOfSplittingPartBasedOnBasicAuthLoggingStatusConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is bool credentials && credentials)
      {
        return "0,0,0,0";
      }
      return "0,20,0,0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
