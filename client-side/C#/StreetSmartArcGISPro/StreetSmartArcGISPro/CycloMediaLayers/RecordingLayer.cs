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

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using Envelope = ArcGIS.Core.Geometry.Envelope;

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public enum TypeOfLayer
  {
    YearPip = 1,
    YearForbidden = 2,
    YearDepthMap = 3,
    YearNoDepthMap = 4
  }

  public class RecordingLayer : CycloMediaLayer
  {
    #region Members

    private static Dictionary<FeatureLayer, Dictionary<TypeOfLayer, List<int>>> _yearsVisible;
    private static Dictionary<int, Dictionary<TypeOfLayer, CIMUniqueValueGroup>> _uniqueValueGroups;
    private static double _minimumScale;

    private static readonly ConstantsRecordingLayer Constants;

    #endregion

    #region Properties

    public override string Name => Properties.Resources.ResourceManager.GetString("RecordingLayerName", LanguageSettings.Instance.CultureInfo);

    public override string FcName => Constants.RecordingLayerFeatureClassName;

    public override string WfsRequest
      =>
        "<wfs:GetFeature service=\"WFS\" version=\"1.1.0\" resultType=\"results\" outputFormat=\"text/xml; subtype=gml/3.1.1\" xmlns:wfs=\"http://www.opengis.net/wfs\">" +
        "<wfs:Query typeName=\"atlas:Recording\" srsName=\"{0}\" xmlns:atlas=\"http://www.cyclomedia.com/atlas\"><ogc:Filter xmlns:ogc=\"http://www.opengis.net/ogc\">" +
        "<ogc:And><ogc:BBOX><gml:Envelope srsName=\"{0}\" xmlns:gml=\"http://www.opengis.net/gml\"><gml:lowerCorner>{1} {2}</gml:lowerCorner>" +
        "<gml:upperCorner>{3} {4}</gml:upperCorner></gml:Envelope></ogc:BBOX><ogc:PropertyIsNull><ogc:PropertyName>expiredAt</ogc:PropertyName></ogc:PropertyIsNull>" +
        "</ogc:And></ogc:Filter></wfs:Query></wfs:GetFeature>";

    public override double MinimumScale
    {
      get => _minimumScale;
      set => _minimumScale = value;
    }

    private static Dictionary<FeatureLayer, Dictionary<TypeOfLayer, List<int>>> YearsVisible => _yearsVisible ??= [];

    private static Dictionary<int, Dictionary<TypeOfLayer, CIMUniqueValueGroup>> UniqueValueGroups => _uniqueValueGroups ??= [];

    private List<int> GetYearValue(FeatureLayer layer, TypeOfLayer type)
    {
      if (layer == null)
      {
        return null;
      }

      if (!YearsVisible.ContainsKey(layer))
      {
        YearsVisible.Add(layer, []);
      }

      return GetValue(YearsVisible[layer], type);
    }

    private List<int> GetValue(Dictionary<TypeOfLayer, List<int>> yearValue, TypeOfLayer type)
    {
      if (!yearValue.ContainsKey(type))
      {
        yearValue.Add(type, []);
      }

      return yearValue[type];
    }

    private void AddValue(Dictionary<TypeOfLayer, List<int>> yearValue, TypeOfLayer type, int year)
    {
      var toAdd = GetValue(yearValue, type);

      if (!toAdd.Contains(year))
      {
        toAdd.Add(year);
      }
    }

    private Dictionary<TypeOfLayer, CIMUniqueValueGroup> GetUniqueValueGroupYear(int year)
    {
      if (!UniqueValueGroups.ContainsKey(year))
      {
        UniqueValueGroups.Add(year, []);
      }

      return UniqueValueGroups[year];
    }

    private CIMUniqueValueGroup GetUniqueValue(Dictionary<TypeOfLayer, CIMUniqueValueGroup> typeOfLayer, TypeOfLayer type)
    {
      if (!typeOfLayer.ContainsKey(type))
      {
        typeOfLayer.Add(type, null);
      }

      return typeOfLayer[type];
    }

    private CIMUniqueValueGroup GetUniqueValueGroup(int year, TypeOfLayer type)
    {
      return GetUniqueValue(GetUniqueValueGroupYear(year), type);
    }

    private void AddUniqueValueGroup(int year, TypeOfLayer type, CIMUniqueValueGroup uniqueValueGroup)
    {
      var typeOfLayer = GetUniqueValueGroupYear(year);
      GetUniqueValue(typeOfLayer, type);
      typeOfLayer[type] = uniqueValueGroup;
    }

    #endregion

    #region Functions

    protected override bool Filter(Recording recording)
    {
      bool result = recording != null;

      if (result)
      {
        DateTime? recordedAt = recording.RecordedAt;
        result = recordedAt != null;

        if (result)
        {
          var dateTime = (DateTime)recordedAt;
          int year = dateTime.Year;
          int month = dateTime.Month;
          var yearMonth = GetYearMonth(Layer);

          if (!yearMonth.ContainsKey(year))
          {
            yearMonth.Add(year, month);
            // ReSharper disable once ExplicitCallerInfoArgument
            NotifyPropertyChanged(nameof(YearMonth));
          }
          else
          {
            yearMonth[year] = month;
          }
        }
      }

      return result;
    }

    protected override async Task PostEntryStepAsync(Envelope envelope)
    {
      await QueuedTask.Run(() =>
      {
        var years = new Dictionary<TypeOfLayer, List<int>>();

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          if (featureClass != null)
          {
            SpatialQueryFilter spatialFilter = new SpatialQueryFilter
            {
              FilterGeometry = envelope,
              SpatialRelationship = SpatialRelationship.Contains,
              SubFields = $"{Recording.FieldRecordedAt},{Recording.FieldPip},{Recording.FieldIsAuthorized},{Recording.FieldHasDepthMap}"
            };

            using (RowCursor existsResult = featureClass.Search(spatialFilter, false))
            {
              int hasDepthMapId = existsResult.FindField(Recording.FieldHasDepthMap);
              int imId = existsResult.FindField(Recording.FieldRecordedAt);
              int pipId = existsResult.FindField(Recording.FieldPip);
              int forbiddenId = existsResult.FindField(Recording.FieldIsAuthorized);

              while (existsResult.MoveNext())
              {
                using (Row row = existsResult.Current)
                {
                  object value = row?.GetOriginalValue(imId);

                  if (value != null)
                  {
                    var dateTime = (DateTime)value;
                    int year = dateTime.Year;

                    object pipValue = row?.GetOriginalValue(pipId);

                    if (pipValue != null)
                    {
                      bool pip = bool.Parse((string)pipValue);

                      if (pip)
                      {
                        AddValue(years, TypeOfLayer.YearPip, year);
                      }
                    }

                    object forbiddenValue = row?.GetOriginalValue(forbiddenId);

                    if (forbiddenValue != null)
                    {
                      bool forbidden = !bool.Parse((string)forbiddenValue);

                      if (forbidden)
                      {
                        AddValue(years, TypeOfLayer.YearForbidden, year);
                      }
                    }

                    string depthMapValue = row?.GetOriginalValue(hasDepthMapId) as string;
                    bool depthMap = bool.Parse(string.IsNullOrEmpty(depthMapValue) ? false.ToString() : depthMapValue);
                    AddValue(years, depthMap ? TypeOfLayer.YearDepthMap : TypeOfLayer.YearNoDepthMap, year);
                  }
                }
              }
            }
          }
        }

        CIMRenderer featureRenderer = Layer?.GetRenderer();

        if (featureRenderer is CIMUniqueValueRenderer uniqueValueRenderer)
        {
          TypeOfLayer[] typeOfLayers =
            {TypeOfLayer.YearNoDepthMap, TypeOfLayer.YearDepthMap, TypeOfLayer.YearPip, TypeOfLayer.YearForbidden};
          var groups = new List<CIMUniqueValueGroup>();
          bool change = false;

          foreach (var typeOfLayer in typeOfLayers)
          {
            List<int> typeValue = GetValue(years, typeOfLayer);
            List<int> typeValuePast = GetYearValue(Layer, typeOfLayer);

            foreach (var value in typeValue)
            {
              var uniqueValue = GetUniqueValueGroup(value, typeOfLayer);

              if (!typeValuePast.Contains(value))
              {
                if (uniqueValue == null)
                {
                  switch (typeOfLayer)
                  {
                    case TypeOfLayer.YearNoDepthMap:
                      uniqueValue = CreateNoDepthValueGroup(value);
                      break;
                    case TypeOfLayer.YearDepthMap:
                      uniqueValue = CreateDepthValueGroup(value);
                      break;
                    case TypeOfLayer.YearPip:
                      uniqueValue = CreatePipValueGroup(value);
                      break;
                    case TypeOfLayer.YearForbidden:
                      uniqueValue = CreateForbiddenValueGroup(value);
                      break;
                  }

                  AddUniqueValueGroup(value, typeOfLayer, uniqueValue);
                }

                change = true;
                typeValuePast.Add(value);
              }

              groups.Add(uniqueValue);
            }

            int i = 0;

            while (i < typeValuePast.Count)
            {
              int value = typeValuePast[i];

              if (typeValue.Contains(value))
              {
                i++;
              }
              else
              {
                typeValuePast.Remove(value);
                change = true;
              }
            }
          }

          if (change)
          {
            uniqueValueRenderer.Groups = groups.ToArray();
            Layer.SetRenderer(uniqueValueRenderer);
          }
        }
      });
    }

    private CIMUniqueValueGroup CreateDepthValueGroup(int value)
    {
      CIMColor cimColor = CIMColor.CreateRGBColor(152, 194, 60);
      CIMSymbolReference pointSymbolReference = MakePointSymbol(cimColor);

      CIMUniqueValue uniqueValue = new CIMUniqueValue
      {
        FieldValues = new[] { value.ToString(), false.ToString(), true.ToString(), true.ToString() }
      };

      var uniqueValueClass = new CIMUniqueValueClass
      {
        Editable = true,
        Visible = true,
        Values = new[] { uniqueValue },
        Symbol = pointSymbolReference,
        Label = value.ToString(CultureInfo.InvariantCulture)
      };

      return new CIMUniqueValueGroup
      {
        Classes = new[] { uniqueValueClass },
        Heading = string.Empty
      };
    }

    private CIMUniqueValueGroup CreateNoDepthValueGroup(int value)
    {
      CIMColor cimColor = CIMColor.CreateRGBColor(128, 176, 255);
      CIMSymbolReference pointSymbolReference = MakePointSymbol(cimColor);

      CIMUniqueValue uniqueValue = new CIMUniqueValue
      {
        FieldValues = new[] { value.ToString(), false.ToString(), true.ToString(), false.ToString() }
      };

      var uniqueValueClass = new CIMUniqueValueClass
      {
        Editable = true,
        Visible = true,
        Values = new[] { uniqueValue },
        Symbol = pointSymbolReference,
        Label = value.ToString(CultureInfo.InvariantCulture)
      };

      return new CIMUniqueValueGroup
      {
        Classes = new[] { uniqueValueClass },
        Heading = string.Empty
      };
    }

    private CIMUniqueValueGroup CreatePipValueGroup(int value)
    {
      // ToDo: Add a rotation to the PIP symbols
      Color color = Color.FromArgb(255, Color.FromArgb(0x80B0FF));
      CIMMarker marker = GetPipSymbol(color);
      var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
      var pointSymbolReference = pointSymbol.MakeSymbolReference();
      string detailImagesString = Properties.Resources.ResourceManager.GetString("RecordingLayerDetailImages", LanguageSettings.Instance.CultureInfo);

      CIMUniqueValue uniqueValue = new() { FieldValues = [value.ToString(), true.ToString(), true.ToString(), false.ToString()] };

      var uniqueValueClass = new CIMUniqueValueClass
      {
        Editable = true,
        Visible = true,
        Values = [uniqueValue],
        Symbol = pointSymbolReference,
        Label = $"{value} ({detailImagesString})"
      };

      return new CIMUniqueValueGroup
      {
        Classes = [uniqueValueClass],
        Heading = string.Empty
      };
    }

    private CIMUniqueValueGroup CreateForbiddenValueGroup(int value)
    {
      Color color = Color.FromArgb(255, Color.FromArgb(0x80B0FF));
      CIMMarker marker = GetForbiddenSymbol(color);
      var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
      var pointSymbolReference = pointSymbol.MakeSymbolReference();
      string noAuthorizationString = Properties.Resources.ResourceManager.GetString("RecordingLayerNoAuthorization", LanguageSettings.Instance.CultureInfo);

      CIMUniqueValue uniqueValue = new() { FieldValues = [value.ToString(), false.ToString(), false.ToString(), false.ToString()] };

      CIMUniqueValue uniqueValuePip = new() { FieldValues = [value.ToString(), true.ToString(), false.ToString(), false.ToString()] };

      CIMUniqueValue depthMap = new() { FieldValues = [value.ToString(), false.ToString(), false.ToString(), true.ToString()] };

      var uniqueValueClass = new CIMUniqueValueClass
      {
        Editable = true,
        Visible = true,
        Values = new[] { uniqueValue, uniqueValuePip, depthMap },
        Symbol = pointSymbolReference,
        Label = $"{value} ({noAuthorizationString})"
      };

      return new CIMUniqueValueGroup
      {
        Classes = [uniqueValueClass],
        Heading = string.Empty
      };
    }

    private CIMSymbolReference MakePointSymbol(CIMColor cimColor)
    {
      var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(cimColor, Constants.SizeLayer, SimpleMarkerStyle.Circle);
      CIMMarkerGraphic[] markerGraphics = (pointSymbol.SymbolLayers[0] as CIMVectorMarker)?.MarkerGraphics;
      CIMPolygonSymbol polygonSymbol = markerGraphics?[0].Symbol as CIMPolygonSymbol;

      if (polygonSymbol?.SymbolLayers[0] is CIMSolidStroke solidStroke)
      {
        CIMColor cimStrokeColor = CIMColor.CreateRGBColor(255, 255, 255);
        solidStroke.Color = cimStrokeColor;
      }

      return pointSymbol.MakeSymbolReference();
    }

    protected override void Remove()
    {
      base.Remove();
      ClearYears();
    }

    protected override void ClearYears()
    {
      TypeOfLayer[] typeOfLayers = [TypeOfLayer.YearNoDepthMap, TypeOfLayer.YearDepthMap, TypeOfLayer.YearPip, TypeOfLayer.YearForbidden];

      foreach (TypeOfLayer typeOfLayer in typeOfLayers)
      {
        var years = GetYearValue(Layer, typeOfLayer);
        years?.Clear();
      }
    }

    #endregion

    #region Constructors

    static RecordingLayer()
    {
      Constants = ConstantsRecordingLayer.Instance;
      _minimumScale = Constants.MinimumScale;
    }

    public RecordingLayer(CycloMediaGroupLayer layer, Envelope initialExtent = null)
      : base(layer, initialExtent)
    {
    }

    #endregion
  }
}
