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
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;

namespace StreetSmartArcGISPro.Configuration.File
{
  public class Setting: INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private SpatialReference _recordingLayerCoordinateSystem;
    private SpatialReference _cycloramaViewerCoordinateSystem;
    private int _overlayDrawDistance;
    private string _map;

    #endregion

    #region Properties

    /// <summary>
    /// Name of the map
    /// </summary>
    [XmlAttribute("Name")]
    public string Map
    {
      get => _map;
      set
      {
        if (_map != value)
        {
          _map = value;
          OnPropertyChanged();
        }
      }
    }

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
          bool changed = value != _recordingLayerCoordinateSystem;
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
          bool changed = value != _cycloramaViewerCoordinateSystem;
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

    #endregion

    #region Functions

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static Setting Create(string map)
    {
      var result = new Setting
      {
        RecordingLayerCoordinateSystem = null,
        CycloramaViewerCoordinateSystem = null,
        OverlayDrawDistance = 30,
        Map = map ?? string.Empty
      };

      return result;
    }

    #endregion
  }
}
