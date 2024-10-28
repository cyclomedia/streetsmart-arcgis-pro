/*
 * Integration in ArcMap for Cycloramas
 * Copyright (c) 2015 - 2018, CycloMedia, All rights reserved.
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

using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SystemIOFile = System.IO.File;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("StoredLayers")]
  public class StoredLayerList : ObservableCollection<StoredLayer>
  {
    #region Members

    private static readonly XmlSerializer XmlStoredLayerList;

    private static StoredLayerList _storedLayerList;

    #endregion

    #region Constructor

    static StoredLayerList()
    {
      XmlStoredLayerList = new XmlSerializer(typeof(StoredLayerList));
    }

    #endregion

    #region Properties

    public static StoredLayerList Instance
    {
      get
      {
        if (_storedLayerList == null)
        {
          try
          {
            Load();
          }
          catch (Exception e)
          {
            EventLog.Write(EventLogLevel.Error, $"Street Smart: (StoredLayerList.cs) (Instance) error: {e}");
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

    public static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _storedLayerList = (StoredLayerList)XmlStoredLayerList.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    private static StoredLayerList Create()
    {
      var result = new StoredLayerList();
      result.Save();
      return result;
    }

    public StoredLayer GetLayer(string name)
    {
      return this.FirstOrDefault(storedLayer => storedLayer.Name == name);
    }

    public bool GetVisibility(string name)
    {
      StoredLayer storedLayer = GetLayer(name);
      return storedLayer != null && storedLayer.Visible;
    }

    public void Update(string name, bool visible)
    {
      StoredLayer storedLayer = GetLayer(name);

      if (storedLayer == null)
      {
        storedLayer = new StoredLayer { Name = name, Visible = visible };
        Add(storedLayer);
      }
      else
      {
        storedLayer.Visible = visible;
      }

      Save();
    }

    #endregion
  }
}
