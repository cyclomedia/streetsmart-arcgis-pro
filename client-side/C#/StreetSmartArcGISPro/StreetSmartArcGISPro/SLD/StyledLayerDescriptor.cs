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

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.SLD
{
  /// <exclude/>
  [XmlType(AnonymousType = true, Namespace = "http://www.opengis.net/sld")]
  [XmlRoot(Namespace = "http://www.opengis.net/sld", IsNullable = false)]
  public sealed class StyledLayerDescriptor : IStyledLayerDescriptor
  {
    // ReSharper disable once InconsistentNaming
    const string VersionNumber = "1.1.0";

    public StyledLayerDescriptor()
    {
    }

    public StyledLayerDescriptor(Rule rule)
    {
      Version = VersionNumber;

      UserLayer = new UserLayer
      {
        UserStyle = new UserStyle
        {
          FeatureTypeStyle = new FeatureTypeStyle
          {
            Rules = new ObservableCollection<Rule> { rule }
          }
        }
      };
    }

    public StyledLayerDescriptor(ObservableCollection<Rule> rules)
    {
      Version = VersionNumber;

      UserLayer = new UserLayer
      {
        UserStyle = new UserStyle
        {
          FeatureTypeStyle = new FeatureTypeStyle
          {
            Rules = rules
          }
        }
      };
    }

    [XmlAttribute("version", Namespace = "http://www.opengis.net/sld")]
    public string Version { get; set; }

    [XmlElement("UserLayer", Namespace = "http://www.opengis.net/sld")]
    public UserLayer UserLayer { get; set; }

    public string GetSerializedSld()
    {
      try
      {
        return SerializeToXml();
      }
      catch (Exception ex)
      {
        // Log the exception details here
        return CustomSerialization();
      }
    }

    private string SerializeToXml()
    {
      var serializer = new XmlSerializer(typeof(StyledLayerDescriptor));
      using var writer = new Utf8StringWriter();
      serializer.Serialize(writer, this);
      return writer.ToString();
    }

    public string CustomSerialization()
    {
      var sb = new StringBuilder();
      using (var writer = XmlWriter.Create(sb))
      {
        writer.WriteStartElement("StyledLayerDescriptor", "http://www.opengis.net/sld");
        writer.WriteAttributeString("version", Version);

        writer.WriteStartElement("UserLayer", "http://www.opengis.net/sld");
        writer.WriteStartElement("UserStyle", "http://www.opengis.net/sld");
        writer.WriteStartElement("FeatureTypeStyle", "http://www.opengis.net/sld");

        foreach (var rule in UserLayer.UserStyle.FeatureTypeStyle.Rules)
        {
          writer.WriteStartElement("Rule", "http://www.opengis.net/sld");

          writer.WriteEndElement(); // Rule
        }

        writer.WriteEndElement(); // FeatureTypeStyle
        writer.WriteEndElement(); // UserStyle
        writer.WriteEndElement(); // UserLayer

        writer.WriteEndElement(); // StyledLayerDescriptor
      }
      return sb.ToString();
    }

    private class Utf8StringWriter : StringWriter
    {
      public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
  }
}
