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
  public class Mark : IMark
  {
    public Mark()
    {
    }

    public Mark(SymbolizerType type, SvgParameterCollection<FillType> fill, SvgParameterCollection<StrokeType> stroke)
    {
      WellKnownName = type;
      Fill = fill;
      Stroke = stroke;
    }

    public Mark(SymbolizerType? type, Color fillColor, double? fillOpacity, Color? strokeColor, double? strokeWidth, double? strokeOpacity)
    {

      WellKnownName = type;

      Fill = SvgParameterCollection<FillType>.GetFillObject(fillColor, fillOpacity);

      if (strokeColor != null)
      {
        Stroke = SvgParameterCollection<StrokeType>.GetStrokeObject((Color)strokeColor, strokeWidth, strokeOpacity);
      }
    }

    [XmlElement("WellKnownName", Namespace = "http://www.opengis.net/se")]
    public SymbolizerType? WellKnownName { get; set; }

    [XmlElement("Fill", Namespace = "http://www.opengis.net/se")]
    public SvgParameterCollection<FillType> Fill { get; set; }

    [XmlElement("Stroke", Namespace = "http://www.opengis.net/se")]
    public SvgParameterCollection<StrokeType> Stroke { get; set; }

    public bool ShouldSerializeWellKnownName()
    {
      return WellKnownName.HasValue;
    }
  }

#pragma warning restore 1591
}
