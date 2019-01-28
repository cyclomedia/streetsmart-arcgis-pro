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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmart.Common.Interfaces.GeoJson;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Utilities;

using ArcGISGeometryType = ArcGIS.Core.Geometry.GeometryType;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;

using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

using WindowsPoint = System.Windows.Point;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  public class MeasurementPoint : ObservableCollection<MeasurementObservation>, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly ConstantsViewer _constants;
    private readonly CultureInfo _ci;

    public IFeature Feature { get; set; }

    private MapPoint _point;
    private int _pointId;
    private bool _added;
    private bool _open;
    private IDisposable _disposeText;
    private object _apiMeasurementPoint;
    private bool _updatePoint;
    private bool _isDisposed;

    public IDictionary<string, MeasurementObservation> Observations { get; }

    #endregion

    #region Properties

    public Measurement Measurement { get; }

    public int PointId
    {
      get => _pointId;
      set
      {
        _pointId = value;
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("PreviousPoint");
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("NextPoint");
        OnPropertyChanged("Index");
      }
    }

    public bool Open
    {
      get => _open;
      private set
      {
        _open = value;
        OnPropertyChanged();
      }
    }

    public object ApiPoint
    {
      get
      {
        // Todo: _apiMeasurementPoint x, y, z
        if (_apiMeasurementPoint != null && double.IsNaN(0.0) &&
            double.IsNaN(0.0) && double.IsNaN(0.0))
        {
          _apiMeasurementPoint = Measurement.GetApiPoint(PointId);
        }

        return _apiMeasurementPoint;
      }
      set
      {
        _apiMeasurementPoint = value;
        OnPropertyChanged();
      }
    }

    public MapPoint Point
    {
      get => _point;
      private set
      {
        _point = value;
        OnPropertyChanged();
      }
    }

    public MapPoint LastPoint { get; private set; }

    public bool NotCreated => Point == null;

    public bool IsFirstNumber => _pointId == 0;

    public bool IsLastNumber => Measurement == null || _pointId == Measurement.Count - 1;

    public MeasurementPoint PreviousPoint
      => Measurement != null && !IsFirstNumber ? Measurement.GetPointByNr(_pointId - 1) : null;

    public MeasurementPoint NextPoint
      => Measurement != null && !IsLastNumber ? Measurement.GetPointByNr(_pointId + 1) : null;

    #endregion

    #region Constructor

    public MeasurementPoint(int pointId, Measurement measurement)
    {
      _isDisposed = false;
      _updatePoint = false;
      Measurement = measurement;
      Point = null;
      LastPoint = null;
      PointId = pointId;
      _added = false;
      Open = false;
      _constants = ConstantsViewer.Instance;
      _ci = CultureInfo.InvariantCulture;
      Observations = new Dictionary<string, MeasurementObservation>();
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
    }

    #endregion

    #region Functions

    public async Task UpdateObservationAsync(IResultDirection direction, int i)
    {
      string imageId = direction.Id;
      double x = direction.Position?.X ?? 0.0;
      double y = direction.Position?.Y ?? 0.0;
      double z = direction.Position?.Z ?? 0.0;
      double xDir = direction.Direction?.X ?? 0.0;
      double yDir = direction.Direction?.Y ?? 0.0;
      MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);

      if (Observations.ContainsKey(imageId))
      {
        Observations[imageId].Point = point;
        Observations[imageId].ImageId = imageId;
        Observations[imageId].XDir = xDir;
        Observations[imageId].YDir = yDir;
        Observations[imageId].LineNumber = i;
        await Observations[imageId].RedrawObservationAsync();
      }
      else
      {
        MeasurementObservation measurementObservation = new MeasurementObservation(this, imageId, point, xDir, yDir, i);
        Observations.Add(imageId, measurementObservation);
        Add(measurementObservation);
        await measurementObservation.RedrawObservationAsync();
      }

     // SetDetailPane();
    }

    public bool IsObservationVisible()
    {
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      MeasurementList measurementList = streetSmart.MeasurementList;
      return streetSmart.InsideScale() && !_isDisposed &&
             ((Measurement?.IsOpen ?? false) ||
              (Measurement?.IsPointMeasurement ?? false) && measurementList.Sketch == null) &&
             (bool)Measurement?.DrawPoint;
    }

    public void RemoveObservation(string imageId)
    {
      /*
      if (_observations.ContainsKey(imageId))
      {
        MeasurementObservation observation = _observations[imageId];
        observation.Dispose();
        // Remove(observation);
        _observations.Remove(imageId);
      }
      */
    }

    public void RemoveObservation(MeasurementObservation observation)
    {
      Measurement.RemoveObservation(PointId, observation?.ImageId);
    }

    private void SetDetailPane()
    {
      Measurement.SetDetailPanePoint(this);
    }

    public void Closed()
    {
      Open = false;
      LastPoint = null;
    }

    public void Opened()
    {
      Open = true;
      SetDetailPane();
    }

    public void OpenPoint()
    {
      Open = true;
      Measurement?.OpenPoint(PointId);
      SetDetailPane();
    }

    public void ClosePoint()
    {
      if (Open)
      {
        Open = false;
        Measurement?.ClosePoint(PointId);
      }
    }

    public async Task Dispose()
    {
      _isDisposed = true;
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      Measurement.SetDetailPanePoint(null, this);
      _disposeText?.Dispose();

      MapView thisView = MapView.Active;
      Geometry geometry = await thisView.GetCurrentSketchAsync();
      var ptColl = await Measurement.ToPointCollectionAsync(geometry);

      if (ptColl.Count > _pointId)
      {
        ptColl.RemoveAt(_pointId);
      }

      await QueuedTask.Run(() =>
      {
        if (Measurement.IsGeometryType(ArcGISGeometryType.Polygon))
        {
          geometry = PolygonBuilder.CreatePolygon(ptColl, geometry.SpatialReference);
        }
        else if (Measurement.IsGeometryType(ArcGISGeometryType.Polyline))
        {
          geometry = PolylineBuilder.CreatePolyline(ptColl, geometry.SpatialReference);
        }

        thisView.SetCurrentSketchAsync(geometry);
      });

      foreach (MeasurementObservation observation in this)
      {
        observation.Dispose();
      }
    }

    public async Task<bool> UpdatePointAsync(IFeature measurementPoint, int index)
    {
      bool result = false;

      if (!_updatePoint)
      {
        _updatePoint = true;
        PointId = index;

        ApiPoint = null;
        double x = 0;
        double y = 0;
        double z = 0;

        Feature = measurementPoint;
        IGeometry geom = Feature.Geometry;

        switch (geom.Type)
        {
          case StreetSmartGeometryType.Point:
            IPoint point = (IPoint) geom;
            result = point.X == null || point.Y == null;
            x = point.X ?? 0;
            y = point.Y ?? 0;
            z = point.Z ?? 0;
            break;
          case StreetSmartGeometryType.LineString:
            ILineString line = (ILineString) geom;
            result = line[index].X == null || line[index].Y == null;
            x = line[index].X ?? 0;
            y = line[index].Y ?? 0;
            z = line[index].Z ?? 0;
            break;
          case StreetSmartGeometryType.Polygon:
            IPolygon polygon = (IPolygon) geom;
            result = polygon[0][index].X == null || polygon[0][index].Y == null;
            x = polygon[0][index].X ?? 0;
            y = polygon[0][index].Y ?? 0;
            z = polygon[0][index].Z ?? 0;
            break;
        }

        IMeasurementProperties properties = measurementPoint.Properties as IMeasurementProperties;
        IMeasureDetails details = properties?.MeasureDetails?.Count > index ? properties.MeasureDetails[index] : null;
        List<string> imageIds = new List<string>();

        if (details?.Details is IDetailsForwardIntersection forwardIntersection)
        {
          for (int i = 0; i < forwardIntersection.ResultDirections.Count; i++)
          {
            imageIds.Add(forwardIntersection.ResultDirections[i].Id);
            await UpdateObservationAsync(forwardIntersection.ResultDirections[i], i);
          }
        }

        foreach (string observation in Observations.Keys)
        {
          if (!imageIds.Contains(observation))
          {
            RemoveObservation(observation);
          }
        }

        if (!result)
        {
          Point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);
          LastPoint = LastPoint ?? Point;

          MapView thisView = MapView.Active;
          Geometry geometry = await thisView.GetCurrentSketchAsync();

          if (geometry != null)
          {
            var ptColl = await Measurement.ToPointCollectionAsync(geometry);
            int nrPoints = Measurement.PointNr;

            if (ptColl != null && Measurement.IsSketch)
            {
              if (_pointId < nrPoints)
              {
                MapPoint pointC = ptColl[_pointId];

                if (!IsSame(pointC))
                {
                  await QueuedTask.Run(() =>
                  {
                    MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, Point.M,
                      geometry.SpatialReference);

                    if (Measurement.IsPointMeasurement)
                    {
                      thisView.SetCurrentSketchAsync(point);
                    }
                    else
                    {
                      ptColl[_pointId] = point;

                      if (_pointId == 0 && nrPoints + 1 == ptColl.Count)
                      {
                        ptColl[ptColl.Count - 1] = point;
                      }

                      if (Measurement.IsGeometryType(ArcGISGeometryType.Polygon))
                      {
                        geometry = PolygonBuilder.CreatePolygon(ptColl, geometry.SpatialReference);
                      }
                      else if (Measurement.IsGeometryType(ArcGISGeometryType.Polyline))
                      {
                        if (ptColl.Count == 1)
                        {
                          ptColl.Add(ptColl[0]);
                        }

                        geometry = PolylineBuilder.CreatePolyline(ptColl, geometry.SpatialReference);
                      }

                      thisView.SetCurrentSketchAsync(geometry);
                    }
                  });
                }
              }
              else
              {
                await QueuedTask.Run(() =>
                {
                  MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, Point.M,
                    geometry.SpatialReference);
                  int nrPoints2 = ptColl.Count;

                  switch (nrPoints2)
                  {
                    case 0:
                      ptColl.Add(point);

                      //if (geometry is Polygon)
                      //{
                      //  ptColl.Add(point);
                     // }

                      break;
                    case 1:
                      ptColl.Add(point);
                      break;
                    default:
                      if (_pointId <= nrPoints)
                      {
                        if (_pointId != nrPoints2 && geometry is Polyline || _pointId != nrPoints2 - 1 && geometry is Polygon)
                        {
                          ptColl.Insert(_pointId, point);
                        }
                        else
                        {
                          if (geometry is Polygon)
                          {
                            ptColl.Insert(_pointId, point);
                          }
                          else
                          {
                            ptColl.Add(point);
                          }
                        }
                      }

                      break;
                  }

                  if (Measurement.IsGeometryType(ArcGISGeometryType.Polygon))
                  {
                    if (ptColl.Count == 1)
                    {
                      ptColl.Add(ptColl[0]);
                    }
                    else if (ptColl.Count >= 3 && IsSame(ptColl[0]) && IsSame(ptColl[ptColl.Count - 1]))
                    {
                      int removeCount = 0;
                      int nrPoints3 = ptColl.Count;

                      for (int i = 1; i < nrPoints3 - 1; i++)
                      {
                        if (IsSame(ptColl[i - removeCount]))
                        {
                          ptColl.RemoveAt(i - removeCount);
                          removeCount++;
                        }
                      }
                    }

                    if (Measurement.Count == ptColl.Count)
                    {
                      geometry = PolygonBuilder.CreatePolygon(ptColl, geometry.SpatialReference);
                    }
                    else
                    {
                      
                    }
                  }
                  else if (Measurement.IsGeometryType(ArcGISGeometryType.Polyline))
                  {
                    if (ptColl.Count == 1)
                    {
                      ptColl.Add(ptColl[0]);
                    }

                    geometry = PolylineBuilder.CreatePolyline(ptColl, geometry.SpatialReference);
                  }
                  else if (Measurement.IsPointMeasurement && ptColl.Count == 1)
                  {
                    geometry = ptColl[0];
                  }

                  thisView.SetCurrentSketchAsync(geometry);
                });
              }
            }
            else
            {
              if (geometry is MapPoint)
              {
                await QueuedTask.Run(() =>
                {
                  if (geometry.IsEmpty)
                  {
                    if ((!double.IsNaN(Point.X)) && (!double.IsNaN(Point.Y)))
                    {
                      if (!_added)
                      {
                        _added = true;
                        MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, PointId + 1);
                        thisView.SetCurrentSketchAsync(point);
                        _added = false;
                      }
                    }
                  }
                  else
                  {
                    var pointC = geometry as MapPoint;

                    if (!IsSame(pointC))
                    {
                      if ((!double.IsNaN(Point.X)) && (!double.IsNaN(Point.Y)))
                      {
                        MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, PointId + 1);
                        thisView.SetCurrentSketchAsync(point);
                      }
                    }
                  }
                });
              }
            }
          }
        }

        _updatePoint = false;
      }

      if (!result)
      {
        await RedrawPointAsync();
      }

      return result;
    }

    public bool IsSame(MapPoint point)
    {
      return IsSame(point, Point != null && !Point.IsEmpty && !double.IsNaN(Point.Z));
    }

    public bool IsSame(MapPoint point, bool includeZ)
    {
      const double distance = 0.01;
      return InsideDistance(point, distance, includeZ);
    }

    private bool InsideDistance(MapPoint point, double dinstance, bool includeZ)
    {
      return Point != null && point != null && !Point.IsEmpty && !point.IsEmpty &&
             Math.Abs(Point.X - point.X) < dinstance && Math.Abs(Point.Y - point.Y) < dinstance &&
             (!includeZ || Math.Abs(Point.Z - point.Z) < dinstance);
    }

    public void OpenNearestImage()
    {
      Measurement?.OpenNearestImage(ApiPoint);
    }

    public void LookAtMeasurement()
    {
      Measurement?.LookAtMeasurement(ApiPoint);
    }

    public async Task RedrawPointAsync()
    {
      await QueuedTask.Run(() =>
      {
        ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
        MeasurementList measurementList = streetSmart.MeasurementList;
        _disposeText?.Dispose();

        if (streetSmart.InsideScale() && !_isDisposed && Point != null &&
            ((Measurement?.IsOpen ?? false) ||
             (Measurement?.IsPointMeasurement ?? false) && measurementList.Sketch == null) &&
            (Measurement?.DrawPoint ?? false) && !double.IsNaN(Point.X) && !double.IsNaN(Point.Y))
        {
          MapView thisView = MapView.Active;
          WindowsPoint winPoint = thisView.MapToScreen(Point);
          float fontSize = _constants.MeasurementFontSize;
          int fontSizeT = (int) (fontSize*2);
          int fontSizeR = (int) ((fontSize*3)/2);
          int fontSizeK = (int) (fontSize/4);
          string text = (Measurement?.IsOpen ?? true) ? (PointId + 1).ToString(_ci) : Measurement.MeasurementName;
          int characters = text.Length;
          Bitmap bitmap = new Bitmap((fontSizeT*characters), fontSizeT);

          double pointSize = _constants.MeasurementPointSize;
          double pointSizePoint = pointSize*6/4;
          WindowsPoint winPointText = new WindowsPoint {X = winPoint.X + pointSizePoint, Y = winPoint.Y - pointSizePoint};
          MapPoint pointText = thisView.ScreenToMap(winPointText);

          using (var sf = new StringFormat())
          {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
              g.Clear(Color.Transparent);
              Font font = new Font("Arial", fontSize);
              sf.Alignment = StringAlignment.Center;
              Rectangle rectangle = new Rectangle(fontSizeK, fontSizeK, (fontSizeR*characters), fontSizeR);
              g.DrawString(text, font, Brushes.Black, rectangle, sf);
            }
          }

          BitmapSource source = bitmap.ToBitmapSource();
          var frame = BitmapFrame.Create(source);
          var encoder = new PngBitmapEncoder();
          encoder.Frames.Add(frame);

          using (MemoryStream stream = new MemoryStream())
          {
            encoder.Save(stream);
            byte[] imageBytes = stream.ToArray();
            string base64String = Convert.ToBase64String(imageBytes);
            string url = $"data:image/bmp;base64,{base64String}";

            CIMPictureMarker marker = new CIMPictureMarker
            {
              URL = url,
              Enable = true,
              ScaleX = 1,
              Size = fontSizeR
            };

            CIMPointSymbol symbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
            CIMSymbolReference symbolReference = symbol.MakeSymbolReference();
            _disposeText = thisView.AddOverlay(pointText, symbolReference);
          }
        }
      });
    }

    public void RemoveMe()
    {
      Measurement.RemoveMeasurementPoint(PointId);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Event handlers

    private async void OnMapViewCameraChanged(MapViewCameraChangedEventArgs args)
    {
      await RedrawPointAsync();
    }

    #endregion
  }
}
