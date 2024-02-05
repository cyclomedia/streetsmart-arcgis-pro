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
using StreetSmartArcGISPro.Utilities;
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
        private const string _appConfigurationRequestPrefix = "/configuration/configuration/";
        private const string spatialReferencesXml = "/spatialreferences/SpatialReferences.xml";
        private const string recordingRequest =
          "{0}?service=WFS&version=1.1.0&request=GetFeature&srsname={1}&featureid={2}&TYPENAME=atlas:Recording";
        // ReSharper restore InconsistentNaming

        #endregion

        #region Members

        protected readonly FileConfiguration Configuration;
        private XmlSerializer xmlSerializer;

        private readonly string _configId;

        #endregion

        #region Constructor

        protected Urls()
        {
            Configuration = FileConfiguration.Instance;

            _configId = FetchConfigIdFromUserInfoConfiguration();
            ConfigurationUrl = $"{BaseUrl}{_appConfigurationRequestPrefix}{_configId}";

            var globeSpotterConfig = FetchConfigurationByConfigId();
            RecordingServiceUrl = globeSpotterConfig.ServicesConfiguration.RecordingLocationService.OnlineResource.ResourceLink.Replace("?", "");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Base url
        /// </summary>
        private string BaseUrl => Configuration.UseDefaultConfigurationUrl
          ? baseUrl
          : Configuration.ConfigurationUrlLocation.Replace(@"/configuration", string.Empty);

        /// <summary>
        /// UserInfo Configuration URL
        /// </summary>
        protected string UserInfoConfigurationUrl => $"{BaseUrl}{_userInfoConfigurationRequest}";

        /// <summary>
        /// Configuration URL
        /// </summary>
        protected string ConfigurationUrl { get; set; }

        /// <summary>
        /// Spatialreferences URL
        /// </summary>
        protected string SpatialReferenceUrl => $"{BaseUrl.Replace(@"/api", string.Empty)}{spatialReferencesXml}";

        /// <summary>
        /// Recordings URL
        /// </summary>
        protected string RecordingServiceUrl { get; set; }

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
                    var userInfoConfig = (UserInfo) xmlSerializer.Deserialize(userInfoConfigStream);
                    configId = userInfoConfig.Packages.Where(w => w.ApiPackage).Select(s => s.ConfigId).FirstOrDefault();

                    userInfoConfigStream.Close();
                }
            }
            catch (System.Exception)
            {

            }
            return configId;
        }

        private GlobeSpotterConfiguration FetchConfigurationByConfigId()
        {
            var globeSpotterConfig = new GlobeSpotterConfiguration();
            try
            {               
                var configStream =
                    GetRequest(
                        ConfigurationUrl,
                        GetStreamCallback,
                        TypeDownloadConfig.XML,
                        Configuration,
                        Login.Instance,
                        Resource.ApiKey.Instance,
                        true
                        ) as Stream;

                if (configStream != null)
                {
                    configStream.Position = 0;

                    xmlSerializer = new XmlSerializer(typeof(GlobeSpotterConfiguration));
                    globeSpotterConfig = (GlobeSpotterConfiguration)xmlSerializer.Deserialize(configStream);

                    configStream.Close();
                }
            }
            catch (System.Exception)
            {

            }
            return globeSpotterConfig;
        }

        #endregion
    }
}
