﻿/*
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

    private readonly string _username;
    private readonly string _oAuthUsername;
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
      _oAuthUsername = _login.OAuthUsername;
      _password = _login.Password;
      _isOAuth = _login.IsOAuth;

      _login.PropertyChanged += OnLoginPropertyChanged;

      SignInCommand = new RelayCommand(async () => await SignInOAuth());
      SignOutCommand = new RelayCommand(async () => await SignOutOAuth());
    }

    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLog.EventType.Information, $"Street Smart: (Pages.Login.cs) (OnLoginPropertyChanged)");

      Application.Current.Dispatcher.Invoke(() => //TODO: check if we actually need this here at all
      {
        switch (args.PropertyName)
        {
          case "OAuthAuthenticationStatus":

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

            break;
          case "OAuthUsername":

            NotifyPropertyChanged("Username");

            break;
        }
      });
    }


    private async Task SignOutOAuth()
    {
      _login.OAuthAuthenticationStatus = OAuthStatus.SigningOut;
      _login.OAuthUsername = string.Empty;

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
      if (_login.Username != _username || _login.Password != _password || _login.IsOAuth != _isOAuth || _login.OAuthUsername != _oAuthUsername)
      {
        Save();
      }

      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _login.Username = _username;
      _login.OAuthUsername = _oAuthUsername;
      _login.Password = _password;
      _login.IsOAuth = _isOAuth;

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

    #endregion
  }
}
