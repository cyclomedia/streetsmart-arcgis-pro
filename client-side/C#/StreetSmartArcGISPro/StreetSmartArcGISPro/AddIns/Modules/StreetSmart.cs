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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Overlays;
using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;
using StreetSmartArcGISPro.VectorLayers;

using Project = ArcGIS.Desktop.Core.Project;
using DockPaneStreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using StreetSmartAPI = StreetSmart.Common.Interfaces.API.IStreetSmartAPI;
using System.Windows;
using ArcGIS.Desktop.Core.Events;

namespace StreetSmartArcGISPro.AddIns.Modules
{
    internal class StreetSmart : Module
    {
        #region Members

        private static StreetSmart _streetSmart;
        private static LanguageSettings _langSettings;

        private Dictionary<MapView, CycloMediaGroupLayer> _cycloMediaGroupLayer;
        private VectorLayerList _vectorLayerList;
        private ViewerList _viewerList;
        private MeasurementList _measurementList;
        private readonly Agreement _agreement;
        private ResourceManager _resourceManager;

        #endregion

        #region Properties

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static StreetSmart Current
          =>
            _streetSmart ??
            (_streetSmart = (StreetSmart)FrameworkApplication.FindModule($"streetSmartArcGISPro_module_{_langSettings.Locale}"));

        public Dictionary<MapView, CycloMediaGroupLayer> CycloMediaGroupLayer =>
          _cycloMediaGroupLayer ?? (_cycloMediaGroupLayer = new Dictionary<MapView, CycloMediaGroupLayer>());

        private string GroupLayerName => _resourceManager.GetString("RecordingLayerGroupName", _langSettings.CultureInfo);

        public StreetSmartAPI Api { get; set; }
        public CycloMediaGroupLayer GetCycloMediaGroupLayer(MapView mapView)
        {
            CycloMediaGroupLayer result = null;

            if (mapView != null)
            {
                if (CycloMediaGroupLayer.ContainsKey(mapView))
                {
                    result = CycloMediaGroupLayer[mapView];
                }
                else
                {
                    result = new CycloMediaGroupLayer(mapView);
                    result.PropertyChanged += OnLayerPropertyChanged;
                    CycloMediaGroupLayer.Add(mapView, result);
                }
            }

            return result;
        }

        public CycloMediaGroupLayer GetCycloMediaGroupLayer(Map map)
        {
            CycloMediaGroupLayer result = null;

            if (map != null)
            {
                foreach (var cycloMediaGroupLayer in CycloMediaGroupLayer.Values)
                {
                    result = cycloMediaGroupLayer.MapView.Map == map ? cycloMediaGroupLayer : result;
                }
            }

            return result;
        }

        public ViewerList ViewerList => _viewerList ?? (_viewerList = new ViewerList());

        public MeasurementList MeasurementList => _measurementList ?? (_measurementList = new MeasurementList());

        #endregion

        #region Constructor

        public StreetSmart()
        {
            _langSettings = LanguageSettings.Instance;
            _agreement = Agreement.Instance;
            _resourceManager = Properties.Resources.ResourceManager;

            if (_agreement.Value)
            {
                FrameworkApplication.State.Activate("streetSmartArcGISPro_agreementAcceptedState");
            }

            var splitId = ID.Split('_');
            string langId = splitId.Length == 0 ? string.Empty : splitId[splitId.Length - 1];
            Language language = Languages.Instance.Get(langId);

            if (language != null)
            {
                _langSettings.Language = language;
                _langSettings.Save();
            }
            else
            {
                _langSettings.Language = Languages.Instance.Get("en-GB");
            }

            MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
            MapClosedEvent.Subscribe(OnMapClosedDocument);
            ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
            ProjectOpenedEvent.Subscribe(OnProjectOpenedEvent);
            ApplicationStartupEvent.Subscribe(OnApplicationStartupEvent);
        }

