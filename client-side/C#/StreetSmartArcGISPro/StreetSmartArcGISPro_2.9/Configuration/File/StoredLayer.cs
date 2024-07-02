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

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreetSmartArcGISPro.Configuration.File
{
  public class StoredLayer : INotifyPropertyChanged
  {
    #region Events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private string _name;
    private bool _visible;

    #endregion

    #region Properties

    public string Name
    {
      get => _name;
      set
      {
        if (_name != value)
        {
          _name = value;
          OnPropertyChanged();
        }
      }
    }

    public bool Visible
    {
      get => _visible;
      set
      {
        if (_visible != value)
        {
          _visible = value;
          OnPropertyChanged();
        }
      }
    }

    #endregion

    #region Functions

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
