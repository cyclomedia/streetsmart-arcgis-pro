using ArcGIS.Desktop.Mapping;
using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StreetSmartArcGISPro.Utilities
{
  public class AvailableCoordSystems : INotifyPropertyChanged
  {
    #region events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region members

    private readonly Dictionary<MapView, List<SpatialReference>> _existsInAreaSpatialReferences = [];
    private static AvailableCoordSystems _instance;

    #endregion

    #region Properties

    public List<SpatialReference> ExistInAreaSpatialReferences
    {
      get
      {
        if (MapView.Active != null && _existsInAreaSpatialReferences.TryGetValue(MapView.Active, out var r))
        {
          return r;
        }

        return null;
      }
      set
      {
        if (MapView.Active != null && value != null)
        {
          _existsInAreaSpatialReferences[MapView.Active] = value;
        }
      }
    }

    public static AvailableCoordSystems Instance => _instance ??= new AvailableCoordSystems();

    #endregion

    #region functions

    public async Task CheckAvailableCoordinateSystems()
    {
      ExistInAreaSpatialReferences = [];
      var existsInAreaSpatialReferences = new List<SpatialReference>();
      SpatialReferenceDictionary spatialReferenceList = SpatialReferenceDictionary.Instance;

      foreach (var spatialReference in spatialReferenceList)
      {
        bool exists = await spatialReference.ExistsInAreaAsync();

        if (exists)
        {
          existsInAreaSpatialReferences.Add(spatialReference);
        }
      }

      ExistInAreaSpatialReferences = existsInAreaSpatialReferences;

      // ReSharper disable once ExplicitCallerInfoArgument
      NotifyPropertyChanged("ExistInAreaSpatialReferences");
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}
