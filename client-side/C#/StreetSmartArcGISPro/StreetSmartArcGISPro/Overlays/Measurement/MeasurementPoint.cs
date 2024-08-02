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
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using StreetSmartGeometryType = StreetSmart.Common.Interfaces.GeoJson.GeometryType;
using WindowsPoint = System.Windows.Point;

namespace StreetSmartArcGISPro.Overlays.Measurement
{
  public class MeasurementPoint : Dictionary<string, MeasurementObservation>, INotifyPropertyChanged, IDisposable
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly ConstantsViewer _constants;
    private readonly CultureInfo _ci;

    public IFeature Feature { get; set; }

    private MapPoint _point;
    private bool _open;
    private IDisposable _disposeText;
    private bool _updatePoint;
    private bool _isDisposed;

    #endregion

    #region Properties

    public Measurement Measurement { get; }

    public int PointId { get; set; }

    public bool Open
    {
      get => _open;
      private set
      {
        _open = value;
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

    public bool Updated { get; set; }

    public bool IsFirstNumber => PointId == 0;

    public bool IsLastNumber => Measurement == null || PointId == Measurement.Count - 1;

    public MeasurementPoint PreviousPoint
      => Measurement != null && !IsFirstNumber ? Measurement.GetPointByNr(PointId - 1) : null;

    public MeasurementPoint NextPoint
      => Measurement != null && !IsLastNumber ? Measurement.GetPointByNr(PointId + 1) : null;

    #endregion

    #region Constructor

    public MeasurementPoint(int pointId, Measurement measurement)
    {
      _isDisposed = false;
      _updatePoint = false;
      Measurement = measurement;
      Point = null;
      PointId = pointId;
      Open = false;
      _constants = ConstantsViewer.Instance;
      _ci = CultureInfo.InvariantCulture;
      MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);
    }

    #endregion

    #region Functions

    public async Task UpdateObservationAsync(IResultDirection direction, int i)
    {
      IResultDirectionPanorama directionPan = direction as IResultDirectionPanorama;
      string imageId = direction.Id;
      double x = directionPan?.Position?.X ?? 0.0;
      double y = directionPan?.Position?.Y ?? 0.0;
      double z = directionPan?.Position?.Z ?? 0.0;
      double xDir = directionPan?.Direction?.X ?? 0.0;
      double yDir = directionPan?.Direction?.Y ?? 0.0;
      MapView mapView = await Measurement.GetMeasurementView();

      if (mapView != null)
      {
        MapPoint point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z, mapView);

        if (ContainsKey(imageId))
        {
          this[imageId].Point = point;
          this[imageId].ImageId = imageId;
          this[imageId].XDir = xDir;
          this[imageId].YDir = yDir;
          this[imageId].LineNumber = i;
          await this[imageId].RedrawObservationAsync();
        }
        else
        {
          MeasurementObservation measurementObservation =
            new MeasurementObservation(this, imageId, point, xDir, yDir, i);
          Add(imageId, measurementObservation);
          await measurementObservation.RedrawObservationAsync();
        }
      }
    }

    public bool IsObservationVisible()
    {
      ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
      return streetSmart.InsideScale(MapView.Active) && !_isDisposed;
    }

    public void RemoveObservation(string imageId)
    {
      if (ContainsKey(imageId))
      {
        MeasurementObservation observation = this[imageId];
        observation.Dispose();
        Remove(imageId);
      }
    }

    public bool HasOneObservation()
    {
      return Count == 1;
    }

    public void Dispose()
    {
      _isDisposed = true;
      MapViewCameraChangedEvent.Unsubscribe(OnMapViewCameraChanged);
      _disposeText?.Dispose();

      while (Count >= 1)
      {
        var element = this.ElementAt(0);
        MeasurementObservation observation = element.Value;
        observation.Dispose();
        Remove(element.Key);
      }
    }

