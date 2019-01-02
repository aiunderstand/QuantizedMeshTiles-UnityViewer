using System;

namespace b3dm_pnts
{
    [Serializable]
    public class Rootobject
    {
        public Asset asset { get; set; }
        public float geometricError { get; set; }
        public Root root { get; set; }
    }

    [Serializable]
    public class Asset
    {
        public string version { get; set; }
    }

    [Serializable]
    public class Root
    {
        public Boundingvolume boundingVolume { get; set; }
        public Child[] children { get; set; }
        public Content content { get; set; }
        public float geometricError { get; set; }
        public string refine { get; set; }
    }

    [Serializable]
    public class Boundingvolume
    {
        public float[] box { get; set; }
    }

    [Serializable]
    public class Content
    {
        public string url { get; set; }
    }

    [Serializable]
    public class Child
    {
        public Boundingvolume boundingVolume { get; set; }
        public Content content { get; set; }
        public float geometricError { get; set; }
        public Child[] children { get; set; }
    }
}