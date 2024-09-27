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

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using StreetSmartArcGISPro.Configuration.File;
using System.Linq;
using System.Threading.Tasks;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;

namespace StreetSmartArcGISPro.Utilities
{
  class CoordSystemUtils
  {
    public static async Task<SpatialReference> CycloramaSpatialReferenceAsync(MapView mapView)
    {
      Map map = mapView?.Map;

      Setting settings = ProjectList.Instance.GetSettings(mapView);
      MySpatialReference gsSpatRel = settings?.CycloramaViewerCoordinateSystem;

      SpatialReference mapSpatialReference = map?.SpatialReference;
      SpatialReference gsSpatialReference = gsSpatRel == null
        ? mapSpatialReference
        : gsSpatRel.ArcGisSpatialReference ?? await gsSpatRel.CreateArcGisSpatialReferenceAsync();
      return gsSpatialReference;
    }

    public static async Task<MapPoint> CycloramaToMapPointAsync(double x, double y, double z, MapView mapView)
    {
      Map map = mapView?.Map;
      SpatialReference mapSpatialReference = map?.SpatialReference;
      SpatialReference gsSpatialReference = await CycloramaSpatialReferenceAsync(mapView);
      MapPoint point = null;

      await QueuedTask.Run(() =>
      {
        MapPoint mapPoint = MapPointBuilderEx.CreateMapPoint(x, y, z, gsSpatialReference);

        if (mapSpatialReference != null && gsSpatialReference != null &&
            gsSpatialReference.Wkid != mapSpatialReference.Wkid)
        {
          ProjectionTransformation projection = ProjectionTransformation.Create(gsSpatialReference, mapSpatialReference);
          point = GeometryEngine.Instance.ProjectEx(mapPoint, projection) as MapPoint;
        }
        else
        {
          point = (MapPoint)mapPoint.Clone();
        }
      });

      return point;
    }

    public static async Task<bool> CheckInAreaCycloramaSpatialReferenceAsync(MapView mapView)
    {
      bool result = false;
      Setting settings = ProjectList.Instance.GetSettings(mapView);
      MySpatialReference spatialReference = settings?.CycloramaViewerCoordinateSystem;

      if (spatialReference != null)
      {
        result = await spatialReference.ExistsInAreaAsync();

        if (!result)
        {
          CheckCycloramaSpatialReference(null, mapView);
        }
      }

      return result;
    }

    public static string CheckCycloramaSpatialReferenceMapView(MapView mapView)
    {
      if (mapView == null)
      {
        return null;
      }

      Setting settings = ProjectList.Instance.GetSettings(mapView);
      MySpatialReference spatialReference = settings?.CycloramaViewerCoordinateSystem;
      return CheckCycloramaSpatialReference(spatialReference, mapView);
    }

    private static string CheckCycloramaSpatialReference(MySpatialReference spatialReference, MapView mapView)
    {
      if (mapView == null)
      {
        return null;
      }

      ProjectList projectList = ProjectList.Instance;
      Setting settings = projectList.GetSettings(mapView);
      MySpatialReference recordingSpatialReference = settings?.RecordingLayerCoordinateSystem;
      string epsgCode = spatialReference == null
        ? recordingSpatialReference == null
          ? $"EPSG:{MapView.Active?.Map?.SpatialReference.Wkid ?? 0}"
          : recordingSpatialReference.SRSName
        : spatialReference.SRSName;

      if (spatialReference?.ArcGisSpatialReference == null)
      {
        var spatialReferences = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReferenceDictionary.Instance;
        spatialReference = spatialReferences.GetItem(epsgCode) ?? spatialReferences.FirstOrDefault(spatialReferenceComp => spatialReferenceComp.ArcGisSpatialReference != null);
        if (spatialReference != null)
        {
          epsgCode = spatialReference.SRSName;

          if (settings != null)
          {
            settings.CycloramaViewerCoordinateSystem = spatialReference;
            projectList.Save();
          }
        }
      }

      return epsgCode;
    }
  }
}
