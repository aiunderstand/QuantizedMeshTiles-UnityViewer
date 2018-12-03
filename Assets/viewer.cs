using UnityEngine;
using UnityEngine.Networking;
using Terrain.Tiles;
using System.Collections;
using System.IO;
using ExtensionMethods;

public class viewer : MonoBehaviour {

    Vector2 location = new Vector2(16918, 12911);
    GameObject tile;
    public void CreateTile()
    {
        StartCoroutine(requestQMTile((int)location.x, (int)location.y, 14));
    }

    private IEnumerator requestQMTile(int x, int y, int z)
    {
        string url = $"https://saturnus.geodan.nl/tomt/data/tiles/{z}/{x}/{y}.terrain?v=1.0.0";
       
        DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
        TerrainTile terrainTile;
        UnityWebRequest http = new UnityWebRequest(url);
  
        http.downloadHandler = handler;
        yield return http.SendWebRequest();

        if (!http.isNetworkError)
        {
            //get data
            MemoryStream stream = new MemoryStream(http.downloadHandler.data);

            //parse into tile data structure
            terrainTile = TerrainTileParser.Parse(stream);

            //create unity tile with tile data structure
            tile = new GameObject("tile x:" + x + " y:" + y + " z:" + z);
            tile.AddComponent<MeshFilter>().mesh = terrainTile.GetMesh();
           
            tile.transform.localScale = new Vector3(0.5f, -1f, 1);
            tile.transform.position = new Vector3((x-location.x) * -180f, 0, (y-location.y) * -180f);

            StartCoroutine(requestWMSTile(0, 0, 0));
        }
        else
        {
            Debug.Log("Error loading tile");
        }
    }

    private IEnumerator requestWMSTile(int x, int y, int z)
    {
        string url = $"https://saturnus.geodan.nl/mapproxy/bgt/service?crs=EPSG%3A3857&service=WMS&version=1.1.1&request=GetMap&styles=&format=image%2Fjpeg&layers=bgt&bbox=5.866699218749994%2C51.844482421875%2C5.877685546875001%2C51.85546875&width=256&height=256&srs=EPSG%3A4326";
      
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (!www.isNetworkError || !www.isHttpError)
        {
            Texture2D myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            myTexture.filterMode = FilterMode.Point;
            
            var tempMaterial = new Material(Shader.Find("Unlit/Texture"));
            tempMaterial.mainTexture = myTexture;
            tempMaterial.SetTextureScale("_MainTex", new Vector2(-0.0028f, -0.005f));
            tempMaterial.SetTextureOffset("_MainTex", new Vector2(-0.5f, 0.5f));
            tile.AddComponent<MeshRenderer>().sharedMaterial = tempMaterial;
        }
        else
        {
            Debug.Log("Error loading tile");
        }
    }
}