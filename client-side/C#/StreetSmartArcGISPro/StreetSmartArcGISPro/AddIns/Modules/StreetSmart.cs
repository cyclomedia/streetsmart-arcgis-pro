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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DockPaneStreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using Project = ArcGIS.Desktop.Core.Project;

namespace StreetSmartArcGISPro.AddIns.Modules
{
  internal class StreetSmart : Module
  {
    #region Members

    private static StreetSmart _streetSmart;
    private static LanguageSettings _langSettings;
    private VectorLayerList _vectorLayerList;
    private readonly Agreement _agreement;
    private readonly Dictionary<MapView, CycloMediaGroupLayer> CycloMediaGroupLayer = [];

    #endregion

    #region Properties

    public static IDisposable SentrySdkInit = LogData.Instance.UseSentryLogging ? EventLog.InitializeSentry(LogData.SentryDsnUrl) : null;

    /// <summary>
    /// Retrieve the singleton instance to this module here
    /// </summary>
    public static StreetSmart Current => _streetSmart ??= (StreetSmart)FrameworkApplication.FindModule($"streetSmartArcGISPro_module_{_langSettings.Locale}");

    private static string GroupLayerName => Properties.Resources.ResourceManager.GetString("RecordingLayerGroupName", _langSettings.CultureInfo);

    public readonly ViewerList ViewerList = [];

    public readonly MeasurementList MeasurementList = [];

    #endregion

    #region Constructor

    public StreetSmart()
    {
      _langSettings = LanguageSettings.Instance;
      _agreement = Agreement.Instance;

      if (_agreement.Value)
      {
        FrameworkApplication.State.Activate("streetSmartArcGISPro_agreementAcceptedState");
      }

      var splitId = ID.Split('_');
      string langId = splitId.Length == 0 ? string.Empty : splitId[splitId.Length - 1];
      Language language = Languages.Instance.Get(langId);

      if (language != null)
      {
        _langSettings.Language = language;
        _langSettings.Save();
      }
      else
      {
        _langSettings.Language = Languages.Instance.Get("en-GB");
      }

      MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
      MapClosedEvent.Subscribe(OnMapClosedDocument);
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
      ApplicationStartupEvent.Subscribe(OnApplicationStartupEvent);
      ApplicationClosingEvent.Subscribe(OnApplicationClosingEvent);
    }

    private Task OnApplicationClosingEvent(CancelEventArgs args)
    {
      LogData.Instance.Save();
      return Task.CompletedTask;
    }

