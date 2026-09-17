using System;

namespace Viking.AU
{
    static class State
    {
        private static readonly string CacheSubPath = "Cache";

        public static string CachePath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\" + CacheSubPath;

        public static VolumeModel.Volume Volume;

        public static VolumeModel.MappingManager MappingsManager;
    }
}
