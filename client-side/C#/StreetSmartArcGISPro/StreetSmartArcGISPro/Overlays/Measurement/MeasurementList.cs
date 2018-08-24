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

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using GlobeSpotterAPI;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.VectorLayers;

using ApiMeasurementObservation = GlobeSpotterAPI.MeasurementObservation;
using ApiMeasurementPoint = GlobeSpotterAPI.MeasurementPoint;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  class MeasurementList : Dictionary<int, Measurement>, IAPIClient
  {
    #region Members

    private readonly ConstantsViewer _constants;
    private bool _screenPointAdded;
    private bool _mapPointAdded;
    private bool _drawingSketch;
    private bool _pointAdded;

    #endregion

    #region Properties

    public Measurement Sketch { get; set; }
    public Measurement Open { get; set; }
    public API Api { private get; set; }
    public bool DrawPoint { private get; set; }

    #endregion

    #region Constructor

    public MeasurementList()
    {
      Open = null;
      Sketch = null;
      _constants = ConstantsViewer.Instance;
      _screenPointAdded = false;
      _mapPointAdded = false;
      DrawPoint = true;
      _drawingSketch = false;
      _pointAdded = false;
    }

    #endregion

    #region Functions

    public void CloseOpenMeasurement()
    {
      Open?.CloseMeasurement();
    }

    public Measurement Get(int entityId)
    {
      return ContainsKey(entityId) ? this[entityId] : null;
    }

    public Task<Measurement> GetAsync(Geometry geometry)
    {
      return GetAsync(geometry, true);
    }

    public async Task<Measurement> GetAsync(Geometry geometry, bool includeZ)
    {
      Measurement result = null;

      if (geometry != null)
      {
        for (int i = 0; ((i < Count) && (result == null)); i++)
        {
          var element = this.ElementAt(i);
          Measurement measurement = element.Value;
          var ptColl = await measurement.ToPointCollectionAsync(geometry);
          int nrPoints = measurement.PointNr;

          if (ptColl != null)
          {
            int msPoints = measurement.Count;

            if (nrPoints == msPoints)
            {
              bool found = true;

              for (int j = 0; j < nrPoints && found; j++)
              {
                MapPoint point = ptColl[j];
                MeasurementPoint measurementPoint = measurement.GetPointByNr(j);

                if (point != null)
                {
                  found = measurementPoint?.IsSame(point, includeZ) ?? true;
                }
              }

              if (found)
              {
                result = measurement;
              }
            }
          }
        }
      }

      return result;
    }

    public Measurement Get(long objectId)
    {
      Measurement result = null;

      for (int i = 0; ((i < Count) && (result == null)); i++)
      {
        var element = this.ElementAt(i);
        Measurement measurement = element.Value;
        result = measurement.ObjectId == objectId ? measurement : null;
      }

      return result;
    }

    public void RemoveAll()
    {
      while (Count >= 1)
      {
        var element = this.ElementAt(0);
        Measurement measurement = element.Value;
        measurement.RemoveMeasurement();
      }
    }

    public void RemoveUnusedMeasurements(List<Measurement> usedMeasurements)
    {
      if (Sketch != null)
      {
        if (!usedMeasurements.Contains(Sketch))
        {
          usedMeasurements.Add(Sketch);
        }
      }

      int i = 0;

      while (i < Count)
      {
        var measurement = this.ElementAt(i);
        Measurement element = measurement.Value;

        if (!usedMeasurements.Contains(element))
        {
          element.RemoveMeasurement();
        }
        else
        {
          i++;
        }
      }
    }

    public void DisableMeasurementSeries()
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Api?.SetMeasurementSeriesModeEnabled(false);
      }
    }

    public void EnableMeasurementSeries(int entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Api?.NextMeasurementSeries(entityId);
        Api?.SetMeasurementSeriesModeEnabled(true);
      }
    }

    public void OpenMeasurement(int entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Api?.OpenMeasurement(entityId);
        Api?.SetFocusEntity(entityId);
      }
    }

    public void AddMeasurementPoint(int entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Api?.AddMeasurementPoint(entityId);
      }
    }

    public int CreateMeasurement(GeometryType geometryType, long? objectId)
    {
      int entityId = -1;
      string measurementName = _constants.MeasurementName;

      if (Api != null)
      {
        switch (geometryType)
        {
          case GeometryType.Point:
            if (GlobeSpotterConfiguration.MeasurePoint)
            {
              entityId = Api.AddPointMeasurement($"Point_{measurementName}_{objectId}");
              OpenMeasurement(entityId);
              DisableMeasurementSeries();
              AddMeasurementPoint(entityId);
            }

            break;
          case GeometryType.Polyline:
            if (GlobeSpotterConfiguration.MeasureLine)
            {
              entityId = Api.AddLineMeasurement($"Line_{measurementName}_{objectId}");
              OpenMeasurement(entityId);
              EnableMeasurementSeries(entityId);
            }

            break;
          case GeometryType.Polygon:
            if (GlobeSpotterConfiguration.MeasurePolygon)
            {
              entityId = Api.AddSurfaceMeasurement($"Polygon_{measurementName}_{objectId}");
              Api.SetMeasurementExtrusionEnabled(entityId, false);
              OpenMeasurement(entityId);
              EnableMeasurementSeries(entityId);
            }

            break;
        }
      }

      return entityId;
    }

    private async Task UpdateMeasurementPointAsync(int entityId, int pointId)
    {
      await MeasurementPointUpdatedAsync(entityId, pointId);
    }

    public async Task MeasurementPointUpdatedAsync(int entityId, int pointId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        _screenPointAdded = !_mapPointAdded;

        PointMeasurementData measurementData = Api.GetMeasurementPointData(entityId, pointId);
        ApiMeasurementPoint measurementPoint = measurementData.measurementPoint;
        Measurement measurement = Get(entityId);

        if (measurement != null)
        {
          int index = (int) Api?.GetMeasurementPointIndex(entityId, pointId);
          await measurement.UpdatePointAsync(pointId, measurementPoint, index);
        }

        _screenPointAdded = false;
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

    public async Task SketchModifiedAsync(Geometry geometry, VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        _mapPointAdded = !_screenPointAdded;
        Measurement measurement = Sketch;

        if (geometry != null)
        {
          if (((!_drawingSketch) && (!geometry.IsEmpty)) || (measurement == null))
          {
            _drawingSketch = true;
            measurement = StartMeasurement(geometry, measurement, true, null, vectorLayer);
          }

          if (measurement != null)
          {
            await measurement.UpdateMeasurementPointsAsync(geometry);
          }
        }

        _mapPointAdded = false;
      }
    }

    public Measurement StartMeasurement(Geometry geometry, Measurement measurement, bool sketch, long? objectId, VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        bool measurementExists = false;
        GeometryType geometryType = geometry?.GeometryType ?? GeometryType.Unknown;

        if (geometryType == GeometryType.Point || geometryType == GeometryType.Polygon ||
            (geometryType == GeometryType.Polyline))
        {
          if (measurement?.IsGeometryType(geometryType) ?? false)
          {
            measurementExists = true;
            measurement.OpenMeasurement();
          }
          else
          {
            measurement?.RemoveMeasurement();
          }

          if (!measurementExists)
          {
            CloseOpenMeasurement();
            int entityId = CreateMeasurement(geometryType, objectId);
            measurement = entityId == -1 ? null : Get(entityId);

            if (measurement != null)
            {
              measurement.ObjectId = objectId;
              measurement.VectorLayer = vectorLayer;
            }

            measurement?.Open();

            if (sketch)
            {
              measurement?.SetSketch();
            }
          }
        }
      }

      return measurement;
    }

    public int GetMeasurementNumber(Measurement measurement)
    {
      return Keys.Aggregate(1, (current, elem) => current + ((elem < measurement.EntityId) ? 1 : 0));
    }

    #endregion

    #region streetSmart events

    public void OnComponentReady() { }

    public void OnAPIReady() { }

    public void OnAPIFailed() { }

    public void OnOpenImageFailed(string input) { }

    public void OnOpenImageResult(string input, bool opened, string imageId) { }

    public void OnOpenNearestImageResult(string input, bool opened, string imageId, Point3D position) { }

    public void OnImageChanged(uint viewerId) { }

    public void OnImagePreviewCompleted(uint viewerId) { }

    public void OnImageSegmentLoaded(uint viewerId) { }

    public void OnImageCompleted(uint viewerId) { }

    public void OnImageFailed(uint viewerId) { }

    public void OnViewLoaded(uint viewerId) { }

    public void OnImageDistanceSliderChanged(uint viewerId, double distance) { }

    public void OnViewChanged(uint viewerId, double yaw, double pitch, double hFov) { }

    public void OnViewClicked(uint viewerId, double[] mouseCoords) { }

    public void OnMarkerClicked(uint viewerId, uint drawingId, double[] markerCoords) { }

    public void OnEntityDataChanged(int entityId, EntityData data) { }

    public void OnEntityFocusChanged(int entityId) { }

    public void OnFocusPointChanged(double x, double y, double z) { }

    public void OnFeatureClicked(Dictionary<string, string> feature) { }

    public void OnViewerAdded(uint viewerId) { }

    public void OnViewerRemoved(uint viewerId) { }

    public void OnViewerActive(uint viewerId) { }

    public void OnViewerInactive(uint viewerId) { }

    public void OnMaxViewers() { }

    public void OnMeasurementCreated(int entityId, string entityType)
    {
      Measurement measurement = new Measurement(entityId, entityType, DrawPoint, Api);
      Add(entityId, measurement);
    }

    public async void OnMeasurementClosed(int entityId, EntityData data)
    {
      Measurement measurement = Get(entityId);

      if (measurement != null)
      {
        await measurement.CloseAsync();
      }
    }

    public void OnMeasurementOpened(int entityId, EntityData data)
    {
      Measurement measurement = Get(entityId);
      measurement?.Open();
    }

    public void OnMeasurementCanceled(int entityId) { }

    public void OnMeasurementModeChanged(bool mode) { }

    public async void OnMeasurementPointAdded(int entityId, int pointId)
    {
      Measurement measurement = Get(entityId);
      measurement?.AddPoint(pointId);
      _pointAdded = true;

      if ((!(Api?.GetMeasurementSeriesModeEnabled() ?? true)) || (measurement?.IsPointMeasurement ?? false))
      {
        await UpdateMeasurementPointAsync(entityId, pointId);
      }
    }

    public async void OnMeasurementPointUpdated(int entityId, int pointId)
    {
      await UpdateMeasurementPointAsync(entityId, pointId);
    }

    public void OnMeasurementPointRemoved(int entityId, int pointId)
    {
      Measurement measurement = Get(entityId);
      measurement?.RemovePoint(pointId);
    }

    public void OnMeasurementPointOpened(int entityId, int pointId)
    {
      Measurement measurement = Get(entityId);

      if (measurement?.ContainsKey(pointId) ?? false)
      {
        MeasurementPoint point = measurement[pointId];
        point.Opened();

        if ((!measurement.IsPointMeasurement) && (_pointAdded))
        {
          point.Closed();
        }
      }

      _pointAdded = false;
    }

    public void OnMeasurementPointClosed(int entityId, int pointId)
    {
      Measurement measurement = Get(entityId);

      if (measurement?.ContainsKey(pointId) ?? false)
      {
        MeasurementPoint point = measurement[pointId];
        point.Closed();
      }
    }

    public async void OnMeasurementPointObservationAdded(int entityId, int pointId, string imageId, Bitmap match)
    {
      ObservationMeasurementData observation = Api.GetMeasurementPointObservationData(entityId, pointId, imageId);
      ApiMeasurementObservation measurementObservation = observation.measurementObservation;
      Measurement measurement = Get(entityId);

      if (measurement?.ContainsKey(pointId) ?? false)
      {
        MeasurementPoint measurementPoint = measurement[pointId];
        await measurementPoint.UpdateObservationAsync(measurementObservation, match);
      }
    }

    public async void OnMeasurementPointObservationUpdated(int entityId, int pointId, string imageId)
    {
      ObservationMeasurementData observation = Api.GetMeasurementPointObservationData(entityId, pointId, imageId);
      ApiMeasurementObservation measurementObservation = observation.measurementObservation;
      Measurement measurement = Get(entityId);

      if (measurement?.ContainsKey(pointId) ?? false)
      {
        MeasurementPoint measurementPoint = measurement[pointId];
        await measurementPoint.UpdateObservationAsync(measurementObservation, null);
      }
    }

    public void OnMeasurementPointObservationRemoved(int entityId, int pointId, string imageId)
    {
      Measurement measurement = Get(entityId);

      if (measurement?.ContainsKey(pointId) ?? false)
      {
        MeasurementPoint measurementPoint = measurement[pointId];
        measurementPoint.RemoveObservation(imageId);
      }
    }

    public void OnDividerPositionChanged(double position) { }

    public void OnMapClicked(Point2D point) { }

    public void OnMapExtentChanged(MapExtent extent, Point2D mapCenter, uint zoomLevel) { }

    public void OnAutoCompleteResult(string request, string[] results) { }

    public void OnMapInitialized() { }

    public void OnShowLocationRequested(uint viewerId, Point3D point3D) { }

    public void OnDetailImagesVisibilityChanged(bool value) { }

    public void OnMeasurementHeightLevelChanged(int entityId, double level) { }

    public void OnMeasurementPointHeightLevelChanged(int entityId, int pointId, double level) { }

    public void OnMapBrightnessChanged(double value) { }

    public void OnMapContrastChanged(double value) { }

    public void OnObliqueImageChanged() { }

    #endregion
  }
}
