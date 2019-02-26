/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018, CycloMedia, All rights reserved.
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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;

using FileLogin = StreetSmartArcGISPro.Configuration.File.Login;

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
    private readonly string _password;

    #endregion

    #region Constructors

    protected Login()
    {
      _login = FileLogin.Instance;
      _username = _login.Username;
      _password = _login.Password;
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

    public bool Credentials => _login.Credentials;

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      if (_login.Username != _username || _login.Password != _password)
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
