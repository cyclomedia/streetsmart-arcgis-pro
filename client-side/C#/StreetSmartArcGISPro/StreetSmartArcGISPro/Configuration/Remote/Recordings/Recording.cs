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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;

namespace StreetSmartArcGISPro.Configuration.Remote.Recordings
{
  [XmlType(AnonymousType = true, Namespace = "http://www.cyclomedia.com/atlas")]
  [XmlRoot(Namespace = "http://www.cyclomedia.com/atlas", IsNullable = false)]
  public class Recording
  {
    #region Members

    private bool? _hasDepthMap;

    #endregion

    #region Fields

    public const string FieldId = "Id";                            // "Id"
    public const string FieldImageId = "ImageId";                  // "ImageId"
    public const string FieldRecordedAt = "RecordedAt";            // "RecordedAt"
    public const string FieldHeight = "Height";                    // "Height"
    public const string FieldHeightSystem = "HeightSyst";          // "HeightSystem"
    public const string FieldLatitudePrecision = "LatPrec";        // "LatitudePrecision"
    public const string FieldLongitudePrecision = "LongPrec";      // "LongitudePrecision"
    public const string FieldHeightPrecision = "HeightPrec";       // "HeightPrecision"
    public const string FieldOrientation = "Orient";               // "Orientation"
    public const string FieldOrientationPrecision = "OrientPrec";  // "OrientationPrecision"
    public const string FieldGroundLevelOffset = "GroundLev";      // "GroundLevelOffset"
    public const string FieldRecorderDirection = "RecordDir";      // "RecorderDirection"
    public const string FieldProductType = "ProdType";             // "ProductType"
    public const string FieldIsAuthorized = "IsAuthoriz";          // "IsAuthorized"
    public const string FieldExpiredAt = "ExpiredAt";              // "ExpiredAt"
    public const string FieldTileSchema = "TileSchema";            // "TileSchema"
    public const string FieldYear = "Year";                        // "Year"
    public const string FieldPip = "PIP";                          // "PIP"
    public const string FieldPip1Yaw = "PIP1Yaw";                  // "PIP1Yaw"
    public const string FieldPip2Yaw = "PIP2Yaw";                  // "PIP2Yaw"
    public const string FieldShape = "Shape";                      // "Shape"
    public const string FieldHasDepthMap = "HasDepthMa";           // "HasDepthMap"

    #endregion

    #region Properties

    public static string ObjectId => FieldImageId;

    public static string ShapeFieldName => FieldShape;

    [XmlAttribute("id", Namespace = "http://www.opengis.net/gml")]
    public string Id { get; set; }

    [XmlElement("imageId", Namespace = "http://www.cyclomedia.com/atlas")]
    public string ImageId { get; set; }

    [XmlElement("recordedAt", Namespace = "http://www.cyclomedia.com/atlas")]
    public DateTime? RecordedAt { get; set; }

    [XmlElement("location", Namespace = "http://www.cyclomedia.com/atlas")]
    public Location Location { get; set; }

    [XmlElement("height", Namespace = "http://www.cyclomedia.com/atlas")]
    public Height Height { get; set; }

