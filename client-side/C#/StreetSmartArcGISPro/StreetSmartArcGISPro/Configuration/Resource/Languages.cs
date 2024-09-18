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

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Resource
{
  [XmlRoot("Languages")]
  public class Languages : ObservableCollection<Language>
  {
    #region Members

    private static readonly XmlSerializer XmlLanguages;
    private static Languages _languages;

    #endregion

    #region Constructors

    static Languages()
    {
      XmlLanguages = new XmlSerializer(typeof(Languages));
    }

    #endregion

    #region Properties

    public static Languages Instance
    {
      get
      {
        if (_languages == null)
        {
          Load();
        }

        return _languages ?? (_languages = []);
      }
    }

    #endregion

    #region Functions

    public Language Get(string locale)
    {
      return this.FirstOrDefault(check => check.Locale == locale);
    }

    private static void Load()
    {
      Assembly thisAssembly = Assembly.GetExecutingAssembly();
      const string manualPath = @"StreetSmartArcGISPro.Resources.Languages.xml";
      Stream manualStream = thisAssembly.GetManifestResourceStream(manualPath);

      if (manualStream != null)
      {
        _languages = (Languages)XmlLanguages.Deserialize(manualStream);
        manualStream.Close();
      }
    }

    #endregion
  }
}
