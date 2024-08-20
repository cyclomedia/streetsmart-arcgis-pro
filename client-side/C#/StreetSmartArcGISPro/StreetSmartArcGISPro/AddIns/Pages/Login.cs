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

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Utilities;
using FileLogin = StreetSmartArcGISPro.Configuration.File.Login;
using DockPaneStreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using static StreetSmartArcGISPro.Configuration.File.Login;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Login : Page, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly FileLogin _login;

    private bool _isOAuthChecked;
    private readonly string _username;
    private readonly string _password;
    private readonly bool _isOAuth;
    private bool _isSignOutVisible;
    private Visibility _isOAuthVisible;
    private Visibility _isOAuthButtonsVisible;
    private bool _isSignInVisible;
    private bool _isOAuthEnabled;
    private Visibility _areLoginElementsVisible;

    public ICommand SignInCommand { get; }
    public ICommand SignOutCommand { get; }

    public Visibility IsOAuthVisible
    {
      get => _isOAuthVisible;
      private set
      {
        _isOAuthVisible = value;
        NotifyPropertyChanged();
      }
    }
    public Visibility IsOAuthButtonsVisible
    {
      get => _isOAuthButtonsVisible;
      private set
      {
        _isOAuthButtonsVisible = value;
        NotifyPropertyChanged();
      }
    }
    public bool IsSignInVisible
    {
      get => _isSignInVisible;
      private set
      {
        _isSignInVisible = value;
        NotifyPropertyChanged();
      }
    }
    public bool IsOAuthEnabled
    {
      get => _isOAuthEnabled;
      private set
      {
        _isOAuthEnabled = value;
        NotifyPropertyChanged();
      }
    }
    public bool IsOAuthChecked
    {
      get => _login.IsOAuthChecked;
      private set
      {
        IsModified = true;
        _login.IsOAuthChecked = value;
        OnStartUpSetSettingsPageLogin();
        NotifyPropertyChanged();
      }
    }
    public bool IsSignOutVisible
    {
      get => _isSignOutVisible;
      private set
      {
        _isSignOutVisible = value;
        NotifyPropertyChanged();
      }
    }
    public Visibility AreLoginElementsVisible
    {
      get => _areLoginElementsVisible;
      private set
      {
        _areLoginElementsVisible = value;
        NotifyPropertyChanged();
      }
    }
    #endregion

    #region Constructors

    protected Login()
    {
      _login = FileLogin.Instance;
      _username = _login.Username;
      _password = _login.Password;
      _isOAuth = _login.IsOAuth;
      _isOAuthChecked = _login.IsOAuthChecked;

      _login.PropertyChanged += OnLoginPropertyChanged;


      SignInCommand = new RelayCommand(async () => await SignInOAuth());
      SignOutCommand = new RelayCommand(async () => await SignOutOAuth());

      OnStartUpSetSettingsPageLogin();
    }


    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLog.EventType.Information, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged)");

      Application.Current.Dispatcher.Invoke(() =>
      {
        switch (args.PropertyName)
        {
          case "OAuthAuthenticationStatus":

            IsModified = true;

            switch (_login.OAuthAuthenticationStatus)
            {
              case FileLogin.OAuthStatus.SigningIn:
                IsOAuthButtonsVisible = Visibility.Visible;
                IsOAuthVisible = Visibility.Visible;
                IsOAuthEnabled = false;
                IsSignInVisible = false;
                IsSignOutVisible = false;
                AreLoginElementsVisible = Visibility.Collapsed;
                break;
              case FileLogin.OAuthStatus.SignedIn:
                IsOAuthButtonsVisible = Visibility.Visible;
                IsOAuthVisible = Visibility.Visible;
                IsOAuthEnabled = false;
                IsSignInVisible = false;
                IsSignOutVisible = true;
                AreLoginElementsVisible = Visibility.Collapsed;
                _login.Check();
                break;
              case FileLogin.OAuthStatus.SigningOut:
                IsOAuthButtonsVisible = Visibility.Visible;
                IsOAuthEnabled = false;
                IsOAuthVisible = Visibility.Collapsed;
                IsSignInVisible = false;
                IsSignOutVisible = false;
                AreLoginElementsVisible = Visibility.Collapsed;
                break;
              case FileLogin.OAuthStatus.SignedOut:
                IsOAuthButtonsVisible = Visibility.Visible;
                IsOAuthEnabled = true;
                IsOAuthVisible = Visibility.Visible;
                IsSignInVisible = true;
                IsSignOutVisible = false;
                AreLoginElementsVisible = Visibility.Collapsed;
                _login.Check();
                break;
            }
            NotifyPropertyChanged();
            break;
        }
      });
    }


    private async Task SignOutOAuth()
    {
      _login.OAuthAuthenticationStatus = OAuthStatus.SigningOut;

      try
      {
        DockPaneStreetSmart streetSmart = DockPaneStreetSmart.Current;
        await streetSmart.Destroy(true);
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLog.EventType.Error, $"Street Smart: (Login.cs) (SignOutOAuth) {ex}");
      }

      _login.OAuthAuthenticationStatus = OAuthStatus.SignedOut;
    }

    private async Task SignInOAuth()
    {
      _login.OAuthAuthenticationStatus = OAuthStatus.SigningIn;

      try
      {
        DockPaneStreetSmart streetSmart = DockPaneStreetSmart.Current;
        if (streetSmart.Api != null)
        {
          await streetSmart.Destroy(false);
          _login.IsFromSettingsPage = true;
          await QueuedTask.Run(async () => await streetSmart.InitialApi());
        }
        else
        {
          _login.IsFromSettingsPage = true;
          streetSmart = DockPaneStreetSmart.ActivateStreetSmart();
        }
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLog.EventType.Error, $"Street Smart: (Login.cs) (SignInOAuth) {ex}");

        _login.OAuthAuthenticationStatus = OAuthStatus.SignedOut;
      }
    }

    #endregion

    #region Properties

    public string Username
    {
      get => _login.Username;
      set
      {
        if (_login.Username != value)
        {
          IsModified = true;
          _login.Username = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string Password
    {
      get => _login.Password;
      set
      {
        if (_login.Password != value)
        {
          IsModified = true;
          _login.Password = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool IsOAuth
    {
      get => _login.IsOAuth;
      set
      {
        /* if (_configuration != null)
             if (_configuration.UseDefaultStreetSmartUrl)
             {
                 if (!_login.IsOAuth)
                 {
                     IsModified = true;
                     _login.IsOAuth = false;
                     NotifyPropertyChanged();
                 }
             }*/
        if ((_login.IsOAuth != value))
        {
          IsModified = true;
          _login.IsOAuth = value;
          if (value)
          {
            IsOAuthButtonsVisible = Visibility.Visible;
            IsOAuthVisible = Visibility.Visible;
            IsOAuthEnabled = true;
            IsSignInVisible = true;
            IsSignOutVisible = false;
            AreLoginElementsVisible = Visibility.Collapsed;
          }
          if (!value)
          {
            IsOAuthButtonsVisible = Visibility.Collapsed;
            IsOAuthVisible = Visibility.Visible;
            IsOAuthEnabled = true;
            IsSignInVisible = false;
            IsSignOutVisible = false;
            AreLoginElementsVisible = Visibility.Visible;
          }

          NotifyPropertyChanged();
        }
      }
    }
    public void OnStartUpSetSettingsPageLogin()
    {
      if (IsOAuthChecked)
        if (_login.IsOAuth && _login.OAuthAuthenticationStatus != FileLogin.OAuthStatus.SignedIn)
        {
          IsOAuthButtonsVisible = Visibility.Visible;
          AreLoginElementsVisible = Visibility.Collapsed;
          IsOAuthEnabled = true;
          IsSignInVisible = true;
          IsSignOutVisible = false;
          IsOAuthVisible = Visibility.Visible;

        }
        else if (_login.IsOAuth && _login.OAuthAuthenticationStatus == FileLogin.OAuthStatus.SignedIn)
        {
          IsOAuthButtonsVisible = Visibility.Visible;
          AreLoginElementsVisible = Visibility.Collapsed;
          IsOAuthEnabled = false;
          IsSignOutVisible = true;
          IsSignInVisible = false;
          IsOAuthVisible = Visibility.Visible;
        }
        else
        {
          IsOAuthButtonsVisible = Visibility.Collapsed;
          AreLoginElementsVisible = Visibility.Visible;
          IsSignInVisible = false;
          IsSignOutVisible = false;
          IsOAuthEnabled = true;
          IsOAuthVisible = Visibility.Visible;
        }
      else
      {
        IsOAuthButtonsVisible = Visibility.Collapsed;
        AreLoginElementsVisible = Visibility.Visible;
        IsSignInVisible = false;
        IsSignOutVisible = false;
        IsOAuthEnabled = true;
        IsOAuthVisible = Visibility.Collapsed;
      }
    }
    public bool Credentials => _login.Credentials;

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      if (_login.Username != _username || _login.Password != _password || _login.IsOAuth != _isOAuth || _login.IsOAuthChecked != _isOAuthChecked)
      {
        Save();
      }

      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _login.Username = _username;
      _login.Password = _password;
      _login.IsOAuth = _isOAuth;
      _login.IsOAuthChecked = _isOAuthChecked;

      Save();

      return base.CancelAsync();
    }

    #endregion

    #region Functions

    protected override void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Save()
    {
      _login.Save();
      // ReSharper disable once ExplicitCallerInfoArgument
      NotifyPropertyChanged("Credentials");
    }

    public void LoginPage_OAuth()
    {
      if (IsOAuthChecked)
      {
        IsOAuthChecked = false;
      }
      else
      {
        IsOAuthChecked = true;
      }
    }
    #endregion
  }
}
