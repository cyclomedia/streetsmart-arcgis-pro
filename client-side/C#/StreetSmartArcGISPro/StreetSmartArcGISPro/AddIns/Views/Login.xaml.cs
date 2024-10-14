using ArcGIS.Desktop.Framework;
using StreetSmartArcGISPro.Configuration.File;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Resources;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for Login.xaml
  /// </summary>
  public partial class Login
  {
    ResourceManager resourceManager = Properties.Resources.ResourceManager;
    LanguageSettings language = LanguageSettings.Instance;
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

    private void OnLoginButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        var viewModel = (dynamic)DataContext;
        viewModel.Save();
        ShowNotification(viewModel.Credentials);
      }
    }
    private void OnLogoutButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        var temp = ((dynamic)DataContext).Password;
        ((dynamic)DataContext).Password = String.Empty;
        ((dynamic)DataContext).Save();
        ((dynamic)DataContext).Password = temp;
      }
    }


    public void ShowNotification(bool Credentials)
    {
      Notification notification = new Notification()
      {
        Title = resourceManager.GetString("LoginStatus", language.CultureInfo),
        Message = resourceManager.GetString(Credentials ? "LoginSuccessfully" : "LoginFailed", language.CultureInfo)
      };
      FrameworkApplication.AddNotification(notification);
    }
  }
}
