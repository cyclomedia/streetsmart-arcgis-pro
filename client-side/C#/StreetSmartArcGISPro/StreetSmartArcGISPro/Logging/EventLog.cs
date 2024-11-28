using ArcGIS.Desktop.Mapping;
using Sentry;
using StreetSmartArcGISPro.Configuration.File;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static ArcGIS.Desktop.Framework.Utilities.EventLog;

namespace StreetSmartArcGISPro.Logging
{
  public enum TimeUnit : short
  {
    Minute,
    Hour,
    Day
  }

  public enum EventLogLevel : short
  {
    Debug,
    Information,
    Error,
    Warning
  }

  public static class EventLog
  {
    private static bool _hasLogLimitBeenReached = LogData.Instance.LogCount >= LogData.Instance.LogLimit;

    public static IDisposable InitializeSentry(string sentryDsnUrl)
    {
      try
      {
        LogData.Instance.LastResetTime = DateTime.Now;
        return SentrySdk.Init(options =>
        {
          options.Dsn = sentryDsnUrl;
          options.Debug = true;
          options.TracesSampleRate = 1.0;
          options.ProfilesSampleRate = 1.0;
        });
      }
      catch (Exception ex)
      {
        ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Warning, $"Sentry is not initialized properly: {ex.GetBaseException()}", true);
        return null;
      }
    }

    public static void Write(EventLogLevel type, string entry, bool flush = false, [CallerMemberName] string methodName = "")
    {
      ArcGIS.Desktop.Framework.Utilities.EventLog.Write(MapEventLogTypeToEventLog(type), entry, flush);

      if (!SentrySdk.IsEnabled)
      {
        return;
      }

      if (TimeElapsedSinceLastRestart(LogData.Instance.TimeUnit) >= 1)
      {
        ResetCounterIfNeeded();
      }

      SentryLevel loggingLevel = MapEventLogTypeToSentryLevel(type);
      if (loggingLevel == SentryLevel.Error)
      {
        if (LogData.Instance.LogCount >= LogData.Instance.LogLimit)
        {
          HandleRateLimitExceeded();
        }
        else
        {
          entry = AddDataToLogMessage(entry);
          SentrySdk.CaptureMessage(entry, SentryLevel.Error);
          IncrementLogCount();
        }
        
        SaveIfFlushRequested(flush);
      }
    }
    
    private static string AddDataToLogMessage(string logEntry)
    {
      var logMessageBuilder = new StringBuilder(logEntry);

      AddProjectDetails(logMessageBuilder);
      AddCoordinateSystems(logMessageBuilder);
      AddVectorLayerDetails(logMessageBuilder);
      AddViewerDetails(logMessageBuilder);

      return logMessageBuilder.ToString();
    }

    private static void AddProjectDetails(StringBuilder logMessageBuilder)
    {
      var projectName = ArcGIS.Desktop.Core.Project.Current?.Name;
      if (string.IsNullOrEmpty(projectName))
      {
        logMessageBuilder.AppendLine("\nThe project name is not specified or does not exist.");
      }
      else
      {
        logMessageBuilder.AppendLine($"\nProject name: {projectName}");
      }
    }

    private static void AddCoordinateSystems(StringBuilder logMessageBuilder)
    {
      var projectSettings = ProjectList.Instance?.GetSettings(MapView.Active);
      if (projectSettings == null)
      {
        logMessageBuilder.AppendLine("\nProject settings could not be retrieved.");
        return;
      }

      if (projectSettings.CycloramaViewerCoordinateSystem != null)
      {
        logMessageBuilder.AppendLine($"\nCoordinate system for the cyclorama viewer: {projectSettings.CycloramaViewerCoordinateSystem}");
      }

      if (projectSettings.RecordingLayerCoordinateSystem != null)
      {
        logMessageBuilder.AppendLine($"\nCoordinate system for the recording layer: {projectSettings.RecordingLayerCoordinateSystem}");
      }
    }

