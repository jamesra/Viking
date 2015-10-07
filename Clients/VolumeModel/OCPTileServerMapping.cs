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
    /// Handles mapping tiles that are fetched from an Open Connectome Project server.  Repurposed from nornir-web tile server code.
    /// </summary>
    public class OCPTileServerMapping : TileGridMappingBase
    {
        protected readonly string Host; //Host for tile paths, Viking will set to volume host if null
        protected readonly string CoordSpaceName; //Host for tile paths, Viking will set to volume host if null
          
        #region TextureFileNames

        override public string TileFullPath(int iX, int iY, int DownsampleLevel)
        {
            string tileFileName = TileGridPath +
                                '/' + ((int)Math.Log(DownsampleLevel,2)).ToString("D3") +
                                '/' + this.TileTextureFileName(iX, iY);
             
            tileFileName = this.Host + '/' +
                           this.Section.volume.Name + '/' +
                           this.CoordSpaceName + '/' + 
                           tileFileName;

            return tileFileName;
        }

        protected override string TileTextureFileName(int iX, int iY)
        {
            return this.TilePrefix + "X" + iX.ToString("D3") + "_Y" + iY.ToString("D3") + "_Z" + this.Section.Number.ToString("D3") + TilePostfix;
        }

        protected override string TileTextureCacheFileName(int downsample, int iX, int iY)
        {
            return this.CoordSpaceName + System.IO.Path.DirectorySeparatorChar + downsample.ToString("D3") + System.IO.Path.DirectorySeparatorChar + TileTextureFileName(iX, iY);
        }

        #endregion

        protected OCPTileServerMapping(OCPTileServerMapping ToCopy, Section section, string name) :
            base(ToCopy, section, name)
        { 
            this.Host = ToCopy.Host;
            this.CoordSpaceName = ToCopy.CoordSpaceName;
        }

        public OCPTileServerMapping(Section section,
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
