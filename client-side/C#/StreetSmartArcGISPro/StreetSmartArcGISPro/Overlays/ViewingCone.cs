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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmart.Common.Interfaces.Data;

using StreetSmartArcGISPro.Configuration.File;

using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using StreetSmartModule = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;

using WinPoint = System.Windows.Point;
using SystCol = System.Drawing.Color;

namespace StreetSmartArcGISPro.Overlays
{
  public class ViewingCone: INotifyPropertyChanged
  {
    #region Constants

    private const double Size = 96.0;
    private const byte Alpha = 192;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private ICoordinate _coordinate;
    private MapPoint _mapPoint;
    private IOrientation _orientation;
    private bool _isInitialized;
    private IDisposable _disposePolygon;
    private SystCol? _color;

    #endregion Members

    #region Constructors

    public ViewingCone()
    {
      Coordinate = null;
    }

    #endregion

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

    public ICoordinate Coordinate
    {
      get => _coordinate;
      private set
      {
        _coordinate = value;
        OnPropertyChanged();
      }
    }

    #endregion

    #region Functions

    protected async Task InitializeAsync(ICoordinate coordinate, IOrientation orientation, Color color)
    {
      Coordinate = coordinate;
      _orientation = orientation;
      Color = color;
      _isInitialized = true;

      double x = coordinate.X ?? 0.0;
      double y = coordinate.Y ?? 0.0;
      Settings settings = Settings.Instance;
      MySpatialReference spatRel = settings.CycloramaViewerCoordinateSystem;

      await QueuedTask.Run(() =>
      {
        Map map = MapView.Active?.Map;
        SpatialReference mapSpatialReference = map?.SpatialReference;
        SpatialReference spatialReference = spatRel?.ArcGisSpatialReference ?? mapSpatialReference;
        MapPoint point = MapPointBuilder.CreateMapPoint(x, y, spatialReference);

        if (mapSpatialReference != null && spatialReference.Wkid != mapSpatialReference.Wkid)
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
      Color = null;

      if (_isInitialized)
      {
        MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
        _isInitialized = false;
      }
    }

    public async Task UpdateAsync(IOrientation orientation)
    {
      if (_isInitialized)
      {
        const double epsilon = 1.0;
        double angle = orientation.Yaw ?? 0.0;
        double hFov = orientation.HFov ?? 0.0;
        double thisAngle = _orientation.Yaw ?? 0.0;
        double thishFov = _orientation.HFov ?? 0.0;
        bool update = !(Math.Abs(thisAngle - angle) < epsilon) || !(Math.Abs(thishFov - hFov) < epsilon);

        if (update)
        {
          _orientation = orientation;
          await RedrawConeAsync();
        }
      }
    }

    private async Task RedrawConeAsync()
    {
      await QueuedTask.Run(() =>
      {
        StreetSmartModule streetSmart = StreetSmartModule.Current;

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
          double angleh = (_orientation.HFov ?? 0.0) * Math.PI / 360;
          double angle = (270 + (_orientation.Yaw ?? 0.0)) % 360 * Math.PI / 180;
          double angle1 = angle - angleh;
          double angle2 = angle + angleh;
          double x = point.X;
          double y = point.Y;
          double size = Size / 2;

          WinPoint screenPoint1 = new WinPoint(x + size * Math.Cos(angle1), y + size * Math.Sin(angle1));
          WinPoint screenPoint2 = new WinPoint(x + size * Math.Cos(angle2), y + size * Math.Sin(angle2));
          MapPoint point1 = thisView.ScreenToMap(screenPoint1);
          MapPoint point2 = thisView.ScreenToMap(screenPoint2);

          IList<MapPoint> polygonPointList = new List<MapPoint>();
          polygonPointList.Add(_mapPoint);
          polygonPointList.Add(point1);
          polygonPointList.Add(point2);
          polygonPointList.Add(_mapPoint);
          Polygon polygon = PolygonBuilder.CreatePolygon(polygonPointList);

          Color colorPolygon = SystCol.FromArgb(Alpha, thisColor);
          CIMColor cimColorPolygon = ColorFactory.Instance.CreateColor(colorPolygon);
          CIMPolygonSymbol polygonSymbol = SymbolFactory.Instance.DefaultPolygonSymbol;
          polygonSymbol.SetColor(cimColorPolygon);
          polygonSymbol.SetOutlineColor(null);
          CIMSymbolReference polygonSymbolReference = polygonSymbol.MakeSymbolReference();
          IDisposable disposePolygon = thisView.AddOverlay(polygon, polygonSymbolReference);

          _disposePolygon?.Dispose();
          _disposePolygon = disposePolygon;
        }
        else
        {
          _disposePolygon?.Dispose();
        }
      });
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
