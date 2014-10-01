using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Viking.VolumeViewModel
{
    static class Global
    {
        static private string CacheSubPath = "Cache";
        static public string CachePath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\" + CacheSubPath;

        static public ImageBrushCache BrushCache = new ImageBrushCache();

        static public VolumeModel.Volume Volume = null;
    }
}
