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
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Editing.Templates;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;

namespace StreetSmartArcGISPro.VectorLayers
{
  #region Delegates

  public delegate void VectorLayerDelegate(VectorLayer layer);
  public delegate void VectorUpdatedDelegate();

  #endregion

  public class VectorLayerList : Dictionary<MapView, List<VectorLayer>>
  {
    #region Events

    public event VectorLayerDelegate LayerAdded;
    public event VectorLayerDelegate LayerRemoved;
    public event VectorUpdatedDelegate LayerUpdated;

    #endregion

    #region Properties

    private readonly MeasurementList _measurementList;
    private string _currentToolId;
    private bool _updateHeight;
    private bool _eventsInitialized;

    //GC: adding global variable that check if the editing tool was closed or not
    private static bool _closeMeasurement = false;

    #endregion

    #region Properties

    public VectorLayer LastSelectedLayer { get; set; }

    public EditTools EditTool { get; private set; }

    #endregion

    #region Constructor

    public VectorLayerList()
    {
      _eventsInitialized = false;
      _updateHeight = false;
      _currentToolId = string.Empty;
      ModuleStreetSmart modulestreetSmart = ModuleStreetSmart.Current;
      _measurementList = modulestreetSmart.MeasurementList;
      EditTool = EditTools.NoEditTool;
      ActiveMapViewChangedEvent.Subscribe(OnMapViewChangedEvent);
    }

    #endregion

    #region Functions

    public VectorLayer GetLayer(Layer layer, MapView mapView)
    {
      var layerList = ContainsKey(mapView) ? this[mapView] : null;
      return layerList?.FirstOrDefault(layerCheck => layerCheck?.Layer == layer);
    }

    public VectorLayer GetLayer(string layerId, MapView mapView)
    {
      if (TryGetValue(mapView, out var layerList))
      {
        return layerList?.FirstOrDefault(layerCheck => (layerCheck?.Overlay?.Id ?? string.Empty) == layerId);
      }

      return null;
    }

    public async Task LoadMeasurementsAsync(MapView mapView)
    {
      var layerList = this[mapView];

      foreach (VectorLayer vectorLayer in layerList)
      {
        await vectorLayer.LoadMeasurementsAsync();
      }
    }

    public async Task DetectVectorLayersAsync(MapView mapView)
    {
      await DetectVectorLayersAsync(true, mapView);
    }

    private async Task DetectVectorLayersAsync(bool initEvents, MapView initMapView = null)
    {
      MapView mapView = initMapView ?? MapView.Active;
      Map map = mapView?.Map;
      IReadOnlyList<Layer> layers = map?.GetLayersAsFlattenedList();

      if (layers != null)
      {
        var addLayerTasks = layers.Select(layer => AddLayerAsync(layer, mapView));
        await Task.WhenAll(addLayerTasks);
      }

      if (initEvents)
      {
        AddEvents();
        MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
        MapClosedEvent.Subscribe(OnMapClosed);
      }
    }

    private async Task AddLayerAsync(Layer layer, MapView mapView)
    {
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      CycloMediaGroupLayer cycloGrouplayer = streetSmart.GetOrAddCycloMediaGroupLayer(mapView);

      if (!TryGetValue(mapView, out List<VectorLayer> layerList))
      {
        layerList = [];
        Add(mapView, layerList);
      }

      if (layer is not FeatureLayer featureLayer || cycloGrouplayer == null || cycloGrouplayer.IsKnownName(featureLayer?.Name) || layerList.Any(vecLayer => vecLayer.Layer == layer))
      {
        return;
      }

      var vectorLayer = new VectorLayer(featureLayer, this);
      if (await vectorLayer.InitializeEventsAsync())
      {
        layerList.Add(vectorLayer);
        LayerAdded?.Invoke(vectorLayer);
      }
    }

    private void AddEvents()
    {
      if (!_eventsInitialized)
      {
        _eventsInitialized = true;
        LayersAddedEvent.Subscribe(OnLayersAdded);
        LayersMovedEvent.Subscribe(OnLayersMoved);
        LayersRemovedEvent.Subscribe(OnLayersRemoved);
        MapMemberPropertiesChangedEvent.Subscribe(OnMapMemberPropertiesChanged);
        TOCSelectionChangedEvent.Subscribe(OnTocSelectionChanged);
        DrawStartedEvent.Subscribe(OnDrawStarted);
        DrawCompleteEvent.Subscribe(OnDrawCompleted);
        ActiveTemplateChangedEvent.Subscribe(OnActiveTemplateChangedEvent);
        ActiveToolChangedEvent.Subscribe(OnActiveToolChangedEvent);
        EditCompletedEvent.Subscribe(OnEditCompleted);
      }
    }

