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

using System.Globalization;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.Recordings
{
  [XmlType(AnonymousType = true, Namespace = "http://www.opengis.net/gml")]
  [XmlRoot(Namespace = "http://www.opengis.net/gml", IsNullable = false)]
  public class Point
  {
    #region Members

    private readonly CultureInfo _ci;
    private string _pos;

    #endregion

    #region Constructor

    public Point()
    {
      _ci = CultureInfo.InvariantCulture;
    }

    #endregion

    #region Properties

    [XmlAttribute("srsName", Namespace = "http://www.opengis.net/gml")]
    public string SrsName { get; set; }

    [XmlAttribute("srsDimension", Namespace = "http://www.opengis.net/gml")]
    public int SrsDimension { get; set; }

    [XmlElement("pos", Namespace = "http://www.opengis.net/gml")]
    public string Pos
    {
      get => _pos;
      set
      {
        _pos = value;

        if (!string.IsNullOrEmpty(_pos))
        {
          string position = _pos.Trim();
          string[] values = position.Split(' ');
          X = values.Length >= 1 ? double.Parse(values[0], _ci) : 0.0;
          Y = values.Length >= 2 ? double.Parse(values[1], _ci) : 0.0;
          Z = values.Length >= 3 ? double.Parse(values[2], _ci) : 0.0;
        }
      }
    }

    [XmlIgnore]
    public double X { get; set; }

    [XmlIgnore]
    public double Y { get; set; }

    [XmlIgnore]
    public double Z { get; set; }

    #endregion
  }
}
