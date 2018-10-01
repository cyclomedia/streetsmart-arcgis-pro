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
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Editing.Templates;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;

namespace StreetSmartArcGISPro.VectorLayers
{
  #region Delegates

  public delegate void VectorLayerDelegate(VectorLayer layer);
  public delegate void VectorUpdatedDelegate();

  #endregion

  public class VectorLayerList : List<VectorLayer>
  {
    #region Events

    public event VectorLayerDelegate LayerAdded;
    public event VectorLayerDelegate LayerRemoved;
    public event VectorUpdatedDelegate LayerUpdated;

    #endregion

    #region Properties

    private readonly MeasurementList _measurementList;
    private readonly CycloMediaGroupLayer _cycloMediaGroupLayer;
    private string _currentToolId;
    private bool _updateHeight;

    #endregion

    #region Properties

    public EditTools EditTool { get; private set; }

    #endregion

    #region Constructor

    public VectorLayerList()
    {
      _updateHeight = false;
      _currentToolId = string.Empty;
      AddIns.Modules.StreetSmart modulestreetSmart = AddIns.Modules.StreetSmart.Current;
      _measurementList = modulestreetSmart.MeasurementList;
      _cycloMediaGroupLayer = modulestreetSmart.CycloMediaGroupLayer;
      EditTool = EditTools.NoEditTool;
    }

    #endregion

    #region Functions

    public VectorLayer GetLayer(Layer layer)
    {
      return this.Aggregate<VectorLayer, VectorLayer>(null,
        (current, layerCheck) => (layerCheck?.Layer == layer) ? layerCheck : current);
    }

    public async Task LoadMeasurementsAsync()
    {
      foreach (VectorLayer vectorLayer in this)
      {
        await vectorLayer.LoadMeasurementsAsync();
      }
    }

    public async Task DetectVectorLayersAsync()
    {
      await DetectVectorLayersAsync(true);
    }

    private async Task DetectVectorLayersAsync(bool initEvents, MapView initMapView = null)
    {
      Clear();
      MapView mapView = initMapView ?? MapView.Active;
      Map map = mapView?.Map;
      IReadOnlyList<Layer> layers = map?.GetLayersAsFlattenedList();

      if (layers != null)
      {
        foreach (var layer in layers)
        {
          await AddLayerAsync(layer);
        }
      }

      if (initEvents)
      {
        AddEvents();
        MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
        MapClosedEvent.Subscribe(OnMapClosed);
      }
    }

    private async Task AddLayerAsync(Layer layer)
    {
      FeatureLayer featureLayer = layer as FeatureLayer;
      AddIns.Modules.StreetSmart streetSmart = AddIns.Modules.StreetSmart.Current;
      CycloMediaGroupLayer cycloGrouplayer = streetSmart?.CycloMediaGroupLayer;

      if (featureLayer != null && cycloGrouplayer != null && !cycloGrouplayer.IsKnownName(featureLayer.Name))
      {
        if (!this.Aggregate(false, (current, vecLayer) => vecLayer.Layer == layer || current))
        {
          var vectorLayer = new VectorLayer(featureLayer, this);
          bool initialized = await vectorLayer.InitializeEventsAsync();

          if (initialized)
          {
            Add(vectorLayer);
            vectorLayer.PropertyChanged += OnVectorLayerPropertyChanged;
            LayerAdded?.Invoke(vectorLayer);
          }
        }
      }
    }

    private void AddEvents()
    {
      LayersAddedEvent.Subscribe(OnLayersAdded);
      LayersMovedEvent.Subscribe(OnLayersMoved);
      LayersRemovedEvent.Subscribe(OnLayersRemoved);
      MapMemberPropertiesChangedEvent.Subscribe(OnMapMemberPropertiesChanged);
      TOCSelectionChangedEvent.Subscribe(OnTocSelectionChanged);
      DrawStartedEvent.Subscribe(OnDrawStarted);
      DrawCompleteEvent.Subscribe(OnDrawCompleted);
      ActiveToolChangedEvent.Subscribe(OnActiveToolChangedEvent);
      EditCompletedEvent.Subscribe(OnEditCompleted);
    }

