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
using System.Drawing;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.Configuration.File;

using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;

namespace StreetSmartArcGISPro.Overlays
{
  public class CrossCheck
  {
    #region Members

    private IDisposable _disposeCross;

    #endregion

    #region Functions

    public async Task UpdateAsync(double x, double y, double size)
    {
      Settings settings = Settings.Instance;
      MySpatialReference spatRel = settings.CycloramaViewerCoordinateSystem;

      await QueuedTask.Run(() =>
      {
        MapView thisView = MapView.Active;
        Map map = thisView?.Map;
        SpatialReference mapSpatialReference = map?.SpatialReference;
        SpatialReference spatialReference = spatRel?.ArcGisSpatialReference ?? mapSpatialReference;
        MapPoint point = MapPointBuilder.CreateMapPoint(x, y, spatialReference);
        MapPoint mapPoint;

        if (mapSpatialReference != null && spatialReference.Wkid != mapSpatialReference.Wkid)
        {
          ProjectionTransformation projection = ProjectionTransformation.Create(spatialReference, mapSpatialReference);
          mapPoint = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
        }
        else
        {
          mapPoint = (MapPoint) point.Clone();
        }

        if (mapPoint != null && !mapPoint.IsEmpty)
        {
          CIMColor cimColor = ColorFactory.Instance.CreateColor(Color.Black);
          CIMMarker cimMarker = SymbolFactory.Instance.ConstructMarker(cimColor, size, SimpleMarkerStyle.Cross);
          CIMPointSymbol pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(cimMarker);
          CIMSymbolReference pointSymbolReference = pointSymbol.MakeSymbolReference();
          IDisposable disposeCross = thisView.AddOverlay(mapPoint, pointSymbolReference);

          _disposeCross?.Dispose();
          _disposeCross = disposeCross;
        }
      });
    }

    public void Dispose()
    {
      _disposeCross?.Dispose();
    }

    #endregion
  }
}
