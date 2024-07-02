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

using System.IO;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("ConstantsRecordingLayer")]
  public class ConstantsRecordingLayer
  {
    #region Members

    private static readonly XmlSerializer XmlConstantsRecordingLayer;
    private static ConstantsRecordingLayer _constantsRecordingLayer;

    #endregion

    #region Constructors

    static ConstantsRecordingLayer()
    {
      XmlConstantsRecordingLayer = new XmlSerializer(typeof(ConstantsRecordingLayer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// The size of the layer
    /// </summary>
    public double SizeLayer { get; set; }

    /// <summary>
    /// Minimum scale
    /// </summary>
    public double MinimumScale { get; set; }

    /// <summary>
    /// Recording feature class name
    /// </summary>
    public string RecordingLayerFeatureClassName { get; set; }

    public static ConstantsRecordingLayer Instance
    {
      get
      {
        if (_constantsRecordingLayer == null)
        {
          Load();
        }

        return _constantsRecordingLayer ?? (_constantsRecordingLayer = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "ConstantsRecordingLayer.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlConstantsRecordingLayer.Serialize(streamFile, this);
      streamFile.Close();
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _constantsRecordingLayer = (ConstantsRecordingLayer) XmlConstantsRecordingLayer.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    private static ConstantsRecordingLayer Create()
    {
      var result = new ConstantsRecordingLayer
      {
        SizeLayer = 8.0,
        MinimumScale = 2000.0,
        RecordingLayerFeatureClassName = "FCStreetSmartRecordings"
      };

      result.Save();
      return result;
    }

    #endregion
  }
}
