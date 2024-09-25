using ArcGIS.Desktop.Framework.Contracts;
using StreetSmartArcGISPro.Configuration.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileSettings = StreetSmartArcGISPro.Configuration.File.Setting;
using FileProjectList = StreetSmartArcGISPro.Configuration.File.ProjectList;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using DockPanestreetSmart = StreetSmartArcGISPro.AddIns.DockPanes.StreetSmart;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class VisibilitySyncing : Button
  {
    private readonly FileProjectList _projectList;
    private FileSettings _settings;

    protected VisibilitySyncing()
    {
      _projectList = FileProjectList.Instance;
      _settings = _projectList.GetSettings(MapView.Active);

      IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
    }
    protected override async void OnClick()
    {
      _settings.SyncLayerVisibility = !(DockPanestreetSmart.Current.ShouldSyncLayersVisibility());
      _projectList.Save();
      IsChecked = !IsChecked;
      if (IsChecked)
        await DockPanestreetSmart.Current.UpdateAllVectorLayersAsync();
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      IsChecked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
    }
  }
}
