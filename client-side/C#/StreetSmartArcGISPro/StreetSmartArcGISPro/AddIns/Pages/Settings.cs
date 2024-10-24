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

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;
using StreetSmartArcGISPro.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FileProjectList = StreetSmartArcGISPro.Configuration.File.ProjectList;
using FileSettings = StreetSmartArcGISPro.Configuration.File.Setting;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Settings : Page, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly FileSettings _settings;
    private readonly FileProjectList _projectList;
    private readonly AvailableCoordSystems _availableCoordSystems;

    private readonly SpatialReference _recordingLayerCoordinateSystem;
    private readonly SpatialReference _cycloramaViewerCoordinateSystem;
    private readonly bool? _syncLayerVisibility;
    private readonly int _overlayDrawDistance;

    #endregion

    #region Constructors

    protected Settings()
    {
      _projectList = FileProjectList.Instance;
      _settings = _projectList.GetSettings(MapView.Active);

      _availableCoordSystems = AvailableCoordSystems.Instance;
      _availableCoordSystems.PropertyChanged += OnAvailableCoordSystemPropertyChanged;
      _ = _availableCoordSystems.CheckAvailableCoordinateSystems();

      _recordingLayerCoordinateSystem = _settings.RecordingLayerCoordinateSystem;
      _cycloramaViewerCoordinateSystem = _settings.CycloramaViewerCoordinateSystem;

      _overlayDrawDistance = _settings.OverlayDrawDistance;
      _syncLayerVisibility = _settings.SyncLayerVisibility;
    }

    ~Settings()
    {
      _availableCoordSystems.PropertyChanged -= OnAvailableCoordSystemPropertyChanged;
    }

    #endregion

    #region Properties

    /// <summary>
    /// All supporting spatial references
    /// </summary>
    public List<SpatialReference> ExistsInAreaSpatialReferences => _availableCoordSystems.ExistInAreaSpatialReferences;

    public List<int> ListOfOverlayDrawDistance
    {
      get
      {
        List<int> result = [];

        for (int i = 5; i <= 150; i += 5)
        {
          result.Add(i);
        }

        return result;
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
    /// Sync layer visibility
    /// </summary>
    public bool? SyncLayerVisibility
    {
      get => _settings.SyncLayerVisibility;
      set
      {
        if (_settings.SyncLayerVisibility != value)
        {
          IsModified = true;
          _settings.SyncLayerVisibility = value;
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
    /// Overlay draw distance
    /// </summary>
    public int OverlayDrawDistance
    {
      get => _settings.OverlayDrawDistance;
      set
      {
        if (_settings.OverlayDrawDistance != value)
        {
          IsModified = true;
          _settings.OverlayDrawDistance = value;
          NotifyPropertyChanged();
        }
      }
    }

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      _projectList.Save();
      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _settings.RecordingLayerCoordinateSystem = _recordingLayerCoordinateSystem;
      _settings.CycloramaViewerCoordinateSystem = _cycloramaViewerCoordinateSystem;

      _settings.OverlayDrawDistance = _overlayDrawDistance;

      _projectList.Save();
      return base.CancelAsync();
    }

    #endregion

    #region Functions

    protected override void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnAvailableCoordSystemPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "ExistInAreaSpatialReferences")
      {
        // ReSharper disable once ExplicitCallerInfoArgument
        NotifyPropertyChanged("ExistsInAreaSpatialReferences");
      }
    }

    #endregion
  }
}
