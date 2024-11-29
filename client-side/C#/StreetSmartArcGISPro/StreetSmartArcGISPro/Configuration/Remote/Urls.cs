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

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Models;
using StreetSmartArcGISPro.Logging;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using static StreetSmartArcGISPro.Utilities.WebUtils;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;

namespace StreetSmartArcGISPro.Configuration.Remote
{
  /// <summary>
  /// This file contains default URLs
  /// </summary>
  public class Urls
  {
    #region Constants

    // ReSharper disable InconsistentNaming        
    private const string baseUrl = "https://atlasapi.cyclomedia.com/api";
    private const string _userInfoConfigurationRequest = "/configuration/userinfo";
    private const string _appConfigurationRequestPrefix = "/configuration";
    private const string spatialReferencesXml = "/spatialreferences/SpatialReferences.xml";
    private const string recordingRequest =
      "{0}?service=WFS&version=1.1.0&request=GetFeature&srsname={1}&featureid={2}&TYPENAME=atlas:Recording";
    // ReSharper restore InconsistentNaming

    #endregion

    #region Members

    protected FileConfiguration Configuration;
    private XmlSerializer xmlSerializer;

    private string _configId;

    #endregion

    #region Constructor

    public void CreateUrls()
    {
      Configuration = FileConfiguration.Instance;

      _configId = FetchConfigIdFromUserInfoConfiguration();
      ConfigServiceUrl = $"{BaseUrl}{_appConfigurationRequestPrefix}";
      ConfigurationUrl = $"{ConfigServiceUrl}{_appConfigurationRequestPrefix}/{_configId}";

      var globeSpotterConfig = FetchConfigurationByConfigId();
      RecordingServiceUrl = globeSpotterConfig?.ServicesConfiguration?.RecordingLocationService?.OnlineResource?.ResourceLink?.Replace("?", "") ?? string.Empty;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Base url
    /// </summary>
    private string BaseUrl => Configuration.UseDefaultConfigurationUrl
      ? baseUrl
      : Configuration.ConfigurationUrlLocation.Replace(_appConfigurationRequestPrefix, string.Empty);

    /// <summary>
    /// UserInfo Configuration URL
    /// </summary>
    protected string UserInfoConfigurationUrl => $"{BaseUrl}{_userInfoConfigurationRequest}";

    /// <summary>
    /// Configuration URL
    /// </summary>
    protected string ConfigurationUrl { get; private set; }

    /// <summary>
    /// Configuration URL
    /// </summary>
    public string ConfigServiceUrl { get; private set; }

    /// <summary>
    /// Spatialreferences URL
    /// </summary>
    protected string SpatialReferenceUrl => $"{BaseUrl.Replace(@"/api", string.Empty)}{spatialReferencesXml}";

    /// <summary>
    /// Recordings URL
    /// </summary>
    protected string RecordingServiceUrl { get; private set; }

    #endregion

    #region Functions

    protected string ImageIdUrl(string imageId, string epsgCode)
    {
      return string.Format(recordingRequest, RecordingServiceUrl, epsgCode, imageId);
    }

    private string FetchConfigIdFromUserInfoConfiguration()
    {
      string configId = string.Empty;
      try
      {
        // var userInfoConfigStream = await TestRequest(UserInfoConfigurationUrl, Login.Instance, ApiKey.Instance);
        var userInfoConfigStream =
            GetRequest(
                UserInfoConfigurationUrl,
                GetStreamCallback,
                TypeDownloadConfig.XML,
                Configuration,
                Login.Instance,
                Resource.ApiKey.Instance,
                true
                ) as Stream;

        if (userInfoConfigStream != null)
        {
          userInfoConfigStream.Position = 0;

          xmlSerializer = new XmlSerializer(typeof(UserInfo));
          var userInfoConfig = (UserInfo)xmlSerializer.Deserialize(userInfoConfigStream);
          configId = userInfoConfig.Packages.Where(w => w.ApiPackage).Select(s => s.ConfigId).FirstOrDefault();

          userInfoConfigStream.Close();
        }
      }
      catch (System.Exception ex)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (Urls.cs) (FetchConfigIdFromUserInfoConfiguration) error: {ex}");
      }
      return configId;
    }
    /*
            public async Task<Stream> TestRequest(string url, Login login, ApiKey apiKey)
            {
              Stream result = null;
              HttpClient client = new HttpClient();
              client.BaseAddress = new Uri(url);
              client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
              client.DefaultRequestHeaders.Accept.Clear();
              client.DefaultRequestHeaders.Add("ApiKey", apiKey.Value);
              var user = login.Username;
              var password = login.Password;
              var base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}"));
              client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64String);
              HttpResponseMessage response = await client.GetAsync("/");
              if (response.IsSuccessStatusCode == true)
              {
                result = response.Content.ReadAsStreamAsync().Result;
              }
              return result;
            }
    */

    private GlobeSpotterConfiguration FetchConfigurationByConfigId()
    {
      var globeSpotterConfig = new GlobeSpotterConfiguration();
      try
      {
        if (GetRequest(
                ConfigurationUrl,
                GetStreamCallback,
                TypeDownloadConfig.XML,
                Configuration,
                Login.Instance,
                Resource.ApiKey.Instance,
                true
                ) is Stream configStream)
        {
          configStream.Position = 0;

          xmlSerializer = new XmlSerializer(typeof(GlobeSpotterConfiguration));
          globeSpotterConfig = (GlobeSpotterConfiguration)xmlSerializer.Deserialize(configStream);

          configStream.Close();
        }
      }
      catch (System.Exception ex)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (Urls.cs) (FetchConfigurationByConfigId) error: {ex}");
      }
      return globeSpotterConfig;
    }

    #endregion
  }
}
