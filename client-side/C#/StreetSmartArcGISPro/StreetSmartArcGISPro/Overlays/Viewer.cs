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

using System.Drawing;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using StreetSmart.Common.Interfaces.Data;

namespace StreetSmartArcGISPro.Overlays
{
  public class Viewer : ViewingCone
  {
    #region Members

    private string _imageId;

    #endregion

    #region Properties

    public string ImageId
    {
      get => _imageId;
      set
      {
        _imageId = value;
        OnPropertyChanged();
      }
    }

    public bool HasMarker { get; set; }

    #endregion

    #region Constructors

    public Viewer(string imageId)
    {
      ImageId = imageId;
      HasMarker = false;
    }

    #endregion

    #region Functions

    public async Task SetAsync(ICoordinate coordinate, IOrientation orientation, Color color, MapView mapView)
    {
      Dispose();
      await InitializeAsync(coordinate, orientation, color, mapView);
    }

    #endregion
  }
}
