using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace StreetSmartArcGISPro.Configuration.Remote.Models
{
    [XmlRoot("GlobeSpotterConfiguration", Namespace = "https://www.globespotter.com/gsc")]
    public class GlobeSpotterConfiguration
    {
        [XmlElement("ServicesConfiguration")]
        public ServicesConfiguration ServicesConfiguration { get; set; }
    }

    public class ServicesConfiguration
    {
        [XmlElement("RecordingLocationService")]
        public RecordingLocationService RecordingLocationService { get; set; }
    }
    public class RecordingLocationService
    {
        public string Name { get; set;}
        public string Description { get; set; }
        public OnlineResource OnlineResource { get; set; }
    }

    public class OnlineResource
    {
        [XmlAttribute("href", Namespace = "http://www.w3.org/1999/xlink")]
        public string  ResourceLink { get; set; }
    }
}
