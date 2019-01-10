using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Terrain
{
    public enum DataService
    {
        TMS,
        WMS,
        B3DM_3DTILE,
        PNTS_3DTILE,
        QM
    }

    public enum DataType
    {
        Terrain,
        Surface,
        Buildings,
        BuildingRootTile,
        Trees,
        TreeCollection
    }

    [Serializable]
    public class Settings
    {
        public Datasource[] DataSources { get; set; }
        public Location[] Locations { get; set; }
    }

    [Serializable]
    public class Datasource
    {
        public string Name { get; set; }
        public DataType DataType { get; set; }
        public DataService Service { get; set; }
        public string[] Url { get; set; }
        public string[] Collection {get;set;}
        public int EPSG { get; set; }
        public int minZoom { get; set; }
        public int maxZoom { get; set; }
        public Wgs84bounds WGS84Bounds { get; set; }
    }

    [Serializable]
    public class Wgs84bounds
    {
        public double minLon { get; set; }
        public double minLat { get; set; }
        public double maxLon { get; set; }
        public double maxLat { get; set; }
    }

    [Serializable]
    public class Location
    {
        public string Name { get; set; }
        public Wgs84bounds WGS84Bounds { get; set; }
    }
}
