using Sentry;
using StreetSmartArcGISPro.Configuration.File;
using System;
using System.Runtime.CompilerServices;
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
    private static int _errorLogsLimit = LogData.Instance.LogLimit;
    private static TimeUnit _timeUnit = LogData.Instance.TimeUnit;
    private static int _logCount = 0;
    private static DateTime _lastLogLimitCheckTime;
    private static LogData _logData = LogData.Instance;
    private static bool _hasLogLimitBeenReached = false;

    public static IDisposable InitializeSentry(string sentryDsnUrl)
    {
      try
      {
        _lastLogLimitCheckTime = DateTime.Now;
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
    static EventLog()
    {
      _logData = LogData.Instance;
      _hasLogLimitBeenReached = _logData.LogCount >= _errorLogsLimit;
      _logCount = _logData.LogCount;
      _lastLogLimitCheckTime = _logData.LastResetTime;
    }

    public static void Write(EventLogLevel type, string entry, bool flush = false, [CallerMemberName] string methodName = "")
    {
      ArcGIS.Desktop.Framework.Utilities.EventLog.Write(MapEventLogTypeToEventLog(type), entry, flush);

      if (!SentrySdk.IsEnabled)
      {
        return;
      }
      ResetCounterIfNeeded();

      SentryLevel loggingLevel = MapEventLogTypeToSentryLevel(type);
      if (loggingLevel == SentryLevel.Error)
      {
        if (_logCount >= _errorLogsLimit)
        {
          HandleRateLimitExceeded();
          return;
        }

        SentrySdk.CaptureMessage(entry, SentryLevel.Error);
        IncrementLogCount();
        return;
      }
    }
    private static void ResetCounterIfNeeded()
    {
      if (TimeElapsedSinceLastRestart(_timeUnit) >= 1)
      {
        _logCount = 0;
        _lastLogLimitCheckTime = DateTime.Now;
        _hasLogLimitBeenReached = false;
        SaveLogState();
      }
    }

    private static void HandleRateLimitExceeded()
    {
      if (!_hasLogLimitBeenReached)
      {
        ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Warning, $"Log rate limit reached for the {_timeUnit}", true);
        SentrySdk.CaptureMessage($"Log rate limit exceeded for this {_timeUnit}.", SentryLevel.Warning);
        _hasLogLimitBeenReached = true;
      }
    }

    private static void IncrementLogCount()
    {
      _logCount++;
      SaveLogState();
    }

    private static void SaveLogState()
    {
      _logData.LogCount = _logCount;
      _logData.LastResetTime = _lastLogLimitCheckTime;
      _logData.Save();
    }

    private static double TimeElapsedSinceLastRestart(TimeUnit timeUnit) => timeUnit switch
    {
      TimeUnit.Minute => (DateTime.Now - _lastLogLimitCheckTime).TotalMinutes,
      TimeUnit.Hour => (DateTime.Now - _lastLogLimitCheckTime).TotalHours,
      TimeUnit.Day => (DateTime.Now - _lastLogLimitCheckTime).TotalDays,
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
