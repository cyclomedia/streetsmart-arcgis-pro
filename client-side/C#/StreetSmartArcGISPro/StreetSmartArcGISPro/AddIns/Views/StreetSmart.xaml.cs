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
using System.Resources;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Logging;

using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ModulestreetSmart = StreetSmartArcGISPro.AddIns.Modules.StreetSmart;
using ThisResources = StreetSmartArcGISPro.Properties.Resources;

namespace StreetSmartArcGISPro.AddIns.Views
{
  /// <summary>
  /// Interaction logic for streetSmart.xaml
  /// </summary>
  public partial class StreetSmart
  {
    #region Members

    private static bool _undockWarningShown;

    private bool _sourceChangedHandlerAdded;

    #endregion

    #region Constructor

    public StreetSmart()
    {
      InitializeComponent();
      Loaded += OnLoaded;
      Unloaded += OnUnloaded;
    }

    #endregion

    private void StreetSmartApi_MouseEnter(object sender, MouseEventArgs e)
    {
      ModulestreetSmart.Current.InsideViewer = true; // Set the flag in
    }

    private void StreetSmartApi_MouseLeave(object sender, MouseEventArgs e)
    {
      ModulestreetSmart.Current.InsideViewer = false; // Set the flag in
    }

    #region Dock state detection

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
      if (!_sourceChangedHandlerAdded)
      {
        PresentationSource.AddSourceChangedHandler(this, OnPresentationSourceChanged);
        _sourceChangedHandlerAdded = true;
      }

      // Covers a saved project layout where the pane is already floating at startup.
      CheckDockState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
      if (_sourceChangedHandlerAdded)
      {
        PresentationSource.RemoveSourceChangedHandler(this, OnPresentationSourceChanged);
        _sourceChangedHandlerAdded = false;
      }
    }

    private void OnPresentationSourceChanged(object sender, SourceChangedEventArgs e)
    {
      if (e.NewSource == null)
      {
        return;
      }

      CheckDockState();
    }

    private void CheckDockState()
    {
      try
      {
        if (_undockWarningShown)
        {
          return;
        }

        Window window = Window.GetWindow(this);
        bool isFloating = window != null && !ReferenceEquals(window, Application.Current?.MainWindow);
        bool isAutoHide = !isFloating && HasAutoHideAncestor();

        if (isFloating || isAutoHide)
        {
          _undockWarningShown = true;
          string detectedState = isFloating ? "floating" : "autoHide";
          EventLog.Write(EventLogLevel.Information, $"Cyclorama Viewer dock pane undocked state detected: {detectedState}, showing one-time warning");

          Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
          {
            try
            {
              ResourceManager res = ThisResources.ResourceManager;
              LanguageSettings language = LanguageSettings.Instance;
              string message = res.GetString("DockPaneFloatWarning", language.CultureInfo);
              string title = res.GetString("DockPaneFloatWarningTitle", language.CultureInfo);
              MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
              EventLog.Write(EventLogLevel.Error, $"Failed to show Cyclorama Viewer undock warning: {ex}");
            }
          }));
        }
      }
      catch (Exception ex)
      {
        EventLog.Write(EventLogLevel.Error, $"Cyclorama Viewer dock state detection failed: {ex}");
      }
    }

    private bool HasAutoHideAncestor()
    {
      DependencyObject current = this;

      while (current != null)
      {
        if (current.GetType().Name.IndexOf("AutoHide", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          return true;
        }

        DependencyObject parent = current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current)
            : null;
        current = parent ?? LogicalTreeHelper.GetParent(current);
      }

      return false;
    }

    #endregion
  }
}
