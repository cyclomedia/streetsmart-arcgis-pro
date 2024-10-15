using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Utilities;
using System;
using System.IO;
using System.Xml.Serialization;
using static ArcGIS.Desktop.Framework.Utilities.EventLog;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("LogData")]
  public class LogData
  {
    [XmlElement("LogCount")]
    public int LogCount { get; set; }

    [XmlElement("LastResetTime")]
    public DateTime LastResetTime { get; set; }

    [XmlElement("UseSentryLogging")]
    public bool UseSentryLogging { get; set; }

    [XmlElement("LogLimit")]
    public int LogLimit { get; set; }

    [XmlElement("TimeUnit")]
    public TimeUnit TimeUnit { get; set; }

    public static string SentryDsnUrl => "https://96100a2b9133a1f056bf8df35d72d201@o4508125690593280.ingest.de.sentry.io/4508125706387536"; // TODO: change to proper production key

    private static string LogDataFileName => Path.Combine(FileUtils.FileDir, "logdata.xml");
    private static LogData _logData;

    private LogData() { }

    public static LogData Instance
    {
      get
      {
        if (_logData == null)
        {
          Load();
        }

        return _logData ??= Create();
      }
    }

    public static void Load()
    {
      try
      {
        if (System.IO.File.Exists(LogDataFileName))
        {
          using (FileStream streamFile = new FileStream(LogDataFileName, FileMode.Open))
          {
            XmlSerializer serializer = new XmlSerializer(typeof(LogData));
            _logData = (LogData)serializer.Deserialize(streamFile);
            streamFile.Close();
          }
        }
      }
      catch (Exception ex)
      {
        ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Error, $"Street Smart: (EventLog.cs) (SaveLogData) Failed to load log data: {ex} ");
      }
    }

    public void Save()
    {
      try
      {
        using (FileStream streamFile = new FileStream(LogDataFileName, FileMode.Create))
        {
          XmlSerializer serializer = new XmlSerializer(typeof(LogData));
          serializer.Serialize(streamFile, this);
          streamFile.Close();
        }
      }
      catch (Exception ex)
      {
        ArcGIS.Desktop.Framework.Utilities.EventLog.Write(EventType.Error, $"Street Smart: (EventLog.cs) (SaveLogData) Failed to save log data: {ex} ");
      }
    }

    private static LogData Create()
    {
      var result = new LogData
      {
        LogCount = 0,
        LastResetTime = DateTime.Now,
        UseSentryLogging = true,
        LogLimit = 20,
        TimeUnit = TimeUnit.Day
      };
      result.Save();
      return result;
    }
  }
}
