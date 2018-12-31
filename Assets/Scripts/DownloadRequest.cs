using Terrain;
using UnityEngine;

namespace Assets.Scripts
{
    public struct DownloadRequest
    {

        public string Url;
        public DataType Datatype;
        public Vector3 TileId;
        
        public DownloadRequest(string url, DataType datatype, Vector3 tileId)
        {
            Url = url;
            Datatype = datatype;
            TileId = tileId;
        }
    }
}
