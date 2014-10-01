using System;
using System.Collections.Generic;
using System.Text;
using System.Xml; 
using System.Xml.Linq;
using System.Diagnostics;
using Utils;

using Geometry;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Tile grid mappings refer to a pre-assembled set of tiles, where the tile size is fixed
    /// to the same value at every level of the pyramid, so the area must change
    /// </summary>
    public class TileServerMapping : TileGridMappingBase
    {
        protected readonly string Host; //Host for tile paths, Viking will set to volume host if null
        protected readonly string CoordSpaceName; //Host for tile paths, Viking will set to volume host if null
          
        #region TextureFileNames

        override public string TileFullPath(int iX, int iY, int DownsampleLevel)
        {
            string tileFileName = TileGridPath +
                                '/' + DownsampleLevel.ToString("D3") +
                                '/' + this.TileTextureFileName(iX, iY);
             
            tileFileName = this.Host + '/' +
                           this.Section.volume.Name + '/' +
                           this.CoordSpaceName + '/' + 
                           this.Section.Number.ToString() + '/' +
                           tileFileName;

            return tileFileName;
        }

        protected virtual string TileTextureCacheFileName(int downsample, int iX, int iY)
        {
            return this.CoordSpaceName + System.IO.Path.DirectorySeparatorChar + downsample.ToString("D3") + System.IO.Path.DirectorySeparatorChar + TileTextureFileName(iX, iY);
        }

        #endregion

        protected TileServerMapping(TileServerMapping ToCopy, Section section, string name) :
            base(ToCopy, section, name)
        { 
            this.Host = ToCopy.Host;
            this.CoordSpaceName = ToCopy.CoordSpaceName;
        }

        public TileServerMapping(Section section,
                                 string name,
                                 string Prefix, string Postfix,
                                 int TileSizeX, int TileSizeY,
                                 string TileServerHost, 
                                 string CoordSpaceName, 
                                 string TileGridPath,
                                 string GridCoordFormat = null) :
            base(section, name, Prefix, Postfix, TileSizeX, TileSizeY, TileGridPath, GridCoordFormat)
        {
            this.Host = TileServerHost;
            this.CoordSpaceName = CoordSpaceName;
        } 
    }
}
