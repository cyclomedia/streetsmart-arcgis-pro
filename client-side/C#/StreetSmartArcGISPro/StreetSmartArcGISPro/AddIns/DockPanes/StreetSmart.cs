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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using StreetSmart.Common.Exceptions;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.DomElement;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;

using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using ThisResources = StreetSmartArcGISPro.Properties.Resources;

namespace StreetSmartArcGISPro.AddIns.DockPanes
{
  internal class StreetSmart : DockPane, INotifyPropertyChanged
  {
    #region Constants

    private const string DockPaneId = "streetSmartArcGISPro_streetSmartDockPane";

    #endregion

    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private string _location;
    private bool _isActive;
    private bool _replace;
    private bool _nearest;
    private bool _inRestart;
    private ICoordinate _lookAt;
    private IOptions _options;

    private readonly ApiKey _apiKey;
    private readonly Settings _settings;
    private readonly FileConfiguration _configuration;
    private readonly ConstantsViewer _constants;
    private readonly Login _login;
    private readonly List<string> _openNearest;
    private readonly ViewerList _viewerList;
    private readonly MeasurementList _measurementList;
    private readonly CycloMediaGroupLayer _cycloMediaGroupLayer;
    private readonly Dispatcher _currentDispatcher;

    private CrossCheck _crossCheck;
    private SpatialReference _lastSpatialReference;

    #endregion

    #region Constructor

    protected StreetSmart()
    {
      ProjectClosedEvent.Subscribe(OnProjectClosed);
      _currentDispatcher = Dispatcher.CurrentDispatcher;
      _inRestart = false;

      _apiKey = ApiKey.Instance;
      _settings = Settings.Instance;
      _constants = ConstantsViewer.Instance;

      _login = Login.Instance;
      _login.PropertyChanged += OnLoginPropertyChanged;

      _configuration = FileConfiguration.Instance;
      _configuration.PropertyChanged += OnConfigurationPropertyChanged;

      _openNearest = new List<string>();
      _crossCheck = null;
      _lastSpatialReference = null;

      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _viewerList = streetSmartModule.ViewerList;
      _measurementList = streetSmartModule.MeasurementList;
      _cycloMediaGroupLayer = streetSmartModule.CycloMediaGroupLayer;

      Initialize();
    }

    #endregion

    #region Properties

    public IStreetSmartAPI Api { get; private set; }

