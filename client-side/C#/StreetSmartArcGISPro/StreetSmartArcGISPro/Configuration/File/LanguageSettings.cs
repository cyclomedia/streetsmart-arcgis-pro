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

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("LanguageSettings")]
  public class LanguageSettings : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static readonly XmlSerializer XmlLanguageSettings;
    private static LanguageSettings _languageSettings;

    private Language _language;

    #endregion

    #region Constructors

    static LanguageSettings()
    {
      XmlLanguageSettings = new XmlSerializer(typeof(LanguageSettings));
    }

    #endregion

    #region Properties

    /// <summary>
    /// User language
    /// </summary>
    [XmlIgnore]
    public Language Language
    {
      get => _language;
      set
      {
        if (_language != value)
        {
          _language = value;
          OnPropertyChanged();
        }
      }
    }

    public string Locale
    {
      get => _language.Locale;
      set => _language = Languages.Instance.Get(value);
    }

    [XmlIgnore]
    public CultureInfo CultureInfo => new CultureInfo(Locale);

    public static LanguageSettings Instance
    {
      get
      {
        if (_languageSettings == null)
        {
          Load();
        }

        return _languageSettings ?? (_languageSettings = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "LanguageSettings.xml");

    #endregion

    #region Functions

    public void Save()
    {
      OnPropertyChanged();
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlLanguageSettings.Serialize(streamFile, this);
      streamFile.Close();
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _languageSettings = (LanguageSettings) XmlLanguageSettings.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static LanguageSettings Create()
    {
      return new LanguageSettings
      {
        Language = Languages.Instance.Get("en-GB")
      };
    }

    #endregion
  }
}
