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

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Login : Page
  {
    #region Members

    private readonly FileLogin _login;

    private readonly string _username;
    private readonly string _password;
    private readonly bool _isOAuth;

    public ICommand SignInCommand { get; }
    public ICommand SignOutCommand { get; }

    #endregion

    #region Constructors

    protected Login()
    {
      _login = FileLogin.Instance;
      _username = _login.Username;
      _password = _login.Password;
      _isOAuth = _login.IsOAuth;

      _login.PropertyChanged += OnLoginPropertyChanged;

      SignInCommand = new RelayCommand(async () => await SignInOAuth());
      SignOutCommand = new RelayCommand(async () => await SignOutOAuth());
    }

    private void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLog.EventType.Information, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged) ({args.PropertyName})");

      Application.Current.Dispatcher.Invoke(() => //TODO: check if we can do this better
      {
        switch (args.PropertyName)
        {
          case nameof(FileLogin.Credentials):

            EventLog.Write(EventLog.EventType.Debug, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged) (Credentials) {_login.Credentials}");

            break;
          case nameof(FileLogin.OAuthAuthenticationStatus):

            EventLog.Write(EventLog.EventType.Debug, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged) (OAuthAuthenticationStatus) {_login.OAuthAuthenticationStatus}");

            IsModified = true;

            switch (_login.OAuthAuthenticationStatus)
            {
              case FileLogin.OAuthStatus.SignedIn:
                _login.Check();
                break;
              case FileLogin.OAuthStatus.SignedOut:
                _login.Clear();
                break;
            }

            NotifyPropertyChanged("OAuthAuthenticationStatus");
            NotifyPropertyChanged("IsOAuth");

            break;
          case nameof(FileLogin.OAuthUsername):

            EventLog.Write(EventLog.EventType.Debug, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged) (OAuthUsername) {_login.OAuthUsername}");
            NotifyPropertyChanged("Username");

            break;
        }
      });
    }


    private async Task SignOutOAuth()
    {
      _login.OAuthAuthenticationStatus = OAuthStatus.SigningOut;
      _login.OAuthUsername = string.Empty;
      _login.Bearer = null;

      try
      {
        await DockPaneStreetSmart.Current.SignOutOAuth();
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLog.EventType.Error, $"Street Smart: (Login.cs) (SignOutOAuth) {ex}");
      }

      _login.IsOAuth = false;
      _login.Credentials = false;
      NotifyPropertyChanged("Credentials");
      _login.OAuthAuthenticationStatus = OAuthStatus.SignedOut;
      
    }

    private async Task SignInOAuth()
    {
      _login.IsOAuth = true;
      _login.OAuthAuthenticationStatus = OAuthStatus.SigningIn;

      try
      {
        _login.IsFromSettingsPage = true;
        await DockPaneStreetSmart.Current.SignInOAuth();
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLog.EventType.Error, $"Street Smart: (Login.cs) (SignInOAuth) {ex}");

        _login.OAuthAuthenticationStatus = OAuthStatus.SignedOut;
        _login.IsOAuth = false;
      }
    }

    #endregion

    #region Properties

    public string Username
    {
      get => _login.IsOAuth ? _login.OAuthUsername : _login.Username;
      set
      {
        if (_login.IsOAuth)
        {
          if (_login.OAuthUsername != value)
          {
            IsModified = true;
            _login.OAuthUsername = value;
            NotifyPropertyChanged();
          }
        }
        else
        {
          if (_login.Username != value)
          {
            IsModified = true;
            _login.Username = value;
            NotifyPropertyChanged();
          }
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

          NotifyPropertyChanged();
          NotifyPropertyChanged("Username");
          NotifyPropertyChanged("OAuthAuthenticationStatus");
        }
      }
    }

    public OAuthStatus OAuthAuthenticationStatus
    {
      get => _login.OAuthAuthenticationStatus;
      set
      {
        if (_login.OAuthAuthenticationStatus != value)
        {
          _login.OAuthAuthenticationStatus = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool Credentials => _login.Credentials;

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      if (_login.Username != _username || _login.Password != _password || _login.IsOAuth != _isOAuth)
      {
        Save();
      }

      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _login.Username = _username;
      _login.Password = _password;

      Save();

      return base.CancelAsync();
    }

    #endregion

    #region Functions

    public void Save()
    {
      _login.Save();
      NotifyPropertyChanged("Credentials");
      NotifyPropertyChanged("Username");
    }

    #endregion
  }
}
