﻿/*
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

using System.IO;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("Agreement")]
  public class Agreement
  {
    #region Members

    private static readonly XmlSerializer XmlAgreement;
    private static Agreement _agreement;

    #endregion

    #region Constructors

    static Agreement()
    {
      XmlAgreement = new XmlSerializer(typeof(Agreement));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Value
    /// </summary>
    public bool Value { get; set; }

    public static Agreement Instance
    {
      get
      {
        if (_agreement == null)
        {
          Load();
        }

        return _agreement ?? (_agreement = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Agreement.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlAgreement.Serialize(streamFile, this);
      streamFile.Close();
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _agreement = (Agreement) XmlAgreement.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    private static Agreement Create()
    {
      var result = new Agreement {Value = false};
      result.Save();
      return result;
    }

    #endregion
  }
}
