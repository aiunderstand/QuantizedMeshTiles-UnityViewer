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
using Newtonsoft.Json;
using B3dm.Tile;
using GLTF.Schema;
using GLTF;
using UnityGLTF;
using Pnts.Tile;
using DotSpatial.Projections;
using DotSpatial.Positioning;

namespace Terrain
{
    public class MapViewer : MonoBehaviour
    {
        Extent extent;
        int zoomLevel = 13;

        [SerializeField]
        private GameObject placeholderTile;

        private Datasource terrain;
        private Datasource surface;
        private Datasource buildings;
        private Datasource trees;

        private const int tilesize = 180;
       
        private TMP_Dropdown geocoderDropdown;
        private TMP_Dropdown terrainDropdown;
        private TMP_Dropdown surfaceDropdown;
        private TMP_Dropdown buildingsDropdown;
        private TMP_Dropdown treesDropdown;
       
        //implement will use floating origin
        Vector2 floatingOrigin;
        const float UnityUnitsPerGraadX = 68600;
        const float UnityUnitsPerGraadY = 111300;
        readonly Dictionary<Vector3, GameObject> tileDb = new Dictionary<Vector3, GameObject>();

        const int maxParallelRequests = 4;
        readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();
        readonly Dictionary<string, DownloadRequest> pendingQueue = new Dictionary<string, DownloadRequest>(maxParallelRequests);
        Settings settings;
        int maxDepthLevel = 0; //increase to allow more pointcloud detail (zoom levels)

        [UsedImplicitly]
        private void Awake()
        {
            settings = LoadSettingsFromDisk();

            InitSettings();

            LoadMap();
        }

        private void InitSettings()
        {
            var comp = GetComponentUI("Terrain");

            if (comp != null)
            {
                terrainDropdown = comp.GetComponent<TMP_Dropdown>();
                SetTerrainProvider(terrainDropdown.value);
            }
            else
            {
                Debug.LogError("terrain UI component not found");
            }

            comp = GetComponentUI("Surface");

            if (comp != null)
            {
                surfaceDropdown = comp.GetComponent<TMP_Dropdown>();
                SetSurfaceProvider(surfaceDropdown.value);
            }
            else
            {
                Debug.LogError("surface UI component not found");
            }

            comp = GetComponentUI("Buildings");

            if (comp != null)
            {
                buildingsDropdown = comp.GetComponent<TMP_Dropdown>();
                SetBuildingsProvider(buildingsDropdown.value);
            }
            else
            {
                Debug.LogError("buildings UI component not found");
            }

            comp = GetComponentUI("Trees");

            if (comp != null)
            {
                treesDropdown = comp.GetComponent<TMP_Dropdown>();
                SetTreesProvider(treesDropdown.value);
            }
            else
            {
                Debug.LogError("trees UI component not found");
            }

            comp = GetComponentUI("geocoder");

            if (comp != null)
            {
                geocoderDropdown = comp.GetComponent<TMP_Dropdown>();
                GotoLocation(geocoderDropdown.value);
            }
            else
            {
                Debug.LogError("geocoder UI component not found");
            }
        }

        public void SetTreesProvider(int value)
        {
            var treesName = treesDropdown.options[value].text;

            foreach (var ds in settings.DataSources)
            {
                if (ds.DataType == DataType.Trees)
                {
                    if (ds.Name.Equals(treesName))
                    {
                        trees = ds;
                    }
                }
            }

            foreach (var treeTile in trees.Collection)
            {
                downloadQueue.Enqueue(new DownloadRequest(trees.Url[0] + treeTile + "/", DataType.TreeCollection, Vector3.zero));
            }
        }

        private IEnumerator RequestTreeCollection(string url)
        {
            DownloadHandlerBuffer handler = new DownloadHandlerBuffer();

            var www = new UnityWebRequest(url + "tileset.json")
            {
                downloadHandler = handler
            };
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                //get data
                string jsonString = www.downloadHandler.text;

                //convert to jsonTree
                var json = JsonConvert.DeserializeObject<b3dm_pnts.Rootobject>(jsonString);

                //collect all url nodes in jsonTree and add to list
                List<string> tiles = new List<string>();

               if (json.root.content != null)
                    tiles.Add(url + json.root.content.url);

                //hackish lod system
                int depthLevel = 2; // tileset 1/x/x/x;
                if (depthLevel <= maxDepthLevel)
                {
                    if (json.root.children != null)
                    {
                        foreach (var c in json.root.children)
                        {
                            tiles.Add(url + c.content.url);

                            depthLevel++; // tileset 2/x/x/x;
                            if (depthLevel <= maxDepthLevel)
                            {
                                if (c.children != null)
                                {
                                    AddToTiles(c.children, tiles, url, depthLevel);
                                }
                            }
                        }
                    }
                }
                //download and load tiles
                for (int i = 0; i < tiles.Count; i++)
                {
                    downloadQueue.Enqueue(new DownloadRequest(tiles[i], DataType.Trees, Vector3.zero));
                }

            }
            else
            {
                Debug.Log("Tile: [" + url + "] Error loading tileset data");
            }

