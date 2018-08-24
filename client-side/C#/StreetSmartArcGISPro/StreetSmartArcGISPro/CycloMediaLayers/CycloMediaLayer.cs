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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using StreetSmartArcGISPro.Utilities;

using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using MySpatialReferenceList = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReferenceList;
using RecordingPoint = StreetSmartArcGISPro.Configuration.Remote.Recordings.Point;

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public abstract class CycloMediaLayer: INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static SortedDictionary<int, int> _yearMonth;

    private readonly CycloMediaGroupLayer _cycloMediaGroupLayer;
    private readonly ConstantsRecordingLayer _constants;

    private Envelope _lastextent;
    private FeatureCollection _addData;
    private bool _isVisibleInstreetSmart;
    private bool _visible;

    private readonly IList<Color> _colors = new List<Color>
    {
      Color.FromArgb(255, Color.FromArgb(0x80B3FF)),
      Color.FromArgb(255, Color.FromArgb(0x0067FF)),
      Color.FromArgb(255, Color.FromArgb(0x405980)),
      Color.FromArgb(255, Color.FromArgb(0x001F4D)),
      Color.FromArgb(255, Color.FromArgb(0xFFD080)),
      Color.FromArgb(255, Color.FromArgb(0xFFA100)),
      Color.FromArgb(255, Color.FromArgb(0x806840)),
      Color.FromArgb(255, Color.FromArgb(0x4D3000)),
      Color.FromArgb(255, Color.FromArgb(0xDDFF80)),
      Color.FromArgb(255, Color.FromArgb(0xBBFF00)),
      Color.FromArgb(255, Color.FromArgb(0x6F8040)),
      Color.FromArgb(255, Color.FromArgb(0x384D00)),
      Color.FromArgb(255, Color.FromArgb(0xFF80D9)),
      Color.FromArgb(255, Color.FromArgb(0xFF00B2)),
      Color.FromArgb(255, Color.FromArgb(0x80406C)),
      Color.FromArgb(255, Color.FromArgb(0x4D0035)),
      Color.FromArgb(255, Color.FromArgb(0xF2F2F2)),
      Color.FromArgb(255, Color.FromArgb(0xBFBFBF)),
      Color.FromArgb(255, Color.FromArgb(0x404040)),
      Color.FromArgb(255, Color.FromArgb(0x000000))
    };

    #endregion

    #region Properties

    public abstract string Name { get; }
    public abstract string FcName { get; }
    public abstract bool UseDateRange { get; }
    public abstract string WfsRequest { get; }
    public abstract double MinimumScale { get; set; }

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

    public bool IsVisibleInstreetSmart
    {
      get => _isVisibleInstreetSmart && IsVisible;
      set
      {
        _isVisibleInstreetSmart = value;
        NotifyPropertyChanged();
      }
    }

    public bool InsideScale
    {
      get
      {
        Camera camera = MapView.Active?.Camera;
        return camera != null && Layer != null && Math.Floor(camera.Scale) <= (MinimumScale = Layer.MinScale);
      }
    }

    protected static SortedDictionary<int, int> YearMonth => _yearMonth ?? (_yearMonth = new SortedDictionary<int, int>());

    #endregion

    #region Constructor

    protected CycloMediaLayer(CycloMediaGroupLayer layer, Envelope initialExtent = null)
    {
      _constants = ConstantsRecordingLayer.Instance;
      _cycloMediaGroupLayer = layer;
      _isVisibleInstreetSmart = true;
      Visible = false;
      IsRemoved = true;
      _lastextent = initialExtent ?? MapView.Active?.Extent;
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

    protected Color GetCol(int year)
    {
      DateTime now = DateTime.Now;
      int nowYear = now.Year;
      int yearDiff = nowYear - year;
      int nrColors = _colors.Count;
      int index = Math.Min(yearDiff, (nrColors - 1));
      return _colors[index];
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
          result = (Envelope) envelope.Clone();
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
        await RemoveLayerAsync(_cycloMediaGroupLayer.GroupLayer, oldLayer);
        await RefreshAsync();
      }
    }

    private async Task CreateFeatureLayerAsync()
    {
      Map map = MapView.Active?.Map;
      SpatialReference spatialReference = null;
      Settings config = Settings.Instance;
      MySpatialReference spatialReferenceRecording = config.RecordingLayerCoordinateSystem;

      if (spatialReferenceRecording == null)
      {
        if (map != null)
        {
          spatialReference = map.SpatialReference;
        }
      }
      else
      {
        spatialReference = spatialReferenceRecording.ArcGisSpatialReference ??
                           await spatialReferenceRecording.CreateArcGisSpatialReferenceAsync();
      }

      int wkid = spatialReference?.Wkid ?? 0;
      string fcNameWkid = string.Concat(FcName, wkid);
      Project project = Project.Current;
      await CreateFeatureClassAsync(project, fcNameWkid, spatialReference);
      Layer = await CreateLayerAsync(project, fcNameWkid, _cycloMediaGroupLayer.GroupLayer);
      await MakeEmptyAsync();
      await CreateUniqueValueRendererAsync();
      await project.SaveEditsAsync();
    }

    public async Task AddToLayersAsync(MapView mapView = null)
    {
      Layer = null;
      Map map = (mapView ?? MapView.Active)?.Map;

      if (map != null)
      {
        var layersByName = map.FindLayers(Name);
        bool leave = false;

        foreach (Layer layer in layersByName)
        {
          if (layer is FeatureLayer)
          {
            if (!leave)
            {
              Layer = layer as FeatureLayer;
              leave = true;
            }
          }
          else
          {
            await RemoveLayerAsync(map, layer);
          }
        }
      }

      if (Layer == null)
      {
        await CreateFeatureLayerAsync();
      }
      else
      {
        await MakeEmptyAsync();
        await UpdateSpatialReferenceSettings();
        await CreateUniqueValueRendererAsync();
        Project project = Project.Current;
        await project.SaveEditsAsync();
      }

      IsRemoved = false;
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
      TOCSelectionChangedEvent.Subscribe(OnTocSelectionChanged);
      LayersRemovedEvent.Subscribe(OnLayersRemoved);
      await RefreshAsync();
      IsInitialized = true;
    }

    private async Task UpdateSpatialReferenceSettings()
    {
      SpatialReference spatialReference = await GetSpatialReferenceAsync();
      MySpatialReferenceList mySpatialReferenceList = MySpatialReferenceList.Instance;
      MySpatialReference mySpatialReference = mySpatialReferenceList.GetItem($"EPSG:{spatialReference.Wkid}");
      Settings settings = Settings.Instance;
      settings.RecordingLayerCoordinateSystem = mySpatialReference;
      settings.Save();
    }

    private async Task<FeatureLayer> CreateLayerAsync(Project project, string fcName, ILayerContainerEdit layerContainer)
    {
      return await QueuedTask.Run(() =>
      {
        string featureClassUrl = $@"{project.DefaultGeodatabasePath}\{fcName}";
        Uri uri = new Uri(featureClassUrl);
        FeatureLayer result = LayerFactory.Instance.CreateFeatureLayer(uri, layerContainer);
        result.SetName(Name);
        result.SetMinScale(MinimumScale);
        result.SetVisibility(true);
        result.SetEditable(true);
        return result;
      });
    }

    private async Task RemoveLayerAsync(ILayerContainerEdit layerContainer, Layer layer)
    {
      await QueuedTask.Run(() =>
      {
        if (layerContainer?.Layers.Contains(layer) ?? false)
        {
          layerContainer.RemoveLayer(layer);
        }
      });
    }

    public async Task DisposeAsync(bool fromGroup)
    {
      if (fromGroup)
      {
        await RemoveLayerAsync(_cycloMediaGroupLayer.GroupLayer, Layer);
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
        Envelope extent = MapView.Active?.Extent;

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
        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          if (featureClass != null)
          {
            const double searchBox = 25.0;
            double xMin = x - searchBox;
            double xMax = x + searchBox;
            double yMin = y - searchBox;
            double yMax = y + searchBox;
            Envelope envelope = EnvelopeBuilder.CreateEnvelope(xMin, xMax, yMin, yMax);

            SpatialQueryFilter spatialFilter = new SpatialQueryFilter
            {
              FilterGeometry = envelope,
              SpatialRelationship = SpatialRelationship.Contains,
              SubFields = $"{Recording.FieldGroundLevelOffset}, SHAPE"
            };

            using (RowCursor existsResult = featureClass.Search(spatialFilter, false))
            {
              int groundLevelId = existsResult.FindField(Recording.FieldGroundLevelOffset);

              while (existsResult.MoveNext())
              {
                using (Row row = existsResult.Current)
                {
                  double? groundLevel = row?.GetOriginalValue(groundLevelId) as double?;

                  if (groundLevel != null)
                  {
                    Feature feature = row as Feature;
                    Geometry geometry = feature?.GetShape();
                    MapPoint point = geometry as MapPoint;
                    double height = point?.Z ?? 0;

                    // ReSharper disable once AccessToModifiedClosure
                    result = result ?? 0.0;
                    result = result + height - ((double) groundLevel);
                    count++;
                  }
                }
              }
            }
          }
        }
      });

      result = result != null ? result/Math.Max(count, 1) : null;
      return result;
    }

    protected virtual void Remove()
    {
      Layer = null;
      IsRemoved = true;
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      TOCSelectionChangedEvent.Unsubscribe(OnTocSelectionChanged);
      LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
    }

    private async Task CreateUniqueValueRendererAsync()
    {
      ClearYears();

      await QueuedTask.Run(() =>
      {
        string[] fieldNames = {Recording.FieldYear, Recording.FieldPip, Recording.FieldIsAuthorized};
        var uniqueValueRendererDefinition = new UniqueValueRendererDefinition(fieldNames);
        var uniqueValueRenderer = (CIMUniqueValueRenderer) Layer.CreateRenderer(uniqueValueRendererDefinition);
        uniqueValueRenderer.DefaultLabel = string.Empty;
        uniqueValueRenderer.DefaultSymbol = null;
        uniqueValueRenderer.Groups = null;
        Layer.SetRenderer(uniqueValueRenderer);
      });
    }

    private async Task CreateFeatureClassAsync(Project project, string fcName, SpatialReference spatialReference)
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
          catch (GeodatabaseException)
          {
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
          Map map = MapView.Active?.Map;

          if (map != null)
          {
            Layer thisLayer =
              map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(
                checkLayer => checkLayer.Name == fcName);
            map.RemoveLayer(thisLayer);
          }
        });
      }
    }

    private async Task<IGPResult> CreateFeatureClass(Project project, string fcName, string template, SpatialReference spatialReference)
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

    public async Task<bool> SaveFeatureMembersAsync(FeatureCollection featureCollection, Envelope envelope)
    {
      return await QueuedTask.Run(() =>
      {
        var editOperation = new EditOperation
        {
          Name = Name,
          SelectNewFeatures = false,
          ShowModalMessageAfterFailure = false
        };

        if ((featureCollection != null) && (featureCollection.NumberOfFeatures >= 1))
        {
          FeatureMembers featureMembers = featureCollection.FeatureMembers;
          Recording[] recordings = featureMembers?.Recordings;

          if ((Layer != null) && (recordings != null))
          {
            string idField = Recording.ObjectId;
            var exists = new Dictionary<string, long>();

            using (FeatureClass featureClass = Layer?.GetFeatureClass())
            {
              if (featureClass != null)
              {
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
                    using (Row row = existsResult.Current)
                    {
                      string recValue = row?.GetOriginalValue(imId) as string;

                      if (!string.IsNullOrEmpty(recValue) && !exists.ContainsKey(recValue))
                      {
                        long objectId = row.GetObjectID();
                        exists.Add(recValue, objectId);
                      }
                    }
                  }
                }

                FeatureClassDefinition definition = featureClass.GetDefinition();
                SpatialReference spatialReference = definition.GetSpatialReference();

                foreach (Recording recording in recordings)
                {
                  Location location = recording?.Location;
                  RecordingPoint point = location?.Point;

                  if (location != null && point != null)
                  {
                    if (!exists.ContainsKey((string)recording.FieldToItem(idField)))
                    {
                      if (Filter(recording))
                      {
                        Dictionary<string, object> toAddFields = Recording.Fields.ToDictionary(fieldId => fieldId.Key,
                          fieldId => recording.FieldToItem(fieldId.Key));

                        MapPoint newPoint = MapPointBuilder.CreateMapPoint(point.X, point.Y, point.Z, spatialReference);
                        toAddFields.Add(Recording.ShapeFieldName, newPoint);
                        editOperation.Create(Layer, toAddFields);
                      }
                    }
                    else
                    {
                      if (Filter(recording))
                      {
                        exists.Remove((string) recording.FieldToItem(idField));
                      }
                    }
                  }
                }

                foreach (var row in exists)
                {
                  editOperation.Delete(Layer, row.Value);
                }
              }
            }
          }
        }

        return editOperation.IsEmpty ? Task.FromResult(true) : editOperation.ExecuteAsync();
      });
    }

    public static void ResetYears()
    {
      YearMonth.Clear();
    }

    protected CIMMarker GetPipSymbol(Color color)
    {
      var size025 = (int) _constants.SizeLayer;
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
      var size025 = (int) _constants.SizeLayer;
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
        MapView mapView = MapView.Active;

        if (mapView != null && Layer != null && _cycloMediaGroupLayer != null && _addData == null)
        {
          const double epsilon = 0.0;
          var extent = mapView.Extent;

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

              if ((_addData != null) && (_addData.NumberOfFeatures >= 1))
              {
                await SaveFeatureMembersAsync(_addData, thisEnvelope);
              }

              _addData = null;
              Project project = Project.Current;
              await PostEntryStepAsync(thisEnvelope);
              await project.SaveEditsAsync();
              await QueuedTask.Run(() => Layer.ClearDisplayCache());
            }
          }
        }
      }
      else
      {
        _addData = null;
        YearMonth.Clear();
      }

      if (InsideScale)
      {
        FrameworkApplication.State.Activate("StreetSmartArcGISPro_InsideScaleState");
      }
      else
      {
        FrameworkApplication.State.Deactivate("StreetSmartArcGISPro_InsideScaleState");
      }
    }

    private void OnTocSelectionChanged(MapViewEventArgs mapViewEventArgs)
    {
      // todo: get color from layer
      // todo: Layer changed event
    }

    private async void OnLayersRemoved(LayerEventsArgs args)
    {
      IEnumerable<Layer> layers = args.Layers;
      bool contains = layers.Aggregate(false, (current, layer) => (layer == Layer) || current);

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

    #endregion
  }
}
