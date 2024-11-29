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

using ArcGIS.Desktop.Framework.Contracts;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;
using FileLogin = StreetSmartArcGISPro.Configuration.File.Login;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Configuration : Page, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly FileConfiguration _configuration;
    private readonly FileLogin _login;

    private readonly bool _useDefaultConfigurationUrl;
    private readonly string _configurationUrlLocation;

    private readonly bool _useDefaultStreetSmartUrl;
    private readonly string _streetSmartLocation;

    private readonly bool _isSyncOfVisibilityEnabled;

    private readonly bool _useProxyServer;
    private readonly string _proxyAddress;
    private readonly int _proxyPort;
    private readonly bool _proxyBypassLocalAddresses;
    private readonly bool _proxyUseDefaultCredentials;
    private readonly string _proxyUsername;
    private readonly string _proxyPassword;
    private readonly string _proxyDomain;

    private readonly bool _useSentryLogging;

    #endregion

    #region Constructors

    protected Configuration()
    {
      _configuration = FileConfiguration.Instance;
      _login = FileLogin.Instance;

      _useDefaultConfigurationUrl = _configuration.UseDefaultConfigurationUrl;
      _configurationUrlLocation = _configuration.ConfigurationUrlLocation;

      _useDefaultStreetSmartUrl = _configuration.UseDefaultStreetSmartUrl;
      _streetSmartLocation = _configuration.StreetSmartLocation;

      _isSyncOfVisibilityEnabled = _configuration.IsSyncOfVisibilityEnabled;

      _useProxyServer = _configuration.UseProxyServer;
      _proxyAddress = _configuration.ProxyAddress;
      _proxyPort = _configuration.ProxyPort;
      _proxyBypassLocalAddresses = _configuration.ProxyBypassLocalAddresses;
      _proxyUseDefaultCredentials = _configuration.ProxyUseDefaultCredentials;
      _proxyUsername = _configuration.ProxyUsername;
      _proxyPassword = _configuration.ProxyPassword;
      _proxyDomain = _configuration.ProxyDomain;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Base url
    /// </summary>
    public bool UseDefaultConfigurationUrl
    {
      get => _configuration.UseDefaultConfigurationUrl;
      set
      {
        if (_configuration.UseDefaultConfigurationUrl != value)
        {
          IsModified = true;
          _configuration.UseDefaultConfigurationUrl = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string ConfigurationUrlLocation
    {
      get => _configuration.ConfigurationUrlLocation;
      set
      {
        if (_configuration.ConfigurationUrlLocation != value)
        {
          IsModified = true;
          _configuration.ConfigurationUrlLocation = value;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Swf url
    /// </summary>
    public bool UseDefaultStreetSmartUrl
    {
      get => _configuration.UseDefaultStreetSmartUrl;
      set
      {
        if (_configuration.UseDefaultStreetSmartUrl != value)
        {
          IsModified = true;
          _configuration.UseDefaultStreetSmartUrl = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool IsSyncOfVisibilityEnabled
    {
      get => _configuration.IsSyncOfVisibilityEnabled;
      set
      {
        if (_configuration.IsSyncOfVisibilityEnabled != value)
        {
          IsModified = true;
          _configuration.IsSyncOfVisibilityEnabled = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string StreetSmartLocation
    {
      get => _configuration.StreetSmartLocation;
      set
      {
        if (_configuration.StreetSmartLocation != value)
        {
          IsModified = true;
          _configuration.StreetSmartLocation = value;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Proxy service
    /// </summary>
    public bool UseProxyServer
    {
      get => _configuration.UseProxyServer;
      set
      {
        if (_configuration.UseProxyServer != value)
        {
          IsModified = true;
          _configuration.UseProxyServer = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string ProxyAddress
    {
      get => _configuration.ProxyAddress;
      set
      {
        if (_configuration.ProxyAddress != value)
        {
          IsModified = true;
          _configuration.ProxyAddress = value;
          NotifyPropertyChanged();
        }
      }
    }

    public int ProxyPort
    {
      get => _configuration.ProxyPort;
      set
      {
        if (_configuration.ProxyPort != value)
        {
          IsModified = true;
          _configuration.ProxyPort = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool ProxyBypassLocalAddresses
    {
      get => _configuration.ProxyBypassLocalAddresses;
      set
      {
        if (_configuration.ProxyBypassLocalAddresses != value)
        {
          IsModified = true;
          _configuration.ProxyBypassLocalAddresses = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool ProxyUseDefaultCredentials
    {
      get => _configuration.ProxyUseDefaultCredentials;
      set
      {
        if (_configuration.ProxyUseDefaultCredentials != value)
        {
          IsModified = true;
          _configuration.ProxyUseDefaultCredentials = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string ProxyUsername
    {
      get => _configuration.ProxyUsername;
      set
      {
        if (_configuration.ProxyUsername != value)
        {
          IsModified = true;
          _configuration.ProxyUsername = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string ProxyPassword
    {
      get => _configuration.ProxyPassword;
      set
      {
        if (_configuration.ProxyPassword != value)
        {
          IsModified = true;
          _configuration.ProxyPassword = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string ProxyDomain
    {
      get => _configuration.ProxyDomain;
      set
      {
        if (_configuration.ProxyDomain != value)
        {
          IsModified = true;
          _configuration.ProxyDomain = value;
          NotifyPropertyChanged();
        }
      }
    }

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      Save();
      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _configuration.UseDefaultConfigurationUrl = _useDefaultConfigurationUrl;
      _configuration.ConfigurationUrlLocation = _configurationUrlLocation;

      _configuration.UseDefaultStreetSmartUrl = _useDefaultStreetSmartUrl;
      _configuration.StreetSmartLocation = _streetSmartLocation;


      _configuration.IsSyncOfVisibilityEnabled = _isSyncOfVisibilityEnabled;

      _configuration.UseProxyServer = _useProxyServer;
      _configuration.ProxyAddress = _proxyAddress;
      _configuration.ProxyPort = _proxyPort;
      _configuration.ProxyBypassLocalAddresses = _proxyBypassLocalAddresses;
      _configuration.ProxyUseDefaultCredentials = _proxyUseDefaultCredentials;
      _configuration.ProxyUsername = _proxyUsername;
      _configuration.ProxyPassword = _proxyPassword;
      _configuration.ProxyDomain = _proxyDomain;

      Save();
      return base.CancelAsync();
    }

    #endregion

    #region Functions

    protected override void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void Save()
    {
      _configuration.Save();
      _login.Check();
    }

    #endregion
  }
}
