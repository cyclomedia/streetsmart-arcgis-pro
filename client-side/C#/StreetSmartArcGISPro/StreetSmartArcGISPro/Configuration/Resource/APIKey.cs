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

using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Resource
{
  [XmlRoot("APIKey")]
  public class ApiKey
  {
    #region Members

    private static readonly XmlSerializer XmlApiKey;
    private static ApiKey _apiKey;

    #endregion

    #region Constructors

    static ApiKey()
    {
      XmlApiKey = new XmlSerializer(typeof (ApiKey));
    }

    #endregion

    #region Properties

    /// <summary>
    /// API Key
    /// </summary>
    [XmlElement("APIKey")]
    public string Value { get; set; }

    public static ApiKey Instance
    {
      get
      {
        if (_apiKey == null)
        {
          Load();
        }

        return _apiKey ?? (_apiKey = new ApiKey {Value = string.Empty});
      }
    }

    #endregion

    #region Functions

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
