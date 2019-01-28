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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ArcGIS.Core.Geometry;

using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.Events;
using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.VectorLayers;

using ArcGISGeometryType = ArcGIS.Core.Geometry.GeometryType;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  class MeasurementList : Dictionary<string, Measurement>
  {
    #region Members

    private int MaxWaitTime = 10000;

    private bool _drawingSketch;
    private VectorLayer _lastVectorLayer;
    private long? _lastObjectId;
    private bool _lastSketch;

    #endregion

    #region Properties

    public Measurement Sketch { get; set; }
    public Measurement Open { get; set; }
    public IStreetSmartAPI Api { private get; set; }
    public bool DrawPoint { private get; set; }

    public EventWaitHandle InUpdateMeasurementMode { get; set; }

    public IFeatureCollection FeatureCollection { get; set; }

    #endregion

    #region Constructor

    public MeasurementList()
    {
      InUpdateMeasurementMode = new AutoResetEvent(true);
      Open = null;
      Sketch = null;
      DrawPoint = true;
      _drawingSketch = false;
      _lastObjectId = null;
      _lastVectorLayer = null;
      _lastSketch = false;
    }

    #endregion

    #region Functions

    public void CloseOpenMeasurement()
    {
      Open?.CloseMeasurement();
    }

    public Measurement Get(string entityId)
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
        // Todo: disable measurement series mode
      }
    }

    public void EnableMeasurementSeries(string entityId)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        // Todo: enable measurement series
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
          IList<IViewer> viewers = await Api.GetViewers();
          IPanoramaViewer panoramaViewer = viewers.Aggregate<IViewer, IPanoramaViewer>(null,
            (current, viewer) => viewer is IPanoramaViewer ? (IPanoramaViewer) viewer : current);

          if (panoramaViewer != null)
          {
            IMeasurementOptions options = MeasurementOptionsFactory.Create(measurementGeometryType);
            Api.StartMeasurementMode(panoramaViewer, options);
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

    public async Task SketchModifiedAsync(Geometry geometry, VectorLayer vectorLayer)
    {
      if (GlobeSpotterConfiguration.MeasurePermissions)
      {
        Measurement measurement = Sketch;

        if (geometry != null)
        {
          if (!_drawingSketch && !geometry.IsEmpty || measurement == null)
          {
            _drawingSketch = true;
            measurement = await StartMeasurement(geometry, measurement, true, null, vectorLayer);
          }

          if (measurement != null)
          {
            await measurement.UpdateMeasurementPointsAsync(geometry);
          }
        }
      }
    }

    public async Task<Measurement> StartMeasurement(Geometry geometry, Measurement measurement, bool sketch, long? objectId, VectorLayer vectorLayer)
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
          else
          {
            measurement?.RemoveMeasurement();
          }

          if (!measurementExists)
          {
            CloseOpenMeasurement();
            _lastObjectId = objectId;
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

    public async void OnMeasurementChanged(object sender, IEventArgs<IFeatureCollection> args)
    {
      InUpdateMeasurementMode.WaitOne(MaxWaitTime);
      InUpdateMeasurementMode.Reset();
      FeatureCollection = args.Value;
      IStreetSmartAPI api = sender as IStreetSmartAPI;

      foreach (IFeature feature in FeatureCollection.Features)
      {
        if (feature.Properties is IMeasurementProperties properties)
        {
          // Hack, solve at a good way!
//        if (!ContainsKey(properties.Id))
          if (Count == 0)
          {
            Measurement measurement =
              new Measurement(properties, feature.Geometry, DrawPoint, api)
              {
                ObjectId = _lastObjectId,
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
            // Hack, solve at a good way!
            // Measurement measurement = this[properties.Id];
            Measurement measurement = this.ElementAt(0).Value;
            measurement.ObservationLines = properties.ObservationLines;

            if (!measurement.UpdateMeasurement)
            {
              measurement.UpdateMeasurement = true;
              IGeometry geometry = feature.Geometry;
              StreetSmartGeometryType geometryType = geometry.Type;

              switch (geometryType)
              {
                case StreetSmartGeometryType.Point:
                  if (measurement.Geometry is IPoint pointSrc && geometry is IPoint pointDst)
                  {
                    bool result = await measurement.UpdatePointAsync(0, feature);
                    measurement.Geometry = geometry;

                    if (result)
                    {
                      measurement.RemoveObservations(0, feature);
                      await measurement.RemovePoint(0);
                    }
                    // update point
                  }

                  break;
                case StreetSmartGeometryType.LineString:
                  if (measurement.Geometry is ILineString lineSrc && geometry is ILineString lineDst)
                  {
                    for (int i = 0; i < Math.Max(lineDst.Count, lineSrc.Count); i++)
                    {
                      measurement.RemoveObservations(i, feature);

                      if (lineSrc.Count > i && lineDst.Count > i)
                      {
                        bool result = await measurement.UpdatePointAsync(i, feature);

                        if (result)
                        {
                          await measurement.RemovePoint(i);
                        }
                        // update point [i]
                      }
                      else if (lineSrc.Count <= i && lineDst.Count > i)
                      {
                        measurement.AddPoint(lineSrc.Count);
                        bool result = await measurement.UpdatePointAsync(i, feature);

                        if (result)
                        {
                          await measurement.RemovePoint(i);
                        }
                        // add point [lineSrc.Count]
                      }
                      else if (lineSrc.Count > i && lineDst.Count <= i)
                      {
                        await measurement.RemovePoint(i);
                        bool result = await measurement.UpdatePointAsync(Math.Min(i, lineDst.Count - 1), feature);

                        if (result)
                        {
                          await measurement.RemovePoint(Math.Min(i, lineDst.Count - 1));
                        }
                        // remove point [i]
                      }
                    }

                    measurement.Geometry = geometry;
                  }

                  break;
                case StreetSmartGeometryType.Polygon:
                  if (measurement.Geometry is IPolygon polySrc && geometry is IPolygon polyDst)
                  {
                    int polySrcCount = polySrc[0].Count;
                    int pylyDstCount = polyDst[0].Count;

                    for (int i = 0; i < Math.Max(pylyDstCount, polySrcCount); i++)
                    {
                      measurement.RemoveObservations(i, feature);
                      if (polySrcCount > i && pylyDstCount > i)
                      {
                        bool result = await measurement.UpdatePointAsync(i, feature);

                        if (result)
                        {
                          await measurement.RemovePoint(i);
                        }
                        // update point [i]
                      }
                      else if (polySrcCount <= i && pylyDstCount > i)
                      {
                        measurement.AddPoint(polySrcCount++);
                        bool result = await measurement.UpdatePointAsync(i, feature);

                        if (result)
                        {
                          await measurement.RemovePoint(i);
                        }
                        // add point [lineSrc.Count]
                      }
                      else if (polySrcCount < i && pylyDstCount <= i)
                      {
                        await measurement.RemovePoint(i);
                        polySrcCount--;
                        bool result = await measurement.UpdatePointAsync(Math.Min(i, pylyDstCount - 1), feature);

                        if (result)
                        {
                          await measurement.RemovePoint(Math.Min(i, pylyDstCount - 1));
                        }
                        // remove point [i]
                      }
                    }

                    measurement.Geometry = geometry;
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
      }

      if (FeatureCollection.Type == FeatureType.Unknown)
      {
        if (Count == 1)
        {
          // Close measurement
          Measurement measurement = this.ElementAt(0).Value;
          await measurement.CloseAsync();
        }
      }

      InUpdateMeasurementMode.Set();
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

    #endregion
  }
}
