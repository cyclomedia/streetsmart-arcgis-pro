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

namespace StreetSmartArcGISPro.SLD
{
  /// <summary>
  /// Factory for create SLD objects
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class SLDFactory
  {
    /// <summary>
    /// Returns a polygon style
    /// </summary>
    /// <param name="fillColor">The fill color of the polygon</param>
    /// <param name="fillOpacity">The fill opacity of thye polygon</param>
    /// <param name="strokeColor">The stroke color of the polygon</param>
    /// <param name="strokeWidth">The stroke width of the polygon</param>
    /// <returns>Polygon symbolizer which describes the polygon</returns>
    public static IPolygonSymbolizer CreateStylePolygon(Color fillColor, double? fillOpacity = null,
      Color? strokeColor = null, double? strokeWidth = null) =>
      new PolygonSymbolizer(fillColor, fillOpacity, strokeColor, strokeWidth);

    /// <summary>
    /// Returns a line style
    /// </summary>
    /// <param name="color">The color of the line</param>
    /// <param name="width">The width of the line</param>
    /// <param name="opacity">The opacity of the line</param>
    /// <returns>Line symbolizer which describes the line</returns>
    public static ILineSymbolizer CreateStyleLine(Color color, double? width = null, double? opacity = null) =>
      new LineSymbolizer(color, width, opacity);

    /// <summary>
    /// Returns a point style
    /// </summary>
    /// <param name="type">Well known name of the point objects</param>
    /// <param name="size">Size of the point objects</param>
    /// <param name="fillColor">The fill color of the point objects</param>
    /// <param name="fillOpacity">The fill opacity of the point objects</param>
    /// <param name="strokeColor">The stroke color of the point objects</param>
    /// <param name="strokeWidth">The stroke width of the point objects</param>
    /// <param name="strokeOpacity">The stroke opacity of the point objects</param>
    /// <returns>Point symbolizer which describes the image symbol</returns>
    public static IPointSymbolizer CreateStylePoint(SymbolizerType? type, double size, Color fillColor,
      double? fillOpacity = null, Color? strokeColor = null, double? strokeWidth = null, double? strokeOpacity = null) =>
      new PointSymbolizer(new Graphic(new Mark(type, fillColor, fillOpacity, strokeColor, strokeWidth, strokeOpacity), size));

    /// <summary>
    /// Returns a point symbolizer of an image symbol
    /// </summary>
    /// <param name="size">Size of the point objects</param>
    /// <param name="base64">Base64 string of an image object</param>
    /// <returns>Point symbolizer which describes the image symbol</returns>
    public static IPointSymbolizer CreateImageSymbol(double size, string base64) =>
      new PointSymbolizer(new Graphic(new ExternalGraphic(Encoding.Base64, base64), size));

    /// <summary>
    /// Returns a point symbolizer of an image symbol
    /// </summary>
    /// <param name="size">Size of the point objects</param>
    /// <param name="image">The image object</param>
    /// <returns>Point symbolizer which describes the image symbol</returns>
    public static IPointSymbolizer CreateImageSymbol(double size, Image image) =>
      new PointSymbolizer(new Graphic(new ExternalGraphic(Encoding.Base64, image), size));

    /// <summary>
    /// Returns a sld filter
    /// </summary>
    /// <param name="propertyName">Property name</param>
    /// <param name="literal">Litteral</param>
    /// <returns>Rule of the image object</returns>
    public static IFilter CreateEqualIsFilter(string propertyName, string literal) =>
      new Filter(new FilterPropertyIsEqualTo(propertyName, literal));

    /// <summary>
    /// Returns a sld rule
    /// </summary>
    /// <param name="symbolizer">The point / line or polygon symbolizer</param>
    /// <param name="filter">The filter</param>
    /// <param name="vendorOption">The vendor option</param>
    /// <returns>Rule of the image object</returns>
    public static IRule CreateRule(ISymbolizer symbolizer, IFilter filter = null, IVendorOption vendorOption = null) =>
      new Rule(symbolizer as Symbolizer, filter as Filter, vendorOption as VendorOption);

    /// <summary>
    /// Returns a sld rule
    /// </summary>
    /// <param name="symbolizer">The point / line or polygon symbolizer</param>
    /// <param name="vendorOption">The vendor option</param>
    /// <returns>Rule of the image object</returns>
    public static IRule CreateRule(ISymbolizer symbolizer, IVendorOption vendorOption) =>
      new Rule(symbolizer as Symbolizer, vendorOption as VendorOption);

    /// <summary>
    /// Returns a vendor option
    /// </summary>
    /// <param name="type">Vendor option type</param>
    /// <returns>Vendor option</returns>
    public static IVendorOption CreateVendorOption(VendorOptionType type) => new VendorOption(type);

    /// <summary>
    /// Add an rule to an sld style
    /// </summary>
    /// <param name="sld">The styled layer description of a layer</param>
    /// <param name="rule">Rule which must be added to the sld</param>
    public static void AddRuleToStyle(IStyledLayerDescriptor sld, IRule rule)
    {
      sld?.UserLayer?.UserStyle?.FeatureTypeStyle?.Rules?.Add(rule as Rule);
    }

    /// <summary>
    /// Returns an empty layer style
    /// </summary>
    /// <returns>Styled layer description</returns>
    public static IStyledLayerDescriptor CreateEmptyStyle() =>
      new StyledLayerDescriptor(new ObservableCollection<Rule>());
  }
}
