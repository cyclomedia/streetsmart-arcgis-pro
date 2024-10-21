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
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public class CycloMediaGroupLayer : List<CycloMediaLayer>, INotifyPropertyChanged
  {
    #region Members

    private IList<CycloMediaLayer> _acceptableLayers;
    private bool _updateVisibility;
    private Envelope _initialExtent;

    #endregion

    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Properties

    private static string GroupLayerName
    {
      get
      {
        ResourceManager resourceManager = Properties.Resources.ResourceManager;
        LanguageSettings language = LanguageSettings.Instance;
        return resourceManager.GetString("RecordingLayerGroupName", language.CultureInfo);
      }
    }

    public GroupLayer GroupLayer { get; private set; }

    public MapView MapView { get; }

    public IList<CycloMediaLayer> AcceptableLayers => _acceptableLayers ??= [new RecordingLayer(this, InitialExtent)];

    public bool ContainsLayers => Count != 0;

    public Envelope InitialExtent => _initialExtent ??= MapView.Extent;

    #endregion

    #region Constructor

    public CycloMediaGroupLayer(MapView mapView)
    {
      MapView = mapView;
    }

    #endregion

    #region Functions

    public async Task InitializeAsync()
    {
      _updateVisibility = false;
      GroupLayer = null;
      Clear();
      _initialExtent = MapView.Extent;
      Map map = MapView?.Map;

      if (map != null)
      {
        var layers = map.GetLayersAsFlattenedList();
        var layersForGroupLayer = map.FindLayers(GroupLayerName);
        GroupLayer = layersForGroupLayer.OfType<GroupLayer>().FirstOrDefault();
        var layersToRemove = layersForGroupLayer.Except(GroupLayer == null ? [] : [GroupLayer]);
        await QueuedTask.Run(() =>
        {
          map.RemoveLayers(layersToRemove);
        });

        if (GroupLayer == null)
        {
          await QueuedTask.Run(() =>
          {
            GroupLayer = LayerFactory.Instance.CreateGroupLayer(map, 0, GroupLayerName);
            GroupLayer.SetExpanded(true);
          });
        }

        foreach (Layer layer in layers)
        {
          await AddAcceptableLayerAsync(layer.Name);
        }
      }

      MapMemberPropertiesChangedEvent.Subscribe(OnMapMemberPropertiesChanged);
      await CheckVisibilityLayersAsync();
    }

    public CycloMediaLayer GetLayer(Layer layer)
    {
      return this.FirstOrDefault(layerCheck => layerCheck.Layer == layer);
    }

    public async Task<CycloMediaLayer> AddAcceptableLayerAsync(string name)
    {
      if (this.Any(cycloLayer => cycloLayer.Name == name))
      {
        return null;
      }

      var thisLayer = AcceptableLayers.FirstOrDefault(checkLayer => checkLayer.Name == name);
      if (thisLayer == null)
      {
        return null;
      }

      Add(thisLayer);
      await thisLayer.AddToLayersAsync();
      // ReSharper disable once ExplicitCallerInfoArgument
      NotifyPropertyChanged(nameof(Count));
      FrameworkApplication.State.Activate("streetSmartArcGISPro_recordingLayerEnabledState");
      return thisLayer;
    }

    public async Task RemoveLayerAsync(string name, bool fromGroup)
    {
      var layer = this.FirstOrDefault(checkLayer => checkLayer.Name == name);
      if (layer != null)
      {
        Remove(layer);
        await layer.DisposeAsync(fromGroup);
        // ReSharper disable once ExplicitCallerInfoArgument
        NotifyPropertyChanged(nameof(Count));

        if (Count == 0)
        {
          FrameworkApplication.State.Deactivate("streetSmartArcGISPro_recordingLayerEnabledState");
        }
      }
    }

    public bool IsKnownName(string name)
    {
      return name == GroupLayerName || this.Any(layer => layer.Name == name) ||
#if ARCGISPRO29
        this.Any(layer => layer.FcName == name?.Substring(0, Math.Min(layer.FcName.Length, name.Length)));
#else
        this.Any(layer => layer.FcName == name?[..Math.Min(layer.FcName.Length, name.Length)]);
#endif
    }

    public async Task DisposeAsync(bool fromMap)
    {
      while (Count > 0)
      {
        await RemoveLayerAsync(this[0].Name, false);
      }

      if (fromMap)
      {
        await QueuedTask.Run(() =>
        {
          Map map = MapView?.Map;

          if (map != null && GroupLayer != null)
          {
            bool exists = false;

            foreach (Layer layer in map.Layers)
            {
              exists = layer == GroupLayer || exists;
            }

            if (exists)
            {
              map.RemoveLayer(GroupLayer);
            }
          }
        });
      }

      MapMemberPropertiesChangedEvent.Unsubscribe(OnMapMemberPropertiesChanged);
    }

    public async Task<Recording> GetRecordingAsync(string imageId)
    {
      Recording result = null;

      foreach (CycloMediaLayer layer in this)
      {
        Recording recording = await layer.GetRecordingAsync(imageId);
        result = recording ?? result;
      }

      return result;
    }

    public async Task<double?> GetHeightAsync(double x, double y)
    {
      double? result = null;
      int count = 0;

      foreach (CycloMediaLayer layer in this)
      {
        double? height = await layer.GetHeightAsync(x, y);

        if (height != null)
        {
          result = result ?? 0.0;
          result = result + height;
          count++;
        }
      }

      result = result != null ? result / Math.Max(count, 1) : null;
      return result;
    }

    private async Task CheckVisibilityLayersAsync()
    {
      if (!_updateVisibility)
      {
        _updateVisibility = true;

        foreach (var layer in AcceptableLayers)
        {
          if (!this.Any(visLayer => visLayer == layer))
          {
            await layer.SetVisibleAsync(true);
            layer.Visible = false;
          }
        }

        CycloMediaLayer changedLayer = this.FirstOrDefault(layer => layer.IsVisible && !layer.Visible);
        CycloMediaLayer refreshLayer = null;

        foreach (var layer in this)
        {
          if (layer.IsInitialized)
          {
            bool visible = (changedLayer == null || layer == changedLayer) && layer.IsVisible;
            refreshLayer = layer.IsVisible != visible ? layer : refreshLayer;
            await layer.SetVisibleAsync(visible);
            layer.Visible = layer.IsVisible;
          }
        }

        _updateVisibility = false;
      }
    }

#endregion

    #region Event handlers

    private async void OnMapMemberPropertiesChanged(MapMemberPropertiesChangedEventArgs args)
    {
      await CheckVisibilityLayersAsync();
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
