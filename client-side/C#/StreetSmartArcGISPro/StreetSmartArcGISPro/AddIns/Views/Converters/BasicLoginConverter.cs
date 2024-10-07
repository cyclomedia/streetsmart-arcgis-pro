using ArcGIS.Desktop.Internal.Catalog.PropertyPages.NetworkDataset;
using CefSharp.DevTools.CSS;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace StreetSmartArcGISPro.AddIns.Views.Converters
{
  public class BasicLoginConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is bool credentials && credentials)
      {
        return false;
      }
      return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
