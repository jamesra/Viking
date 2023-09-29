using System;

namespace Viking.AU
{
    static class State
    {
        static private readonly string CacheSubPath = "Cache";

        static public string CachePath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\" + CacheSubPath;

        static public VolumeModel.Volume Volume;

        static public VolumeModel.MappingManager MappingsManager;
    }
}
