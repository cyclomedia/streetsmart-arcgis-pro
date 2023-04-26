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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.Events;
using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.VectorLayers;

using ArcGISGeometryType = ArcGIS.Core.Geometry.GeometryType;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;
using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using System.Diagnostics.Metrics;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  class MeasurementList : Dictionary<string, Measurement>
  {
    #region Members

    private bool _drawingSketch;
    private VectorLayer _lastVectorLayer;
    private bool _lastSketch;

    #endregion

    #region Properties

    public long? ObjectId { get; set; }

    public Measurement Sketch { get; set; }
    public Measurement Open { get; set; }
    public IStreetSmartAPI Api { get; set; }

    public bool FromMap { get; set; }

    public EventWaitHandle InUpdateMeasurementMode { get; set; }

    public IFeatureCollection FeatureCollection { get; set; }

    #endregion

    #region Constructor

    public MeasurementList()
    {
      InUpdateMeasurementMode = new AutoResetEvent(true);
      Open = null;
      Sketch = null;
      _drawingSketch = false;
      ObjectId = null;
      _lastVectorLayer = null;
      _lastSketch = false;
      FromMap = false;
    }

    #endregion

    #region Functions

    public void CloseOpenMeasurement()
    {
      Open?.CloseMeasurement();
    }

    public void RemoveAll()
    {
      while (Count >= 1)
      {
        var element = this.ElementAt(0);
        Measurement measurement = element.Value;
        measurement.RemoveMeasurement();
        Remove(element.Key);
      }
    }

    public void OpenMeasurement(string entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: open measurement en set focus
      }
    }

    public void AddMeasurementPoint(string entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: add measurement point
      }
    }

    public async Task CreateMeasurement(ArcGISGeometryType geometryType)
    {
      if (Api != null)
      {
        MeasurementGeometryType measurementGeometryType = MeasurementGeometryType.Unknown;

        switch (geometryType)
        {
          case ArcGISGeometryType.Point:
            if (GlobeSpotterConfiguration.MeasurePoint)
            {
              measurementGeometryType = MeasurementGeometryType.Point;
            }

            break;
          case ArcGISGeometryType.Polyline:
            if (GlobeSpotterConfiguration.MeasureLine)
            {
              measurementGeometryType = MeasurementGeometryType.LineString;
            }

            break;
          case ArcGISGeometryType.Polygon:
            if (GlobeSpotterConfiguration.MeasurePolygon)
            {
              measurementGeometryType = MeasurementGeometryType.Polygon;
            }

            break;
        }

        if (measurementGeometryType != MeasurementGeometryType.Unknown)
        {
          ModulestreetSmart streetSmartModule = ModulestreetSmart.Current;
          ViewerList viewerList = streetSmartModule.ViewerList;
          IPanoramaViewer panoramaViewer = viewerList.ActiveViewer;

          if (panoramaViewer != null)
          {
            if (Count == 0)
            {
              Measurement measurement = new Measurement(null, null, Api)
              {
                VectorLayer = _lastVectorLayer
              };

              Add(new Guid().ToString(), measurement);
              measurement.Open();
            }

            Measurement measurement2 = this.ElementAt(0).Value;
            measurement2.VectorLayer = _lastVectorLayer;
            measurement2.SetSketch();
            measurement2.IsDisposed = false;
            FromMap = true;

            IMeasurementOptions options = MeasurementOptionsFactory.Create(measurementGeometryType);
            await Api.StartMeasurementMode(panoramaViewer, options);
          }
        }
      }
    }

    public void SketchFinished()
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        _drawingSketch = false;
        Sketch = null;
      }
    }

    public async Task SketchModifiedAsync(MapView mapView, VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Measurement measurement = Sketch;
        Geometry geometry = await mapView.GetCurrentSketchAsync();

        if (geometry != null)
        {
          if (!_drawingSketch && !geometry.IsEmpty || measurement == null)
          {
            _drawingSketch = true;
            measurement = await StartMeasurement(geometry, measurement, true, vectorLayer);
          }

          if (measurement != null)
          {
            await measurement.UpdateMeasurementPointsAsync(mapView, null);
          }
        }
      }
    }

    public async Task<Measurement> StartMeasurement(Geometry geometry, Measurement measurement, bool sketch, VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        bool measurementExists = false;
        ArcGISGeometryType geometryType = geometry?.GeometryType ?? ArcGISGeometryType.Unknown;

        if (geometryType == ArcGISGeometryType.Point || geometryType == ArcGISGeometryType.Polygon ||
            geometryType == ArcGISGeometryType.Polyline)
        {
          if (measurement?.IsGeometryType(geometryType) ?? false)
          {
            measurementExists = true;
            measurement.OpenMeasurement();
          }

          if (!measurementExists)
          {
            CloseOpenMeasurement();
            _lastVectorLayer = vectorLayer;
            _lastSketch = sketch;
            await CreateMeasurement(geometryType);
          }
        }
      }

      return measurement;
    }

    #endregion

    #region streetSmart events

    public void OnMeasurementStarted(object sender, IEventArgs<IFeatureCollection> args)
    {
      FeatureCollection = args.Value;
    }

    public async void OnMeasurementStopped(object sender, IEventArgs<IFeatureCollection> args)
    {
      FeatureCollection = args.Value;

      foreach (IFeature feature in FeatureCollection.Features)
      {
        IGeometry geometry = feature.Geometry;
        StreetSmartGeometryType geometryType = geometry.Type;
        IStreetSmartAPI api = sender as IStreetSmartAPI;
        Measurement measurement;

        if (feature.Properties is IMeasurementProperties properties)
        {
          if (Count == 0)
          {
            measurement = new Measurement(properties, feature.Geometry, api)
            {
              VectorLayer = _lastVectorLayer
            };

            Add(properties.Id, measurement);
            measurement.Open();

            if (_lastSketch)
            {
              measurement.SetSketch();
            }
          }
          else
          {
            measurement = this.ElementAt(0).Value;
          }

          MapView mapView = MapView.Active;
          Geometry geometrySketch = await mapView.GetCurrentSketchAsync();

          switch (geometryType)
          {
            case StreetSmartGeometryType.Point:
              RemoveLineStringPoints(measurement);
              RemovePolygonPoints(measurement);
              await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
              await mapView.ClearSketchAsync();
              measurement.Dispose();
              break;
            case StreetSmartGeometryType.Polygon:
              RemovePointPoints(measurement);
              RemoveLineStringPoints(measurement);
              await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
              await mapView.ClearSketchAsync();

              if (geometrySketch != null)
              {
                await QueuedTask.Run(async () =>
                {
                  List<MapPoint> points = new List<MapPoint>();
                  Polygon surface = PolygonBuilderEx.CreatePolygon(points, geometrySketch.SpatialReference);
                  await mapView.SetCurrentSketchAsync(surface);
                });
              }

              measurement.Dispose();
              break;
            case StreetSmartGeometryType.LineString:
              RemovePointPoints(measurement);
              RemovePolygonPoints(measurement);
              await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
              await mapView.ClearSketchAsync();

              if (geometrySketch != null)
              {
                await QueuedTask.Run(async () =>
                {
                  List<MapPoint> points = new List<MapPoint>();
                  Polyline line = PolylineBuilderEx.CreatePolyline(points, geometrySketch.SpatialReference);
                  await mapView.SetCurrentSketchAsync(line);
                });
              }

              measurement.Dispose();
              break;
          }
        }
      }

      if (FeatureCollection.Type == FeatureType.Unknown)
      {
        if (Count == 1)
        {
          string currentTool = FrameworkApplication.CurrentTool;

          switch (currentTool)
          {
            case "esri_editing_SketchLineTool":
            case "esri_editing_SketchPolygonTool":
            case "esri_editing_SketchPointTool":
              await FrameworkApplication.SetCurrentToolAsync(string.Empty);
              break;
            case "esri_editing_ModifyFeatureImpl":
              var geometry = await MapView.Active.GetCurrentSketchAsync();

              if (geometry != null)
              {
                if (geometry.GeometryType == ArcGISGeometryType.Polygon ||
                    geometry.GeometryType == ArcGISGeometryType.Polyline)
                {
                  await MapView.Active.ClearSketchAsync();
                }
              }

              break;
          }
        }
      }
    }

    public async void OnMeasurementChanged(object sender, IEventArgs<IFeatureCollection> args)
    {
      FeatureCollection = args.Value;
      IStreetSmartAPI api = sender as IStreetSmartAPI;

      foreach (IFeature feature in FeatureCollection.Features)
      {
        if (feature.Properties is IMeasurementProperties properties)
        {
          Measurement measurement;

          if (Count == 0)
          {
            measurement = new Measurement(properties, feature.Geometry, api)
            {
              VectorLayer = _lastVectorLayer
            };

            Add(properties.Id, measurement);
            measurement.Open();

            if (_lastSketch)
            {
              measurement.SetSketch();
            }
          }
          else
          {
            measurement = this.ElementAt(0).Value;
          }

          measurement.ObservationLines = properties.ObservationLines;

          if (measurement.Properties == null)
          {
            measurement.Properties = properties;
          }

          if (measurement.Geometry == null)
          {
            measurement.Geometry = feature.Geometry;
          }

          if (!measurement.UpdateMeasurement)
          {
            measurement.UpdateMeasurement = true;
            IGeometry geometry = feature.Geometry;
            StreetSmartGeometryType geometryType = geometry.Type;

            switch (geometryType)
            {
              case StreetSmartGeometryType.Point:
                RemoveLineStringPoints(measurement);
                RemovePolygonPoints(measurement);

                if (geometry is IPoint pointDst)
                {
                  if (measurement.Count >= 1 && measurement[0].Point != null &&
                      (pointDst.X == null || pointDst.Y == null) && measurement.MeasurementId != properties.Id &&
                      measurement.VectorLayer != null && !FromMap)
                  {
                    MapView mapView = MapView.Active;
                    Geometry geometrySketch = await mapView.GetCurrentSketchAsync();
                    await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
                    await mapView.ClearSketchAsync();
                    measurement.Dispose();
                  }
                  else
                  {
                    measurement.MeasurementId = properties.Id;
                    await measurement.UpdatePointAsync(0, feature);
                    measurement.Geometry = geometry;
                    FromMap = false;
                  }
                }

                await measurement.UpdateMap();

                break;
              case StreetSmartGeometryType.LineString:
                RemovePointPoints(measurement);
                RemovePolygonPoints(measurement);

                if (geometry is ILineString lineDst)
                {
                  if (measurement.Count >= 1 && measurement[0].Point != null &&
                      lineDst.Count == 0 && measurement.MeasurementId != properties.Id &&
                      measurement.VectorLayer != null && !FromMap)
                  {
                    MapView mapView = MapView.Active;
                    Geometry geometrySketch = await mapView.GetCurrentSketchAsync();
                    await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
                    await mapView.ClearSketchAsync();

                    if (geometrySketch != null)
                    {
                      await QueuedTask.Run(async() =>
                      {
                        List<MapPoint> points = new List<MapPoint>();
                        Polyline line = PolylineBuilderEx.CreatePolyline(points, geometrySketch.SpatialReference);
                        await mapView.SetCurrentSketchAsync(line);
                      });
                    }

                    measurement.Dispose();
                  }
                  else if (measurement.Geometry is ILineString lineSrc)
                  {
                    measurement.MeasurementId = properties.Id;

                    for (int i = 0; i < Math.Max(lineDst.Count, lineSrc.Count); i++)
                    {
                      measurement.RemoveObservations(i, feature);

                      if (lineSrc.Count > i && lineDst.Count > i)
                      {
                        await measurement.UpdatePointAsync(i, feature);
                      }
                      else if (lineSrc.Count <= i && lineDst.Count > i)
                      {
                        measurement.AddPoint(lineSrc.Count);
                        await measurement.UpdatePointAsync(i, feature);
                      }
                      else if (lineSrc.Count > i && lineDst.Count <= i)
                      {
                        measurement.RemovePoint(i);
                        await measurement.UpdatePointAsync(Math.Min(i, lineDst.Count - 1), feature);
                      }
                    }

                    measurement.Geometry = geometry;
                    await measurement.UpdateMap();
                  }
                  else
                  {
                    measurement.MeasurementId = properties.Id;

                    for (int i = 0; i < lineDst.Count; i++)
                    {
                      measurement.AddPoint(i);
                      await measurement.UpdatePointAsync(i, feature);
                    }

                    measurement.Geometry = geometry;
                    await measurement.UpdateMap();
                  }
                }

                break;
              case StreetSmartGeometryType.Polygon:
                RemovePointPoints(measurement);
                RemoveLineStringPoints(measurement);

                if (geometry is IPolygon polyDst)
                {
                  if (measurement.Count >= 1 && measurement[measurement.ElementAt(0).Key].Point != null &&
                      polyDst[0].Count == 0 && measurement.MeasurementId != properties.Id &&
                      measurement.VectorLayer != null && !FromMap)
                  {
                    MapView mapView = MapView.Active;
                    Geometry geometrySketch = await mapView.GetCurrentSketchAsync();
                    await measurement.VectorLayer.AddUpdateFeature(ObjectId, geometrySketch, measurement);
                    await mapView.ClearSketchAsync();

                    if (geometrySketch != null)
                    {
                      await QueuedTask.Run(async() =>
                      {
                        List<MapPoint> points = new List<MapPoint>();
                        Polygon polygon = PolygonBuilderEx.CreatePolygon(points, geometrySketch.SpatialReference);
                        await mapView.SetCurrentSketchAsync(polygon);
                      });
                    }

                    measurement.Dispose();
                  }
                  else if (measurement.Geometry is IPolygon polySrc)
                  {
                    measurement.MeasurementId = properties.Id;
                    int polySrcCount = polySrc[0].Count;
                    int pylyDstCount = polyDst[0].Count;
                    int j = 0;

                    for (int i = 0; i < Math.Max(pylyDstCount, polySrcCount); i++)
                    {
                      measurement.RemoveObservations(i, feature);
                      if (polySrcCount > i && pylyDstCount > i)
                      {
                        await measurement.UpdatePointAsync(i, feature);
                      }
                      else if (polySrcCount <= i && pylyDstCount > i)
                      {
                        measurement.AddPoint(polySrcCount++);
                        await measurement.UpdatePointAsync(i, feature);
                      }
                      else if (polySrcCount > i && pylyDstCount <= i)
                      {
                        /*measurement.RemovePoint(i - j); //this is where the number on the map gets removed
                        j++;*/

                        measurement.RemovePoint(i); //GC: fixed where polygon edit won't cancel correctly

                        if (measurement.Count > Math.Min(i, pylyDstCount - 1))
                        {
                          await measurement.UpdatePointAsync(Math.Min(i, pylyDstCount - 1), feature);
                        }
                      }
                    }

                    measurement.Geometry = geometry;
                    await measurement.UpdateMap();
                  }
                  else
                  {
                    measurement.MeasurementId = properties.Id;
                    int pylyDstCount = polyDst[0].Count;

                    for (int i = 0; i < pylyDstCount; i++)
                    {
                      measurement.AddPoint(i);
                      await measurement.UpdatePointAsync(i, feature);
                    }

                    measurement.Geometry = geometry;
                    await measurement.UpdateMap();
                  }
                }

                break;
            }

            measurement.UpdateMeasurement = false;
          }
          else
          {
            measurement.DoChange = true;
          }
        }
      }

      FromMap = false;

      if (FeatureCollection.Type == FeatureType.Unknown)
      {
        if (Count == 1)
        {
          string currentTool = FrameworkApplication.CurrentTool;

          switch (currentTool)
          {
            case "esri_editing_SketchLineTool":
            case "esri_editing_SketchPolygonTool":
            case "esri_editing_SketchPointTool":
              await FrameworkApplication.SetCurrentToolAsync(string.Empty);
              break;
            case "esri_editing_ModifyFeatureImpl":
              var geometry = await MapView.Active.GetCurrentSketchAsync();

              if (geometry != null)
              {
                if (geometry.GeometryType == ArcGISGeometryType.Polygon ||
                    geometry.GeometryType == ArcGISGeometryType.Polyline)
                {
                  await MapView.Active.ClearSketchAsync();
                }
              }

              break;
          }
        }
      }
    }

    public void RemoveLineStringPoints(Measurement measurement)
    {
      if (measurement.Geometry is ILineString)
      {
        while (measurement.Count >= 1)
        {
          measurement.RemovePoint(measurement.ElementAt(0).Key);
        }
      }
    }

    public void RemovePolygonPoints(Measurement measurement)
    {
      if (measurement.Geometry is IPolygon)
      {
        while (measurement.Count >= 1)
        {
          measurement.RemovePoint(measurement.ElementAt(0).Key);
        }
      }
    }

    public void RemovePointPoints(Measurement measurement)
    {
      if (measurement.Geometry is IPoint)
      {
        while (measurement.Count >= 1)
        {
          measurement.RemovePoint(measurement.ElementAt(0).Key);
        }
      }
    }

    #endregion
  }
}
