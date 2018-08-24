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
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File.Layers
{
  [XmlRoot("StoredLayers")]
  public class StoredLayerList : List<StoredLayer>
  {
    #region Members

    private static readonly XmlSerializer XmlStoredLayerList;

    private static StoredLayerList _storedLayerList;

    #endregion

    #region Constructor

    static StoredLayerList()
    {
      XmlStoredLayerList = new XmlSerializer(typeof (StoredLayerList));
    }

    #endregion

    #region Properties

    public StoredLayer[] StoredLayer
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

    public static StoredLayerList Instance
    {
      get
      {
        if (_storedLayerList == null || _storedLayerList.Count == 0)
        {
          try
          {
            Load();
          }
          // ReSharper disable once EmptyGeneralCatchClause
          catch
          {
          }
        }

        return _storedLayerList ?? (_storedLayerList = Create());
      }
    }

    public static string FileName => Path.Combine(FileUtils.FileDir, "StoredLayers.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlStoredLayerList.Serialize(streamFile, this);
      streamFile.Close();
    }

    public StoredLayer GetLayer(string name)
    {
      return this.Aggregate<StoredLayer, StoredLayer>
        (null, (current, storedLayer) => (storedLayer.Name == name) ? storedLayer : current);
    }

    public bool Get(string name)
    {
      StoredLayer storedLayer = GetLayer(name);
      return storedLayer != null && storedLayer.Visible;
    }

    public void Update(string name, bool visible)
    {
      StoredLayer storedLayer = GetLayer(name);

      if (storedLayer == null)
      {
        storedLayer = new StoredLayer {Name = name, Visible = visible};
        Add(storedLayer);
      }
      else
      {
        storedLayer.Visible = visible;
      }

      Save();
    }

    public static StoredLayerList Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _storedLayerList = (StoredLayerList) XmlStoredLayerList.Deserialize(streamFile);
        streamFile.Close();
      }

      return _storedLayerList;
    }

    private static StoredLayerList Create()
    {
      var result = new StoredLayerList();
      result.Save();
      return result;
    }

    #endregion
  }
}
