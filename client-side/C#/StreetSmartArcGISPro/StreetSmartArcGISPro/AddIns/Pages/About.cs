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
using System.Diagnostics;

using System.Reflection;
using System.Resources;

using ArcGIS.Desktop.Framework.Contracts;

using StreetSmartArcGISPro.Configuration.File;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class About: Page
  {
    #region Constructors

    protected About()
    {
    }

    #endregion

    #region Properties

    public string AboutText
    {
      get
      {
        ResourceManager res = Properties.Resources.ResourceManager;
        LanguageSettings language = LanguageSettings.Instance;
        string versionTxt = res.GetString("AboutVersion", language.CultureInfo);

        // Assembly info
        Type type = GetType();
        Assembly assembly = type.Assembly;
        string location = assembly.Location;
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(location);
        AssemblyName assName = assembly.GetName();

        // Version info
        string product = info.ProductName;
        string copyright = info.LegalCopyright;
        Version version = assName.Version;

        return $"{product}{Environment.NewLine}{copyright}{Environment.NewLine}{versionTxt}: {version}.";
      }
    }

    #endregion
  }
}
