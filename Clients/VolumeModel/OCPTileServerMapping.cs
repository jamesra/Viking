using Geometry;
using System;
using System.Linq;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Handles mapping tiles that are fetched from an Open Connectome Project server.  Repurposed from nornir-web tile server code.
    /// </summary>
    public class OCPTileServerMapping : TileGridMappingBase
    {
        protected readonly string Host; //Host for tile paths, Viking will set to volume host if null
        protected readonly string ChannelName; //Host for tile paths, Viking will set to volume host if null

        #region TextureFileNames

        override public string TileFullPath(int iX, int iY, int DownsampleLevel)
        {
            string tileFileName = ((int)Math.Log(DownsampleLevel, 2)).ToString("D3") +
                                '/' + this.TileTextureFileName(iX, iY);

            tileFileName = this.Host + '/' +
                           this.Section.volume.Name + '/' +
                           TileGridPath + '/' +
                           this.ChannelName + '/' +
                           tileFileName;

            return tileFileName;
        }

        protected override string TileTextureFileName(int iX, int iY)
        {
            return this.TilePrefix + "X" + iX.ToString("D3") + "_Y" + iY.ToString("D3") + "_Z" + this.Section.Number.ToString("D3") + TilePostfix;
        }

        protected override string TileTextureCacheFileName(int downsample, int iX, int iY)
        {
            return this.ChannelName + System.IO.Path.DirectorySeparatorChar + downsample.ToString("D3") + System.IO.Path.DirectorySeparatorChar + TileTextureFileName(iX, iY);
        }

        #endregion

        protected OCPTileServerMapping(OCPTileServerMapping ToCopy, Section section, string name) :
            base(ToCopy, section, name)
        {
            this.Host = ToCopy.Host;
            this.ChannelName = ToCopy.ChannelName;
        }

        public OCPTileServerMapping(Section section,
                                 string Name,
                                 string channelName,
                                 string Prefix, string Postfix,
                                 int TileSizeX, int TileSizeY,
                                 string TileServerHost,
                                 string TileGridPath,
                                 string GridCoordFormat = null) :
            base(section, Name, Prefix, Postfix, TileSizeX, TileSizeY, TileGridPath, GridCoordFormat, null)
        {
            this.Host = TileServerHost;
            this.ChannelName = channelName;
        }

        public void PopulateLevels(int MaxLevel, int GridDimX, int GridDimY)
        {
            for (int CurrentLevel = 0; CurrentLevel <= MaxLevel; CurrentLevel++)
            {
                int downsample = (int)Math.Pow(2, CurrentLevel);
                this.AddLevel(downsample, (int)Math.Ceiling((double)GridDimX / downsample), (int)Math.Ceiling((double)GridDimY / downsample), CurrentLevel.ToString("D3"));
            }
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