    private async Task StartMeasurementSketchAsync(VectorLayer vectorLayer, MapView mapView)
    {
      Measurement measurement = _measurementList.Sketch;
      Geometry geometry = await mapView.GetCurrentSketchAsync();
      await _measurementList.StartMeasurement(geometry, measurement, true, vectorLayer);
    }

    public async Task StartSketchToolAsync(MapView mapView)
    {
      EditingTemplate editingFeatureTemplate = EditingTemplate.Current;
      Layer layer = editingFeatureTemplate?.Layer;
      VectorLayer vectorLayer = GetLayer(layer, mapView);

      var window = FrameworkApplication.ActiveWindow;
      //GC: Added an additional requirement for measurement tool to activate
      if (vectorLayer != null && (((PlugIn)window).Caption == "Map" || ((PlugIn)window).Caption == "Carte"
        || ((PlugIn)window).Caption == "Create Features" || ((PlugIn)window).Caption == "Créer des entités"))
      {
        await StartMeasurementSketchAsync(vectorLayer, mapView);
      }
    }

    private async Task AddHeightToMeasurementAsync(Geometry geometry, MapView mapView)
    {
      const double e = 0.1;

      switch (geometry.GeometryType)
      {
        case GeometryType.Point:
          if (!_updateHeight)
          {
            _updateHeight = true;
            await UpdateHeightAsync(mapView);
            _updateHeight = false;
          }

          break;
        case GeometryType.Polyline:
          if (!_updateHeight)
          {
            _updateHeight = true;
            await UpdateHeightAsync(mapView);
            Polyline polyline = geometry as Polyline;
            List<MapPoint> mapLinePoints = [];
            bool changesLine = false;

            if (polyline != null)
            {
              foreach (MapPoint point in polyline.Points)
              {
                if (Math.Abs(point.Z) < e)
                {
                  changesLine = true;
                  MapPoint srcLinePoint = await AddHeightToMapPointAsync(point, mapView);
                  mapLinePoints.Add(MapPointBuilderEx.CreateMapPoint(srcLinePoint, polyline.SpatialReference));
                }
                else
                {
                  mapLinePoints.Add(point);
                }
              }

              if (changesLine)
              {
                await QueuedTask.Run(() =>
                {
#if ARCGISPRO29
                  polyline = PolylineBuilder.CreatePolyline(mapLinePoints, polyline.SpatialReference);
#else
                  polyline = PolylineBuilderEx.CreatePolyline(mapLinePoints, polyline.SpatialReference);
#endif
                });

                await mapView.SetCurrentSketchAsync(polyline);
              }
            }

            _updateHeight = false;
          }

          break;
        case GeometryType.Polygon:
          if (!_updateHeight)
          {
            _updateHeight = true;
            await UpdateHeightAsync(mapView);
            Polygon polygon = geometry as Polygon;
            List<MapPoint> mapPolygonPoints = [];
            bool changesPolygon = false;

            if (polygon != null)
            {
              for (int j = 0; j < polygon.Points.Count; j++)
              {
                MapPoint mapPoint = polygon.Points[j];

                if (Math.Abs(mapPoint.Z) < e && j <= polygon.Points.Count - 2)
                {
                  changesPolygon = true;
                  MapPoint srcPolygonPoint = await AddHeightToMapPointAsync(mapPoint, mapView);
                  mapPolygonPoints.Add(srcPolygonPoint);
                }
                else if (changesPolygon && j == polygon.Points.Count - 1)
                {
                  mapPolygonPoints.Add(mapPolygonPoints[0]);
                }
                else
                {
                  mapPolygonPoints.Add(mapPoint);
                }
              }

              if (changesPolygon)
              {
                await QueuedTask.Run(() =>
                {
#if ARCGISPRO29
                  polygon = PolygonBuilder.CreatePolygon(mapPolygonPoints, polygon.SpatialReference);
#else
                  polygon = PolygonBuilderEx.CreatePolygon(mapPolygonPoints, polygon.SpatialReference);
#endif
                });

                await mapView.SetCurrentSketchAsync(polygon);
              }
            }

            _updateHeight = false;
          }

          break;
      }
    }

