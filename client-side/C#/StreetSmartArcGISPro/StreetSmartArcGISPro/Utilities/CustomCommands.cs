using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StreetSmartArcGISPro.Utilities
{
    public static class CustomCommands
    {
        public static readonly RoutedUICommand OAuthToBeVisibleCommand = new RoutedUICommand(
            "OpenMethod",
            "OpenMethod",
            typeof(CustomCommands),
            new InputGestureCollection() { new KeyGesture(Key.O, ModifierKeys.Control) }
        );
    }
}
