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
    private static readonly FileProjectList _projectList = FileProjectList.Instance;
    private static FileSettings _settings = _projectList.GetSettings(MapView.Active);
    private bool _checked = true;

    protected VisibilitySyncing()
    {
      _checked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      IsChecked = _checked;
      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
    }
    protected override async void OnClick()
    {
      _checked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      _settings.SyncLayerVisibility = !_checked;
      _projectList.Save();
      IsChecked = !IsChecked;
      if (IsChecked)
        await DockPanestreetSmart.Current.UpdateAllVectorLayersAsync();
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      _checked = DockPanestreetSmart.Current.ShouldSyncLayersVisibility();
      IsChecked = _checked;
    }
  }
}