    private async Task StartMeasurementSketchAsync(VectorLayer vectorLayer)
    {
      Measurement measurement = _measurementList.Sketch;
      MapView mapView = MapView.Active;
      Geometry geometry = await mapView.GetCurrentSketchAsync();
      _measurementList.StartMeasurement(geometry, measurement, true, null, vectorLayer);
    }

    public async Task StartSketchToolAsync()
    {
      EditingTemplate editingFeatureTemplate = EditingTemplate.Current;
      Layer layer = editingFeatureTemplate?.Layer;
      VectorLayer vectorLayer = GetLayer(layer);

      if (vectorLayer?.IsVisibleInstreetSmart ?? false)
      {
        await StartMeasurementSketchAsync(vectorLayer);
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
            List<MapPoint> mapLinePoints = new List<MapPoint>();
            bool changesLine = false;

            if (polyline != null)
            {
              foreach (MapPoint point in polyline.Points)
              {
                if (Math.Abs(point.Z) < e)
                {
                  changesLine = true;
                  MapPoint srcLinePoint = await AddHeightToMapPointAsync(point);
                  mapLinePoints.Add(MapPointBuilder.CreateMapPoint(srcLinePoint, polyline.SpatialReference));
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
                  polyline = PolylineBuilder.CreatePolyline(mapLinePoints, polyline.SpatialReference);
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
            List<MapPoint> mapPolygonPoints = new List<MapPoint>();
            bool changesPolygon = false;

            if (polygon != null)
            {
              for(int j = 0; j < polygon.Points.Count; j++)
              {
                MapPoint mapPoint = polygon.Points[j];

                if (Math.Abs(mapPoint.Z) < e && j <= polygon.Points.Count - 2)
                {
                  changesPolygon = true;
                  MapPoint srcPolygonPoint = await AddHeightToMapPointAsync(mapPoint);
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
                  polygon = PolygonBuilder.CreatePolygon(mapPolygonPoints, polygon.SpatialReference);
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
        double centerX = (envelope.XMax - envelope.XMin)/2 + envelope.XMin;
        double centerY = (envelope.YMax - envelope.YMin)/2 + envelope.YMin;
        double centerZ = (envelope.ZMax - envelope.ZMin)/2 + envelope.ZMin;
        MapPoint srcPoint = MapPointBuilder.CreateMapPoint(centerX, centerY, centerZ);
        MapPoint dstPoint = await AddHeightToMapPointAsync(srcPoint);
        ElevationCapturing.ElevationConstantValue = dstPoint.Z;
      });
    }

    public async Task<MapPoint> AddHeightToMapPointAsync(MapPoint srcPoint)
    {
      return await QueuedTask.Run(async () =>
      {
        MapView mapView = MapView.Active;
        Map map = mapView.Map;
        SpatialReference srcSpatialReference = map.SpatialReference;
        SpatialReference dstSpatialReference = await CoordSystemUtils.CycloramaSpatialReferenceAsync();

        ProjectionTransformation dstProjection = ProjectionTransformation.Create(srcSpatialReference,
          dstSpatialReference);
        MapPoint dstPoint = GeometryEngine.Instance.ProjectEx(srcPoint, dstProjection) as MapPoint;

        if (dstPoint != null)
        {
          double? height = await _cycloMediaGroupLayer.GetHeightAsync(dstPoint.X, dstPoint.Y);

          if (height != null)
          {
            dstPoint = MapPointBuilder.CreateMapPoint(dstPoint.X, dstPoint.Y, ((double)height), dstSpatialReference);
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
      Measurement sketch = _measurementList.Sketch;

      if (sketch != null)
      {
        sketch.CloseMeasurement();
        _measurementList.SketchFinished();
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
            await StartSketchToolAsync();
            break;
          case "esri_editing_SketchPolygonTool":
            EditTool = EditTools.SketchPolygonTool;
            SketchFinished();
            await StartSketchToolAsync();
            break;
          case "esri_editing_SketchPointTool":
            EditTool = EditTools.SketchPointTool;
            SketchFinished();
            await StartSketchToolAsync();
            break;
          default:
            EditTool = EditTools.NoEditTool;
            SketchFinished();
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

      if ((geometry?.HasZ ?? false) && (EditTool == EditTools.SketchPointTool))
      {
        await AddHeightToMeasurementAsync(geometry, mapView);
      }

      if (geometry != null && EditTool == EditTools.ModifyFeatureImpl)
      {
        EditTool = EditTools.Verticles;
        Measurement measurement = _measurementList.Sketch;
        measurement?.OpenMeasurement();
        measurement?.EnableMeasurementSeries();
      }
      else if ((geometry == null) && (EditTool == EditTools.Verticles))
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
      VectorLayer thisVectorLayer = GetLayer(layer);

      if (geometry != null)
      {
        switch (EditTool)
        {
          case EditTools.ModifyFeatureImpl:
            if (_measurementList.Count == 1)
            {
              KeyValuePair<int, Measurement> firstElement = _measurementList.ElementAt(0);
              Measurement measurement = firstElement.Value;
              measurement.SetSketch();
              VectorLayer vectorLayer = measurement.VectorLayer;

              if (geometry.PointCount == 0)
              {
                await StartMeasurementSketchAsync(vectorLayer);
              }
              else if (geometry.HasZ)
              {
                await AddHeightToMeasurementAsync(geometry, mapView);
              }

              await _measurementList.SketchModifiedAsync(geometry, thisVectorLayer);
            }

            break;
          case EditTools.SketchLineTool:
          case EditTools.SketchPolygonTool:
          case EditTools.Verticles:
            if (geometry.HasZ)
            {
              await AddHeightToMeasurementAsync(geometry, mapView);
            }

            await _measurementList.SketchModifiedAsync(geometry, thisVectorLayer);
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
      bool completed = args.Members.Select(mapMember => mapMember as FeatureLayer).Aggregate
        (false, (current, featureLayer)
          => featureLayer != null && featureLayer == measurementLayer || current);

      if (completed)
      {
        SketchFinished();
      }

      return Task.FromResult(0);
    }

    #endregion

    #region Event handlers

    private void OnVectorLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Measurements")
      {
        List<Measurement> totalMeasurements = new List<Measurement>();

        foreach (var vectorLayer in this)
        {
          List<Measurement> measurements = vectorLayer.Measurements;

          if (measurements != null)
          {
            totalMeasurements.AddRange(measurements);
          }
        }

        _measurementList.RemoveUnusedMeasurements(totalMeasurements);
      }
    }

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

    private async void OnMapClosed(MapClosedEventArgs args)
    {
      LayersAddedEvent.Unsubscribe(OnLayersAdded);
      LayersMovedEvent.Unsubscribe(OnLayersMoved);
      LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
      MapMemberPropertiesChangedEvent.Unsubscribe(OnMapMemberPropertiesChanged);
      TOCSelectionChangedEvent.Unsubscribe(OnTocSelectionChanged);
      ActiveToolChangedEvent.Unsubscribe(OnActiveToolChangedEvent);
      EditCompletedEvent.Unsubscribe(OnEditCompleted);
      DrawCompleteEvent.Unsubscribe(OnDrawCompleted);
      DrawStartedEvent.Unsubscribe(OnDrawStarted);

      while (Count >= 1)
      {
        VectorLayer vectorLayer = this[0];
        await RemoveLayer(vectorLayer);
      }
    }

    private async void OnLayersAdded(LayerEventsArgs args)
    {
      foreach (Layer layer in args.Layers)
      {
        await AddLayerAsync(layer);
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
          int i = 0;

          while (Count > i)
          {
            if (this[i].Layer == featureLayer)
            {
              VectorLayer vectorLayer = this[i];
              await RemoveLayer(vectorLayer);
            }
            else
            {
              i++;
            }
          }
        }
      }
    }

    private async Task RemoveLayer(VectorLayer vectorLayer)
    {
      vectorLayer.PropertyChanged -= OnVectorLayerPropertyChanged;
      LayerRemoved?.Invoke(vectorLayer);
      await vectorLayer.DisposeAsync();

      if (Contains(vectorLayer))
      {
        Remove(vectorLayer);
      }
    }

    #endregion
  }
}
