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
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;

using GeometryType = ArcGIS.Core.Geometry.GeometryType;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using StreetSmartModule = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using Unit = ArcGIS.Core.Geometry.Unit;

namespace StreetSmartArcGISPro.VectorLayers
{
  public class VectorLayer : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly Settings _settings;
    private readonly ViewerList _viewerList;
    private readonly MeasurementList _measurementList;
    private readonly VectorLayerList _vectorLayerList;
    private readonly CultureInfo _ci;

    private SubscriptionToken _rowChanged;
    private SubscriptionToken _rowDeleted;
    private SubscriptionToken _rowCreated;

    private IFeatureCollection _geoJson;
    private IList<long> _selection;
    private List<Measurement> _measurements;
    private bool _updateMeasurements;

    #endregion

    #region Constructor

    public VectorLayer(FeatureLayer layer, VectorLayerList vectorLayerList)
    {
      _vectorLayerList = vectorLayerList;
      Layer = layer;
      _settings = Settings.Instance;
      Overlay = null;
      _selection = null;
      _updateMeasurements = false;

      StreetSmartModule streetSmart = StreetSmartModule.Current;
      _viewerList = streetSmart.ViewerList;
      _measurementList = streetSmart.MeasurementList;
      _ci = CultureInfo.InvariantCulture;
    }

    #endregion

    #region Properties

    public IFeatureCollection GeoJson
    {
      get => _geoJson;
      private set
      {
        _geoJson = value;
        NotifyPropertyChanged();
      }
    }

    public Color Color { get; private set; }

    public FeatureLayer Layer { get; }

    public IOverlay Overlay { get; set; }

    public bool GeoJsonChanged { get; private set; }

    public string Name => Layer?.Name ?? string.Empty;

    public bool IsVisible => Layer != null && Layer.IsVisible;

    public List<Measurement> Measurements
    {
      get => _measurements;
      set
      {
        _measurements = value;
        NotifyPropertyChanged();
      }
    }

    #endregion

    #region Functions

    public async Task<bool> InitializeEventsAsync()
    {
      bool result = Layer.ConnectionStatus == ConnectionStatus.Connected;

      if (result)
      {
        MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        DrawCompleteEvent.Subscribe(OnDrawCompleted);

        await QueuedTask.Run(() =>
        {
          var table = Layer.GetTable();
          _rowChanged = RowChangedEvent.Subscribe(OnRowChanged, table);
          _rowDeleted = RowDeletedEvent.Subscribe(OnRowDeleted, table);
          _rowCreated = RowCreatedEvent.Subscribe(OnRowCreated, table);
        });
      }

      await LoadMeasurementsAsync();
      return result;
    }

