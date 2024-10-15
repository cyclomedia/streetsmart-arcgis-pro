using Nancy.Xml;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Utilities;
using System;
using System.IO;
using System.Xml.Serialization;

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

  public string SentryDsnUrl
  {
    get
    {
      return "https://96100a2b9133a1f056bf8df35d72d201@o4508125690593280.ingest.de.sentry.io/4508125706387536"; // TODO: change to proper production key
    }
  }
  private static string LogDataFileName => Path.Combine(FileUtils.FileDir, "LogData.xml");
  private static LogData _logData;

  public LogData() { }

  public static LogData Instance
  {
    get
    {
      if (_logData == null)
      {
        LoadLogData();
      }

      return _logData ?? (_logData = Create());
    }
  }

  public static void LoadLogData()
  {
    try
    {
      if (File.Exists(LogDataFileName))
      {
        using (FileStream streamFile = new FileStream(LogDataFileName, FileMode.Open))
        {
          XmlSerializer serializer = new XmlSerializer(typeof(LogData));
          _logData = (LogData)serializer.Deserialize(streamFile);
          streamFile.Close();
        }
      }
    }
    catch
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (EventLog.cs) (LoadLogData)");
    }
  }

  public void SaveLogData()
  {
    try
    {
      FileStream streamFile = new FileStream(LogDataFileName, FileMode.Create);
      XmlSerializer serializer = new XmlSerializer(typeof(LogData));
      serializer.Serialize(streamFile, this);
      streamFile.Close();
    }

    catch
    {
      EventLog.Write(EventLogLevel.Error, $"Street Smart: (EventLog.cs) (SaveLogData)");
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
    result.SaveLogData();
    return result;
  }
}
