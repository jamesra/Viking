using System;
using System.Collections.Generic;
using System.Linq;

using Geometry;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Tile grid mappings refer to a pre-assembled set of tiles, where the tile size is fixed
    /// to the same value at every level of the pyramid, so the area must change
    /// </summary>
    public abstract class TileGridMappingBase : MappingBase
    {
        protected struct GridInfo
        {
            public readonly int GridXDim;
            public readonly int GridYDim;
            public readonly int Downsample;
            public readonly string Path;
           

            public GridInfo(int XDim, int YDim, int downsample, string path)
            {
                GridXDim = XDim;
                GridYDim = YDim;
                Downsample = downsample;
                this.Path = path; 
            }
        }

        protected readonly int TileSizeX;
        protected readonly int TileSizeY;
        protected readonly int TotalTileSize;
        protected readonly string GridCoordFormat = "D3";

        private int _MaxDownsample = int.MinValue;
        private int _MinDownsample = int.MaxValue;

        protected AxisUnits _XYScale;
        public Geometry.AxisUnits XYScale
        {
            get
            {
                return _XYScale;
            }
        }

        /// <summary>
        /// Adjust the downsample level to match the difference between the scale used in the pyramid/mapping and the default scale for the volume
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected double AdjustDownsampleForScale(double input)
        {
            if (this.XYScale == null)
                return input;

            double relative_scale = this.XYScale.Value / this.Section.XYScale.Value;
            return input / relative_scale;
        }

        public int MaxDownsample
        {
            get { return _MaxDownsample; }
            protected set { _MaxDownsample = value; }
        }

        public int MinDownsample
        {
            get { return _MinDownsample; }
            protected set { _MinDownsample = value; }
        }

        protected readonly string TileGridPath; 

        protected SortedDictionary<int, GridInfo> LevelToGridInfo = new SortedDictionary<int, GridInfo>();
         
        private int[] _AvailableLevels = null;
        public override int[] AvailableLevels
        {
            get
            {
                if (_AvailableLevels == null)
                {
                    _AvailableLevels = new int[LevelToGridInfo.Keys.Count];
                    LevelToGridInfo.Keys.CopyTo(_AvailableLevels, 0);
                }

                return _AvailableLevels;
            }
        }

        public override GridRectangle ControlBounds
        {
            get 
            {
                GridInfo Level = LevelToGridInfo[MaxDownsample];
                return new GridRectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY); 
            }
        }

        public override GridRectangle? SectionBounds
        {
            get
            {
                GridInfo Level = LevelToGridInfo[MaxDownsample];
                return new GridRectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY);
            }
        }

        public override GridRectangle? VolumeBounds
        {
            get
            {
                GridInfo Level = LevelToGridInfo[MaxDownsample];
                return new GridRectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY);
            }
        }
        
        /*
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
        */
        #region TextureFileNames

        public abstract string TileFullPath(int iX, int iY, int DownsampleLevel);

        protected virtual string TileTextureCacheFileName(int downsample, int iX, int iY)
        {
            return this.Name + System.IO.Path.DirectorySeparatorChar + downsample.ToString("D3") + System.IO.Path.DirectorySeparatorChar + TileTextureFileName(iX, iY);
        }

        /// <summary>
        /// Provides the filename for a tile at the given grid coordinates
        /// </summary>
        /// <param name="iX"></param>
        /// <param name="iY"></param>
        /// <returns></returns>
        protected virtual string TileTextureFileName(int iX, int iY)
        {
            return this.TilePrefix + "X" + iX.ToString("D3") + "_Y" + iY.ToString("D3") + this.TilePostfix;
        }
        
        #endregion
        
        /// <summary>
        /// C# has reverse formatting notation compared to the python scripts which generate VikingXML files.  If the format starts with a number instead of a letter this 
        /// function will correct the issue by swapping them
        /// </summary>
        /// <param name="gridFormat"></param>
        /// <returns></returns>
        public static string GridTileFormatStringFromPythonString(string gridFormat)
        {
            string outputFormat = string.Copy(gridFormat);
            if (!char.IsLetter(gridFormat[0]))
            { 
                if (char.IsLetter(gridFormat[gridFormat.Length - 1]))
                {
                    outputFormat = outputFormat[gridFormat.Length - 1] + outputFormat.Substring(0, gridFormat.Length - 1);
                }
            }

            return outputFormat;

        }


        protected TileGridMappingBase(TileGridMappingBase ToCopy, Section section, string name) :
            base(section, name, ToCopy.TilePrefix, ToCopy.TilePostfix)
        {
            TileSizeX = ToCopy.TileSizeX; 
            TileSizeY = ToCopy.TileSizeY;
            TotalTileSize = ToCopy.TotalTileSize; 
            TileGridPath = ToCopy.TileGridPath;
            MinDownsample = ToCopy.MinDownsample;
            MaxDownsample = ToCopy.MaxDownsample;
            this.GridCoordFormat = ToCopy.GridCoordFormat;
            this._XYScale = ToCopy.XYScale;

            foreach(GridInfo info in ToCopy.LevelToGridInfo.Values)
            {
                GridInfo infoCopy = new GridInfo(info.GridXDim, info.GridYDim, info.Downsample, info.Path); 
                LevelToGridInfo.Add(infoCopy.Downsample, infoCopy); 
            }
        }

        public TileGridMappingBase(Section section, string name, string Prefix, string Postfix, int TileSizeX, int TileSizeY, string TileGridPath, string GridCoordFormat, AxisUnits XYScale) :
            base(section, name, Prefix, Postfix )
        {
            this.TileSizeX = TileSizeX;
            this.TileSizeY = TileSizeY; 
            this.TotalTileSize = TileSizeX * TileSizeY;
            this.TileGridPath = TileGridPath;
            this._XYScale = XYScale;
            if(GridCoordFormat != null)
                this.GridCoordFormat = GridCoordFormat; 
        }

        /// <summary>
        /// Add a level to the tile grid mapping
        /// </summary>
        /// <param name="Downsample"></param>
        /// <param name="GridDimX">Number of tiles on X axis</param>
        /// <param name="GridDimY">Number of tiles on Y axis</param>
        /// <param name="LevelPath">Path to level data</param>
        /// <returns></returns>
        public void AddLevel(int Downsample, int GridDimX, int GridDimY, string LevelPath)
        {
            if (Downsample > this.MaxDownsample)
                this.MaxDownsample = Downsample;

            if (Downsample < this.MinDownsample)
                this.MinDownsample = Downsample;

            GridInfo Level = new GridInfo(GridDimX, GridDimY, Downsample, LevelPath);
            LevelToGridInfo.Add(Downsample, Level);
            this._AvailableLevels = null;
        }

        protected virtual PositionNormalTextureVertex[] CalculateVerticies(int iX, int iY, int roundedDownsample)
        {
            PositionNormalTextureVertex[] verticies = new PositionNormalTextureVertex[4];

            verticies[0] = new PositionNormalTextureVertex(new GridVector3(iX * this.TileSizeX * roundedDownsample, iY * this.TileSizeY * roundedDownsample, 0),
                                                               GridVector3.UnitZ,
                                                           new GridVector2(0, 0));
            verticies[1] = new PositionNormalTextureVertex(new GridVector3((iX + 1) * this.TileSizeX * roundedDownsample, iY * this.TileSizeY * roundedDownsample, 0),
                                                               GridVector3.UnitZ,
                                                           new GridVector2(1, 0));
            verticies[2] = new PositionNormalTextureVertex(new GridVector3(iX * this.TileSizeX * roundedDownsample, (iY + 1) * this.TileSizeY * roundedDownsample, 0),
                                                               GridVector3.UnitZ,
                                                           new GridVector2(0, 1));
            verticies[3] = new PositionNormalTextureVertex(new GridVector3((iX + 1) * this.TileSizeX * roundedDownsample, (iY + 1) * this.TileSizeY * roundedDownsample, 0),
                                                               GridVector3.UnitZ,
                                                           new GridVector2(1, 1));

            return verticies; 
        }

        protected virtual int[] TriangleEdges
        {
            get { return new int[] { 0, 1, 2, 1, 3, 2 }; }
        }

        public override TilePyramid VisibleTiles(GridRectangle VisibleBounds, double DownSample)
        {
            TilePyramid VisibleTiles = new TilePyramid(VisibleBounds);

            double scaledDownsampleLevel = AdjustDownsampleForScale(DownSample);

            int roundedDownsample = NearestAvailableLevel(scaledDownsampleLevel); 

            //Starting with low-res tiles, add tiles to the list until we reach desired resolution
//            List<Tile> TilesToDraw = new List<Tile>(); 

            //Find the starting level of our rendering
            int iLevel = AvailableLevels.Length - 1;
            int level = AvailableLevels[iLevel];

            do
            {
                List<Tile> newTiles = RecursiveVisibleTiles(
                    VisibleBounds,
                    level 
                    //PORT: AsynchTextureLoad
                    );

                //Insert at the beginning so we overwrite earlier tiles with poorer resolution
                VisibleTiles.AddTiles(level, newTiles);
                //TilesToDraw.AddRange(newTiles);

                iLevel--;
                if(iLevel >= 0)
                    level = AvailableLevels[iLevel];
            }
            while (level >= roundedDownsample && iLevel >= 0);

            //Trace.WriteLine("Drawing " + TilesToDraw.Count.ToString() + " Tiles", "VolumeModel");

            return VisibleTiles; 
        }


        private List<Tile> RecursiveVisibleTiles(GridRectangle VisibleBounds, int roundedDownsample)
        {
            GridInfo gridInfo = LevelToGridInfo[roundedDownsample]; 

            int ScaledTileSizeX = this.TileSizeX * roundedDownsample;
            int ScaledTileSizeY = this.TileSizeX * roundedDownsample; 

            //Figure out which grid locations are visible
            int iMinX = (int)Math.Floor(VisibleBounds.Left / ScaledTileSizeX);
            int iMinY = (int)Math.Floor(VisibleBounds.Bottom / ScaledTileSizeY);
            int iMaxX = (int)Math.Ceiling(VisibleBounds.Right / ScaledTileSizeX);
            int iMaxY = (int)Math.Ceiling(VisibleBounds.Top / ScaledTileSizeY);

            iMinX = iMinX < 0 ? 0 : iMinX;
            iMinY = iMinY < 0 ? 0 : iMinY;
            iMaxX = iMaxX >= gridInfo.GridXDim ? gridInfo.GridXDim : iMaxX;
            iMaxY = iMaxY >= gridInfo.GridYDim ? gridInfo.GridYDim : iMaxY;

            if (iMaxX < 0)
                iMaxX = 0;
            if (iMaxY < 0)
                iMaxY = 0;
            if (iMinX > iMaxX)
                iMinX = iMaxX;
            if (iMinY > iMaxY)
                iMinY = iMaxY; 

            int ExpectedTileCount = (iMaxX - iMinX) * (iMaxY - iMinY);
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);

            for (int iX = iMinX; iX < iMaxX; iX++)
            {
                for (int iY = iMinY; iY < iMaxY; iY++)
                {
                    string UniqueID = Tile.CreateUniqueKey(Section.Number, Name, Name, roundedDownsample, this.TileTextureFileName(iX, iY));
                    string TextureFileName = TileFullPath(iX, iY, roundedDownsample);
                    Tile tile = Global.TileCache.Fetch(UniqueID);
                    if (tile == null)
                    {
                        //First create a new tile
                        //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
                        PositionNormalTextureVertex[] verticies = CalculateVerticies(iX, iY, roundedDownsample);
                        int MipMapLevels = 1; //No mip maps

                        if (roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                            MipMapLevels = 0; //Generate mipmaps for lowest res texture

                       
                        tile = Global.TileCache.ConstructTile(UniqueID,
                                                            verticies,
                                                            this.TriangleEdges,
                                                            TextureFileName,
                                                            TileTextureCacheFileName(roundedDownsample, iX, iY),
                                                            //PORT TextureCacheFileName,
                                                            this.Name,
                                                            roundedDownsample, 
                                                            MipMapLevels);

                        //Check for tiles at higher resolution
//                        int iTempX = iX / 2;
//                        int iTempY = iY / 2;
//                        int iTempDownsample = roundedDownsample * 2;



                    }

                    TilesToDraw.Add(tile);
                }
            }

            return TilesToDraw;
        }
    }
}
