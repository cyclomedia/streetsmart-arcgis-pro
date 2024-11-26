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

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmart.Common.Exceptions;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.DomElement;
using StreetSmart.Common.Interfaces.Events;
using StreetSmart.Common.Interfaces.GeoJson;
using StreetSmart.Common.Interfaces.SLD;
using StreetSmart.WPF;
using StreetSmartArcGISPro.AddIns.Views;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;
using Login = StreetSmartArcGISPro.Configuration.File.Login;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
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

    private static StreetSmart _streetSmart;
    private string _location;
    private bool _isActive;
    private bool _replace;
    private bool _nearest;
    private bool _inRestart;
    private object _inRestartLockObject = new object();
    private bool _inClose;
    private bool _inUpdateAllVectorLayers;

    private string _epsgCode;
    private ICoordinate _lookAt;
    private IOptions _options;
    private MapView _mapView;
    private MapView _oldMapView;
    private readonly IList<string> _configurationPropertyChanged;
    private IList<string> _toRestartImages;

    private readonly LanguageSettings _languageSettings;
    private readonly ApiKey _apiKey;
    private readonly FileConfiguration _configuration;
    private readonly ConstantsViewer _constants;
    private readonly Login _login;
    private readonly List<string> _openNearest;
    private readonly ViewerList _viewerList;
    private readonly MeasurementList _measurementList;
    private readonly Dispatcher _currentDispatcher;
    private readonly IList<VectorLayer> _vectorLayerInChange;
    private readonly StoredLayerList _storedLayerList;

    private CrossCheck _crossCheck;
    private SpatialReference _lastSpatialReference;
    private VectorLayerList _vectorLayerList;

    #endregion

    #region Constructor

    protected StreetSmart()
    {
      ProjectOpenedEvent.Subscribe(OnProjectOpened);
      _storedLayerList = StoredLayerList.Instance;
      ProjectClosedEvent.Subscribe(OnProjectClosed);
      _currentDispatcher = Dispatcher.CurrentDispatcher;
      _inClose = false;
      _vectorLayerInChange = [];
      _languageSettings = LanguageSettings.Instance;
      _languageSettings.PropertyChanged += OnLanguageSettingsChanged;

      _apiKey = ApiKey.Instance;
      _constants = ConstantsViewer.Instance;

      _login = Login.Instance;
      _login.PropertyChanged += OnLoginPropertyChanged;

      _configuration = FileConfiguration.Instance;
      _configuration.PropertyChanged += OnConfigurationPropertyChanged;

      _openNearest = [];
      _crossCheck = null;
      _lastSpatialReference = null;
      _configurationPropertyChanged = [];

      GetVectorLayerListAsync();

      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _viewerList = streetSmartModule.ViewerList;
      _measurementList = streetSmartModule.MeasurementList;

      _epsgCode = string.Empty;
      _mapView = MapView.Active;
      _oldMapView = MapView.Active;

      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);

      _toRestartImages = [];

      if (_mapView != null)
      {
        Setting settings = ProjectList.Instance.GetSettings(_mapView);

        if (settings != null)
        {
          settings.PropertyChanged += OnSettingsPropertyChanged;
        }
      }


      //New API call
      InitializeApi();
      WpfApi = new WpfApi();
      WpfApi.PropertyChanged += OnApiAdded;

      MapClosedEvent.Subscribe(OnMapClosedEvent);
    }

    #endregion

    #region Properties

    public static StreetSmart Current => _streetSmart ??= FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

    public IStreetSmartAPI Api { get; set; }

    public WpfApi WpfApi { get; set; }

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

    public MapView MapView
    {
      get => _mapView;
      set
      {
        if (_mapView != value)
        {
          _oldMapView = _mapView;

          if (_mapView != null)
          {
            Setting outSettings = ProjectList.Instance.GetSettings(_mapView);

            if (outSettings != null)
            {
              outSettings.PropertyChanged -= OnSettingsPropertyChanged;
            }
          }

          if (value != null)
          {
            Setting inSettings = ProjectList.Instance.GetSettings(value);
            _mapView = value;

            if (inSettings != null)
            {
              inSettings.PropertyChanged += OnSettingsPropertyChanged;
            }
          }

          NotifyPropertyChanged();
        }
      }
    }

    #endregion

    #region Overrides

    protected override void OnActivate(bool isActive)
    {
      IsActive = isActive || _isActive;
      base.OnActivate(isActive);
    }

    protected override async void OnHidden()
    {
      IsActive = false;
      _location = string.Empty;
      _replace = false;
      _nearest = false;

      await CloseViewersAsync();

      base.OnHidden();
    }

    protected override void OnShow(bool isVisible)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnShow):{isVisible}");
      var contentControl = ((Views.StreetSmart)Content).StreetSmartApi;

      if (contentControl.Content == null && isVisible)
      {
        contentControl.Content = new StreetSmartApi();

        Restart();
      }

      base.OnShow(isVisible);

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnShow) Finished");
    }

    #endregion

    #region Functions

    private void OnApiAdded(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnApiAdded):{_configuration.StreetSmartLocation}");

      if (args != null)
      {
        if (args.PropertyName == "Api")
        {
          Api = WpfApi.Api;
          Api.APIReady += OnApiReady;
          Api.ViewerAdded += OnViewerAdded;
          Api.ViewerRemoved += OnViewerRemoved;
          Api.BearerTokenChanged += OnBearerTokenChanged;

          if (!_configuration.UseDefaultStreetSmartUrl && !string.IsNullOrEmpty(_configuration.StreetSmartLocation))
          {
            Api.RestartStreetSmart(_configuration.StreetSmartLocation);
          }
        }
      }
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      MapView = args.IncomingView;
    }

    private async Task CloseViewersAsync()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (CloseViewersAsync)");

      if (!_inClose && Api != null && await Api.GetApiReadyState())
      {
        _inClose = true;

        IList<IViewer> viewers = await Api.GetViewers();

        if (viewers.Any())
        {
          try
          {
            await Api.CloseViewer(await viewers[0].GetId());
          }
          catch (StreetSmartCloseViewerException e)
          {
            EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (CloseViewersAsync): exception: {e}");
          }
        }
        else
        {
          _inClose = false;
        }
      }
    }

    private async void GetVectorLayerListAsync()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (GetVectorLayerListAsync)");
      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _vectorLayerList = await streetSmartModule.GetVectorLayerListAsync(MapView.Active);
    }

    private void InitializeApi()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitializeApi)");
      string cachePath = Path.Combine(FileUtils.FileDir, "Cache");

      try
      {
        IAPISettings settings = CefSettingsFactory.Create(cachePath);
        settings.Locale = _languageSettings.Locale;
        settings.SetDefaultBrowserSubprocessPath();
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitializeApi): Cache: {cachePath}, locale: {_languageSettings.Locale}");
        StreetSmartAPIFactory.Initialize(settings);
      }
      catch (Exception e)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (InitializeApi): exception: {e}");
        return;
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitializeApi) Finished");
    }

    private void Restart()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Initialize): {_configuration.StreetSmartLocation}");

      if (_login.Credentials)
      {
        if (Api != null)
        {
          if (_configuration.UseDefaultStreetSmartUrl)
          {
            Api.RestartStreetSmart();
          }
          else if (!string.IsNullOrEmpty(_configuration.StreetSmartLocation))
          {
            Api.RestartStreetSmart(_configuration.StreetSmartLocation);
          }
        }
      }
      else
      {
        if (Api != null) // TODO: find better place for this
        {
          DoHide();
        }
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Restart) Finished");
    }

    private void DoHide()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Restart)");

      try
      {
        _currentDispatcher.Invoke(Hide, DispatcherPriority.ContextIdle);
      }
      catch (TaskCanceledException e)
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (DoHide) TaskCanceledException: {e}");
      }
      catch (Exception e)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (DoHide) Exception: {e}");
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (DoHide) Finished");
    }

    private async Task RestartStreetSmart(bool reloadApi, bool forceRestart = false)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (RestartStreetSmart), reloadApi: {reloadApi}, forceRestart: {forceRestart}");

      lock (_inRestartLockObject)
      {
        if (_inRestart)
        {
          EventLog.Write(EventLogLevel.Warning, $"Street Smart: (StreetSmart.cs) (RestartStreetSmart) Skipped due to race condition");
          return;
        }

        _inRestart = true;
      }

      if (forceRestart || Api == null || await Api.GetApiReadyState())
      {
        await Destroy(false);

        if (reloadApi || Api == null)
        {
          Restart();
        }
        else
        {
          await InitApi();
        }
      }
      else
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (RestartStreetSmart) Skipped due to current API state");
      }

      lock (_inRestartLockObject)
      {
        _inRestart = false;
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (RestartStreetSmart) Finished");
    }

    private async Task Destroy(bool rememberOpenImages = true)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Destroy)");

      _measurementList.RemoveAll();

      _vectorLayerList.LayerAdded -= OnAddVectorLayer;
      _vectorLayerList.LayerRemoved -= OnRemoveVectorLayer;
      _vectorLayerList.LayerUpdated -= OnUpdateVectorLayer;

      if (MapView != null && _vectorLayerList.ContainsKey(MapView))
      {
        foreach (var vectorLayer in _vectorLayerList[MapView])
        {
          vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
        }

        foreach (var vectorLayer in _vectorLayerList[MapView])
        {
          IOverlay overlay = vectorLayer.Overlay;

          if (overlay != null)
          {
            if (Api != null && await Api.GetApiReadyState())
            {
              await Api.RemoveOverlay(overlay.Id);
            }

            vectorLayer.Overlay = null;
          }
        }
      }

      if (rememberOpenImages)
      {
        foreach (var viewer in _viewerList)
        {
          _toRestartImages.Add(viewer.Value.ImageId);
        }
      }

      _viewerList.RemoveViewers();

      if (Api != null && await Api.GetApiReadyState())
      {
        IList<IViewer> viewers = await Api.GetViewers();

        foreach (IViewer viewer in viewers)
        {
          await Api.CloseViewer(await viewer.GetId());
        }

        _options.SRS = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);
      }

      if (Api != null)
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Destroy) API Destroy Call");

        await Api.Destroy(_options);

        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Destroy) API Destroy Finished");
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Destroy) Finished");
    }

    private async Task OpenImageAsync()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync)");
      MapPoint point = null;

      if (Nearest && _toRestartImages.Count == 0)
      {
        Setting settings = ProjectList.Instance.GetSettings(_mapView);

        MySpatialReference spatialReference = settings.CycloramaViewerCoordinateSystem;
        SpatialReference thisSpatialReference = spatialReference.ArcGisSpatialReference ??
                                                await spatialReference.CreateArcGisSpatialReferenceAsync();

        string[] splitLoc = _location.Split(',');
        CultureInfo ci = CultureInfo.InvariantCulture;
        double x = double.Parse(splitLoc.Length >= 1 ? splitLoc[0] : "0.0", ci);
        double y = double.Parse(splitLoc.Length >= 2 ? splitLoc[1] : "0.0", ci);

        await QueuedTask.Run(() =>
        {
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) Create map point {x}, {y}");
          point = MapPointBuilderEx.CreateMapPoint(x, y, _lastSpatialReference);
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

            EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) Create location {point.X} {point.Y}");
            _location = string.Format(ci, "{0},{1}", point.X, point.Y);
          }
        }
      }

      _epsgCode = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);
      IList<ViewerType> viewerTypes = [ViewerType.Panorama];
      IPanoramaViewerOptions panoramaOptions = PanoramaViewerOptionsFactory.Create(true, false, true, true, _toRestartImages.Count == 0 && Replace, true, 0);
      panoramaOptions.MeasureTypeButtonToggle = false;
      IViewerOptions viewerOptions = ViewerOptionsFactory.Create(viewerTypes, _epsgCode, panoramaOptions);
      string toOpen;

      do
      {
        toOpen = _location;

        if (_toRestartImages.Count > 0)
        {
          toOpen = _toRestartImages[0];
          _toRestartImages.Remove(toOpen);

          if (toOpen == _location && _toRestartImages.Count > 0)
          {
            toOpen = _toRestartImages[0];
            _toRestartImages.Remove(toOpen);
          }
        }

        if (string.IsNullOrEmpty(toOpen))
        {
          continue;
        }

        try
        {
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) Open image: {toOpen}");
          IList<IViewer> viewers = await Api.Open(toOpen, viewerOptions);

          if (Nearest && _toRestartImages.Count == 0 && toOpen == _location && point != null)
          {
            if (_crossCheck == null)
            {
              _crossCheck = new CrossCheck();
            }

            double size = _constants.CrossCheckSize;
            EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) Open cross check: {point.X}, {point.Y}");
            await _crossCheck.UpdateAsync(point.X, point.Y, size);

            foreach (IViewer cyclViewer in viewers)
            {
              if (cyclViewer is IPanoramaViewer panoramaViewer)
              {
                Viewer viewer = _viewerList.GetViewer(panoramaViewer);

                if (viewer != null)
                {
                  viewer.HasMarker = true;
                }
                else
                {
                  IRecording recording = await panoramaViewer.GetRecording();
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) Add to open nearest: {recording.Id}");
                  _openNearest.Add(recording.Id);
                }
              }
            }
          }

          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) get settings");
          Setting settings = ProjectList.Instance.GetSettings(_mapView);
          MySpatialReference cycloSpatialReference = settings?.CycloramaViewerCoordinateSystem;

          if (cycloSpatialReference != null)
          {
            _lastSpatialReference = cycloSpatialReference.ArcGisSpatialReference ??
                                    await cycloSpatialReference.CreateArcGisSpatialReferenceAsync();
          }
        }
        catch (StreetSmartImageNotFoundException e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (OpenImageAsync) StreetSmartCanNotOpenImage {toOpen} {_epsgCode}: error: {e}");
          ResourceManager res = ThisResources.ResourceManager;
          string canNotOpenImageTxt = res.GetString("StreetSmartCanNotOpenImage", _languageSettings.CultureInfo);
          MessageBox.Show($"{canNotOpenImageTxt}: {toOpen} ({_epsgCode})",
            canNotOpenImageTxt, MessageBoxButton.OK, MessageBoxImage.Error);
        }

      } while (_toRestartImages.Count > 0 || toOpen != _location);
    }

    private async Task MoveToLocationAsync(IPanoramaViewer panoramaViewer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (MoveToLocationAsync)");
      IRecording recording = await panoramaViewer.GetRecording();
      ICoordinate coordinate = recording.XYZ;

      if (coordinate != null)
      {
        double x = coordinate.X ?? 0.0;
        double y = coordinate.Y ?? 0.0;
        double z = coordinate.Z ?? 0.0;
        MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z, MapView);
        Envelope envelope = _mapView?.Extent;

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

            await QueuedTask.Run(() => { _mapView.PanTo(camera); });
          }
        }
      }
    }

    protected override async void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (NotifyPropertyChanged) propertyName: {propertyName}");
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

      if (!_login.Credentials || Api == null || !await Api.GetApiReadyState())
      {
        return;
      }

      switch (propertyName)
      {
        case nameof(Location):
          string newEpsgCode = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);

          if (_oldMapView != _mapView)
          {
            if (_oldMapView != null && _vectorLayerList.ContainsKey(_oldMapView))
            {
              foreach (var vectorLayer in _vectorLayerList[_oldMapView])
              {
                vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
              }

              foreach (var vectorLayer in _vectorLayerList[_oldMapView])
              {
                IOverlay overlay = vectorLayer.Overlay;

                if (overlay != null && Api != null)
                {
                  await Api.RemoveOverlay(overlay.Id);
                  vectorLayer.Overlay = null;
                }
              }
            }

            if (_mapView != null && _vectorLayerList.ContainsKey(_mapView))
            {
              foreach (var vectorLayer in _vectorLayerList[_mapView])
              {
                vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
              }
            }
          }

          if (_epsgCode != newEpsgCode)
          {
            await RestartStreetSmart(false);
          }
          else
          {
            await OpenImageAsync();
          }

          break;
        case nameof(IsActive):
          if (!IsActive)
          {
            await CloseViewersAsync();
          }
          break;
      }
    }

    internal static StreetSmart ActivateStreetSmart()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ActivateStreetSmart)");

      StreetSmart streetSmart = FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

      if (!(streetSmart?.IsVisible ?? true))
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ActivateStreetSmart) Will execute");

        streetSmart.Activate();
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ActivateStreetSmart) Finished");

      return streetSmart;
    }

    public async Task SignOutOAuth()
    {
      var keepValue = _options.DoOAuthLogoutOnDestroy;
      _options.DoOAuthLogoutOnDestroy = true;
      await Destroy(false);
      _options.DoOAuthLogoutOnDestroy = keepValue;
    }

    public async Task SignInOAuth()
    {
      if (Api != null)
      {
        await RestartStreetSmart(false, true);
      }
      else
      {
        ActivateStreetSmart();
      }
    }

    internal static StreetSmart Show()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (Show)");
      StreetSmart streetSmart = FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

      if (!(streetSmart?.IsVisible ?? true))
      {
        streetSmart.Activate();
      }

      return streetSmart;
    }

    public async Task UpdateAllVectorLayersAsync()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (UpdateAllVectorLayersAsync)");

      if(_inUpdateAllVectorLayers)
      {
        return;
      }

      if (_vectorLayerList != null)
        if (_vectorLayerList.ContainsKey(MapView))
        {
          _inUpdateAllVectorLayers = true;

          EventLog.Write(EventLogLevel.Debug, $"Street Smart: (StreetSmart.cs) (UpdateAllVectorLayersAsync) Will execute");

          if (_vectorLayerList[MapView].All(x => x.VisibilityChangeStatus != VectorLayer.VectorLayerVisibilityChangeStatus.InUpdate))
          {
            EventLog.Write(EventLogLevel.Debug, $"Street Smart: (StreetSmart.cs) (UpdateAllVectorLayersAsync) Update vector layer will be executed");

            for (int i = 0; i < _vectorLayerList[MapView].Count; i++)
            {
              VectorLayer vectorLayer = _vectorLayerList[MapView][i];

              EventLog.Write(EventLogLevel.Debug, $"Street Smart: (StreetSmart.cs) (UpdateAllVectorLayersAsync) Update vector layer: " + vectorLayer.NameAndUri);

              await UpdateVectorLayerAsync(vectorLayer);
            }
          }
          _inUpdateAllVectorLayers = false;
        }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (UpdateAllVectorLayersAsync) Finished");
    }



    private async Task UpdateVectorLayerAsync(VectorLayer vectorLayer, bool forceUpdateGeoJson = false)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart:  (StreetSmart.cs) (UpdateVectorLayerAsync (VectorLayer))");

      await vectorLayer.GenerateJsonAsync(_mapView, forceUpdateGeoJson);
    }

    private async Task AddOrUpdateVectorLayerOverlayAsync(VectorLayer vectorLayer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart:  (StreetSmart.cs) (AddOrUpdateVectorLayerOverlayAsync)");

      if (vectorLayer.Layer.Map != _mapView.Map)
      {
        return;
      }

      Setting settings = ProjectList.Instance.GetSettings(_mapView);
      MySpatialReference cyclSpatRel = settings?.CycloramaViewerCoordinateSystem;
      string srsName = cyclSpatRel?.SRSName;

      if (!string.IsNullOrEmpty(srsName))
      {
        //GC: create transparency value here
        string layerName = vectorLayer.Name;
        string layerNameAndUri = vectorLayer.NameAndUri;
        bool visible = vectorLayer.DesiredOverlayVisibility;

        IFeatureCollection geoJson = vectorLayer.GeoJson;
        IStyledLayerDescriptor sld = vectorLayer.Sld;

        // Feature property escape character sanitation.
        foreach (var feature in geoJson.Features)
        {
          for (int i = 0; i < feature.Properties.Count; i++)
          {
            try
            {
              if (feature.Properties[feature.Properties.Keys.ElementAt(i)].ToString().Contains("\\"))
              {
                feature.Properties[feature.Properties.Keys.ElementAt(i)] = feature.Properties[feature.Properties.Keys.ElementAt(i)].ToString().Replace("\\", "/");
              }
            }
            catch (Exception e)
            {
              EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (AddOrUpdateVectorLayerOverlayAsync): error: {e}");
              return;
            }
          }
        }

        IGeoJsonOverlay overlay = OverlayFactory.Create(geoJson, layerName, srsName, sld?.GetSerializedSld(), visible);

        if (vectorLayer.Overlay == null)
        {
          overlay = await Api.AddOverlay(overlay);
        }
        else
        {
          overlay.Id = vectorLayer.Overlay.Id;
          overlay = await Api.UpdateOverlay(overlay);
        }

        vectorLayer.Overlay = overlay;

        StoredLayer layer = _storedLayerList.GetLayer(layerNameAndUri);

        if (layer == null || ShouldSyncLayersVisibility())
        {
          _storedLayerList.Update(layerNameAndUri, visible);
        }
      }
    }
    

    public bool ShouldSyncLayersVisibility()
    {
      bool? syncLayerVisibility = ProjectList.Instance.GetSettings(_mapView)?.SyncLayerVisibility;
      var result = syncLayerVisibility ?? _configuration.IsSyncOfVisibilityEnabled;
      return result;
    }

    private async Task RemoveVectorLayerOverlayAsync(VectorLayer vectorLayer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart:  (StreetSmart.cs) (RemoveVectorLayerAsync)");

      IOverlay overlay = vectorLayer?.Overlay;

      if (overlay != null)
      {
        await Api.RemoveOverlay(overlay.Id);
        vectorLayer.Overlay = null;
      }
    }

    #endregion

    #region Event handlers
    private void OnProjectOpened(ProjectEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart:  (StreetSmart.cs) (OnProjectOpened)");

      DoHide();
    }
    private void OnProjectClosed(ProjectEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart:  (StreetSmart.cs) (OnProjectClosed)");

      DoHide();
    }

    private async void OnApiReady(object sender, EventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ApiReady)");

      await InitApi();
    }

    private async Task InitApi()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitApi)");

      if (_constants.ShowDevTools)
      {
        Api.ShowDevTools();
      }

      string epsgCode = "EPSG:4326"; // used for OAuth SignIn when project is not alive yet and no map view at all

      if (_mapView != null)
      {
        epsgCode = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);
      }

      if (!epsgCode.Equals("EPSG:0"))
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitApi) Will execute");

        IAddressSettings addressSettings =
          AddressSettingsFactory.Create(_constants.AddressLanguageCode, _constants.AddressDatabase);
        IDomElement element = DomElementFactory.Create();
        if (_login.IsOAuth)
          if (_login.IsFromSettingsPage)
            _options = OptionsFactory.CreateOauth(_login.OAuthUsername, "DAC6C8E5-77AB-4F04-AFA5-D2A94DE6713F", _apiKey.Value, epsgCode, _languageSettings.Locale, addressSettings, element, loginOauthSilentOnly: false);
          else
            _options = OptionsFactory.CreateOauth(_login.OAuthUsername, "DAC6C8E5-77AB-4F04-AFA5-D2A94DE6713F", _apiKey.Value, epsgCode, _languageSettings.Locale, addressSettings, element, loginOauthSilentOnly: true);
        else
          _options = _configuration.UseDefaultConfigurationUrl
           ? OptionsFactory.Create(_login.Username, _login.Password, _apiKey.Value, epsgCode, _languageSettings.Locale,
             addressSettings, element)
           : OptionsFactory.Create(_login.Username, _login.Password, null, _apiKey.Value, epsgCode, _languageSettings.Locale,
             Web.Instance.ConfigServiceUrl, addressSettings, element);

        try
        {
          await Api.Init(_options);

          _login.CheckAuthenticationStatus(await Api.GetBearerToken());

          if (MapView != null)
          {
            GlobeSpotterConfiguration.Load();

            _measurementList.Api = Api;
            Api.MeasurementChanged += _measurementList.OnMeasurementChanged;
            Api.MeasurementStarted += _measurementList.OnMeasurementStarted;
            Api.MeasurementStopped += _measurementList.OnMeasurementStopped;
            Api.MeasurementSaved += _measurementList.OnMeasurementSaved;

            _vectorLayerList.LayerAdded += OnAddVectorLayer;
            _vectorLayerList.LayerRemoved += OnRemoveVectorLayer;
            _vectorLayerList.LayerUpdated += OnUpdateVectorLayer;

            if (_vectorLayerList.ContainsKey(MapView))
            {
              foreach (var vectorLayer in _vectorLayerList[MapView])
              {
                vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
              }
            }

            if (string.IsNullOrEmpty(Location) && _toRestartImages.Count == 0)
            {
              EventLog.Write(EventLogLevel.Debug, $"Street Smart: (StreetSmart.cs) (InitApi) DoHide call");
              DoHide();
            }
            else
            {
              EventLog.Write(EventLogLevel.Debug, $"Street Smart: (StreetSmart.cs) (InitApi) OpenImageAsync call");
              await OpenImageAsync();
            }
          }
        }
        catch (StreetSmartLoginFailedException e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (InitApi) Login failed: {e}");

          if (_login.IsFromSettingsPage)
          {
            ResourceManager res = ThisResources.ResourceManager;
            string loginFailedTxt = res.GetString("StreetSmartOnLoginFailed", _languageSettings.CultureInfo);
            MessageBox.Show(loginFailedTxt, loginFailedTxt, MessageBoxButton.OK, MessageBoxImage.Error);
          }
          else
          {
            Notification toast = new Notification();
            ResourceManager res = ThisResources.ResourceManager;
            toast.Title = res.GetString("StreetSmartOnLoginFailedTitle", _languageSettings.CultureInfo);
            toast.Message = res.GetString("StreetSmartOnLoginFailedMessage", _languageSettings.CultureInfo);
            toast.ImageSource = Application.Current.Resources["ToastLicensing32"] as System.Windows.Media.ImageSource;
            FrameworkApplication.AddNotification(toast);
          }

          if (_login.IsOAuth)
          {
            DoHide();

            try
            {
              await Destroy();
            }
            catch (Exception ex)
            {
              EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (InitApi) Destroy failed after login failure: {ex}");
            }

            _login.OAuthAuthenticationStatus = Login.OAuthStatus.SignedOut;
            _login.IsOAuth = false;
          }
        }
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (InitApi) Finished");
    }

    private async void OnViewerAdded(object sender, IEventArgs<IViewer> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded)");

      IViewer cyclViewer = args.Value;
      //GC: added an extra condition in order for the viewing cone to only be created once
      if (cyclViewer is IPanoramaViewer panoramaViewer)
      {
        panoramaViewer.ImageChange += OnImageChanged;
        panoramaViewer.ViewChange += OnViewChanged;
        panoramaViewer.FeatureClick += OnFeatureClick;
        panoramaViewer.LayerVisibilityChange += OnOverlayVisibilityChanged;

        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomIn, false);
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomOut, false);
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.Measure, GlobeSpotterConfiguration.MeasurePermissions);

        Setting settings = ProjectList.Instance.GetSettings(_mapView);
        Api.SetOverlayDrawDistance(settings.OverlayDrawDistance);

        IRecording recording = await panoramaViewer.GetRecording();
        string imageId = recording.Id;
        _viewerList.Add(panoramaViewer, imageId);

        Viewer viewer = _viewerList.GetViewer(panoramaViewer);
        ICoordinate coordinate = recording.XYZ;
        IOrientation orientation = OrientationFactory.Create(0, 0, 0);
        Color color = await panoramaViewer.GetViewerColor();
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) set coordinate, orientation and color");
        await viewer.SetAsync(coordinate, orientation, color, _mapView);

        if (_openNearest.Contains(imageId))
        {
          // ToDo: get pitch and draw marker in the Cyclorama
          viewer.HasMarker = true;
          _openNearest.Remove(imageId);
        }

        if (LookAt != null)
        {
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) look at coordinate");
          await panoramaViewer.LookAtCoordinate(LookAt);
          LookAt = null;
        }

        orientation = await panoramaViewer.GetOrientation();
        await viewer.UpdateAsync(orientation);

        try
        {
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) move to location");
          await MoveToLocationAsync(panoramaViewer);
        }
        catch (Exception e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (ViewerAdded) move to location exception: {e}");
        }

        foreach (StoredLayer layer in _storedLayerList)
        {
          switch (layer.Name)
          {
            case "addressLayer":
              //GC: Remove Address Layer if cyclorama not in the Netherlands
              if (_epsgCode == "EPSG:28992")
              {
                EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) add address layer");
                panoramaViewer.ToggleAddressesVisible(layer.Visible);
              }
              else
              {
                EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) remove address layer");
                await Api.RemoveOverlay("addressLayer");
              }
              break;
            case "surfaceCursorLayer":
              EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) toggle surface cursor layer");
              panoramaViewer.Toggle3DCursor(layer.Visible);
              break;
            case "cyclorama-recordings":
              EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) toggle cyclorama recordings");
              panoramaViewer.ToggleRecordingsVisible(layer.Visible);
              break;
          }
        }

        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) Finished panorama");
      }
      else if (cyclViewer is IObliqueViewer obliqueViewer)
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) Toggle oblique");
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomIn, false);
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomOut, false);
      }

      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (ViewerAdded) Toggle vector layer async");
        await UpdateAllVectorLayersAsync();
      }
    }

    private async void OnOverlayVisibilityChanged(object sender, IEventArgs<ILayerInfo> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnOverlayVisibilityChanged)");

      ILayerInfo overlayInfo = args.Value;
      VectorLayer vectorLayer = _vectorLayerList.GetLayer(overlayInfo.LayerId, MapView);

      if (!ShouldSyncLayersVisibility())
        _storedLayerList.Update(vectorLayer?.NameAndUri ?? overlayInfo.LayerId, overlayInfo.Visible);

      if (vectorLayer != null)
      {
        if (vectorLayer.Overlay != null)
        {
          vectorLayer.Overlay.Visible = overlayInfo.Visible;
        }

        if (vectorLayer.VisibilityChangeStatus != VectorLayer.VectorLayerVisibilityChangeStatus.InUpdate)
        {
          vectorLayer.VisibilityChangeStatus = VectorLayer.VectorLayerVisibilityChangeStatus.InUpdate;

          if (ShouldSyncLayersVisibility())
          {
            await QueuedTask.Run(() => vectorLayer.Layer.SetVisibility(overlayInfo.Visible));
          }

          await UpdateVectorLayerAsync(vectorLayer, true);
        }

        vectorLayer.VisibilityChangeStatus = VectorLayer.VectorLayerVisibilityChangeStatus.Updated;
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnOverlayVisibilityChanged) finished");
    }

    private async void OnFeatureClick(object sender, IEventArgs<IFeatureInfo> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnFeatureClick)");

      string id = await (sender as IPanoramaViewer).GetId();
      IFeatureInfo featureInfo = args.Value;
      VectorLayer layer = _vectorLayerList.GetLayer(featureInfo.LayerId, MapView);
      layer?.SelectFeature(featureInfo.FeatureProperties, MapView, id);
    }

    private async void OnViewerRemoved(object sender, IEventArgs<IViewer> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnViewerRemoved)");

      IViewer cyclViewer = args.Value;

      if (cyclViewer is IPanoramaViewer panoramaViewer)
      {
        RemovePanoramaViewer(panoramaViewer);
      }
      else
      {
        IList<IViewer> viewers = await Api.GetViewers();
        IList<IViewer> removeList = [];

        foreach (var keyValueViewer in _viewerList)
        {
          IViewer viewer = keyValueViewer.Key;
          bool exists = viewers.Any(viewer2 => viewer2 == viewer);

          if (!exists)
          {
            removeList.Add(viewer);
          }
        }

        foreach (var viewerRemove in removeList)
        {
          if (viewerRemove is IPanoramaViewer panoramaViewer2)
          {
            RemovePanoramaViewer(panoramaViewer2);
          }
        }
      }

      var proceed = false;

      lock (_inRestartLockObject)
      {
        if (Api != null && !_inRestart)
          proceed = true;
      }

      if (proceed)
      {
        var viewers = await Api.GetViewers();

        if (viewers.Count == 0)
        {
          _inClose = false;
          DoHide();
          _lastSpatialReference = null;
        }
        else if (_inClose)
        {
          await Api.CloseViewer(await viewers[0].GetId());
        }
      }
    }

    private void OnBearerTokenChanged(object sender, IEventArgs<IBearer> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnBearerTokenChanged)");
      Login.Instance.Bearer = args.Value.BearerToken;
    }

    private void RemovePanoramaViewer(IPanoramaViewer panoramaViewer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (RemovePanoramaViewer)");

      Viewer viewer = _viewerList.GetViewer(panoramaViewer);
      panoramaViewer.ImageChange -= OnImageChanged;

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

      panoramaViewer.ImageChange -= OnImageChanged;
      panoramaViewer.ViewChange -= OnViewChanged;
      panoramaViewer.FeatureClick -= OnFeatureClick;
      panoramaViewer.LayerVisibilityChange -= OnOverlayVisibilityChanged;
    }

    private async void OnConfigurationPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnConfigurationPropertyChanged)");
      switch (args.PropertyName)
      {
        case "Save":
          if (_configurationPropertyChanged.Count >= 1)
          {
            bool restartApi = false;
            bool reloadApi = false;

            foreach (string configurationProperty in _configurationPropertyChanged)
            {
              switch (configurationProperty)
              {
                case "UseDefaultConfigurationUrl":
                case "ConfigurationUrlLocation":
                  restartApi = true;
                  break;
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
                  restartApi = true;
                  reloadApi = true;
                  break;
                case "IsSyncOfVisibilityEnabled":
                  break;
              }
            }

            if (restartApi)
            {
              await RestartStreetSmart(reloadApi);
            }
            _configurationPropertyChanged.Clear();
          }

          break;
        default:
          _configurationPropertyChanged.Add(args.PropertyName);
          break;
      }
    }

    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnLoginPropertyChanged)");

      switch (args.PropertyName)
      {
        case "Credentials":

          EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnLoginPropertyChanged) (Credentials) {_login.Credentials}");

          if (!_login.Credentials && Api != null)
          {
            DoHide();
          }

          if (_login.Credentials)
          {
            await RestartStreetSmart(false, true);
          }

          break;
      }
    }

    private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnSettingsPropertyChanged)");

      switch (args.PropertyName)
      {
        case "CycloramaViewerCoordinateSystem":
          await RestartStreetSmart(false);
          break;
        case "OverlayDrawDistance":
          if (Api != null && await Api.GetApiReadyState())
          {
            Setting settings = ProjectList.Instance.GetSettings(_mapView);
            Api.SetOverlayDrawDistance(settings.OverlayDrawDistance);
          }

          break;
      }
    }

    private async void OnLanguageSettingsChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnLanguageSettingsChanged)");

      switch (args.PropertyName)
      {
        case "Language":
          await CefSettingsFactory.SetLanguage(_languageSettings.Locale);
          await RestartStreetSmart(false);
          break;
      }
    }

    private async void OnImageChanged(object sender, EventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnImageChanged)");

      try
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
            await viewer.SetAsync(coordinate, orientation, color, _mapView);

            await MoveToLocationAsync(panoramaViewer);

            if (viewer.HasMarker)
            {
              viewer.HasMarker = false;
              List<Viewer> markerViewers = _viewerList.MarkerViewers;

              if (markerViewers.Count == 0 && _crossCheck != null)
              {
                _crossCheck.Dispose();
                _crossCheck = null;
              }
            }

            if (GlobeSpotterConfiguration.AddLayerWfs)
            {
              await UpdateAllVectorLayersAsync();
            }
          }
        }
      }
      catch (Exception e)
      {
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (OnImageChanged): exception: {e}");
      }
    }

    private async void OnViewChanged(object sender, IEventArgs<IOrientation> args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnViewChanged)");

      if (sender is IPanoramaViewer panoramaViewer)
      {
        _viewerList.ActiveViewer = panoramaViewer;
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);
        if (viewer != null)
        {
          IOrientation orientation = args.Value;
          await viewer.UpdateAsync(orientation);
        }
      }
    }

    private async void OnUpdateVectorLayer()
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnUpdateVectorLayer)");

      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        await UpdateAllVectorLayersAsync();
      }
    }

    private async void OnAddVectorLayer(VectorLayer vectorLayer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnAddVectorLayer)");

      if (GlobeSpotterConfiguration.AddLayerWfs && vectorLayer.Layer.Map == MapView.Map)
      {
        vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
        await UpdateVectorLayerAsync(vectorLayer);
      }
    }

    private async void OnRemoveVectorLayer(VectorLayer vectorLayer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnRemoveVectorLayer)");

      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
        await RemoveVectorLayerOverlayAsync(vectorLayer);
      }
    }

    private async void OnVectorLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnVectorLayerPropertyChanged)");

      if (!GlobeSpotterConfiguration.AddLayerWfs || sender is not VectorLayer vectorLayer)
      {
        return;
      }

      switch (args.PropertyName)
      {
        case nameof(VectorLayer.GeoJson):
          await UpdateVectorLayerOverlay(vectorLayer);
          break;
      }
      
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnVectorLayerPropertyChanged) Finished");
    }

    private async Task UpdateVectorLayerOverlay(VectorLayer vectorLayer)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (UpdateVectorLayerOverlay)");

      bool changeVectorLayer = false;

      lock (_vectorLayerInChange)
      {
        if ((vectorLayer.Overlay == null || vectorLayer.GeoJsonChanged) && !_vectorLayerInChange.Contains(vectorLayer))
        {
          _vectorLayerInChange.Add(vectorLayer);

          changeVectorLayer = true;
        }
      }

      if (changeVectorLayer)
      {
        try
        {
          await AddOrUpdateVectorLayerOverlayAsync(vectorLayer);
        }
        catch (Exception e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (StreetSmart.cs) (UpdateVectorLayer): exception: {e}");
        }

        lock (_vectorLayerInChange)
        {
          _vectorLayerInChange.Remove(vectorLayer);
        }
      }
    }

    private async void OnMapClosedEvent(MapClosedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (StreetSmart.cs) (OnMapClosedEvent)");

      if (args.MapPane.MapView == MapView)
      {
        await CloseViewersAsync();
      }
    }

    #endregion
  }
}
