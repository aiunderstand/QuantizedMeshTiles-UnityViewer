using BruTile;
namespace Terrain
{
    public class TmsOpenStreetMap : TileSchema
    {
        public TmsOpenStreetMap()
        {
            OriginX = -20037508.342789;
            OriginY = 20037508.342789;
            YAxis = YAxis.OSM;
            Extent = new Extent(-20037508.342789, -20037508.342789, 20037508.342789, 20037508.342789);
            Format = "png";

            double[] _unitsPerPixelArray =
       {
            156543.033900000, 78271.516950000, 39135.758475000, 19567.879237500, 9783.939618750,
            4891.969809375, 2445.984904688, 1222.992452344, 611.496226172, 305.748113086,
            152.874056543, 76.437028271, 38.218514136, 19.109257068, 9.554628534, 4.777314267,
            2.388657133, 1.194328567, 0.597164283};


            for (var p = 0; p < _unitsPerPixelArray.Length; p++)
            {
                Resolutions.Add(p.ToString(), new Resolution(p.ToString(), _unitsPerPixelArray[p]));
            };

            Srs = "EPSG:900913";
        }
    }
}
