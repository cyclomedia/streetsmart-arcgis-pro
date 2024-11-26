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

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Events;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Editing.Templates;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using Nancy.Json;
using StreetSmart.Common.Factories;
using StreetSmart.Common.Interfaces.API;
using StreetSmart.Common.Interfaces.Data;
using StreetSmart.Common.Interfaces.GeoJson;
using StreetSmart.Common.Interfaces.SLD;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ColorConverter = StreetSmartArcGISPro.Utilities.ColorConverter;
using GeometryType = ArcGIS.Core.Geometry.GeometryType;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using StreetSmartModule = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using Unit = ArcGIS.Core.Geometry.Unit;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;

namespace StreetSmartArcGISPro.VectorLayers
{
  public class VectorLayer : INotifyPropertyChanged
  {
    #region Enums

    public enum VectorLayerVisibilityChangeStatus
    {
      Undefined,
      InUpdate,
      Updated
    }

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly ViewerList _viewerList;
    private readonly MeasurementList _measurementList;
    private readonly VectorLayerList _vectorLayerList;
    private readonly FileConfiguration _configuration;
    private readonly StoredLayerList _storedLayerList;

    private SubscriptionToken _rowChanged;
    private SubscriptionToken _rowDeleted;
    private SubscriptionToken _rowCreated;

    private IFeatureCollection _geoJson;
    private IList<long> _selection;
    private bool _updateMeasurements;

    private string _clickedViewerId;

    #endregion

    #region Constructor

