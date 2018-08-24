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
using ArcGIS.Desktop.Mapping.Events;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Remote.Recordings;

namespace StreetSmartArcGISPro.CycloMediaLayers
{
  public class HistoricalLayer : CycloMediaLayer
  {
    #region Members

    private static List<int> _yearPip;
    private static List<int> _yearForbidden;
    private static List<int> _years;
    private static double _minimumScale;
    private readonly HistoricalRecordings _historicalRecordings;

    private static readonly ConstantsRecordingLayer Constants;

    #endregion

    #region Properties

    public override string Name => Constants.HistoricalRecordingLayerName;
    public override string FcName => Constants.HistoricalRecordingLayerFeatureClassName;
    public override bool UseDateRange => true;

    public override string WfsRequest
      =>
        "<wfs:GetFeature service=\"WFS\" version=\"1.1.0\" resultType=\"results\" outputFormat=\"text/xml; subtype=gml/3.1.1\" xmlns:wfs=\"http://www.opengis.net/wfs\">" +
        "<wfs:Query typeName=\"atlas:Recording\" srsName=\"{0}\" xmlns:atlas=\"http://www.cyclomedia.com/atlas\"><ogc:Filter xmlns:ogc=\"http://www.opengis.net/ogc\">" +
        "<ogc:And><ogc:BBOX><gml:Envelope srsName=\"{0}\" xmlns:gml=\"http://www.opengis.net/gml\"><gml:lowerCorner>{1} {2}</gml:lowerCorner>" +
        "<gml:upperCorner>{3} {4}</gml:upperCorner></gml:Envelope></ogc:BBOX><ogc:PropertyIsBetween><ogc:PropertyName>recordedAt</ogc:PropertyName><ogc:LowerBoundary>" +
        "<ogc:Literal>1991-12-31T23:00:00-00:00</ogc:Literal></ogc:LowerBoundary><ogc:UpperBoundary><ogc:Literal>{5}</ogc:Literal></ogc:UpperBoundary></ogc:PropertyIsBetween>" +
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
      bool result = recording != null;

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

        CIMBaseLayer cimBaseLayer = Layer?.GetDefinition();
        CIMBasicFeatureLayer cimBasicFeatureLayer = cimBaseLayer as CIMBasicFeatureLayer;
        CIMFeatureTable cimFeatureTable = cimBasicFeatureLayer?.FeatureTable;

        if ((cimFeatureTable != null) && (cimFeatureTable.TimeFields == null))
        {
          cimFeatureTable.TimeFields = new CIMTimeTableDefinition
          {
            StartTimeField = Recording.FieldRecordedAt
          };

          cimFeatureTable.TimeDefinition = new CIMTimeDataDefinition
          {
            UseTime = true,
            HasLiveData = false,
            CustomTimeExtent = new TimeExtent
            {
              StartTime = _historicalRecordings.DateFrom,
              EndTime = _historicalRecordings.DateTo,
              Empty = false
            }
          };

          cimFeatureTable.TimeDisplayDefinition = new CIMTimeDisplayDefinition
          {
            Cumulative = false,
            TimeInterval = 0,
            TimeIntervalUnits = esriTimeUnits.esriTimeUnitsUnknown,
            TimeOffset = 0,
            TimeOffsetUnits = esriTimeUnits.esriTimeUnitsDays
          };

          cimFeatureTable.TimeDimensionFields = new CIMTimeDimensionDefinition();
          Layer.SetDefinition(cimBaseLayer);
          var mapView = MapView.Active;

          mapView.Time = new TimeRange
          {
            Start = _historicalRecordings.DateFrom,
            End = _historicalRecordings.DateTo
          };
        }

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
                    int month = dateTime.Month;
                    int calcYear = year*4 + (int) Math.Floor((double) (month - 1)/3);

                    if (!Years.Contains(calcYear) && !added.Contains(calcYear) && YearInsideRange(year, month))
                    {
                      added.Add(calcYear);
                    }

                    object pipValue = row?.GetOriginalValue(pipId);

                    if (pipValue != null)
                    {
                      bool pip = bool.Parse((string) pipValue);

                      if (pip && !YearPip.Contains(calcYear) && !pipAdded.Contains(calcYear) && YearInsideRange(year, month))
                      {
                        pipAdded.Add(calcYear);
                      }
                    }

                    object forbiddenValue = row?.GetOriginalValue(forbiddenId);

