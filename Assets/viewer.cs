using UnityEngine;
using UnityEngine.Networking;
using System.IO.Compression;
using Terrain.Tiles;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using ExtensionMethods;

public class viewer : MonoBehaviour {

    public int X = 0;
    public int Y = 0;
    public int Z = 0;
    public Material mat1;
    public Material mat2;
    //public bool showVertices = false;
    private List<GameObject> tiles = new List<GameObject>();

    public void CreateTile()
    {
        StartCoroutine(requestTile(0,0,0));
        StartCoroutine(requestTile(0,1,0));
    }

    public void SwitchMaterial()
    {
        foreach (var t in tiles)
        {
            if (t.GetComponent<MeshRenderer>().material == mat1)
                t.GetComponent<MeshRenderer>().material = mat2;              
            else
                t.GetComponent<MeshRenderer>().material = mat1;
        }
    }
    private IEnumerator requestTile(int x, int y, int z)
    {
        // todo: must be z/x/y in request, two top tiles are 0/0/0 (z/x/y) and 0/1/0 (z/y/y)
        string url = $"http://assets.agi.com/stk-terrain/v1/tilesets/world/tiles/{x}/{y}/{z}.terrain";

        DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
        TerrainTile terrainTile;
        UnityWebRequest http = new UnityWebRequest(url);
        http.downloadHandler = handler;
        yield return http.Send();

        if (!http.isError)
        {
            //get data
            MemoryStream stream = new MemoryStream(http.downloadHandler.data);

            //unzip
            var deflateStream = new GZipStream(stream, CompressionMode.Decompress);

            //parse into tile
            terrainTile = TerrainTileParser.Parse(deflateStream);

            //create tile 
            var tile = new GameObject("tile x:" + x + " y:" + y + " z:" + z);
            tile.AddComponent<MeshFilter>().mesh = terrainTile.GetMesh1(x, y, z);


            if (y==0)
                tile.AddComponent<MeshRenderer>().material = mat1;
            else
                tile.AddComponent<MeshRenderer>().material = mat2;


            tile.transform.localScale = new Vector3(0.5f, -0.003f, 1);
            tile.transform.position = new Vector3(x - (y * 180), 0, 0);
            tiles.Add(tile);
            
            ////create vertices (if needed)
            //if (showVertices)
            //{
            //    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //    Mesh cubeMesh = cube.GetComponent<MeshFilter>().mesh;
            //    cube.SetActive(false);

            //    var vert = tile.GetComponent<MeshFilter>().mesh.vertices;

            //    for (int i = 0; i < vert.Length; i++)
            //    {
            //        GameObject computePoint = new GameObject(name) { hideFlags = HideFlags.HideInHierarchy };
            //        computePoint.transform.parent = tile.transform;
            //        computePoint.transform.position = new Vector3(vert[i].x, vert[i].y, vert[i].z);
            //        computePoint.AddComponent<MeshFilter>().mesh = cubeMesh;
            //        computePoint.AddComponent<MeshRenderer>().material = mat2;
            //        computePoint.AddComponent<MeshCollider>();
            //    }
            //}
        }
        else
        {
            Debug.Log("Error loading tile");
        }
    }
}