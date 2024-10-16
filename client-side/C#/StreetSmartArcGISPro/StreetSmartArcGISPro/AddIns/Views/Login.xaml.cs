﻿using System.Windows;
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

    private void OnCheckButtonClicked(object sender, RoutedEventArgs e)
    {
      if (DataContext != null)
      {
        ((dynamic)DataContext).Save();
      }
    }
  }
}
