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

using System.Collections.Generic;
using System.ComponentModel;

using StreetSmartArcGISPro.CycloMediaLayers;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for streetSmart.xaml
  /// </summary>
  public partial class StreetSmart
  {
    #region Members

    private readonly List<CycloMediaLayer> _layers;

    #endregion

    #region Constructor

    public StreetSmart()
    {
      InitializeComponent();
      _layers = new List<CycloMediaLayer>();
    }

    #endregion

    #region Events Properties

    private void OnGroupLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaGroupLayer groupLayer && args.PropertyName == "Count")
      {
        foreach (CycloMediaLayer layer in groupLayer)
        {
          if (!_layers.Contains(layer))
          {
            _layers.Add(layer);
            layer.PropertyChanged += OnLayerPropertyChanged;
          }
        }
      }
    }

    private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
      if (sender is CycloMediaLayer layer)
      {
        switch (args.PropertyName)
        {
          case "Visible":
          case "IsVisibleInstreetSmart":
            // Todo about update
            break;
        }
      }
    }

    #endregion
  }
}
