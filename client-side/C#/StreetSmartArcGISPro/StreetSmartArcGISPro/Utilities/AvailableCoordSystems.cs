using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

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

          NotifyPropertyChanged();
        }
      }
    }

    public static AvailableCoordSystems Instance { get; private set; }

    #endregion

    #region functions

    public static void Init()
    {
      if (Instance == null)
      {
        Instance = new AvailableCoordSystems();
        Instance.Start();
      }
    }

    public static void Destroy()
    {
      if (Instance != null)
      {
        Instance.Stop();
        Instance = null;
      }
    }

    private void Start()
    {
      MapViewCameraChangedEvent.Subscribe(MapViewCameraChanged);
      ActiveMapViewChangedEvent.Subscribe(ActiveMapViewChanged);
    }

    private void Stop()
    {
      MapViewCameraChangedEvent.Unsubscribe(MapViewCameraChanged);
      ActiveMapViewChangedEvent.Unsubscribe(ActiveMapViewChanged);
    }

    private void MapViewCameraChanged(MapViewCameraChangedEventArgs args)
    {
      CreateExistsInAreaSpatialReferences();
    }

    private void ActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
    {
      CreateExistsInAreaSpatialReferences();
    }

    private async void CreateExistsInAreaSpatialReferences()
    {
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
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    #endregion
  }
}
