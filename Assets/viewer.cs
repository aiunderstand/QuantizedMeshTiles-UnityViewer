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

    Extent extent = new Extent(4.88, 52.36, 4.92, 52.38); //part of Amsterdam, Netherlands in Latitude/Longitude (GPS) coordinates as boundingbox.
    public int zoomLevel = 14;

    public GameObject placeholderTile;
    public string terrainUrl = @"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0";
    public string textureUrl = @"https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox={xMin}%2C{yMin}%2C{xMax}%2C{yMax}&width=256&height=256&srs=EPSG%3A4326";
    
    Dictionary<Vector2, GameObject> tileDb = new Dictionary<Vector2, GameObject>();

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
            StartCoroutine(requestWMSTile(subtileExtent,t.Index.Col, t.Index.Row));
            
            //get tile height data (
            StartCoroutine(requestQMTile(t.Index.Col, t.Index.Row, int.Parse(t.Index.Level)));
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
    }

    private Vector3 SetTilePosition(TileIndex index, TileRange tileRange)
    {
        return new Vector3((index.Col - tileRange.FirstCol) * -360f, 0, (index.Row - tileRange.FirstRow) * 180);
    }

    private IEnumerator requestQMTile(int x, int y, int z)
    {
        var url = terrainUrl.Replace("{x}", x.ToString()).Replace("{y}", y.ToString()).Replace("{z}", z.ToString());

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
            tileDb[new Vector2(x,y)].GetComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh(-44); //height offset is manually done to nicely align height data with place holder at 0
            tileDb[new Vector2(x, y)].transform.rotation = Quaternion.Euler(new Vector3(180, 0, 0));
            tileDb[new Vector2(x, y)].transform.localScale = new Vector3(1, 1, 1);
        }
        else
        {
            Debug.LogError("Tile: [" + x + " " + y + "] Error loading height data");
        }
    }

    private IEnumerator requestWMSTile(Extent extent, int x, int y)
    {
        var url = textureUrl.Replace("{xMin}", extent.MinX.ToString()).Replace("{yMin}", extent.MinY.ToString()).Replace("{xMax}", extent.MaxX.ToString()).Replace("{yMax}", extent.MaxY.ToString()).Replace(",", ".");

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (!www.isNetworkError && !www.isHttpError)
        {
            Texture2D myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            //myTexture.filterMode = FilterMode.Point;

            //update tile with height data
            tileDb[new Vector2(x, y)].GetComponent<MeshRenderer>().material.mainTexture = myTexture;           
        }
        else
        {
            Debug.LogError("Tile: [" + x + " " + y + "] Error loading texture data");
        }
    }
}