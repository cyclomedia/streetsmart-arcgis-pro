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
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;
using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("Settings")]
  public class Settings: INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static readonly XmlSerializer XmlSettings;
    private static Settings _settings;
    private SpatialReference _recordingLayerCoordinateSystem;
    private SpatialReference _cycloramaViewerCoordinateSystem;
    private int _overlayDrawDistance;

    #endregion

    #region Constructors

    static Settings()
    {
      XmlSettings = new XmlSerializer(typeof(Settings));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Recording layer coordinate system
    /// </summary>
    public SpatialReference RecordingLayerCoordinateSystem
    {
      get => _recordingLayerCoordinateSystem;
      set
      {
        if (value != null)
        {
          bool changed = (value != _recordingLayerCoordinateSystem);
          SpatialReferenceList spatialReferenceList = SpatialReferenceList.Instance;
          _recordingLayerCoordinateSystem = value;

          foreach (var spatialReference in spatialReferenceList)
          {
            if (spatialReference.SRSName == value.SRSName)
            {
              _recordingLayerCoordinateSystem = spatialReference;
            }
          }

          if (changed)
          {
            OnPropertyChanged();
          }
        }
      }
    }

    /// <summary>
    /// Cyclorama viewer coordinate system
    /// </summary>
    public SpatialReference CycloramaViewerCoordinateSystem
    {
      get => _cycloramaViewerCoordinateSystem;
      set
      {
        if (value != null)
        {
          bool changed = (value != _cycloramaViewerCoordinateSystem);
          SpatialReferenceList spatialReferenceList = SpatialReferenceList.Instance;
          _cycloramaViewerCoordinateSystem = value;

          foreach (var spatialReference in spatialReferenceList)
          {
            if (spatialReference.SRSName == value.SRSName)
            {
              _cycloramaViewerCoordinateSystem = spatialReference;
            }
          }

          if (changed)
          {
            OnPropertyChanged();
          }
        }
      }
    }

    /// <summary>
    /// overlay draw distance
    /// </summary>
    public int OverlayDrawDistance
    {
      get => _overlayDrawDistance;
      set
      {
        if (_overlayDrawDistance != value)
        {
          _overlayDrawDistance = value;
          OnPropertyChanged();
        }
      }
    }

    public static Settings Instance
    {
      get
      {
        if (_settings == null)
        {
          Load();
        }

        return _settings ?? (_settings = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Settings.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlSettings.Serialize(streamFile, this);
      streamFile.Close();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _settings = (Settings) XmlSettings.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    private static Settings Create()
    {
      var result = new Settings
      {
        RecordingLayerCoordinateSystem = null,
        CycloramaViewerCoordinateSystem = null,
        OverlayDrawDistance = 30
      };

      result.Save();
      return result;
    }

    #endregion
  }
}
