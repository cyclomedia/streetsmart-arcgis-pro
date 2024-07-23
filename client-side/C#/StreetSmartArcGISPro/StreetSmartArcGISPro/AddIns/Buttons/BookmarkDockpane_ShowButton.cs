using ArcGIS.Desktop.Framework.Contracts;
using StreetSmartArcGISPro.AddIns.ViewModels;

namespace StreetSmartArcGISPro.AddIns.Buttons
{
  /// <summary>
  /// Button implementation to show the DockPane.
  /// </summary>
  internal class BookmarkDockpane_ShowButton : Button
  {
    protected override void OnClick()
    {
      BookmarkDockpaneViewModel.Show();
    }
  }
}
