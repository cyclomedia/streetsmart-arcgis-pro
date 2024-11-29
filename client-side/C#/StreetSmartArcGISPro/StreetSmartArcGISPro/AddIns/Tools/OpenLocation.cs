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
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using StreetSmartArcGISPro.CycloMediaLayers;
using StreetSmartArcGISPro.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DockPaneStreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ModuleStreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using MySpatialReference = StreetSmartArcGISPro.Configuration.Remote.SpatialReference.SpatialReference;
using ThisResources = StreetSmartArcGISPro.Properties.Resources;
using WinPoint = System.Windows.Point;

namespace StreetSmartArcGISPro.AddIns.Tools
{
  class OpenLocation : MapTool
  {
    #region Members

    private readonly LanguageSettings _languageSettings;

    #endregion

    #region Constructor

    public OpenLocation()
    {
      Type thisType = GetType();
      string cursorPath = $@"StreetSmartArcGISPro.Images.{thisType.Name}.cur";
      Assembly thisAssembly = Assembly.GetAssembly(thisType);
      Stream cursorStream = thisAssembly.GetManifestResourceStream(cursorPath);

      _languageSettings = LanguageSettings.Instance;

      if (cursorStream != null)
      {
        Cursor = new Cursor(cursorStream);
      }

      IsSketchTool = true;
      SketchType = SketchGeometryType.Point;
    }

    #endregion

    #region Overrides

    protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
      bool nearest = false;
      string location = string.Empty;
      MapView activeView = MapView.Active;

      if (activeView.Map?.MapType == ArcGIS.Core.CIM.MapType.Scene)
      {
        ResourceManager res = ThisResources.ResourceManager;
        string openLocationNotSupportedInSceneTxt = res.GetString("OpenLocationNotSupportedInScene", _languageSettings.CultureInfo);
        MessageBox.Show(
          openLocationNotSupportedInSceneTxt,
          openLocationNotSupportedInSceneTxt,
          MessageBoxButton.OK,
          MessageBoxImage.Error);

        return false;
      }

      await QueuedTask.Run(async () =>
      {
        if (geometry is MapPoint point && activeView != null)
        {
          var constants = ConstantsRecordingLayer.Instance;
          double size = constants.SizeLayer;
          double halfSize = size / 2;

          SpatialReference pointSpatialReference = point.SpatialReference;
          var pointScreen = activeView.MapToScreen(point);
          double x = pointScreen.X;
          double y = pointScreen.Y;
          WinPoint pointScreenMin = new WinPoint(x - halfSize, y - halfSize);
          WinPoint pointScreenMax = new WinPoint(x + halfSize, y + halfSize);
          var pointMapMin = activeView.ScreenToMap(pointScreenMin);
          var pointMapMax = activeView.ScreenToMap(pointScreenMax);

          Envelope envelope = EnvelopeBuilderEx.CreateEnvelope(pointMapMin, pointMapMax, pointSpatialReference);
          var features = activeView.GetFeatures(envelope);

          ModuleStreetSmart streetSmart = ModuleStreetSmart.Current;
          CycloMediaGroupLayer groupLayer = streetSmart?.GetOrAddCycloMediaGroupLayer(activeView);

          if (features != null && groupLayer != null)
          {
            nearest = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (nearest)
            {
              Setting settings = ProjectList.Instance.GetSettings(activeView);
              MySpatialReference cycloCoordSystem = settings.CycloramaViewerCoordinateSystem;

              if (cycloCoordSystem != null)
              {
                SpatialReference cycloSpatialReference = cycloCoordSystem.ArcGisSpatialReference ?? await cycloCoordSystem.CreateArcGisSpatialReferenceAsync();

                if (pointSpatialReference.Wkid != cycloSpatialReference.Wkid)
                {
                  ProjectionTransformation projection = ProjectionTransformation.Create(pointSpatialReference, cycloSpatialReference);
                  point = GeometryEngine.Instance.ProjectEx(point, projection) as MapPoint;
                }

                if (point != null)
                {
                  CultureInfo ci = CultureInfo.InvariantCulture;
                  location = string.Format(ci, "{0},{1}", point.X, point.Y);

                  if (!streetSmart.InsideScale(activeView))
                  {
                    double minimumScale = ConstantsRecordingLayer.Instance.MinimumScale;
                    double scale = minimumScale / 2;
                    Camera camera = new Camera(point.X, point.Y, scale, 0.0);
                    activeView.ZoomTo(camera);
                  }
                }
              }
            }
            else
            {
#if ARCGISPRO29
              foreach (var feature in features)
#else
              foreach (var feature in features.ToDictionary())
#endif
              {
                if (feature.Key is Layer layer)
                {
                  CycloMediaLayer cycloMediaLayer = groupLayer.GetLayer(layer);

                  if (cycloMediaLayer != null)
                  {
                    foreach (long uid in feature.Value)
                    {
                      Recording recording = await cycloMediaLayer.GetRecordingAsync(uid);

                      if (recording.IsAuthorized == null || (bool)recording.IsAuthorized)
                      {
                        location = recording.ImageId;
                      }
                    }
                  }
                }
                else
                {
                  EventLog.Write(EventLogLevel.Warning, $"A feature is not a layer, should we cover this? Its: {feature.Key}");
                }
              }
            }
          }
        }
      });

      if (!string.IsNullOrEmpty(location))
      {
        bool replace = !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
        DockPaneStreetSmart streetSmart = DockPaneStreetSmart.Show();

        if (streetSmart != null)
        {
          EventLog.Write(EventLogLevel.Information, $"Street Smart: (OpenLocation.cs) (OnSketchCompleteAsync) Open Street Smart location: {location}");
          streetSmart.MapView = activeView;
          streetSmart.LookAt = null;
          streetSmart.Replace = replace;
          streetSmart.Nearest = nearest;
          streetSmart.Location = location;
        }
      }

      return true;
    }

    #endregion
  }
}
