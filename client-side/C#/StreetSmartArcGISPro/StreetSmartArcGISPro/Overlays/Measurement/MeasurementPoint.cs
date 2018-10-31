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
  public class MeasurementPoint : ObservableCollection<MeasurementObservation>, IDisposable, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly ConstantsViewer _constants;
    private readonly CultureInfo _ci;

    private MapPoint _point;
    private int _pointId;
    private bool _added;
    private bool _open;
    private IDisposable _disposeText;
    private readonly IDictionary<string, MeasurementObservation> _observations;
    private object _apiMeasurementPoint;
    private bool _updatePoint;
    private bool _isDisposed;

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
      _observations = new Dictionary<string, MeasurementObservation>();
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
    }

    #endregion

    #region Functions

    public async Task UpdateObservationAsync(object observation, Bitmap match)
    {
      // Todo: get observation data
      string imageId = string.Empty; // observation.imageId;
      double x = 0; // observation.x;
      double y = 0; // observation.y;
      double z = 0; // observation.z;
      double xDir = 0.0; // observation.Dir_x;
      double yDir = 0.0; // observation.Dir_y;
      MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);

      if (_observations.ContainsKey(imageId))
      {
        _observations[imageId].Point = point;
        _observations[imageId].ImageId = imageId;
        _observations[imageId].Match = match;
        _observations[imageId].XDir = xDir;
        _observations[imageId].YDir = yDir;
        await _observations[imageId].RedrawObservationAsync();
      }
      else
      {
        MeasurementObservation measurementObservation = new MeasurementObservation(this, imageId, point, match, xDir, yDir);
        _observations.Add(imageId, measurementObservation);
        Add(measurementObservation);
        await measurementObservation.RedrawObservationAsync();
      }

      SetDetailPane();
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
      if (_observations.ContainsKey(imageId))
      {
        MeasurementObservation observation = _observations[imageId];
        observation.Dispose();
        Remove(observation);
        _observations.Remove(imageId);
      }
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

    public void Dispose()
    {
      _isDisposed = true;
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      Measurement.SetDetailPanePoint(null, this);
      _disposeText?.Dispose();

      foreach (MeasurementObservation observation in this)
      {
        observation.Dispose();
      }
    }

    public async Task UpdatePointAsync(IFeature measurementPoint, int index)
    {
      if (!_updatePoint)
      {
        _updatePoint = true;
        PointId = index;

        ApiPoint = null;
        double x = 0;
        double y = 0;
        double z = 0;

        IGeometry geom = measurementPoint.Geometry;

        switch (geom.Type)
        {
          case StreetSmartGeometryType.Point:
            IPoint point = (IPoint) geom;
            x = point.X ?? 0;
            y = point.Y ?? 0;
            z = point.Z ?? 0;
            break;
          case StreetSmartGeometryType.LineString:
            ILineString line = (ILineString) geom;
            x = line[index].X ?? 0;
            y = line[index].Y ?? 0;
            z = line[index].Z ?? 0;
            break;
          case StreetSmartGeometryType.Polygon:
            IPolygon polygon = (IPolygon) geom;
            x = polygon[0][index].X ?? 0;
            y = polygon[0][index].Y ?? 0;
            z = polygon[0][index].Z ?? 0;
            break;
        }

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

                    if (geometry is Polygon)
                    {
                      ptColl.Add(point);
                    }

                    break;
                  case 1:
                    ptColl.Add(point);
                    break;
                  default:
                    if (_pointId <= nrPoints)
                    {
                      if (_pointId != nrPoints2)
                      {
                        ptColl.Insert(_pointId, point);
                      }
                      else
                      {
                        ptColl.Add(point);
                      }
                    }

                    break;
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

        _updatePoint = false;
      }

      await RedrawPointAsync();
    }

    public bool IsSame(MapPoint point)
    {
      return IsSame(point, (Point != null) && (!Point.IsEmpty) && (!double.IsNaN(Point.Z)));
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
