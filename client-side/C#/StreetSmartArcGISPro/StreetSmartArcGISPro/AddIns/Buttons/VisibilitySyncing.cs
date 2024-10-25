using System.ComponentModel;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using FileProjectList = StreetSmartArcGISPro.Configuration.File.ProjectList;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;
using FileConfiguration = StreetSmartArcGISPro.Configuration.File.Configuration;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class VisibilitySyncing : Button
  {
    protected VisibilitySyncing()
    {
      IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
      FileConfiguration.Instance.PropertyChanged += OnSyncOfVisibilityEnabledChanged;
    }
    protected override async void OnClick()
    {
      IsChecked = !IsChecked;

      var settings = FileProjectList.Instance.GetSettings(MapView.Active);
      settings.SyncLayerVisibility = IsChecked;
      FileProjectList.Instance.Save();

      if (IsChecked)
        await DockPanestreetSmart.Current.UpdateAllVectorLayersAsync();
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
    }

    private async void OnSyncOfVisibilityEnabledChanged(object sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName.Equals(nameof(FileConfiguration.IsSyncOfVisibilityEnabled)))
      {
        IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();

        if (IsChecked)
          await DockPanestreetSmart.Current.UpdateAllVectorLayersAsync();
      }
    }
  }
}
