using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using FileProjectList = StreetSmartArcGISPro.Configuration.File.ProjectList;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class VisibilitySyncing : Button
  {
    protected VisibilitySyncing()
    {
      IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
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
  }
}
