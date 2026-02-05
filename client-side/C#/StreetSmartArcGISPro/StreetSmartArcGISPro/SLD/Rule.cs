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
  public class Rule : IRule
  {
    public Rule()
    {
    }

    public Rule(Graphic graphic, VendorOption vendorOption)
    {
      Symbolizer = new PointSymbolizer(graphic);
      VendorOption = vendorOption;
    }

    public Rule(Symbolizer symbolizer, VendorOption vendorOption)
    {
      Symbolizer = symbolizer;
      VendorOption = vendorOption;
    }

    public Rule(Symbolizer symbolizer, Filter filter, VendorOption vendorOption)
    {
      Symbolizer = symbolizer;
      Filter = filter;
      VendorOption = vendorOption;
    }

    [XmlElement("VendorOption", Namespace = "http://www.opengis.net/se")]
    public VendorOption VendorOption { get; set; }

    [XmlElement("Filter", Namespace = "http://www.opengis.net/ogc")]
    public Filter Filter { get; set; }

    [XmlElement("PointSymbolizer", typeof(PointSymbolizer), Namespace = "http://www.opengis.net/se")]
    [XmlElement("LineSymbolizer", typeof(LineSymbolizer), Namespace = "http://www.opengis.net/se")]
    [XmlElement("PolygonSymbolizer", typeof(PolygonSymbolizer), Namespace = "http://www.opengis.net/se")]
    public Symbolizer Symbolizer { get; set; }
  }
#pragma warning restore 1591
}
