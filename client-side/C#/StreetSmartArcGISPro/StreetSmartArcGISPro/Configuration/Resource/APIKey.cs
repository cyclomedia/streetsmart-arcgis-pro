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

using StreetSmartArcGISPro.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EventLog = StreetSmartArcGISPro.Logging.EventLog;

namespace StreetSmartArcGISPro.Configuration.Resource
{
  [XmlRoot("APIKey")]
  public class ApiKey
  {
    #region Version Enum

    private enum SupportedArcGisProVersion
    {
      Unknown,
      V2_9,
      V3_0,
      V3_1,
      V3_2,
      V3_3
    }

    #endregion

    #region Members

    private static readonly XmlSerializer XmlApiKey;
    private static ApiKey _apiKey;
    private static readonly SupportedArcGisProVersion _currentArcGisProVersion;

    #endregion

    #region Constructors

    static ApiKey()
    {
      _currentArcGisProVersion = GetArcGisProVersion();

      XmlApiKey = new XmlSerializer(typeof (ApiKey));
    }

    #endregion

    #region Properties

    /// <summary>
    /// API Key Unsupported
    /// </summary>
    [XmlElement("APIKey_Unsupported")]
    public string APIKeyUnsupported { get; set; }

    /// <summary>
    /// API Key 2.9
    /// </summary>
    [XmlElement("APIKey_29")]
    public string ApiKey29 { get; set; }

    /// <summary>
    /// API Key 3.0
    /// </summary>
    [XmlElement("APIKey_30")]
    public string ApiKey30 { get; set; }

    /// <summary>
    /// API Key 3.1
    /// </summary>
    [XmlElement("APIKey_31")]
    public string ApiKey31 { get; set; }

    /// <summary>
    /// API Key 3.2
    /// </summary>
    [XmlElement("APIKey_32")]
    public string ApiKey32 { get; set; }

    /// <summary>
    /// API Key 3.3
    /// </summary>
    [XmlElement("APIKey_33")]
    public string ApiKey33 { get; set; }

    /// <summary>
    /// Versioned API Key
    /// </summary>
    [XmlIgnore()]
    public string Value
    {
      get
      {
        return _currentArcGisProVersion switch
        {
          SupportedArcGisProVersion.V2_9 => ApiKey29,
          SupportedArcGisProVersion.V3_0 => ApiKey30,
          SupportedArcGisProVersion.V3_1 => ApiKey31,
          SupportedArcGisProVersion.V3_2 => ApiKey32,
          SupportedArcGisProVersion.V3_3 => ApiKey33,
          _ => APIKeyUnsupported // Fallback if the version is not recognized
        };
      }
    }

    public static ApiKey Instance
    {
      get
      {
        if (_apiKey == null)
        {
          Load();
        }

        return _apiKey ??= new ApiKey ();
      }
    }

    #endregion

    #region Functions

    private static SupportedArcGisProVersion GetArcGisProVersion()
    {
      var entryAssembly = Assembly.GetEntryAssembly();

      if (entryAssembly == null)
      {
        EventLog.Write(EventLogLevel.Warning, $"Street Smart: (APIKey.cs) (GetVersion) Cannot read entry assembly.");

        return SupportedArcGisProVersion.Unknown;
      }

      string versionString;

      try
      {
        var version = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
        versionString = $"{version.ProductMajorPart}.{version.ProductMinorPart}";
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLogLevel.Warning, $"Street Smart: (APIKey.cs) (GetVersion) Error in version reading from entry assembly. Exception: {ex}.");

        return SupportedArcGisProVersion.Unknown;
      }

      return versionString switch
      {
        "2.9" => SupportedArcGisProVersion.V2_9,
        "3.0" => SupportedArcGisProVersion.V3_0,
        "3.1" => SupportedArcGisProVersion.V3_1,
        "3.2" => SupportedArcGisProVersion.V3_2,
        "3.3" => SupportedArcGisProVersion.V3_3,
        _ => SupportedArcGisProVersion.Unknown // Fallback if the version is not recognized
      };
    }

    private static void Load()
    {
      Assembly thisAssembly = Assembly.GetExecutingAssembly();
      

      const string manualPath = @"StreetSmartArcGISPro.Resources.APIKey.xml";
      Stream manualStream = thisAssembly.GetManifestResourceStream(manualPath);

      if (manualStream != null)
      {
        _apiKey = (ApiKey) XmlApiKey.Deserialize(manualStream);
        manualStream.Close();
      }
    }

    #endregion
  }
}
