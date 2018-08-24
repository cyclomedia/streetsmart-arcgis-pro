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
using System.ComponentModel;
using System.Linq;

namespace StreetSmartArcGISPro.Overlays
{
  #region Delegates

  public delegate void ViewerDelegate(Viewer viewer);

  #endregion

  public class ViewerList : Dictionary<uint, Viewer>
  {
    #region Events

    public event ViewerDelegate ViewerAdded;
    public event ViewerDelegate ViewerRemoved;
    public event ViewerDelegate ViewerMoved;

    #endregion

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

    public Viewer Get(uint viewerId)
    {
      return ContainsKey(viewerId) ? this[viewerId] : null;
    }

    public Viewer Get(string imageId)
    {
      return Viewers.Aggregate<Viewer, Viewer>(null, (current, viewer) => (viewer.ImageId == imageId) ? viewer : current);
    }

    public void Add(uint viewerId, string imageId, double overlayDrawDistance)
    {
      Viewer viewer = new Viewer(viewerId, imageId, overlayDrawDistance);
      viewer.PropertyChanged += OnViewerPropertyChanged;
      Add(viewerId, viewer);
      ViewerAdded?.Invoke(viewer);
    }

    public void Delete(uint viewerId)
    {
      if (ContainsKey(viewerId))
      {
        Viewer viewer = this[viewerId];
        viewer.PropertyChanged -= OnViewerPropertyChanged;
        viewer.Dispose();
        Remove(viewerId);
        ViewerRemoved?.Invoke(viewer);
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
