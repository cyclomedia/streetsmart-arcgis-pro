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

    protected VisibilitySyncing()
    {
      IsChecked = _settings?.SyncLayerVisibility == true ? true : false;

      ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
    }
    protected override async void OnClick()
    {
      _settings.SyncLayerVisibility = !(_settings.SyncLayerVisibility ?? false);
      _projectList.Save();
      IsChecked = !IsChecked;
      if (IsChecked)
        await DockPanestreetSmart.Current.UpdateAllVectorLayersAsync();
    }

    private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      _settings = _projectList.GetSettings(MapView.Active);
      IsChecked = _settings?.SyncLayerVisibility == true ? true : false;
    }
  }
}
