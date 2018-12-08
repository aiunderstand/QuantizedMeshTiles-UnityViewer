using UnityEngine;

namespace Terrain
{
    public struct Viewport
    {
        public Vector3 center;
        public int height;
        public int width;

        public Viewport(Vector3 center, int height, int width)
        {
            this.center = center;
            this.height = height;
            this.width = width;
        }
    }
}