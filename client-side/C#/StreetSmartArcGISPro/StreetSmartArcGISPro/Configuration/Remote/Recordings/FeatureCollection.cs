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
using StreetSmartArcGISPro.Logging;
using System;
using System.IO;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.Recordings
{
  [XmlType(AnonymousType = true, Namespace = "http://www.opengis.net/wfs")]
  [XmlRoot(Namespace = "http://www.opengis.net/wfs", IsNullable = false)]
  public class FeatureCollection
  {
    #region Members

    private static readonly XmlSerializer XmlFeatureCollection;
    private static readonly Web Web;

    #endregion

    #region Constructor

    static FeatureCollection()
    {
      XmlFeatureCollection = new XmlSerializer(typeof(FeatureCollection));
      Web = Web.Instance;
    }

    #endregion

    #region Properties

    [XmlAttribute("numberOfFeatures", Namespace = "http://www.opengis.net/wfs")]
    public int NumberOfFeatures { get; set; }

    [XmlAttribute("timeStamp", Namespace = "http://www.opengis.net/wfs")]
    public DateTime TimeStamp { get; set; }

    [XmlAttribute("schemaLocation", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
    public string SchemaLocation { get; set; }

    [XmlElement("featureMembers", Namespace = "http://www.opengis.net/gml")]
    public FeatureMembers FeatureMembers { get; set; }

    #endregion

    #region Functions

    public static FeatureCollection Load(Envelope envelope, string wfsRequest)
    {
      FeatureCollection features = null;

      if (envelope != null && !string.IsNullOrEmpty(wfsRequest))
      {
        try
        {
          Stream featuresStream = Web.GetByBbox(envelope, wfsRequest);

          if (featuresStream != null)
          {
            featuresStream.Position = 0;
            features = (FeatureCollection)XmlFeatureCollection.Deserialize(featuresStream);
            EventLog.Write(EventLogLevel.Information, $"Street Smart: (FeatureCollection) (Load (Envelope)) Loaded features: {features?.NumberOfFeatures ?? 0}");
            featuresStream.Close();
          }
        }
        catch (Exception e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (FeatureCollection.cs) (Load) error: {e}");
        }
      }

      return features;
    }

    public static FeatureCollection Load(string imageId, string epsgCode)
    {
      FeatureCollection features = null;

      if (!string.IsNullOrEmpty(imageId) && !string.IsNullOrEmpty(epsgCode))
      {
        try
        {
          Stream featuresStream = Web.GetByImageId(imageId, epsgCode);

          if (featuresStream != null)
          {
            featuresStream.Position = 0;
            features = (FeatureCollection)XmlFeatureCollection.Deserialize(featuresStream);
            EventLog.Write(EventLogLevel.Information, $"Street Smart: (FeatureCollection.cs) (Load (ImageId)) Loaded features: {features?.NumberOfFeatures ?? 0}");
            featuresStream.Close();
          }
        }
        catch (Exception e)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (FeatureCollection.cs) (Load) error: {e}");
        }
      }

      return features;
    }

    #endregion
  }
}
