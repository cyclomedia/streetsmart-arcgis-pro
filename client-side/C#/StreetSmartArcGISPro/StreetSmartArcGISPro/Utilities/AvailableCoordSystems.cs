using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.Configuration.Remote.SpatialReference;

namespace StreetSmartArcGISPro.Utilities
{
  class AvailableCoordSystems : INotifyPropertyChanged
  {
    #region events

    public event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region members

    private Dictionary<MapView, List<SpatialReference>> _existsInAreaSpatialReferences;
    private static AvailableCoordSystems _instance;

    #endregion

    #region Properties

    public List<SpatialReference> ExistInAreaSpatialReferences
    {
      get
      {
        List<SpatialReference> result = null;

        if (MapView.Active != null && (_existsInAreaSpatialReferences?.ContainsKey(MapView.Active) ?? false))
        {
          result = _existsInAreaSpatialReferences[MapView.Active];
        }

        return result;
      }
      set
      {
        if (MapView.Active != null && value != null)
        {
          if (_existsInAreaSpatialReferences?.ContainsKey(MapView.Active) ?? false)
          {
            _existsInAreaSpatialReferences[MapView.Active] = value;
          }
          else
          {
            if (_existsInAreaSpatialReferences == null)
            {
              _existsInAreaSpatialReferences = new Dictionary<MapView, List<SpatialReference>>();
            }

            _existsInAreaSpatialReferences.Add(MapView.Active, value);
          }
        }
      }
    }

    public static AvailableCoordSystems Instance => _instance ?? (_instance = new AvailableCoordSystems());

    #endregion

    #region functions

    public async Task CheckAvailableCoordinateSystems()
    {
      ExistInAreaSpatialReferences = new List<SpatialReference>();
      var existsInAreaSpatialReferences = new List<SpatialReference>();
      SpatialReferenceList spatialReferenceList = SpatialReferenceList.Instance;

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
