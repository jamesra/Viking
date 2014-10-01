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
    public class TileGridMapping : TileGridMappingBase
    {  
         

        #region TextureFileNames

        public override string TileFullPath(int iX, int iY, int DownsampleLevel)
        {
            string filename = this.TileTextureFileName(iX, iY);

            /* Port
            string tileFileName = this.Section.Path +
                                System.IO.Path.DirectorySeparatorChar + TileGridPath +
                                System.IO.Path.DirectorySeparatorChar + DownsampleLevel.ToString("D3") +
                                System.IO.Path.DirectorySeparatorChar + filename;
             */

            string tileFileName = TileGridPath +
                                System.IO.Path.DirectorySeparatorChar + DownsampleLevel.ToString("D3") +
                                System.IO.Path.DirectorySeparatorChar + filename;
             
            return tileFileName;
        }

       

        /*PORT:
        protected string TileCacheName(int iX, int iY, int DownsampleLevel)
        {
            string filename = this.TilePrefix + "X" + iX.ToString("D3") + "_Y" + iY.ToString("D3") + this.TilePostfix;
            string cachePath = State.CachePath +
                                System.IO.Path.DirectorySeparatorChar + Section.volume.Name +
                                System.IO.Path.DirectorySeparatorChar + Section.SectionSubPath +
                                System.IO.Path.DirectorySeparatorChar + TileGridPath +
                                System.IO.Path.DirectorySeparatorChar + DownsampleLevel.ToString("D3") +
                                System.IO.Path.DirectorySeparatorChar + filename;
            return cachePath;
        }
         */

        #endregion

        protected TileGridMapping(TileGridMapping ToCopy, Section section, string name) :
            base(ToCopy, section, name)
        {
        }

        public TileGridMapping(Section section,
                               string name,
                               string Prefix, string Postfix,
                               int TileSizeX, int TileSizeY, 
                               string GridTilePath, 
                               string GridCoordFormat) :
            base(section, name, Prefix, Postfix, TileSizeX, TileSizeY, GridTilePath, GridCoordFormat)
        { 
        }

        public static TileGridMapping CreateFromTilesetElement(XElement TilesetNode, Section section)
        {
            string Name = IO.GetAttributeCaseInsensitive(TilesetNode, "name").Value;
            string mosaicTransformPath = IO.GetAttributeCaseInsensitive(TilesetNode, "path").Value;
            int TileSizeX = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(TilesetNode, "TileXDim").Value);
            int TileSizeY = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(TilesetNode, "TileYDim").Value);
            string TilePrefix = IO.GetAttributeCaseInsensitive(TilesetNode, "FilePrefix").Value;
            string TilePostfix = IO.GetAttributeCaseInsensitive(TilesetNode, "FilePostfix").Value;
            string TileGridPath = IO.GetAttributeCaseInsensitive(TilesetNode, "path").Value;
            string GridTileFormat = null;

            XAttribute GridTileFormatAttribute = TilesetNode.Attribute("CoordFormat"); 
            if(GridTileFormatAttribute != null)
            {
                GridTileFormat = TileGridMapping.GridTileFormatStringFromPythonString(GridTileFormatAttribute.Value.ToString());
            }

            TileGridMapping mapping = new TileGridMapping(section, Name, TilePrefix, TilePostfix,
                                                          TileSizeX, TileSizeY, TileGridPath, GridTileFormat);

            
            foreach (XNode node in TilesetNode.Nodes())
            {
                XElement elem = node as XElement;
                if (elem == null)
                    continue; 

                //Fetch the name if we know it
                switch (elem.Name.LocalName)
                {
                    case "Level":
                        mapping.AddLevel(System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "Downsample").Value),
                                       System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "GridDimX").Value),
                                       System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "GridDimY").Value),
                                       IO.GetAttributeCaseInsensitive(elem, "path").Value);
                        break; 
                }

            }

            return mapping;
        }
    }
}