    [XmlElement("latitudePrecision", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? LatitudePrecision { get; set; }

    [XmlElement("longitudePrecision", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? LongitudePrecision { get; set; }

    [XmlElement("heightPrecision", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? HeightPrecision { get; set; }

    [XmlElement("orientation", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? Orientation { get; set; }

    [XmlElement("orientationPrecision", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? OrientationPrecision { get; set; }

    [XmlElement("groundLevelOffset", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? GroundLevelOffset { get; set; }

    [XmlElement("recorderDirection", Namespace = "http://www.cyclomedia.com/atlas")]
    public double? RecorderDirection { get; set; }

    [XmlElement("productType", Namespace = "http://www.cyclomedia.com/atlas")]
    public ProductType ProductType { get; set; }

    [XmlElement("Images", Namespace = "http://www.cyclomedia.com/atlas")]
    public Images Images { get; set; }

    [XmlElement("isAuthorized", Namespace = "http://www.cyclomedia.com/atlas")]
    public bool? IsAuthorized { get; set; }

    [XmlElement("expiredAt", Namespace = "http://www.cyclomedia.com/atlas")]
    public DateTime? ExpiredAt { get; set; }

    [XmlElement("tileSchema", Namespace = "http://www.cyclomedia.com/atlas")]
    public TileSchema TileSchema { get; set; }

    [XmlElement("hasDepthMap", Namespace = "http://www.cyclomedia.com/atlas")]
    public bool HasDepthMap
    {
      get => _hasDepthMap ?? false;
      set => _hasDepthMap = value;
    }

    public static Dictionary<string, FieldType> Fields => new Dictionary<string, FieldType>
    {
      {FieldId, FieldType.String},
      {FieldImageId, FieldType.String},
      {FieldRecordedAt, FieldType.Date},
      {FieldHeight, FieldType.Double},
      {FieldHeightSystem, FieldType.String},
      {FieldLatitudePrecision, FieldType.Double},
      {FieldLongitudePrecision, FieldType.Double},
      {FieldHeightPrecision, FieldType.Double},
      {FieldOrientation, FieldType.Double},
      {FieldOrientationPrecision, FieldType.Double},
      {FieldGroundLevelOffset, FieldType.Double},
      {FieldRecorderDirection, FieldType.Double},
      {FieldProductType, FieldType.String},
      {FieldIsAuthorized, FieldType.String},
      {FieldExpiredAt, FieldType.Date},
      {FieldTileSchema, FieldType.String},
      {FieldYear, FieldType.Integer},
      {FieldPip, FieldType.String},
      {FieldPip1Yaw, FieldType.Double},
      {FieldPip2Yaw, FieldType.Double},
      {FieldHasDepthMap, FieldType.String}
    };

    #endregion

    #region Constructor

    public Recording()
    {
      _hasDepthMap = null;
    }

    #endregion

    #region Functions

    public object FieldToItem(string name)
    {
      object result = null;

      switch (name)
      {
        case FieldId:
          result = Id;
          break;
        case FieldImageId:
          result = ImageId;
          break;
        case FieldRecordedAt:
          result = RecordedAt;
          break;
        case FieldLatitudePrecision:
          result = LatitudePrecision;
          break;
        case FieldLongitudePrecision:
          result = LongitudePrecision;
          break;
        case FieldHeightPrecision:
          result = HeightPrecision;
          break;
        case FieldOrientation:
          result = Orientation;
          break;
        case FieldOrientationPrecision:
          result = OrientationPrecision;
          break;
        case FieldGroundLevelOffset:
          result = GroundLevelOffset;
          break;
        case FieldRecorderDirection:
          result = RecorderDirection;
          break;
        case FieldProductType:
          result = ProductType.ToString();
          break;
        case FieldIsAuthorized:
          result = IsAuthorized.ToString();
          break;
        case FieldExpiredAt:
          result = ExpiredAt;
          break;
        case FieldTileSchema:
          result = TileSchema.ToString();
          break;
        case FieldYear:
          if (RecordedAt != null)
          {
            var thisDateTime = (DateTime) RecordedAt;
            result = thisDateTime.Year;
          }
          break;
        case FieldPip:
          result = (Images.Image.Length >= 2).ToString();
          break;
        case FieldPip1Yaw:
          result = Images.Image.Length >= 1 ? Images.Image[0].Yaw : null;
          break;
        case FieldPip2Yaw:
          result = Images.Image.Length >= 2 ? Images.Image[1].Yaw : null;
          break;
        case FieldHasDepthMap:
          result = HasDepthMap.ToString();
          break;
        case FieldHeight:
          result = Height?.Value;
          break;
        case FieldHeightSystem:
          result = Height?.System;
          break;
      }

      return result;
    }

    public void UpdateItem(string name, object item)
    {
      if (item != null)
      {
        switch (name)
        {
          case FieldId:
            Id = (string) item;
            break;
          case FieldImageId:
            ImageId = (string) item;
            break;
          case FieldRecordedAt:
            RecordedAt = (DateTime?) item;
            break;
          case FieldLatitudePrecision:
            LatitudePrecision = (double?) item;
            break;
          case FieldLongitudePrecision:
            LongitudePrecision = (double?) item;
            break;
          case FieldHeightPrecision:
            HeightPrecision = (double?) item;
            break;
          case FieldOrientation:
            Orientation = (double?) item;
            break;
          case FieldOrientationPrecision:
            OrientationPrecision = (double?) item;
            break;
          case FieldGroundLevelOffset:
            GroundLevelOffset = (double?) item;
            break;
          case FieldRecorderDirection:
            RecorderDirection = (double?) item;
            break;
          case FieldProductType:
            ProductType = (ProductType) Enum.Parse(typeof (ProductType), (string) item);
            break;
          case FieldIsAuthorized:
            IsAuthorized = bool.Parse((string) item);
            break;
          case FieldExpiredAt:
            ExpiredAt = (DateTime?) item;
            break;
          case FieldTileSchema:
            TileSchema = (TileSchema) Enum.Parse(typeof (TileSchema), (string) item);
            break;
          case FieldYear:
            break;
          case FieldPip:
            break;
          case FieldPip1Yaw:
            break;
          case FieldPip2Yaw:
            break;
          case FieldShape:
            if (item is MapPoint mapPoint)
            {
              Location = new Location
              {
                Point = new Point
                {
                  Pos = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", mapPoint.X, mapPoint.Y, mapPoint.Z),
                  SrsDimension = mapPoint.HasZ ? 3 : 2,
                  SrsName = $"EPSG: {mapPoint.SpatialReference.Wkid}"
                }
              };
            }

            break;
          case FieldHasDepthMap:
            HasDepthMap = bool.Parse((string) item);
            break;
          case FieldHeight:
            if (Height == null)
            {
              Height = new Height();
            }

            Height.Value = (double?) item;
            break;
          case FieldHeightSystem:
            if (Height == null)
            {
              Height = new Height();
            }

            Height.System = (string) item;
            break;
        }
      }
    }

    #endregion
  }
}
