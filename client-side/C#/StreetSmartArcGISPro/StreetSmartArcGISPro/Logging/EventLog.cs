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
          SentrySdk.CaptureMessage(entry, SentryLevel.Error);
          IncrementLogCount();
        }
        SaveIfFlushRequested(flush);
        
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