    public VectorLayer(FeatureLayer layer, VectorLayerList vectorLayerList)
    {
      _vectorLayerList = vectorLayerList;
      Layer = layer;
      Overlay = null;
      _selection = null;
      _updateMeasurements = false;
      _configuration = FileConfiguration.Instance;
      _storedLayerList = StoredLayerList.Instance;

      StreetSmartModule streetSmart = StreetSmartModule.Current;
      _viewerList = streetSmart.ViewerList;
      _measurementList = streetSmart.MeasurementList;
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

    public IStyledLayerDescriptor Sld { get; private set; }

    public FeatureLayer Layer { get; }

    public IOverlay Overlay { get; set; }

    public bool GeoJsonChanged { get; private set; }

    public string Name => Layer?.Name ?? string.Empty;

    public string NameAndUri => Layer?.Name + "___" + Layer?.URI ?? string.Empty;

    public bool IsLayerVisible => Layer != null && Layer.IsVisible;

    public VectorLayerVisibilityChangeStatus VisibilityChangeStatus { get; internal set; }

    public bool DesiredOverlayVisibility => CalculateOverlayVisibility();


    //GC: Adding global counter variable to make sure that object infos are not being overwritten
    public static int Counter = 0;

    #endregion

    #region Functions

    public async Task<bool> InitializeEventsAsync()
    {
      if (Layer.ConnectionStatus != ConnectionStatus.Connected)
      {
        await LoadMeasurementsAsync();
        return false;
      }

      MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
      DrawCompleteEvent.Subscribe(OnDrawCompleted);

      await QueuedTask.Run(() =>
      {
        try
        {
          var table = Layer?.GetTable();
          if (table != null)
          {
            _rowChanged = RowChangedEvent.Subscribe(OnRowChanged, table);
            _rowDeleted = RowDeletedEvent.Subscribe(OnRowDeleted, table);
            _rowCreated = RowCreatedEvent.Subscribe(OnRowCreated, table);
          }
        }
        catch (Exception e)
        {
          EventLog.Write(EventLogLevel.Warning, $"Street Smart: (VectorLayer.cs) (InitializeEventsAsync) error: {e}");
        }
      });

      await LoadMeasurementsAsync();
      return true;
    }

    public async Task<IFeatureCollection> GenerateJsonAsync(MapView mapView, bool forceUpdateGeoJson = false)
    {
      EventLog.Write(EventLogLevel.Information, $"Street Smart: (VectorLayer.cs) (GenerateJsonAsync)");
      Map map = mapView?.Map;
      SpatialReference mapSpatRef = map?.SpatialReference;

      Setting settings = ProjectList.Instance.GetSettings(mapView);
      MySpatialReference myCyclSpatRef = settings?.CycloramaViewerCoordinateSystem;

      SpatialReference cyclSpatRef = myCyclSpatRef == null
        ? mapSpatRef
        : (myCyclSpatRef.ArcGisSpatialReference ?? await myCyclSpatRef.CreateArcGisSpatialReferenceAsync());

      Unit unit = cyclSpatRef?.Unit;
      double factor = unit?.ConversionFactor ?? 1;
      IFeatureCollection featureCollection = null;
      GeoJsonChanged = forceUpdateGeoJson;
      double distanceFactor = (cyclSpatRef?.IsGeographic ?? true) ? factor : 1 / factor;

      if (Layer.Map == map)
      {
        await QueuedTask.Run(async () =>
        {
          SpatialReference layerSpatRef = Layer?.GetSpatialReference();
          IList<IList<Segment>> geometries = [];
          ICollection<Viewer> viewers = _viewerList.Viewers;

          var viewerTasks = viewers.Select(viewer => Task.Run(() =>
          {
            double distance = settings?.OverlayDrawDistance ?? 0.0;
            ICoordinate coordinate = viewer.Coordinate;

            if (coordinate != null)
            {
              distance = distance * distanceFactor;

              double x = coordinate.X ?? 0.0;
              double y = coordinate.Y ?? 0.0;
              double xMin = x - distance;
              double xMax = x + distance;
              double yMin = y - distance;
              double yMax = y + distance;

              Envelope envelope = EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax, cyclSpatRef);
              Envelope copyEnvelope = envelope;

              if (layerSpatRef?.Wkid != 0 && cyclSpatRef?.Wkid != 0)
              {
                try
                {
                  if (layerSpatRef != null)
                  {
                    ProjectionTransformation projection = ProjectionTransformation.Create(cyclSpatRef, layerSpatRef);
                    copyEnvelope = GeometryEngine.Instance.ProjectEx(envelope, projection) as Envelope;
                  }
                }
                catch (Exception ex)
                {
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (VectorLayer.cs) (GenerateJsonAsync) {ex}");
                }
              }

              Polygon copyPolygon = PolygonBuilderEx.CreatePolygon(copyEnvelope, layerSpatRef);
              ReadOnlyPartCollection polygonParts = copyPolygon.Parts;

              using (IEnumerator<ReadOnlySegmentCollection> polygonSegments = polygonParts.GetEnumerator())
              {
                IList<Segment> segments = [];
                //this is where the pop-up keeps minimizing
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
          })).ToArray();
          await Task.WhenAll(viewerTasks);

          featureCollection = GeoJsonFactory.CreateFeatureCollection(layerSpatRef?.Wkid ?? 0);

          if (DesiredOverlayVisibility)
          {
            List<long> objectIds = [];

            var featureTasks = geometries.Select(async geom =>
            {
#if ARCGISPRO29
              Polygon polygon = PolygonBuilder.CreatePolygon(geom, layerSpatRef);
#else
              Polygon polygon = PolygonBuilderEx.CreatePolygon(geom, layerSpatRef);
#endif
              //this is where the new pop-up attributes are made
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
                    var fieldValues = GetPropertiesFromRow(existsResult);

                    Row row = existsResult.Current;
                    Feature feature = row as Feature;
                    Geometry geometry = feature?.GetShape();
                    long objectId = feature.GetObjectID();

                    if (!objectIds.Contains(objectId))
                    {
                      objectIds.Add(objectId);
                      GeometryType geometryType = geometry?.GeometryType ?? GeometryType.Unknown;
                      Geometry copyGeometry = geometry;

                      if (geometry != null && layerSpatRef.Wkid != 0 && cyclSpatRef != null)
                      {
                        ProjectionTransformation projection = ProjectionTransformation.Create(layerSpatRef, cyclSpatRef);
                        copyGeometry = GeometryEngine.Instance.ProjectEx(geometry, projection);
                      }

                      if (copyGeometry != null)
                      {
                        switch (geometryType)
                        {
                          case GeometryType.Point:
                            if (copyGeometry is MapPoint point)
                            {
                              ICoordinate coordinate = await GeoJsonCoordAsync(point);
                              var featurePoint = GeoJsonFactory.CreatePointFeature(coordinate);
                              AddFieldValueToFeature(featurePoint, fieldValues);
                              featureCollection.Features.Add(featurePoint);
                            }

                            break;
                          case GeometryType.Polygon:
                            if (copyGeometry is Polygon polygonGeoJson)
                            {
                              ReadOnlyPartCollection polygonParts = polygonGeoJson.Parts;
                              IList<IList<ICoordinate>> polygonCoordinates = [];

                              using (IEnumerator<ReadOnlySegmentCollection> polygonSegments = polygonParts.GetEnumerator())
                              {
                                while (polygonSegments.MoveNext())
                                {
                                  ReadOnlySegmentCollection segments = polygonSegments.Current;
                                  IList<ICoordinate> coordinates = [];

                                  if (segments != null)
                                  {
                                    for (int i = 0; i < segments.Count; i++)
                                    {
                                      if (segments[i].SegmentType == SegmentType.Line)
                                      {
                                        MapPoint polygonPoint = segments[i].StartPoint;
                                        coordinates.Add(await GeoJsonCoordAsync(polygonPoint));

                                        if (i == segments.Count - 1)
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

                              var featurePolygon = GeoJsonFactory.CreatePolygonFeature(polygonCoordinates);
                              AddFieldValueToFeature(featurePolygon, fieldValues);
                              featureCollection.Features.Add(featurePolygon);
                            }

                            break;
                          case GeometryType.Polyline:
                            if (copyGeometry is Polyline polyLineGeoJson)
                            {
                              ReadOnlyPartCollection polyLineParts = polyLineGeoJson.Parts;

                              using (IEnumerator<ReadOnlySegmentCollection> polyLineSegments = polyLineParts.GetEnumerator())
                              {
                                while (polyLineSegments.MoveNext())
                                {
                                  ReadOnlySegmentCollection segments = polyLineSegments.Current;
                                  IList<ICoordinate> coordinates = [];

                                  if (segments != null)
                                  {
                                    for (int i = 0; i < segments.Count; i++)
                                    {
                                      if (segments[i].SegmentType == SegmentType.Line)
                                      {
                                        MapPoint linePoint = segments[i].StartPoint;
                                        coordinates.Add(await GeoJsonCoordAsync(linePoint));

                                        if (i == segments.Count - 1)
                                        {
                                          linePoint = segments[i].EndPoint;
                                          coordinates.Add(await GeoJsonCoordAsync(linePoint));
                                        }
                                      }
                                    }
                                  }

                                  var featureLine = GeoJsonFactory.CreateLineFeature(coordinates);
                                  AddFieldValueToFeature(featureLine, fieldValues);
                                  featureCollection.Features.Add(featureLine);

                                }
                              }
                            }

                            break;
                          case GeometryType.Envelope:
                          case GeometryType.Multipatch:
                          case GeometryType.Multipoint:
                          case GeometryType.Unknown:
                            break;
                        }
                      }
                    }
                    //this is where the point is made
                    GeoJsonChanged = await CreateSld(featureCollection) || GeoJsonChanged;
                  }
                }
              }
            }).ToArray();
            await Task.WhenAll(featureTasks);
          }
        });
      
        GeoJsonChanged = (featureCollection != null && !featureCollection.Equals(GeoJson)) || GeoJsonChanged;
        GeoJson = featureCollection;
      }

      EventLog.Write(EventLogLevel.Information, $"Street Smart: (VectorLayer.cs) (GenerateJsonAsync) Generated geoJson finished");
      return featureCollection;
    }

    private bool CalculateOverlayVisibility()
    {
      return ShouldSyncLayersVisibility()
          ? IsLayerVisible
          : StoredLayerList.Instance.GetVisibility(NameAndUri);
    }

    private bool ShouldSyncLayersVisibility()
    {
      bool? syncLayerVisibility = ProjectList.Instance.GetSettings(MapView.Active).SyncLayerVisibility;
      var result = syncLayerVisibility ?? Configuration.File.Configuration.Instance.IsSyncOfVisibilityEnabled;
      return result;
    }

    //fix missing line feature bug with this new method
    private void AddFieldValueToFeature(IFeature feature, Dictionary<string, string> fieldValues)
    {
      foreach (var fieldValue in fieldValues)
      {
        if (!feature.Properties.ContainsKey(fieldValue.Key))
        {
          //GC: made to fix apostrophe error for symbology
          if (fieldValue.Value.Contains("'"))
          {
            feature.Properties.Add(fieldValue.Key, fieldValue.Value.Replace("'", ""));
          }
          else
          {
            feature.Properties.Add(fieldValue.Key, fieldValue.Value);
          }
        }
      }
    }

    private async Task<bool> CreateSld(IFeatureCollection featureCollection)
    {
      return await QueuedTask.Run(() =>
      {
        string oldSld = Sld?.GetSerializedSld();

        if (featureCollection.Features.Count >= 1)
        {
          Sld = SLDFactory.CreateEmptyStyle();

          CIMRenderer renderer = Layer?.GetRenderer();
          CIMSimpleRenderer simpleRenderer = renderer as CIMSimpleRenderer;
          CIMUniqueValueRenderer uniqueValueRendererRenderer = renderer as CIMUniqueValueRenderer;
          CIMSymbolReference symbolRef = simpleRenderer?.Symbol ?? uniqueValueRendererRenderer?.DefaultSymbol;

          if (uniqueValueRendererRenderer?.Groups != null)
          {
            var fields = uniqueValueRendererRenderer.Fields;

            foreach (var group in uniqueValueRendererRenderer.Groups)
            {
              foreach (var uniqueClass in group.Classes)
              {
                IFilter filter = null;

                foreach (var uniqueValue in uniqueClass.Values)
                {
                  for (int i = 0; i < fields.Length; i++)
                  {
                    string value = uniqueValue.FieldValues.Length >= i ? uniqueValue.FieldValues[i] : string.Empty;
                    //GC: made to fix apostrophe error for symbology
                    if (value.Contains("'"))
                    {
                      value = value.Replace("'", "");
                    }
                    filter = SLDFactory.CreateEqualIsFilter(fields[i], value);

                    CIMSymbolReference uniqueSymbolRef = uniqueClass.Symbol;
                    ISymbolizer symbolizer = CreateSymbolizer(uniqueSymbolRef);
                    IRule rule = SLDFactory.CreateRule(symbolizer, filter);
                    SLDFactory.AddRuleToStyle(Sld, rule);
                  }
                }
                /*CIMSymbolReference uniqueSymbolRef = uniqueClass.Symbol;
                ISymbolizer symbolizer = CreateSymbolizer(uniqueSymbolRef);
                IRule rule = SLDFactory.CreateRule(symbolizer, filter);
                SLDFactory.AddRuleToStyle(Sld, rule);*/
              }
            }
          }
          else
          {
            ISymbolizer symbolizer = CreateSymbolizer(symbolRef);
            IRule rule = SLDFactory.CreateRule(symbolizer);
            SLDFactory.AddRuleToStyle(Sld, rule);
          }
        }
        return !(oldSld?.Equals(Sld?.GetSerializedSld()) ?? false);
      });
    }

    private ISymbolizer CreateSymbolizer(CIMSymbolReference symbolRef)
    {
      double strokeWidth = 1.0;
      double strokeOpacity = 1.0;
      CIMSymbol symbol = symbolRef?.Symbol;
      CIMColor cimColor = symbol?.GetColor();
      CIMColor cimStroke = null;
      double fillOpacity = (cimColor?.Alpha ?? 100) / 100;

      if (symbol is CIMPointSymbol pointSymbol)
      {
        foreach (CIMSymbolLayer layer in pointSymbol.SymbolLayers)
        {
          if (layer is CIMVectorMarker vectorMarker)
          {
            foreach (var markerGraphics in vectorMarker.MarkerGraphics)
            {
              if (markerGraphics.Symbol is CIMPolygonSymbol polygonSymbol)
              {
                foreach (CIMSymbolLayer layer2 in polygonSymbol.SymbolLayers)
                {
                  if (layer2 is CIMSolidStroke stroke)
                  {
                    cimStroke = stroke.Color;
                    strokeWidth = stroke.Width;
                    strokeOpacity = cimStroke.Alpha / 100;
                  }
                  else if (layer2 is CIMSolidFill fill)
                  {
                    cimColor = fill.Color;
                    fillOpacity = cimColor.Alpha / 100;
                  }
                }
              }
            }

            Color color = CimColorToWinColor(cimColor);
            Color? strokeColor = cimStroke == null ? null : CimColorToWinColor(cimStroke);

            return strokeColor != null
              ? SLDFactory.CreateStylePoint(SymbolizerType.Circle, 10.0, color, fillOpacity, strokeColor, strokeWidth, strokeOpacity)
              : SLDFactory.CreateStylePoint(SymbolizerType.Circle, 10.0, color);
          }
          else if (layer is CIMPictureMarker pictureMarker)
          {
            double size = pictureMarker.Size;
            string url = pictureMarker.URL;

            string[] parts = url.Split(';');
            string base64 = parts.Length >= 2 ? parts[1] : string.Empty;
            base64 = base64.Replace("base64,", string.Empty);
            return SLDFactory.CreateImageSymbol(size, base64);
          }
        }
      }
      else if (symbol is CIMLineSymbol)
      {
        Color color = CimColorToWinColor(cimColor);
        return SLDFactory.CreateStyleLine(color, null, fillOpacity);
      }
      else if (symbol is CIMPolygonSymbol polygonSymbol)
      {
        Color color = CimColorToWinColor(cimColor);
        var stroke = polygonSymbol.SymbolLayers.OfType<CIMSolidStroke>().FirstOrDefault();
        var strokeColor = stroke == null ? (Color?)null : CimColorToWinColor(stroke.Color);
        return SLDFactory.CreateStylePolygon(color, fillOpacity, strokeColor, stroke?.Width);
      }

      return null;
    }

    private Color CimColorToWinColor(CIMColor cimColor)
    {
      int red, green, blue, alpha;

      if (cimColor is CIMHSVColor)
      {
        double[] colorValues = cimColor?.Values;
        double h = colorValues != null && colorValues.Length >= 1 ? colorValues[0] : 0.0;
        double s = colorValues != null && colorValues.Length >= 2 ? colorValues[1] : 0.0;
        double v = colorValues != null && colorValues.Length >= 3 ? colorValues[2] : 0.0;
        alpha = colorValues != null && colorValues.Length >= 4 ? (int)colorValues[3] : 255;

        //GC: added catch statements that turns the s and v values to percentages because it was causing incorrect overlay colors
        if (s > 1)
          s = s / 100;
        if (v > 1)
          v = v / 100;

        Hsv data = new Hsv(h, s, v);
        Rgb value = ColorConverter.HsvToRgb(data);
        red = value.R;
        green = value.G;
        blue = value.B;
      }
      else
      {
        double[] colorValues = cimColor?.Values;
        red = colorValues != null && colorValues.Length >= 1 ? (int)colorValues[0] : 255;
        green = colorValues != null && colorValues.Length >= 2 ? (int)colorValues[1] : 255;
        blue = colorValues != null && colorValues.Length >= 3 ? (int)colorValues[2] : 255;
        alpha = colorValues != null && colorValues.Length >= 4 ? (int)colorValues[3] : 255;
      }

      return Color.FromArgb(alpha, red, green, blue);
    }

    public async Task<double> GetOffsetZAsync()
    {
      if (Layer == null) return 0.0;

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
      double z = point.HasZ ? point.Z + await GetOffsetZAsync() : 0.0;
      return point.HasZ
          ? CoordinateFactory.Create(point.X, point.Y, z)
          : CoordinateFactory.Create(point.X, point.Y);
    }

    public async void SelectFeature(IJson properties, MapView mapView, string id)
    {
      await QueuedTask.Run(() =>
      {
        _clickedViewerId = id;

        using (FeatureClass featureClass = Layer.GetFeatureClass())
        {
          FeatureClassDefinition definition = featureClass?.GetDefinition();
          string objectIdField = definition?.GetObjectIDField();

          if (!string.IsNullOrEmpty(objectIdField) && properties.ContainsKey(objectIdField))
          {
            string objectId = properties[objectIdField];
            QueryFilter filter = new QueryFilter { WhereClause = $"{objectIdField} = {objectId}" };

            using (RowCursor existsResult = featureClass.Search(filter, false))
            {
              while (existsResult?.MoveNext() ?? false)
              {
                using (Row row = existsResult.Current)
                {
                  Feature feature = row as Feature;
                  Geometry geometry = feature?.GetShape();
                  Geometry selectGeometry = geometry;

                  if (geometry is Polyline || geometry is Polygon)
                  {
                    var parts = geometry is Polygon polygon ? polygon.Parts : (geometry as Polyline).Parts;

                    using (IEnumerator<ReadOnlySegmentCollection> lineSegments = parts.GetEnumerator())
                    {
                      if (lineSegments.MoveNext())
                      {
                        ReadOnlySegmentCollection segments = lineSegments.Current;

                        if (segments != null)
                        {
                          if (segments.Count >= 1 && segments[0].SegmentType == SegmentType.Line)
                          {
                            selectGeometry = segments[0].StartPoint;
                          }
                        }
                      }
                    }
                  }
                  //this is the function that generates the pop-ups
                  mapView.SelectFeatures(selectGeometry);
                }
              }
            }
          }
        }
      });
    }

    public async Task<MapPoint> AddHeightToMapPointAsync(MapPoint mapPoint, MapView mapView)
    {
      return await _vectorLayerList.AddHeightToMapPointAsync(mapPoint, mapView);
    }

    public async Task LoadMeasurementsAsync()
    {
      await ReloadSelectionAsync();

      if (_measurementList.Any())
      {
        _selection = [];
      }
    }

    public async Task AddUpdateFeature(long? objectId, Geometry sketch, Measurement measurement)
    {
      if (objectId == null || _vectorLayerList.EditTool != EditTools.Verticles)
      {
        await AddFeatureAsync(sketch, measurement);
      }
      else
      {
        await UpdateFeatureAsync((long)objectId, sketch, measurement);
      }
    }

    public async Task AddFeatureAsync(Geometry geometry, Measurement measurement)
    {
      await QueuedTask.Run(async () =>
      {
        var editOperation = new EditOperation
        {
          Name = $"Add feature to layer: {Name}",
          SelectNewFeatures = true,
          ShowModalMessageAfterFailure = false
        };

        geometry = await ToRealSpatialReference(geometry, measurement);
        EditingTemplate editingFeatureTemplate = EditingTemplate.Current;

        double measurementX = 0;
        double measurementY = 0;
        double measurementZ = 0;

        var serializer = new JavaScriptSerializer();

        if (measurement.Count >= 1)
        {
          var measurementGeoJson = serializer.Serialize(measurement[0].Feature.Geometry);

          try
          {
            measurementX = serializer.Deserialize<Dictionary<string, double>>(measurementGeoJson)["x"];
            measurementY = serializer.Deserialize<Dictionary<string, double>>(measurementGeoJson)["y"];
            measurementZ = serializer.Deserialize<Dictionary<string, double>>(measurementGeoJson)["z"];
          }
          catch (Exception)
          {
            try
            {
              measurementX = serializer.Deserialize<List<Dictionary<string, double>>>(measurementGeoJson)[0]["x"];
              measurementY = serializer.Deserialize<List<Dictionary<string, double>>>(measurementGeoJson)[0]["y"];
              measurementZ = serializer.Deserialize<List<Dictionary<string, double>>>(measurementGeoJson)[0]["z"];
            }
            catch (Exception)
            {
              try
              {
                measurementX =
                  serializer.Deserialize<List<List<Dictionary<string, double>>>>(measurementGeoJson)[0][0]["x"];
                measurementY =
                  serializer.Deserialize<List<List<Dictionary<string, double>>>>(measurementGeoJson)[0][0]["y"];
                measurementZ =
                  serializer.Deserialize<List<List<Dictionary<string, double>>>>(measurementGeoJson)[0][0]["z"];
              }
              catch (Exception e)
              {
                EventLog.Write(EventLogLevel.Error, $"Street Smart: (VectorLayer.cs) (AddFeatureAsync) error: {e}");
              }
            }
          }

#if ARCGISPRO29
          if (!(editingFeatureTemplate?.GetDefinition() is CIMBasicFeatureTemplate definition) || definition.DefaultValues == null)
#else
          if (editingFeatureTemplate?.GetDefinition() is not CIMRowTemplate definition || definition.DefaultValues == null)
#endif
          {
            editOperation.Create(Layer, geometry);
            await editOperation.ExecuteAsync();
          }
          else
          {
            Dictionary<string, object> toAddFields = [];

            foreach (var value in definition.DefaultValues)
            {
              toAddFields.Add(value.Key, value.Value);
            }

            toAddFields.Add("Shape", geometry);

            foreach (var value in Layer.GetFieldDescriptions())
            {
              switch (value.Name)
              {
                case "x":
                  toAddFields["x"] = measurementX;
                  break;
                case "y":
                  toAddFields["y"] = measurementY;
                  break;
                case "z":
                  toAddFields["z"] = measurementZ;
                  break;
                case "ZGeom":
                  toAddFields["ZGeom"] = measurementZ;
                  break;
              }
            }

            editOperation.Create(Layer, toAddFields);
            await editOperation.ExecuteAsync();
          }
        }
      });
    }

    public async Task<Geometry> ToRealSpatialReference(Geometry geometry, Measurement measurement)
    {
      await QueuedTask.Run(async () =>
      {
        if (geometry != null)
        {
          SpatialReference spatialReference = Layer?.GetSpatialReference();
          GeometryType geometryType = geometry.GeometryType;
          var points = await measurement.ToPointCollectionAsync(geometry);

          switch (geometryType)
          {
            case GeometryType.Polygon:
#if ARCGISPRO29
              geometry = PolygonBuilder.CreatePolygon(points, spatialReference);
#else
              geometry = PolygonBuilderEx.CreatePolygon(points, spatialReference);
#endif
              break;
            case GeometryType.Polyline:
#if ARCGISPRO29
              geometry = PolylineBuilder.CreatePolyline(points, spatialReference);
#else
              geometry = PolylineBuilderEx.CreatePolyline(points, spatialReference);
#endif
              break;
            case GeometryType.Point:
              if (points.Count >= 1)
              {
                geometry = MapPointBuilderEx.CreateMapPoint(points[0], spatialReference);
              }

              break;
          }
        }
      });

      return geometry;
    }

    public async Task UpdateFeatureAsync(long uid, Geometry geometry, Measurement measurement)
    {
      var editOperation = new EditOperation
      {
        Name = $"Update feature {uid} in layer: {Name}",
        SelectNewFeatures = true,
        ShowModalMessageAfterFailure = false
      };

      geometry = await ToRealSpatialReference(geometry, measurement);
      editOperation.Modify(Layer, uid, geometry);
      await editOperation.ExecuteAsync();
    }

    private async Task ReloadSelectionAsync()
    {
      if (Layer.SelectionCount < 1 || _measurementList.Api == null || !await _measurementList.Api.GetApiReadyState())
      {
        return;
      }

      IList<IViewer> viewers = await _measurementList.Api.GetViewers();
      if (!viewers.Any())
      {
        return;
      }

      _vectorLayerList.LastSelectedLayer = this;

      await QueuedTask.Run(async () =>
      {
        try
        {
          Selection selectionFeatures = Layer?.GetSelection();
          if (selectionFeatures == null)
          {
            return;
          }

          using (RowCursor rowCursor = selectionFeatures.Search())
          {
            while (rowCursor?.MoveNext() ?? false)
            {
              Dictionary<string, string> properties = GetPropertiesFromRow(rowCursor);
              IJson json = JsonFactory.Create(properties);

              foreach (IViewer viewer in viewers)
              {
                if (viewer is IPanoramaViewer panoramaViewer && Overlay != null && Counter == 0 && await viewer.GetId() == _clickedViewerId)
                {
                  panoramaViewer.SetSelectedFeatureByProperties(json, Overlay.Id);
                  ++Counter;
                }

              }
            }
          }
        }
        catch (NullReferenceException e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (VectorLayer.cs) (ReloadSelectionAsync) error: {e}");
        }
      });
    }

    public Dictionary<string, string> GetPropertiesFromRow(RowCursor rowCursor)
    {
      bool isExceptionAlreadyLogged = false;
      Dictionary<string, string> properties = [];
      Row row = rowCursor.Current;

      if (row is not Feature feature)
      {
        return properties;
      }

      IReadOnlyList<Field> fields = feature?.GetFields();

      if (fields == null || fields.Count == 0)
      {
        return properties;
      }

      foreach (Field field in fields)
      {
        string name = field.Name;
        int fieldId = rowCursor.FindField(name);

        try
        {
          properties.Add(name, feature.GetOriginalValue(fieldId)?.ToString() ?? string.Empty);
        }
        catch (ArgumentException e)
        {
          if (!isExceptionAlreadyLogged)
          {
            EventLog.Write(EventLogLevel.Warning, $"Street Smart: (VectorLayer.cs) (GetPropertiesFromRow) {e}");
            isExceptionAlreadyLogged = true;
          }
        }
        catch (Exception ex)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (VectorLayer.cs) (GetPropertiesFromRow) {ex}");
        }
      }

      return properties;
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
      MapView mapView = _vectorLayerList.GetMapViewFromMap(args.Map);
      _measurementList.ObjectId = null;

#if ARCGISPRO29
      foreach (var selection in args.Selection)
#else
      foreach (var selection in args.Selection.ToDictionary())
#endif

      {
        MapMember mapMember = selection.Key;
        FeatureLayer layer = mapMember as FeatureLayer;
        _measurementList.ObjectId = selection.Value.Count >= 1 ? selection.Value[0] : null;

        if (layer == Layer)
        {
          _selection = selection.Value;
          contains = true;

          try
          {
            //this is the function that updates the pop-ups or closes it
            await GenerateJsonAsync(mapView);

            if (_vectorLayerList.EditTool != EditTools.SketchPointTool)
            {
              await ReloadSelectionAsync();
            }
            else
            {
              _updateMeasurements = true;
            }
          }
          catch (Exception e)
          {
            EventLog.Write(EventLogLevel.Error, $"Street Smart: (VectorLayer.cs) (OnMapSelectionChanged) error: {e}");
          }
        }
      }

      if (!contains && _selection != null)
      {
        _selection = null;
        await ReloadSelectionAsync();
        await GenerateJsonAsync(mapView);
      }
    }

    private void OnRowChanged(RowChangedEventArgs args)
    {
      if (_vectorLayerList.EditTool == EditTools.Verticles)
      {
        _measurementList.Api.StopMeasurementMode();
      }
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
      });
    }

    private async void OnRowCreated(RowChangedEventArgs args)
    {
      Row row = args.Row;
      Feature feature = row as Feature;
      Geometry geometry = feature?.GetShape();
      const double e = 0.1;

      if (geometry?.GeometryType == GeometryType.Point)
      {
        if (geometry is MapPoint srcPoint && Math.Abs(srcPoint.Z) < e)
        {
          MapPoint dstPoint = await _vectorLayerList.AddHeightToMapPointAsync(srcPoint, MapView.Active);
          ElevationCapturing.ElevationConstantValue = dstPoint.Z;
        }
      }
    }

    private async void OnDrawCompleted(MapViewEventArgs args)
    {
      MapView mapView = args.MapView;
      Geometry sketchGeometry = await mapView.GetCurrentSketchAsync();

      if (sketchGeometry == null)
      {
        await GenerateJsonAsync(mapView);
      }

      if (_updateMeasurements)
      {
        _updateMeasurements = false;
        // Measurements = await ReloadSelectionAsync();
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
