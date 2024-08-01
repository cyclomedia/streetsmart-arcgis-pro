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
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmart.Common.Interfaces.GeoJson;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using Point = System.Windows.Point;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  public class MeasurementObservation : IDisposable, INotifyPropertyChanged
  {
    #region Constants

    private const double OuterLineSize = 1.25;
    private const double DistLine = 30.0;

    #endregion

    #region Members

    private readonly MeasurementPoint _measurementPoint;

    private MapPoint _point;
    private Viewer _viewer;
    private string _imageId;
    private IDisposable _disposeOuterLine;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Properties

    public double XDir { get; set; }

    public double YDir { get; set; }

    public int LineNumber { get; set; }

    public string ImageId
    {
      get => _imageId;
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

    #endregion

    #region Constructor

    public MeasurementObservation(MeasurementPoint measurementPoint, string imageId, MapPoint observationPoint, double xDir, double yDir, int i)
    {
      XDir = xDir;
      YDir = yDir;
      _measurementPoint = measurementPoint;
      ImageId = imageId;
      Point = observationPoint;
      AddIns.Modules.StreetSmart streetSmart = AddIns.Modules.StreetSmart.Current;
      ViewerList viewerList = streetSmart.ViewerList;
      Viewer = viewerList.GetImageId(imageId);
      LineNumber = i;

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
      _disposeOuterLine?.Dispose();
      Viewer = null;
      AddIns.Modules.StreetSmart streetSmart = AddIns.Modules.StreetSmart.Current;
      ViewerList viewerList = streetSmart.ViewerList;

      // event listeners
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      viewerList.ViewerAdded -= OnViewerAdded;
      viewerList.ViewerRemoved -= OnViewerRemoved;
      viewerList.ViewerMoved -= OnViewerMoved;
      _measurementPoint.PropertyChanged -= OnPropertyMeasurementPointChanged;
      PropertyChanged -= OnPropertyMeasurementObservationChanged;
    }

    public async Task RedrawObservationAsync()
    {
      await QueuedTask.Run(() =>
      {
        _disposeOuterLine?.Dispose();

        if (_measurementPoint?.IsObservationVisible() ?? false)
        {
          MapView thisView = MapView.Active;
          MapPoint measPoint = _measurementPoint?.Point;
          MapPoint mapPointObsLine;

          if (measPoint != null && !double.IsNaN(measPoint.X) && !double.IsNaN(measPoint.Y))
          {
            Point winMeasPoint = thisView.MapToScreen(measPoint);
            Point winObsPoint = thisView.MapToScreen(Point);

            double xdir = (winMeasPoint.X - winObsPoint.X) * 3 / 2;
            double ydir = (winMeasPoint.Y - winObsPoint.Y) * 3 / 2;
            Point winPointObsLine = new Point(winObsPoint.X + xdir, winObsPoint.Y + ydir);
            mapPointObsLine = thisView.ScreenToMap(winPointObsLine);
          }
          else
          {
            mapPointObsLine = MapPointBuilderEx.CreateMapPoint(Point.X + XDir * DistLine, Point.Y + YDir * DistLine);
          }

          IList<MapPoint> linePointList = [mapPointObsLine, Point];
#if ARCGISPRO29
          Polyline polyline = PolylineBuilder.CreatePolyline(linePointList);
#else
          Polyline polyline = PolylineBuilderEx.CreatePolyline(linePointList);
#endif
          Color outerColorLine = Color.DarkGray;
          IObservationLines observationLines = _measurementPoint.Measurement.ObservationLines;

          //if (observationLines.ActiveObservation + 1 == _measurementPoint.PointId)
          //{
          //  if (observationLines.RecordingId == ImageId)
          //  {
          //    outerColorLine = observationLines.Color;
          //  }
          //  else
          //  {
          //    switch (LineNumber)
          //    {
          //      case 0:
          //        outerColorLine = Color.Blue;
          //        break;
          //      case 1:
          //        outerColorLine = Color.Yellow;
          //        break;
          //      case 2:
          //        outerColorLine = Color.Red;
          //        break;
          //    }
          //  }
          //}

          CIMColor cimOuterColorLine = ColorFactory.Instance.CreateColor(Color.FromArgb(255, outerColorLine));
          CIMLineSymbol cimOuterLineSymbol = SymbolFactory.Instance.DefaultLineSymbol;
          cimOuterLineSymbol.SetColor(cimOuterColorLine);
          cimOuterLineSymbol.SetSize(OuterLineSize);
          CIMSymbolReference cimOuterLineSymbolRef = cimOuterLineSymbol.MakeSymbolReference();
          _disposeOuterLine = thisView.AddOverlay(polyline, cimOuterLineSymbolRef);
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
