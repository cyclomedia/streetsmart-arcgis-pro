using ArcGIS.Desktop.Internal.Mapping;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for Login.xaml
  /// </summary>
  public partial class Login
  {
    public Login()
    {
      InitializeComponent();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        ((dynamic)DataContext).Password = passwordBox.Password;
      }
    }

    private void OnPasswordLoaded(object sender, RoutedEventArgs e)
    {
      if (DataContext != null && sender is PasswordBox passwordBox)
      {
        passwordBox.Password = ((dynamic)DataContext).Password;
      }
    }

    private async void OnLoginButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        ((dynamic)DataContext).Save();

        await ShowNotification();
      }
    }
    private void OnLogoutButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        var temp = ((dynamic)DataContext).Password;
        ((dynamic)DataContext).Password = "";
        ((dynamic)DataContext).Save();
        ((dynamic)DataContext).Password = temp;
      }
    }


    public async Task ShowNotification()
    {
      NotificationMessage.Visibility = Visibility.Visible;
      await Task.Delay(3000);
      NotificationMessage.Visibility = Visibility.Collapsed;
    }
  }
}
