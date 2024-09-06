using Sentry;
using System;
using System.Runtime.CompilerServices;
using static ArcGIS.Desktop.Framework.Utilities.EventLog;

namespace StreetSmartArcGISPro.Logging
{
  public enum EventLogLevel : short
  {
    Debug,
    Information,
    Error,
    Warning
  }

  public static class EventLog
  {
    public static IDisposable InitializeSentry(string sentryDsnUrl)
    {
      try
      {
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
      SentryLevel loggingLevel = MapEventLogTypeToSentryLevel(type);

      if(!SentrySdk.IsEnabled)
      {
        return;
      }

      if (loggingLevel == SentryLevel.Error)
      {
        SentrySdk.CaptureMessage(entry, SentryLevel.Error);
        return;
      }
    }

    private static SentryLevel MapEventLogTypeToSentryLevel(EventLogLevel type) => type switch
    {
      EventLogLevel.Error => SentryLevel.Error,
      EventLogLevel.Warning => SentryLevel.Warning,
      EventLogLevel.Information => SentryLevel.Info,
      EventLogLevel.Debug => SentryLevel.Debug,
      _ => throw new ArgumentException(nameof(type))
    };

    private static EventType MapEventLogTypeToEventLog(EventLogLevel type) => type switch
    {
      EventLogLevel.Error => EventType.Error,
      EventLogLevel.Information => EventType.Information,
      EventLogLevel.Warning => EventType.Warning,
      EventLogLevel.Debug => EventType.Debug,
      _ => throw new ArgumentException(nameof(type))
    };
  }
}
