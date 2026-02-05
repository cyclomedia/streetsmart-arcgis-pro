/*
 * Street Smart .NET integration
 * Copyright (c) 2016 - 2021, CycloMedia, All rights reserved.
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

namespace StreetSmartArcGISPro.SLD
{
  /// <summary>
  /// Stroke type
  /// </summary>
  public enum StrokeType
  {
    /// <summary>
    /// Stroke
    /// </summary>
    [XmlEnum("stroke")]
    Stroke,

    /// <summary>
    /// Width
    /// </summary>
    [XmlEnum("stroke-width")]
    StrokeWidth,

    /// <summary>
    /// Opacity
    /// </summary>
    [XmlEnum("stroke-opacity")]
    StrokeOpacity
  }
}
