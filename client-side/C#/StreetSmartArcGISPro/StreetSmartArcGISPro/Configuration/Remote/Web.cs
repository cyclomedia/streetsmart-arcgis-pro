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

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Utilities;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Resource;

using mySpatialReferenceList = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReferenceList;

namespace StreetSmartArcGISPro.Configuration.Remote
{
  public class Web: Urls
  {
    #region Constants

    private const int BufferImageLengthService = 2048;
    private const int LeaseTimeOut = 5000;
    private const int DefaultConnectionLimit = 5;

    #endregion

    #region Members

    private readonly int[] _waitTimeInternalServerError = {5000, 0};
    private readonly int[] _timeOutService = {3000, 1000};
    private readonly int[] _retryTimeService = {3, 1};

    private static Web _web;

    private readonly Login _login;
    private readonly ApiKey _apiKey;
    private readonly CultureInfo _ci;

    #endregion

    #region Enums

    private enum TypeDownloadConfig
    {
      // ReSharper disable once InconsistentNaming
      XML = 0
    }

    #endregion

    #region Properties

    public static Web Instance => _web ?? (_web = new Web());

    #endregion

    #region Constructor

    private Web()
    {
      _login = Login.Instance;
      _apiKey = ApiKey.Instance;
      _ci = CultureInfo.InvariantCulture;
      ServicePointManager.DefaultConnectionLimit = DefaultConnectionLimit;
    }

    #endregion

    #region Interface functions

    public Stream SpatialReferences()
    {
      return GetRequest(SpatialReferenceUrl, GetStreamCallback, TypeDownloadConfig.XML, false) as Stream;
    }

    public Stream GlobeSpotterConfiguration()
    {
      const string authorizationItem = "";
      return PostRequest(ConfigurationUrl, GetStreamCallback, authorizationItem, TypeDownloadConfig.XML) as Stream;
    }

    public Stream GetByBbox(Envelope envelope, string wfsRequest)
    {
      string epsgCode = $"EPSG:{envelope.SpatialReference.Wkid}";
      epsgCode = mySpatialReferenceList.Instance.ToKnownSrsName(epsgCode);
      string dateString = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:00-00:00");
      string recordingItem = string.Format(_ci, wfsRequest, epsgCode, envelope.XMin, envelope.YMin, envelope.XMax,
        envelope.YMax, dateString);
      EventLog.Write(EventLog.EventType.Information,
        $"Street Smart: (Web.cs) (GetByBbox) Get recordings by BBOX, EPSG Code: {epsgCode}, BBOX: {envelope.XMin}, {envelope.YMin}, {envelope.XMax}, {envelope.YMax}, date:{dateString}");
      return PostRequest(RecordingServiceUrl, GetStreamCallback, recordingItem, TypeDownloadConfig.XML) as Stream;
    }

    public Stream GetByImageId(string imageId, string epsgCode)
    {
      epsgCode = mySpatialReferenceList.Instance.ToKnownSrsName(epsgCode);
      string imageIdUrl = ImageIdUrl(imageId, epsgCode);
      return GetRequest(imageIdUrl, GetStreamCallback, TypeDownloadConfig.XML) as Stream;
    }

    #endregion

    #region Web request functions

    private object GetRequest(string remoteLocation, AsyncCallback asyncCallback, TypeDownloadConfig typeDownloadConfig, bool useAuthorisation = true)
    {
      object result = null;
      bool download = false;
      int retry = 0;
      var configId = (int) typeDownloadConfig;
      WebRequest request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Get, 0, useAuthorisation);
      var state = new State {Request = request};

      while (download == false && retry < _retryTimeService[configId])
      {
        try
        {
          lock (this)
          {
            ManualResetEvent waitObject = state.OperationComplete;
            request.BeginGetResponse(asyncCallback, state);

            if (!waitObject.WaitOne(_timeOutService[configId]))
            {
              throw new Exception("Time out download item");
            }

            if (state.OperationException != null)
            {
              throw state.OperationException;
            }

            result = state.Result;
            download = true;
          }
        }
        catch (WebException ex)
        {
          retry++;
          var responce = ex.Response as HttpWebResponse;

          if (responce?.StatusCode == HttpStatusCode.InternalServerError && (retry < _retryTimeService[configId]))
          {
            Thread.Sleep(_waitTimeInternalServerError[configId]);
          }

          if (retry == _retryTimeService[configId])
          {
            throw;
          }
        }
        catch (Exception)
        {
          retry++;

          if (retry == _retryTimeService[configId])
          {
            throw;
          }
        }
      }

