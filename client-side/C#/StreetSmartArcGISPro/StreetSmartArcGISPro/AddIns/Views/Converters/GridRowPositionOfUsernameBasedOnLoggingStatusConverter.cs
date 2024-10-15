using System;
using System.Globalization;
using System.Windows.Data;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  

  public class GridRowPositionOfUsernameBasedOnLoggingStatusConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is bool isOAuth && isOAuth)
      {
        return 0;
      }
      return 1; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return null; 
    }
  }

}
