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

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using Microsoft.Win32;
using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Properties;
using StreetSmartArcGISPro.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using EventLog = StreetSmartArcGISPro.Logging.EventLog;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class Help : Button
  {
    #region Properties

    private Process _process;

    #endregion

    #region Constructors

    protected Help()
    {
      _process = null;
    }

    #endregion

    #region Overrides

    protected override void OnClick()
    {
      ResourceManager res = Resources.ResourceManager;
      LanguageSettings language = LanguageSettings.Instance;

      try
      {
        OnUpdate();
        RegistryKey pdfKey = Registry.ClassesRoot.OpenSubKey(".pdf");

        if (pdfKey != null)
        {
          if (_process == null)
          {
            Type thisType = GetType();
            Assembly thisAssembly = Assembly.GetAssembly(thisType);
            const string manualName = "Street Smart for ArcGIS Pro User Manual.pdf";
            const string manualPath = @"StreetSmartArcGISPro.Resources." + manualName;
            Stream manualStream = thisAssembly.GetManifestResourceStream(manualPath);
            string fileName = Path.Combine(FileUtils.FileDir, manualName);

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

              var processInfo = new ProcessStartInfo
              {
                FileName = fileName,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = true //new code added for .net 6 that allows pdf to open
              };

              _process = Process.Start(processInfo);

              if (_process != null)
              {
                _process.EnableRaisingEvents = true;
                _process.Exited += ExitProcess;
              }
            }
          }
          else
          {
            _process.Kill();
          }
        }
        else
        {
          string errorPdfTxt = res.GetString("HelpNoPdfViewerInstalledOnYourSystem", language.CultureInfo);
          EventLog.Write(EventLogLevel.Warning, $"Street Smart: (Help.cs) (OnClick) {errorPdfTxt}");
          MessageBox.Show(errorPdfTxt);
        }
      }
      catch (Exception ex)
      {
        string errorTxt = res.GetString("HelpErrorOpenHelpDocument", language.CultureInfo);
        EventLog.Write(EventLogLevel.Error, $"Street Smart: (Help.cs) (OnClick) {errorTxt}");
        MessageBox.Show(ex.Message, errorTxt);
      }
    }

    #endregion

    #region eventHandlers

    private void ExitProcess(object sender, EventArgs e)
    {
      _process.Exited -= ExitProcess;
      _process = null;
      OnUpdate();
    }

    #endregion
  }
}
