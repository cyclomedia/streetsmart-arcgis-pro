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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using StreetSmartArcGISPro.Overlays.Measurement;
using StreetSmartArcGISPro.Utilities;

using Image = System.Windows.Controls.Image;

using static StreetSmartArcGISPro.Properties.Resources;

namespace StreetSmartArcGISPro.AddIns.DockPanes
{
  internal class MeasurementDetail : DockPane, INotifyPropertyChanged
  {
    #region Constants

    private const string DockPaneId = "streetSmartArcGISPro_MeasurementDetail";

    #endregion

    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private MeasurementPoint _measurementPoint;
    private MeasurementObservation _selectedObservation;

    #endregion

    #region Icons

    public Image SystemSearch => new Image { Source = SystemSearch16.ToBitmapSource() };

    public Image FocusMode => new Image { Source = FocusMode16.ToBitmapSource() };

    public Image UserTrash => new Image { Source = UserTrash16.ToBitmapSource() };

    #endregion

    #region Constructor

    protected MeasurementDetail() { }

    #endregion

    #region Properties

    public MeasurementObservation SelectedObservation
    {
      get => _selectedObservation;
      set
      {
        _selectedObservation = value;
        OnPropertyChanged();
      }
    }

    public MeasurementPoint MeasurementPoint
    {
      get => _measurementPoint;
      set
      {
        if (_measurementPoint != value)
        {
          if (value != null && !IsVisible)
          {
            Activate();
          }

          _measurementPoint = value;
          OnPropertyChanged();

          if (_measurementPoint == null)
          {
            SelectedObservation = null;
          }
        }
      }
    }

    #endregion

    #region Functions

    internal static MeasurementDetail Get()
    {
      return FrameworkApplication.DockPaneManager.Find(DockPaneId) as MeasurementDetail;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
