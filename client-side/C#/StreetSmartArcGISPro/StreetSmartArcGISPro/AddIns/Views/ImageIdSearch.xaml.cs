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

using System.Windows.Controls;
using System.Windows.Input;

using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using StreetSmartArcGISPro.CycloMediaLayers;

using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using PaneImageIdSearch = StreetSmartArcGISPro.AddIns.DockPanes.ImageIdSearch;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for ImageIdSearch.xaml
  /// </summary>
  public partial class ImageIdSearch
  {
    public ImageIdSearch()
    {
      InitializeComponent();
    }

    private void OnMatchesMouseDoubleClicked(object sender, MouseButtonEventArgs e)
    {
      if (sender is ListBox listBox)
      {
        foreach (Recording selectedItem in listBox.SelectedItems)
        {
          DockPanestreetSmart streetSmart = DockPanestreetSmart.Show();

          if (streetSmart != null)
          {
            streetSmart.MapView = MapView.Active;
            streetSmart.LookAt = null;
            streetSmart.Replace = true;
            streetSmart.Nearest = false;
            streetSmart.Location = selectedItem.ImageId;
          }
        }
      }
    }

    private async void OnImageIdChanged(object sender, TextChangedEventArgs e)
    {
      TextBox textBox = sender as TextBox;
      string imageId = textBox?.Text ?? string.Empty;
      PaneImageIdSearch paneImageIdSearch = (dynamic)DataContext;
      paneImageIdSearch.ImageInfo.Clear();

      if (imageId.Length == 8)
      {
        ModulestreetSmart streetSmart = ModulestreetSmart.Current;
        CycloMediaGroupLayer groupLayer = streetSmart.GetOrAddCycloMediaGroupLayer(MapView.Active);

        if (groupLayer != null)
        {
          foreach (var layer in groupLayer)
          {
            SpatialReference spatialReference = await layer.GetSpatialReferenceAsync();
            string epsgCode = $"EPSG:{spatialReference.Wkid}";
            FeatureCollection featureCollection = FeatureCollection.Load(imageId, epsgCode);

            if (featureCollection != null)
            {
              if (featureCollection.NumberOfFeatures >= 1)
              {
                foreach (Recording recording in featureCollection.FeatureMembers.Recordings)
                {
                  paneImageIdSearch.ImageInfo.Add(recording);
                }
              }
            }
          }
        }
      }
    }
  }
}
