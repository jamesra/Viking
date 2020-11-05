using System;

namespace Geometry.Transforms
{

    [Serializable]
    public class TransformInfo
    {
        /// <summary>
        /// This records the modified date of the file the transform was loaded from
        /// </summary>
        public readonly DateTime LastModified = DateTime.MinValue;

        public TransformInfo()
        {
        }

        public TransformInfo(DateTime lastModified)
        {
            this.LastModified = lastModified;
        }
    }

    [Serializable]
    public class StosTransformInfo : TransformInfo
    {
        /// <summary>
        /// Space the transform maps to
        /// </summary>
        public readonly int ControlSection;

        /// <summary>
        /// Space the transform maps from
        /// </summary>
        public readonly int MappedSection;

        public StosTransformInfo(int cSection, int mSection)
        {
            this.ControlSection = cSection;
            this.MappedSection = mSection;
        }

        public StosTransformInfo(int cSection, int mSection, DateTime lastModified)
            : base(lastModified)
        {
            this.ControlSection = cSection;
            this.MappedSection = mSection;
        }

        public string GetCacheFilename(string extension)
        {
            return MappedSection.ToString() + "-" + ControlSection.ToString() + extension;
        }

        public override string ToString()
        {
            return MappedSection.ToString() + " to " + ControlSection.ToString();
        }

        public static StosTransformInfo Merge(StosTransformInfo AtoB, StosTransformInfo BtoC)
        {
            return new StosTransformInfo(BtoC.ControlSection,
                                         AtoB.MappedSection,
                                         BtoC.LastModified > AtoB.LastModified ? AtoB.LastModified : BtoC.LastModified);
        }
    }

    [Serializable]
    public class TileTransformInfo : TransformInfo
    {
        public readonly int TileNumber;
        public string TileFileName;

        //We record these values because as tiles are transformed the dimensions of the mapped space can change and no longer match the image size
        public double ImageWidth;
        public double ImageHeight;


        public TileTransformInfo(string TileFileName, int tileNumber, DateTime lastModified, double Width, double Height)
            : base(lastModified)
        {

            this.TileFileName = TileFileName;
            this.TileNumber = tileNumber;
            this.ImageWidth = Width;
            this.ImageHeight = Height;
        }

        public override string ToString()
        {
            return TileFileName;
        }
    }

    /// <summary>
    /// Transforms can expose this interface for to provide storage location info for serializing transforms
    /// </summary>
    [Serializable]
    public class TransformCacheInfo : ITransformCacheInfo
    {
        private readonly string cacheDirectory;
        private string _FilenameBase = null;
        private string _Extension = null;

        public TransformCacheInfo(string CacheDirectory, string Filename)
        {
            _Extension = ".stos_bin";
            _FilenameBase = System.IO.Path.GetFileNameWithoutExtension(Filename);
            _Extension = System.IO.Path.GetExtension(Filename);
            cacheDirectory = CacheDirectory;
        }

        public string CacheDirectory
        {
            get
            {
                return cacheDirectory;
            }
        }

        public string CacheFilename
        {
            get
            {
                return _FilenameBase + _Extension;
            }
        }

        public string CacheFullPath
        {
            get
            {
                return System.IO.Path.Combine(this.CacheDirectory, this.CacheFilename);
            }
        }

        public string Extension
        {
            get
            {
                return _Extension;
            }

            set
            {
                _Extension = value;
            }
        }
    }

}

