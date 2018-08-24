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

using System.Drawing;
using System.Threading.Tasks;
using GlobeSpotterAPI;

namespace StreetSmartArcGISPro.Overlays
{
  public class Viewer : ViewingCone
  {
    #region Members

    private RecordingLocation _location;
    private string _imageId;

    #endregion

    #region Properties

    public RecordingLocation Location
    {
      get => _location;
      private set
      {
        _location = value;
        OnPropertyChanged();
      }
    }

    public string ImageId
    {
      get => _imageId;
      set
      {
        _imageId = value;
        OnPropertyChanged();
      }
    }

    public uint ViewerId { get; }

    public double OverlayDrawDistance { get; set; }

    public bool HasMarker { get; set; }

    #endregion

    #region Constructors

    public Viewer(uint viewerId, string imageId, double overlayDrawDistance)
    {
      Location = null;
      ViewerId = viewerId;
      ImageId = imageId;
      OverlayDrawDistance = overlayDrawDistance;
      HasMarker = false;
    }

    #endregion

    #region Functions

    public async Task SetAsync(RecordingLocation location, double angle, double hFov, Color color)
    {
      Dispose();
      Location = location;
      await InitializeAsync(location, angle, hFov, color);
    }

    #endregion
  }
}
