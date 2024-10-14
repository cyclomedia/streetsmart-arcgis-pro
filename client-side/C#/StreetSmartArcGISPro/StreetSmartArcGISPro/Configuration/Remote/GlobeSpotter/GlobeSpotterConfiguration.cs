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
using System.IO;
using System.Net;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter
{
  [XmlType(AnonymousType = true, Namespace = "https://www.globespotter.com/gsc")]
  [XmlRoot(Namespace = "https://www.globespotter.com/gsc", IsNullable = false)]
  public class GlobeSpotterConfiguration
  {
    #region Members

    private static readonly XmlSerializer XmlstreetSmartconfiguration;
    private static readonly Web Web;

    private static GlobeSpotterConfiguration _globeSpotterConfiguration;

    #endregion

    #region Constructors

    static GlobeSpotterConfiguration()
    {
      XmlstreetSmartconfiguration = new XmlSerializer(typeof(GlobeSpotterConfiguration));
      Web = Web.Instance;
    }

    #endregion

    #region Properties

    /// <summary>
    /// ApplicationConfiguration
    /// </summary>
    public ApplicationConfiguration ApplicationConfiguration { get; set; }

    [XmlIgnore]
    public bool LoginFailed { get; private set; }

    [XmlIgnore]
    public bool LoadException { get; private set; }

    [XmlIgnore]
    public Exception Exception { get; private set; }

    [XmlIgnore]
    public bool Credentials => ApplicationConfiguration != null && ApplicationConfiguration.Functionalities.Length >= 1;

    public static bool MeasureSmartClick => Instance.CheckFunctionality("MeasureSmartClick");

    public static bool MeasurePoint => Instance.CheckFunctionality("MeasurePoint");

    public static bool MeasureLine => Instance.CheckFunctionality("MeasureLine");

    public static bool MeasurePolygon => Instance.CheckFunctionality("MeasurePolygon");

    public static bool AddLayerWfs => Instance.CheckFunctionality("AddLayerWFS");

    public static bool MeasurePermissions => MeasurePoint || MeasureLine || MeasurePolygon || MeasureSmartClick;

    public static GlobeSpotterConfiguration Instance
    {
      get
      {
        bool loadException = false;
        bool loginFailed = false;
        Exception exception = null;

        if (_globeSpotterConfiguration == null)
        {
          try
          {
            Load();
          }
          catch (WebException ex)
          {
            exception = ex;

            if (ex.Response is HttpWebResponse responce)
            {
              if (responce.StatusCode == HttpStatusCode.Unauthorized ||
                  responce.StatusCode == HttpStatusCode.Forbidden ||
                  responce.StatusCode == HttpStatusCode.NotFound)
              {
                loginFailed = true;
              }
              else
              {
                loadException = true;
              }
            }
            else
            {
              loadException = true;
            }
          }
          catch (Exception ex)
          {
            exception = ex;
            loadException = true;
          }
        }

        return _globeSpotterConfiguration ?? (_globeSpotterConfiguration = new GlobeSpotterConfiguration
        {
          LoadException = loadException,
          LoginFailed = loginFailed,
          Exception = exception
        });
      }
    }

    #endregion

    #region Functions

    private bool CheckFunctionality(string name)
    {
      return ApplicationConfiguration?.GetFunctionality(name) != null;
    }

    public static GlobeSpotterConfiguration Load()
    {
      try
      {
        Web.CreateUrls();
        Stream streetSmartConf = Web.GlobeSpotterConfiguration();

        if (streetSmartConf != null)
        {
          streetSmartConf.Position = 0;
          _globeSpotterConfiguration = (GlobeSpotterConfiguration)XmlstreetSmartconfiguration.Deserialize(streetSmartConf);
          streetSmartConf.Close();
        }
      }
      catch (Exception e)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (GlobeSpotter.cs) (Instance) error: {e}");
      }

      return _globeSpotterConfiguration;
    }

    public static void Delete()
    {
      _globeSpotterConfiguration = null;
    }

    public static bool CheckCredentials()
    {
      Delete();
      return Instance.Credentials;
    }

    #endregion
  }
}
