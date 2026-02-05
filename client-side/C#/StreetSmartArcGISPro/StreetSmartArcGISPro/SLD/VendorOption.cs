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
#pragma warning disable 1591
  /// <exclude/>
  public class VendorOption : IVendorOption
  {
    public VendorOption()
    {
    }

    public VendorOption(VendorOptionType name)
    {
      Name = name;
    }

    [XmlAttribute("name", Namespace = "http://www.opengis.net/se")]
    public VendorOptionType Name { get; set; }
  }
#pragma warning restore 1591
}
