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
        string url = $"https://assets.cesium.com/1/{x}/{y}/{z}.terrain?v=1.1.0";

        DownloadHandlerBuffer handler = new DownloadHandlerBuffer();
        TerrainTile terrainTile;
        UnityWebRequest http = new UnityWebRequest(url);
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiI3OTk2ODFjOS1lY2ZjLTRjNGEtYjFlYi0wMTYwMjZiYTZmMDYiLCJpZCI6NDc4OSwiYXNzZXRzIjp7IjEiOnsidHlwZSI6IlRFUlJBSU4iLCJleHRlbnNpb25zIjpbdHJ1ZSx0cnVlLHRydWVdfX0sInNyYyI6IjI3MjZmNTYxLWEwM2UtNDFhZC04NGZmLTA2NDBkOTRkYWJmMiIsImlhdCI6MTU0MjA5ODc5NCwiZXhwIjoxNTQyMTAyMzk0fQ.43QlB4y9xuC3I31fMsaVFYVyNG2bsd1Kp39EojAACQU";
        http.SetRequestHeader("accept", $"application/vnd.quantized-mesh,application/octet-stream;q=0.9,*/*;q=0.01,*/*;access_token={token}");

        http.downloadHandler = handler;
        yield return http.Send();

        if (!http.isNetworkError)
        {
            //get data
            MemoryStream stream = new MemoryStream(http.downloadHandler.data);

            //unzip
            var deflateStream = new GZipStream(stream, CompressionMode.Decompress);

            //parse into tile
            terrainTile = TerrainTileParser.Parse(deflateStream);

            //create tile 
            var tile = new GameObject("tile x:" + x + " y:" + y + " z:" + z);
            tile.AddComponent<MeshFilter>().mesh = terrainTile.GetMesh(x, y, z);


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