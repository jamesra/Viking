using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.AU
{
    static class State
    {
        static private string CacheSubPath = "Cache";

        static public string CachePath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\" + CacheSubPath;

        static public VolumeModel.Volume Volume;

        static public VolumeModel.MappingManager MappingsManager;
    }
}
