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
using System.IO;
using System.Reflection;

namespace StreetSmartArcGISPro.Utilities
{
  internal class FileUtils
  {
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

    #endregion
  }
}
