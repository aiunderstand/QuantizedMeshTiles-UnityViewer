using System;

namespace b3dm
{
    [Serializable]
    public class Rootobject
    {
        public Asset asset { get; set; }
        public int geometricError { get; set; }
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
        public int geometricError { get; set; }
        public Child[] children { get; set; }
        public string refine { get; set; }
        public float[] transform { get; set; }
    }

    [Serializable]
    public class Boundingvolume
    {
        public float[] box { get; set; }
    }

    [Serializable]
    public class Child
    {
        public Boundingvolume boundingVolume { get; set; }
        public float geometricError { get; set; }
        public Child[] children { get; set; }
        public string refine { get; set; }
        public Content content { get; set; }
    }

    [Serializable]
    public class Content
    {
        public string uri { get; set; }
    }
}