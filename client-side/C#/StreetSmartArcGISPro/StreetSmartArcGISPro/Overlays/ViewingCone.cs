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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using GlobeSpotterAPI;
using StreetSmartArcGISPro.AddIns.Modules;
using StreetSmartArcGISPro.Configuration.File;

using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using WinPoint = System.Windows.Point;
using SystCol = System.Drawing.Color;

namespace StreetSmartArcGISPro.Overlays
{
  public class ViewingCone: INotifyPropertyChanged
  {
    #region Constants

    private const double BorderSize = 1.5;
    private const double BorderSizeBlinking = 2.5;
    private const double Size = 96.0;
    private const byte BlinkAlpha = 255;
    private const byte NormalAlpha = 128;
    private const int BlinkTime = 300;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Constructor

    protected ViewingCone()
    {
      _active = false;
      _blinking = false;
    }

    #endregion

    #region Members

    private MapPoint _mapPoint;
    private double _angle;
    private double _hFov;
    private bool _blinking;
    private Timer _blinkTimer;
    private bool _active;
    private bool _isInitialized;
    private IDisposable _disposePolygon;
    private IDisposable _disposePolyLine;
    private SystCol? _color;

    #endregion Members

    #region Properties

    public SystCol? Color
    {
      get => _color;
      private set
      {
        _color = value;
        OnPropertyChanged();
      }
    }

    #endregion

    #region Functions

    protected async Task InitializeAsync(RecordingLocation location, double angle, double hFov, Color color)
    {
      _angle = angle;
      _hFov = hFov;
      Color = color;
      _isInitialized = true;

      double x = location.X;
      double y = location.Y;
      Settings settings = Settings.Instance;
      MySpatialReference spatRel = settings.CycloramaViewerCoordinateSystem;

      await QueuedTask.Run(() =>
      {
        Map map = MapView.Active?.Map;
        SpatialReference mapSpatialReference = map?.SpatialReference;
        SpatialReference spatialReference = spatRel?.ArcGisSpatialReference ?? mapSpatialReference;
        MapPoint point = MapPointBuilder.CreateMapPoint(x, y, spatialReference);

        if ((mapSpatialReference != null) && spatialReference.Wkid != mapSpatialReference.Wkid)
        {
          ProjectionTransformation projection = ProjectionTransformation.Create(spatialReference, mapSpatialReference);
          _mapPoint = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
        }
        else
        {
          _mapPoint = (MapPoint) point.Clone();
        }
      });

      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
      await RedrawConeAsync();
    }

    public void Dispose()
    {
      _disposePolygon?.Dispose();
      _disposePolyLine?.Dispose();
      Color = null;

      if (_isInitialized)
      {
        MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
        _isInitialized = false;
      }
    }

    public async Task UpdateAsync(double angle, double hFov)
    {
      if (_isInitialized)
      {
        const double epsilon = 1.0;
        bool update = (!(Math.Abs(_angle - angle) < epsilon)) || (!(Math.Abs(_hFov - hFov) < epsilon));

        if (update)
        {
          _hFov = hFov;
          _angle = angle;
          await RedrawConeAsync();
        }
      }
    }

    public async Task SetActiveAsync(bool active)
    {
      _blinking = active;
      _active = active;

      if (_isInitialized)
      {
        await RedrawConeAsync();
      }
    }

    private async Task RedrawConeAsync()
    {
      await QueuedTask.Run(() =>
      {
        StreetSmart streetSmart = StreetSmart.Current;

        if (streetSmart.InsideScale() && !_mapPoint.IsEmpty && Color != null)
        {
          var thisColor = (SystCol) Color;
          MapView thisView = MapView.Active;
          Map map = thisView.Map;
          SpatialReference mapSpat = map.SpatialReference;
          SpatialReference mapPointSpat = _mapPoint.SpatialReference;
          ProjectionTransformation projection = ProjectionTransformation.Create(mapPointSpat, mapSpat);
          _mapPoint = GeometryEngine.Instance.ProjectEx(_mapPoint, projection) as MapPoint;

          WinPoint point = thisView.MapToScreen(_mapPoint);
          double angleh = (_hFov*Math.PI)/360;
          double angle = (((270 + _angle)%360)*Math.PI)/180;
          double angle1 = angle - angleh;
          double angle2 = angle + angleh;
          double x = point.X;
          double y = point.Y;
          double size = Size/2;

          WinPoint screenPoint1 = new WinPoint(x + size*Math.Cos(angle1), y + size*Math.Sin(angle1));
          WinPoint screenPoint2 = new WinPoint(x + size*Math.Cos(angle2), y + size*Math.Sin(angle2));
          MapPoint point1 = thisView.ScreenToMap(screenPoint1);
          MapPoint point2 = thisView.ScreenToMap(screenPoint2);

          IList<MapPoint> polygonPointList = new List<MapPoint>();
          polygonPointList.Add(_mapPoint);
          polygonPointList.Add(point1);
          polygonPointList.Add(point2);
          polygonPointList.Add(_mapPoint);
          Polygon polygon = PolygonBuilder.CreatePolygon(polygonPointList);

          Color colorPolygon = SystCol.FromArgb(_blinking ? BlinkAlpha : NormalAlpha, thisColor);
          CIMColor cimColorPolygon = ColorFactory.Instance.CreateColor(colorPolygon);
          CIMPolygonSymbol polygonSymbol = SymbolFactory.Instance.DefaultPolygonSymbol;
          polygonSymbol.SetColor(cimColorPolygon);
          polygonSymbol.SetOutlineColor(null);
          CIMSymbolReference polygonSymbolReference = polygonSymbol.MakeSymbolReference();
          IDisposable disposePolygon = thisView.AddOverlay(polygon, polygonSymbolReference);

          IList<MapPoint> linePointList = new List<MapPoint>();
          linePointList.Add(point1);
          linePointList.Add(_mapPoint);
          linePointList.Add(point2);
          Polyline polyline = PolylineBuilder.CreatePolyline(linePointList);

          Color colorLine = _active ? SystCol.Yellow : SystCol.Gray;
          CIMColor cimColorLine = ColorFactory.Instance.CreateColor(colorLine);
          CIMLineSymbol cimLineSymbol = SymbolFactory.Instance.DefaultLineSymbol;
          cimLineSymbol.SetColor(cimColorLine);
          cimLineSymbol.SetSize(_blinking ? BorderSizeBlinking : BorderSize);
          CIMSymbolReference lineSymbolReference = cimLineSymbol.MakeSymbolReference();
          IDisposable disposePolyLine = thisView.AddOverlay(polyline, lineSymbolReference);

          _disposePolygon?.Dispose();
          _disposePolygon = disposePolygon;
          _disposePolyLine?.Dispose();
          _disposePolyLine = disposePolyLine;

          if (_blinking)
          {
            var blinkEvent = new AutoResetEvent(true);
            var blinkTimerCallBack = new TimerCallback(ResetBlinking);
            _blinkTimer = new Timer(blinkTimerCallBack, blinkEvent, BlinkTime, -1);
          }
        }
        else
        {
          _disposePolygon?.Dispose();
          _disposePolyLine?.Dispose();
        }
      });
    }

    #endregion

    #region Thread functions

    private async void ResetBlinking(object args)
    {
      _blinking = false;
      await RedrawConeAsync();
    }

    #endregion

    #region Event handlers

    private async void OnMapViewCameraChanged(MapViewCameraChangedEventArgs args)
    {
      await RedrawConeAsync();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
