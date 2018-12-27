using BruTile;
using Terrain.ExtensionMethods;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terrain.Tiles;
using UnityEngine;
using UnityEngine.Networking;
using System;
using JetBrains.Annotations;
using Assets.Scripts;
using DotSpatial.Projections;

namespace Terrain
{
    public class MapViewer : MonoBehaviour
    {
        [SerializeField]
        private Extent extent;

        private readonly Extent Amsterdam = new Extent(4.88, 52.36, 4.92, 52.38); //Amsterdam, Netherlands
        private readonly Extent Ispra = new Extent(8.57, 45.78, 8.67, 45.85); //EU Joint Research Centre in Ispra, Italy
        private readonly Extent Kongsberg = new Extent(9.61, 59.64, 9.70, 59.69); //Kongsberg, Norway
        private readonly Extent All = new Extent(-180, -90, 180, 90); //All


        private int zoomLevel = 0;
        [SerializeField]
        private GameObject placeholderTile;

        private string terrainUrl;
        bool useWorld = true;
        private string geodanTerrain = @"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0"; //max zoom is 17!
        private string cesiumTerrain = @"https://maps.tilehosting.com/data/terrain-quantized-mesh/{z}/{x}/{y}.terrain?key=irAs6FzTF3gJ9ArfQPjh"; //max zoom is 13!       

        private string textureUrl;
        bool useSatellite = false;
        private string geodanTerrainCover= @"https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox={xMin}%2C{yMin}%2C{xMax}%2C{yMax}&width=256&height=256&srs=EPSG%3A4326";
        private string cesiumTerrainCover = @"https://a.tile.openstreetmap.org/{z}/{x}/{y}.png";
        //private string cesiumTerrainCover = @"https://maps.tilehosting.com/data/satellite/{z}/{x}/{y}.jpg?key=irAs6FzTF3gJ9ArfQPjh";


        private string buildingsUrl = @"https://saturnus.geodan.nl/tomt/data/buildingtiles_adam/tiles/{id}.b3dm";
        private const int tilesize = 1;

        //implement will use floating origin
        Vector2 floatingOrigin;
        const float UnityUnitsPerGraadX = 100; //68600
        const float UnityUnitsPerGraadY = 100; //111300
        const float UnityUnitsPerGraadZ = 10; //111300
        readonly Dictionary<Vector3, GameObject> tileDb = new Dictionary<Vector3, GameObject>();

        const int maxParallelRequests = 4;
        readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();
        readonly Dictionary<string, DownloadRequest> pendingQueue = new Dictionary<string, DownloadRequest>(maxParallelRequests);

        [UsedImplicitly]
        private void Awake()
        {
            //hack 
            //floatingOrigin = new Vector2((float)Amsterdam.CenterX, (float)Amsterdam.CenterY);
            //extent = Amsterdam;
            floatingOrigin = new Vector2((float)All.CenterX, (float)All.CenterY);
            extent = All;

            LoadMap();
        }

        private void LoadMap()
        {
            ClearTiles();

            //settings should be refactored to settings class
            ProcessSettings();

            var schema = new TmsGlobalGeodeticTileSchema();
            var tiles = schema.GetTileInfos(extent, zoomLevel.ToString()).ToList();

            RequestTiles(schema, tiles);


            if (useSatellite)
            {
                var schemaOsm = new TmsOpenStreetMap();

                var cMin = DegreesToMeters(extent.MinX, extent.MinX < -85.06? -85.06 : extent.MinX);
                var cMax = DegreesToMeters(extent.MaxX, extent.MinX > 85.06 ? 85.06 : extent.MaxX);
                var extentOsm = new Extent(cMin.x, cMin.y, cMax.x, cMax.y);
                var tileRange = TileTransform.WorldToTile(extentOsm, (zoomLevel).ToString(), schemaOsm);
                var rowMax = tileRange.FirstRow + tileRange.RowCount -1;

                var tiles2 = schemaOsm.GetTileInfos(extentOsm, (zoomLevel).ToString()).ToList();
                Debug.Log("tms count:" + tiles2.Count);
                foreach (var t in tiles2)
                {
                    var url = textureUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                    downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.TMS, new Vector3(t.Index.Col * 2, (rowMax - t.Index.Row - 1), int.Parse(t.Index.Level)), new Extent()));
                }
            }

