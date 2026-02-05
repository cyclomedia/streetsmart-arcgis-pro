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
using System;
using System.IO;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.SLD
{
  /// <exclude/>
  public class InlineContent : IInlineContent
  {
    public InlineContent()
    {
    }

    public InlineContent(Encoding encoding, string value)
    {
      Encoding = encoding;
      Value = value;
    }

    public InlineContent(Encoding encoding, Image image)
    {
      Encoding = encoding;

      if (encoding == Encoding.Base64)
      {
        using MemoryStream stream = new();
        image.Save(stream, image.RawFormat);
        byte[] imageBytes = stream.ToArray();
        Value = Convert.ToBase64String(imageBytes);
      }
    }

    [XmlAttribute("encoding", Namespace = "http://www.opengis.net/se")]
    public Encoding Encoding { get; set; }

    [XmlText]
    public string Value { get; set; }
  }
}
