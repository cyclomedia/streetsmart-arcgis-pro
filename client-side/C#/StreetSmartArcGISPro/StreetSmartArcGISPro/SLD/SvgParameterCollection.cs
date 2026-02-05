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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.SLD
{
#pragma warning disable 1591
  /// <exclude/>
  public class SvgParameterCollection<T> : ISvgParameterCollection<T>
  {
    [XmlElement("SvgParameter", Namespace = "http://www.opengis.net/se")]
    public ObservableCollection<SvgParameter<T>> SvgParameter { get; set; }

    public static SvgParameterCollection<FillType> GetFillObject(Color color, double? opacity)
    {
      SvgParameterCollection<FillType> result = new SvgParameterCollection<FillType>
      {
        SvgParameter = new ObservableCollection<SvgParameter<FillType>>
        {
          new()
          {
            Name = FillType.Fill,
            Value = $"#{color.ToArgb().ToString("X8").Substring(2)}"
          }
        }
      };

      if (opacity != null)
      {
        result.SvgParameter.Add(new SvgParameter<FillType>
        {
          Name = FillType.FillOpacity,
          Value = opacity?.ToString(CultureInfo.InvariantCulture)
        });
      }

      return result;
    }

    public static SvgParameterCollection<StrokeType> GetStrokeObject(Color color, double? width, double? opacity)
    {
      SvgParameterCollection<StrokeType> result = new SvgParameterCollection<StrokeType>
      {
        SvgParameter = new ObservableCollection<SvgParameter<StrokeType>>
        {
          new()
          {
            Name = StrokeType.Stroke,
            Value = $"#{color.ToArgb().ToString("X8").Substring(2)}"
          }
        }
      };

      if (width != null)
      {
        result.SvgParameter.Add(new SvgParameter<StrokeType>
        {
          Name = StrokeType.StrokeWidth,
          Value = width?.ToString(CultureInfo.InvariantCulture)
        });
      }

      if (opacity != null)
      {
        result.SvgParameter.Add(new SvgParameter<StrokeType>
        {
          Name = StrokeType.StrokeOpacity,
          Value = opacity?.ToString(CultureInfo.InvariantCulture)
        });
      }

      return result;
    }
  }
#pragma warning restore 1591
}
