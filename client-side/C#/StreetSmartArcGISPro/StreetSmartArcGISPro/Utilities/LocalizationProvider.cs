using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Logging;
using StreetSmartArcGISPro.Properties;
using System;
using System.Collections.Generic;
using System.Resources;
using System.Windows;
using System.Windows.Controls;

namespace StreetSmartArcGISPro.Utilities
{
  public static class LocalizationProvider
  {
    private static readonly List<DependencyObject> _toolTipObjects;
    private static readonly List<DependencyObject> _contentObjects;
    private static readonly LanguageSettings _languageSettings;

    static LocalizationProvider()
    {
      _toolTipObjects = [];
      _contentObjects = [];
      _languageSettings = LanguageSettings.Instance;
    }

    public static void UpdateAllObjects()
    {
      ResourceManager res = Resources.ResourceManager;

      foreach (DependencyObject obj in _contentObjects)
      {
        if (obj is ContentControl control)
        {
          if (obj.GetValue(ContentIdProperty) is string key)
          {
            string resourceValue = res.GetString(key, _languageSettings.CultureInfo);
            control.SetValue(ContentControl.ContentProperty, resourceValue);
          }
        }
        else if (obj is TextBlock textBlock)
        {
          if (obj.GetValue(ContentIdProperty) is string name)
          {
            string resourceValue = res.GetString(name, _languageSettings.CultureInfo);
            textBlock.Text = resourceValue;
          }
        }
      }

      foreach (DependencyObject obj in _toolTipObjects)
      {
        if (obj is ContentControl control)
        {
          if (obj.GetValue(TooltipIdProperty) is string key)
          {
            string resourceValue = res.GetString(key, _languageSettings.CultureInfo);
            control.SetValue(ContentControl.ToolTipProperty, resourceValue);
          }
        }
      }
    }

    #region ContentID property

    public static string GetContentId(DependencyObject obj)
    {
      return (string)obj.GetValue(ContentIdProperty);
    }

    public static void SetContentId(DependencyObject obj, string value)
    {
      obj.SetValue(ContentIdProperty, value);
    }

    public static string GetTooltipId(DependencyObject obj)
    {
      return (string)obj.GetValue(TooltipIdProperty);
    }

    public static void SetTooltipId(DependencyObject obj, string value)
    {
      obj.SetValue(TooltipIdProperty, value);
    }


    private static void OnContentIdChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
      ResourceManager res = Resources.ResourceManager;

      if (obj is ContentControl control && e.NewValue is string name)
      {
        string resourceValue = null;

        try
        {
          resourceValue = res.GetString(name, _languageSettings.CultureInfo);
        }
        catch (Exception ex)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (LocalizationProvider.cs) (OnContentIdChanged) error: {ex}");
        }

        if (resourceValue != null)
        {
          control.SetValue(ContentControl.ContentProperty, resourceValue);

          if (!_contentObjects.Contains(obj))
          {
            _contentObjects.Add(obj);
          }
        }
      }
      else if (obj is TextBlock textBlock && e.NewValue is string text)
      {
        string resourceValue = null;

        try
        {
          resourceValue = res.GetString(text, _languageSettings.CultureInfo);
        }
        catch (Exception ex)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (LocalizationProvider.cs) (OnContentIdChanged) error: {ex}");
        }

        if (resourceValue != null)
        {
          textBlock.Text = resourceValue;

          if (!_contentObjects.Contains(obj))
          {
            _contentObjects.Add(obj);
          }
        }
      }
    }

    private static void OnTooltipChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
      ResourceManager res = Resources.ResourceManager;

      if (obj is ContentControl control && e.NewValue is string name)
      {
        string resourceValue = null;

        try
        {
          resourceValue = res.GetString(name, _languageSettings.CultureInfo);
        }
        catch (Exception ex)
        {
          EventLog.Write(EventLogLevel.Error, $"Street Smart: (LocalizationProvider.cs) (OnTooltipChanged) error: {ex}");
        }

        if (resourceValue != null)
        {
          control.SetValue(ContentControl.ToolTipProperty, resourceValue);

          if (!_toolTipObjects.Contains(obj))
          {
            _toolTipObjects.Add(obj);
          }
        }
      }
    }

    public static DependencyProperty ContentIdProperty =
      DependencyProperty.RegisterAttached("ContentId", typeof(string), typeof(LocalizationProvider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange, OnContentIdChanged));

    public static DependencyProperty TooltipIdProperty =
      DependencyProperty.RegisterAttached("TooltipId", typeof(string), typeof(LocalizationProvider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange, OnTooltipChanged));

    #endregion
  }
}
