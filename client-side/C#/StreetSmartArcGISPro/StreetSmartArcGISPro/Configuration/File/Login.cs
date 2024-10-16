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

using ArcGIS.Desktop.Framework;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.Utilities;
using System;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("Login")]
  public class Login : INotifyPropertyChanged
  {
    public enum OAuthStatus
    {
      None,
      SigningIn,
      SignedIn,
      SigningOut,
      SignedOut
    }

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Constants
    private const string CheckWord = "1234567890!";
    private const int HashSize = 32; // SHA256
    #endregion

    #region Members

    private static readonly XmlSerializer XmlLogin;
    private static readonly byte[] Salt;

    private static Login _login;

    private bool _credentials;
    private bool _isOAuth;
    private string _oAuthUsername;
    private OAuthStatus _oAuthAuthenticationStatus;
    private bool _isFromSettingsPage;
    #endregion

    #region Constructors

    static Login()
    {
      XmlLogin = new XmlSerializer(typeof(Login));
      Salt = [1, 2, 3, 4, 5, 6, 7, 8, 9, 0];
    }

    private Login()
    {
      _credentials = false;
      _isOAuth = false;
      _oAuthAuthenticationStatus = OAuthStatus.None;
      _isFromSettingsPage = false;
    }

    #endregion

    #region Properties

    [XmlIgnore]
    public string Username { get; set; }

    [XmlIgnore]
    public string Password { get; set; }

    public bool IsOAuth
    {
      get => _isOAuth;
      set
      {
        if (_isOAuth != value)
        {
          _isOAuth = value;

          _oAuthAuthenticationStatus = _isOAuth ? OAuthStatus.SignedOut : OAuthStatus.None;

          OnPropertyChanged();
          OnPropertyChanged("OAuthAuthenticationStatus");
        }
      }
    }

    [XmlIgnore]
    public string OAuthUsername
    {
      get => _oAuthUsername;
      set
      {
        if (_oAuthUsername != value)
        {
          _oAuthUsername = value;
          OnPropertyChanged();
        }
      }
    }


    [XmlIgnore]
    public bool IsFromSettingsPage
    {
      get => _isFromSettingsPage;
      set
      {
        if (_isFromSettingsPage != value)
        {
          _isFromSettingsPage = value;
          OnPropertyChanged();
        }
      }
    }

    [XmlIgnore]
    public OAuthStatus OAuthAuthenticationStatus
    {
      get => _oAuthAuthenticationStatus;
      set
      {
        if (_oAuthAuthenticationStatus != value)
        {
          _oAuthAuthenticationStatus = value;

          if (_oAuthAuthenticationStatus == OAuthStatus.SignedIn)
          {
            Check();
          }

          OnPropertyChanged();
        }
      }
    }

    [XmlIgnore]
    public string Bearer { get; set; }

    [XmlIgnore]
    public bool Credentials
    {
      get => _credentials;
      set
      {
        if (_credentials != value)
        {
          _credentials = value;
          OnPropertyChanged();

          if (value)
          {
            FrameworkApplication.State.Activate("streetSmartArcGISPro_loginSuccessfullyState");
          }
          else
          {
            FrameworkApplication.State.Deactivate("streetSmartArcGISPro_loginSuccessfullyState");
          }
        }
      }
    }

    public string Code
    {
      get
      {
        var ct1 = Encrypt(CheckWord, Salt, Encoding.UTF8.GetBytes($"{Username};{Password}"));
        return Convert.ToBase64String(ct1);
      }
      set
      {
        var ct1 = Convert.FromBase64String(value);
        var pt1 = Decrypt(CheckWord, Salt, ct1);
        string result = Encoding.UTF8.GetString(pt1);
        var values = result.Split(';');
        Username = values.Length >= 1 ? values[0] : string.Empty;
        Password = values.Length >= 2 ? values[1] : string.Empty;
      }
    }

    public static Login Instance
    {
      get
      {
        if (_login == null)
        {
          Load();
        }

        return _login ?? (_login = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Login.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlLogin.Serialize(streamFile, this);
      streamFile.Close();
      Check();
    }

    private static Login Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        using var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _login = (Login)XmlLogin.Deserialize(streamFile);
        streamFile.Close();
        if (!_login.IsOAuth)
          _login.Check();
      }

      return _login;
    }

    private static Login Create()
    {
      var result = new Login();
      result.Save();
      return result;
    }

    public void Check()
    {
      Credentials = (IsOAuth || (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))) && GlobeSpotterConfiguration.CheckCredentials();
    }

    public void Clear()
    {
      GlobeSpotterConfiguration.Delete();
      Credentials = false;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Performs encryption with random IV (prepended to output), and includes hash of plaintext for verification.
    /// </summary>
    private static byte[] Encrypt(string password, byte[] passwordSalt, byte[] plainText)
    {
      // Construct message with hash
      var msg = new byte[HashSize + plainText.Length];
      var hash = ComputeHash(plainText, 0, plainText.Length);
      Buffer.BlockCopy(hash, 0, msg, 0, HashSize);
      Buffer.BlockCopy(plainText, 0, msg, HashSize, plainText.Length);

      // Encrypt
      using (var aes = CreateAes(password, passwordSalt))
      {
        aes.GenerateIV();

        using (var enc = aes.CreateEncryptor())
        {
          var encBytes = enc.TransformFinalBlock(msg, 0, msg.Length);

          // Prepend IV to result
          var res = new byte[aes.IV.Length + encBytes.Length];
          Buffer.BlockCopy(aes.IV, 0, res, 0, aes.IV.Length);
          Buffer.BlockCopy(encBytes, 0, res, aes.IV.Length, encBytes.Length);
          return res;
        }
      }
    }

    private static byte[] Decrypt(string password, byte[] passwordSalt, byte[] cipherText)
    {
      using (var aes = CreateAes(password, passwordSalt))
      {
        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(cipherText, 0, iv, 0, iv.Length);
        aes.IV = iv; // Probably could copy right to the byte array, but that's not guaranteed

        using (var dec = aes.CreateDecryptor())
        {
          var decBytes = dec.TransformFinalBlock(cipherText, iv.Length, cipherText.Length - iv.Length);

          // Verify hash
          var hash = ComputeHash(decBytes, HashSize, decBytes.Length - HashSize);
          var existingHash = new byte[HashSize];
          Buffer.BlockCopy(decBytes, 0, existingHash, 0, HashSize);

          if (!CompareBytes(existingHash, hash))
          {
            throw new CryptographicException("Message hash incorrect.");
          }

          // Hash is valid, we're done
          var res = new byte[decBytes.Length - HashSize];
          Buffer.BlockCopy(decBytes, HashSize, res, 0, res.Length);
          return res;
        }
      }
    }

    private static bool CompareBytes(byte[] a1, byte[] a2)
    {
      bool result = a1.Length == a2.Length;

      if (result)
      {
        for (int i = 0; i < a1.Length; i++)
        {
          result = (a1[i] == a2[i]) && result;
        }
      }

      return result;
    }

    private static Aes CreateAes(string password, byte[] salt)
    {
      var pdb = new PasswordDeriveBytes(password, salt, "SHA512", 129);
      var aes = Aes.Create();

      if (aes != null)
      {
        aes.Mode = CipherMode.CBC;
        // ReSharper disable once CSharpWarnings::CS0618
        aes.Key = pdb.GetBytes(aes.KeySize / 8);
      }

      return aes;
    }

    private static byte[] ComputeHash(byte[] data, int offset, int count)
    {
      byte[] result;

      using (var sha = SHA256.Create())
      {
        result = sha.ComputeHash(data, offset, count);
      }

      return result;
    }

    private void ExtractOAuthUsername()
    {
      var handler = new JwtSecurityTokenHandler();
      var jwtSecurityToken = handler.ReadJwtToken(_login.Bearer);

      OAuthUsername = jwtSecurityToken.Claims.First(x => x.Type == "sub").Value;
    }

    public void CheckAuthenticationStatus(string bearerToken)
    {
      if (_isOAuth)
      {
        _login.Bearer = bearerToken;
        _login.OAuthAuthenticationStatus = Login.OAuthStatus.SignedIn;

        ExtractOAuthUsername();
      }
    }

    #endregion
  }
}
