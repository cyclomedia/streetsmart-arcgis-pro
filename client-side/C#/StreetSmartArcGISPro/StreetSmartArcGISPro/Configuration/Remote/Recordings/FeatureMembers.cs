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
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.Recordings
{
  [XmlType(AnonymousType = true, Namespace = "http://www.opengis.net/gml")]
  [XmlRoot(Namespace = "http://www.opengis.net/gml", IsNullable = false)]
  public class FeatureMembers
  {
    private List<Recording> _recordings;

    #region Properties

    [XmlElement("Recording", Namespace = "http://www.cyclomedia.com/atlas")]
    public Recording[] Recordings
    {
      get => _recordings?.ToArray() ?? new Recording[0];
      set
      {
        if (value != null)
        {
          if (_recordings == null)
          {
            _recordings = [];
          }

          _recordings.AddRange(value);
        }
      }
    }

    #endregion
  }
}