        private async void OnProjectOpenedEvent(ProjectEventArgs args)
        {
            if (Login.Instance.IsOAuth)
            {
                Login.Instance.Bearer = await Api.GetBearerToken();
            }
            Login.Instance.Save();
        }
        private void OnApplicationStartupEvent(EventArgs args)
        {
            if (Login.Instance.IsOAuth)
            {
                DockPaneStreetSmart streetSmart = DockPaneStreetSmart.ActivateStreet();
            }
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            return true;
        }

        protected override async void Uninitialize()
        {
            foreach (var layer in CycloMediaGroupLayer)
            {
                await RemoveLayersAsync(true, layer.Key);
            }

            MapViewInitializedEvent.Unsubscribe(OnMapViewInitialized);
            MapClosedEvent.Unsubscribe(OnMapClosedDocument);
            base.Uninitialize();
        }

        #endregion

        #region Functions

        internal bool InsideScale(MapView mapView)
        {
            //GC: added a new catch statement for mapView when opening attribute table while editing
            if (mapView != null)
            {
                return GetCycloMediaGroupLayer(mapView).InsideScale;
            }
            else
            {
                return false;
            }
        }

        private bool ContainsCycloMediaLayer(MapView mapView)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
            return mapView?.Map?.Layers.Aggregate(false, (current, layer) =>
                       (cycloMediaGroupLayer?.IsKnownName(layer.Name) ??
                        layer.Name == GroupLayerName) || current) ?? false;
        }

