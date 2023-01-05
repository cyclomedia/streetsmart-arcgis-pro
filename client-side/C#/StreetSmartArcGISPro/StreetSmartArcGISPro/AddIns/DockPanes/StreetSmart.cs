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

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmart.Common.Exceptions;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.DomElement;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Events;
using StreetSmart.Common.Interfaces.GeoJson;
using StreetSmart.Common.Interfaces.SLD;

using StreetSmartArcGISPro.AddIns.Views;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;

using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;
using Login = StreetSmartArcGISPro.Configuration.File.Login;
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
    private bool _inRestartDock;
    private bool _inClose;
    private string _epsgCode;
    private ICoordinate _lookAt;
    private IOptions _options;
    private MapView _mapView;
    private MapView _oldMapView;
    private readonly IList<string> _configurationPropertyChanged;
    private IList<string> _toRestartImages;

    private static bool _fromShow;
    private static bool _fromConstructor;

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

    //GC: adding global variable that shows if the map was changed to restart the API or not
    public static bool _restart = false;
    #endregion

    #region Constructor

    static StreetSmart()
    {
      _fromShow = false;
      _fromConstructor = false;
    }

    protected StreetSmart()
    {
      if (!_fromShow)
      {
        _fromConstructor = true;
      }

      _storedLayerList = StoredLayerList.Instance;
      ProjectClosedEvent.Subscribe(OnProjectClosed);
      _currentDispatcher = Dispatcher.CurrentDispatcher;
      _inRestart = false;
      _inClose = false;
      _inRestartDock = false;
      _vectorLayerInChange = new List<VectorLayer>();

      _languageSettings = LanguageSettings.Instance;
      _languageSettings.PropertyChanged += OnLanguageSettingsChanged;

      _apiKey = ApiKey.Instance;
      _constants = ConstantsViewer.Instance;

      _login = Login.Instance;
      _login.PropertyChanged += OnLoginPropertyChanged;

      _configuration = FileConfiguration.Instance;
      _configuration.PropertyChanged += OnConfigurationPropertyChanged;

      _openNearest = new List<string>();
      _crossCheck = null;
      _lastSpatialReference = null;
      _configurationPropertyChanged = new List<string>();

      GetVectorLayerListAsync();

      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _viewerList = streetSmartModule.ViewerList;
      _measurementList = streetSmartModule.MeasurementList;

      _epsgCode = string.Empty;
      _mapView = MapView.Active;
      _oldMapView = MapView.Active;

      _toRestartImages = new List<string>();

      if (_mapView != null)
      {
        Setting settings = ProjectList.Instance.GetSettings(_mapView);

        if (settings != null)
        {
          settings.PropertyChanged += OnSettingsPropertyChanged;
        }
      }

      InitializeApi();

      MapClosedEvent.Subscribe(OnMapClosedEvent);
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

    protected override async void OnShow(bool isVisible)
    {
      var contentControl = ((Views.StreetSmart)Content).StreetSmartApi;

      if (isVisible && !_inRestartDock && (contentControl.Content == null || _fromConstructor))
      {
        _inRestartDock = true;

        if (Api != null && MapView != null)
        {
          _measurementList.RemoveAll();

          _vectorLayerList.LayerAdded -= OnAddVectorLayer;
          _vectorLayerList.LayerRemoved -= OnRemoveVectorLayer;
          _vectorLayerList.LayerUpdated -= OnUpdateVectorLayer;

          if (_vectorLayerList.ContainsKey(MapView))
          {
            foreach (var vectorLayer in _vectorLayerList[MapView])
            {
              vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
            }

            foreach (var vectorLayer in _vectorLayerList[MapView])
            {
              vectorLayer.Overlay = null;
            }
          }

          foreach (var viewer in _viewerList)
          {
            _toRestartImages.Add(viewer.Value.ImageId);
          }

          _viewerList.RemoveViewers();
          await Api.Destroy(_options);
        }

        contentControl.Content = new StreetSmartApi();
        Initialize();
      }

      base.OnShow(isVisible);
    }

    #endregion

    #region Functions

    private async Task CloseViewersAsync()
    {
      if (!_inClose)
      {
        _inClose = true;

        if (Api != null && await Api.GetApiReadyState())
        {
          IList<IViewer> viewers = await Api.GetViewers();

          if (viewers.Count >= 1)
          {
            try
            {
              await Api.CloseViewer(await viewers[0].GetId());
            }
            catch (StreetSmartCloseViewerException)
            {
            }
          }
        }

        _inClose = false;
      }
    }

    private async void GetVectorLayerListAsync()
    {
      ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
      _vectorLayerList = await streetSmartModule.GetVectorLayerListAsync(MapView.Active);
    }

    private void InitializeApi()
    {
      string cachePath = Path.Combine(FileUtils.FileDir, "Cache");
      try
      {
        IAPISettings settings = CefSettingsFactory.Create(cachePath);
        settings.Locale = _languageSettings.Locale;
        settings.SetDefaultBrowserSubprocessPath();
        StreetSmartAPIFactory.Initialize(settings, true);
      }
      catch(Exception e)
      {
        return;
      }
    }

    private void Initialize()
    {
      if (_login.Credentials)
      {
        try
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
        catch(Exception e)
        {
          return;
        }
      }
      else
      {
        DoHide();
      }
    }

    private void Restart()
    {
      if (_login.Credentials)
      {
        if (Api == null)
        {
          Initialize();
        }
        else if (_configuration.UseDefaultStreetSmartUrl)
        {
          Api.RestartStreetSmart();
        }
        else if (!string.IsNullOrEmpty(_configuration.StreetSmartLocation))
        {
          Api.RestartStreetSmart(_configuration.StreetSmartLocation);
        }
      }
      else
      {
        DoHide();
      }
    }

    private void DoHide()
    {
      try
      {
        _currentDispatcher.Invoke(Hide, DispatcherPriority.ContextIdle);
      }
      catch (TaskCanceledException)
      {
      }
    }

    private async Task RestartStreetSmart(bool reloadApi)
    {
      if (Api == null || await Api.GetApiReadyState())
      {
        _inRestart = true;
        _measurementList.RemoveAll();

        _vectorLayerList.LayerAdded -= OnAddVectorLayer;
        _vectorLayerList.LayerRemoved -= OnRemoveVectorLayer;
        _vectorLayerList.LayerUpdated -= OnUpdateVectorLayer;

        if (_vectorLayerList.ContainsKey(MapView))
        {
          foreach (var vectorLayer in _vectorLayerList[MapView])
          {
            vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
          }

          foreach (var vectorLayer in _vectorLayerList[MapView])
          {
            IOverlay overlay = vectorLayer.Overlay;

            if (overlay != null && Api != null)
            {
              await Api.RemoveOverlay(overlay.Id);
              vectorLayer.Overlay = null;
            }
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

          await Api.Destroy(_options);
        }

        if (reloadApi || Api == null)
        {
          Restart();
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

      _epsgCode = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);
      IList<ViewerType> viewerTypes = new List<ViewerType> { ViewerType.Panorama };
      IPanoramaViewerOptions panoramaOptions = PanoramaViewerOptionsFactory.Create(true, false, true, true, _toRestartImages.Count == 0 && Replace, true);
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

        try
        {
          IList<IViewer> viewers = await Api.Open(toOpen, viewerOptions);

          if (Nearest && _toRestartImages.Count == 0 && toOpen == _location && point != null)
          {
            if (_crossCheck == null)
            {
              _crossCheck = new CrossCheck();
            }

            double size = _constants.CrossCheckSize;
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
                  _openNearest.Add(recording.Id);
                }
              }
            }
          }

          Setting settings = ProjectList.Instance.GetSettings(_mapView);
          MySpatialReference cycloSpatialReference = settings?.CycloramaViewerCoordinateSystem;

          if (cycloSpatialReference != null)
          {
            _lastSpatialReference = cycloSpatialReference.ArcGisSpatialReference ??
                                    await cycloSpatialReference.CreateArcGisSpatialReferenceAsync();
          }
        }
        catch (StreetSmartImageNotFoundException)
        {
          ResourceManager res = ThisResources.ResourceManager;
          string canNotOpenImageTxt = res.GetString("StreetSmartCanNotOpenImage", _languageSettings.CultureInfo);
          MessageBox.Show($"{canNotOpenImageTxt}: {toOpen} ({_epsgCode})",
            canNotOpenImageTxt, MessageBoxButton.OK, MessageBoxImage.Error);
        }
      } while (_toRestartImages.Count > 0 || toOpen != _location);

      _inRestartDock = false;
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
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

      if (Api != null && await Api.GetApiReadyState())
      {
        switch (propertyName)
        {
          case "Location":
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

            if (!string.IsNullOrEmpty(_epsgCode) && _epsgCode != newEpsgCode)
            {
              await RestartStreetSmart(false);
            }
            else
            {
              //GC: This is where the API is restarted after a new map is opened
              await OpenImageAsync();
              if (_restart == true)
              {
                _restart = false;
                await RestartStreetSmart(false);
              }
            }

            break;
          case "IsActive":
            if (!IsActive)
            {
              await CloseViewersAsync();
            }

            break;
        }
      }
    }

    internal static StreetSmart Show()
    {
      _fromShow = true;
      StreetSmart streetSmart = FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

      if (!(streetSmart?.IsVisible ?? true))
      {
        streetSmart.Activate();
      }

      return streetSmart;
    }

    private async Task UpdateVectorLayerAsync()
    {
      if (_vectorLayerList.ContainsKey(MapView))
      {
        //GC: create new list to keep track if duplicates are being added to the map
        List<String> vectors = new List<String>();
        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < _vectorLayerList[MapView].Count; i++)
        {
          VectorLayer vectorLayer = _vectorLayerList[MapView][i];
          if (!vectors.Contains(vectorLayer.Name))
          {
            vectors.Add(vectorLayer.Name);
            await UpdateVectorLayerAsync(vectorLayer);
          }
          //await UpdateVectorLayerAsync(_vectorLayerList[MapView][i]);
        }
      }
    }

    private async Task UpdateVectorLayerAsync(VectorLayer vectorLayer)
    {
      await vectorLayer.GenerateJsonAsync(_mapView);
    }

    private async Task AddVectorLayerAsync(VectorLayer vectorLayer)
    {
      if (vectorLayer.Layer.Map == _mapView.Map)
      {
        Setting settings = ProjectList.Instance.GetSettings(_mapView);
        MySpatialReference cyclSpatRel = settings?.CycloramaViewerCoordinateSystem;
        string srsName = cyclSpatRel?.SRSName;

        if (vectorLayer.Overlay == null && !string.IsNullOrEmpty(srsName))
        {
          string layerName = vectorLayer.Name;
          bool visible = _storedLayerList.GetVisibility(layerName);

          if (!visible)
          {
            await vectorLayer.GeoJsonToOld();
          }

          IFeatureCollection geoJson = vectorLayer.GeoJson;
          IStyledLayerDescriptor sld = vectorLayer.Sld;

          // Feature property escape character sanitation.
          foreach (var feature in geoJson.Features)
          {
            for (int i = 0; i < feature.Properties.Count; i++)
            {
              try
              {
                if(feature.Properties[feature.Properties.Keys.ElementAt(i)].ToString().Contains("\\"))
                {
                  feature.Properties[feature.Properties.Keys.ElementAt(i)] = feature.Properties[feature.Properties.Keys.ElementAt(i)].ToString().Replace("\\", "/");
                }
              }
              catch(Exception e)
              {
                return;
              }
            }
          }

          IGeoJsonOverlay overlay = OverlayFactory.Create(geoJson, layerName, srsName, sld?.SLD, visible);
          overlay = await Api.AddOverlay(overlay);
          StoredLayer layer = _storedLayerList.GetLayer(layerName);

          if (layer == null)
          {
            _storedLayerList.Update(layerName, false);
          }

          vectorLayer.Overlay = overlay;
        }
      }
    }

    private async Task RemoveVectorLayerAsync(VectorLayer vectorLayer)
    {
      IOverlay overlay = vectorLayer?.Overlay;

      if (overlay != null)
      {
        await Api.RemoveOverlay(overlay.Id);
        vectorLayer.Overlay = null;
      }
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
      if (_constants.ShowDevTools)
      {
        Api.ShowDevTools();
      }

      string epsgCode = CoordSystemUtils.CheckCycloramaSpatialReferenceMapView(_mapView);

      if (!epsgCode.Equals("EPSG:0"))
      {
        IAddressSettings addressSettings =
          AddressSettingsFactory.Create(_constants.AddressLanguageCode, _constants.AddressDatabase);
        IDomElement element = DomElementFactory.Create();
        _options = _configuration.UseDefaultConfigurationUrl
          ? OptionsFactory.Create(_login.Username, _login.Password, _apiKey.Value, epsgCode, _languageSettings.Locale,
            addressSettings, element)
          : OptionsFactory.Create(_login.Username, _login.Password, _apiKey.Value, epsgCode, _languageSettings.Locale,
            _configuration.ConfigurationUrlLocation, addressSettings, element);

        try
        {
          await Api.Init(_options);
          GlobeSpotterConfiguration.Load();
          _measurementList.Api = Api;
          Api.MeasurementChanged += _measurementList.OnMeasurementChanged;

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
            DoHide();
          }
          else
          {
            await OpenImageAsync();
          }
        }
        catch (StreetSmartLoginFailedException)
        {
          ResourceManager res = ThisResources.ResourceManager;
          string loginFailedTxt = res.GetString("StreetSmartOnLoginFailed", _languageSettings.CultureInfo);
          MessageBox.Show(loginFailedTxt, loginFailedTxt, MessageBoxButton.OK, MessageBoxImage.Error);
          DoHide();
        }
      }
    }

    private async void ViewerAdded(object sender, IEventArgs<IViewer> args)
    {
      IViewer cyclViewer = args.Value;
      //GC: added an extra condition in order for the viewing cone to only be created once
      if (cyclViewer is IPanoramaViewer panoramaViewer && _restart == false)
      {
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomIn, false);
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.ZoomOut, false);
        panoramaViewer.ToggleButtonEnabled(PanoramaViewerButtons.Measure, GlobeSpotterConfiguration.MeasurePermissions);

        Setting settings = ProjectList.Instance.GetSettings(_mapView);
        Api.SetOverlayDrawDistance(settings.OverlayDrawDistance);

        IRecording recording = await panoramaViewer.GetRecording();
        string imageId = recording.Id;
        _viewerList.Add(panoramaViewer, imageId);

        // ToDo: set culture: date and time
        Viewer viewer = _viewerList.GetViewer(panoramaViewer);
        ICoordinate coordinate = recording.XYZ;
        IOrientation orientation = await panoramaViewer.GetOrientation();
        Color color = await panoramaViewer.GetViewerColor();
        await viewer.SetAsync(coordinate, orientation, color, _mapView);

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

        try
        {
          await MoveToLocationAsync(panoramaViewer);
        }
        catch(Exception e) { }

        panoramaViewer.ImageChange += OnImageChange;
        panoramaViewer.ViewChange += OnViewChange;
        panoramaViewer.FeatureClick += OnFeatureClick;
        panoramaViewer.LayerVisibilityChange += OnLayerVisibilityChanged;

        foreach (StoredLayer layer in _storedLayerList)
        {
          switch (layer.Name)
          {
            case "addressLayer":
              panoramaViewer.ToggleAddressesVisible(layer.Visible);
              break;
            case "surfaceCursorLayer":
              panoramaViewer.Toggle3DCursor(layer.Visible);
              break;
            case "cyclorama-recordings":
              panoramaViewer.ToggleRecordingsVisible(layer.Visible);
              break;
          }
        }
      }
      else if (cyclViewer is IObliqueViewer obliqueViewer)
      {
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomIn, false);
        obliqueViewer.ToggleButtonEnabled(ObliqueViewerButtons.ZoomOut, false);
      }

      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        await UpdateVectorLayerAsync();
      }
    }

    private async void OnLayerVisibilityChanged(object sender, IEventArgs<ILayerInfo> args)
    {
      ILayerInfo layerInfo = args.Value;
      VectorLayer vectorLayer = _vectorLayerList.GetLayer(layerInfo.LayerId, MapView);
      _storedLayerList.Update(vectorLayer?.Name ?? layerInfo.LayerId, layerInfo.Visible);

      if (vectorLayer != null)
      {
        await UpdateVectorLayer(vectorLayer);
      }
    }

    private async void OnFeatureClick(object sender, IEventArgs<IFeatureInfo> args)
    {
      string id = await(sender as IPanoramaViewer).GetId();
      IFeatureInfo featureInfo = args.Value;
      VectorLayer layer = _vectorLayerList.GetLayer(featureInfo.LayerId, MapView);
      layer?.SelectFeature(featureInfo.FeatureProperties, MapView, id);
    }

    private async void ViewerRemoved(object sender, IEventArgs<IViewer> args)
    {
      IViewer cyclViewer = args.Value;

      if (cyclViewer is IPanoramaViewer panoramaViewer)
      {
        RemovePanoramaViewer(panoramaViewer);
      }
      else
      {
        IList<IViewer> viewers = await Api.GetViewers();
        IList<IViewer> removeList = new List<IViewer>();

        foreach (var keyValueViewer in _viewerList)
        {
          IViewer viewer = keyValueViewer.Key;
          bool exists = viewers.Aggregate(false, (current, viewer2) => viewer2 == viewer || current);

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

      if (Api != null && !_inRestart)
      {
        IList<IViewer> viewers = await Api.GetViewers();
        int nrViewers = viewers.Count;

        if (nrViewers == 0)
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

    private void RemovePanoramaViewer(IPanoramaViewer panoramaViewer)
    {
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
      panoramaViewer.FeatureClick -= OnFeatureClick;
      panoramaViewer.LayerVisibilityChange -= OnLayerVisibilityChanged;
    }

    private async void OnConfigurationPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      switch (args.PropertyName)
      {
        case "Save":
          if (_configurationPropertyChanged.Count >= 1)
          {
            bool restart = false;

            foreach (string configurationProperty in _configurationPropertyChanged)
            {
              switch (configurationProperty)
              {
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
                  restart = true;
                  break;
              }
            }

            await RestartStreetSmart(restart);
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
      switch (args.PropertyName)
      {
        case "Language":
          CefSettingsFactory.SetLanguage(_languageSettings.Locale);
          await RestartStreetSmart(false);
          break;
      }
    }

    private async void OnImageChange(object sender, EventArgs args)
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
            await UpdateVectorLayerAsync();
          }
        }
      }
    }

    private async void OnViewChange(object sender, IEventArgs<IOrientation> args)
    {
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
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        await UpdateVectorLayerAsync();
      }
    }

    private async void OnAddVectorLayer(VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.AddLayerWfs && vectorLayer.Layer.Map == MapView.Map)
      {
        vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
        await UpdateVectorLayerAsync(vectorLayer);
      }
    }

    private async void OnRemoveVectorLayer(VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.AddLayerWfs && vectorLayer.Layer.Map == MapView.Map)
      {
        vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
        await RemoveVectorLayerAsync(vectorLayer);
      }
    }

    private async void OnVectorLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {//GC: this is where layer list toggle can be found
      if (GlobeSpotterConfiguration.AddLayerWfs)
      {
        if (sender is VectorLayer vectorLayer)
        {
          switch (args.PropertyName)
          {
            case "GeoJson":
              await UpdateVectorLayer(vectorLayer);
              break;
          }
          //GC: checks if the layer list visibilty is different from the overlay list visibilty
          //fixes Pro crashing bug because the overlay was null
          if (vectorLayer.Overlay != null)
          {
            if ((vectorLayer.IsVisible && !vectorLayer.Overlay.Visible) || (!vectorLayer.IsVisible && vectorLayer.Overlay.Visible))
            {
              await UpdateVectorLayer(vectorLayer, sender, true);
            }
          }
        }
      }
    }

    private async Task UpdateVectorLayer(VectorLayer vectorLayer)
    {
      if ((vectorLayer.Overlay == null || vectorLayer.GeoJsonChanged) && !_vectorLayerInChange.Contains(vectorLayer))
      {
        _vectorLayerInChange.Add(vectorLayer);

        try
        {
          //GC: checks if the sender is a panoramaViewer in order to call the ToggleOverlay function
          if (sender is IPanoramaViewer panoramaViewer)
          {
            //checks if the overlay is invisible to call the vector layer reset function
            if (_invisible)
            {
              await RemoveVectorLayerAsync(vectorLayer);
              await AddVectorLayerAsync(vectorLayer);
              _invisible = false;
            }
            else
            {
              //calls the toggle overlay function to turn on/off the overlay which should show up without having to move first
              panoramaViewer.ToggleOverlay(vectorLayer.Overlay);
            }

          }
          else
          {
            //calls the vector layer reset function for the initial set up or if the selected overlay is still visible
            //another crash fix because the overlay was undefined
            if (vectorLayer.Overlay == null)
            {
              await RemoveVectorLayerAsync(vectorLayer);
              await AddVectorLayerAsync(vectorLayer);
            }else if(vectorLayer.Overlay.Visible)
            {
              await RemoveVectorLayerAsync(vectorLayer);
              await AddVectorLayerAsync(vectorLayer);
            }
            else
            {
              //turns the global variable ON if the selected overlay is invisible
              _invisible = true;
            }
          }
        }
        catch (Exception)
        {
        }

        _vectorLayerInChange.Remove(vectorLayer);
      }
    }

    private async void OnMapClosedEvent(MapClosedEventArgs args)
    {
      if (args.MapPane.MapView == MapView)
      {
        await CloseViewersAsync();
        _restart = true;
      }
    }

    #endregion
  }
}
