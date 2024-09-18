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
using System.Linq;

namespace StreetSmartArcGISPro.Configuration.Remote.GlobeSpotter
{
  public class ApplicationConfiguration
  {
    #region Members

    private readonly List<Functionality> _functionalities;

    #endregion

    #region Constructors

    public ApplicationConfiguration()
    {
      _functionalities = [];
    }

    #endregion

    #region properties

    public Functionality[] Functionalities
    {
      get => _functionalities.ToArray();
      set
      {
        if (value != null)
        {
          _functionalities.AddRange(value);
        }
      }
    }

    #endregion

    #region Functions

    public Functionality GetFunctionality(string name)
    {
      return _functionalities.FirstOrDefault(functionality => functionality.Name == name);
    }

    #endregion
  }
}