            pendingQueue.Remove(url);
        }

        private IEnumerator RequestTreeTile(string url)
        {
            DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
            var www = new UnityWebRequest(url)
            {
                downloadHandler = handler
            };
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                //get data
                var stream = new MemoryStream(www.downloadHandler.data);

                var pnts = PntsParser.ParsePnts(stream);
                var pointData = pnts.Points;
                var colorData = pnts.Colors;
                
                Vector3[] verts = new Vector3[pointData.Count];
                for (int i = 0; i < pointData.Count; i++)
                {
                    var dist_x = new Distance(pnts.FeatureTableMetadata.Rtc_Center[0] + pointData[i].X, DistanceUnit.Meters); //lon
                    var dist_y = new Distance(pnts.FeatureTableMetadata.Rtc_Center[1] + pointData[i].Y, DistanceUnit.Meters); //lat
                    var dist_z = new Distance(pnts.FeatureTableMetadata.Rtc_Center[2] + pointData[i].Z, DistanceUnit.Meters); //alt

                    var WGS84Coor = new CartesianPoint(dist_x, dist_y, dist_z).ToPosition3D();

                    double x = WGS84Coor.Longitude.DecimalDegrees;
                    double height = WGS84Coor.Altitude.Value;
                    double z = WGS84Coor.Latitude.DecimalDegrees;

                    //conversion to view
                    var xv = (x - floatingOrigin.x) * UnityUnitsPerGraadX;
                    var zv = (z - floatingOrigin.y) * UnityUnitsPerGraadY;
                   
                    verts[i] = new Vector3((float) xv, (float) height, (float) zv);
                }

                
                Color[] colors = new Color[colorData.Count];
                for (int i = 0; i < colorData.Count; i++)
                {
                    colors[i] = new Color(colorData[i].R/ (float) 255f, colorData[i].G/ (float) 255f, colorData[i].B/ (float)255f, colorData[i].A/ (float)255f);
                    
                }

                int[] tris = new int[pointData.Count];

                for (int i = 0; i < tris.Length; i++)
                {
                    tris[i] = i;
                }

                Mesh m = new Mesh();
                m.vertices = verts;
                m.colors = colors;
                m.SetIndices(tris, MeshTopology.Points, 0);
                m.RecalculateBounds();

                GameObject trees = new GameObject("trees: " + url);// { hideFlags = HideFlags.HideInHierarchy };
                trees.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Point Cloud/Point"));
                trees.AddComponent<MeshFilter>().mesh = m;
            }
            else
            {
                Debug.Log("Tile: [" + url + "] Error loading b3dm data");
            }

