using UnityEngine;

namespace Assets.Scripts
{
    public struct DownloadRequest
    {

        public string Url;
        public TileServiceType Service;
        public Vector3 TileId;

        public DownloadRequest(string url, TileServiceType service, Vector3 tileId)
        {
            Url = url;
            Service = service;
            TileId = tileId;
        }
    }
}
