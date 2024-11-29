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

using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.File
{
  public class Setting : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private SpatialReference _recordingLayerCoordinateSystem;
    private SpatialReference _cycloramaViewerCoordinateSystem;
    private int _overlayDrawDistance;
    private string _map;
    private bool? _syncLayerVisibility;

    #endregion

    #region Properties
    /// <summary>
    /// Enable/disable of syncing visibility on project level between ArcGIS Pro map layers and cyclorama overlays
    /// </summary>
    [XmlElement(IsNullable = true,ElementName = "SyncLayerVisibility")]
    public bool? SyncLayerVisibility
    {
      get
      {
        return _syncLayerVisibility;
      }
      set
      {
        if(_syncLayerVisibility != value)
        { 
          _syncLayerVisibility = value;
          OnPropertyChanged();
        }
      } 
    }
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
        if (value != null && (_recordingLayerCoordinateSystem == null || value.SRSName != _recordingLayerCoordinateSystem.SRSName))
        {
          _recordingLayerCoordinateSystem = SpatialReferenceDictionary.Instance.TryGetValue(value.SRSName, out var result) ? result : value;
          OnPropertyChanged();
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
        if (value != null && (_cycloramaViewerCoordinateSystem == null || value.SRSName != _cycloramaViewerCoordinateSystem.SRSName))
        {
          _cycloramaViewerCoordinateSystem = SpatialReferenceDictionary.Instance.TryGetValue(value.SRSName, out var result) ? result : value;
          OnPropertyChanged();
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
        SyncLayerVisibility = null,
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