        private bool ContainsCycloMediaLayer(Map map)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(map);
            return map?.Layers.Aggregate(false, (current, layer) =>
                       (cycloMediaGroupLayer?.IsKnownName(layer.Name) ??
                        layer.Name == GroupLayerName) || current) ?? false;
        }

        private async Task CloseCycloMediaLayerAsync(bool closeMap, MapView mapView)
        {
            if (!ContainsCycloMediaLayer(mapView) || closeMap)
            {
                await RemoveLayersAsync(false, mapView);
            }

            if (closeMap)
            {
                CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);

                if (cycloMediaGroupLayer != null)
                {
                    cycloMediaGroupLayer.PropertyChanged -= OnLayerPropertyChanged;
                    CycloMediaGroupLayer.Remove(mapView);
                }

                if (CycloMediaGroupLayer.Count == 0)
                {
                    Setting settings = ProjectList.Instance.GetSettings(mapView);
                    Login login = Login.Instance;
                    LayersRemovedEvent.Unsubscribe(OnLayerRemoved);

                    if (settings != null)
                    {
                        settings.PropertyChanged -= OnSettingsPropertyChanged;
                    }

                    login.PropertyChanged -= OnLoginPropertyChanged;
                }
            }
        }

        private async Task CloseCycloMediaLayerAsync(Map map)
        {
            if (!ContainsCycloMediaLayer(map))
            {
                await RemoveLayersAsync(map);
            }
        }

        public async Task<VectorLayerList> GetVectorLayerListAsync(MapView mapView)
        {
            if (_vectorLayerList == null)
            {
                _vectorLayerList = new VectorLayerList();
            }

            if (mapView != null && !_vectorLayerList.ContainsKey(mapView))
            {
                await _vectorLayerList.DetectVectorLayersAsync(mapView);
            }

            return _vectorLayerList;
        }

        public async Task<VectorLayerList> GetVectorLayerListAsync()
        {
            return await GetVectorLayerListAsync(MapView.Active);
        }

        public async Task AddLayersAsync(MapView mapView)
        {
            await AddLayersAsync(null, mapView);
        }

        public async Task AddLayersAsync(string name, MapView mapView)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);

            if (cycloMediaGroupLayer.Count == 0)
            {
                await cycloMediaGroupLayer.InitializeAsync();
            }

            if (!string.IsNullOrEmpty(name))
            {
                await cycloMediaGroupLayer.AddLayerAsync(name);
            }
        }

        public async Task RemoveLayerAsync(string name, MapView mapView)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);
            await cycloMediaGroupLayer.RemoveLayerAsync(name, true);
        }

        public async Task RemoveLayersAsync(bool fromMap, MapView mapView)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(mapView);

            if (cycloMediaGroupLayer != null)
            {
                await cycloMediaGroupLayer.DisposeAsync(fromMap);
            }
        }

        public async Task RemoveLayersAsync(Map map)
        {
            CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(map);

            if (cycloMediaGroupLayer != null)
            {
                await cycloMediaGroupLayer.DisposeAsync(false);
            }
        }

        #endregion

        #region Event handlers

        private async void OnMapViewInitialized(MapViewEventArgs args)
        {
            CycloMediaGroupLayer cycloMediaLayer = GetCycloMediaGroupLayer(args.MapView);

            if (cycloMediaLayer != null)
            {
                foreach (var layer in cycloMediaLayer)
                {
                    CycloMediaLayer.ResetYears(layer.Layer);
                }
            }

            bool addEvents = CycloMediaGroupLayer.Count == 0 ||
                             CycloMediaGroupLayer.Count == 1 && CycloMediaGroupLayer.ContainsKey(args.MapView);

            if (addEvents)
            {
                LayersRemovedEvent.Subscribe(OnLayerRemoved);
            }

            if (ContainsCycloMediaLayer(args.MapView))
            {
                await AddLayersAsync(args.MapView);
            }

            Setting settings = ProjectList.Instance.GetSettings(args.MapView);

            if (settings != null)
            {
                settings.PropertyChanged += OnSettingsPropertyChanged;
            }

            if (addEvents)
            {
                Login login = Login.Instance;
                login.PropertyChanged += OnLoginPropertyChanged;
            }

            if (settings.CycloramaViewerCoordinateSystem != null)
            {
                await CoordSystemUtils.CheckInAreaCycloramaSpatialReferenceAsync(args.MapView);
            }

            if (!_agreement.Value)
            {
                PropertySheet.ShowDialog("streetSmartArcGISPro_optionsPropertySheet", "streetSmartArcGISPro_agreementPage");
            }
        }

        private async void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "RecordingLayerCoordinateSystem")
            {
                CycloMediaGroupLayer cycloMediaGroupLayer = GetCycloMediaGroupLayer(MapView.Active);

                foreach (var layer in cycloMediaGroupLayer)
                {
                    await layer.UpdateLayerAsync();
                }
            }
        }

        private async void OnLoginPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Credentials")
            {
                Login login = Login.Instance;

                foreach (CycloMediaGroupLayer cycloMediaGroupLayer in CycloMediaGroupLayer.Values)
                {
                    foreach (var layer in cycloMediaGroupLayer)
                    {
                        if (login.Credentials)
                        {
                            await layer.RefreshAsync();
                        }
                        else
                        {
                            await layer.MakeEmptyAsync();
                            Project project = Project.Current;
                            await project.SaveEditsAsync();
                        }
                    }
                }
            }
        }

        private async void OnMapClosedDocument(MapClosedEventArgs args)
        {
            MapView mapView = args.MapPane.MapView;
            await CloseCycloMediaLayerAsync(true, mapView);
        }

        private async void OnLayerRemoved(LayerEventsArgs args)
        {
            var layers = args.Layers;

            foreach (var layer in layers)
            {
                await CloseCycloMediaLayerAsync(layer.Map);
            }
        }

        private async void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Count" && sender is CycloMediaGroupLayer cycloMediaGroupLayer)
            {
                if (!cycloMediaGroupLayer.ContainsLayers)
                {
                    await RemoveLayersAsync(true, cycloMediaGroupLayer.MapView);
                }
            }
        }

        public void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            if (args.IncomingView != null)
            {
                CycloMediaGroupLayer groupLayer = GetCycloMediaGroupLayer(args.IncomingView);

                if (groupLayer.ContainsLayers)
                {
                    FrameworkApplication.State.Activate("streetSmartArcGISPro_recordingLayerEnabledState");
                }
                else
                {
                    FrameworkApplication.State.Deactivate("streetSmartArcGISPro_recordingLayerEnabledState");
                }
            }
        }

        #endregion
    }
}