      return result;
    }

    private object PostRequest(string remoteLocation, AsyncCallback asyncCallback, string postItem, TypeDownloadConfig typeDownloadConfig, bool useAuthorisation = true)
    {
      object result = null;
      bool download = false;
      int retry = 0;
      var configId = (int) typeDownloadConfig;
      var bytes = (new UTF8Encoding()).GetBytes(postItem);
      WebRequest request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Post, bytes.Length, useAuthorisation);
      var state = new State {Request = request};

      lock (this)
      {
        using (Stream reqstream = request.GetRequestStream())
        {
          reqstream.Write(bytes, 0, bytes.Length);
        }
      }

      while (download == false && retry < _retryTimeService[configId])
      {
        try
        {
          lock (this)
          {
            ManualResetEvent waitObject = state.OperationComplete;
            request.BeginGetResponse(asyncCallback, state);

            if (!waitObject.WaitOne(_timeOutService[configId]))
            {
              throw new Exception("Time out download item.");
            }

            if (state.OperationException != null)
            {
              throw state.OperationException;
            }

            result = state.Result;
            download = true;
          }
        }
        catch (WebException ex)
        {
          retry++;

          if (ex.Response is HttpWebResponse responce)
          {
            Uri responseUri = responce.ResponseUri;

            if (responseUri != null)
            {
              string absoluteUri = responseUri.AbsoluteUri;

              if (absoluteUri != remoteLocation)
              {
                remoteLocation = absoluteUri;
                request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Post, bytes.Length);
                state = new State {Request = request};

                lock (this)
                {
                  using (Stream reqstream = request.GetRequestStream())
                  {
                    reqstream.Write(bytes, 0, bytes.Length);
                  }
                }
              }
            }

            if (responce.StatusCode == HttpStatusCode.InternalServerError && retry < _retryTimeService[configId])
            {
              Thread.Sleep(_waitTimeInternalServerError[configId]);
            }
          }

          if (retry == _retryTimeService[configId])
          {
            throw;
          }
        }
        catch (Exception)
        {
          retry++;

          if (retry == _retryTimeService[configId])
          {
            throw;
          }
        }
      }

      return result;
    }

    private WebRequest OpenWebRequest(string remoteLocation, string webRequest, int length, bool useAuthorization = true)
    {
      IWebProxy proxy;

      if (Configuration.UseProxyServer)
      {
        var webProxy = new WebProxy(Configuration.ProxyAddress, Configuration.ProxyPort)
        {
          BypassProxyOnLocal = Configuration.ProxyBypassLocalAddresses,
          UseDefaultCredentials = Configuration.ProxyUseDefaultCredentials
        };

        if (!Configuration.ProxyUseDefaultCredentials)
        {
          webProxy.Credentials = new NetworkCredential(Configuration.ProxyUsername, Configuration.ProxyPassword, Configuration.ProxyDomain);
        }

        proxy = webProxy;
      }
      else
      {
        proxy = WebRequest.GetSystemWebProxy();
      }

      string credentials1 = Base64Encode($"{_login.Username}:{_login.Password}");
      string credentials = $"Basic {credentials1}";

      var request = (HttpWebRequest) WebRequest.Create(remoteLocation);
      request.Credentials = new NetworkCredential(_login.Username, _login.Password);
      request.Method = webRequest;
      request.ContentLength = length;
      request.KeepAlive = true;
      request.Pipelined = true;
      request.Proxy = proxy;
      request.PreAuthenticate = true;
      request.ContentType = "text/xml";
      request.Headers.Add("ApiKey", _apiKey.Value);

      if (useAuthorization)
      {
        request.Headers.Add("authorization", credentials);
      }

      request.ServicePoint.ConnectionLeaseTimeout = LeaseTimeOut;
      request.ServicePoint.MaxIdleTime = LeaseTimeOut;
      return request;
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
      var state = (State) ar.AsyncState;

      try
      {
        var response = state.Request.EndGetResponse(ar);
        Stream responseStream = response.GetResponseStream();

        if (responseStream != null)
        {
          var readFile = new BinaryReader(responseStream);
          state.Result = new MemoryStream();
          var writeFile = new BinaryWriter((Stream) state.Result);
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
