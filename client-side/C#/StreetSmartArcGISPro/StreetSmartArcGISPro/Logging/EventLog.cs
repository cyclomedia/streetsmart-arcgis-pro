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
    private static int LogsLimit = LogData.Instance.LogLimit;
    private static TimeUnit timeUnit = LogData.Instance.TimeUnit;
    private static int logCount = 0;
    private static DateTime lastReset;
    private static LogData _logData = LogData.Instance;
    private static bool isLimitReachedBefore = false;

    public static IDisposable InitializeSentry(string sentryDsnUrl)
    {
      try
      {
        lastReset = DateTime.Now;
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
      if (_logData.LogCount >= LogsLimit)
      {
        isLimitReachedBefore = true;
      }
      logCount = _logData.LogCount;
      lastReset = _logData.LastResetTime;
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
        if (logCount >= LogsLimit)
        {
          if (!isLimitReachedBefore)
          {
            ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Warning, $"Log rate limit reached for the {timeUnit}", true);
            SentrySdk.CaptureMessage($"Log rate limit exceeded for this {timeUnit}.", SentryLevel.Warning);
            isLimitReachedBefore = true;
          }
          return;
        }

        SentrySdk.CaptureMessage(entry, SentryLevel.Error);
        logCount++;
        _logData.LogCount = logCount;
        _logData.LastResetTime = lastReset;
        _logData.Save();
        return;
      }
    }
    private static void ResetCounterIfNeeded()
    {
      if (TimeElapsedSinceLastRestart(timeUnit) >= 1)
      {
        logCount = 0;
        lastReset = DateTime.Now;
        isLimitReachedBefore = false;
        _logData.LogCount = logCount;
        _logData.LastResetTime = lastReset;
        _logData.Save();
      }
    }

    private static double TimeElapsedSinceLastRestart(TimeUnit timeUnit) => timeUnit switch
    {
      TimeUnit.Minute => (DateTime.Now - lastReset).TotalMinutes,
      TimeUnit.Hour => (DateTime.Now - lastReset).TotalHours,
      TimeUnit.Day => (DateTime.Now - lastReset).TotalDays,
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
