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
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

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

    private bool _useDefaultSwfUrl;
    private string _swfLocation;

    private bool _useDefaultBaseUrl;
    private string _baseUrlLocation;

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
    public bool UseDefaultBaseUrl
    {
      get => _useDefaultBaseUrl;
      set
      {
        if (_useDefaultBaseUrl != value)
        {
          _useDefaultBaseUrl = value;
          OnPropertyChanged();
        }
      }
    }

    public string BaseUrlLocation
    {
      get => _baseUrlLocation;
      set
      {
        if (_baseUrlLocation != value)
        {
          _baseUrlLocation = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Swf url
    /// </summary>
    public bool UseDefaultSwfUrl
    {
      get => _useDefaultSwfUrl;
      set
      {
        if (_useDefaultSwfUrl != value)
        {
          _useDefaultSwfUrl = value;
          OnPropertyChanged();
        }
      }
    }

    public string SwfLocation
    {
      get => _swfLocation;
      set
      {
        if (_swfLocation != value)
        {
          _swfLocation = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Proxy service
    /// </summary>
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

        return _configuration ?? (_configuration = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Configuration.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlConfiguration.Serialize(streamFile, this);
      streamFile.Close();
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _configuration = (Configuration) XmlConfiguration.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Configuration Create()
    {
      var result = new Configuration
      {
        _useDefaultBaseUrl = true,
        _baseUrlLocation = string.Empty,
        _useDefaultSwfUrl = true,
        _swfLocation = string.Empty,
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
