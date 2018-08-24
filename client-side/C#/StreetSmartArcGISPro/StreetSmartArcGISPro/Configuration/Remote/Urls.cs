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
    private const string baseUrl = "https://atlas.cyclomedia.com";
    private const string apiUrl = "https://globeSpotter.cyclomedia.com/v31/api";
    private const string configurationRequest = "{0}/configuration/configuration/API";
    private const string apiSwf = "/viewer_api.swf";
    private const string spatialReferencesXml = "/config/srs/globespotterspatialreferences.xml";
    private const string recordingRequest =
      "{0}?service=WFS&version=1.1.0&request=GetFeature&srsname={1}&featureid={2}&TYPENAME=atlas:Recording";
    // ReSharper restore InconsistentNaming

    #endregion

    #region Members

    protected readonly FileConfiguration Configuration;

    #endregion

    #region Constructor

    protected Urls()
    {
      Configuration = FileConfiguration.Instance;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Base url
    /// </summary>
    private string BaseUrl => Configuration.UseDefaultBaseUrl ? baseUrl : Configuration.BaseUrlLocation;

    /// <summary>
    /// Configuration URL
    /// </summary>
    protected string ConfigurationUrl => string.Format(configurationRequest, BaseUrl);

    /// <summary>
    /// Spatialreferences URL
    /// </summary>
    protected string SpatialReferenceUrl => Configuration.UseDefaultSwfUrl
      ? string.Concat(apiUrl, spatialReferencesXml)
      : Configuration.SwfLocation.Replace(apiSwf, spatialReferencesXml);

    /// <summary>
    /// Recordings URL
    /// </summary>
    protected string RecordingServiceUrl => $"{BaseUrl}/recordings/wfs";

    #endregion

    #region Functions

    protected string ImageIdUrl(string imageId, string epsgCode)
    {
      return string.Format(recordingRequest, RecordingServiceUrl, epsgCode, imageId);
    }

    #endregion
  }
}
