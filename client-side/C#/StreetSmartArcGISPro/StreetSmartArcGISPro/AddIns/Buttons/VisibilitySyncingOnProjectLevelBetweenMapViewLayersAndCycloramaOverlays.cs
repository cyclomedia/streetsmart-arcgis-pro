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

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  internal class VisibilitySyncingOnProjectLevelBetweenMapViewLayersAndCycloramaOverlays : Button
  {
    private static readonly FileProjectList _projectList = FileProjectList.Instance;
    private static readonly FileSettings _settings = _projectList.GetSettings(MapView.Active);

    protected override void OnClick()
    {
      _settings.SyncLayerVisibility = !(_settings.SyncLayerVisibility ?? false);
      _projectList.Save();
    }
  }
}
