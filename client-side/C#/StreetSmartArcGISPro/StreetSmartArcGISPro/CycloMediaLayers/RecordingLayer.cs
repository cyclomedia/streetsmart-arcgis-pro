/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018, CycloMedia, All rights reserved.
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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public class RecordingLayer : CycloMediaLayer
  {
    #region Members

    private static List<int> _yearPip;
    private static List<int> _yearForbidden;
    private static List<int> _years;
    private static double _minimumScale;

    private static readonly ConstantsRecordingLayer Constants;

    #endregion

    #region Properties

    public override string Name => Constants.RecordingLayerName;
    public override string FcName => Constants.RecordingLayerFeatureClassName;
    public override bool UseDateRange => false;

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

    private static List<int> Years => _years ?? (_years = new List<int>());

    private static List<int> YearPip => _yearPip ?? (_yearPip = new List<int>());

    private static List<int> YearForbidden => _yearForbidden ?? (_yearForbidden = new List<int>());

    #endregion

    #region Functions

    protected override bool Filter(Recording recording)
    {
      bool result = (recording != null);

      if (result)
      {
        DateTime? recordedAt = recording.RecordedAt;
        result = (recordedAt != null);

        if (result)
        {
          var dateTime = (DateTime) recordedAt;
          int year = dateTime.Year;
          int month = dateTime.Month;

          if (!YearMonth.ContainsKey(year))
          {
            YearMonth.Add(year, month);
            // ReSharper disable once ExplicitCallerInfoArgument
            NotifyPropertyChanged(nameof(YearMonth));
          }
          else
          {
            YearMonth[year] = month;
          }
        }
      }

      return result;
    }

    protected override async Task PostEntryStepAsync(Envelope envelope)
    {
      await QueuedTask.Run(() =>
      {
        var added = new List<int>();
        var pipAdded = new List<int>();
        var forbiddenAdded = new List<int>();

        using (FeatureClass featureClass = Layer?.GetFeatureClass())
        {
          if (featureClass != null)
          {
            SpatialQueryFilter spatialFilter = new SpatialQueryFilter
            {
              FilterGeometry = envelope,
              SpatialRelationship = SpatialRelationship.Contains,
              SubFields = $"{Recording.FieldRecordedAt},{Recording.FieldPip},{Recording.FieldIsAuthorized}"
            };

            using (RowCursor existsResult = featureClass.Search(spatialFilter, false))
            {
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
                    var dateTime = (DateTime) value;
                    int year = dateTime.Year;

                    if (!Years.Contains(year))
                    {
                      Years.Add(year);
                      added.Add(year);
                    }

                    object pipValue = row?.GetOriginalValue(pipId);

                    if (pipValue != null)
                    {
                      bool pip = bool.Parse((string) pipValue);

                      if (pip && !YearPip.Contains(year))
                      {
                        YearPip.Add(year);
                        pipAdded.Add(year);
                      }
                    }

                    object forbiddenValue = row?.GetOriginalValue(forbiddenId);

                    if (forbiddenValue != null)
                    {
                      bool forbidden = !bool.Parse((string) forbiddenValue);

                      if (forbidden && !YearForbidden.Contains(year))
                      {
                        YearForbidden.Add(year);
                        forbiddenAdded.Add(year);
                      }
                    }
                  }
                }
              }
            }
          }
        }

        CIMRenderer featureRenderer = Layer?.GetRenderer();

        if (featureRenderer is CIMUniqueValueRenderer uniqueValueRenderer)
        {
          foreach (var value in added)
          {
            Color color = GetCol(value);
            CIMColor cimColor = ColorFactory.Instance.CreateColor(color);
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(cimColor, Constants.SizeLayer, SimpleMarkerStyle.Circle);
            var pointSymbolReference = pointSymbol.MakeSymbolReference();

            CIMUniqueValue uniqueValue = new CIMUniqueValue
            {
              FieldValues = new[] {value.ToString(), false.ToString(), true.ToString()}
            };

            var uniqueValueClass = new CIMUniqueValueClass
            {
              Editable = true,
              Visible = true,
              Values = new[] {uniqueValue},
              Symbol = pointSymbolReference,
              Label = value.ToString(CultureInfo.InvariantCulture)
            };

            CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
            {
              Classes = new[] {uniqueValueClass},
              Heading = string.Empty
            };

            var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
            groups.Add(uniqueValueGroup);
            uniqueValueRenderer.Groups = groups.ToArray();
            Layer.SetRenderer(uniqueValueRenderer);
          }

          foreach (var value in pipAdded)
          {
            // ToDo: Add a rotation to the PIP symbols
            Color color = GetCol(value);
            CIMMarker marker = GetPipSymbol(color);
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
            var pointSymbolReference = pointSymbol.MakeSymbolReference();

            CIMUniqueValue uniqueValue = new CIMUniqueValue
            {
              FieldValues = new[] { value.ToString(), true.ToString(), true.ToString() }
            };

            var uniqueValueClass = new CIMUniqueValueClass
            {
              Editable = true,
              Visible = true,
              Values = new[] { uniqueValue },
              Symbol = pointSymbolReference,
              Label = $"{value} (Detail images)"
            };

            CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
            {
              Classes = new[] { uniqueValueClass },
              Heading = string.Empty
            };

            var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
            groups.Add(uniqueValueGroup);
            uniqueValueRenderer.Groups = groups.ToArray();
            Layer.SetRenderer(uniqueValueRenderer);
          }

          foreach (var value in forbiddenAdded)
          {
            Color color = GetCol(value);
            CIMMarker marker = GetForbiddenSymbol(color);
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
            var pointSymbolReference = pointSymbol.MakeSymbolReference();

            CIMUniqueValue uniqueValue = new CIMUniqueValue
            {
              FieldValues = new[] { value.ToString(), false.ToString(), false.ToString() }
            };

            CIMUniqueValue uniqueValuePip = new CIMUniqueValue
            {
              FieldValues = new[] {value.ToString(), true.ToString(), false.ToString()}
            };

            var uniqueValueClass = new CIMUniqueValueClass
            {
              Editable = true,
              Visible = true,
              Values = new[] { uniqueValue, uniqueValuePip },
              Symbol = pointSymbolReference,
              Label = $"{value} (No Authorization)"
            };

            CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
            {
              Classes = new[] { uniqueValueClass },
              Heading = string.Empty
            };

            var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
            groups.Add(uniqueValueGroup);
            uniqueValueRenderer.Groups = groups.ToArray();
            Layer.SetRenderer(uniqueValueRenderer);
          }
        }
      });
    }

    protected override void Remove()
    {
      base.Remove();
      ClearYears();
    }

    protected override void ClearYears()
    {
      Years.Clear();
      YearPip.Clear();
      YearForbidden.Clear();
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
