using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using BruTileTerrain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terrain;
using Terrain.ExtensionMethods;
using Terrain.Tiles;
using UnityEngine;

public class LoadTest : MonoBehaviour
{
    static Mesh m;

    private void Awake()
    {
        m = Resources.Load<Mesh>("tileplaceholder");

        LoadTiles();
    }

    private void LoadTiles()
    {
        // input variablen: zoomLevel and Extent (in WGS94)
        // Sample: Amsterdam on level 1
        int zoomLevel =3;
        var extent = new Extent(-180, -84, 180, 84); //Amsterdam, Netherlands

        // Request OSM tiles
        var osmSchema = new GlobalSphericalMercator(0, 18);
        var osmTileSource = new HttpTileSource(osmSchema, "http://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", new[] { "a", "b", "c" }, "OSM");
        var tilesOsm = GetOSMTiles(osmTileSource, extent, zoomLevel);
        PrintTiles("OSM", osmTileSource, tilesOsm);

        // Request terrain tiles
        var terrainUrl = @"https://maps.tilehosting.com/data/terrain-quantized-mesh/{z}/{x}/{y}.terrain?key=irAs6FzTF3gJ9ArfQPjh"; //max zoom is 13!       
        var globalGeodeticSchema = new TmsGlobalGeodeticTileSchema();
        var terrainTileSource = new HttpTileSource(globalGeodeticSchema, terrainUrl);
        var tilesTerrain = GetTiles(terrainTileSource, zoomLevel, extent);
        PrintTiles("Terrain", terrainTileSource, tilesTerrain);

        // Sample: request Mapbox vector tiles
        var osmVectorTileSource = new HttpTileSource(osmSchema, "https://maps.tilehosting.com/data/v3/{z}/{x}/{y}.pbf?key=hWWfWrAiWGtv68r8wA6D");
        var vectorTilesOsm = GetOSMTiles(osmVectorTileSource, extent, zoomLevel);
        PrintTiles("Vector tiles OSM", osmVectorTileSource, vectorTilesOsm);

        Console.WriteLine("press any key to continue...");
        Console.ReadKey();
    }

    private static void PrintTiles(string message, HttpTileSource tileSource, List<TileInfo> tiles)
    {
        Console.WriteLine("Print tile from: " + message);
        
        foreach (var tileInfo in tiles)
        {
            var tile = tileSource.GetTile(tileInfo);

            Console.WriteLine(
                $"tile col: {tileInfo.Index.Col}, " +
                $"tile row: {tileInfo.Index.Row}, " +
                $"tile level: {tileInfo.Index.Level} , " +
                $"tile size {tile.Length}");


            var ms = new MemoryStream(tile);

            // tile is a sort of image, todo: use stream in UNity3D
            switch (message)
            {
                case "Terrain":
                    {
                        var terrainTile = TerrainTileParser.Parse(ms);
                   
                        //update tile with height data
                        var go = new GameObject("qm_" + tileInfo.Index.Col + "_" + tileInfo.Index.Row);

                        var X = ((tileInfo.Index.Col + 0.5) * 90);
                        var Y = HackYOffset(tileInfo.Index.Row);
                        go.transform.position = new Vector3((float)X, 0, (float)Y);
                        go.transform.localScale = new Vector3(0.5f, 0.01f, HackY(tileInfo.Index.Row));
                        go.AddComponent<MeshFilter>().sharedMesh = terrainTile.GetMesh(); //height offset is manually done to nicely align height data with place holder at 0
                        go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
                    }
                    break;
                case "OSM":
                    {
                       
                        //update tile with height data
                        var go = new GameObject("osm_" + tileInfo.Index.Col + "_" + tileInfo.Index.Row);

                        var X = ((tileInfo.Index.Col + 0.5) * 180);
                        var Y = ((ReverseTileIdY(tileInfo.Index.Row, 7) +0.5f) * 180);
                        go.transform.position = new Vector3((float)X, 0.1f, (float)Y);

                        Texture2D tex = new Texture2D(2, 2);
                        tex.LoadImage(ms.ToArray());
                        var mat = new Material(Shader.Find("Standard"));
                        mat.SetTextureOffset("_MainTex", new Vector2(0.5f, 0.5f));
                        mat.mainTexture = tex;

                        go.AddComponent<MeshFilter>().sharedMesh = m;
                        go.AddComponent<MeshRenderer>().sharedMaterial = mat; 

                    }
                    break;

            }

        }
    }

    private static float HackYOffset(int row)
    {
        switch (row)
        {
            case 0:
                return 180;
            case 1:
                return 450;
            case 2:
                return 585;
            case 3:
                return 675;
            case 4:
                return 765;
            case 5:
                return 855;
            case 6:
                return 990;
            case 7:
                return 1260;
            default: return 0;
        }
    }

    private static float HackY(int row)
    {
        switch (row)
              {
            case 0:
                return 2f;
            case 1:
                return 1f;
            case 2:
                return 0.5f;
            case 3:
                return 0.5f;
            case 4:
                return 0.5f;
            case 5:
                return 0.5f;
            case 6:
                return 1f;
            case 7:
                return 2f;
            default:
                return 1;
        }
    }

    private static float ReverseTileIdY(int row, int totalRows)
    {
        return totalRows - row;
    }

    private static List<TileInfo> GetOSMTiles(HttpTileSource tileSource, Extent extent, int zoomLevel)
    {
        var from = SpatialConvertor.ToSphericalMercatorFromWgs84(extent.MinX, extent.MinY);
        var to = SpatialConvertor.ToSphericalMercatorFromWgs84(extent.MaxX, extent.MaxY);
        var extentSphericalMercator = new Extent(from[0], from[1], to[0], to[1]);

        var tiles = GetTiles(tileSource, zoomLevel, extentSphericalMercator);
        return tiles;
    }

    private static List<TileInfo> GetTiles(HttpTileSource tileSource, int zoomLevel, Extent extent)
    {
        var tileRange = TileTransform.WorldToTile(extent, zoomLevel.ToString(), tileSource.Schema);
        var tiles = tileSource.Schema.GetTileInfos(extent, zoomLevel.ToString()).ToList();
        return tiles;
    }
}