    public async Task UpdateHeightAsync(MapView mapView)
    {
      await QueuedTask.Run(async () =>
      {
        if (ElevationCapturing.CaptureMode != ElevationCaptureMode.Constant)
        {
          await ElevationCapturing.SetCaptureModeAsync(ElevationCaptureMode.Constant);
        }

        Envelope envelope = mapView.Extent;
        double centerX = (envelope.XMax - envelope.XMin) / 2 + envelope.XMin;
        double centerY = (envelope.YMax - envelope.YMin) / 2 + envelope.YMin;
        double centerZ = (envelope.ZMax - envelope.ZMin) / 2 + envelope.ZMin;
        MapPoint srcPoint = MapPointBuilderEx.CreateMapPoint(centerX, centerY, centerZ);
        MapPoint dstPoint = await AddHeightToMapPointAsync(srcPoint, mapView);
        ElevationCapturing.ElevationConstantValue = dstPoint.Z;
      });
    }

    public async Task<MapPoint> AddHeightToMapPointAsync(MapPoint srcPoint, MapView mapView)
    {
      return await QueuedTask.Run(async () =>
      {
        Map map = mapView.Map;
        SpatialReference srcSpatialReference = map.SpatialReference;
        SpatialReference dstSpatialReference = await CoordSystemUtils.CycloramaSpatialReferenceAsync(mapView);

        ProjectionTransformation dstProjection = ProjectionTransformation.Create(srcSpatialReference,
          dstSpatialReference);

        if (GeometryEngine.Instance.ProjectEx(srcPoint, dstProjection) is MapPoint dstPoint)
        {
          ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
          CycloMediaGroupLayer cycloMediaGroupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(mapView);
          double? height = await cycloMediaGroupLayer.GetHeightAsync(dstPoint.X, dstPoint.Y);

          if (height != null)
          {
            dstPoint = MapPointBuilderEx.CreateMapPoint(dstPoint.X, dstPoint.Y, (double)height, dstSpatialReference);
            ProjectionTransformation srcProjection = ProjectionTransformation.Create(dstSpatialReference,
              srcSpatialReference);
            srcPoint = GeometryEngine.Instance.ProjectEx(dstPoint, srcProjection) as MapPoint;
          }
        }

        return srcPoint;
      });
    }

    public void SketchFinished()
    {
      _measurementList.FromMap = true;

      if (_measurementList.Count >= 1)
      {
        _measurementList[_measurementList.ElementAt(0).Key].Dispose();
      }

      Measurement sketch = _measurementList.Sketch;

      if (sketch != null)
      {
        sketch.CloseMeasurement();
        _measurementList.SketchFinished();
      }

      var features = _measurementList?.FeatureCollection?.Features;

      if (features?.Count >= 1)
      {
        var geometry = features[0].Geometry;

        if ((geometry.Type == StreetSmartGeometryType.Polygon || geometry.Type == StreetSmartGeometryType.LineString) &&
            (EditTool == EditTools.Verticles || EditTool == EditTools.ModifyFeatureImpl))
        {
          _measurementList.Api.StopMeasurementMode();
        }
      }
    }

    #endregion

    #region Edit events

    protected async void OnActiveToolChangedEvent(ToolEventArgs args)
    {
      if (_currentToolId != args.CurrentID)
      {
        _currentToolId = args.CurrentID;

        switch (_currentToolId)
        {
          case "esri_editing_ModifyFeatureImpl":
            EditTool = EditTools.ModifyFeatureImpl;
            break;
          case "esri_editing_ReshapeFeature":
            EditTool = EditTools.ReshapeFeature;
            break;
          case "esri_editing_SketchLineTool":
            EditTool = EditTools.SketchLineTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          case "esri_editing_SketchPolygonTool":
            EditTool = EditTools.SketchPolygonTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          case "esri_editing_SketchPointTool":
            EditTool = EditTools.SketchPointTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          default:
            EditTool = EditTools.NoEditTool;
            SketchFinished();

            //GC: Added new catch statements to fix the change location bug
            if (_currentToolId == "streetSmartArcGISPro_openImageTool" && (args.PreviousID == "esri_editing_SketchPointTool" || args.PreviousID == "esri_editing_SketchPolygonTool"
              || args.PreviousID == "esri_editing_SketchLineTool" || args.PreviousID == "esri_editing_ReshapeFeature" || args.PreviousID == "esri_editing_ModifyFeatureImpl"))
            {
              _closeMeasurement = true;
              await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            }

            if (_currentToolId == "esri_mapping_exploreTool" && args.PreviousID == "streetSmartArcGISPro_openImageTool" && _closeMeasurement == true)
            {
              _closeMeasurement = false;
              await FrameworkApplication.SetCurrentToolAsync("streetSmartArcGISPro_openImageTool");
            }


            if (_measurementList?.Api != null && await _measurementList.Api.GetApiReadyState())
            {
              _measurementList.Api.StopMeasurementMode();
            }

            break;
        }

        if (EditTool == EditTools.NoEditTool)
        {
          FrameworkApplication.State.Deactivate("streetSmartArcGISPro_measurementState");
        }
        else
        {
          FrameworkApplication.State.Activate("streetSmartArcGISPro_measurementState");
        }
      }
    }