    public async Task<IFeatureCollection> GenerateJsonAsync()
    {
      MapView mapView = MapView.Active;
      Map map = mapView?.Map;
      SpatialReference mapSpatRef = map?.SpatialReference;
      MySpatialReference myCyclSpatRef = _settings.CycloramaViewerCoordinateSystem;

      SpatialReference cyclSpatRef = myCyclSpatRef == null
        ? mapSpatRef
        : (myCyclSpatRef.ArcGisSpatialReference ?? await myCyclSpatRef.CreateArcGisSpatialReferenceAsync());

      Unit unit = cyclSpatRef?.Unit;
      double factor = unit?.ConversionFactor ?? 1;
      Color color = Color.White;
      IFeatureCollection featureCollection = null;

      await QueuedTask.Run(async () =>
      {
        SpatialReference layerSpatRef = Layer.GetSpatialReference();
        IList<IList<Segment>> geometries = new List<IList<Segment>>();
        ICollection<Viewer> viewers = _viewerList.Viewers;

        foreach (var viewer in viewers)
        {
          ConstantsViewer constants = ConstantsViewer.Instance;
          double distance = constants.OverlayDrawDistance;
          ICoordinate coordinate = viewer.Coordinate;

          if (coordinate != null)
          {
            if (cyclSpatRef?.IsGeographic ?? true)
            {
              distance = distance * factor;
            }
            else
            {
              distance = distance / factor;
            }

            double x = coordinate.X ?? 0.0;
            double y = coordinate.Y ?? 0.0;
            double xMin = x - distance;
            double xMax = x + distance;
            double yMin = y - distance;
            double yMax = y + distance;

            Envelope envelope = EnvelopeBuilder.CreateEnvelope(xMin, yMin, xMax, yMax, cyclSpatRef);
            Envelope copyEnvelope = envelope;

            if (layerSpatRef.Wkid != 0)
            {
              ProjectionTransformation projection = ProjectionTransformation.Create(cyclSpatRef, layerSpatRef);
              copyEnvelope = GeometryEngine.Instance.ProjectEx(envelope, projection) as Envelope;
            }

            Polygon copyPolygon = PolygonBuilder.CreatePolygon(copyEnvelope, layerSpatRef);
            ReadOnlyPartCollection polygonParts = copyPolygon.Parts;

            using (IEnumerator<ReadOnlySegmentCollection> polygonSegments = polygonParts.GetEnumerator())
            {
              IList<Segment> segments = new List<Segment>();

              while (polygonSegments.MoveNext())
              {
                ReadOnlySegmentCollection polygonSegment = polygonSegments.Current;

                if (polygonSegment != null)
                {
                  foreach (Segment segment in polygonSegment)
                  {
                    segments.Add(segment);
                  }
                }
              }

              geometries.Add(segments);
            }
          }
        }

        Polygon polygon = PolygonBuilder.CreatePolygon(geometries, layerSpatRef);
        featureCollection = GeoJsonFactory.CreateFeatureCollection(layerSpatRef?.Wkid ?? 0);

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          SpatialQueryFilter spatialFilter = new SpatialQueryFilter
          {
            FilterGeometry = polygon,
            SpatialRelationship = SpatialRelationship.Intersects,
            SubFields = "*"
          };

          using (RowCursor existsResult = featureClass?.Search(spatialFilter, false))
          {
            while (existsResult?.MoveNext() ?? false)
            {
              // Todo: change handling about selections
              // if (_selection == null || !_selection.Contains(objectId))
              {
                Row row = existsResult.Current;
                Feature feature = row as Feature;
                var fieldvalues = new Dictionary<string, string>();
                IReadOnlyList<Field> fields = feature.GetFields();

                foreach (Field field in fields)
                {
                  string name = field.Name;
                  int fieldId = existsResult.FindField(name);
                  fieldvalues.Add(name, feature.GetOriginalValue(fieldId).ToString());
                }

                Geometry geometry = feature?.GetShape();
                GeometryType geometryType = geometry?.GeometryType ?? GeometryType.Unknown;
                Geometry copyGeometry = geometry;

                if (geometry != null && layerSpatRef.Wkid != 0)
                {
                  ProjectionTransformation projection = ProjectionTransformation.Create(layerSpatRef, cyclSpatRef);
                  copyGeometry = GeometryEngine.Instance.ProjectEx(geometry, projection);
                }

                if (copyGeometry != null)
                {
                  switch (geometryType)
                  {
                    case GeometryType.Envelope:
                      break;
                    case GeometryType.Multipatch:
                      break;
                    case GeometryType.Multipoint:
                      break;
                    case GeometryType.Point:
                      if (copyGeometry is MapPoint point)
                      {
                        ICoordinate coordinate = await GeoJsonCoordAsync(point);
                        featureCollection.Features.Add(GeoJsonFactory.CreatePointFeature(coordinate));
                      }

                      break;
                    case GeometryType.Polygon:
                      if (copyGeometry is Polygon polygonGeoJson)
                      {
                        ReadOnlyPartCollection polygonParts = polygonGeoJson.Parts;
                        IList<IList<ICoordinate>> polygonCoordinates = new List<IList<ICoordinate>>();

                        using (IEnumerator<ReadOnlySegmentCollection> polygonSegments = polygonParts.GetEnumerator())
                        {
                          while (polygonSegments.MoveNext())
                          {
                            ReadOnlySegmentCollection segments = polygonSegments.Current;
                            IList<ICoordinate> coordinates = new List<ICoordinate>();

                            if (segments != null)
                            {
                              for (int i = 0; i < segments.Count; i++)
                              {
                                if (segments[i].SegmentType == SegmentType.Line)
                                {
                                  MapPoint polygonPoint = segments[i].StartPoint;
                                  coordinates.Add(await GeoJsonCoordAsync(polygonPoint));

                                  if (i == (segments.Count - 1))
                                  {
                                    polygonPoint = segments[i].EndPoint;
                                    coordinates.Add(await GeoJsonCoordAsync(polygonPoint));
                                  }
                                }
                              }
                            }

                            polygonCoordinates.Add(coordinates);
                          }
                        }

                        featureCollection.Features.Add(GeoJsonFactory.CreatePolygonFeature(polygonCoordinates));
                      }

                      break;
                    case GeometryType.Polyline:
                      if (copyGeometry is Polyline polylineGeoJson)
                      {
                        ReadOnlyPartCollection polylineParts = polylineGeoJson.Parts;

                        using (IEnumerator<ReadOnlySegmentCollection> polylineSegments = polylineParts.GetEnumerator())
                        {
                          while (polylineSegments.MoveNext())
                          {
                            ReadOnlySegmentCollection segments = polylineSegments.Current;
                            IList<ICoordinate> coordinates = new List<ICoordinate>();

                            if (segments != null)
                            {
                              for (int i = 0; i < segments.Count; i++)
                              {
                                if (segments[i].SegmentType == SegmentType.Line)
                                {
                                  MapPoint linePoint = segments[i].StartPoint;
                                  coordinates.Add(await GeoJsonCoordAsync(linePoint));

                                  if (i == (segments.Count - 1))
                                  {
                                    linePoint = segments[i].EndPoint;
                                    coordinates.Add(await GeoJsonCoordAsync(linePoint));
                                  }
                                }
                              }
                            }

                            featureCollection.Features.Add(GeoJsonFactory.CreateLineFeature(coordinates));
                          }
                        }
                      }

                      break;
                    case GeometryType.Unknown:
                      break;
                  }

                  foreach (var fieldvalue in fieldvalues)
                  {
                    featureCollection.Features[featureCollection.Features.Count - 1].Properties
                      .Add(fieldvalue.Key, fieldvalue.Value);
                  }
                }
              }
            }
          }
        }

        CIMRenderer renderer = Layer.GetRenderer();
        CIMSimpleRenderer simpleRenderer = renderer as CIMSimpleRenderer;
        CIMUniqueValueRenderer uniqueValueRendererRenderer = renderer as CIMUniqueValueRenderer;
        CIMSymbolReference symbolRef = simpleRenderer?.Symbol ?? uniqueValueRendererRenderer?.DefaultSymbol;
        CIMSymbol symbol = symbolRef?.Symbol;
        CIMColor cimColor = symbol?.GetColor();
        double[] colorValues = cimColor?.Values;

        int red = colorValues != null && colorValues.Length >= 1 ? (int) colorValues[0] : 255;
        int green = colorValues != null && colorValues.Length >= 2 ? (int) colorValues[1] : 255;
        int blue = colorValues != null && colorValues.Length >= 3 ? (int) colorValues[2] : 255;
        int alpha = colorValues != null && colorValues.Length >= 4 ? (int) colorValues[3] : 255;
        color = Color.FromArgb(alpha, red, green, blue);
      });

