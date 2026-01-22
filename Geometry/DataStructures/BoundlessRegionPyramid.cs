using System;
using System.Collections.Concurrent;


namespace Geometry
{
    public class BoundlessRegionPyramidLevel<T>(int Level, GridCellDimensions UnscaledCellDim, double pixelDimensionsOfLevel) : IRegionPyramidLevel<T> where T : class
    {
        public ConcurrentDictionary<GridIndex, T> Cells = new();

        public readonly GridCellDimensions UnscaledCellDimensions = UnscaledCellDim;
        public readonly GridCellDimensions ScaledCellDimensions = new(pixelDimensionsOfLevel * UnscaledCellDim.Width,
                                                               pixelDimensionsOfLevel * UnscaledCellDim.Height);

        public delegate double DimensionsForLevelDelegate(int level);

        public int Level { get; } = Level;

        private readonly double _MinRadius = pixelDimensionsOfLevel;

        public double MinRadius => _MinRadius;

        private static T GetCell(ConcurrentDictionary<GridIndex, T> Cells, GridIndex key)
        {
            if (Cells.TryGetValue(key, out T Result))
                return Result;
            return null;
        }

        public T GetOrAddCell(GridIndex key, Func<GridIndex, T> valueFactory) => Cells.GetOrAdd(key, valueFactory);

        public T AddOrUpdateCell(GridIndex key, T addValue, Func<GridIndex, T, T> updateFunction) => Cells.AddOrUpdate(key, addValue, updateFunction);

        public T AddOrUpdateCell(GridIndex key, Func<GridIndex, T> addFunction, Func<GridIndex, T, T> updateFunction) => Cells.AddOrUpdate(key, addFunction, updateFunction);

        public bool TryUpdateCell(GridIndex key, T value, T comparisonValue) => Cells.TryUpdate(key, value, comparisonValue);

        private static double TwoToTheLevel(int Level) => Math.Pow(2.0, (double)Level);

        public T[] ArrayForRegion(in GridRectangle volumeBounds)
        {
            GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds, ScaledCellDimensions);
            return BoundlessRegionPyramidLevel<T>.ToArray(Cells, iGrid);
        }

        public GridRange<T> SubGridForRegion(in GridRectangle? volumeBounds)
        {
            if (!volumeBounds.HasValue)
            {
                throw new ArgumentException("Volume Bounds does not have a value.  Continuuous transforms should  ");
            }

            GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds.Value, this.ScaledCellDimensions);
            return BoundlessRegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);
        }

        public static T[] ToArray(ConcurrentDictionary<GridIndex, T> grid, GridIndicies iGrid)
        {
            T[] output = new T[iGrid.NumberOfCells];
            int i = 0;
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[i++] = GetCell(grid, new GridIndex(iX, iY));
                }
            }

            return output;
        }

        public static GridRange<T> ToSubGrid(ConcurrentDictionary<GridIndex, T> grid, GridIndicies iGrid)
        {
            T[,] output = new T[iGrid.Width, iGrid.Height];
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[iX - iGrid.iMinX, iY - iGrid.iMinY] = GetCell(grid, new GridIndex(iX, iY));
                }
            }

            return new GridRange<T>(output, iGrid);
        }

        public GridRectangle CellBounds(int iX, int iY)
        {
            return new GridRectangle(new GridVector2(iX * ScaledCellDimensions.Width, iY * ScaledCellDimensions.Height),
                                      ScaledCellDimensions.Width, ScaledCellDimensions.Height);
        }

        public override string ToString() => $"Level: {this.Level} MinRadius: {this.MinRadius} {this.ScaledCellDimensions}";
    }

    public interface IRegionPyramid<T> where T : class
    {
        IRegionPyramidLevel<T> GetLevel(double SinglePixelRadius);

        double LevelToPixelDimension(int Level);
    }

    /// <summary>
    /// Divides a volume up into a grid of evenly sized cells.  Query a region to determine which cells are visible
    /// 
    /// Level 0 -> Region Area = Cell Area * 2^0
    /// Level 1 -> Region Area = Cell Area * 2^1
    /// Level N -> Region Area = Cell Area * 2^N
    /// 
    /// Volume Size Screen Size Visible Bounds  Single Pixel Annotation Size    Level   Grid Cell Size
    /// ?           64          512             1                               0       1024
    ///             64          1024            1                               0       1024
    ///             64          2048            2                               1       1024
    ///             64          4096            4                               2       1024
    /// The Pyramid cannot scale to arbitrarily high resolution beyond the cell size, however the 
    /// boundaries are not required to be known
    /// 
    /// </summary>
    public class BoundlessRegionPyramid<T>(GridCellDimensions cellDimensions, double powerScale) : IRegionPyramid<T> where T : class
    {
        /// <summary>
        /// Width & Height of a grid cell in the RegionPyramid
        /// </summary>
        public readonly GridCellDimensions CellDimensions = cellDimensions;

        /// <summary>
        /// The base of the exponential we use to determine the level in a region pyramid
        /// </summary>
        protected readonly double PowerScale = powerScale;

        readonly ConcurrentDictionary<int, BoundlessRegionPyramidLevel<T>> Levels = new();

        protected BoundlessRegionPyramidLevel<T> GetOrAddLevel(int Level) => this.Levels.GetOrAdd(Level, new BoundlessRegionPyramidLevel<T>(Level, this.CellDimensions, LevelToPixelDimension(Level)));

        protected virtual int PixelDimensionToLevel(double SinglePixelRadius)
        {
            int Level = (int)Math.Floor(Math.Log(SinglePixelRadius, PowerScale));
            if (Level < 0)
                Level = 0;
            return Level;
        }

        public virtual double LevelToPixelDimension(int Level) => Math.Pow(PowerScale, Level);

        /// <summary>
        /// Size of an object that occupies a single pixel at the given level
        /// </summary>
        /// <param name="screenBounds"></param>
        /// <param name="Level"></param>
        /// <returns></returns>
        protected virtual double MinRadiusForLevel(in GridRectangle screenBounds, int Level) => Math.Pow(PowerScale, Level);

        public IRegionPyramidLevel<T> GetLevel(double SinglePixelRadius)
        {
            int Level = PixelDimensionToLevel(SinglePixelRadius);

            return GetOrAddLevel(Level);
        }
    }
}