            pendingQueue.Remove(url);
        }
        
        public void SetBuildingsProvider(int value)
        {
            var buildingsName = buildingsDropdown.options[value].text;

            foreach (var ds in settings.DataSources)
            {
                if (ds.DataType == DataType.Buildings)
                {
                    if (ds.Name.Equals(buildingsName))
                    {
                        buildings = ds;
                    }
                }
            }

            foreach (var buildingTile in buildings.Url)
            {
                downloadQueue.Enqueue(new DownloadRequest(buildingTile, DataType.BuildingRootTile, Vector3.zero));
            }
        }

        private IEnumerator RequestBuildingRootTile(string url)
        {
            DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
            var www = new UnityWebRequest(url + "tileset.json")
            {
                downloadHandler = handler
            };
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                //get data
                string jsonString = www.downloadHandler.text;

                //convert to jsonTree
                var json = JsonConvert.DeserializeObject<b3dm.Rootobject>(jsonString);

                //collect all url nodes in jsonTree and add to list
                List<string> tiles = new List<string>();

                foreach (var c in json.root.children)
                {
                    tiles.Add(url + c.content.url);

                    if (c.children != null)
                        AddToTiles(c.children, tiles, url);
                }
           
                //get translation offset
                var dist_x = new Distance(json.root.transform[12], DistanceUnit.Meters); //lon
                var dist_y = new Distance(json.root.transform[13], DistanceUnit.Meters); //lat
                var dist_z = new Distance(json.root.transform[14], DistanceUnit.Meters); //alt

                var WGS84Coor = new CartesianPoint(dist_x, dist_y, dist_z).ToPosition3D();

                double x = WGS84Coor.Longitude.DecimalDegrees;
                double height = WGS84Coor.Altitude.Value;
                double z = WGS84Coor.Latitude.DecimalDegrees;

                //conversion to view
                var xv = (x - floatingOrigin.x) * UnityUnitsPerGraadX;
                var zv = (z - floatingOrigin.y) * UnityUnitsPerGraadY;

                var offset = new Vector3((float)xv, (float)height, (float)zv);

                //download and load tiles
                for (int i = 0; i < tiles.Count; i++)
                {
                    downloadQueue.Enqueue(new DownloadRequest(tiles[i], DataType.Buildings, offset));
                }
               
            }
            else
            {
                Debug.Log("Tile: [" + url + "] Error loading tileset data");
            }
        }
        
        private void AddToTiles(b3dm.Child[] children, List<string> tiles, string url)
        {
            foreach (var c in children)
            {
                tiles.Add(url + c.content.url);

                if (c.children != null)
                    AddToTiles(c.children, tiles, url);
            }

        }

        private void AddToTiles(b3dm_pnts.Child[] children, List<string> tiles, string url, int depthLevel)
        {
            foreach (var c in children)
            {
                tiles.Add(url + c.content.url);

                depthLevel++;
                if (depthLevel <= maxDepthLevel)
                {
                    if (c.children != null)
                        AddToTiles(c.children, tiles, url, depthLevel);
                }
            }

        }

        private IEnumerator RequestBuildingTile(string url, Vector3 offset)
        {
            DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
            var www = new UnityWebRequest(url)
            {
                downloadHandler = handler
            };
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                //get data
                var stream = new MemoryStream(www.downloadHandler.data);

                var b3dm = B3dmParser.ParseB3dm(stream); //set to false because currently no batchtable is supplied in adam datasources. When adding eg. BagID's, this needs to be set to true + plus batchtable implementation.
                //Debug.Log(b3dm.Glb.GltfModelJson);
                var memoryStream = new MemoryStream(b3dm.GlbData);
                Load(memoryStream, offset);
            }
            else
            {
                Debug.Log("Tile: [" + url + "] Error loading b3dm data");
            }

            pendingQueue.Remove(url);
        }

        private void Load(Stream stream, Vector3 offset)
        {
            GLTFRoot gLTFRoot;
            GLTFParser.ParseJson(stream, out gLTFRoot);
            var loader = new GLTFSceneImporter(gLTFRoot, null, null, stream);
            loader.Offset = offset;
            loader.LoadSceneAsync();
        }

        public void SetTerrainProvider(int value)
        {
            var terrainName = terrainDropdown.options[value].text;

            foreach (var ds in settings.DataSources)
            {
                if (ds.DataType == DataType.Terrain)
                {
                    if (ds.Name.Equals(terrainName))
                    {
                        terrain = ds;
                    }
                }
            }

            LoadMap();
        }

        public void SetSurfaceProvider(int value)
        {
            var surfaceName = surfaceDropdown.options[value].text;

            foreach (var ds in settings.DataSources)
            {
                if (ds.DataType == DataType.Surface)
                {
                    if (ds.Name.Equals(surfaceName))
                    {
                        surface = ds;
                    }
                }
            }

            LoadMap();
        }

        private Settings LoadSettingsFromDisk()
        {
            //load file from disk
            string path = Application.dataPath + "/settings.json";
            string jsonString = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<Settings>(jsonString);

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
            ClearBuildingCache();

            var schema = new TmsGlobalGeodeticTileSchema();
            var tileRange = TileTransform.WorldToTile(extent, zoomLevel.ToString(), schema);
            var tiles = schema.GetTileInfos(extent, zoomLevel.ToString()).ToList();

            RequestTiles(schema, tileRange, tiles);

            //if (useSatellite)
            //{
            //    var schema2 = new BruTile.Predefined.GlobalSphericalMercator(YAxis.TMS);
            //    var tiles2 = schema2.GetTileInfos(extent, zoomLevel.ToString()).ToList();

            //    foreach (var t in tiles2)
            //    {
            //        var url = textureUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString()); //WMTS
            //        downloadQueue.Enqueue(new DownloadRequest(url, TileServiceType.WMS, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
            //    }
            //}
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
                string surfaceUrl = "";

                switch (surface.Service)
                {
                    case DataService.WMS:
                        var subtileExtent = TileTransform.TileToWorld(new TileRange(t.Index.Col, t.Index.Row), t.Index.Level.ToString(), schema);
                        var urlWMS = surface.Url[0];
                        surfaceUrl = urlWMS.Replace("{xMin}", subtileExtent.MinX.ToString()).Replace("{yMin}", subtileExtent.MinY.ToString()).Replace("{xMax}", subtileExtent.MaxX.ToString()).Replace("{yMax}", subtileExtent.MaxY.ToString()).Replace(",", ".");
                        break;
                    case DataService.TMS:
                        var urlTMS = surface.Url[0];
                        surfaceUrl = urlTMS.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                        break;
                }

                downloadQueue.Enqueue(new DownloadRequest(surfaceUrl, DataType.Surface, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
                

                //get tile height data (
                var terrainUrl = terrain.Url[0].Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
                downloadQueue.Enqueue(new DownloadRequest(terrainUrl, DataType.Terrain, new Vector3(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level))));
            }
        }

        private GameObject DrawPlaceHolder(TileRange tileRange, TileInfo t)
        {
            var tile = Instantiate(placeholderTile);
            tile.name = $"tile/{t.Index.ToIndexString()}";
            tile.transform.position = GetTilePosition(t.Index, tileRange);
            tile.transform.localScale = new Vector3(ComputeScaleFactorX(int.Parse(t.Index.Level)), 1, ComputeScaleFactorY(int.Parse(t.Index.Level)));
            return tile;
        }

        private void ClearBuildingCache()
        {
            tileDb.ToList().ForEach(t => Destroy(t.Value));
            tileDb.Clear();          
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

        private IEnumerator RequestTerrainTile(string url, Vector3 tileId)
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
                tileDb[tileId].GetComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh();
                tileDb[tileId].transform.localScale = new Vector3(ComputeScaleFactorX((int)tileId.z), 1, ComputeScaleFactorY((int)tileId.z));
            }
            else
            {
                Debug.Log("Tile: [" + tileId.x + " " + tileId.y + "] Error loading height data" + " url:  " + url);
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

        private IEnumerator RequestSurfaceTile(string url, Vector3 tileId)
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
                Debug.Log("Tile: [" + tileId.x + " " + tileId.y + "] Error loading texture data" + " url:  " + url);
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
                switch (request.Datatype)
                {
                    case DataType.Terrain:
                        StartCoroutine(RequestTerrainTile(request.Url, request.TileId));
                        break;
                    case DataType.Surface:
                        StartCoroutine(RequestSurfaceTile(request.Url, request.TileId));
                        break;
                    case DataType.Buildings:
                        StartCoroutine(RequestBuildingTile(request.Url, request.TileId)); //hack tileid is now offset, add datatype                    
                        break;
                    case DataType.BuildingRootTile:
                        StartCoroutine(RequestBuildingRootTile(request.Url));                   
                        break;
                    case DataType.Trees:
                        StartCoroutine(RequestTreeTile(request.Url));
                        break;
                    case DataType.TreeCollection:
                        StartCoroutine(RequestTreeCollection(request.Url));
                        break;
                }
            }
        }

        public void ZoomIn()
        {
            if ((zoomLevel + 1) <= terrain.maxZoom) 
            {
                zoomLevel++;

                downloadQueue.Clear();
                pendingQueue.Clear();
                LoadMap();
            }
        }

        public void ZoomOut()
        {
            if ((zoomLevel - 1) >= terrain.minZoom)
            {
                zoomLevel--;
                downloadQueue.Clear();
                pendingQueue.Clear();
                LoadMap();
            }            
        }
        
        public void GotoLocation(int locationIndex)
        {
           var location = geocoderDropdown.options[locationIndex].text;

            foreach (var l in settings.Locations)
            {
                if (l.Name.Equals(location))
                {
                    var bounds = l.WGS84Bounds;
                    extent = new Extent(bounds.minLon, bounds.minLat, bounds.maxLon, bounds.maxLat);
                    floatingOrigin = new Vector2((float)extent.CenterX, (float)extent.CenterY);
                }
            }

            LoadMap();
        }
    }
}