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

using ArcGIS.Desktop.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.SpatialReference
{
  [XmlType(AnonymousType = true, Namespace = "https://www.globespotter.com/gsc")]
  [XmlRoot("SpatialReferences", Namespace = "https://www.globespotter.com/gsc", IsNullable = false)]
  public class SpatialReferenceList : List<SpatialReference>
  {
    #region Members

    private static readonly XmlSerializer XmlSpatialReferenceList;
    private static readonly Web Web;

    private static SpatialReferenceList _spatialReferenceList;

    #endregion

    #region Constructor

    static SpatialReferenceList()
    {
      XmlSpatialReferenceList = new XmlSerializer(typeof(SpatialReferenceList));
      Web = Web.Instance;
    }

    #endregion

    #region properties

    public SpatialReference[] SpatialReference
    {
      get => ToArray();
      set
      {
        if (value != null)
        {
          AddRange(value);
        }
      }
    }

    public static SpatialReferenceList Instance
    {
      get
      {
        if (_spatialReferenceList == null || _spatialReferenceList.Count == 0)
        {
          try
          {
            Load();
          }
          catch (Exception e)
          {
            EventLog.Write(EventLog.EventType.Error, $"Street Smart: (SpatialReferenceList.cs) (Instance) error: {e}");
          }
        }

        return _spatialReferenceList ??= [];
      }
    }

    #endregion

    #region Functions

    public SpatialReference GetItem(string srsName)
    {
      return this.FirstOrDefault(spatialReference => spatialReference.SRSName == srsName);
    }

    public SpatialReference GetCompatibleSrsNameItem(string srsName)
    {
      return this.FirstOrDefault(spatialReference => spatialReference.CompatibleSRSNames == srsName);
    }

    public string ToKnownSrsName(string srsName)
    {
      SpatialReference spatRef = GetCompatibleSrsNameItem(srsName);
      return (spatRef == null) ? srsName : spatRef.SRSName;
    }

    public static SpatialReferenceList Load()
    {
      try
      {
        Stream spatialRef = Web.SpatialReferences();

        if (spatialRef != null)
        {
          spatialRef.Position = 0;
          _spatialReferenceList = (SpatialReferenceList)XmlSpatialReferenceList.Deserialize(spatialRef);
          spatialRef.Close();
        }
      }
      catch (Exception e)
      {
        EventLog.Write(EventLog.EventType.Error, $"Street Smart: (SpatialReferenceList.cs) (Load) error: {e}");
      }

      return _spatialReferenceList;
    }

    #endregion
  }
}
