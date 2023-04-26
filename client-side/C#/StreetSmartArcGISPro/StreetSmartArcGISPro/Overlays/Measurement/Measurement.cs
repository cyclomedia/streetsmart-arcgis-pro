﻿/*
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
using System.Threading.Tasks;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.VectorLayers;

using ArcGISGeometryType = ArcGIS.Core.Geometry.GeometryType;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;

using ArcGISSpatialReference = ArcGIS.Core.Geometry.SpatialReference;
using StreetSmartSpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;

using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  public class Measurement : SortedDictionary<int, MeasurementPoint>, IDisposable
  {
    #region Members

    private readonly MeasurementList _measurementList;
    private readonly IStreetSmartAPI _api;

    private ArcGISGeometryType _geometryType;
    private IGeometry _geometry;

    #endregion

    #region Properties

    public string MeasurementId { get; set; }

    public IMeasurementProperties Properties { get; set; }

    public IGeometry Geometry
    {
      get => _geometry;
      set
      {
        _geometry = value;

        if (_geometry != null)
        {
          switch (_geometry.Type)
          {
            case StreetSmartGeometryType.Point:
              _geometryType = ArcGISGeometryType.Point;
              break;
            case StreetSmartGeometryType.Polygon:
              _geometryType = ArcGISGeometryType.Polygon;
              break;
            case StreetSmartGeometryType.LineString:
              _geometryType = ArcGISGeometryType.Polyline;
              break;
            default:
              _geometryType = ArcGISGeometryType.Unknown;
              break;
          }
        }
      }
    }

    public VectorLayer VectorLayer { get; set; }

    public bool IsPointMeasurement => _geometryType == ArcGISGeometryType.Point;

    public bool IsOpen => _measurementList.Open == this;

    public bool IsDisposed { get; set; }

    public bool UpdateMeasurement { get; set; }

    public bool DoChange { get; set; }

    public string EntityId => Properties.Id;

    public IObservationLines ObservationLines { get; set; }

    #endregion

    #region Constructor

    public Measurement(IMeasurementProperties properties, IGeometry geometry, IStreetSmartAPI api)
    {
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      _measurementList = streetSmart.MeasurementList;

      _api = api;
      Properties = properties;
      UpdateMeasurement = false;
      DoChange = false;
      Geometry = geometry;
      IsDisposed = false;
      // SetDetailPanePoint(null);

      if (geometry != null)
      {
        switch (geometry.Type)
        {
          case StreetSmartGeometryType.Point:
            _geometryType = ArcGISGeometryType.Point;
            break;
          case StreetSmartGeometryType.Polygon:
            _geometryType = ArcGISGeometryType.Polygon;
            break;
          case StreetSmartGeometryType.LineString:
            _geometryType = ArcGISGeometryType.Polyline;
            break;
          default:
            _geometryType = ArcGISGeometryType.Unknown;
            break;
        }
      }
    }

    #endregion

    #region Functions

    public void Dispose()
    {
      IsDisposed = true;

//      foreach (var element in this)
//      {
//        MeasurementPoint measurementPoint = element.Value;
//        measurementPoint.Dispose();
//      }

      while (Count >= 1)
      {
        var element = this.ElementAt(0);
        MeasurementPoint measurementPoint = element.Value;
        measurementPoint.Dispose();
        Remove(element.Key);
      }
    }

    public bool IsGeometryType(ArcGISGeometryType geometryType)
    {
      return _geometryType == geometryType;
    }

    public MeasurementPoint GetPointByNr(int nr)
    {
      return Values.Count > nr ? Values.ElementAt(nr) : null;
    }

    public void Close()
    {
      _measurementList.Open = null;

      for (int i = 0; i < Count; i++)
      {
        MeasurementPoint point = this.ElementAt(i).Value;
        point.Dispose();
      }
    }

    public void Open()
    {
      _measurementList.Open = this;
    }

    public void SetSketch()
    {
      _measurementList.Sketch = this;
    }

    public void AddPoint(int pointId)
    {
      if (ContainsKey(pointId))
      {
        this[pointId] = new MeasurementPoint(pointId, this);
      }
      else
      {
        Add(pointId, new MeasurementPoint(pointId, this));
      }
    }

    public MeasurementPoint GetPoint(MapPoint point)
    {
      return Values.Aggregate<MeasurementPoint, MeasurementPoint>
        (null, (current, value) => value.IsSame(point) ? value : current);
    }

    public async Task UpdatePointAsync(int pointId, IFeature apiMeasurementPoint)
    {
      if (pointId >= 0)
      {
        if (!ContainsKey(pointId))
        {
          AddPoint(pointId);
        }

        if (ContainsKey(pointId))
        {
          MeasurementPoint measurementPoint = this[pointId];
          await measurementPoint.UpdatePointAsync(apiMeasurementPoint, pointId);
        }
      }
    }

    public void RemoveObservations(int pointId, IFeature apiMeasurementPoint)
    {
      if (ContainsKey(pointId))
      {
        MeasurementPoint measurementPoint = this[pointId];
        List<string> imageIds = new List<string>();
        IMeasurementProperties properties = apiMeasurementPoint.Properties as IMeasurementProperties;
        IMeasureDetails details = properties?.MeasureDetails?.Count > pointId ? properties.MeasureDetails[pointId] : null;

        if (details?.Details is IDetailsForwardIntersection forwardIntersection)
        {
          for (int i = 0; i < forwardIntersection.ResultDirections.Count; i++)
          {
            imageIds.Add(forwardIntersection.ResultDirections[i].Id);
          }
        }

        List<string> toRemove = new List<string>();

        foreach (string observation in measurementPoint.Keys)
        {
          if (!imageIds.Contains(observation))
          {
            toRemove.Add(observation);
          }
        }

        foreach (string remove in toRemove)
        {
          measurementPoint.RemoveObservation(remove);
        }
      }
    }

    public void CloseMeasurement()
    {
      if (IsOpen && GlobeSpotterConfiguration.MeasurePermissions)
      {
        _measurementList.Open = null;
      }
    }

    public void OpenMeasurement()
    {
      if (!IsOpen && GlobeSpotterConfiguration.MeasurePermissions)
      {
        _measurementList.Open?.CloseMeasurement();
        _measurementList.Open = this;
        _measurementList.OpenMeasurement(EntityId);
      }

      if (IsPointMeasurement && GlobeSpotterConfiguration.MeasurePermissions)
      {
        _measurementList.AddMeasurementPoint(EntityId);
      }
    }

    public void RemoveMeasurement()
    {
      if (IsOpen)
      {
        CloseMeasurement();
      }

      Dispose();
    }

    public void RemovePoint(int pointId)
    {
      if (ContainsKey(pointId))
      {
        MeasurementPoint measurementPoint = this[pointId];
        Remove(pointId);

        for (int i = 0; i < Count; i++)
        {
          MeasurementPoint msPoint = GetPointByNr(i);

          if (msPoint != null)
          {
            msPoint.PointId = i;
          }
        }

        measurementPoint.Dispose();
      }
    }

    public async Task<List<MapPoint>> ToPointCollectionAsync(Geometry geometry)
    {
      List<MapPoint> result = null;

      if (geometry != null)
      {
        double zScale = 1.0;
        double modScale = 1.0;

        await QueuedTask.Run(() =>
        {
          var spatialReference = VectorLayer?.Layer?.GetSpatialReference();
          double conversionFactor = spatialReference?.ZUnit?.ConversionFactor ?? 1.0;
          zScale = 1 / conversionFactor;
          double modifierFactor = spatialReference?.Unit?.ConversionFactor ?? 1.0;
          modScale = 1 / modifierFactor;
        });

        result = new List<MapPoint>();
        ArcGISGeometryType geometryType = geometry.GeometryType;

        switch (geometryType)
        {
          case ArcGISGeometryType.Point:
            if (!geometry.IsEmpty && IsPointMeasurement)
            {
              if (geometry is MapPoint mapPoint)
              {
                result.Add(await AddZOffsetAsync(mapPoint, zScale * modScale));
              }
            }

            break;
          case ArcGISGeometryType.Polygon:
          case ArcGISGeometryType.Polyline:

            if (geometry is Multipart multipart)
            {
              ReadOnlyPointCollection points = multipart.Points;

              using (IEnumerator<MapPoint> enumPoints = points.GetEnumerator())
              {
                while (enumPoints.MoveNext())
                {
                  MapPoint mapPointPart = enumPoints.Current;
                  if(geometryType == ArcGISGeometryType.Polyline && mapPointPart == points.First() && points.Count == 1)
                  {
                    result.Add(await AddZOffsetAsync(mapPointPart, zScale));
                  }
                  else
                  {
                    result.Add(await AddZOffsetAsync(mapPointPart, zScale * modScale));
                  }
                }
              }
            }

            break;
        }
      }

      return result;
    }

    private async Task<MapPoint> AddZOffsetAsync(MapPoint mapPoint, double zScale)
    {
      return await QueuedTask.Run(async () => mapPoint.HasZ
        ? MapPointBuilderEx.CreateMapPoint(mapPoint.X, mapPoint.Y,
          (mapPoint.Z * zScale) + (VectorLayer != null ? await VectorLayer.GetOffsetZAsync() : 0),
          mapPoint.SpatialReference)
        : MapPointBuilderEx.CreateMapPoint(mapPoint.X, mapPoint.Y, mapPoint.SpatialReference));
    }

    public async Task UpdateMeasurementPointsAsync(MapView mapView, Geometry inGeometry)
    {
      if ((mapView != null || inGeometry != null) && !UpdateMeasurement)
      {
        UpdateMeasurement = true;
        Geometry geometry = mapView == null ? inGeometry : await mapView.GetCurrentSketchAsync();
        List<MapPoint> ptColl = await ToPointCollectionAsync(geometry);
        IFeatureCollection featureCollection =
          GeoJsonFactory.CloneFeatureCollection(_measurementList.FeatureCollection);
        IFeature feature = featureCollection?.Features?.Count >= 1 ? featureCollection.Features[0] : null;

        if (feature != null)
        {
          List<ICoordinate> coordinates = new List<ICoordinate>();
          IMeasurementProperties properties = (IMeasurementProperties) feature.Properties;
          IList<IMeasureDetails> measureDetails = properties.MeasureDetails;
          bool changes = false;

          switch (feature.Geometry.Type)
          {
            case StreetSmartGeometryType.Point:
              coordinates.Add((IPoint) feature.Geometry);
              break;
            case StreetSmartGeometryType.LineString:
              coordinates.AddRange((ILineString) feature.Geometry);
              break;
            case StreetSmartGeometryType.Polygon:
              coordinates.AddRange(((IPolygon) feature.Geometry)[0]);
              break;
          }

          if (ptColl != null)
          {
            for (int i = 0; i < ptColl.Count; i++)
            {
              MapPoint point = ptColl[i];

              if (coordinates.Count <= i)
              {
                coordinates.Add(await GeoJsonCoordAsync(point));
                changes = true;

                if (feature.Geometry.Type != StreetSmartGeometryType.Polygon || ptColl.Count != coordinates.Count)
                {
                  measureDetails.Add(GeoJsonFactory.CreateMeasureDetails());
                }
              }
              else
              {
                var measurementPoint = GetPoint(point);

                if (measurementPoint != null)
                {
                  int j = i;
                  int k = 0;

                  while (ContainsKey(j) && !this[j++].IsSame(point) && j < ptColl.Count)
                  {
                    int l = i + k;

                    if (coordinates.Count > l)
                    {
                      if (this[l].HasOneObservation())
                      {
                        k++;
                      }
                      else
                      {
                        coordinates.RemoveAt(l);
                        changes = true;

                        if (measureDetails.Count > l)
                        {
                          measureDetails.RemoveAt(l);
                        }
                      }
                    }
                  }
                }
                else
                {
                  if (coordinates.Count == ptColl.Count)
                  {
                    coordinates[i] = await GeoJsonCoordAsync(point);
                    changes = true;

                    if (measureDetails.Count > i)
                    {
                      measureDetails[i] = GeoJsonFactory.CreateMeasureDetails();
                    }
                  }
                  else
                  {
                    coordinates.Insert(i, await GeoJsonCoordAsync(point));
                    changes = true;
                    measureDetails.Insert(i, GeoJsonFactory.CreateMeasureDetails());
                  }
                }
              }
            }

            if (coordinates.Count > ptColl.Count)
            {
              int mapCount = 0;
              int cyclCount = 0;

              while (cyclCount < coordinates.Count)
              {
                if (mapCount < ptColl.Count)
                {
                  if (ContainsKey(cyclCount) && !this[cyclCount].HasOneObservation())
                  {
                    mapCount++;
                  }

                  cyclCount++;
                }
                else
                {
                  coordinates.RemoveAt(cyclCount);
                  changes = true;

                  if (cyclCount < measureDetails.Count)
                  {
                    measureDetails.RemoveAt(cyclCount);
                  }
                }
              }
            }

            if (changes || DoChange)
            {
              switch (feature.Geometry.Type)
              {
                case StreetSmartGeometryType.Point:
                  if (coordinates.Count == 1)
                  {
                    feature.Geometry = GeoJsonFactory.CreatePointGeometry(coordinates[0]);
                  }

                  break;
                case StreetSmartGeometryType.LineString:
                  feature.Geometry = GeoJsonFactory.CreateLineGeometry(coordinates);
                  break;
                case StreetSmartGeometryType.Polygon:
                  IList<IList<ICoordinate>> coordinatesPolygon = new List<IList<ICoordinate>>();
                  coordinatesPolygon.Add(coordinates);
                  feature.Geometry = GeoJsonFactory.CreatePolygonGeometry(coordinatesPolygon);
                  break;
              }

              _measurementList.FromMap = true;
              _api.SetActiveMeasurement(featureCollection);
              DoChange = false;
            }
          }
        }

        UpdateMeasurement = false;
      }
    }

    public async Task<ICoordinate> GeoJsonCoordAsync(MapPoint point)
    {
      MapView mapView = MapView.Active;
      Map map = mapView?.Map;
      ArcGISSpatialReference mapSpatRef = map?.SpatialReference;

      Setting settings = ProjectList.Instance.GetSettings(mapView);
      StreetSmartSpatialReference myCyclSpatRef = settings.CycloramaViewerCoordinateSystem;
      ArcGISSpatialReference cyclSpatRef = myCyclSpatRef == null
        ? mapSpatRef
        : myCyclSpatRef.ArcGisSpatialReference ?? await myCyclSpatRef.CreateArcGisSpatialReferenceAsync();
      ArcGISSpatialReference layerSpatRef = point.SpatialReference ?? cyclSpatRef;
      MapPoint copyGsPoint = null;

      await QueuedTask.Run(() =>
      {
        ProjectionTransformation projection = ProjectionTransformation.Create(layerSpatRef, cyclSpatRef);
        copyGsPoint = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
      });

      double e = 0.0001;

      if (copyGsPoint.HasZ && Math.Abs(copyGsPoint.Z) < e)
      {
        copyGsPoint = await VectorLayer.AddHeightToMapPointAsync(copyGsPoint, mapView);
      }

      return copyGsPoint.HasZ
        ? CoordinateFactory.Create(copyGsPoint.X, copyGsPoint.Y, copyGsPoint.Z)
        : CoordinateFactory.Create(copyGsPoint.X, copyGsPoint.Y);
    }

    public async Task UpdateMap()
    {
      MapView thisView = MapView.Active;
      Geometry geometry = await thisView.GetCurrentSketchAsync();
      List<MapPoint> points = new List<MapPoint>();
      bool toUpdate = (geometry?.PointCount ?? 0) != Count;
      IList<MapPoint> pointsGeometry = await ToPointCollectionAsync(geometry);

      if (!_measurementList.FromMap && VectorLayer != null)
      {
        await QueuedTask.Run(async () =>
        {
          ArcGISSpatialReference spatialReference = VectorLayer.Layer.GetSpatialReference();
          ArcGISSpatialReference mapSpatialReference = thisView.Map.SpatialReference;

          //Client.RegisterClientScriptBlock(this, this.GetType(), "alertMessage", "alert('Record Inserted Successfully')", true);

          for (int i = 0; i < Count; i++)
          {
            MapPoint mapPoint = pointsGeometry?.Count > i ? pointsGeometry[i] : null;
            MeasurementPoint mp = this.ElementAt(i).Value;
            toUpdate = toUpdate || mp.Updated && !mp.IsSame(mapPoint);

            if (mp.Point != null)
            {
              MapPoint point = mp.Point;
              double conversionFactor = spatialReference?.ZUnit?.ConversionFactor ?? 1.0;
              double z = conversionFactor * (point?.Z ?? 0);
              points.Add(MapPointBuilderEx.CreateMapPoint(point.X, point.Y, z));
            }
          }

          if (toUpdate)
          {
            if (IsGeometryType(ArcGISGeometryType.Polygon))
            {
              geometry = PolygonBuilderEx.CreatePolygon(points, spatialReference);
            }
            else if (IsGeometryType(ArcGISGeometryType.Polyline))
            {
              geometry = PolylineBuilderEx.CreatePolyline(points, spatialReference);
            }
            else if (geometry is MapPoint mapPoint)
            {
              MapPoint point = Count >= 1 ? this.ElementAt(0).Value.Point : mapPoint;
              double conversionFactor = spatialReference?.ZUnit?.ConversionFactor ?? 1.0;
              double z = conversionFactor * (point?.Z ?? 0);
              geometry = point == null ? null : MapPointBuilderEx.CreateMapPoint(point.X, point.Y, z, spatialReference);
            }

            await thisView.SetCurrentSketchAsync(geometry); //this is where the point on the map dissappears
          }
        });
      }
    }

    public async Task<MapView> GetMeasurementView()
    {
      var moduleStreetSmart = ModuleStreetSmart.Current;
      var vectorLayerList = await moduleStreetSmart.GetVectorLayerListAsync();
      return VectorLayer != null ? vectorLayerList.GetMapViewFromLayer(VectorLayer.Layer) : null;
    }

    #endregion
  }
}
