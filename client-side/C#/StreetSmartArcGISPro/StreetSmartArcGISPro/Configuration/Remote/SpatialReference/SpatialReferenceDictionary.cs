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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.SpatialReference
{
  public sealed class SpatialReferenceDictionary : KeyedCollection<string, SpatialReference>
  {
    private static SpatialReferenceDictionary _spatialReferences;

    private SpatialReferenceDictionary()
    {
    }

    #region properties

    public SpatialReference[] SpatialReference
    {
      get => this.ToArray();
      set
      {
        if (value != null)
        {
          foreach (var item in value)
          {
            this.Add(item);
          }
        }
      }
    }

    public static SpatialReferenceDictionary Instance
    {
      get
      {
        if (_spatialReferences == null || _spatialReferences.Count == 0)
        {
          try
          {
            var spatialReferences = SpatialReferenceList.Load();
            _spatialReferences = new SpatialReferenceDictionary();
            foreach (var item in spatialReferences)
            {
              if (_spatialReferences.Contains(item.SRSName))
              {
                EventLog.Write(EventLog.EventType.Error, $"Street Smart: (SpatialReferenceList.cs) (Instance) An item with the same key has already been added. Key: {item.SRSName}");
                continue; 
              }
              _spatialReferences.Add(item);
            }
          }
          catch (Exception e)
          {
            EventLog.Write(EventLog.EventType.Error, $"Street Smart: (SpatialReferenceList.cs) (Instance) error: {e}");
          }
        }

        return _spatialReferences ??= [];
      }
    }

    #endregion

    #region Functions

    public SpatialReference GetItem(string srsName)
    {
      return this.TryGetValue(srsName, out var result) ? result : null;
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

    protected override string GetKeyForItem(SpatialReference item)
    {
      return item.SRSName;
    }

    #endregion

    [XmlType(AnonymousType = true, Namespace = "https://www.globespotter.com/gsc")]
    [XmlRoot("SpatialReferences", Namespace = "https://www.globespotter.com/gsc", IsNullable = false)]
    public sealed class SpatialReferenceList : List<SpatialReference>
    {
      #region Members

      private static readonly XmlSerializer XmlSpatialReferenceList;
      private static readonly Web Web;

      #endregion

      #region Constructor

      static SpatialReferenceList()
      {
        XmlSpatialReferenceList = new XmlSerializer(typeof(SpatialReferenceList));
        Web = Web.Instance;
      }

      #endregion

      public static SpatialReferenceList Load()
      {
        Stream spatialRef = null;
        try
        {
          spatialRef = Web.SpatialReferences();
          if (spatialRef != null)
          {
            spatialRef.Position = 0;
            return (SpatialReferenceList)XmlSpatialReferenceList.Deserialize(spatialRef);
          }
        }
        catch (Exception e)
        {
          EventLog.Write(EventLog.EventType.Error, $"Street Smart: (SpatialReferenceList.cs) (Load) error: {e}");
        }
        finally
        {
          spatialRef?.Close();
        }

        return null;
      }
    }
  }
}