    private static void AddVectorLayerDetails(StringBuilder logMessageBuilder)
    {
      var vectorLayers = StreetSmartArcGISPro.AddIns.Modules.StreetSmart.Current?
          .GetVectorLayerListAsync(MapView.Active)?
          .Result
          .FirstOrDefault()
          .Value;

      if (vectorLayers == null || !vectorLayers.Any())
      {
        logMessageBuilder.AppendLine("\nNo vector layers found in the map.");
        return;
      }

      int totalLayers = vectorLayers.Count;
      int visibleOnMapCount = vectorLayers.Count(layer => layer.IsLayerVisible);
      int visibleInCycloramaCount = vectorLayers.Count(layer => layer.Overlay.Visible);

      logMessageBuilder.AppendLine($"\nTotal Layers: {totalLayers}");
      logMessageBuilder.AppendLine($" Visible on Map: {visibleOnMapCount}");
      logMessageBuilder.AppendLine($" Visible in Cyclorama Viewer: {visibleInCycloramaCount}");

      foreach (var layer in vectorLayers)
      {
        logMessageBuilder.AppendLine($"\nFeature Name: {layer.Name}");
        logMessageBuilder.AppendLine($" Type: {layer.Layer.ShapeType}");
        logMessageBuilder.AppendLine($" Layer Visibility: {layer.IsLayerVisible}");
        logMessageBuilder.AppendLine($" Overlay Visibility: {layer.Overlay.Visible}");
      }
    }

    private static void AddViewerDetails(StringBuilder logMessageBuilder)
    {
      var viewerList = StreetSmartArcGISPro.AddIns.Modules.StreetSmart.Current.ViewerList;

      if (viewerList == null || !viewerList.Any())
      {
        logMessageBuilder.AppendLine("\nNo viewers opened");
        return;
      }

      logMessageBuilder.AppendLine($"\nViewers count: {viewerList.Count}\n");
      foreach (var viewer in viewerList)
      {
        logMessageBuilder.AppendLine($"Viewer type: {viewer.Key}");
      }
    }

    private static void ResetCounterIfNeeded()
    {
      LogData.Instance.LogCount = 0;
      LogData.Instance.LastResetTime = DateTime.Now;
      _hasLogLimitBeenReached = false;
    }

    private static void HandleRateLimitExceeded()
    {
      if (!_hasLogLimitBeenReached)
      {
        ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Warning, $"Log rate limit reached for the {LogData.Instance.TimeUnit}", true);
        SentrySdk.CaptureMessage($"Log rate limit exceeded for this {LogData.Instance.TimeUnit}.", SentryLevel.Warning);
        _hasLogLimitBeenReached = true;
      }
    }

    private static void IncrementLogCount()
    {
      LogData.Instance.LogCount++;
    }

    private static void SaveIfFlushRequested(bool flush)
    {
      if (flush)
      {
        LogData.Instance.Save();
      }
    }
    private static double TimeElapsedSinceLastRestart(TimeUnit timeUnit) => timeUnit switch
    {
      TimeUnit.Minute => (DateTime.Now - LogData.Instance.LastResetTime).TotalMinutes,
      TimeUnit.Hour => (DateTime.Now - LogData.Instance.LastResetTime).TotalHours,
      TimeUnit.Day => (DateTime.Now - LogData.Instance.LastResetTime).TotalDays,
      _ => throw new ArgumentException($"Value {timeUnit.GetType()} not expected", nameof(timeUnit))
    };

    private static SentryLevel MapEventLogTypeToSentryLevel(EventLogLevel type) => type switch
    {
      EventLogLevel.Error => SentryLevel.Error,
      EventLogLevel.Warning => SentryLevel.Warning,
      EventLogLevel.Information => SentryLevel.Info,
      EventLogLevel.Debug => SentryLevel.Debug,
      _ => throw new ArgumentException($"Value {type.GetType()} not expected", nameof(type))
    };

    private static EventType MapEventLogTypeToEventLog(EventLogLevel type) => type switch
    {
      EventLogLevel.Error => EventType.Error,
      EventLogLevel.Information => EventType.Information,
      EventLogLevel.Warning => EventType.Warning,
      EventLogLevel.Debug => EventType.Debug,
      _ => throw new ArgumentException($"Value {type.GetType()} not expected", nameof(type))
    };
  }
}
