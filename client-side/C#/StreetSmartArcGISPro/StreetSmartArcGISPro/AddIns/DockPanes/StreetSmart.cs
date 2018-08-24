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

using System.ComponentModel;
using System.Runtime.CompilerServices;

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using GlobeSpotterAPI;

namespace StreetSmartArcGISPro.AddIns.DockPanes
{
  internal class StreetSmart : DockPane, INotifyPropertyChanged
  {
    #region Constants

    private const string DockPaneId = "StreetSmartArcGISPro_streetSmartDockPane";

    #endregion

    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Constructor

    protected StreetSmart()
    {
      ProjectClosedEvent.Subscribe(OnProjectClosed);
    }

    #endregion

    #region Members

    private string _location;
    private bool _isActive;
    private bool _replace;
    private bool _nearest;
    private Point3D _lookAt;

    #endregion

    #region Properties

    public string Location
    {
      get => _location;
      set
      {
        if (_location != value)
        {
          _location = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool IsActive
    {
      get => _isActive;
      set
      {
        if (_isActive != value)
        {
          _isActive = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool Replace
    {
      get => _replace;
      set
      {
        if (_replace != value)
        {
          _replace = value;
          NotifyPropertyChanged();
        }
      }
    }

    public bool Nearest
    {
      get => _nearest;
      set
      {
        if (_nearest != value)
        {
          _nearest = value;
          NotifyPropertyChanged();
        }
      }
    }

    public Point3D LookAt
    {
      get => _lookAt;
      set
      {
        _lookAt = value;
        NotifyPropertyChanged();
      }
    }

    #endregion

    #region Overrides

    protected override void OnActivate(bool isActive)
    {
      IsActive = isActive || _isActive;
      base.OnActivate(isActive);
    }

    protected override void OnHidden()
    {
      IsActive = false;
      _location = string.Empty;
      _replace = false;
      _nearest = false;
      base.OnHidden();
    }

    #endregion

    #region Functions

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal static StreetSmart Show()
    {
      StreetSmart streetSmart = FrameworkApplication.DockPaneManager.Find(DockPaneId) as StreetSmart;

      if (!(streetSmart?.IsVisible ?? true))
      {
        streetSmart.Activate();
      }

      return streetSmart;
    }

    #endregion

    #region Event handlers

    private void OnProjectClosed(ProjectEventArgs args)
    {
      Hide();
    }

    #endregion
  }
}
