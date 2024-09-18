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

using ArcGIS.Desktop.Internal.Framework.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.File
{
  public class Project : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private string _uri;
    private ObservableCollection<Setting> _settings;

    #endregion

    #region Properties

    /// <summary>
    /// 
    /// </summary>
    [XmlAttribute("Name")]
    public string Uri
    {
      get => _uri;
      set
      {
        if (_uri != value)
        {
          _uri = value;
          OnPropertyChanged();
        }
      }
    }

    /// <summary>
    /// Recording layer coordinate system
    /// </summary>
    public ObservableCollection<Setting> Settings
    {
      get => _settings;
      set
      {
        _settings = value;
        OnPropertyChanged();
      }
    }

    #endregion

    #region Functions

    public static Project Create(string uri)
    {
      return new Project { Uri = uri };
    }

    public Setting GetSettings(string map)
    {
      if (Settings == null)
      {
        Settings = new SortableObservableCollection<Setting>();
      }

      return Settings?.FirstOrDefault(element => element.Map == map);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