            //What is the difference between TMS and Google Maps name convention?
            //The tile raster itself is the same (equal extent, projection, pixel size),
            //there is just different identification of the same raster tile.
            //Tiles in TMS are counted from[0, 0] in the bottom-left corner, id is XYZ.
            //Google placed the origin[0, 0] to the top - left corner, reference is XYZ.

            //What coordinate conversions do we need for TMS Global Geodetic tiles ?
            //Global Geodetic tiles are using geodetic coordinates (latitude, longitude)
            //directly as planar coordinates XY(it is also called Unprojected or Plate
            //Carre). We need only scaling to pixel pyramid and cutting to tiles.
            //Pyramid has on top level two tiles, so it is not square but rectangle.
            //Area[-180, -90, 180, 90] is scaled to 512x256 pixels.
            //TMS has coordinate origin(for pixels and tiles) in bottom - left corner.
            //Rasters are in EPSG: 4326 and therefore are compatible with Google Earth.

            //Conclusion:
            //1 tile becomes 2 tiles with transformation -> scale * 2 in x direction, split in 2.
            //tile position: from origin bottom left -> top left. 
            // y = RowLength -posY;
            // x = posX *2 and skip  if x is odd

            //if (t.Index.Col < (tileRange.FirstCol + tileRange.ColCount) / 2)
            //{
            //    var url = textureUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
            //    downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.TMS, new Vector3(t.Index.Col * 2, (rowMax - t.Index.Row - 1), int.Parse(t.Index.Level))));
            //}
            //}
        }

        private void RequestTiles(TmsGlobalGeodeticTileSchema schema, List<TileInfo> tiles)
        {
            //var tileRange = TileTransform.WorldToTile(extent, zoomLevel.ToString(), schema);
            //var rowMax = tileRange.FirstRow + tileRange.RowCount;

            //immediately draw placeholder tile and fire request for texture and height. Depending on which one returns first, update place holder.
            foreach (var t in tiles)
            {
                //draw placeholder tile
                var tile = DrawPlaceHolder(t);

                tileDb.Add(new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level)), tile);
                var subtileExtent = TileTransform.TileToWorld(new TileRange(t.Index.Col, t.Index.Row), t.Index.Level.ToString(), schema);

                //get tile texture data
                if (!useSatellite)
                {
                    
                    var url = textureUrl.Replace("{xMin}", subtileExtent.MinX.ToString()).Replace("{yMin}", subtileExtent.MinY.ToString()).Replace("{xMax}", subtileExtent.MaxX.ToString()).Replace("{yMax}", subtileExtent.MaxY.ToString()).Replace(",", "."); //WMS
                    downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.TMS, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level)), new Extent()));
                }
                //else
                //{
                //    //What is the difference between TMS and Google Maps name convention?
                //    //The tile raster itself is the same (equal extent, projection, pixel size),
                //    //there is just different identification of the same raster tile.
                //    //Tiles in TMS are counted from[0, 0] in the bottom-left corner, id is XYZ.
                //    //Google placed the origin[0, 0] to the top - left corner, reference is XYZ.

                //    //What coordinate conversions do we need for TMS Global Geodetic tiles ?
                //    //Global Geodetic tiles are using geodetic coordinates (latitude, longitude)
                //    //directly as planar coordinates XY(it is also called Unprojected or Plate
                //    //Carre). We need only scaling to pixel pyramid and cutting to tiles.
                //    //Pyramid has on top level two tiles, so it is not square but rectangle.
                //    //Area[-180, -90, 180, 90] is scaled to 512x256 pixels.
                //    //TMS has coordinate origin(for pixels and tiles) in bottom - left corner.
                //    //Rasters are in EPSG: 4326 and therefore are compatible with Google Earth.

                //    //Conclusion:
                //    //1 tile becomes 2 tiles with transformation -> scale * 2 in x direction, split in 2.
                //    //tile position: from origin bottom left -> top left. 
                //    // y = RowLength -posY;
                //    // x = posX *2 and skip  if x is odd

                //    if (t.Index.Col < (tileRange.FirstCol+tileRange.ColCount)/2)
                //    {
                //        var url = textureUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                //        downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.TMS, new Vector3(t.Index.Col * 2, (rowMax - t.Index.Row - 1), int.Parse(t.Index.Level))));
                //    }
                //}


                //get tile height data (
             
                var qmUrl = terrainUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                downloadQueue.Enqueue(new DownloadRequest(qmUrl, TileServiceType.QM, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level)),subtileExtent));
            }
        }

        private void ProcessSettings()
        {
            terrainUrl = useWorld ? cesiumTerrain : geodanTerrain;
            textureUrl = useSatellite ? cesiumTerrainCover : geodanTerrainCover;
        }

        private GameObject DrawPlaceHolder(TileInfo t)
        {
            var tile = Instantiate(placeholderTile);
            tile.name = $"tile/{t.Index.ToIndexString()}";
            tile.transform.position = GetTilePosition(t.Index);
            tile.transform.localScale = new Vector3(ComputeScaleFactorX(int.Parse(t.Index.Level)), 1, ComputeScaleFactorY(int.Parse(t.Index.Level)));
            return tile;
        }

        private void ClearTiles()
        {
            tileDb.ToList().ForEach(t => Destroy(t.Value));
            tileDb.Clear();
            downloadQueue.Clear();
            pendingQueue.Clear();         
        }

        private Vector3 GetTilePosition(TileIndex index)
        {
            var tegelbreedte = tilesize / Math.Pow(2, int.Parse(index.Level)); //tegelbreedte in graden
            var originX = ((index.Col + 0.5) * tegelbreedte);
            var originY = ((index.Row + 0.5) * tegelbreedte);
            var X = (originX - floatingOrigin.x) * UnityUnitsPerGraadX;
            var Y = (originY - floatingOrigin.y) * UnityUnitsPerGraadY;
            return new Vector3((float)X, 0, (float)Y);
        }

        private IEnumerator RequestQMTile(string url, Vector3 tileId, Extent subextent)
        {
            DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
            TerrainTile terrainTile;
            var www = new UnityWebRequest(url)
            {
                downloadHandler = handler
            };
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                //get data
                var stream = new MemoryStream(www.downloadHandler.data);

                //parse into tile data structure
                terrainTile = TerrainTileParser.Parse(stream);

                //update tile with height data
                tileDb[tileId].GetComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh(subextent); //height offset is manually done to nicely align height data with place holder at 0
                tileDb[tileId].transform.localScale = new Vector3(ComputeScaleFactorX((int)tileId.z), 0.01f, ComputeScaleFactorY((int)tileId.z));
            }
            else
            {
                Debug.Log("Tile: [" + tileId.x + " " + tileId.y + "] Error loading height data");
            }

            pendingQueue.Remove(url);           
        }

        private float ComputeScaleFactorX(int z)
        {
            return (float)(UnityUnitsPerGraadX / Math.Pow(2, z));
        }

        private float ComputeScaleFactorY(int z)
        {
            return (float)(UnityUnitsPerGraadY / Math.Pow(2, z));
        }

        private float ComputeScaleFactorZ(int z)
        {
            return (float)(UnityUnitsPerGraadZ / Math.Pow(2, z));
        }

        private IEnumerator RequestTMSTile(string url, Vector3 tileId)
        {
            var www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                var tmsTex = ((DownloadHandlerTexture)www.downloadHandler).texture;


                if (useSatellite)
                {
                    //var id = new Vector3(tileId.x, UnityEngine.Mathf.Floor(tileId.y / 2f), tileId.z - 1);
                    ////Debug.Log("id " + id + " " + tileId.y + " " + tileId.y / 2f + " " + UnityEngine.Mathf.Floor(tileId.y / 2f));
                    //var newTexture = new Texture2D(256, 512);

                    ////check if we need to overwrite or create new texture
                    //var tex = (Texture2D)tileDb[id].GetComponent<MeshRenderer>().material.mainTexture;
                    //if (tex.height == 256) //create new
                    //{
                    //    if (tileId.y % 2 == 0) //top, since we are top, fill bottom with empty tile texture
                    //    {
                    //        var pixels = tex.GetPixels(0, 0, 256, 256);
                    //        newTexture.SetPixels(0, 255, 256, 256, pixels);
                    //    }
                    //    else //top, since we are top, fill top with empty tile texture
                    //    {
                    //        var pixels = tex.GetPixels(0, 0, 256, 256);
                    //        newTexture.SetPixels(0, 0, 256, 256, pixels);
                    //    }

                    //}
                    //else
                    //{
                    //    var pixels = tex.GetPixels(0, 0, 256, 512);
                    //    newTexture.SetPixels(0, 0, 256, 512, pixels);
                    //}


                    ////overwrite pixels (top or bottom)
                    //if (tileId.y % 2 == 0) //top
                    //    newTexture.SetPixels(0, 0, 256, 256, tmsTex.GetPixels());
                    //else
                    //    newTexture.SetPixels(0, 255, 256, 256, tmsTex.GetPixels());

                    //newTexture.Apply();

                    //////assign
                    //tileDb[id].GetComponent<MeshRenderer>().material.mainTexture = newTexture;





                    ////stretch over x (from 256 pixels to 512)
                    //var croppedPixels = tmsTex.GetPixels(0, 0, 256, 256);

                    ////duplicate pixels
                    //Color[] stretchedImage = new Color[croppedPixels.Length * 2];
                    //for (int i = 0; i < croppedPixels.Length; i++)
                    //{
                    //    stretchedImage[i * 2] = croppedPixels[i];
                    //    stretchedImage[i * 2 + 1] = croppedPixels[i];
                    //}


                    //var croppedTexture = new Texture2D(512, 256);
                    //croppedTexture.SetPixels(stretchedImage);
                    //croppedTexture.Apply();

                    //var myNewTexture = croppedTexture;

                    ////slice in 2
                    //var croppedPixelsLeft = myNewTexture.GetPixels(0, 0, 256, 256);
                    //var croppedTextureLeft = new Texture2D(256, 256);
                    //croppedTextureLeft.SetPixels(croppedPixelsLeft);
                    //croppedTextureLeft.Apply();

                    //var croppedPixelsRight = myNewTexture.GetPixels(256, 0, 256, 256);
                    //var croppedTextureRight = new Texture2D(256, 256);
                    //croppedTextureRight.SetPixels(croppedPixelsRight);
                    //croppedTextureRight.Apply();


                    ////assign
                    //tileDb[tileId].GetComponent<MeshRenderer>().material.mainTexture = croppedTextureLeft;
                    //tileDb[new Vector3(tileId.x + 1, tileId.y, tileId.z)].GetComponent<MeshRenderer>().material.mainTexture = croppedTextureRight;

                    //resize 
                    var myNewTexture = Resize(tmsTex, 512, 512);

                    ////slice in 2
                    var croppedPixelsLeft = myNewTexture.GetPixels(0, 0, 256, 512);
                    var croppedTextureLeft = new Texture2D(256, 512);
                    croppedTextureLeft.SetPixels(croppedPixelsLeft);
                    croppedTextureLeft.Apply();

                    var croppedPixelsRight = myNewTexture.GetPixels(256, 0, 256, 512);
                    var croppedTextureRight = new Texture2D(256, 512);
                    croppedTextureRight.SetPixels(croppedPixelsRight);
                    croppedTextureRight.Apply();


                    //assign
                    tileDb[tileId].GetComponent<MeshRenderer>().material.mainTexture = croppedTextureLeft;
                    tileDb[new Vector3(tileId.x + 1, tileId.y, tileId.z)].GetComponent<MeshRenderer>().material.mainTexture = croppedTextureRight;
                }
                else
                {
                    tileDb[tileId].GetComponent<MeshRenderer>().material.mainTexture = tmsTex;
                }
            }
            else
            {
                Debug.Log("Tile: [" + tileId.x + " " + tileId.y + "] Error loading texture data");
            }

            pendingQueue.Remove(url);           
        }

        public static Texture2D Resize(Texture2D source, int newWidth, int newHeight)
        {
            source.filterMode = FilterMode.Point;
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            rt.filterMode = FilterMode.Point;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            var nTex = new Texture2D(newWidth, newHeight);
            nTex.ReadPixels(new Rect(0, 0, newWidth, newWidth), 0, 0);
            nTex.Apply();
            RenderTexture.active = null;
            return nTex;
        }

        public void Update()
        {

            if (pendingQueue.Count < maxParallelRequests && downloadQueue.Count > 0)
            {
                var request = downloadQueue.Dequeue();
                pendingQueue.Add(request.Url, request);

                //fire request
                switch (request.Service)
                {
                    case TileServiceType.QM:
                        StartCoroutine(RequestQMTile(request.Url, request.TileId, request.Extent));
                        break;
                    case TileServiceType.TMS:
                        StartCoroutine(RequestTMSTile(request.Url, request.TileId));
                        break;
                }
            }
        }

        public void ZoomIn()
        {
            //hacky, we should implement some sort of checking of tileset bounds
            if (useWorld) 
            {
                if ((zoomLevel + 1) <= 13) //max zoom of world terrain tile is 13
                {
                    zoomLevel++;
                    LoadMap();
                }
            }
            else
            {
                if (zoomLevel + 1 <= 17) //max zoom of geodan terrain tile is 17
                {
                    zoomLevel++;
                    LoadMap();
                }
            }
        }

        public void ZoomOut()
        {
            if ((zoomLevel - 1) >=0)
            {
                zoomLevel--;
                LoadMap();
            }            
        }

        public void ToggleTerrain(float value)
        {
            useWorld = value == 1 ? false : true;

            //update zoom level and reload
            if (useWorld)
            {
                if (zoomLevel > 13)
                    zoomLevel = 13;

                LoadMap();
            }
            else
            {
                if (zoomLevel > 17)
                    zoomLevel = 17;

                LoadMap();
            }
        }

        public void ToggleTerrainCover(float value)
        {
            useSatellite = value == 1 ? false : true;

            LoadMap();
        }

        public void GotoLocation(string locationName)
        {
            switch (locationName)
            {
                case "Amsterdam":
                    extent = Amsterdam;
                    floatingOrigin = new Vector2((float)Amsterdam.CenterX, (float)Amsterdam.CenterY);
                    break;
                case "Ispra":
                    extent = Ispra;
                    floatingOrigin = new Vector2((float)Ispra.CenterX, (float)Ispra.CenterY);
                    break;
                case "Kongsberg":                    
                    extent = Kongsberg;
                    floatingOrigin = new Vector2((float)Kongsberg.CenterX, (float)Kongsberg.CenterY);
                    break;
            }

            LoadMap();
        }
        
        //lon, lat (EPSG: 4326) -> x,y (EPSG: 900913 / EPSG:3785) , from: https://gist.github.com/onderaltintas/6649521
        public static Vector2 DegreesToMeters(double lon, double lat) {

            var x = lon * 20037508.34 / 180;
            var y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            y = y * 20037508.34 / 180;
            return new Vector2((float) x, (float) y);
        }

        //x,y (EPSG: 900913 / EPSG:3785) -> lon, lat (EPSG: 4326), from: https://gist.github.com/onderaltintas/6649521
        public static Vector2 MetersToDegrees(double x, double y)
        {
            var lon = x * 180 / 20037508.34;
            var lat = Math.Atan(Math.Exp(y * Math.PI / 20037508.34)) * 360 / Math.PI - 90;
            return new Vector2((float)lon, (float)lat);
        }
    }
}