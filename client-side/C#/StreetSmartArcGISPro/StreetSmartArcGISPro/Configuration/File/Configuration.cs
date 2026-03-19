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

using StreetSmartArcGISPro.Utilities;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("Configuration")]
  public class Configuration : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static readonly XmlSerializer XmlConfiguration;
    private static Configuration _configuration;

    private bool _useDefaultStreetSmartUrl;
    private string _streetSmartLocation;

    private bool _isSyncOfVisibilityEnabled = true;

    private bool _useDefaultConfigurationUrl;
    private string _configurationUrlLocation;

    private bool _allowMultipleInstances;

    #endregion

    #region Constructors

    static Configuration()
    {
      XmlConfiguration = new XmlSerializer(typeof(Configuration));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Base url
    /// </summary>
    public bool UseDefaultConfigurationUrl
    {
      get => _useDefaultConfigurationUrl;
      set
      {
        if (_useDefaultConfigurationUrl != value)
        {
          _useDefaultConfigurationUrl = value;
          OnPropertyChanged();
        }
      }
    }

    public string ConfigurationUrlLocation
    {
      get => _configurationUrlLocation;
      set
      {
        if (_configurationUrlLocation != value)
        {
          _configurationUrlLocation = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Swf url
    /// </summary>
    public bool UseDefaultStreetSmartUrl
    {
      get => _useDefaultStreetSmartUrl;
      set
      {
        if (_useDefaultStreetSmartUrl != value)
        {
          _useDefaultStreetSmartUrl = value;
          OnPropertyChanged();
        }
      }
    }

    public bool IsSyncOfVisibilityEnabled
    {
      get => _isSyncOfVisibilityEnabled;
      set
      {
        if (_isSyncOfVisibilityEnabled != value)
        {
          _isSyncOfVisibilityEnabled = value;
          OnPropertyChanged();
        }
      }
    }

    public string StreetSmartLocation
    {
      get => _streetSmartLocation;
      set
      {
        if (_streetSmartLocation != value)
        {
          _streetSmartLocation = value;
          OnPropertyChanged();
        }
      }
    }

    /// When enabled, uses a separate browser cache per instance to allow multiple
    /// ArcGIS Pro projects simultaneously. SSO users will need to re-authenticate
    /// in each instance. When disabled, a single shared cache preserves SSO cookies.
     public bool AllowMultipleInstances
    {
      get => _allowMultipleInstances;
      set
      {
        if (_allowMultipleInstances != value)
        {
          _allowMultipleInstances = value;
          OnPropertyChanged();
        }
      }
    }

    
    /// Proxy service
   
    public bool UseProxyServer { get; set; }

    public string ProxyAddress { get; set; }

    public int ProxyPort { get; set; }

    public bool ProxyBypassLocalAddresses { get; set; }

    public bool ProxyUseDefaultCredentials { get; set; }

    public string ProxyUsername { get; set; }

    public string ProxyPassword { get; set; }

    public string ProxyDomain { get; set; }


    public static Configuration Instance
    {
      get
      {
        if (_configuration == null)
        {
          Load();
        }

        return _configuration ??= Create();
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Configuration.xml");

    #endregion

    #region Functions

    public void Save()
    {
      OnPropertyChanged();
      FileUtils.SafeSerializeToFile(FileName, XmlConfiguration, this);
    }

    private static void Load()
    {
      _configuration = FileUtils.SafeDeserializeFromFile<Configuration>(FileName, XmlConfiguration);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Configuration Create()
    {
      var result = new Configuration
      {
        _useDefaultConfigurationUrl = true,
        _configurationUrlLocation = string.Empty,
        _useDefaultStreetSmartUrl = true,
        _isSyncOfVisibilityEnabled = true,
        _allowMultipleInstances = false,
        _streetSmartLocation = string.Empty,
        UseProxyServer = false,
        ProxyAddress = string.Empty,
        ProxyPort = 80,
        ProxyBypassLocalAddresses = false,
        ProxyUseDefaultCredentials = true,
        ProxyUsername = string.Empty,
        ProxyPassword = string.Empty,
        ProxyDomain = string.Empty
      };

      result.Save();
      return result;
    }

    #endregion
  }
}