    public string Location
    {
      get => _location;
      set
      {
        if (_location != value)
        {
          _location = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool IsActive
    {
      get => _isActive;
      set
      {
        if (_isActive != value)
        {
          _isActive = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool Replace
    {
      get => _replace;
      set
      {
        if (_replace != value)
        {
          _replace = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool Nearest
    {
      get => _nearest;
      set
      {
        if (_nearest != value)
        {
          _nearest = value;
          NotifyPropertyChanged();
        }
      }
    }

    public ICoordinate LookAt
    {
      get => _lookAt;
      set
      {
        _lookAt = value;
        NotifyPropertyChanged();
      }
    }

    #endregion

    #region Overrides

    protected override void OnActivate(bool isActive)
    {
      IsActive = isActive || _isActive;
      base.OnActivate(isActive);
    }

    protected override void OnHidden()
    {
      IsActive = false;
      _location = string.Empty;
      _replace = false;
      _nearest = false;
      base.OnHidden();
    }

    #endregion

    #region Functions

    private void Initialize()
    {
      if (_login.Credentials)
      {
        Api = _configuration.UseDefaultStreetSmartUrl
          ? StreetSmartAPIFactory.Create()
          : !string.IsNullOrEmpty(_configuration.StreetSmartLocation)
            ? StreetSmartAPIFactory.Create(_configuration.StreetSmartLocation)
            : null;

        if (Api != null)
        {
          Api.APIReady += ApiReady;
          Api.ViewerAdded += ViewerAdded;
          Api.ViewerRemoved += ViewerRemoved;
        }
      }
      else
      {
        DoHide();
      }
    }

    private void DoHide()
    {
      _currentDispatcher.Invoke(new Action(Hide), DispatcherPriority.ContextIdle);
    }

    private async Task RestartStreetSmart(bool reloadApi)
    {
      if (Api == null || await Api.GetApiReadyState())
      {
        _inRestart = true;
        _measurementList.Api = null;
        _cycloMediaGroupLayer.PropertyChanged += OnGroupLayerPropertyChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        _measurementList.RemoveAll();

        // toDo: about vector layer remove event listners
        _viewerList.RemoveViewers();

        if (Api != null && await Api.GetApiReadyState())
        {
          IList<IViewer> viewers = await Api.GetViewers();

          foreach (IViewer viewer in viewers)
          {
            await Api.CloseViewer(await viewer.GetId());
          }

          await Api.Destroy(_options);
        }

        if (reloadApi || Api == null)
        {
          Initialize();
        }
        else
        {
          await InitApi();
        }

        _inRestart = false;
      }
    }

    private async Task OpenImageAsync()
    {
      MapPoint point = null;

      if (Nearest)
      {
        MySpatialReference spatialReference = _settings.CycloramaViewerCoordinateSystem;
        SpatialReference thisSpatialReference = spatialReference.ArcGisSpatialReference ??
                                                await spatialReference.CreateArcGisSpatialReferenceAsync();

        string[] splitLoc = _location.Split(',');
        CultureInfo ci = CultureInfo.InvariantCulture;
        double x = double.Parse(splitLoc.Length >= 1 ? splitLoc[0] : "0.0", ci);
        double y = double.Parse(splitLoc.Length >= 2 ? splitLoc[1] : "0.0", ci);

        await QueuedTask.Run(() =>
        {
          point = MapPointBuilder.CreateMapPoint(x, y, _lastSpatialReference);
        });

        if (_lastSpatialReference != null && thisSpatialReference.Wkid != _lastSpatialReference.Wkid)
        {
          await QueuedTask.Run(() =>
          {
            ProjectionTransformation projection = ProjectionTransformation.Create(_lastSpatialReference,
              thisSpatialReference);
            point = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
          });

          if (point != null)
          {
            _location = string.Format(ci, "{0},{1}", point.X, point.Y);
          }
        }
      }

      string epsgCode = CoordSystemUtils.CheckCycloramaSpatialReference();
      IList<ViewerType> viewerTypes = new List<ViewerType> { ViewerType.Panorama };
      IPanoramaViewerOptions panoramaOptions = PanoramaViewerOptionsFactory.Create(true, false, true, true, Replace, true);
      IViewerOptions viewerOptions = ViewerOptionsFactory.Create(viewerTypes, epsgCode, panoramaOptions);

      try
      {
        IList<IViewer> viewers = await Api.Open(_location, viewerOptions);

        if (Nearest && point != null)
        {
          if (_crossCheck == null)
          {
            _crossCheck = new CrossCheck();
          }

          double size = _constants.CrossCheckSize;
          await _crossCheck.UpdateAsync(point.X, point.Y, size);

          foreach (IViewer cyclViewer in viewers)
          {
            if (cyclViewer is IPanoramaViewer)
            {
              IPanoramaViewer panoramaViewer = cyclViewer as IPanoramaViewer;

              Viewer viewer = _viewerList.GetViewer(panoramaViewer);

              if (viewer != null)
              {
                viewer.HasMarker = true;
              }
              else
              {
                IRecording recording = await panoramaViewer.GetRecording();
                _openNearest.Add(recording.Id);
              }
            }
          }
        }

        MySpatialReference cycloSpatialReference = _settings.CycloramaViewerCoordinateSystem;
        _lastSpatialReference = cycloSpatialReference.ArcGisSpatialReference ??
                                await cycloSpatialReference.CreateArcGisSpatialReferenceAsync();
      }
      catch (StreetSmartImageNotFoundException)
      {
        MessageBox.Show($"{ThisResources.StreetSmartCanNotOpenImage}: {_location} ({epsgCode})",
          ThisResources.StreetSmartCanNotOpenImage, MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private async Task MoveToLocationAsync(IPanoramaViewer panoramaViewer)
    {
      IRecording recording = await panoramaViewer.GetRecording();
      ICoordinate coordinate = recording.XYZ;

      if (coordinate != null)
      {
        double x = coordinate.X ?? 0.0;
        double y = coordinate.Y ?? 0.0;
        double z = coordinate.Z ?? 0.0;
        MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);
        MapView thisView = MapView.Active;
        Envelope envelope = thisView?.Extent;

        if (point != null && envelope != null)
        {
          const double percent = 10.0;
          double xBorder = (envelope.XMax - envelope.XMin) * percent / 100;
          double yBorder = (envelope.YMax - envelope.YMin) * percent / 100;
          bool inside = point.X > envelope.XMin + xBorder && point.X < envelope.XMax - xBorder &&
                        point.Y > envelope.YMin + yBorder && point.Y < envelope.YMax - yBorder;

          if (!inside)
          {
            Camera camera = new Camera
            {
              X = point.X,
              Y = point.Y,
              Z = point.Z,
              SpatialReference = point.SpatialReference
            };

            await QueuedTask.Run(() =>
            {
              thisView.PanTo(camera);
            });
          }
        }
      }
    }

    private async void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

      if (Api != null && await Api.GetApiReadyState())
      {
        switch (propertyName)
        {
          case "Location":
            await OpenImageAsync();
            break;
          case "IsActive":
            if (!IsActive)
            {
              IList<IViewer> viewers = await Api.GetViewers();

              foreach (IViewer viewer in viewers)
              {
                string viewerId = await viewer.GetId();
                await Api.CloseViewer(viewerId);
              }
            }

            break;
        }
      }
    }

    internal static StreetSmart Show()
    {
      StreetSmart streetSmart = FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

      if (!(streetSmart?.IsVisible ?? true))
      {
        streetSmart.Activate();
      }

      return streetSmart;
    }

    #endregion

    #region Event handlers

    private void OnProjectClosed(ProjectEventArgs args)
    {
      DoHide();
    }

    private async void ApiReady(object sender, EventArgs args)
    {
      await InitApi();
    }

    private async Task InitApi()
    {
      string epsgCode = CoordSystemUtils.CheckCycloramaSpatialReference();
      IAddressSettings addressSettings = AddressSettingsFactory.Create(_constants.AddressLanguageCode, _constants.AddressDatabase);
      IDomElement element = DomElementFactory.Create();
      _options = _configuration.UseDefaultConfigurationUrl
        ? OptionsFactory.Create(_login.Username, _login.Password, _apiKey.Value, epsgCode, addressSettings, element)
        : OptionsFactory.Create(_login.Username, _login.Password, _apiKey.Value, epsgCode, string.Empty,
          _configuration.ConfigurationUrlLocation, addressSettings, element);

      try
      {
        await Api.Init(_options);
        GlobeSpotterConfiguration.Load();
        _measurementList.Api = Api;

        _cycloMediaGroupLayer.PropertyChanged += OnGroupLayerPropertyChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;

        if (string.IsNullOrEmpty(Location))
        {
          DoHide();
        }
        else
        {
          await OpenImageAsync();
        }
      }
      catch (StreetSmartLoginFailedException)
      {
        MessageBox.Show(ThisResources.StreetSmartOnLoginFailed, ThisResources.StreetSmartOnLoginFailed,
          MessageBoxButton.OK, MessageBoxImage.Error);
        DoHide();
      }
    }

    private async void ViewerAdded(object sender, IEventArgs<IViewer> args)
    {
      IViewer cyclViewer = args.Value;

      if (cyclViewer is IPanoramaViewer)
      {
        IPanoramaViewer panoramaViewer = cyclViewer as IPanoramaViewer;
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomIn, false);
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomOut, false);

        IRecording recording = await panoramaViewer.GetRecording();
        string imageId = recording.Id;
        _viewerList.Add(panoramaViewer, imageId);

        // ToDo: set culture: date and time
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);
        ICoordinate coordinate = recording.XYZ;
        IOrientation orientation = await panoramaViewer.GetOrientation();
        Color color = await panoramaViewer.GetViewerColor();
        await viewer.SetAsync(coordinate, orientation, color);

        if (_openNearest.Contains(imageId))
        {
          // ToDo: get pitch and draw marker in the Cyclorama
          viewer.HasMarker = true;
          _openNearest.Remove(imageId);
        }

        if (LookAt != null)
        {
          await panoramaViewer.LookAtCoordinate(LookAt);
          LookAt = null;
        }

        await MoveToLocationAsync(panoramaViewer);

        panoramaViewer.ImageChange += OnImageChange;
        panoramaViewer.ViewChange += OnViewChange;
      }
      else if (cyclViewer is IObliqueViewer)
      {
        IObliqueViewer obliqueViewer = cyclViewer as IObliqueViewer;
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomIn, false);
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomOut, false);
      }
    }

    private async void ViewerRemoved(object sender, IEventArgs<IViewer> args)
    {
      IViewer cyclViewer = args.Value;

      if (cyclViewer is IPanoramaViewer)
      {
        IPanoramaViewer panoramaViewer = cyclViewer as IPanoramaViewer;
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);
        panoramaViewer.ImageChange -= OnImageChange;

        if (viewer != null)
        {
          bool hasMarker = viewer.HasMarker;
          _viewerList.Delete(panoramaViewer);

          if (hasMarker)
          {
            List<Viewer> markerViewers = _viewerList.MarkerViewers;

            if (markerViewers.Count == 0 && _crossCheck != null)
            {
              _crossCheck.Dispose();
              _crossCheck = null;
            }
          }
        }

        panoramaViewer.ImageChange -= OnImageChange;
        panoramaViewer.ViewChange -= OnViewChange;
      }

      if (Api != null && !_inRestart)
      {
        IList<IViewer> viewers = await Api.GetViewers();
        int nrViewers = viewers.Count;

        if (nrViewers == 0)
        {
          DoHide();
          _lastSpatialReference = null;
        }
      }
    }

    private async void OnConfigurationPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      switch (args.PropertyName)
      {
        case "UseDefaultConfigurationUrl":
        case "ConfigurationUrlLocation":
        case "UseDefaultStreetSmartUrl":
        case "StreetSmartLocation":
        case "UseProxyServer":
        case "ProxyAddress":
        case "ProxyPort":
        case "ProxyBypassLocalAddresses":
        case "ProxyUseDefaultCredentials":
        case "ProxyUsername":
        case "ProxyPassword":
        case "ProxyDomain":
          await RestartStreetSmart(true);
          break;
      }
    }

    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      switch (args.PropertyName)
      {
        case "Credentials":
          if (!_login.Credentials && Api != null && await Api.GetApiReadyState())
          {
            DoHide();
          }

          if (_login.Credentials)
          {
            await RestartStreetSmart(false);
          }

          break;
      }
    }

    private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      switch (args.PropertyName)
      {
        case "CycloramaViewerCoordinateSystem":
          await RestartStreetSmart(false);
          break;
      }
    }

    private void OnGroupLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaGroupLayer groupLayer && args.PropertyName == "Count")
      {
        foreach (CycloMediaLayer layer in groupLayer)
        {
          // Todo: about add / remove layer
        }
      }
    }

    private async void OnImageChange(object sender, IEventArgs<object> args)
    {
      if (sender is IPanoramaViewer panoramaViewer && Api != null)
      {
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);

        if (viewer != null)
        {
          IRecording recording = await panoramaViewer.GetRecording();
          IOrientation orientation = await panoramaViewer.GetOrientation();
          Color color = await panoramaViewer.GetViewerColor();

          ICoordinate coordinate = recording.XYZ;
          string imageId = recording.Id;

          viewer.ImageId = imageId;
          await viewer.SetAsync(coordinate, orientation, color);

          await MoveToLocationAsync(panoramaViewer);

          if (viewer.HasMarker)
          {
            viewer.HasMarker = false;
            List<Viewer> markerViewers = _viewerList.MarkerViewers;

            if ((markerViewers.Count == 0) && (_crossCheck != null))
            {
              _crossCheck.Dispose();
              _crossCheck = null;
            }
          }
        }
      }
    }

    private async void OnViewChange(object sender, IEventArgs<IOrientation> args)
    {
      if (sender is IPanoramaViewer panoramaViewer)
      {
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);

        if (viewer != null)
        {
          IOrientation orientation = args.Value;
          await viewer.UpdateAsync(orientation);
        }
      }
    }

    #endregion
  }
}
