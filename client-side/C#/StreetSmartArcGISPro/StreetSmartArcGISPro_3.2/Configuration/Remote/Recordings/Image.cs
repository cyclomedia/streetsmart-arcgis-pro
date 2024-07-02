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
  public class Image
  {
    #region Properties

    [XmlElement("imageId", Namespace = "http://www.cyclomedia.com/atlas")]
    public string ImageId { get; set; }

    [XmlElement("location", Namespace = "http://www.cyclomedia.com/atlas")]
    public Location Location { get; set; }

    [XmlElement("latitudeStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? LatitudeStDev { get; set; }

    [XmlElement("longitudeStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? LongitudeStDev { get; set; }

    [XmlElement("heightStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? HeightStDev { get; set; }

    [XmlElement("yaw", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? Yaw { get; set; }

    [XmlElement("yawStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? YawStDev { get; set; }

    [XmlElement("pitch", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? Pitch { get; set; }

    [XmlElement("pitchStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? PitchStDev { get; set; }

    [XmlElement("roll", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? Roll { get; set; }

    [XmlElement("rollStDev", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? RollStDev { get; set; }

    [XmlElement("focalLength", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? FocalLength { get; set; }

    [XmlElement("principalPointX", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? PrincipalPointX { get; set; }

    [XmlElement("principalPointY", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? PrincipalPointY { get; set; }

    [XmlElement("imageType", Namespace = "http://www.cyclomedia.com/atlas")]
    public string ImageType { get; set; }

    [XmlElement("imageHeight", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? ImageHeight { get; set; }

    [XmlElement("imageWidth", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? ImageWidth { get; set; }

    [XmlElement("isAuthorized", Namespace = "http://www.cyclomedia.com/atlas")]
    public bool? IsAuthorized { get; set; }

    #endregion
  }
}
