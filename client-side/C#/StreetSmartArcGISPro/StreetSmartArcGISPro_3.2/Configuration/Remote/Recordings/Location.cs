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

using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.Recordings
{
  [XmlType(AnonymousType = true, Namespace = "http://www.cyclomedia.com/atlas")]
  [XmlRoot(Namespace = "http://www.cyclomedia.com/atlas", IsNullable = false)]
  public class Location
  {
    #region Properties

    [XmlElement("Point", Namespace = "http://www.opengis.net/gml")]
    public Point Point { get; set; }

    #endregion
  }
}
