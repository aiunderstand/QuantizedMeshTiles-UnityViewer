using UnityEngine;
using UnityEngine.Networking;
using Terrain.Tiles;
using System.Collections;
using System.IO;
using ExtensionMethods;
using BruTile;
using System.Linq;
using System;
using System.Collections.Generic;

public class viewer : MonoBehaviour {

    public enum TileService {
        WMS,
        QM
    }
    Extent extent = new Extent(4.88, 52.36, 4.92, 52.38); //part of Amsterdam, Netherlands in Latitude/Longitude (GPS) coordinates as boundingbox.
    public int zoomLevel = 14;

    public GameObject placeholderTile;
    public string terrainUrl = @"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0";
    public string textureUrl = @"https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox={xMin}%2C{yMin}%2C{xMax}%2C{yMax}&width=256&height=256&srs=EPSG%3A4326";
    public string buildingsUrl = @"https://saturnus.geodan.nl/tomt/data/buildingtiles_adam/tiles/{id}.b3dm";
    Dictionary<Vector2, GameObject> tileDb = new Dictionary<Vector2, GameObject>();

    const int maxParallelRequests = 20;
    Queue<downloadRequest> downloadQueue = new Queue<downloadRequest>();
    Dictionary<string, downloadRequest> pendingQueue = new Dictionary<string, downloadRequest>(maxParallelRequests);

    public struct downloadRequest {

        public string Url;
        public TileService Service;
        public Vector2 TileId;
 

        public downloadRequest(string url, TileService service, Vector2 tileId) {
            Url = url;
            Service = service;
            TileId = tileId;
        }
    }
    public void CreateTiles()
    {
        ClearTiles();
        
        var schema = new TmsGlobalGeodeticTileSchema();
        var tileRange = TileTransform.WorldToTile(extent, zoomLevel.ToString(), schema);
        
        var tiles = schema.GetTileInfos(extent, zoomLevel.ToString()).ToList();
        
        //immediately draw placeholder tile and fire request for texture and height. Depending on which one returns first, update place holder.
        foreach (var t in tiles)
        {
            //draw placeholder tile
            var tile = GameObject.Instantiate(placeholderTile);
            tile.name = $"tile /{t.Index.Level.ToString()}/{t.Index.Col.ToString()}/{t.Index.Row.ToString()}";
            tile.transform.position = SetTilePosition(t.Index, tileRange);
            
            tileDb.Add(new Vector2(t.Index.Col,t.Index.Row), tile);

            //get tile texture data
            Extent subtileExtent = TileTransform.TileToWorld(new TileRange(t.Index.Col, t.Index.Row), t.Index.Level.ToString(), schema);
            var wmsUrl = textureUrl.Replace("{xMin}", subtileExtent.MinX.ToString()).Replace("{yMin}", subtileExtent.MinY.ToString()).Replace("{xMax}", subtileExtent.MaxX.ToString()).Replace("{yMax}", subtileExtent.MaxY.ToString()).Replace(",", ".");
            downloadQueue.Enqueue(new downloadRequest(wmsUrl, TileService.WMS, new Vector2(t.Index.Col, t.Index.Row)));
            
            //get tile height data (
            var qmUrl = terrainUrl.Replace("{x}", t.Index.Col.ToString()).Replace("{y}", t.Index.Row.ToString()).Replace("{z}", int.Parse(t.Index.Level).ToString());
            downloadQueue.Enqueue(new downloadRequest(qmUrl, TileService.QM, new Vector2(t.Index.Col, t.Index.Row)));
                    }
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
                case TileService.QM:
                    StartCoroutine(requestQMTile(request.Url, request.TileId));
                    break;
                case TileService.WMS:
                    StartCoroutine(requestWMSTile(request.Url, request.TileId));
                    break;
            }
        }
    }

    private void ClearTiles()
    {
        if (tileDb.Count > 0)
        {
            foreach (var t in tileDb)
            {
                Destroy(t.Value);
            }
        }

        tileDb.Clear();
        downloadQueue.Clear();
        pendingQueue.Clear();
    }

    private Vector3 SetTilePosition(TileIndex index, TileRange tileRange)
    {
        return new Vector3((index.Col - tileRange.FirstCol) * -360f, 0, (index.Row - tileRange.FirstRow) * 180);
    }

    private IEnumerator requestQMTile(string url, Vector2 tileId)
    {
        DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
        TerrainTile terrainTile;
        UnityWebRequest www = new UnityWebRequest(url);

        www.downloadHandler = handler;
        yield return www.SendWebRequest();

        if (!www.isNetworkError && !www.isHttpError)
        {
            //get data
            MemoryStream stream = new MemoryStream(www.downloadHandler.data);

            //parse into tile data structure
            terrainTile = TerrainTileParser.Parse(stream);

            //update tile with height data
            tileDb[tileId].GetComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh(-44); //height offset is manually done to nicely align height data with place holder at 0
            tileDb[tileId].transform.rotation = Quaternion.Euler(new Vector3(180, 0, 0));
            tileDb[tileId].transform.localScale = new Vector3(1, 1, 1);
        }
        else
        {
            Debug.LogError("Tile: [" + tileId.x + " " + tileId.y + "] Error loading height data");
        }

        pendingQueue.Remove(url);
    }

    private IEnumerator requestWMSTile(string url, Vector2 tileId)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (!www.isNetworkError && !www.isHttpError)
        {
            Texture2D myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
          
            //update tile with height data
            tileDb[tileId].GetComponent<MeshRenderer>().material.mainTexture = myTexture;           
        }
        else
        {
            Debug.LogError("Tile: [" + tileId.x + " " + tileId.y + "] Error loading texture data");
        }

        pendingQueue.Remove(url);
    }
}