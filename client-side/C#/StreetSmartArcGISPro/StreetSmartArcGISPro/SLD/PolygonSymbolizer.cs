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

using Aspose.Drawing;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.SLD
{
#pragma warning disable 1591
  /// <exclude/>
  public class PolygonSymbolizer : Symbolizer, IPolygonSymbolizer
  {
    public PolygonSymbolizer()
    {
    }

    public PolygonSymbolizer(SvgParameterCollection<FillType> fill, SvgParameterCollection<StrokeType> stroke)
    {
      Fill = fill;
      Stroke = stroke;
    }

    public PolygonSymbolizer(Color? fillColor, double? fillOpacity, Color? strokeColor, double? strokeWidth)
    {
      if (fillColor != null)
      {
        Fill = SvgParameterCollection<FillType>.GetFillObject((Color)fillColor, fillOpacity);
      }

      if (strokeColor != null)
      {
        Stroke = SvgParameterCollection<StrokeType>.GetStrokeObject((Color)strokeColor, strokeWidth, null);
      }
    }

    [XmlElement("Fill", Namespace = "http://www.opengis.net/se")]
    public SvgParameterCollection<FillType> Fill { get; set; }

    [XmlElement("Stroke", Namespace = "http://www.opengis.net/se")]
    public SvgParameterCollection<StrokeType> Stroke { get; set; }
  }
#pragma warning restore 1591
}
