/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018, CycloMedia, All rights reserved.
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

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;

using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;

using FileSettings = StreetSmartArcGISPro.Configuration.File.Settings;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Settings: Page, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly FileSettings _settings;

    private readonly SpatialReference _recordingLayerCoordinateSystem;
    private readonly SpatialReference _cycloramaViewerCoordinateSystem;

    private readonly int _ctrlClickHashTag;
    private readonly int _ctrlClickDelta;
    private readonly bool _showDetailImages;
    private readonly bool _enableSmartClickMeasurement;

    private List<SpatialReference> _existsInAreaSpatialReferences;

    #endregion

    #region Constructors

    protected Settings()
    {
      _settings = FileSettings.Instance;

      _recordingLayerCoordinateSystem = _settings.RecordingLayerCoordinateSystem;
      _cycloramaViewerCoordinateSystem = _settings.CycloramaViewerCoordinateSystem;

      _ctrlClickHashTag = _settings.CtrlClickHashTag;
      _ctrlClickDelta = _settings.CtrlClickDelta;
      _showDetailImages = _settings.ShowDetailImages;
      _enableSmartClickMeasurement = _settings.EnableSmartClickMeasurement;
    }

    #endregion

    #region Properties

    /// <summary>
    /// All supporting spatial references
    /// </summary>
    public List<SpatialReference> ExistsInAreaSpatialReferences
    {
      get
      {
        if (_existsInAreaSpatialReferences == null)
        {
          CreateExistsInAreaSpatialReferences();
        }

        return _existsInAreaSpatialReferences;
      }
    }

    /// <summary>
    /// Recording layer coordinate system
    /// </summary>
    public SpatialReference RecordingLayerCoordinateSystem
    {
      get => _settings.RecordingLayerCoordinateSystem;
      set
      {
        if (_settings.RecordingLayerCoordinateSystem != value)
        {
          IsModified = true;
          _settings.RecordingLayerCoordinateSystem = value;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Cyclorama viewer coordinate system
    /// </summary>
    public SpatialReference CycloramaViewerCoordinateSystem
    {
      get => _settings.CycloramaViewerCoordinateSystem;
      set
      {
        if (_settings.CycloramaViewerCoordinateSystem != value)
        {
          IsModified = true;
          _settings.CycloramaViewerCoordinateSystem = value;
          NotifyPropertyChanged();
          // ReSharper disable once ExplicitCallerInfoArgument
          NotifyPropertyChanged("CanMeasuring");
        }
      }
    }

    /// <summary>
    /// Can measuring property
    /// </summary>
    public bool CanMeasuring => _settings.CycloramaViewerCoordinateSystem != null && _settings.CycloramaViewerCoordinateSystem.CanMeasuring;

    /// <summary>
    /// CTRL-CLICK #
    /// </summary>
    public int CtrlClickHashTag
    {
      get => _settings.CtrlClickHashTag - 1;
      set
      {
        if (_settings.CtrlClickHashTag - 1 != value)
        {
          IsModified = true;
          _settings.CtrlClickHashTag = value + 1;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// CTRL-CLICK Δ
    /// </summary>
    public int CtrlClickDelta
    {
      get => _settings.CtrlClickDelta - 1;
      set
      {
        if (_settings.CtrlClickDelta - 1 != value)
        {
          IsModified = true;
          _settings.CtrlClickDelta = value + 1;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Show detail images
    /// </summary>
    public bool ShowDetailImages
    {
      get => _settings.ShowDetailImages;
      set
      {
        if (_settings.ShowDetailImages != value)
        {
          IsModified = true;
          _settings.ShowDetailImages = value;
          NotifyPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Enable smart click measurement
    /// </summary>
    public bool EnableSmartClickMeasurement
    {
      get => _settings.EnableSmartClickMeasurement;
      set
      {
        if (_settings.EnableSmartClickMeasurement != value)
        {
          IsModified = true;
          _settings.EnableSmartClickMeasurement = value;
          NotifyPropertyChanged();
        }
      }
    }

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      _settings.Save();
      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _settings.RecordingLayerCoordinateSystem = _recordingLayerCoordinateSystem;
      _settings.CycloramaViewerCoordinateSystem = _cycloramaViewerCoordinateSystem;

      _settings.CtrlClickHashTag = _ctrlClickHashTag;
      _settings.CtrlClickDelta = _ctrlClickDelta;
      _settings.ShowDetailImages = _showDetailImages;
      _settings.EnableSmartClickMeasurement = _enableSmartClickMeasurement;

      _settings.Save();
      return base.CancelAsync();
    }

    #endregion

    #region Functions

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void CreateExistsInAreaSpatialReferences()
    {
      _existsInAreaSpatialReferences = new List<SpatialReference>();
      SpatialReferenceList spatialReferenceList = SpatialReferenceList.Instance;

      foreach (var spatialReference in spatialReferenceList)
      {
        bool exists = await spatialReference.ExistsInAreaAsync();

        if (exists && !_existsInAreaSpatialReferences.Contains(spatialReference))
        {
          _existsInAreaSpatialReferences.Add(spatialReference);
        }

        if (!exists && _existsInAreaSpatialReferences.Contains(spatialReference))
        {
          _existsInAreaSpatialReferences.Remove(spatialReference);
        }

        if (RecordingLayerCoordinateSystem != null && spatialReference == RecordingLayerCoordinateSystem)
        {
          // ReSharper disable once ExplicitCallerInfoArgument
          NotifyPropertyChanged("RecordingLayerCoordinateSystem");
        }

        if (CycloramaViewerCoordinateSystem != null && spatialReference == CycloramaViewerCoordinateSystem)
        {
          // ReSharper disable once ExplicitCallerInfoArgument
          NotifyPropertyChanged("CycloramaViewerCoordinateSystem");
        }
      }
    }

    #endregion
  }
}
