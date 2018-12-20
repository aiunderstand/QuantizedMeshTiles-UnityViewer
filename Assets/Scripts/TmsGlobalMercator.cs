using BruTile;

namespace Terrain
{
    public class WebMercator : TileSchema
    {
        public WebMercator()
        {
            OriginX = -180;
            OriginY = -85.051129;
            YAxis = YAxis.TMS;
            Extent = new Extent(-180, -85.051129, 180, 85.051129);
            var f = 0.70312500000000000000;

            for (var p = 0; p <= 20; p++)
            {
                Resolutions.Add(p.ToString(), new Resolution(p.ToString(), f));
                f = f / 2;
            }

            Srs = "EPSG:3857";
        }
    }
}
