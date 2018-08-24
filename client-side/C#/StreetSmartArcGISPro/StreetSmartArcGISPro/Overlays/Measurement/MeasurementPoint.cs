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
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Utilities;

using ApiObservation = GlobeSpotterAPI.MeasurementObservation;
using ApiMeasurementPoint = GlobeSpotterAPI.MeasurementPoint;
using streetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using Point = System.Windows.Point;

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
    private int _intId;
    private int _index;
    private bool _added;
    private bool _open;
    private IDisposable _disposeText;
    private readonly IDictionary<string, MeasurementObservation> _observations;
    private ApiMeasurementPoint _apiMeasurementPoint;
    private bool _updatePoint;
    private bool _isDisposed;

    #endregion

    #region Properties

    public Measurement Measurement { get; }

    public int IntId
    {
      set
      {
        _intId = value;
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("PreviousPoint");
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("NextPoint");
      }
    }

    public int PointId { get; }

    public bool Open
    {
      get => _open;
      private set
      {
        _open = value;
        OnPropertyChanged();
      }
    }

    public ApiMeasurementPoint ApiPoint
    {
      get
      {
        if (_apiMeasurementPoint != null && double.IsNaN(_apiMeasurementPoint.x) &&
            double.IsNaN(_apiMeasurementPoint.y) && double.IsNaN(_apiMeasurementPoint.z))
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

    public int Index
    {
      get => _index;
      set
      {
        _index = value;
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

    public bool IsFirstNumber => _intId == 1;

    public bool IsLastNumber => Measurement == null || _intId == Measurement.Count;

    public MeasurementPoint PreviousPoint
      => Measurement != null && !IsFirstNumber ? Measurement.GetPointByNr(_intId - 2) : null;

    public MeasurementPoint NextPoint
      => Measurement != null && !IsLastNumber ? Measurement.GetPointByNr(_intId) : null;

    #endregion

    #region Constructor

    public MeasurementPoint(int pointId, int intId, Measurement measurement)
    {
      _isDisposed = false;
      _updatePoint = false;
      Measurement = measurement;
      Index = 0;
      IntId = intId;
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

    public async Task UpdateObservationAsync(ApiObservation observation, Bitmap match)
    {
      string imageId = observation.imageId;
      double x = observation.x;
      double y = observation.y;
      double z = observation.z;
      double xDir = observation.Dir_x;
      double yDir = observation.Dir_y;
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
      streetSmart streetSmart = streetSmart.Current;
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
      if (Index == 0)
      {
        Index = Measurement.GetMeasurementPointIndex(PointId);
      }

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

    public async Task UpdatePointAsync(ApiMeasurementPoint measurementPoint, int index)
    {
      if (!_updatePoint)
      {
        _updatePoint = true;
        Index = index;

        ApiPoint = measurementPoint;
        double x = measurementPoint.x;
        double y = measurementPoint.y;
        double z = measurementPoint.z;
        Point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z);
        LastPoint = LastPoint ?? Point;

        MapView thisView = MapView.Active;
        Geometry geometry = await thisView.GetCurrentSketchAsync();

        if (geometry != null)
        {
          var ptColl = await Measurement.ToPointCollectionAsync(geometry);
          int nrPoints = Measurement.PointNr;

          if ((ptColl != null) && Measurement.IsSketch)
          {
            if (_intId <= nrPoints)
            {
              MapPoint pointC = ptColl[_intId - 1];

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
                    ptColl[_intId - 1] = point;

                    if ((_intId == 1) && ((nrPoints + 1) == ptColl.Count))
                    {
                      ptColl[ptColl.Count - 1] = point;
                    }

                    if (Measurement.IsGeometryType(GeometryType.Polygon))
                    {
                      geometry = PolygonBuilder.CreatePolygon(ptColl, geometry.SpatialReference);
                    }
                    else if (Measurement.IsGeometryType(GeometryType.Polyline))
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
                    if (_intId <= (nrPoints + 1))
                    {
                      if ((_intId - 1) != nrPoints2)
                      {
                        ptColl.Insert((_intId - 1), point);
                      }
                      else
                      {
                        ptColl.Add(point);
                      }
                    }

                    break;
                }

                if (Measurement.IsGeometryType(GeometryType.Polygon))
                {
                  geometry = PolygonBuilder.CreatePolygon(ptColl, geometry.SpatialReference);
                }
                else if (Measurement.IsGeometryType(GeometryType.Polyline))
                {
                  if (ptColl.Count == 1)
                  {
                    ptColl.Add(ptColl[0]);
                  }

                  geometry = PolylineBuilder.CreatePolyline(ptColl, geometry.SpatialReference);
                }
                else if ((Measurement.IsPointMeasurement) && (ptColl.Count == 1))
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
                      MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, Index);
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
                      MapPoint point = MapPointBuilder.CreateMapPoint(Point.X, Point.Y, Point.Z, Index);
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
        streetSmart streetSmart = streetSmart.Current;
        MeasurementList measurementList = streetSmart.MeasurementList;
        _disposeText?.Dispose();

        if (streetSmart.InsideScale() && !_isDisposed && Point != null &&
            ((Measurement?.IsOpen ?? false) ||
             (Measurement?.IsPointMeasurement ?? false) && measurementList.Sketch == null) &&
            (Measurement?.DrawPoint ?? false) && !double.IsNaN(Point.X) && !double.IsNaN(Point.Y))
        {
          MapView thisView = MapView.Active;
          Point winPoint = thisView.MapToScreen(Point);
          float fontSize = _constants.MeasurementFontSize;
          int fontSizeT = (int) (fontSize*2);
          int fontSizeR = (int) ((fontSize*3)/2);
          int fontSizeK = (int) (fontSize/4);
          int txt = (Measurement?.IsOpen ?? true) ? Index : measurementList.GetMeasurementNumber(Measurement);
          string text = txt.ToString(_ci);
          int characters = text.Length;
          Bitmap bitmap = new Bitmap((fontSizeT*characters), fontSizeT);

          double pointSize = _constants.MeasurementPointSize;
          double pointSizePoint = pointSize*6/4;
          Point winPointText = new Point {X = winPoint.X + pointSizePoint, Y = winPoint.Y - pointSizePoint};
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
