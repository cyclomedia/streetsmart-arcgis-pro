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
using System.Threading.Tasks;
using System.Windows.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using GlobeSpotterAPI;

using StreetSmartArcGISPro.AddIns.Modules;

using ApiPoint = GlobeSpotterAPI.MeasurementPoint;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using Point = System.Windows.Point;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  public class MeasurementObservation: IDisposable, INotifyPropertyChanged
  {
    #region Constants

    private const double InnerLineSize = 0.75;
    private const double OuterLineSize = 1.25;
    private const double DistLine = 30.0;

    #endregion

    #region Members

    private readonly MeasurementPoint _measurementPoint;

    private MapPoint _point;
    private Viewer _viewer;
    private string _imageId;
    private IDisposable _disposeInnerLine;
    private IDisposable _disposeOuterLine;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Properties

    public double XDir { get; set; }

    public double YDir { get; set; }

    public string ImageId
    {
      get { return _imageId; }
      set
      {
        _imageId = value;
        OnPropertyChanged();
      }
    }

    public Viewer Viewer
    {
      get => _viewer;
      private set
      {
        if (_viewer != null)
        {
          _viewer.PropertyChanged -= OnViewerPropertyChanged;
        }

        _viewer = value;

        if (_viewer != null)
        {
          _viewer.PropertyChanged += OnViewerPropertyChanged;
        }

        OnPropertyChanged();
      }
    }

    public MapPoint Point
    {
      private get
      {
        return _point;
      }
      set
      {
        _point = value;
        OnPropertyChanged();
      }
    }

    public Bitmap Match { get; set; }

    #endregion

    #region Constructor

    public MeasurementObservation(MeasurementPoint measurementPoint, string imageId, MapPoint observationPoint, Bitmap match, double xDir, double yDir)
    {
      XDir = xDir;
      YDir = yDir;
      _measurementPoint = measurementPoint;
      ImageId = imageId;
      Point = observationPoint;
      Match = match;      
      StreetSmart streetSmart = StreetSmart.Current;
      ViewerList viewerList = streetSmart.ViewerList;
      Viewer = viewerList.Get(imageId);

      // event listeners
      _measurementPoint.PropertyChanged += OnPropertyMeasurementPointChanged;
      PropertyChanged += OnPropertyMeasurementObservationChanged;
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
      viewerList.ViewerAdded += OnViewerAdded;
      viewerList.ViewerRemoved += OnViewerRemoved;
      viewerList.ViewerMoved += OnViewerMoved;
    }

    #endregion

    #region Functions

    public void Dispose()
    {      
      _disposeInnerLine?.Dispose();
      _disposeOuterLine?.Dispose();
      Viewer = null;
      StreetSmart streetSmart = StreetSmart.Current;
      ViewerList viewerList = streetSmart.ViewerList;

      // event listeners
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      viewerList.ViewerAdded -= OnViewerAdded;
      viewerList.ViewerRemoved -= OnViewerRemoved;
      viewerList.ViewerMoved -= OnViewerMoved;
      _measurementPoint.PropertyChanged -= OnPropertyMeasurementPointChanged;
      PropertyChanged -= OnPropertyMeasurementObservationChanged;
    }

    public void RemoveMe()
    {
      _measurementPoint.RemoveObservation(this);
    }

    public void LookAtMe()
    {
      ApiPoint point = _measurementPoint.ApiPoint;
      Point3D coord = new Point3D(point.x, point.y, point.z);
      DockPanestreetSmart streetSmart = DockPanestreetSmart.Show();

      if (streetSmart != null)
      {
        streetSmart.LookAt = coord;
        streetSmart.Replace = true;
        streetSmart.Nearest = false;
        streetSmart.Location = ImageId;
      }
    }

    public async Task RedrawObservationAsync()
    {
      await QueuedTask.Run(() =>
      {
        _disposeInnerLine?.Dispose();
        _disposeOuterLine?.Dispose();

        if (_measurementPoint?.IsObservationVisible() ?? false)
        {
          MapView thisView = MapView.Active;
          MapPoint measPoint = _measurementPoint?.Point;
          MapPoint mapPointObsLine;

          if ((measPoint != null) && (!double.IsNaN(measPoint.X)) && (!double.IsNaN(measPoint.Y)))
          {
            Point winMeasPoint = thisView.MapToScreen(measPoint);
            Point winObsPoint = thisView.MapToScreen(Point);

            double xdir = ((winMeasPoint.X - winObsPoint.X) * 3) / 2;
            double ydir = ((winMeasPoint.Y - winObsPoint.Y) * 3) / 2;
            Point winPointObsLine = new Point(winObsPoint.X + xdir, winObsPoint.Y + ydir);
            mapPointObsLine = thisView.ScreenToMap(winPointObsLine);
          }
          else
          {
            mapPointObsLine = MapPointBuilder.CreateMapPoint((Point.X + (XDir*DistLine)), (Point.Y + (YDir*DistLine)));
          }

          IList<MapPoint> linePointList = new List<MapPoint>();
          linePointList.Add(mapPointObsLine);
          linePointList.Add(Point);
          Polyline polyline = PolylineBuilder.CreatePolyline(linePointList);

          Color outerColorLine = Viewer?.Color ?? Color.DarkGray;
          CIMColor cimOuterColorLine = ColorFactory.Instance.CreateColor(Color.FromArgb(255, outerColorLine));
          CIMLineSymbol cimOuterLineSymbol = SymbolFactory.Instance.DefaultLineSymbol;
          cimOuterLineSymbol.SetColor(cimOuterColorLine);
          cimOuterLineSymbol.SetSize(OuterLineSize);
          CIMSymbolReference cimOuterLineSymbolRef = cimOuterLineSymbol.MakeSymbolReference();
          _disposeOuterLine = thisView.AddOverlay(polyline, cimOuterLineSymbolRef);

          Color innerColorLine = Color.LightGray;
          CIMColor cimInnerColorLine = ColorFactory.Instance.CreateColor(innerColorLine);
          CIMLineSymbol cimInnerLineSymbol = SymbolFactory.Instance.DefaultLineSymbol;
          cimInnerLineSymbol.SetColor(cimInnerColorLine);
          cimInnerLineSymbol.SetSize(InnerLineSize);
          CIMSymbolReference cimInnerLineSymbolRef = cimInnerLineSymbol.MakeSymbolReference();
          _disposeInnerLine = thisView.AddOverlay(polyline, cimInnerLineSymbolRef);
        }
      });
    }

    private async Task RedrawViewAsync()
    {
      await RedrawObservationAsync();
      ICollectionView view = CollectionViewSource.GetDefaultView(_measurementPoint);
      view.Refresh();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Event handlers

    private async void OnViewerAdded(Viewer viewer)
    {
      if (viewer.ImageId == ImageId)
      {
        Viewer = viewer;
        await RedrawViewAsync();
      }
    }

    private async void OnViewerRemoved(Viewer viewer)
    {
      if (viewer.ImageId == ImageId)
      {
        Viewer = null;
        await RedrawViewAsync();
      }
    }

    private async void OnViewerMoved(Viewer viewer)
    {
      Viewer = (viewer.ImageId == ImageId) ? viewer : null;
      await RedrawViewAsync();
    }

    private async void OnViewerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      string propertyName = args.PropertyName;

      if (propertyName == "Color")
      {
        await RedrawViewAsync();
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("Viewer");
      }
    }

    private async void OnPropertyMeasurementPointChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Point")
      {
        await RedrawViewAsync();
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("Viewer");
      }
    }

    private async void OnPropertyMeasurementObservationChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "Point")
      {
        await RedrawViewAsync();
        // ReSharper disable once ExplicitCallerInfoArgument
        OnPropertyChanged("Viewer");
      }
    }

    private async void OnMapViewCameraChanged(MapViewCameraChangedEventArgs args)
    {
      await RedrawObservationAsync();
    }

    #endregion
  }
}
