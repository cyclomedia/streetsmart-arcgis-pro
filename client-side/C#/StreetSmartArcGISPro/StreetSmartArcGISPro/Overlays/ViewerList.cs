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

using StreetSmart.Common.Interfaces.API;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace StreetSmartArcGISPro.Overlays
{
  #region Delegates

  public delegate void ViewerDelegate(Viewer viewer);

  #endregion

  public class ViewerList : Dictionary<IPanoramaViewer, Viewer>
  {
    #region Events

    public event ViewerDelegate ViewerAdded;
    public event ViewerDelegate ViewerRemoved;
    public event ViewerDelegate ViewerMoved;

    #endregion

    public IPanoramaViewer ActiveViewer { get; set; }

    public List<Viewer> MarkerViewers => (from viewer in this where viewer.Value.HasMarker select viewer.Value).ToList();

    public ICollection<Viewer> Viewers => Values;

    public void RemoveViewers()
    {
      foreach (var viewer in this)
      {
        Viewer myViewer = viewer.Value;
        myViewer.Dispose();
      }

      Clear();
    }

    public Viewer GetViewer(IPanoramaViewer panoramaViewer)
    {
      return ContainsKey(panoramaViewer) ? this[panoramaViewer] : null;
    }

    public Viewer GetImageId(string imageId)
    {
      return Viewers.FirstOrDefault(viewer => viewer.ImageId == imageId);
    }

    public void Add(IPanoramaViewer panoramaViewer, string imageId)
    {
      ActiveViewer = panoramaViewer;
      Viewer viewer = new Viewer(imageId);
      viewer.PropertyChanged += OnViewerPropertyChanged;
      Add(panoramaViewer, viewer);
      ViewerAdded?.Invoke(viewer);
    }

    public void Delete(IPanoramaViewer panoramaViewer)
    {
      if (ContainsKey(panoramaViewer))
      {
        Viewer viewer = this[panoramaViewer];
        viewer.PropertyChanged -= OnViewerPropertyChanged;
        viewer.Dispose();
        Remove(panoramaViewer);
        ViewerRemoved?.Invoke(viewer);
      }

      if (ActiveViewer == panoramaViewer)
      {
        ActiveViewer = null;
      }
    }

    #region event listners

    private void OnViewerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == "ImageId" && sender is Viewer sendViewer)
      {
        ViewerMoved?.Invoke(sendViewer);
      }
    }

    #endregion
  }
}
