using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// A set of cells in a grid
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct GridIndex(int X, int Y) : IComparable<GridIndex>
    {
        public readonly int X = X;
        public readonly int Y = Y;

        public override string ToString() => $"X:{X} Y:{Y}";

        public override int GetHashCode() => (int)(((long)X * (long)Y) % int.MaxValue);

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (obj is GridIndex other)
            {
                return other.X == this.X && other.Y == this.Y;
            }

            return false;
        }

        public int CompareTo(GridIndex other)
        {
            if (other.X != this.X)
                return other.X - this.X;
            else
                return other.Y - this.Y;
        }

        public static bool operator ==(GridIndex A, GridIndex B) => A.Equals(B);

        public static bool operator !=(GridIndex A, GridIndex B) => !A.Equals(B);
    }

    /// <summary>
    /// A set of cells in a grid
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GridRange<T>(T[,] cells, GridIndices iGrid) where T : class
    {
        public T[,] Cells = cells;
        public GridIndices Indices = iGrid;
    }

    /// <summary>
    /// Dimensions of a grid cell
    /// </summary>
    public readonly struct GridCellDimensions(double Width, double Height)
    {
        public readonly double Width = Width;
        public readonly double Height = Height;

        public override string ToString() => $"W: {Width} H: {Height}";
    }

    /// <summary>
    /// Dimensions of a grid. 
    /// </summary>
    public readonly struct GridDimensions(int Width, int Height)
    {
        public readonly int Width = Width;
        public readonly int Height = Height;

        public override string ToString() => $"W: {Width} H: {Height}";
    }

    public readonly struct GridIndices(int minX, int minY, int maxX, int maxY) : IEnumerable<GridIndex>
    {
        public readonly int iMinY = minY;
        public readonly int iMaxY = maxY;
        public readonly int iMinX = minX;
        public readonly int iMaxX = maxX;

        public int Width => (iMaxX - iMinX);

        public int Height => (iMaxY - iMinY);

        public static GridIndices FromGridDimensions(GridDimensions gridDim)
        {
            return new GridIndices(
                //Figure out which grid locations are visible
                minX: 0,
                minY: 0,
                maxX: gridDim.Width,
                maxY: gridDim.Height);
        }

        public static GridIndices FromRectangle(in Rectangle bounds, GridCellDimensions cellDim) => FromRectangle(in bounds, cellDim.Width, cellDim.Height);

        public static GridIndices FromRectangle(in Rectangle bounds, double CellWidth, double CellHeight)
        {
            return new GridIndices(
                //Figure out which grid locations are visible
                minX: (int)Math.Floor(bounds.Left / CellWidth),
                minY: (int)Math.Floor(bounds.Bottom / CellHeight),
                maxX: (int)Math.Ceiling(bounds.Right / CellWidth),
                maxY: (int)Math.Ceiling(bounds.Top / CellHeight));
        }

        public GridIndices CropToBounds(GridDimensions gridDim) => CropToBounds(0, 0, gridDim.Width, gridDim.Height);

        public GridIndices CropToBounds(int MinX, int MinY, int MaxX, int MaxY)
        {
            return new GridIndices(
                minX: iMinX < MinX ? MinX : iMinX,
                minY: iMinY < MinY ? MinY : iMinY,
                maxX: iMaxX > MaxX ? MaxX : iMaxX,
                maxY: iMaxY > MaxY ? MaxY : iMaxY);
        }

        IEnumerator<GridIndex> IEnumerable<GridIndex>.GetEnumerator() => new GridIndexEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new GridIndexEnumerator(this);

        /// <summary>
        /// Return the number of grid cells covered by the indicies
        /// </summary>
        /// <returns></returns>
        public int NumberOfCells => Width * Height;
    }

    public sealed class GridIndexEnumerator : IEnumerator<GridIndex>
    {
        readonly GridIndices Indices;
        int iX;
        int iY;

        public GridIndexEnumerator(GridIndices indicies)
        {
            this.Indices = indicies;
            this.Reset();
        }

        object IEnumerator.Current
        {
            get
            {
                if (iX < Indices.iMinX)
                    throw new InvalidOperationException("MoveNext() has not been called");

                return new GridIndex(iX, iY);
            }
        }

        GridIndex IEnumerator<GridIndex>.Current => new(iX, iY);

        public void Dispose()
        {
        }

        bool IEnumerator.MoveNext()
        {
            iX++;
            if (iX >= Indices.iMaxX)
            {
                iX = Indices.iMinX;
                iY++;
                if (iY >= Indices.iMaxY)
                {
                    return false;
                }
            }

            return true;
        }

        void Reset()
        {
            iX = Indices.iMinX - 1; //Subtract one because MoveNext must be called before we request the current value
            iY = Indices.iMinY;
        }

        void IEnumerator.Reset() => this.Reset();
    }

    public interface IRegionPyramidLevel<T> where T : class
    {
        T GetOrAddCell(GridIndex key, Func<GridIndex, T> valueFactory);

        T AddOrUpdateCell(GridIndex key, T addValue, Func<GridIndex, T, T> updateFunction);

        T AddOrUpdateCell(GridIndex key, Func<GridIndex, T> addFunction, Func<GridIndex, T, T> updateFunction);

        bool TryUpdateCell(GridIndex i, T value, T comparisonValue);

        T[] ArrayForRegion(in Rectangle volumeBounds);

        GridRange<T> SubGridForRegion(in Rectangle? volumeBounds);

        Rectangle CellBounds(int iX, int iY);

        double MinRadius
        {
            get;
        }

        int Level
        {
            get;
        }
    }

    public class RegionPyramidLevel<T> : IRegionPyramidLevel<T> where T : class
    {
        protected T[,] Cells;

        public readonly GridCellDimensions CellDimensions;
        public readonly GridDimensions GridDimensions;

        private readonly int _Level;

        public int Level => _Level;

        private readonly double _MinRadius;

        public double MinRadius => _MinRadius;

        public RegionPyramidLevel(int Level, GridDimensions gridDim, GridCellDimensions cellDim, double minRadius)
        {
            this._Level = Level;
            this.CellDimensions = cellDim;
            this.GridDimensions = gridDim;
            this.Cells = new T[GridDimensions.Width, GridDimensions.Height];
            this._MinRadius = minRadius;
        }

        public T[] ArrayForRegion(in Rectangle volumeBounds)
        {
            GridIndices iGrid = GridIndices.FromRectangle(volumeBounds, this.CellDimensions).CropToBounds(GridDimensions);
            return RegionPyramidLevel<T>.ToArray(Cells, iGrid);
        }

        public GridRange<T> SubGridForRegion(in Rectangle? volumeBounds)
        {
            if (volumeBounds.HasValue)
            {
                GridIndices iGrid = GridIndices.FromRectangle(volumeBounds.Value, this.CellDimensions).CropToBounds(GridDimensions);
                return RegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);
            }
            else
            {
                GridIndices iGrid = GridIndices.FromGridDimensions(this.GridDimensions);
                return RegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);

            }

        }

        protected static T[] ToArray(T[,] grid, GridIndices iGrid)
        {
            T[] output = new T[iGrid.NumberOfCells];
            int i = 0;
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[i++] = grid[iX, iY];
                }
            }

            return output;
        }

        protected static GridRange<T> ToSubGrid(T[,] grid, GridIndices iGrid)
        {
            T[,] output = new T[iGrid.Width, iGrid.Height];
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[iX - iGrid.iMinX, iY - iGrid.iMinY] = grid[iX, iY];
                }
            }

            return new GridRange<T>(output, iGrid);
        }

        public Rectangle CellBounds(int iX, int iY)
        {
            return new Rectangle(new Vector2(iX * CellDimensions.Width, iY * CellDimensions.Height),
                                      CellDimensions.Width, CellDimensions.Height);
        }

        public T GetOrAddCell(GridIndex key, Func<GridIndex, T> valueFactory)
        {
            lock (this.Cells)
            {
                if (this.Cells[key.X, key.Y] is null)
                    this.Cells[key.X, key.Y] = valueFactory(key);

                return this.Cells[key.X, key.Y];
            }
        }

        public T AddOrUpdateCell(GridIndex key, T addValue, Func<GridIndex, T, T> updateFunction)
        {
            lock (this.Cells)
            {
                this.Cells[key.X, key.Y] = this.Cells[key.X, key.Y] is null ? addValue : updateFunction(key, this.Cells[key.X, key.Y]);

                return this.Cells[key.X, key.Y];
            }
        }

        public T AddOrUpdateCell(GridIndex key, Func<GridIndex, T> addFunction, Func<GridIndex, T, T> updateFunction)
        {
            lock (this.Cells)
            {
                this.Cells[key.X, key.Y] = this.Cells[key.X, key.Y] is null ? addFunction(key) : updateFunction(key, this.Cells[key.X, key.Y]);

                return this.Cells[key.X, key.Y];
            }
        }

        public bool TryUpdateCell(GridIndex key, T value, T comparisonValue)
        {
            lock (this.Cells)
            {
                if (this.Cells[key.X, key.Y] == comparisonValue)
                {
                    this.Cells[key.X, key.Y] = value;
                    return true;
                }
                return false;
            }
        }

        public override string ToString() => $"Level: {this.Level} MinRadius: {this.MinRadius} CellDim: {this.CellDimensions} GridDim: {this.GridDimensions}";
    }

    /// <summary>
    /// Divides a volume up into a grid of evenly sized cells. Query a region to determine which cells are visible
    /// 
    /// Level 0 covers the entire area of the RegionPyramid
    /// Level 1 covers 1/4 of the area of the RegionPyramid
    /// Level N covers 1 / 2^N of the area of the Region Pyramid
    /// 
    /// Volume Size Screen Size Visible Bounds  Single Pixel Annotation Size    Level   Level Grid Cell Size
    /// 1024        64          1024            16                              0       1024
    ///                         512             8                               1       512
    ///                         256             4                               2       256
    ///                         128             2                               3       128
    /// The Pyramid can scale to arbitrarily high resolutions within the provided boundaries.
    /// </summary>
    public class RegionPyramid<T>(Rectangle Boundaries, GridCellDimensions cellDimensions) where T : class
    {
        /// <summary>
        /// Width & Height of a grid cell in the RegionPyramid
        /// </summary>
        public GridCellDimensions CellDimensions = cellDimensions;
        readonly ConcurrentDictionary<int, RegionPyramidLevel<T>> Levels = new();

        public Rectangle RegionBounds = Boundaries;

        public int LevelForVisibleBounds(in Rectangle visibleBounds)
        {
            int level = visibleBounds.Width > visibleBounds.Height ?
                (int)Math.Floor(Math.Log(RegionBounds.Width / visibleBounds.Width, 2)) :
                (int)Math.Floor(Math.Log(RegionBounds.Height / visibleBounds.Height, 2));
            return level < 0 ? 0 : level;
        }


        protected RegionPyramidLevel<T> GetOrAddLevel(int Level, double minRadius)
        {
            return this.Levels.GetOrAdd(Level, new RegionPyramidLevel<T>(Level,
                                                                         GridDimensionsForLevel(Level),
                                                                         CellDimensionsForLevel(Level),
                                                                         minRadius));
        }

        private double MinRadiusForLevel(in Rectangle screenBounds, int Level)
        {
            double minRadius = screenBounds.Width > screenBounds.Height ? RegionBounds.Width / screenBounds.Width : RegionBounds.Height / screenBounds.Height;

            minRadius /= Math.Pow(2, Level);
            return minRadius;
        }

        private GridDimensions GridDimensionsForLevel(int Level)
        {
            GridCellDimensions cellDimensions = CellDimensionsForLevel(Level);
            return new GridDimensions((int)Math.Ceiling(RegionBounds.Width / cellDimensions.Width),
                                      (int)Math.Ceiling(RegionBounds.Height / cellDimensions.Height));
        }

        private GridCellDimensions CellDimensionsForLevel(int Level)
        {
            return new GridCellDimensions(this.CellDimensions.Width / Math.Pow(2, Level),
                                          this.CellDimensions.Height / Math.Pow(2, Level));
        }

        public IRegionPyramidLevel<T> GetLevelForScreenBounds(in Rectangle screenBounds, double SinglePixelRadius)
        {
            Rectangle volumeBounds = new(screenBounds.Left, screenBounds.Right, screenBounds.Bottom, screenBounds.Top);
            volumeBounds *= (SinglePixelRadius);
            return GetLevelForBounds(in screenBounds, in volumeBounds, SinglePixelRadius);
        }

        public IRegionPyramidLevel<T> GetLevelForVolumeBounds(in Rectangle volumeBounds, double SinglePixelRadius)
        {
            Rectangle screenBounds = new(volumeBounds.Left, volumeBounds.Right, volumeBounds.Bottom, volumeBounds.Top);
            screenBounds *= 1.0 / SinglePixelRadius;
            return GetLevelForBounds(in screenBounds, in volumeBounds, SinglePixelRadius);
        }

        private RegionPyramidLevel<T> GetLevelForBounds(in Rectangle screenBounds, in Rectangle volumeBounds, double SinglePixelRadius)
        {
            int iLevel = LevelForVisibleBounds(volumeBounds);
            return GetOrAddLevel(iLevel, MinRadiusForLevel(screenBounds, iLevel));
        }
    }
}