    private void OnApplicationStartupEvent(EventArgs args)
    {
      if (Login.Instance.IsOAuth)
      {
        Login.Instance.IsFromSettingsPage = false;
        DockPaneStreetSmart.ActivateStreetSmart();
      }
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Called by Framework when ArcGIS Pro is closing
    /// </summary>
    /// <returns>False to prevent Pro from closing, otherwise True</returns>
    protected override bool CanUnload()
    {
      return true;
    }

    protected override bool Initialize()
    {
      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
      Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
      TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

      return base.Initialize();
    }

    protected override async void Uninitialize()
    {
      foreach (var layer in CycloMediaGroupLayer)
      {
        await RemoveLayersAsync(true, layer.Key);
      }

      MapViewInitializedEvent.Unsubscribe(OnMapViewInitialized);
      MapClosedEvent.Unsubscribe(OnMapClosedDocument);
      base.Uninitialize();
    }

    #endregion

    #region Functions

    public CycloMediaGroupLayer GetOrAddCycloMediaGroupLayer(MapView mapView)
    {
      if (mapView == null)
      {
        return null;
      }

      if (CycloMediaGroupLayer.TryGetValue(mapView, out var groupLayer))
      {
        return groupLayer;
      }

      var result = new CycloMediaGroupLayer(mapView);
      result.PropertyChanged += OnLayerPropertyChanged;
      CycloMediaGroupLayer.Add(mapView, result);
      return result;
    }

    public CycloMediaGroupLayer GetCycloMediaGroupLayer(MapView mapView)
    {
      if (mapView == null)
      {
        return null;
      }

      if (CycloMediaGroupLayer.TryGetValue(mapView, out var result))
      {
        return result;
      }

      return null;
    }

    public IEnumerable<CycloMediaGroupLayer> GetCycloMediaGroupLayers(Map map)
    {
      if (map == null)
      {
        yield break;
      }

      foreach (var cycloMediaGroupLayer in CycloMediaGroupLayer.Values)
      {
        if (cycloMediaGroupLayer.MapView.Map == map)
        {
          yield return cycloMediaGroupLayer;
        }
      }
    }

    internal bool InsideScale(MapView mapView)
    {
      if (mapView == null)
      {
        return false;
      }

      return GetOrAddCycloMediaGroupLayer(mapView).Any(layer => layer.InsideScale);
    }

    private bool ContainsCycloMediaLayer(MapView mapView)
    {
      CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
      return mapView?.Map?.Layers.Any(layer => cycloMediaGroupLayer?.IsKnownName(layer.Name) ?? layer.Name == GroupLayerName) ?? false;
    }

    private bool ContainsCycloMediaLayer(Map map)
    {
      var cycloMediaGroupLayers = GetCycloMediaGroupLayers(map);
      return map?.Layers.Any(layer => cycloMediaGroupLayers?.Any(l => l.IsKnownName(layer.Name)) ?? layer.Name == GroupLayerName) ?? false;
    }

    private async Task CloseCycloMediaLayerAsync(bool closeMap, MapView mapView)
    {
      if (!ContainsCycloMediaLayer(mapView) || closeMap)
      {
        await RemoveLayersAsync(false, mapView);
      }

      if (closeMap)
      {
        CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
        if (cycloMediaGroupLayer != null)
        {
          cycloMediaGroupLayer.PropertyChanged -= OnLayerPropertyChanged;
          CycloMediaGroupLayer.Remove(mapView);
        }

        if (CycloMediaGroupLayer.Count == 0)
        {
          Setting settings = ProjectList.Instance.GetSettings(mapView);
          Login login = Login.Instance;
          LayersRemovedEvent.Unsubscribe(OnLayerRemoved);

          if (settings != null)
          {
            settings.PropertyChanged -= OnSettingsPropertyChanged;
          }

          login.PropertyChanged -= OnLoginPropertyChanged;
        }
      }
    }

    private async Task CloseCycloMediaLayerAsync(Map map)
    {
      if (!ContainsCycloMediaLayer(map))
      {
        await RemoveLayersAsync(map);
      }
    }

    public async Task<VectorLayerList> GetVectorLayerListAsync(MapView mapView)
    {
      _vectorLayerList ??= [];

      if (mapView != null && !_vectorLayerList.ContainsKey(mapView))
      {
        await _vectorLayerList.DetectVectorLayersAsync(mapView);
      }

      return _vectorLayerList;
    }

    public async Task<VectorLayerList> GetVectorLayerListAsync()
    {
      return await GetVectorLayerListAsync(MapView.Active);
    }

    public async Task AddLayersAsync(string name, MapView mapView)
    {
      CycloMediaGroupLayer cycloMediaGroupLayer = GetOrAddCycloMediaGroupLayer(mapView);

      if (cycloMediaGroupLayer.Count == 0)
      {
        await cycloMediaGroupLayer.InitializeAsync();
      }

      if (!string.IsNullOrEmpty(name))
      {
        await cycloMediaGroupLayer.AddAcceptableLayerAsync(name);
      }
    }

    public async Task RemoveLayerAsync(string name, MapView mapView)
    {
      var cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
      await cycloMediaGroupLayer.RemoveLayerAsync(name, true);
    }

    public async Task RemoveLayersAsync(bool fromMap, MapView mapView)
    {
      var cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
      if (cycloMediaGroupLayer != null)
      {
        await cycloMediaGroupLayer.DisposeAsync(fromMap);
      }
    }

    public async Task RemoveLayersAsync(Map map)
    {
      var cycloMediaGroupLayers = GetCycloMediaGroupLayers(map).ToList();
      foreach (var layer in cycloMediaGroupLayers)
      {
        await layer.DisposeAsync(false);
      }
    }

    #endregion

    #region Event handlers

    private async void OnMapViewInitialized(MapViewEventArgs args)
    {
      CycloMediaGroupLayer cycloMediaLayer = GetOrAddCycloMediaGroupLayer(args.MapView);

      if (cycloMediaLayer != null)
      {
        foreach (var layer in cycloMediaLayer)
        {
          CycloMediaLayer.ResetYears(layer.Layer);
        }
      }

      bool addEvents = CycloMediaGroupLayer.Count == 0 ||
                       CycloMediaGroupLayer.Count == 1 &&
                       CycloMediaGroupLayer.ContainsKey(args.MapView);

      if (addEvents)
      {
        LayersRemovedEvent.Subscribe(OnLayerRemoved);
      }

      if (cycloMediaLayer.Count == 0)
      {
        await cycloMediaLayer.InitializeAsync();
      }

      Setting settings = ProjectList.Instance.GetSettings(args.MapView);

      if (settings != null)
      {
        settings.PropertyChanged += OnSettingsPropertyChanged;
      }

      if (addEvents)
      {
        Login login = Login.Instance;
        login.PropertyChanged += OnLoginPropertyChanged;
      }

      if (settings.CycloramaViewerCoordinateSystem != null)
      {
        await CoordSystemUtils.CheckInAreaCycloramaSpatialReferenceAsync(args.MapView);
      }

      if (!_agreement.Value)
      {
        PropertySheet.ShowDialog("streetSmartArcGISPro_optionsPropertySheet", "streetSmartArcGISPro_agreementPage");
      }
    }

    private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "RecordingLayerCoordinateSystem")
      {
        CycloMediaGroupLayer cycloMediaGroupLayer = GetOrAddCycloMediaGroupLayer(MapView.Active);

        foreach (var layer in cycloMediaGroupLayer)
        {
          await layer.UpdateLayerAsync();
        }
      }
    }