                    if (forbiddenValue != null)
                    {
                      bool forbidden = !bool.Parse((string) forbiddenValue);

                      if (forbidden && !YearForbidden.Contains(calcYear) && !forbiddenAdded.Contains(calcYear) && YearInsideRange(year, month))
                      {
                        forbiddenAdded.Add(calcYear);
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
            bool realAdd = true;
            var newValue = (int) Math.Floor(((double) value)/4);

            for (int i = newValue * 4; i < newValue * 4 + 4; i++)
            {
              realAdd = !Years.Contains(i) && realAdd;
            }

            Years.Add(value);

            if (realAdd)
            {
              Color color = GetCol(newValue);
              CIMColor cimColor = ColorFactory.Instance.CreateColor(color);
              var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(cimColor, Constants.SizeLayer, SimpleMarkerStyle.Circle);
              var pointSymbolReference = pointSymbol.MakeSymbolReference();

              CIMUniqueValue uniqueValue = new CIMUniqueValue
              {
                FieldValues = new[] {newValue.ToString(), false.ToString(), true.ToString()}
              };

              var uniqueValueClass = new CIMUniqueValueClass
              {
                Editable = true,
                Visible = true,
                Values = new[] {uniqueValue},
                Symbol = pointSymbolReference,
                Label = newValue.ToString(CultureInfo.InvariantCulture)
              };

              CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
              {
                Classes = new[] {uniqueValueClass},
                Heading = string.Empty
              };

              var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
              groups.Add(uniqueValueGroup);
              groups.Sort(SortGroups);
              uniqueValueRenderer.Groups = groups.ToArray();
              Layer.SetRenderer(uniqueValueRenderer);
            }
          }

          foreach (var value in pipAdded)
          {
            bool realAdd = true;
            var newValue = (int) Math.Floor((double) value/4);

            for (int i = newValue * 4; i < newValue * 4 + 4; i++)
            {
              realAdd = !YearPip.Contains(i) && realAdd;
            }

            YearPip.Add(value);

            if (realAdd)
            {
              // ToDo: Add a rotation to the PIP symbols
              Color color = GetCol(newValue);
              CIMMarker marker = GetPipSymbol(color);
              var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
              var pointSymbolReference = pointSymbol.MakeSymbolReference();

              CIMUniqueValue uniqueValue = new CIMUniqueValue
              {
                FieldValues = new[] {newValue.ToString(), true.ToString(), true.ToString()}
              };

              var uniqueValueClass = new CIMUniqueValueClass
              {
                Editable = true,
                Visible = true,
                Values = new[] {uniqueValue},
                Symbol = pointSymbolReference,
                Label = $"{newValue} (Detail images)"
              };

              CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
              {
                Classes = new[] { uniqueValueClass },
                Heading = string.Empty
              };

              var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
              groups.Add(uniqueValueGroup);
              groups.Sort(SortGroups);
              uniqueValueRenderer.Groups = groups.ToArray();
              Layer.SetRenderer(uniqueValueRenderer);
            }
          }

          foreach (var value in forbiddenAdded)
          {
            bool realAdd = true;
            var newValue = (int) Math.Floor((double) value/4);

            for (int i = newValue * 4; i < newValue * 4 + 4; i++)
            {
              realAdd = !YearForbidden.Contains(i) && realAdd;
            }

            YearForbidden.Add(value);

            if (realAdd)
            {
              Color color = GetCol(newValue);
              CIMMarker marker = GetForbiddenSymbol(color);
              var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(marker);
              var pointSymbolReference = pointSymbol.MakeSymbolReference();

              CIMUniqueValue uniqueValue = new CIMUniqueValue
              {
                FieldValues = new[] {newValue.ToString(), false.ToString(), false.ToString()}
              };

              CIMUniqueValue uniqueValuePip = new CIMUniqueValue
              {
                FieldValues = new[] {newValue.ToString(), true.ToString(), false.ToString()}
              };

              var uniqueValueClass = new CIMUniqueValueClass
              {
                Editable = true,
                Visible = true,
                Values = new[] {uniqueValue, uniqueValuePip},
                Symbol = pointSymbolReference,
                Label = $"{newValue} (No Authorization)"
              };

              CIMUniqueValueGroup uniqueValueGroup = new CIMUniqueValueGroup
              {
                Classes = new[] {uniqueValueClass},
                Heading = string.Empty
              };

              var groups = uniqueValueRenderer.Groups?.ToList() ?? new List<CIMUniqueValueGroup>();
              groups.Add(uniqueValueGroup);
              groups.Sort(SortGroups);
              uniqueValueRenderer.Groups = groups.ToArray();
              Layer.SetRenderer(uniqueValueRenderer);
            }
          }

          var removed = (from yearColor in Years
                         select yearColor
                         into year
                         where !YearInsideRange((int) Math.Floor((double) year/4), year%4*3 + 1) && !added.Contains(year)
                         select year).ToList();

          foreach (int year in removed)
          {
            int newYear = (int) Math.Floor((double) year/4);

            if (YearPip.Contains(year))
            {
              string[] classValuesPip = {$"{newYear}", $"{true}", $"{true}"};
              RemoveValues(YearPip, classValuesPip, year, uniqueValueRenderer);
            }

            string[] classValues = {$"{newYear}", $"{false}", $"{true}"};
            RemoveValues(Years, classValues, year, uniqueValueRenderer);
          }
        }
      });
    }

    private void RemoveValues(List<int> years, string[] classValues, int year, CIMUniqueValueRenderer uniqueValueRenderer)
    {
      CIMUniqueValueGroup foundGroup = null;

      if (uniqueValueRenderer.Groups != null)
      {
        foreach (var group in uniqueValueRenderer.Groups)
        {
          foreach (var thisClass in group.Classes)
          {
            foreach (var value in thisClass.Values)
            {
              bool found = value.FieldValues.Length >= classValues.Length;

              for (int i = 0; i < classValues.Length; i++)
              {
                found = found && classValues[i] == value.FieldValues[i];
              }

              foundGroup = found ? group : foundGroup;
            }
          }
        }
      }

      if (foundGroup != null)
      {
        var groups = uniqueValueRenderer.Groups.ToList();
        groups.Remove(foundGroup);
        uniqueValueRenderer.Groups = groups.ToArray();
        Layer.SetRenderer(uniqueValueRenderer);
      }

      years.Remove(year);
    }

    private int SortGroups(CIMUniqueValueGroup group1, CIMUniqueValueGroup group2)
    {
      int year1 = 0, year2 = 0;
      bool pip1 = false, pip2 = false;
      bool forbidden1 = false, forbidden2 = false;

      foreach (var thisClass in group1.Classes)
      {
        foreach (var value in thisClass.Values)
        {
          string[] fieldValues = value.FieldValues;
          year1 = fieldValues.Length >= 1 ? int.Parse(fieldValues[0]) : 0;
          pip1 = fieldValues.Length >= 2 && bool.Parse(fieldValues[1]);
          forbidden1 = fieldValues.Length >= 3 && bool.Parse(fieldValues[2]);
        }
      }

      foreach (var thisClass in group2.Classes)
      {
        foreach (var value in thisClass.Values)
        {
          string[] fieldValues = value.FieldValues;
          year2 = fieldValues.Length >= 1 ? int.Parse(fieldValues[0]) : 0;
          pip2 = fieldValues.Length >= 2 && bool.Parse(fieldValues[1]);
          forbidden2 = fieldValues.Length >= 3 && bool.Parse(fieldValues[2]);
        }
      }

      return year1 > year2 ? -1
        : (year2 > year1 ? 1
        : (pip1 == false && pip2 ? -1
        : (pip1 && pip2 == false ? 1
        : (forbidden1 == false && forbidden2 ? -1
        : (forbidden1 && forbidden2 == false ? 1
        : 0)))));
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

    private bool YearInsideRange(int year, int month)
    {
      DateTime fromDateTime = _historicalRecordings.DateFrom;
      DateTime toDateTime = _historicalRecordings.DateTo;
      var checkDateTime = new DateTime(year, month, 1);
      return checkDateTime.CompareTo(fromDateTime) >= 0 && checkDateTime.CompareTo(toDateTime) < 0;
    }

    #endregion

    #region Constructors

    static HistoricalLayer()
    {
      Constants = ConstantsRecordingLayer.Instance;
      _minimumScale = Constants.MinimumScale;
    }

    public HistoricalLayer(CycloMediaGroupLayer layer, Envelope initialExtent = null)
      : base(layer, initialExtent)
    {
      _historicalRecordings = HistoricalRecordings.Instance;
      MapViewTimeChangedEvent.Subscribe(OnTimeChanged);
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
    }

    ~HistoricalLayer()
    {
      MapViewTimeChangedEvent.Unsubscribe(OnTimeChanged);
      ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
    }

    private async void OnTimeChanged(MapViewTimeChangedEventArgs args)
    {
      SetTimeProperties(args.CurrentTime);
      MapView view = args.MapView;

      if (view != null)
      {
        await PostEntryStepAsync(view.Extent);
      }
    }

    private async void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      MapView view = args.IncomingView;

      if (view?.Time != null)
      {
        SetTimeProperties(view.Time);
        await PostEntryStepAsync(view.Extent);
      }
    }

    private void SetTimeProperties(TimeRange time)
    {
      DateTime dateFrom = time?.Start ?? _historicalRecordings.DateFrom;
      DateTime dateTo = time?.End ?? _historicalRecordings.DateTo;
      _historicalRecordings.Update(dateFrom, dateTo);
    }

    #endregion
  }
}
