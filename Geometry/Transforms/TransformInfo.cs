using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

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
        public int ControlSection;

        /// <summary>
        /// Space the transform maps from
        /// </summary>
        public int MappedSection;

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

        public override string ToString()
        {
            return MappedSection.ToString() + " to " + ControlSection.ToString();
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
        
        /*
         * 
#region ISerializable

        public TileGridTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            TileWidth = info.GetInt32("ImageWidth");
            TileHeight = info.GetInt32("ImageHeight");
            Number = info.GetInt32("Number");
            TileFileName = info.GetString("TileFileName");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("TileFileName", TileFileName);
            info.AddValue("ImageWidth", TileWidth);
            info.AddValue("ImageHeight", TileHeight);
            info.AddValue("Number", Number);
        }
        
#endregion
         * 
         */

    }

}

