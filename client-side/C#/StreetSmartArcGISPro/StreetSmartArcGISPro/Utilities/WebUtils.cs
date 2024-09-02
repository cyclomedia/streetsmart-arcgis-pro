using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote;

namespace StreetSmartArcGISPro.Utilities
{
  internal class WebUtils
  {
    #region Constants
    private const int BufferImageLengthService = 2048;
    private const int LeaseTimeOut = 5000;
    #endregion

    #region Members
    private static readonly int[] _retryTimeService = { 3, 1 };
    private static readonly int[] _timeOutService = { 3000, 1000 };
    private static readonly int[] _waitTimeInternalServerError = { 5000, 0 };
    private static readonly object lockObject = new object();
    #endregion
    public enum TypeDownloadConfig
    {
      // ReSharper disable once InconsistentNaming
      XML = 0
    }

    public static object GetRequest(
        string remoteLocation,
        AsyncCallback asyncCallback,
        TypeDownloadConfig typeDownloadConfig,
        Configuration.File.Configuration fileConfiguration,
        Configuration.File.Login login,
        Configuration.Resource.ApiKey apiKey,
        bool useAuthorisation = true
        )
    {
      object result = null;
      bool download = false;
      int retry = 0;
      var configId = (int)typeDownloadConfig;
      WebRequest request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Get, 0, fileConfiguration, login, apiKey, useAuthorisation);
      var state = new State { Request = request };

      while (download == false && retry < _retryTimeService[configId])
      {
        try
        {
          lock (lockObject)
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

    public static object PostRequest
        (
            string remoteLocation,
            AsyncCallback asyncCallback,
            string postItem,
            TypeDownloadConfig typeDownloadConfig,
            Configuration.File.Configuration fileConfiguration,
            Configuration.File.Login login,
            Configuration.Resource.ApiKey apiKey,
            bool useAuthorisation = true)
    {
      object result = null;
      bool download = false;
      int retry = 0;
      var configId = (int)typeDownloadConfig;
      var bytes = (new UTF8Encoding()).GetBytes(postItem);
      WebRequest request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Post, bytes.Length, fileConfiguration, login, apiKey, useAuthorisation);
      var state = new State { Request = request };

      lock (lockObject)
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
          lock (lockObject)
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
                request = OpenWebRequest(remoteLocation, WebRequestMethods.Http.Post, bytes.Length, fileConfiguration, login, apiKey);
                state = new State { Request = request };

                lock (lockObject)
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

    private static WebRequest OpenWebRequest(
        string remoteLocation,
        string webRequest,
        int length,
        Configuration.File.Configuration fileConfiguration,
        Configuration.File.Login login,
        Configuration.Resource.ApiKey apiKey,
        bool useAuthorization = true
        )
    {
      IWebProxy proxy;

      if (fileConfiguration.UseProxyServer)
      {
        var webProxy = new WebProxy(fileConfiguration.ProxyAddress, fileConfiguration.ProxyPort)
        {
          BypassProxyOnLocal = fileConfiguration.ProxyBypassLocalAddresses,
          UseDefaultCredentials = fileConfiguration.ProxyUseDefaultCredentials
        };

        if (!fileConfiguration.ProxyUseDefaultCredentials)
        {
          webProxy.Credentials = new NetworkCredential(fileConfiguration.ProxyUsername, fileConfiguration.ProxyPassword, fileConfiguration.ProxyDomain);
        }

        proxy = webProxy;
      }
      else
      {
        proxy = WebRequest.GetSystemWebProxy();
      }

      string credentials1 = Base64Encode($"{login.Username}:{login.Password}");
      string credentials = $"Basic {credentials1}";

      var request = (HttpWebRequest)WebRequest.Create(remoteLocation);
      request.Credentials = new NetworkCredential(login.Username, login.Password);
      request.Method = webRequest;
      request.ContentLength = length;
      request.KeepAlive = true;
      request.Pipelined = true;
      request.Proxy = proxy;
      request.PreAuthenticate = true;
      request.ContentType = "text/xml";
      request.Headers.Add("ApiKey", apiKey.Value);

      if (useAuthorization)
      {
        if (Login.Instance.IsOAuth)
        {
          var bearer = Login.Instance.Bearer;

          request.Headers.Add("authorization", "Bearer " + bearer);
        }
        else
        {
          request.Headers.Add("authorization", credentials);
        }
      }

      request.ServicePoint.ConnectionLeaseTimeout = LeaseTimeOut;
      request.ServicePoint.MaxIdleTime = LeaseTimeOut;
      return request;
    }

    public static string Base64Encode(string plainText)
    {
      var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
      return Convert.ToBase64String(plainTextBytes);
    }

    #region Callback functions

    public static void GetStreamCallback(IAsyncResult ar)
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
