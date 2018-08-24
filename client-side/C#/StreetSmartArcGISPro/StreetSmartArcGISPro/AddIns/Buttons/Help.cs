/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018, CycloMedia, All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Reflection;

using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Contracts;

using StreetSmartArcGISPro.Properties;
using StreetSmartArcGISPro.Utilities;

using Microsoft.Win32;

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
          MessageBox.Show(Resources.Help_pdf_viewer_is_not_installed_on_your_system_);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, Resources.streetSmart_ArcGISPro_Error_);
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