      GeoJsonChanged = (Color != color);
      Color = color;
      string newJson = featureCollection?.ToString();
      GeoJsonChanged = newJson != GeoJson?.ToString() || GeoJsonChanged;
      return GeoJson = featureCollection;
    }

    public async Task<double> GetOffsetZAsync()
    {
      return await QueuedTask.Run(() =>
      {
        CIMBaseLayer cimBaseLayer = Layer?.GetDefinition();
        CIMBasicFeatureLayer cimBasicFeatureLayer = cimBaseLayer as CIMBasicFeatureLayer;
        CIMLayerElevationSurface layerElevation = cimBasicFeatureLayer?.LayerElevation;
        return layerElevation?.OffsetZ ?? 0.0;
      });
    }

    private async Task<ICoordinate> GeoJsonCoordAsync(MapPoint point)
    {
      bool hasZ = point.HasZ;
      double z = hasZ ? point.Z + await GetOffsetZAsync() : 0.0;
      return hasZ ? CoordinateFactory.Create(point.X, point.Y, z) : CoordinateFactory.Create(point.X, point.Y);
    }

    public async Task LoadMeasurementsAsync()
    {
      await ReloadSelectionAsync();

      if (_measurementList.Count >= 1)
      {
        _selection = new List<long>();
      }

      foreach (KeyValuePair<int, Measurement> keyValue in _measurementList)
      {
        Measurement measurement = keyValue.Value;
        long? objectId = measurement?.ObjectId;

        if (objectId != null)
        {
          _selection.Add((long) objectId);
        }
      }
    }

    public async Task AddFeatureAsync(Geometry geometry)
    {
      var editOperation = new EditOperation
      {
        Name = $"Add feature to layer: {Name}",
        SelectNewFeatures = true,
        ShowModalMessageAfterFailure = false
      };

      editOperation.Create(Layer, geometry);
      await editOperation.ExecuteAsync();
    }

    public async Task UpdateFeatureAsync(long uid, Geometry geometry)
    {
      await QueuedTask.Run(() =>
      {
        using (FeatureClass featureClass = Layer.GetFeatureClass())
        {
          FeatureClassDefinition definition = featureClass?.GetDefinition();
          string objectIdField = definition?.GetObjectIDField();
          QueryFilter filter = new QueryFilter {WhereClause = $"{objectIdField} = {uid}"};

          using (RowCursor existsResult = featureClass?.Search(filter, false))
          {
            while (existsResult?.MoveNext() ?? false)
            {
              using (Row row = existsResult.Current)
              {
                Feature feature = row as Feature;
                feature?.SetShape(geometry);
                feature?.Store();
              }
            }
          }
        }
      });
    }

    private async Task<List<Measurement>> ReloadSelectionAsync()
    {
      List<Measurement> result = new List<Measurement>();
      bool thisMeasurement = false;

      if (Layer.SelectionCount >= 1)
      {
        await QueuedTask.Run(async () =>
        {
          Selection selectionFeatures = Layer?.GetSelection();

          using (RowCursor rowCursur = selectionFeatures?.Search())
          {
            while (rowCursur?.MoveNext() ?? false)
            {
              Row row = rowCursur.Current;
              Feature feature = row as Feature;
              Geometry geometry = feature?.GetShape();
              long objectId = feature?.GetObjectID() ?? -1;

              if (geometry != null && objectId != -1)
              {
                Measurement measurement = _measurementList.Get(objectId) ?? await _measurementList.GetAsync(geometry);
                thisMeasurement = true;
                _measurementList.DrawPoint = false;
                EditTools editTool = _vectorLayerList.EditTool;

                if (editTool != EditTools.SketchLineTool && editTool != EditTools.SketchPointTool &&
                    editTool != EditTools.SketchPolygonTool)
                {
                  measurement = _measurementList.StartMeasurement(geometry, measurement, false, objectId, this);
                }

                _measurementList.DrawPoint = true;

                if (measurement != null)
                {
                  await measurement.UpdateMeasurementPointsAsync(geometry);
                  measurement.CloseMeasurement();
                  result.Add(measurement);
                }
              }
            }
          }
        });
      }

      if (thisMeasurement && _vectorLayerList.EditTool == EditTools.SketchPointTool)
      {
        _vectorLayerList.SketchFinished();
        await _vectorLayerList.StartSketchToolAsync();
      }

      return result;
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Edit events

    private async void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
    {
      bool contains = false;

      foreach (var selection in args.Selection)
      {
        MapMember mapMember = selection.Key;
        FeatureLayer layer = mapMember as FeatureLayer;

        if (layer == Layer)
        {
          _selection = selection.Value;
          contains = true;
          await GenerateJsonAsync();

          if (_vectorLayerList.EditTool != EditTools.SketchPointTool)
          {
            Measurements = await ReloadSelectionAsync();
          }
          else
          {
            _updateMeasurements = true;
          }
        }
      }

      if (!contains && _selection != null)
      {
        Measurements = null;
        _selection = null;
        await GenerateJsonAsync();
      }
    }

    private async void OnRowChanged(RowChangedEventArgs args)
    {
      await QueuedTask.Run(async () =>
      {
        Row row = args.Row;
        Feature feature = row as Feature;
        Geometry geometry = feature?.GetShape();
        long objectId = feature?.GetObjectID() ?? -1;
        Measurement measurement = _measurementList.Get(objectId);
        _measurementList.DrawPoint = false;
        measurement = _measurementList.StartMeasurement(geometry, measurement, false, objectId, this);
        _measurementList.DrawPoint = true;

        if (measurement != null)
        {
          await measurement.UpdateMeasurementPointsAsync(geometry);
          measurement.CloseMeasurement();
        }
      });
    }

    private async void OnRowDeleted(RowChangedEventArgs args)
    {
      await QueuedTask.Run(() =>
      {
        Row row = args.Row;
        Feature feature = row as Feature;
        long objectId = feature?.GetObjectID() ?? -1;

        if (_selection?.Contains(objectId) ?? false)
        {
          _selection.Remove(objectId);
        }

        if (GlobeSpotterConfiguration.MeasurePermissions)
        {
          Measurement measurement = _measurementList.Get(objectId);
          measurement?.RemoveMeasurement();
        }
      });
    }

    private async void OnRowCreated(RowChangedEventArgs args)
    {
      await QueuedTask.Run(async () =>
      {
        Row row = args.Row;
        Feature feature = row as Feature;
        Geometry geometry = feature?.GetShape();
        const double e = 0.1;

        if (geometry?.GeometryType == GeometryType.Point)
        {
          if (geometry is MapPoint srcPoint && Math.Abs(srcPoint.Z) < e)
          {
            MapPoint dstPoint = await _vectorLayerList.AddHeightToMapPointAsync(srcPoint);
            ElevationCapturing.ElevationConstantValue = dstPoint.Z;
            feature.SetShape(dstPoint);
          }
        }
      });
    }

    private async void OnDrawCompleted(MapViewEventArgs args)
    {
      MapView mapView = args.MapView;
      Geometry sketchGeometry = await mapView.GetCurrentSketchAsync();

      if (sketchGeometry == null)
      {
        await GenerateJsonAsync();
      }

      if (_updateMeasurements)
      {
        _updateMeasurements = false;
        Measurements = await ReloadSelectionAsync();
      }
    }

    #endregion

    #region Disposable

    public async Task DisposeAsync()
    {
      await QueuedTask.Run(() =>
      {
        if (_rowChanged != null)
        {
          RowChangedEvent.Unsubscribe(_rowChanged);
        }

        if (_rowDeleted != null)
        {
          RowDeletedEvent.Unsubscribe(_rowDeleted);
        }

        if (_rowCreated != null)
        {
          RowCreatedEvent.Unsubscribe(_rowCreated);
        }
      });

      MapSelectionChangedEvent.Unsubscribe(OnMapSelectionChanged);
      DrawCompleteEvent.Unsubscribe(OnDrawCompleted);
    }

    #endregion
  }
}