    //GC: Added an additional function that fires every time a new template is selected to edit new features on the add-on
    protected async void OnActiveTemplateChangedEvent(ActiveTemplateChangedEventArgs args)
    {
      //GC: Checks if the editing tool was turned off before changing templates
      var active = FrameworkApplication.ActiveTool;
      if (active == null)
      {
        //force the tool to change manually then go back and turn on the sketch tool
        await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");

        if (args.IncomingTemplate != null)
          await FrameworkApplication.SetCurrentToolAsync(args.IncomingTemplate.DefaultToolID);
        else
          EventLog.Write(EventLogLevel.Warning, $"Street Smart: (VectorLayerList.cs) (OnActiveTemplateChangedEvent) IncomingTemplate is null.");
      }

      if (args.IncomingTemplate != null && args.IncomingTemplate.IsActive != false)
      {
        switch (args.IncomingTemplate.DefaultToolID)
        {
          case "esri_editing_ModifyFeatureImpl":
            EditTool = EditTools.ModifyFeatureImpl;
            break;
          case "esri_editing_ReshapeFeature":
            EditTool = EditTools.ReshapeFeature;
            break;
          case "esri_editing_SketchLineTool":
            EditTool = EditTools.SketchLineTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          case "esri_editing_SketchPolygonTool":
            EditTool = EditTools.SketchPolygonTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          case "esri_editing_SketchPointTool":
            EditTool = EditTools.SketchPointTool;
            SketchFinished();
            await StartSketchToolAsync(MapView.Active);
            break;
          default:
            EditTool = EditTools.NoEditTool;
            SketchFinished();

            if (_measurementList?.Api != null && await _measurementList.Api.GetApiReadyState())
            {
              _measurementList.Api.StopMeasurementMode();
            }

            break;
        }

        if (EditTool == EditTools.NoEditTool)
        {
          FrameworkApplication.State.Deactivate("streetSmartArcGISPro_measurementState");
        }
        else
        {
          FrameworkApplication.State.Activate("streetSmartArcGISPro_measurementState");
        }
      }
    }

    protected async void OnDrawStarted(MapViewEventArgs args)
    {
      MapView mapView = args.MapView;
      Geometry geometry = await mapView.GetCurrentSketchAsync();

      if ((geometry?.HasZ ?? false) && EditTool == EditTools.SketchPointTool)
      {
        await AddHeightToMeasurementAsync(geometry, mapView);
      }

      if (geometry != null && EditTool == EditTools.ModifyFeatureImpl)
      {
        EditTool = EditTools.Verticles;
        await _measurementList.StartMeasurement(geometry, null, false, LastSelectedLayer);
        await _measurementList.SketchModifiedAsync(mapView, LastSelectedLayer);
      }
      else if (geometry == null && EditTool == EditTools.Verticles)
      {
        EditTool = EditTools.ModifyFeatureImpl;
      }
    }

    protected async void OnDrawCompleted(MapViewEventArgs args)
    {
      MapView mapView = args.MapView;
      Geometry geometry = await mapView.GetCurrentSketchAsync();
      EditingTemplate editingFeatureTemplate = EditingTemplate.Current;
      Layer layer = editingFeatureTemplate?.Layer;
      VectorLayer thisVectorLayer = GetLayer(layer, mapView) ?? LastSelectedLayer;

      if (geometry != null && thisVectorLayer != null)
      {
        switch (EditTool)
        {
          case EditTools.ModifyFeatureImpl:
            if (_measurementList.Count == 1)
            {
              KeyValuePair<string, Measurement> firstElement = _measurementList.ElementAt(0);
              Measurement measurement = firstElement.Value;
              measurement.SetSketch();
              VectorLayer vectorLayer = measurement.VectorLayer;

              if (geometry.PointCount == 0)
              {
                await StartMeasurementSketchAsync(vectorLayer, mapView);
              }
              else if (geometry.HasZ)
              {
                await AddHeightToMeasurementAsync(geometry, mapView);
              }

              await _measurementList.SketchModifiedAsync(mapView, thisVectorLayer);
            }

            break;
          case EditTools.SketchLineTool:
          case EditTools.SketchPolygonTool:
          case EditTools.Verticles:
            if (geometry.HasZ)
            {
              await AddHeightToMeasurementAsync(geometry, mapView);
            }

            await _measurementList.SketchModifiedAsync(mapView, thisVectorLayer);
            break;
          case EditTools.SketchPointTool:
            if (geometry.HasZ)
            {
              await AddHeightToMeasurementAsync(geometry, mapView);
            }

            break;
        }
      }
      else
      {
        SketchFinished();
      }
    }

