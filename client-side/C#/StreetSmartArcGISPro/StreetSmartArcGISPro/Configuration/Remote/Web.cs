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

using ArcGIS.Core.Geometry;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using static StreetSmartArcGISPro.Utilities.WebUtils;

namespace StreetSmartArcGISPro.Configuration.Remote
{
  public class Web : Urls
  {
    #region Constants

    private const int BufferImageLengthService = 2048;
    private const int DefaultConnectionLimit = 5;

    #endregion

    #region Members

    private static Web _web;

    private readonly Login _login;
    private readonly ApiKey _apiKey;
    private readonly CultureInfo _ci;

    #endregion

    #region Properties

    public static Web Instance => _web ??= new Web();

    #endregion

    #region Constructor

    private Web()
    {
      _login = Login.Instance;
      _apiKey = ApiKey.Instance;
      _ci = CultureInfo.InvariantCulture;
      ServicePointManager.DefaultConnectionLimit = DefaultConnectionLimit;
      CreateUrls();
    }

    #endregion

    #region Interface functions

    public Stream SpatialReferences()
    {
      return GetRequest(SpatialReferenceUrl, GetStreamCallback, TypeDownloadConfig.XML, Configuration, _login, _apiKey, false) as Stream;
    }

    public Stream GlobeSpotterConfiguration()
    {
      const string authorizationItem = "";
      return PostRequest(ConfigurationUrl, GetStreamCallback, authorizationItem, TypeDownloadConfig.XML, Configuration, _login, _apiKey) as Stream;
    }

    public Stream GetByBbox(Envelope envelope, string wfsRequest)
    {
      var epsgCode = SpatialReferenceDictionary.Instance.ToKnownSrsName($"EPSG:{envelope.SpatialReference.Wkid}");
      string dateString = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:00-00:00");
      string recordingItem = string.Format(_ci, wfsRequest, epsgCode, envelope.XMin, envelope.YMin, envelope.XMax,
        envelope.YMax, dateString);
      EventLog.Write(EventLogLevel.Information,
        $"Street Smart: (Web) Get recordings by BBOX, EPSG Code: {epsgCode}, BBOX: {envelope.XMin}, {envelope.YMin}, {envelope.XMax}, {envelope.YMax}, date:{dateString}");
      return PostRequest(RecordingServiceUrl, GetStreamCallback, recordingItem, TypeDownloadConfig.XML, Configuration, _login, _apiKey) as Stream;
    }

    public Stream GetByImageId(string imageId, string srsName)
    {
      var epsgCode = SpatialReferenceDictionary.Instance.ToKnownSrsName(srsName);
      string imageIdUrl = ImageIdUrl(imageId, epsgCode);
      return GetRequest(imageIdUrl, GetStreamCallback, TypeDownloadConfig.XML, Configuration, _login, _apiKey) as Stream;
    }

    #endregion

    public static string Base64Encode(string plainText)
    {
      var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
      return Convert.ToBase64String(plainTextBytes);
    }

    #region Callback functions

    private static void GetStreamCallback(IAsyncResult ar)
    {
      var state = (State)ar.AsyncState;

      try
      {
        var response = state.Request.EndGetResponse(ar);
        Stream responseStream = response.GetResponseStream();

        if (responseStream != null)
        {
          var readFile = new BinaryReader(responseStream);
          state.Result = new MemoryStream();
          var writeFile = new BinaryWriter((Stream)state.Result);
          var buffer = new byte[BufferImageLengthService];
          int readBytes;

          do
          {
            readBytes = readFile.Read(buffer, 0, BufferImageLengthService);
            writeFile.Write(buffer, 0, readBytes);
          } while (readBytes != 0);

          writeFile.Flush();
        }

        response.Close();
        state.OperationComplete.Set();
      }
      catch (Exception e)
      {
        state.OperationException = e;
        state.OperationComplete.Set();
      }
    }

    #endregion
  }
}
