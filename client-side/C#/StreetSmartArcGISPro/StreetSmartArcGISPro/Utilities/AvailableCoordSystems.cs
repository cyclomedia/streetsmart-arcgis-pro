using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

    private List<SpatialReference> _existsInAreaSpatialReferences;

    #endregion

    #region Properties

    public List<SpatialReference> ExistInAreaSpatialReferences
    {
      get => _existsInAreaSpatialReferences;
      set
      {
        _existsInAreaSpatialReferences = value;
        NotifyPropertyChanged();
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
      MapViewCameraChangedEvent.Subscribe(CreateExistsInAreaSpatialReferences);
    }

    private void Stop()
    {
      MapViewCameraChangedEvent.Unsubscribe(CreateExistsInAreaSpatialReferences);
    }

    private async void CreateExistsInAreaSpatialReferences(MapViewCameraChangedEventArgs args)
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