    protected Task OnEditCompleted(EditCompletedEventArgs args)
    {
      Measurement measurement = _measurementList?.Sketch;
      VectorLayer vectorLayer = measurement?.VectorLayer;
      FeatureLayer measurementLayer = vectorLayer?.Layer;
      bool completed = args.Members.Select(mapMember => mapMember as FeatureLayer).Any(featureLayer => featureLayer != null && featureLayer == measurementLayer);

      if (completed)
      {
        SketchFinished();
      }

      return Task.FromResult(0);
    }

    #endregion

    #region Event handlers

    private void OnTocSelectionChanged(MapViewEventArgs args)
    {
      LayerUpdated?.Invoke();
    }

    private void OnMapMemberPropertiesChanged(MapMemberPropertiesChangedEventArgs args)
    {
      LayerUpdated?.Invoke();
    }

    private async void OnMapViewInitialized(MapViewEventArgs args)
    {
      await DetectVectorLayersAsync(false, args.MapView);
      AddEvents();
    }

    private async void OnMapViewChangedEvent(ActiveMapViewChangedEventArgs args)
    {
      if (args.IncomingView != null)
      {
        await DetectVectorLayersAsync(args.IncomingView);
      }
    }

    private async void OnMapClosed(MapClosedEventArgs args)
    {
      if (_eventsInitialized)
      {
        _eventsInitialized = false;
        LayersAddedEvent.Unsubscribe(OnLayersAdded);
        LayersMovedEvent.Unsubscribe(OnLayersMoved);
        LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
        MapMemberPropertiesChangedEvent.Unsubscribe(OnMapMemberPropertiesChanged);
        TOCSelectionChangedEvent.Unsubscribe(OnTocSelectionChanged);
        ActiveTemplateChangedEvent.Unsubscribe(OnActiveTemplateChangedEvent);
        ActiveToolChangedEvent.Unsubscribe(OnActiveToolChangedEvent);
        EditCompletedEvent.Unsubscribe(OnEditCompleted);
        DrawCompleteEvent.Unsubscribe(OnDrawCompleted);
        DrawStartedEvent.Unsubscribe(OnDrawStarted);
      }

      MapView mapView = args.MapPane.MapView;

      while (ContainsKey(mapView) && this[mapView].Count >= 1)
      {
        VectorLayer vectorLayer = this[mapView][0];
        await RemoveLayer(vectorLayer, mapView);
      }

      MapViewInitializedEvent.Unsubscribe(OnMapViewInitialized);
      MapClosedEvent.Unsubscribe(OnMapClosed);

      Remove(mapView);
    }

    private async void OnLayersAdded(LayerEventsArgs args)
    {
      foreach (Layer layer in args.Layers)
      {
        MapView mapView = GetMapViewFromLayer(layer) ?? MapView.Active;
        if (mapView != null)
          await AddLayerAsync(layer, mapView);
      }
    }

    private void OnLayersMoved(LayerEventsArgs args)
    {
      LayerUpdated?.Invoke();
    }

    private async void OnLayersRemoved(LayerEventsArgs args)
    {
      foreach (Layer layer in args.Layers)
      {
        if (layer is FeatureLayer featureLayer)
        {
          MapView mapView = GetMapViewFromLayer(layer);
          int i = 0;

          while (mapView != null && ContainsKey(mapView) && this[mapView].Count > i)
          {
            if (this[mapView][i].Layer == featureLayer)
            {
              VectorLayer vectorLayer = this[mapView][i];
              await RemoveLayer(vectorLayer, mapView);
            }
            else
            {
              i++;
            }
          }
        }
      }
    }

    private async Task RemoveLayer(VectorLayer vectorLayer, MapView mapView)
    {
      LayerRemoved?.Invoke(vectorLayer);
      await vectorLayer.DisposeAsync();

      if (ContainsKey(mapView) && this[mapView].Contains(vectorLayer))
      {
        this[mapView].Remove(vectorLayer);
      }
    }

    public MapView GetMapViewFromLayer(Layer layer)
    {
      return GetMapViewFromMap(layer.Map);
    }

    public MapView GetMapViewFromMap(Map map)
    {
      MapView mapView = null;

      foreach (var elem in this)
      {
        mapView = elem.Key.Map == map ? elem.Key : mapView;
      }

      return mapView;
    }

    #endregion
  }
}
