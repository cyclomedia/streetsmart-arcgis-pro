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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.DomElement;
using StreetSmartArcGISPro.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;

using FileLogin = StreetSmartArcGISPro.Configuration.File.Login;
using DockPaneStreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmart.WPF;
using StreetSmartArcGISPro.AddIns.Modules;
namespace StreetSmartArcGISPro.AddIns.Views
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login
    {
        public FileLogin fileLogin;
        private DockPaneStreetSmart streetSmart;

        #region Constructors

        public Login()
        {
            fileLogin = FileLogin.Instance;
            InitializeComponent();
            UpdateUI(fileLogin.IsOAuth, fileLogin.IsSignedInWithOAuth);
        }

        private void UpdateUI(bool IsOAuthChecked, bool IsSignedIn)
        {
            if (IsOAuthChecked && !IsSignedIn)
            {
                LoginElementsGroup.Visibility = Visibility.Collapsed;
                SignInButton.Visibility = Visibility.Visible;
                SignOutButton.Visibility = Visibility.Collapsed;
                OAuthCheckBox.Visibility = Visibility.Visible;
                OAuthCheckBox.IsChecked = true;
            }
            else if (IsOAuthChecked && IsSignedIn)
            {
                LoginElementsGroup.Visibility = Visibility.Collapsed;
                SignOutButton.Visibility = Visibility.Visible;
                SignInButton.Visibility = Visibility.Collapsed;
                OAuthCheckBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                LoginElementsGroup.Visibility = Visibility.Visible;
                SignInButton.Visibility = Visibility.Collapsed;
                SignOutButton.Visibility = Visibility.Collapsed;
                OAuthCheckBox.Visibility = Visibility.Visible;
                OAuthCheckBox.IsChecked = false;
            }
        }

        #endregion

        #region Event handlers

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
                ((dynamic) DataContext).Save();
            }
        }

        private void OAuthCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                ((dynamic)DataContext).IsOAuth = true;
                ((dynamic)DataContext).Save();
                UpdateUI(true, false);
            }
        }

        private void OAuthCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ((dynamic)DataContext).IsOAuth = false;
            ((dynamic)DataContext).Save();
            UpdateUI(false, false);
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                streetSmart = FrameworkApplication.DockPaneManager.Find("streetSmartArcGISPro_streetSmartDockPane") as DockPaneStreetSmart;
                if (streetSmart.Api != null)
                    await QueuedTask.Run(async () => await streetSmart.InitialApi());
                else
                    streetSmart = DockPaneStreetSmart.ActivateStreet();

                UpdateUI(true, true);

                ((dynamic)DataContext).IsSignedInWithOAuth = true;
                ((dynamic)DataContext).Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error signing in: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (streetSmart == null)
                    streetSmart = FrameworkApplication.DockPaneManager.Find("streetSmartArcGISPro_streetSmartDockPane") as DockPaneStreetSmart;

                streetSmart.Destroy();
                ((dynamic)DataContext).IsSignedInWithOAuth = false;
                ((dynamic)DataContext).Save();
                UpdateUI(true, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error signing out: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
    }
}
