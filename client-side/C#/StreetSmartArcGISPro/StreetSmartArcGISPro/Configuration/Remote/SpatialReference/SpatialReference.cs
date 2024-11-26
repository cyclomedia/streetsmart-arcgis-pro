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
using StreetSmartArcGISPro.Logging;
using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ArcGISSpatialReference = ArcGIS.Core.Geometry.SpatialReference;

namespace StreetSmartArcGISPro.Configuration.Remote.SpatialReference
{
  public class SpatialReference
  {
    #region Properties

    // ReSharper disable InconsistentNaming
    public string Name { get; set; }

    public string SRSName { get; set; }

    public string Units { get; set; }

    public Bounds NativeBounds { get; set; }

    public string ESRICompatibleName { get; set; }

    public string CompatibleSRSNames { get; set; }
    // ReSharper restore InconsistentNaming

    [XmlIgnore]
    public bool CanMeasuring => Units == "m" || Units == "ft";

    [XmlIgnore]
    public ArcGISSpatialReference ArcGisSpatialReference { get; private set; }

    #endregion

    #region Functions

    private void CreateSpatialReference()
    {
      if (string.IsNullOrEmpty(SRSName))
      {
        ArcGisSpatialReference = null;
      }
      else
      {
        string strsrs = SRSName.Replace("EPSG:", string.Empty);

        if (int.TryParse(strsrs, out var srs))
        {
          try
          {
            ArcGisSpatialReference = SpatialReferenceBuilder.CreateSpatialReference(srs);
          }
          catch (ArgumentException)
          {
            if (string.IsNullOrEmpty(CompatibleSRSNames))
            {
              ArcGisSpatialReference = null;
            }
            else
            {
              strsrs = CompatibleSRSNames.Replace("EPSG:", string.Empty);

              if (int.TryParse(strsrs, out srs))
              {
                try
                {
                  ArcGisSpatialReference = SpatialReferenceBuilder.CreateSpatialReference(srs);
                }
                catch (ArgumentException)
                {
                  ArcGisSpatialReference = null;
                }
              }
              else
              {
                ArcGisSpatialReference = null;
              }
            }
          }
        }
        else
        {
          ArcGisSpatialReference = null;
        }
      }
    }

    public async Task<ArcGISSpatialReference> CreateArcGisSpatialReferenceAsync()
    {
      await QueuedTask.Run(() =>
      {
        CreateSpatialReference();
      });

      return ArcGisSpatialReference;
    }

    /// <summary>
    /// asynchronous function to request or this spatial reference exists in the current area
    /// </summary>
    public async Task<bool> ExistsInAreaAsync()
    {
      await QueuedTask.Run(() =>
      {
        if (ArcGisSpatialReference == null)
        {
          CreateSpatialReference();
        }

        if (ArcGisSpatialReference != null)
        {
          MapView activeView = MapView.Active;
          Envelope envelope = activeView?.Extent;

          if (envelope != null)
          {
            ArcGISSpatialReference spatEnv = envelope.SpatialReference;
            int spatEnvFactoryCode = spatEnv?.Wkid ?? 0;

            if (spatEnv != null && spatEnvFactoryCode != ArcGisSpatialReference.Wkid)
            {
              try
              {
                ProjectionTransformation projection = ProjectionTransformation.Create(envelope.SpatialReference,
                  ArcGisSpatialReference);

                if (!(GeometryEngine.Instance.ProjectEx(envelope, projection) is Envelope copyEnvelope) || copyEnvelope.IsEmpty)
                {
                  ArcGisSpatialReference = null;
                }
                else
                {
                  if (NativeBounds != null)
                  {
                    double xMin = NativeBounds.MinX;
                    double yMin = NativeBounds.MinY;
                    double xMax = NativeBounds.MaxX;
                    double yMax = NativeBounds.MaxY;

                    if (copyEnvelope.XMin < xMin || copyEnvelope.XMax > xMax || copyEnvelope.YMin < yMin ||
                        copyEnvelope.YMax > yMax)
                    {
                      ArcGisSpatialReference = null;
                    }
                  }
                }
              }
              catch (ArgumentException)
              {
                ArcGisSpatialReference = null;
              }
              catch (Exception ex)
              {
                EventLog.Write(EventLogLevel.Error, $"Street Smart: (SpatialReference.cs) (ExistsInAreaAsync) {ex}");
              }
            }
          }
        }
      });

      return ArcGisSpatialReference != null;
    }

    #endregion

    #region Overrides

    public override string ToString()
    {
      return $"{(string.IsNullOrEmpty(ESRICompatibleName) ? Name : ESRICompatibleName)} ({SRSName})";
    }

    #endregion
  }
}
