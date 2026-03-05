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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Utilities
{
  internal class FileUtils
  {
    #region Constants

    private const int MaxRetries = 3;
    private const int RetryDelayMs = 150;

    #endregion

    #region Properties

    public static string FileDir
    {
      get
      {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string result = Path.Combine(folder, "StreetSmartArcGISPro");

        if (!Directory.Exists(result))
        {
          Directory.CreateDirectory(result);
        }

        return result;
      }
    }

    public static void GetFileFromAddIn(string addInFile, string relOutPath)
    {
      Type thisType = typeof (FileUtils);
      Assembly thisAssembly = Assembly.GetAssembly(thisType);
      string manualPath = $@"StreetSmartArcGISPro.Resources.{addInFile}";
      Stream manualStream = thisAssembly.GetManifestResourceStream(manualPath);
      string fileName = Path.Combine(FileDir, relOutPath);
      string fileDirectory = Path.GetDirectoryName(fileName);

      if (fileDirectory != null)
      {
        if (!Directory.Exists(fileDirectory))
        {
          Directory.CreateDirectory(fileDirectory);
        }

        if (File.Exists(fileName))
        {
          File.Delete(fileName);
        }

        if (manualStream != null)
        {
          var fileStream = new FileStream(fileName, FileMode.CreateNew);
          const int readBuffer = 2048;
          var buffer = new byte[readBuffer];
          int readBytes;

          do
          {
            readBytes = manualStream.Read(buffer, 0, readBuffer);
            fileStream.Write(buffer, 0, readBytes);
          } while (readBytes != 0);

          fileStream.Flush();
          fileStream.Close();
        }
      }
    }

  
    /// XML FileShare and retry logic
    /// to support multiple ArcGIS Pro instances.
  
    public static void SafeSerializeToFile<T>(string fileName, XmlSerializer serializer, T obj)
    {
      for (int attempt = 0; attempt < MaxRetries; attempt++)
      {
        try
        {
          using var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
          serializer.Serialize(stream, obj);
          return;
        }
        catch (IOException) when (attempt < MaxRetries - 1)
        {
          Thread.Sleep(RetryDelayMs * (attempt + 1));
        }
      }
    }

    public static T SafeDeserializeFromFile<T>(string fileName, XmlSerializer serializer) where T : class
    {
      for (int attempt = 0; attempt < MaxRetries; attempt++)
      {
        try
        {
          if (!File.Exists(fileName))
            return null;

          using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
          return (T)serializer.Deserialize(stream);
        }
        catch (IOException) when (attempt < MaxRetries - 1)
        {
          Thread.Sleep(RetryDelayMs * (attempt + 1));
        }
      }

      return null;
    }

  
    /// Removes stale per-process CEF cache directories from previous ArcGIS Pro sessions.
   
    public static void CleanupStaleCacheDirs()
    {
      try
      {
        string baseDir = FileDir;
        HashSet<int> runningPids = Process.GetProcessesByName("ArcGISPro")
            .Select(p => p.Id)
            .ToHashSet();

        foreach (string dir in Directory.GetDirectories(baseDir, "Cache_*"))
        {
          string dirName = Path.GetFileName(dir);
          string pidStr = dirName.Replace("Cache_", "");

          if (int.TryParse(pidStr, out int pid) && !runningPids.Contains(pid))
          {
            try { Directory.Delete(dir, true); }
            catch { /* ignore, may still be locked */ }
          }
        }
      }
      catch { /* non-critical cleanup */ }
    }

    #endregion
  }
}
