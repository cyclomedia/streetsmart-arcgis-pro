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
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ArcGISProject = ArcGIS.Desktop.Core.Project;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;

#if !ARCGISPRO29
using ArcGIS.Core.Data.Exceptions;
#endif

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public abstract class CycloMediaLayer : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static Dictionary<FeatureLayer, SortedDictionary<int, int>> _yearMonth;

    private readonly CycloMediaGroupLayer _cycloMediaGroupLayer;
    private readonly ConstantsRecordingLayer _constants;

    private Envelope _lastextent;
    private FeatureCollection _addData;
    private bool _visible;

    #endregion

    #region Properties

    public abstract string Name { get; }

    public abstract string FcName { get; }

    public abstract string WfsRequest { get; }

    public abstract double MinimumScale { get; set; }

    public MapView MapView => _cycloMediaGroupLayer.MapView;

    public bool Visible
    {
      get => _visible;
      set
      {
        _visible = value;
        NotifyPropertyChanged();
      }
    }

    public bool IsRemoved { get; set; }

    public bool IsInitialized { get; private set; }

    public FeatureLayer Layer { get; private set; }

    public bool IsVisible => Layer != null && Layer.IsVisible;

    public bool InsideScale
    {
      get
      {
        Camera camera = MapView?.Camera;
        return camera != null && Layer != null && Math.Floor(camera.Scale) <= (MinimumScale = Layer.MinScale);
      }
    }

    protected static Dictionary<FeatureLayer, SortedDictionary<int, int>> YearMonth => _yearMonth ?? (_yearMonth = []);

    protected SortedDictionary<int, int> GetYearMonth(FeatureLayer layer)
    {
      if (layer != null)
      {
        if (!YearMonth.ContainsKey(layer))
        {
          YearMonth.Add(layer, []);
        }
      }

      return layer == null ? null : YearMonth[layer];
    }

    #endregion

    #region Constructor

    protected CycloMediaLayer(CycloMediaGroupLayer layer, Envelope initialExtent = null)
    {
      _constants = ConstantsRecordingLayer.Instance;
      _cycloMediaGroupLayer = layer;
      Visible = false;
      IsRemoved = true;
      _lastextent = initialExtent ?? MapView?.Extent;
      IsInitialized = false;
    }

    #endregion

    #region Functions

    protected abstract bool Filter(Recording recording);

    protected abstract Task PostEntryStepAsync(Envelope envelope);

    protected abstract void ClearYears();

    public async Task SetVisibleAsync(bool value)
    {
      await QueuedTask.Run(() =>
      {
        if (_cycloMediaGroupLayer.Contains(this))
        {
          Layer?.SetVisibility(value);
        }
      });

      if (value)
      {
        await RefreshAsync();
      }
    }

    public async Task<SpatialReference> GetSpatialReferenceAsync()
    {
      return await QueuedTask.Run(() =>
      {
        FeatureClass featureClass = Layer?.GetFeatureClass();
        FeatureClassDefinition featureClassDefinition = featureClass?.GetDefinition();
        return featureClassDefinition?.GetSpatialReference();
      });
    }

    private async Task<Envelope> GetExtentAsync(Envelope envelope)
    {
      return await QueuedTask.Run(() =>
      {
        SpatialReference spatialReference = GetSpatialReferenceAsync().Result;
        SpatialReference envSpat = envelope.SpatialReference;
        Envelope result;

        if (spatialReference != null && envSpat.Wkid != spatialReference.Wkid)
        {
          ProjectionTransformation projection = ProjectionTransformation.Create(envSpat, spatialReference);
          result = GeometryEngine.Instance.ProjectEx(envelope, projection) as Envelope;
        }
        else
        {
          result = (Envelope)envelope.Clone();
        }

        return result;
      });
    }

    public async Task UpdateLayerAsync()
    {
      if (Layer != null)
      {
        Layer oldLayer = Layer;
        await CreateFeatureLayerAsync();
        ClearYears();
        await RemoveLayersAsync(_cycloMediaGroupLayer.GroupLayer, [oldLayer]);
        await RefreshAsync();
      }
    }

    private async Task CreateFeatureLayerAsync()
    {
      Map map = MapView?.Map;
      SpatialReference spatialReference = null;
      Setting config = ProjectList.Instance.GetSettings(MapView);
      MySpatialReference spatialReferenceRecording = config.RecordingLayerCoordinateSystem;

      if (spatialReferenceRecording == null && map != null)
      {
        spatialReference = map.SpatialReference;
      }
      else
      {
        spatialReference = spatialReferenceRecording.ArcGisSpatialReference ?? await spatialReferenceRecording.CreateArcGisSpatialReferenceAsync();
      }

      int wkid = spatialReference?.Wkid ?? 0;
      string fixedMapName = FixMapNameByReplacingSpecialCharacters(map?.Name);
      string fcNameWkid = string.Concat(FcName, fixedMapName, wkid);
      var project = ArcGISProject.Current;
      await CreateFeatureClassAsync(project, fcNameWkid, spatialReference);
      Layer = await CreateLayerAsync(project, fcNameWkid, _cycloMediaGroupLayer.GroupLayer);
      await MakeEmptyAsync();
      await CreateUniqueValueRendererAsync();
      await project.SaveEditsAsync();
    }

    public async Task AddToLayersAsync()
    {
      Layer = null;
      Map map = MapView?.Map;

      if (map != null)
      {
        var layersByName = map.FindLayers(Name);
        Layer = layersByName.OfType<FeatureLayer>().FirstOrDefault();
        var layersToRemove = layersByName.Except([Layer]);
        await RemoveLayersAsync(map, layersToRemove);
      }

      var project = ArcGISProject.Current;

      if (!project.IsEditingEnabled)
      {
        await project.SetIsEditingEnabledAsync(true);
      }

      if (Layer == null)
      {
        await CreateFeatureLayerAsync();
      }
      else
      {
        await MakeEmptyAsync();
        await CreateUniqueValueRendererAsync();
        await project.SaveEditsAsync();
      }

      IsRemoved = false;
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
      LayersRemovedEvent.Subscribe(OnLayersRemoved);
      await RefreshAsync();
      IsInitialized = true;
    }

    private async Task<FeatureLayer> CreateLayerAsync(ArcGISProject project, string fcName, ILayerContainerEdit layerContainer)
    {
      return await QueuedTask.Run(() =>
      {
        string featureClassUrl = $@"{project.DefaultGeodatabasePath}\{fcName}";
        Uri uri = new Uri(featureClassUrl);
        //new 3.0 code
        var layerParams = new FeatureLayerCreationParams(uri);
        FeatureLayer result = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, layerContainer);
        result.SetName(Name);
        result.SetMinScale(MinimumScale);
        result.SetVisibility(true);
        result.SetEditable(true);
        return result;
      });
    }

    private async Task RemoveLayersAsync(ILayerContainerEdit layerContainer, IEnumerable<ArcGIS.Desktop.Mapping.Layer> layers)
    {
      await QueuedTask.Run(() =>
      {
        var intersectLayers = layerContainer?.Layers.Intersect(layers).ToArray();
        if (layerContainer != null && intersectLayers?.Length > 0)
        {
          layerContainer.RemoveLayers(intersectLayers);
        }
      });
    }

    public async Task DisposeAsync(bool fromGroup)
    {
      if (fromGroup)
      {
        await RemoveLayersAsync(_cycloMediaGroupLayer.GroupLayer, [Layer]);
      }

      Remove();
      IsInitialized = false;
    }

    public async Task<Recording> GetRecordingAsync(long uid)
    {
      return await QueuedTask.Run(() =>
      {
        Recording result = null;

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          if (featureClass != null)
          {
            var fields = Recording.Fields;
            string shapeFieldName = Recording.ShapeFieldName;
            FeatureClassDefinition definition = featureClass.GetDefinition();
            string objectIdField = definition.GetObjectIDField();

            QueryFilter filter = new QueryFilter
            {
              WhereClause = $"{objectIdField} = {uid}",
              SubFields =
                $"{fields.Aggregate(string.Empty, (current, field) => $"{current}{(string.IsNullOrEmpty(current) ? string.Empty : ", ")}{field.Key}")}, {shapeFieldName}"
            };

            using (RowCursor existsResult = featureClass.Search(filter, false))
            {
              while (existsResult.MoveNext())
              {
                using (Row row = existsResult.Current)
                {
                  if (row != null)
                  {
                    result = new Recording();

                    foreach (var field in fields)
                    {
                      string name = field.Key;
                      int nameId = existsResult.FindField(name);
                      object item = row.GetOriginalValue(nameId);
                      result.UpdateItem(name, item);
                    }

                    int shapeId = row.FindField(shapeFieldName);
                    object point = row.GetOriginalValue(shapeId);
                    result.UpdateItem(shapeFieldName, point);
                  }
                }
              }
            }
          }
        }

        return result;
      });
    }

    public async Task<Recording> GetRecordingAsync(string imageId)
    {
      return await QueuedTask.Run(() =>
      {
        Recording result = null;

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          if (featureClass != null)
          {
            var fields = Recording.Fields;
            string shapeFieldName = Recording.ShapeFieldName;
            string imageIdField = Recording.FieldImageId;

            QueryFilter filter = new QueryFilter
            {
              WhereClause = $"{imageIdField} = {imageId}",
              SubFields =
                $"{fields.Aggregate(string.Empty, (current, field) => $"{current}{(string.IsNullOrEmpty(current) ? string.Empty : ", ")}{field.Key}")}, {shapeFieldName}"
            };

            using (RowCursor existsResult = featureClass.Search(filter, false))
            {
              while (existsResult.MoveNext())
              {
                using (Row row = existsResult.Current)
                {
                  if (row != null)
                  {
                    result = new Recording();

                    foreach (var field in fields)
                    {
                      string name = field.Key;
                      int nameId = existsResult.FindField(name);
                      object item = row.GetOriginalValue(nameId);
                      result.UpdateItem(name, item);
                    }

                    int shapeId = row.FindField(shapeFieldName);
                    object point = row.GetOriginalValue(shapeId);
                    result.UpdateItem(shapeFieldName, point);
                  }
                }
              }
            }
          }
        }

        return result;
      });
    }

    public async Task RefreshAsync()
    {
      await QueuedTask.Run(() =>
      {
        Envelope extent = MapView?.Extent;

        if (extent != null)
        {
          _lastextent = extent;
          _lastextent = _lastextent.Expand(5, 5, true);
          OnMapViewCameraChanged(null);
        }
      });
    }

    public async Task<double?> GetHeightAsync(double x, double y)
    {
      double? result = null;
      int count = 0;

      await QueuedTask.Run(() =>
      {
        using FeatureClass featureClass = Layer?.GetFeatureClass();
        if (featureClass != null)
        {
          const double searchBox = 25.0;
          double xMin = x - searchBox;
          double xMax = x + searchBox;
          double yMin = y - searchBox;
          double yMax = y + searchBox;
          Envelope envelope = EnvelopeBuilderEx.CreateEnvelope(xMin, xMax, yMin, yMax);

          SpatialQueryFilter spatialFilter = new SpatialQueryFilter
          {
            FilterGeometry = envelope,
            SpatialRelationship = SpatialRelationship.Contains,
            SubFields = $"{Recording.FieldGroundLevelOffset}, {Recording.FieldHeight}, SHAPE"
          };

          using RowCursor existsResult = featureClass.Search(spatialFilter, false);
          int groundLevelId = existsResult.FindField(Recording.FieldGroundLevelOffset);
          int heightId = existsResult.FindField(Recording.FieldHeight);

          while (existsResult.MoveNext())
          {
            using Row row = existsResult.Current;
            double? groundLevel = row?.GetOriginalValue(groundLevelId) as double?;
            double heightValue = row?.GetOriginalValue(heightId) as double? ?? 0.0;
            const double tolerance = 0.01;

            if (groundLevel != null)
            {
              Feature feature = row as Feature;
              Geometry geometry = feature?.GetShape();
              MapPoint point = geometry as MapPoint;
              double height = Math.Abs(heightValue) < tolerance ? point?.Z ?? 0 : heightValue;

              // ReSharper disable once AccessToModifiedClosure
              result = result ?? 0.0;
              result = result + height - ((double)groundLevel);
              count++;
            }
          }
        }
      });

      result = result != null ? result / Math.Max(count, 1) : null;
      return result;
    }

    protected virtual void Remove()
    {
      Layer = null;
      IsRemoved = true;
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
    }

    private async Task CreateUniqueValueRendererAsync()
    {
      ClearYears();

      await QueuedTask.Run(() =>
      {
        string[] fieldNames = [Recording.FieldYear, Recording.FieldPip, Recording.FieldIsAuthorized, Recording.FieldHasDepthMap];
        var uniqueValueRendererDefinition = new UniqueValueRendererDefinition();
        var uniqueValueRenderer = (CIMUniqueValueRenderer)Layer.CreateRenderer(uniqueValueRendererDefinition);
        uniqueValueRenderer.Fields = fieldNames;
        uniqueValueRenderer.DefaultLabel = string.Empty;
        uniqueValueRenderer.DefaultSymbol = null;
        uniqueValueRenderer.Groups = null;
        Layer.SetRenderer(uniqueValueRenderer);
      });
    }

    private async Task CreateFeatureClassAsync(ArcGISProject project, string fcName, SpatialReference spatialReference)
    {
      bool createNewFeatureClass = false;

      await QueuedTask.Run(() =>
      {
        string location = project.DefaultGeodatabasePath;
        Uri uriLocation = new Uri(location);
        FileGeodatabaseConnectionPath pathLocation = new FileGeodatabaseConnectionPath(uriLocation);

        using (var geodatabase = new Geodatabase(pathLocation))
        {
          try
          {
            using (geodatabase.OpenDataset<FeatureClass>(fcName))
            {
            }
          }
          catch (GeodatabaseException e)
          {
            EventLog.Write(EventLogLevel.Warning, $"Street Smart: (CycloMediaLayer.cs) (CreateFeatureClassAsync) error: {e}");
            createNewFeatureClass = true;
          }
        }
      });

      if (createNewFeatureClass)
      {
        FileUtils.GetFileFromAddIn("Recordings.FCRecordings.dbf", @"Recordings\FCRecordings.dbf");
        FileUtils.GetFileFromAddIn("Recordings.FCRecordings.shp", @"Recordings\FCRecordings.shp");
        FileUtils.GetFileFromAddIn("Recordings.FCRecordings.shx", @"Recordings\FCRecordings.shx");
        string template = Path.Combine(FileUtils.FileDir, @"Recordings\FCRecordings.shp");
        await CreateFeatureClass(project, fcName, template, spatialReference);

        await QueuedTask.Run(() =>
        {
          Map map = MapView?.Map;

          Layer thisLayer = map?.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(checkLayer => checkLayer.Name == fcName);

          if (thisLayer != null)
          {
            map.RemoveLayer(thisLayer);
          }
        });
      }
    }

    private async Task<IGPResult> CreateFeatureClass(ArcGISProject project, string fcName, string template, SpatialReference spatialReference)
    {
      var arguments = new List<object>
      {
        project.DefaultGeodatabasePath,
        fcName,
        "POINT",
        template,
        "DISABLED",
        "ENABLED",
        spatialReference
      };

      return await
        Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management",
          Geoprocessing.MakeValueArray(arguments.ToArray()));
    }

    public async Task<bool> MakeEmptyAsync()
    {
      return await QueuedTask.Run(() =>
      {
        var editOperation = new EditOperation
        {
          Name = Name,
          ShowModalMessageAfterFailure = false
        };

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          using (RowCursor rowCursor = featureClass?.Search(null, false))
          {
            if (rowCursor != null)
            {
              while (rowCursor.MoveNext())
              {
                using (Row row = rowCursor.Current)
                {
                  long objectId = row.GetObjectID();
                  editOperation.Delete(Layer, objectId);
                }
              }
            }
          }
        }

        return editOperation.IsEmpty ? Task.FromResult(true) : editOperation.ExecuteAsync();
      });
    }

    public async Task<EditOperation> SaveFeatureMembersAsync(FeatureCollection featureCollection, Envelope envelope)
    {
      return await QueuedTask.Run(() =>
      {
        var editOperation = new EditOperation
        {
          Name = Name,
          SelectNewFeatures = false,
          ShowModalMessageAfterFailure = false
        };

        if (featureCollection != null && featureCollection.NumberOfFeatures >= 1)
        {
          FeatureMembers featureMembers = featureCollection.FeatureMembers;
          Recording[] recordings = featureMembers?.Recordings;
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (SaveFeatureMembersAsync) Start writing number of recordings: {recordings?.Length ?? 0}");

          if (Layer != null && recordings != null)
          {
            string idField = Recording.ObjectId;
            var exists = new Dictionary<string, long>();

            using FeatureClass featureClass = Layer?.GetFeatureClass();
            if (featureClass == null)
            {
              return editOperation;
            }

            SpatialQueryFilter spatialFilter = new SpatialQueryFilter
            {
              FilterGeometry = envelope,
              SpatialRelationship = SpatialRelationship.Contains,
              SubFields = $"OBJECTID, {idField}"
            };

            using (RowCursor existsResult = featureClass.Search(spatialFilter, false))
            {
              int imId = existsResult.FindField(idField);

              while (existsResult.MoveNext())
              {
                using Row row = existsResult.Current;
                string recValue = row?.GetOriginalValue(imId) as string;

                if (!string.IsNullOrEmpty(recValue) && !exists.ContainsKey(recValue))
                {
                  long objectId = row.GetObjectID();
                  exists.Add(recValue, objectId);
                }
              }
            }

            FeatureClassDefinition definition = featureClass.GetDefinition();
            SpatialReference spatialReference = definition.GetSpatialReference();

            foreach (Recording recording in recordings)
            {
              Location location = recording?.Location;
              var point = location?.Point;

              if (location == null || point == null)
              {
                continue;
              }

              if (!exists.ContainsKey((string)recording.FieldToItem(idField)))
              {
                if (Filter(recording))
                {
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (SaveFeatureMembersAsync) Start writing recording to database: {recording.ImageId}");
                  Dictionary<string, object> toAddFields = Recording.Fields.ToDictionary(fieldId => fieldId.Key,
                    fieldId => recording.FieldToItem(fieldId.Key));

                  MapPoint newPoint = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, point.Z, spatialReference);
                  toAddFields.Add(Recording.ShapeFieldName, newPoint);
                  editOperation.Create(Layer, toAddFields);
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (SaveFeatureMembersAsync) Finished writing recording to database: {recording.ImageId}");
                }
              }
              else
              {
                if (Filter(recording))
                {
                  exists.Remove((string)recording.FieldToItem(idField));
                }
              }
            }

            foreach (var row in exists)
            {
              EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (SaveFeatureMembersAsync) delete element from database: {row.Value}, {row.Key}");
              editOperation.Delete(Layer, row.Value);
            }

            return editOperation;
          }
        }

        return editOperation;
      });
    }

    public static void ResetYears(FeatureLayer layer)
    {
      if (layer != null && YearMonth.ContainsKey(layer))
      {
        YearMonth[layer].Clear();
      }
    }

    protected CIMMarker GetPipSymbol(Color color)
    {
      var size025 = (int)_constants.SizeLayer;
      var size05 = size025 * 2;
      var size075 = size025 * 3;
      var size = size025 * 4;
      var size15 = size025 * 6;
      var size25 = size025 * 10;
      var size275 = size025 * 11;
      var size3 = size025 * 12;
      var bitmap = new Bitmap(size3, size3);
      const int sizeLine = 2;

      using (Graphics ga = Graphics.FromImage(bitmap))
      {
        var points = new PointF[6];
        points[0] = new PointF(size05, size025);
        points[1] = new PointF(size25, size025);
        points[2] = new PointF(size15, size15);
        points[3] = new PointF(size25, size275);
        points[4] = new PointF(size05, size275);
        points[5] = new PointF(size15, size15);
        var pathd = new GraphicsPath();
        pathd.AddPolygon(points);
        ga.Clear(Color.Transparent);
        ga.FillPath(Brushes.Yellow, pathd);
        ga.DrawPath(new Pen(Brushes.Gray, sizeLine), pathd);
        ga.DrawEllipse(new Pen(color, sizeLine), size, size, size, size);
        ga.FillEllipse(new SolidBrush(color), size, size, size, size);
        pathd.Dispose();
      }

      string tempPath = Path.GetTempPath();
      string writePath = Path.Combine(tempPath, $"{color.Name}pip.png");
      bitmap.Save(writePath, ImageFormat.Png);
      CIMMarker marker = SymbolFactory.Instance.ConstructMarkerFromFile(writePath);
      marker.Size = size075;
      return marker;
    }

    protected CIMMarker GetForbiddenSymbol(Color color)
    {
      var size025 = (int)_constants.SizeLayer;
      var size075 = size025 * 3;
      var size = size025 * 4;
      var size15 = size025 * 6;
      var size175 = size025 * 7;
      var size3 = size025 * 12;
      var bitmap = new Bitmap(size3, size3);
      const int sizeLine2 = 2;
      const int sizeLine3 = 3;
      const int sizeLine6 = 6;

      using (Graphics ga = Graphics.FromImage(bitmap))
      {
        ga.Clear(Color.Transparent);
        ga.DrawEllipse(new Pen(color, sizeLine2), size, size, size, size);
        ga.FillEllipse(new SolidBrush(color), size, size, size, size);
        ga.DrawEllipse(new Pen(Color.Red, sizeLine2), size15, size15, size075, size075);
        ga.FillEllipse(Brushes.Red, size15, size15, size075, size075);
        ga.DrawRectangle(new Pen(Color.WhiteSmoke, sizeLine2), size15 + sizeLine3, size175, size075 - sizeLine6, size025);
        ga.FillRectangle(Brushes.WhiteSmoke, size15 + sizeLine3, size175, size075 - sizeLine6, size025);
      }

      string tempPath = Path.GetTempPath();
      string writePath = Path.Combine(tempPath, $"{color.Name}forbidden.bmp");
      bitmap.Save(writePath, ImageFormat.Png);
      CIMMarker marker = SymbolFactory.Instance.ConstructMarkerFromFile(writePath);
      marker.Size = size075;
      return marker;
    }

    #endregion

    #region Event handlers

    private async void OnMapViewCameraChanged(MapViewCameraChangedEventArgs args)
    {
      if (InsideScale && IsVisible)
      {
        if (MapView != null && Layer != null && _cycloMediaGroupLayer != null && _addData == null)
        {
          _addData = new FeatureCollection();
          var project = ArcGISProject.Current;
          bool hasEdits = project.HasEdits;
          const double epsilon = 0.0;
          var extent = MapView.Extent;

          if (Math.Abs(extent.XMax - _lastextent.XMax) > epsilon ||
              Math.Abs(extent.YMin - _lastextent.YMin) > epsilon ||
              Math.Abs(extent.XMin - _lastextent.XMin) > epsilon ||
              Math.Abs(extent.YMax - _lastextent.YMax) > epsilon)
          {
            _lastextent = extent;
            Envelope thisEnvelope = await GetExtentAsync(extent);

            if (thisEnvelope != null)
            {
              _addData = FeatureCollection.Load(thisEnvelope, WfsRequest);

              if (_addData != null && _addData.NumberOfFeatures >= 1)
              {
                EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (OnMapViewCameraChanged) loaded features, start saving: {_addData.NumberOfFeatures}");
                EditOperation editOperation = await SaveFeatureMembersAsync(_addData, thisEnvelope);

                if (editOperation.IsEmpty)
                {
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (OnMapViewCameraChanged) there are no features to write to the layer");
                }
                else
                {
                  bool value = await editOperation.ExecuteAsync();
                  EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (OnMapViewCameraChanged) finished writing features, result: {value}");

                  if (value == false)
                  {
                    string errorMessage = editOperation.ErrorMessage;
                    EventLog.Write(EventLogLevel.Information, $"Street Smart: (CycloMediaLayer.cs) (OnMapViewCameraChanged) the error message is: {errorMessage}");
                  }
                }
              }

              await PostEntryStepAsync(thisEnvelope);

              if (!hasEdits)
              {
                await project.SaveEditsAsync();
              }

              await QueuedTask.Run(() => Layer?.ClearDisplayCache());
            }
          }

          _addData = null;
        }
      }
      else
      {
        _addData = null;
        ResetYears(Layer);
      }

      if (InsideScale)
      {
        FrameworkApplication.State.Activate("streetSmartArcGISPro_InsideScaleState");
      }
      else
      {
        FrameworkApplication.State.Deactivate("streetSmartArcGISPro_InsideScaleState");
      }
    }

    private async void OnLayersRemoved(LayerEventsArgs args)
    {
      bool contains = args.Layers.Any(layer => layer == Layer);
      if (contains)
      {
        await _cycloMediaGroupLayer.RemoveLayerAsync(Name, false);

        if (_cycloMediaGroupLayer.Count == 1)
        {
          await _cycloMediaGroupLayer[0].SetVisibleAsync(true);
          await _cycloMediaGroupLayer[0].RefreshAsync();
        }
      }
    }

    protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static string FixMapNameByReplacingSpecialCharacters(string mapName)
    {
      if (mapName == null)
      {
        return string.Empty;
      }

      char[] charsToReplace = [' ', '`', '~', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '=', '+', '[', '{', ']', '}', '\\', '|', ';', ':', '\'', '"', ',', '<', '.', '>', '/', '?'];
      foreach (char c in charsToReplace)
      {
        mapName = mapName.Replace(c, '_');
      }

      return mapName;
    }
    #endregion
  }
}
