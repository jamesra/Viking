using System;

namespace Geometry.Transforms
{
    public interface ITransformBasicInfo
    {
        DateTime LastModified { get; }
    }

    public interface IStosTransformInfo : ITransformBasicInfo
    {
        int ControlSection { get; }
        int MappedSection { get; }

        string GetCacheFilename(string extension);
    }

    public interface ITileTransformInfo : ITransformBasicInfo
    {
        int TileNumber { get; }
        string TileFileName { get; }

        //We record these values because as tiles are transformed the dimensions of the mapped space can change and no longer match the image size
        double ImageWidth { get; }
        double ImageHeight { get; }
    }



    [Serializable]
    public class TransformBasicInfo : ITransformBasicInfo
    {
        /// <summary>
        /// This records the modified date of the file the transform was loaded from
        /// </summary>
        public readonly DateTime LastModified;

        public TransformBasicInfo()
        {
            this.LastModified = DateTime.MinValue;
        }

        public TransformBasicInfo(DateTime lastModified)
        {
            this.LastModified = lastModified;
        }

        DateTime ITransformBasicInfo.LastModified => LastModified;
    }

    [Serializable]
    public class StosTransformInfo : TransformBasicInfo, IStosTransformInfo
    {
        /// <summary>
        /// Space the transform maps to
        /// </summary>
        public readonly int ControlSection;

        /// <summary>
        /// Space the transform maps from
        /// </summary>
        public readonly int MappedSection;
          
        int IStosTransformInfo.ControlSection => ControlSection;

        int IStosTransformInfo.MappedSection => MappedSection;
            
        public StosTransformInfo(int cSection, int mSection) : base(DateTime.MinValue)
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

        public static StosTransformInfo Merge(IStosTransformInfo AtoB, IStosTransformInfo BtoC)
        {
            return new StosTransformInfo(BtoC.ControlSection,
                AtoB.MappedSection,
                BtoC.LastModified > AtoB.LastModified ? AtoB.LastModified : BtoC.LastModified);
        }
    }

    [Serializable]
    public class TileTransformInfo : TransformBasicInfo, ITileTransformInfo
    {
        public readonly int TileNumber;
        public readonly string TileFileName;

        //We record these values because as tiles are transformed the dimensions of the mapped space can change and no longer match the image size
        public readonly double ImageWidth;
        public readonly double ImageHeight;

        public TileTransformInfo(string TileFileName, int tileNumber, DateTime lastModified, double Width, double Height)
            : base(lastModified)
        {

            this.TileFileName = TileFileName;
            this.TileNumber = tileNumber;
            this.ImageWidth = Width;
            this.ImageHeight = Height;
        }

        int ITileTransformInfo.TileNumber => TileNumber;

        string ITileTransformInfo.TileFileName => TileFileName;

        double ITileTransformInfo.ImageWidth => ImageWidth;

        double ITileTransformInfo.ImageHeight => ImageHeight;
          
        public override string ToString()
        {
            return TileFileName;
        }
    }

    /// <summary>
    /// Transforms can expose this interface for to provide storage location info for serializing transforms
    /// </summary>
    [Serializable]
    public readonly struct TransformCacheInfo : ITransformCacheInfo
    {
        private readonly string cacheDirectory;
        private readonly string _FilenameBase;
        private readonly string _Extension;

        public TransformCacheInfo(string CacheDirectory, string Filename, string extension=".stos_bin")
        {
            _Extension = extension;
            _FilenameBase = System.IO.Path.GetFileNameWithoutExtension(Filename);
            _Extension = System.IO.Path.GetExtension(Filename);
            cacheDirectory = CacheDirectory;
        }

        public string CacheDirectory => cacheDirectory;

        public string CacheFilename => _FilenameBase + _Extension;

        public string CacheFullPath => System.IO.Path.Combine(this.CacheDirectory, this.CacheFilename);

        public string Extension => _Extension;
    }

}

