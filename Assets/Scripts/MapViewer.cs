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
using TMPro;

namespace Terrain
{
    public class MapViewer : MonoBehaviour
    {
        [SerializeField]
        private Extent extent;

        private readonly Extent Amsterdam = new Extent(4.88, 52.36, 4.92, 52.38); //Amsterdam, Netherlands
        private readonly Extent Ispra = new Extent(8.57, 45.78, 8.67, 45.85); //EU Joint Research Centre in Ispra, Italy
        private readonly Extent Kongsberg = new Extent(9.61, 59.64, 9.70, 59.69); //Kongsberg, Norway

        private int zoomLevel = 13;

        [SerializeField]
        private GameObject placeholderTile;

        private string terrainUrl;
        bool useWorld = true;
        private string geodanTerrain = @"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0"; //max zoom is 17!
        private string cesiumTerrain = @"https://maps.tilehosting.com/data/terrain-quantized-mesh/{z}/{x}/{y}.terrain?key=irAs6FzTF3gJ9ArfQPjh"; //max zoom is 13!       

        private string textureUrl;
        bool useSatellite = false;
        private string geodanTerrainCover= @"https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox={xMin}%2C{yMin}%2C{xMax}%2C{yMax}&width=256&height=256&srs=EPSG%3A4326";
        private string cesiumTerrainCover = @"https://maps.tilehosting.com/data/satellite/{z}/{x}/{y}.jpg?key=irAs6FzTF3gJ9ArfQPjh";

        private string buildingsUrl = @"https://saturnus.geodan.nl/tomt/data/buildingtiles_adam/tiles/{id}.b3dm";
        private const int tilesize = 180;

        private TMP_Dropdown geocoder;

        //implement will use floating origin
        Vector2 floatingOrigin;
        const float UnityUnitsPerGraadX = 68600;
        const float UnityUnitsPerGraadY = 111300;
        readonly Dictionary<Vector3, GameObject> tileDb = new Dictionary<Vector3, GameObject>();

        const int maxParallelRequests = 4;
        readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();
        readonly Dictionary<string, DownloadRequest> pendingQueue = new Dictionary<string, DownloadRequest>(maxParallelRequests);

        [UsedImplicitly]
        private void Awake()
        {
            var geocoderComponent = GetComponentUI("geocoder");

            if (geocoderComponent != null)
            {
                geocoder = geocoderComponent.GetComponent<TMP_Dropdown>();
                GotoLocation(geocoder.value);
            }
            else
            {
                Debug.LogError("geocoder UI component not found");
            }
        }

        public static GameObject GetComponentUI(string type)
        {
            var elements = GameObject.FindGameObjectsWithTag("UI-element");

            foreach (var e in elements)
            {
                if (e.name.ToLower().Equals(type.ToLower()))
                    return e;
            }

            return null; //not found
        }

        private void LoadMap()
        {
            ClearTiles();

            //settings should be refactored to settings class
            ProcessSettings();

            var schema = new TmsGlobalGeodeticTileSchema();
            var tileRange = TileTransform.WorldToTile(extent, zoomLevel.ToString(), schema);
            var tiles = schema.GetTileInfos(extent, zoomLevel.ToString()).ToList();

            RequestTiles(schema, tileRange, tiles);

            if (useSatellite)
            {
                var schema2 = new WebMercator();
                var tiles2 = schema2.GetTileInfos(extent, zoomLevel.ToString()).ToList();

                foreach (var t in tiles2)
                {
                    var url = textureUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString()); //WMTS
                    downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.WMS, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
                }
            }
        }

        private void RequestTiles(TmsGlobalGeodeticTileSchema schema, TileRange tileRange, List<TileInfo> tiles)
        {
            //immediately draw placeholder tile and fire request for texture and height. Depending on which one returns first, update place holder.
            foreach (var t in tiles)
            {
                //draw placeholder tile
                var tile = DrawPlaceHolder(tileRange, t);

                tileDb.Add(new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level)), tile);

                //get tile texture data
                if (!useSatellite)
                {
                    var subtileExtent = TileTransform.TileToWorld(new TileRange(t.Index.Col, t.Index.Row), t.Index.Level.ToString(), schema);

                    var url = textureUrl.Replace("{xMin}", subtileExtent.MinX.ToString()).Replace("{yMin}", subtileExtent.MinY.ToString()).Replace("{xMax}", subtileExtent.MaxX.ToString()).Replace("{yMax}", subtileExtent.MaxY.ToString()).Replace(",", "."); //WMS
                    downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.WMS, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
                }

                //get tile height data (
                var qmUrl = terrainUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                downloadQueue.Enqueue(new DownloadRequest(qmUrl, TileServiceType.QM, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
            }
        }

        private void ProcessSettings()
        {
            terrainUrl = useWorld ? cesiumTerrain : geodanTerrain;
            textureUrl = useSatellite ? cesiumTerrainCover : geodanTerrainCover;
        }

        private GameObject DrawPlaceHolder(TileRange tileRange, TileInfo t)
        {
            var tile = Instantiate(placeholderTile);
            tile.name = $"tile/{t.Index.ToIndexString()}";
            tile.transform.position = GetTilePosition(t.Index, tileRange);
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

        private Vector3 GetTilePosition(TileIndex index, TileRange tileRange)
        {
            var tegelbreedte = tilesize / Math.Pow(2, int.Parse(index.Level)); //tegelbreedte in graden
            var originX = ((index.Col + 0.5) * tegelbreedte) - 180;
            var originY = ((index.Row + 0.5) * tegelbreedte) - 90;
            var X = (originX - floatingOrigin.x) * UnityUnitsPerGraadX;
            var Y = (originY - floatingOrigin.y) * UnityUnitsPerGraadY;
            return new Vector3((float)X, 0, (float)Y);
        }

        private IEnumerator RequestQMTile(string url, Vector3 tileId)
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
                tileDb[tileId].GetComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh(-44); //height offset is manually done to nicely align height data with place holder at 0
                tileDb[tileId].transform.localScale = new Vector3(ComputeScaleFactorX((int)tileId.z), 1, ComputeScaleFactorY((int)tileId.z));
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

        private IEnumerator RequestWMSTile(string url, Vector3 tileId)
        {
            var www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;

                //update tile with height data
                tileDb[tileId].GetComponent<MeshRenderer>().material.mainTexture = myTexture;
            }
            else
            {
                Debug.Log("Tile: [" + tileId.x + " " + tileId.y + "] Error loading texture data");
            }

            pendingQueue.Remove(url);           
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
                        StartCoroutine(RequestQMTile(request.Url, request.TileId));
                        break;
                    case TileServiceType.WMS:
                        StartCoroutine(RequestWMSTile(request.Url, request.TileId));
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

        public void GotoLocation(int locationIndex)
        {
           var location = geocoder.options[locationIndex].text;

            switch (location)
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
    }
}