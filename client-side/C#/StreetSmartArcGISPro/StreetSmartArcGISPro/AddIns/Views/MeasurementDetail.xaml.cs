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

using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.VectorLayers;

using DockMeasurementDetail = StreetSmartArcGISPro.AddIns.DockPanes.MeasurementDetail;
using Geometry = ArcGIS.Core.Geometry.Geometry;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for MeasurementDetail.xaml
  /// </summary>
  public partial class MeasurementDetail
  {
    #region Constructor

    public MeasurementDetail()
    {
      InitializeComponent();
    }

    #endregion

    #region Event handlers

    private void OnShowButtonClicked(object sender, RoutedEventArgs e)
    {
      LookAtObservation();
    }

    private async void OnOpenCloseButtonClicked(object sender, RoutedEventArgs e)
    {
      DockMeasurementDetail measurementDetail = (DockMeasurementDetail) DataContext;
      MeasurementPoint measurementPoint = measurementDetail?.MeasurementPoint;
      Measurement measurement = measurementPoint?.Measurement;

      if (measurementPoint != null)
      {
        if (measurementPoint.Open)
        {
          measurementPoint.ClosePoint();

          if (!measurement.IsPointMeasurement)
          {
            measurement.EnableMeasurementSeries();
          }

          await measurement.MeasurementPointUpdatedAsync(measurementPoint.PointId);

          if (measurement.IsPointMeasurement)
          {
            MapView mapView = MapView.Active;
            Geometry geometry = await mapView.GetCurrentSketchAsync();
            VectorLayer layer = measurement.VectorLayer;
            long? uid = measurement.ObjectId;

            if (geometry != null && uid == null && layer != null)
            {
              await layer.AddFeatureAsync(geometry);
            }
            else if (uid != null && layer != null)
            {
              MapPoint mapPoint = measurementPoint.Point;
              await layer.UpdateFeatureAsync((long) uid, mapPoint);
            }
          }

          measurementDetail.SelectedObservation = null;
        }
        else
        {
          if (measurement.IsPointMeasurement)
          {
            measurement.OpenMeasurement();
          }
          else
          {
            measurement.DisableMeasurementSeries();
          }

          measurementPoint.OpenPoint();
        }
      }
    }

    private async void OnUndoButtonClicked(object sender, RoutedEventArgs e)
    {
      MeasurementPoint measurementPoint = GetMeasurementPoint();
      Measurement measurement = measurementPoint.Measurement;

      if (measurement.IsPointMeasurement)
      {
        measurementPoint.RemoveMe();

        if (measurement.ObjectId == null)
        {
          measurement.AddMeasurementPoint();
        }
        else
        {
          measurement.CreateMeasurementPoint(measurementPoint.LastPoint);
        }
      }
      else
      {
        MapView mapView = MapView.Active;
        Geometry geometry = await mapView.GetCurrentSketchAsync();
        List<MapPoint> points = await measurement.ToPointCollectionAsync(geometry);
        int removePoints = measurement.IsGeometryType(GeometryType.Polygon) && points.Count == 2 &&
                           measurement.PointNr == 1 ? 2 : 1;

        for (int i = 0; i < removePoints; i++)
        {
          int substract = (measurement.IsGeometryType(GeometryType.Polygon) && (removePoints == 1)) ? 2 : 1;
          points.RemoveAt(points.Count - substract);
        }

        await QueuedTask.Run(() =>
        {
          if (measurement.IsGeometryType(GeometryType.Polygon))
          {
            geometry = PolygonBuilder.CreatePolygon(points);
          }
          else if (measurement.IsGeometryType(GeometryType.Polyline))
          {
            geometry = PolylineBuilder.CreatePolyline(points);
          }
        });

        await mapView.SetCurrentSketchAsync(geometry);
      }
    }

    private void OnPrevButtonClicked(object sender, RoutedEventArgs e)
    {
      MeasurementPoint measurementPoint = GetMeasurementPoint();
      MeasurementPoint previousPoint = measurementPoint?.PreviousPoint;
      previousPoint?.OpenPoint();
    }

    private void OnNextButtonClicked(object sender, RoutedEventArgs e)
    {
      MeasurementPoint measurementPoint = GetMeasurementPoint();
      MeasurementPoint nextPoint = measurementPoint?.NextPoint;
      nextPoint?.OpenPoint();
    }

    private void OnMatchesMouseDoubleClicked(object sender, MouseButtonEventArgs e)
    {
      LookAtObservation();
    }

    private void OnObservationsMouseDoubleClicked(object sender, MouseButtonEventArgs e)
    {
      LookAtObservation();
    }

    private void OnObservationRemoved(object sender, MouseButtonEventArgs e)
    {
      MeasurementObservation observation = GetObservation();
      observation?.RemoveMe();
    }

    private void OnOpenNearestCycloramaClicked(object sender, RoutedEventArgs e)
    {
      MeasurementPoint measurementPoint = GetMeasurementPoint();
      measurementPoint?.OpenNearestImage();
    }

    private void OnFocusAllViewersClicked(object sender, RoutedEventArgs e)
    {
      MeasurementPoint measurementPoint = GetMeasurementPoint();
      measurementPoint?.LookAtMeasurement();
    }

    #endregion

    #region Functions

    private void LookAtObservation()
    {
      MeasurementObservation observation = GetObservation();
      observation?.LookAtMe();
    }

    private MeasurementObservation GetObservation()
    {
      DockMeasurementDetail measurementDetail = (DockMeasurementDetail) DataContext;
      return measurementDetail?.SelectedObservation;
    }

    private MeasurementPoint GetMeasurementPoint()
    {
      DockMeasurementDetail measurementDetail = (DockMeasurementDetail) DataContext;
      return measurementDetail?.MeasurementPoint;
    }

    #endregion
  }
}