    public async Task UpdatePointAsync(IFeature measurementPoint, int index)
    {
      _isDisposed = false;
      bool result = false;

      if (!_updatePoint)
      {
        _updatePoint = true;
        PointId = index;

        double x = 0;
        double y = 0;
        double z = 0;

        Feature = measurementPoint;
        IGeometry geom = Feature.Geometry;

        switch (geom.Type)
        {
          case StreetSmartGeometryType.Point:
            IPoint point = (IPoint)geom;
            result = point.X == null || point.Y == null;
            x = point.X ?? 0;
            y = point.Y ?? 0;
            z = point.Z ?? 0;
            break;
          case StreetSmartGeometryType.LineString:
            ILineString line = (ILineString)geom;
            result = line[index].X == null || line[index].Y == null;
            x = line[index].X ?? 0;
            y = line[index].Y ?? 0;
            z = line[index].Z ?? 0;
            break;
          case StreetSmartGeometryType.Polygon:
            IPolygon polygon = (IPolygon)geom;
            result = polygon[0][index].X == null || polygon[0][index].Y == null;
            x = polygon[0][index].X ?? 0;
            y = polygon[0][index].Y ?? 0;
            z = polygon[0][index].Z ?? 0;
            break;
        }

        IMeasurementProperties properties = measurementPoint.Properties as IMeasurementProperties;
        IMeasureDetails details = properties?.MeasureDetails?.Count > index ? properties.MeasureDetails[index] : null;
        List<string> imageIds = [];

        if (details?.Details is IDetailsForwardIntersection detailForwardIntersection)
        {
          for (int i = 0; i < detailForwardIntersection.ResultDirections.Count; i++)
          {
            imageIds.Add(detailForwardIntersection.ResultDirections[i].Id);
            await UpdateObservationAsync(detailForwardIntersection.ResultDirections[i], i);
          }
        }

        List<string> toRemove = [];

        foreach (string observation in Keys)
        {
          if (!imageIds.Contains(observation))
          {
            toRemove.Add(observation);
          }
        }

        foreach (string observation in toRemove)
        {
          RemoveObservation(observation);
        }

        if (!result)
        {
          MapView mapView = await Measurement.GetMeasurementView();

          if (mapView != null)
          {
            Point = await CoordSystemUtils.CycloramaToMapPointAsync(x, y, z, mapView);

            MapView thisView = MapView.Active;
            Geometry geometry = await thisView.GetCurrentSketchAsync(); //this is where the point on the map dissappears and where z values are created wrong
            Updated = true;

            if (geometry != null)
            {
              var ptColl = await Measurement.ToPointCollectionAsync(geometry); //geometry and point have different z values

              if (ptColl != null)
              {
                if (PointId < ptColl.Count)
                {
                  MapPoint pointC = ptColl[PointId];

                  if (IsSame(pointC))
                  {
                    // Updated = false;
                  }
                }
              }
            }
          }
        }
        else
        {
          Point = null;
        }

        _updatePoint = false;
      }

      await RedrawPointAsync();
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

    public async Task RedrawPointAsync()
    {
      await QueuedTask.Run(() =>
      {
        ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
        MeasurementList measurementList = streetSmart.MeasurementList;
        _disposeText?.Dispose();

        if (streetSmart.InsideScale(MapView.Active) && !_isDisposed && Point != null
            && !double.IsNaN(Point.X) && !double.IsNaN(Point.Y) && !Measurement.IsDisposed)
        {
          if (Measurement.Geometry.Type != StreetSmartGeometryType.Polygon || Measurement.Count >= 1 &&
              (PointId == 0 || !IsSame(Measurement[Measurement.ElementAt(0).Key].Point)))
          {
            MapView thisView = MapView.Active;
            WindowsPoint winPoint = thisView.MapToScreen(Point);
            float fontSize = _constants.MeasurementFontSize;
            int fontSizeT = (int)(fontSize * 2);
            int fontSizeR = (int)(fontSize * 3 / 2);
            int fontSizeK = (int)(fontSize / 4);
            string text = (PointId + 1).ToString(_ci);
            int characters = text.Length;
            Bitmap bitmap = new Bitmap((fontSizeT * characters), fontSizeT);

            double pointSize = _constants.MeasurementPointSize;
            double pointSizePoint = pointSize * 6 / 4;
            WindowsPoint winPointText = new WindowsPoint
            { X = winPoint.X + pointSizePoint, Y = winPoint.Y - pointSizePoint };
            MapPoint pointText = thisView.ScreenToMap(winPointText);

            using (var sf = new StringFormat())
            {
              using (Graphics g = Graphics.FromImage(bitmap))
              {
                g.Clear(Color.Transparent);
                Font font = new Font("Arial", fontSize);
                sf.Alignment = StringAlignment.Center;
                Rectangle rectangle = new Rectangle(fontSizeK, fontSizeK, (fontSizeR * characters), fontSizeR);
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
        }
      });
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
