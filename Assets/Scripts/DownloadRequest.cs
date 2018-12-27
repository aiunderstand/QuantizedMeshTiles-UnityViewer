using BruTile;
using UnityEngine;

namespace Assets.Scripts
{
    public struct DownloadRequest
    {

        public string Url;
        public TileServiceType Service;
        public Vector3 TileId;
        public Extent Extent;

        public DownloadRequest(string url, TileServiceType service, Vector3 tileId, Extent extent)
        {
            Url = url;
            Service = service;
            TileId = tileId;
            Extent = extent;
        }
    }
}
