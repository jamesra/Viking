using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Tile grid mappings refer to a pre-assembled set of tiles, where the tile size is fixed
    /// to the same value at every level of the pyramid, so the area must change
    /// </summary>
    public abstract class TileGridMappingBase : MappingBase
    {
        protected readonly struct TileKey(int x, int y, int downsample) : IEquatable<TileKey>
        {

            public readonly int X = x;
            public readonly int Y = y;
            public readonly int Downsample = downsample;

            public override bool Equals(object obj)
            {
                if (obj is not TileKey)
                    return false;

                TileKey tk = (TileKey)obj;

                return this.X == tk.X && this.Y == tk.Y &&
                       this.Downsample == tk.Downsample;
            }

            public bool Equals(TileKey tk)
            {
                return this.X == tk.X && this.Y == tk.Y &&
                       this.Downsample == tk.Downsample;
            }

            public override int GetHashCode()
            {
                // Simple prime number multiplication approach
                unchecked // Allow arithmetic overflow, wrapping without exceptions
                {
                    int hash = 17; // Prime number starting value
                    hash = hash * 23 + X;
                    hash = hash * 23 + Y;
                    hash = hash * 23 + Downsample;
                    return hash;
                }
            }
        }

        protected readonly struct CreateTileTaskResult(TileViewModel tile, TileGridMappingBase.TileKey key)
        {
            public readonly TileViewModel Tile = tile;
            public readonly TileKey Key = key;
        }

        protected readonly struct GridInfo(int XDim, int YDim, int downsample, string path)
        {
            public readonly int GridXDim = XDim;
            public readonly int GridYDim = YDim;
            public readonly int Downsample = downsample;
            public readonly string Path = path;
        }

        protected readonly int TileSizeX;
        protected readonly int TileSizeY;
        protected readonly int TotalTileSize;
        protected readonly string GridCoordFormat = "D3";

        private int _MaxDownsample = int.MinValue;
        private int _MinDownsample = int.MaxValue;

        protected ConcurrentDictionary<TileKey, Task<CreateTileTaskResult>> TileTasks = new();

        public int MaxDownsample
        {
            get => _MaxDownsample;
            protected set => _MaxDownsample = value;
        }

        public int MinDownsample
        {
            get => _MinDownsample;
            protected set => _MinDownsample = value;
        }

        protected readonly string TileGridPath;

        protected SortedDictionary<int, GridInfo> LevelToGridInfo = [];

        private int[] _AvailableLevels = null;
        public override int[] AvailableLevels
        {
            get
            {
                if (_AvailableLevels is null)
                {
                    _AvailableLevels = new int[LevelToGridInfo.Keys.Count];
                    LevelToGridInfo.Keys.CopyTo(_AvailableLevels, 0);
                }

                return _AvailableLevels;
            }
        }

        public override Rectangle ControlBounds
        {
            get
            {
                GridInfo Level = LevelToGridInfo[MinDownsample];
                return new Rectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY);
            }
        }

        public override Rectangle? SectionBounds
        {
            get
            {
                GridInfo Level = LevelToGridInfo[MinDownsample];
                return new Rectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY);
            }
        }

        public override Rectangle? VolumeBounds
        {
            get
            {
                GridInfo Level = LevelToGridInfo[MinDownsample];
                return new Rectangle(0, Level.GridXDim * Level.Downsample * TileSizeX,
                                         0, Level.GridYDim * Level.Downsample * TileSizeY);
            }
        }

        /*
        public override bool TrySectionToVolume(Vector2 P, out Vector2 transformedP)
        {
            transformedP = P;
            return true; 
        }

        public override bool TryVolumeToSection(Vector2 P, out Vector2 transformedP)
        {
            transformedP = P;
            return true; 
        }
        public override Vector2[] VolumeToSection(Vector2[] P)
        {
            Vector2[] transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }


        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(Vector2[] P, out Vector2[] transformedP)
        {
            transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(Vector2[] P, out Vector2[] transformedP)
        {
            transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
        }

        public override Vector2[] SectionToVolume(Vector2[] P)
        {
            Vector2[] transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }
        */
        #region TextureFileNames

        public abstract string TileFullPath(int iX, int iY, int DownsampleLevel);

        protected virtual string TileTextureCacheFileName(int downsample, int iX, int iY)
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            return $"{Name}{sep}{downsample:D3}{sep}{TileTextureFileName(iX, iY)}";
        }

        /// <summary>
        /// Provides the filename for a tile at the given grid coordinates
        /// </summary>
        /// <param name="iX"></param>
        /// <param name="iY"></param>
        /// <returns></returns>
        protected virtual string TileTextureFileName(int iX, int iY) => $"{this.TilePrefix}X{iX:D3}_Y{iY:D3}{this.TilePostfix}";

        #endregion

        /// <summary>
        /// C# has reverse formatting notation compared to the python scripts which generate VikingXML files.  If the format starts with a number instead of a letter this 
        /// function will correct the issue by swapping them
        /// </summary>
        /// <param name="gridFormat"></param>
        /// <returns></returns>
        public static string GridTileFormatStringFromPythonString(string gridFormat)
        {
            if (!char.IsLetter(gridFormat[0]))
            {
                if (char.IsLetter(gridFormat[gridFormat.Length - 1]))
                {
                    return gridFormat[gridFormat.Length - 1] + gridFormat.Substring(0, gridFormat.Length - 1);
                }
            }

            return gridFormat;

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

            foreach (GridInfo info in ToCopy.LevelToGridInfo.Values)
            {
                GridInfo infoCopy = new(info.GridXDim, info.GridYDim, info.Downsample, info.Path);
                LevelToGridInfo.Add(infoCopy.Downsample, infoCopy);
            }
        }

        public TileGridMappingBase(Section section, string name, string Prefix, string Postfix, int TileSizeX, int TileSizeY, string TileGridPath, string GridCoordFormat, UnitsAndScale.IAxisUnits XYScale) :
            base(section, name, Prefix, Postfix)
        {
            this.TileSizeX = TileSizeX;
            this.TileSizeY = TileSizeY;
            this.TotalTileSize = TileSizeX * TileSizeY;
            this.TileGridPath = TileGridPath;
            this._XYScale = XYScale;
            if (GridCoordFormat != null)
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

            GridInfo Level = new(GridDimX, GridDimY, Downsample, LevelPath);
            if (LevelToGridInfo.ContainsKey(Downsample))
            {
                System.Diagnostics.Trace.WriteLine($"Duplicate Tileset Level {Section.Number}-{LevelPath}");
            }
            else
            {
                LevelToGridInfo.Add(Downsample, Level);
            }

            this._AvailableLevels = null;
        }

        protected virtual PositionNormalTextureVertex[] CalculateVerticies(int iX, int iY, int roundedDownsample)
        {
            PositionNormalTextureVertex[] verticies =
            [
                new PositionNormalTextureVertex(new Vector3(iX * this.TileSizeX * roundedDownsample, iY * this.TileSizeY * roundedDownsample, 0),
                                                                   Vector3.UnitZ,
                                                               new Vector2(0, 0)),
                new PositionNormalTextureVertex(new Vector3((iX + 1) * this.TileSizeX * roundedDownsample, iY * this.TileSizeY * roundedDownsample, 0),
                                                                   Vector3.UnitZ,
                                                               new Vector2(1, 0)),
                new PositionNormalTextureVertex(new Vector3(iX * this.TileSizeX * roundedDownsample, (iY + 1) * this.TileSizeY * roundedDownsample, 0),
                                                                   Vector3.UnitZ,
                                                               new Vector2(0, 1)),
                new PositionNormalTextureVertex(new Vector3((iX + 1) * this.TileSizeX * roundedDownsample, (iY + 1) * this.TileSizeY * roundedDownsample, 0),
                                                                   Vector3.UnitZ,
                                                               new Vector2(1, 1)),
            ];
            return verticies;
        }

        protected static readonly int[] TriangleEdges = [0, 1, 2, 1, 3, 2];

        public override Task<TilePyramid> VisibleTilesAsync(Rectangle VisibleBounds, double DownSample) => Task.Run(() => VisibleTiles(VisibleBounds, DownSample));

        public override TilePyramid VisibleTiles(Rectangle VisibleBounds, double DownSample)
        {
            TilePyramid VisibleTiles = new(VisibleBounds);

            //double scaledDownsampleLevel = AdjustDownsampleForScale(DownSample);

            int roundedDownsample = NearestAvailableLevel(DownSample);
            if (roundedDownsample == int.MaxValue)
                return VisibleTiles;

            //Starting with low-res tiles, add tiles to the list until we reach desired resolution
            //            List<Tile> TilesToDraw = new List<Tile>(); 

            //Find the starting level of our rendering
            int iLevel = AvailableLevels.Length - 1;
            int level = AvailableLevels[iLevel];

            do
            {
                List<TileViewModel> newTiles = RecursiveVisibleTiles(
                    VisibleBounds,
                    level
                    //PORT: AsynchTextureLoad
                    );

                //Insert at the beginning so we overwrite earlier tiles with poorer resolution
                VisibleTiles.AddTiles(level, newTiles);
                //TilesToDraw.AddRange(newTiles);

                iLevel--;
                if (iLevel >= 0)
                    level = AvailableLevels[iLevel];
            }
            while (level >= roundedDownsample && iLevel >= 0);

            //Trace.WriteLine("Drawing " + TilesToDraw.Count.ToString() + " Tiles", "VolumeModel");

            return VisibleTiles;
        }


        private List<TileViewModel> RecursiveVisibleTiles(Rectangle VisibleBounds, int roundedDownsample)
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
            iMaxX = iMaxX >= gridInfo.GridXDim - 1 ? gridInfo.GridXDim - 1 : iMaxX;
            iMaxY = iMaxY >= gridInfo.GridYDim - 1 ? gridInfo.GridYDim - 1 : iMaxY;

            if (iMaxX < 0)
                iMaxX = 0;
            if (iMaxY < 0)
                iMaxY = 0;
            if (iMinX > iMaxX)
                iMinX = iMaxX;
            if (iMinY > iMaxY)
                iMinY = iMaxY;

            int ExpectedTileCount = (iMaxX - iMinX) * (iMaxY - iMinY);
            List<TileViewModel> TilesToDraw = new(ExpectedTileCount);
            //List<Task<TileViewModel>> tileTasks = new List<Task<TileViewModel>>(ExpectedTileCount);

            for (int iX = iMinX; iX <= iMaxX; iX++)
            {
                for (int iY = iMinY; iY <= iMaxY; iY++)
                {
                    TileKey tilekey = new(iX, iY, roundedDownsample);
                    if (TileTasks.ContainsKey(tilekey))
                        continue; //We are already getting this tile, so continue

                    var UniqueID = TileUniqueKey.Create(Section.Number, Name, Name, roundedDownsample, this.TileTextureFileName(iX, iY));
                    string TextureFileName = TileFullPath(iX, iY, roundedDownsample);

                    if (Global.TileCache.TryGetValue(UniqueID, out TileViewModel tileViewModel) && tileViewModel != null)
                    {
                        TilesToDraw.Add(tileViewModel);
                    }
                    else
                    {

                        //Func<string, int, int, int, string, string,Tile> a = CreateTile;
                        int ixc = iX;
                        int iyc = iY;
                        int rd = roundedDownsample;
                        var tileTask = Task.Run<CreateTileTaskResult>(() => CreateTile(UniqueID, tilekey, TextureFileName, Name));
                        tileTask.ContinueWith(previousTask => OnTileCreated(previousTask.Result));
                        TileTasks.TryAdd(tilekey, tileTask);

                        //tileTasks.Add(T);
                        //TilesToDraw.Add(CreateTile(UniqueID, ixc, iyc, rd, TextureFileName, Name));
                    }
                }
            }

            /*
            Task[] tileTaskArray = tileTasks.Cast<Task>().ToArray();
            Task.WaitAll(tileTaskArray);
            TilesToDraw.AddRange(tileTasks.Select(t => t.Result));
            */
            return TilesToDraw;
        }

        private async Task<CreateTileTaskResult> CreateTile(TileUniqueKey uniqueID, TileKey tileKey, string textureFilename, string name, int? MipMapLevels = null)
        {
            //TODO: Make this a task
            int iX = tileKey.X;
            int iY = tileKey.Y;
            int roundedDownsample = tileKey.Downsample;

            //First create a new tile
            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
            PositionNormalTextureVertex[] verticies = CalculateVerticies(iX, iY, roundedDownsample);

            if (MipMapLevels.HasValue == false)
                MipMapLevels = roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1] ? 0 : 1; //0 = Generate mipmaps for lowest res texture, 1 == no MipMaps for higher res textures in the pyramid

            var tile = Global.TileCache.ConstructTile(uniqueID,
                verticies,
                TriangleEdges,
                textureFilename,
                TileTextureCacheFileName(roundedDownsample, iX, iY),
                //PORT TextureCacheFileName,
                name,
                roundedDownsample,
                MipMapLevels.Value);

            //Check for tiles at higher resolution
            //                        int iTempX = iX / 2;
            //                        int iTempY = iY / 2;
            //                        int iTempDownsample = roundedDownsample * 2;
            return new CreateTileTaskResult(tile, tileKey);
        }

        protected void OnTileCreated(CreateTileTaskResult tileview)
        {
            CreateTileTaskResult result = tileview;
            TileTasks.TryRemove(result.Key, out var value);
        }
    }
}
