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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.AddIns.DockPanes;
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

    private readonly ArcGISGeometryType _geometryType;
    private readonly Settings _settings;
    private readonly MeasurementList _measurementList;
    private readonly IStreetSmartAPI _api;
    private readonly MeasurementDetail _detailPane;
    private readonly CultureInfo _ci;
    private readonly IMeasurementProperties _properties;

    #endregion

    #region Properties

    public IGeometry Geometry { get; set; }

    public VectorLayer VectorLayer { get; set; }

    public bool DrawPoint { get; private set; }

    public int PointNr { get; private set; }

    public long? ObjectId { get; set; }

    public bool IsPointMeasurement => _geometryType == ArcGISGeometryType.Point;

    public bool IsSketch => _measurementList.Sketch == this;

    public bool IsOpen => _measurementList.Open == this;

    public string MeasurementName => _properties.Name;

    public bool UpdateMeasurement { get; set; }

    public bool DoChange { get; set; }

    public string EntityId => _properties.Id;

    public IObservationLines ObservationLines { get; set; }

    #endregion

    #region Constructor

    public Measurement(IMeasurementProperties properties, IGeometry geometry, bool drawPoint, IStreetSmartAPI api)
    {
      _detailPane = MeasurementDetail.Get();
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      _measurementList = streetSmart.MeasurementList;

      _ci = CultureInfo.InvariantCulture;
      _api = api;
      _settings = Settings.Instance;
      DrawPoint = drawPoint;
      _properties = properties;
      UpdateMeasurement = false;
      DoChange = false;
      Geometry = geometry;
      // SetDetailPanePoint(null);

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

    #endregion

    #region Functions

    public async Task MeasurementPointUpdatedAsync(int pointId)
    {
      //   await _measurementList.MeasurementPointUpdatedAsync(EntityId, pointId);
    }

    public void SetDetailPanePoint(MeasurementPoint setPoint, MeasurementPoint fromPoint = null)
    {
      /*
      if ((fromPoint == null) || fromPoint == _detailPane.MeasurementPoint)
      {
        _detailPane.MeasurementPoint = setPoint;
      }
      */
    }

    public async void Dispose()
    {
      foreach (var element in this)
      {
        MeasurementPoint measurementPoint = element.Value;
        await measurementPoint.Dispose();
      }

      _measurementList.Open = IsOpen ? null : _measurementList.Open;
      _measurementList.Sketch = IsSketch ? null : _measurementList.Sketch;

      if (_measurementList.ContainsKey(EntityId))
      {
        _measurementList.Remove(EntityId);
      }

      if (VectorLayer != null)
      {
        await VectorLayer.GenerateJsonAsync();
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

    public async Task CloseAsync()
    {
      _measurementList.Open = null;
      DrawPoint = true;

      if (!IsPointMeasurement)
      {
        for (int i = 0; i < Count; i++)
        {
          MeasurementPoint point = this.ElementAt(i).Value;
          await point.RedrawPointAsync();

          for (int j = 0; j < point.Count; j++)
          {
            MeasurementObservation observation = point[j];
            await observation.RedrawObservationAsync();
          }
        }
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

    public void OpenPoint(int pointId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: open measurement point, EntityId, pointId
      }
    }

    public void RemoveObservation(int pointId, string imageId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: remove measurement point, EntityId, pointId, imageId
      }
    }

    public void ClosePoint(int pointId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: close measurement point, EntityId, pointId
      }
    }

    public MeasurementPoint GetPoint(MapPoint point)
    {
      return Values.Aggregate<MeasurementPoint, MeasurementPoint>
        (null, (current, value) => value.IsSame(point) ? value : current);
    }

    public async Task<bool> UpdatePointAsync(int pointId, IFeature apiMeasurementPoint)
    {
      bool result = false;

      if (pointId >= 0)
      {
        if (!ContainsKey(pointId))
        {
          AddPoint(pointId);
        }

        if (ContainsKey(pointId))
        {
          MeasurementPoint measurementPoint = this[pointId];
          result = await measurementPoint.UpdatePointAsync(apiMeasurementPoint, pointId);
        }
      }

      return result;
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

        foreach (string observation in measurementPoint.Observations.Keys)
        {
          if (!imageIds.Contains(observation))
          {
            measurementPoint.RemoveObservation(observation);
          }
        }
      }
    }

    public void CloseMeasurement()
    {
      if (IsOpen && GlobeSpotterConfiguration.MeasurePermissions)
      {
        _measurementList.Open = null;
        // ToDo: Close measurement, EntityId
      }
    }

    public void DisableMeasurementSeries()
    {
      _measurementList.DisableMeasurementSeries();
    }

    public void EnableMeasurementSeries()
    {
      _measurementList.EnableMeasurementSeries(EntityId);
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
        // Todo: set focus EntityId
        _measurementList.AddMeasurementPoint(EntityId);
      }
    }

    public void RemoveMeasurement()
    {
      if (IsOpen)
      {
        CloseMeasurement();
      }

      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: remove EntityId
      }

      Dispose();
    }

    public async Task RemovePoint(int pointId)
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

        if (Count >= 1)
        {
          SetDetailPanePoint(this.ElementAt(Count - 1).Value);
        }

        await measurementPoint.Dispose();
      }
    }

    public async Task<List<MapPoint>> ToPointCollectionAsync(Geometry geometry)
    {
      List<MapPoint> result = null;
      PointNr = 0;

      if (geometry != null)
      {
        result = new List<MapPoint>();
        ArcGISGeometryType geometryType = geometry.GeometryType;

        switch (geometryType)
        {
          case ArcGISGeometryType.Point:
            if (!geometry.IsEmpty && IsPointMeasurement)
            {
              if (geometry is MapPoint mapPoint)
              {
                result.Add(await AddZOffsetAsync(mapPoint));
              }
            }

            break;
          case ArcGISGeometryType.Polygon:
          case ArcGISGeometryType.Polyline:

            if (geometry is Multipart multipart)
            {
              ReadOnlyPointCollection points = multipart.Points;
              IEnumerator<MapPoint> enumPoints = points.GetEnumerator();

              while (enumPoints.MoveNext())
              {
                MapPoint mapPointPart = enumPoints.Current;
                result.Add(await AddZOffsetAsync(mapPointPart));
              }
            }

            break;
        }

        PointNr = result.Count;

        if (PointNr >= 2 && geometryType == ArcGISGeometryType.Polygon)
        {
          MapPoint point1 = result[0];
          MapPoint point2 = result[PointNr - 1];
          PointNr = point1.IsEqual(point2) ? PointNr - 1 : PointNr;
        }

        if (PointNr >= 2 && geometryType == ArcGISGeometryType.Polyline)
        {
          bool removed;

          for (int i = 0; i < PointNr - 1; i = removed ? i : ++i)
          {
            MapPoint point1 = result[i];
            MapPoint point2 = result[i + 1];

            if (point1.IsEqual(point2))
            {
              PointNr = PointNr - 1;
              result.RemoveAt(i + 1);
              removed = true;
            }
            else
            {
              removed = false;
            }
          }
        }
      }

      return result;
    }

    private async Task<MapPoint> AddZOffsetAsync(MapPoint mapPoint)
    {
      return await QueuedTask.Run(async () => mapPoint.HasZ
        ? MapPointBuilder.CreateMapPoint(mapPoint.X, mapPoint.Y,
          mapPoint.Z + (VectorLayer != null ? await VectorLayer.GetOffsetZAsync() : 0),
          mapPoint.SpatialReference)
        : MapPointBuilder.CreateMapPoint(mapPoint.X, mapPoint.Y, mapPoint.SpatialReference));
    }

    public int GetMeasurementPointIndex(int pointId)
    {
      // Todo: get measurement point index (EntityId, pointId)
      return GlobeSpotterConfiguration.MeasurePermissions ? 0 : 0;
    }

    public object GetApiPoint(int pointId)
    {
      // toDo: get point measurement data, pointId
      return null;
    }

    public void RemoveMeasurementPoint(int pointId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: remove measurement point: EntityId, pointId
      }
    }

    public void AddMeasurementPoint()
    {
      _measurementList.AddMeasurementPoint(EntityId);
    }

    public void OpenNearestImage(object apiPoint)
    {
      // Todo: get apipoint x, y, z
      double x = 0; // apiPoint.x;
      double y = 0; // apiPoint.y;
      double z = 0; // apiPoint.z;
      string coordinate = string.Format(_ci, "{0:#0.#},{1:#0.#},{2:#0.#}", x, y, z);
      // Todo: open nearest image: coordinate, 1
    }

    public void LookAtMeasurement(object apiPoint)
    {
      // Todo: get apipoint x, y, z
      // double x = 0; // apiPoint.x;
      // double y = 0; // apiPoint.y;
      // double z = 0; // apiPoint.z;
      // Todo: get the viewerIds
      int[] viewerIds = new int[0];

      foreach (var viewerId in viewerIds)
      {
        // Todo: for every viewer, look to the coordinate of the measurement: viewerId, x, y, z
      }
    }

    public void CreateMeasurementPoint(MapPoint point)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions && point != null)
      {
        // Todo: create measurement point: point.X, point.Y, point.Z, EntityId
      }
    }

    public async Task UpdateMeasurementPointsAsync(Geometry geometry)
    {
      if (geometry != null && !UpdateMeasurement)
      {
        _measurementList.InUpdateMeasurementMode.WaitOne(5000);
        _measurementList.InUpdateMeasurementMode.Reset();
        UpdateMeasurement = true;
        List<MapPoint> ptColl = await ToPointCollectionAsync(geometry);
        IFeatureCollection featureCollection =
          GeoJsonFactory.CloneFeatureCollection(_measurementList.FeatureCollection);
        IFeature feature = featureCollection.Features.Count >= 1 ? featureCollection.Features[0] : null;

        if (feature != null)
        {
          ArcGISGeometryType geometryType = geometry.GeometryType;
          List<ICoordinate> coordinates = new List<ICoordinate>();
          IMeasurementProperties properties = (IMeasurementProperties) feature.Properties;
          IList<IMeasureDetails> measureDetails = properties.MeasureDetails;
          bool changes = false;
          IDerivedData derivedData;

          switch (feature.Geometry.Type)
          {
            case StreetSmartGeometryType.Point:
              coordinates.Add((IPoint) feature.Geometry);
              derivedData = (IDerivedDataPoint) properties.DerivedData;
              break;
            case StreetSmartGeometryType.LineString:
              coordinates.AddRange((ILineString) feature.Geometry);
              derivedData = (IDerivedDataLineString) properties.DerivedData;
              break;
            case StreetSmartGeometryType.Polygon:
              coordinates.AddRange(((IPolygon) feature.Geometry)[0]);
              derivedData = (IDerivedDataPolygon) properties.DerivedData;
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
                measureDetails.Add(GeoJsonFactory.CreateMeasureDetails());
                changes = true;
              }
              else
              {
                var measurementPoint = GetPoint(point);

                if (measurementPoint != null)
                {
                  int j = i;

                  while (ContainsKey(j) && !this[j++].IsSame(point) && j < ptColl.Count)
                  {
                    if (coordinates.Count > i)
                    {
                      coordinates.RemoveAt(i);
                      changes = true;

                      if (measureDetails.Count > i)
                      {
                        measureDetails.RemoveAt(i);
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

                    if (measureDetails.Count > i)
                    {
                      measureDetails[i] = GeoJsonFactory.CreateMeasureDetails();
                    }
                  }
                }
              }
            }

            if (coordinates.Count > ptColl.Count)
            {
              int i = ptColl.Count;

              while (i < coordinates.Count)
              {
                coordinates.RemoveAt(i);
                changes = true;

                if (i < measureDetails.Count)
                {
                  measureDetails.RemoveAt(i);
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

              _api.SetActiveMeasurement(featureCollection);
              DoChange = false;
            }
          }
/*
        if (ptColl != null)
        {
          int msPoints = Count;
          var toRemove = new Dictionary<MeasurementPoint, bool>();
          var toAdd = new List<MapPoint>();
          bool toRemoveFrom = false;

          for (int i = 0; i < msPoints; i++)
          {
            MeasurementPoint measurementPoint = GetPointByNr(i);

            if (measurementPoint != null && (!measurementPoint.NotCreated && !IsPointMeasurement ||
                                             IsPointMeasurement && PointNr >= 1))
            {
              toRemove.Add(measurementPoint, true);
            }
          }

          for (int j = 0; j < PointNr; j++)
          {
            MapPoint point = ptColl[j];
            var measurementPoint = GetPoint(point);

            if (measurementPoint == null)
            {
              toAdd.Add(point);
              toRemoveFrom = true;
            }
            else
            {
              if (!toRemoveFrom)
              {
                if (toRemove.ContainsKey(measurementPoint))
                {
                  toRemove[measurementPoint] = false;
                }
              }
              else
              {
                toAdd.Add(point);
              }
            }
          }

          if (toRemove.Aggregate(false, (current, remove) => remove.Value || current) || (toAdd.Count >= 1))
          {
            if (!IsPointMeasurement)
            {
              DisableMeasurementSeries();
            }

            foreach (var elem in toRemove)
            {
              if (elem.Value && GlobeSpotterConfiguration.MeasurePermissions)
              {
                MeasurementPoint msPoint = elem.Key;
                int pointId = msPoint.PointId;
                // Todo: Remove measurement point: EntityId, pointId
              }
            }

            foreach (var point in toAdd)
            {
              MapView mapView = MapView.Active;
              Map map = mapView?.Map;
              ArcGISSpatialReference mapSpatRef = map?.SpatialReference;

              StreetSmartSpatialReference myCyclSpatRef = _settings.CycloramaViewerCoordinateSystem;
              ArcGISSpatialReference cyclSpatRef = myCyclSpatRef == null
                ? mapSpatRef
                : (myCyclSpatRef.ArcGisSpatialReference ?? await myCyclSpatRef.CreateArcGisSpatialReferenceAsync());
              ArcGISSpatialReference layerSpatRef = point.SpatialReference ?? cyclSpatRef;
              MapPoint copyGsPoint = null;

              await QueuedTask.Run(() =>
              {
                ProjectionTransformation projection = ProjectionTransformation.Create(layerSpatRef, cyclSpatRef);
                copyGsPoint = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
              });

              CreateMeasurementPoint(copyGsPoint);
            }

            if (!IsPointMeasurement)
            {
              EnableMeasurementSeries();
            }
          }
        }*/

        }

        UpdateMeasurement = false;
        _measurementList.InUpdateMeasurementMode.Set();
      }
    }

    public async Task<ICoordinate> GeoJsonCoordAsync(MapPoint point)
    {
      MapView mapView = MapView.Active;
      Map map = mapView?.Map;
      ArcGISSpatialReference mapSpatRef = map?.SpatialReference;

      StreetSmartSpatialReference myCyclSpatRef = _settings.CycloramaViewerCoordinateSystem;
      ArcGISSpatialReference cyclSpatRef = myCyclSpatRef == null
        ? mapSpatRef
        : (myCyclSpatRef.ArcGisSpatialReference ?? await myCyclSpatRef.CreateArcGisSpatialReferenceAsync());
      ArcGISSpatialReference layerSpatRef = point.SpatialReference ?? cyclSpatRef;
      MapPoint copyGsPoint = null;

      await QueuedTask.Run(() =>
      {
        ProjectionTransformation projection = ProjectionTransformation.Create(layerSpatRef, cyclSpatRef);
        copyGsPoint = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
      });

      return copyGsPoint.HasZ
        ? CoordinateFactory.Create(copyGsPoint.X, copyGsPoint.Y, copyGsPoint.Z)
        : CoordinateFactory.Create(copyGsPoint.X, copyGsPoint.Y);
    }

    #endregion
  }
}
