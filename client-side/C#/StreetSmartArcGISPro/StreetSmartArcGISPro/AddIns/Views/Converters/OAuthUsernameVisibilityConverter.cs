using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static StreetSmartArcGISPro.Configuration.File.Login;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  class OAuthUsernameVisibilityConverter: IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var oAuthStatus = ((OAuthStatus)value);
      if (oAuthStatus == OAuthStatus.SignedIn)
      {
        return Visibility.Visible;
      }
      return Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }
  }
}