    private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (Modules.StreetSmart.cs) (OnLoginPropertyChanged) ({args.PropertyName})");

      if (args.PropertyName == "Credentials")
      {
        Login login = Login.Instance;

        EventLog.Write(EventLogLevel.Information, $"Street Smart: (Modules.StreetSmart.cs) (OnLoginPropertyChanged) (Credentials) {login.Credentials}");

        foreach (CycloMediaGroupLayer cycloMediaGroupLayer in CycloMediaGroupLayer.Values)
        {
          foreach (var layer in cycloMediaGroupLayer)
          {
            if (login.Credentials)
            {
              await layer.RefreshAsync();
            }
            else
            {
              await layer.MakeEmptyAsync();
              Project project = Project.Current;
              await project.SaveEditsAsync();
            }
          }
        }
      }
    }

    private async void OnMapClosedDocument(MapClosedEventArgs args)
    {
      MapView mapView = args.MapPane.MapView;
      await CloseCycloMediaLayerAsync(true, mapView);
    }

    private async void OnLayerRemoved(LayerEventsArgs args)
    {
      var layers = args.Layers;

      foreach (var layer in layers)
      {
        await CloseCycloMediaLayerAsync(layer.Map);
      }
    }

    private async void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Count" && sender is CycloMediaGroupLayer cycloMediaGroupLayer)
      {
        if (!cycloMediaGroupLayer.ContainsLayers)
        {
          await RemoveLayersAsync(true, cycloMediaGroupLayer.MapView);
        }
      }
    }

    public void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      if (args.IncomingView != null)
      {
        CycloMediaGroupLayer groupLayer = GetOrAddCycloMediaGroupLayer(args.IncomingView);

        if (groupLayer.ContainsLayers)
        {
          FrameworkApplication.State.Activate("streetSmartArcGISPro_recordingLayerEnabledState");
        }
        else
        {
          FrameworkApplication.State.Deactivate("streetSmartArcGISPro_recordingLayerEnabledState");
        }
      }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (Modules.StreetSmart.cs) (CurrentDomain_UnhandledException) {e.ExceptionObject}", true);
      Exception ex = e.ExceptionObject as Exception;
      HandleException("CurrentDomain_UnhandledException", ex);
    }

    private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (Modules.StreetSmart.cs) (Current_DispatcherUnhandledException) {e.Exception}", true);
      HandleException("Current_DispatcherUnhandledException", e.Exception);
      //e.Handled = true;   // This can prevent application from crashing, but do we want to keep application running in unhandled state?
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (Modules.StreetSmart.cs) (TaskScheduler_UnobservedTaskException) {e.Exception}",true);
      HandleException("TaskScheduler_UnobservedTaskException", e.Exception);
      //e.SetObserved();  // This can prevent application from crashing, but do we want to keep application running in unhandled state?
    }

    private void HandleException(string exceptionSource, Exception ex)
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (Modules.StreetSmart.cs) (HandleException) ({exceptionSource}) unhandled exception: {ex}");
    }

    #endregion
  }
}
