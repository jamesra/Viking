using Geometry;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Utils;

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
                               string GridCoordFormat,
                               UnitsAndScale.IAxisUnits XYScale) :
            base(section, name, Prefix, Postfix, TileSizeX, TileSizeY, GridTilePath, GridCoordFormat, XYScale)
        {
        }

        public override bool Initialized => true;

        public override Task Initialize(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public static TileGridMapping CreateFromTilesetElement(XElement TilesetNode, Section section)
        {
            string Name = TilesetNode.GetAttributeCaseInsensitive("name").Value;
            string mosaicTransformPath = TilesetNode.GetAttributeCaseInsensitive("path").Value;
            int TileSizeX = System.Convert.ToInt32(TilesetNode.GetAttributeCaseInsensitive("TileXDim").Value);
            int TileSizeY = System.Convert.ToInt32(TilesetNode.GetAttributeCaseInsensitive("TileYDim").Value);
            string TilePrefix = TilesetNode.GetAttributeCaseInsensitive("FilePrefix").Value;
            string TilePostfix = TilesetNode.GetAttributeCaseInsensitive("FilePostfix").Value;
            string TileGridPath = TilesetNode.GetAttributeCaseInsensitive("path").Value;
            string GridTileFormat = null;

            XElement scale_elem = TilesetNode.Elements().Where(elem => elem.Name.LocalName == "Scale").FirstOrDefault();
            UnitsAndScale.IAxisUnits XYScale = null;
            if (scale_elem != null)
                XYScale = scale_elem.ParseScale();
            else
            {
                //If we do not have a specific scale, assume the scale matches the section's scale
                XYScale = section.XYScale;
            }

            XAttribute GridTileFormatAttribute = TilesetNode.Attribute("CoordFormat");
            if (GridTileFormatAttribute != null)
            {
                GridTileFormat = TileGridMapping.GridTileFormatStringFromPythonString(GridTileFormatAttribute.Value.ToString());
            }

            //If the tileset node has no entries, then don't create a TileGridMapping
            if (TilesetNode.Nodes().Any() == false)
                return null;

            TileGridMapping mapping = new TileGridMapping(section, Name, TilePrefix, TilePostfix,
                                                          TileSizeX, TileSizeY, TileGridPath, GridTileFormat, XYScale);


            foreach (XNode node in TilesetNode.Nodes())
            {
                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                //Fetch the name if we know it
                switch (elem.Name.LocalName)
                {
                    case "Level":
                        mapping.AddLevel(System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("Downsample").Value),
                                       System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("GridDimX").Value),
                                       System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("GridDimY").Value),
                                       elem.GetAttributeCaseInsensitive("path").Value);
                        break;
                }
            }

            return mapping;
        }

        public override bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            transformedP = P;
            return true;
        }

        public override bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            transformedP = P;
            return true;
        }
        public override GridVector2[] VolumeToSection(GridVector2[] P)
        {
            GridVector2[] transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }


        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
        }

        public override GridVector2[] SectionToVolume(GridVector2[] P)
        {
            GridVector2[] transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }
    }
}
