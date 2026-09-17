using System.IO;

namespace Viking.VolumeModel
{
    public class VolumePaths(string localCachePath, string VolumeName)
    {
        /// <summary>
        /// The path we use to cache data on the local drive
        /// </summary>
        internal readonly string LocalCachePath = localCachePath;

        internal readonly string Name = VolumeName;

        private string VolumeCachePath => this.LocalCachePath + System.IO.Path.DirectorySeparatorChar + this.Name;

        /// <summary>
        /// Server-side stos files loaded from .zip file listed in .vikingxml file
        /// </summary>
        public string ServerStosCachePath => this.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "StosZip";


        string _LocalVolumeDir = null;

        /// <summary>
        /// The directory we use for local files, typically cache files.
        /// </summary>
        public string LocalVolumeDir
        {
            get
            {
                if (_LocalVolumeDir is null)
                {
                    _LocalVolumeDir = System.IO.Path.Combine(LocalCachePath, this.Name) + System.IO.Path.DirectorySeparatorChar;
                    if (!System.IO.Directory.Exists(_LocalVolumeDir))
                    {
                        Directory.CreateDirectory(_LocalVolumeDir);
                    }
                }

                return _LocalVolumeDir;
            }
        }

        private string _StosCacheDir = null;
        /// <summary>
        /// Directory we use to cache stos transforms
        /// </summary>
        public string StosCacheDir
        {
            get
            {
                if (_StosCacheDir is null)
                {
                    _StosCacheDir = System.IO.Path.Combine(this.LocalVolumeDir, "Stos");
                    if (!System.IO.Directory.Exists(_StosCacheDir))
                    {
                        Directory.CreateDirectory(_StosCacheDir);
                    }
                }

                return _StosCacheDir;
            }
        }

        public string GetStosCacheName(long mappedSection, long controlSection, string extension) => System.IO.Path.Combine(this.StosCacheDir, mappedSection.ToString() + "-" + controlSection.ToString() + extension);

        public string GetITKSCacheName(long mappedSection, long controlSection) => GetStosCacheName(mappedSection, controlSection, ".stos");

        public string GetSerializerCacheName(long mappedSection, long controlSection) => GetStosCacheName(mappedSection, controlSection, ".stos_bin");

        public static void CreateDirectories(VolumePaths paths)
        {
            //Create a path for the cache  
            if (System.IO.Directory.Exists(paths.VolumeCachePath) == false)
                System.IO.Directory.CreateDirectory(paths.VolumeCachePath);

            if (System.IO.Directory.Exists(paths.LocalVolumeDir) == false)
                System.IO.Directory.CreateDirectory(paths.LocalVolumeDir);
        }
    }
}
