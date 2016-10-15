using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    /// <summary>
    /// A set of cells in a grid
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct GridIndex : IComparable<GridIndex>
    {
        public readonly int X;
        public readonly int Y;

        public GridIndex(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public override string ToString()
        {
            return string.Format("X:{0} Y:{1}", X, Y);
        }

        public override int GetHashCode()
        {
            return (int)(((long)X * (long)Y) % int.MaxValue);
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
                return true;

            if ((object)obj == null)
                return false;

            if (!typeof(GridIndex).IsInstanceOfType(obj))
                return false;

            GridIndex other = (GridIndex)obj;
            return other.X == this.X && other.Y == this.Y;
        }

        public int CompareTo(GridIndex other)
        {
            if (other.X != this.X)
                return other.X - this.X;
            else
                return other.Y - this.Y;
        }

        public static bool operator ==(GridIndex A, GridIndex B)
        {
            if (object.ReferenceEquals(A, B))
                return true;

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(GridIndex A, GridIndex B)
        {
            if (object.ReferenceEquals(A, B))
                return false;

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }
    }

    /// <summary>
    /// A set of cells in a grid
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GridRange<T> where T : class
    {
        public T[,] Cells;
        public GridIndicies Indicies;

        public GridRange(T[,] cells, GridIndicies iGrid)
        {
            this.Cells = cells;
            this.Indicies = iGrid;
        }
    }

    /// <summary>
    /// Dimensions of a grid cell
    /// </summary>
    public struct GridCellDimensions
    {
        public double Width;
        public double Height;

        public GridCellDimensions(double Width, double Height)
        {
            this.Width = Width;
            this.Height = Height;
        }
    }

    /// <summary>
    /// Dimensions of a grid. 
    /// </summary>
    public struct GridDimensions
    {
        public int Width;
        public int Height;

        public GridDimensions(int Width, int Height)
        {
            this.Width = Width;
            this.Height = Height;
        }
    }

    public struct GridIndicies : IEnumerable<GridIndex>
    {
        public int iMinY;
        public int iMaxY;
        public int iMinX;
        public int iMaxX;

        public int Width
        {
            get { return (iMaxX - iMinX); }
        }

        public int Height
        {
            get { return (iMaxY - iMinY); }
        }

        public static GridIndicies FromGridDimensions(GridDimensions gridDim)
        {
            GridIndicies obj = new GridIndicies();
            //Figure out which grid locations are visible
            obj.iMinX = 0;
            obj.iMinY = 0;
            obj.iMaxX = gridDim.Width;
            obj.iMaxY = gridDim.Height;
            return obj;
        }

        public static GridIndicies FromRectangle(GridRectangle bounds, GridCellDimensions cellDim)
        {
            return FromRectangle(bounds, cellDim.Width, cellDim.Height);
        }

        public static GridIndicies FromRectangle(GridRectangle bounds, double CellWidth, double CellHeight)
        {
            GridIndicies obj = new GridIndicies();
            //Figure out which grid locations are visible
            obj.iMinX = (int)Math.Floor(bounds.Left / CellWidth);
            obj.iMinY = (int)Math.Floor(bounds.Bottom / CellHeight);
            obj.iMaxX = (int)Math.Ceiling(bounds.Right / CellWidth);
            obj.iMaxY = (int)Math.Ceiling(bounds.Top / CellHeight);
            return obj;
        }

        public void CropToBounds(GridDimensions gridDim)
        {
            CropToBounds(0, 0, gridDim.Width, gridDim.Height);
        }

        public void CropToBounds(int MinX, int MinY, int MaxX, int MaxY)
        {
            iMinX = iMinX < MinX ? MinX : iMinX;
            iMinY = iMinY < MinY ? MinY : iMinY;
            iMaxX = iMaxX > MaxX ? MaxX : iMaxX;
            iMaxY = iMaxY > MaxY ? MaxY : iMaxY;
        }

        IEnumerator<GridIndex> IEnumerable<GridIndex>.GetEnumerator()
        {
            return new GridIndexEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new GridIndexEnumerator(this);
        }

        /// <summary>
        /// Return the number of grid cells covered by the indicies
        /// </summary>
        /// <returns></returns>
        public int NumberOfCells
        {
            get { return Width * Height; }
        }
    }

    public class GridIndexEnumerator : IEnumerator<GridIndex>
    {
        GridIndicies Indicies;
        int iX;
        int iY;

        public GridIndexEnumerator(GridIndicies indicies)
        {
            this.Indicies = indicies;
            this.Reset();
        }

        object IEnumerator.Current
        {
            get
            {
                if (iX < Indicies.iMinX)
                    throw new InvalidOperationException("MoveNext() has not been called");

                return new GridIndex(iX, iY);
            }
        }

        GridIndex IEnumerator<GridIndex>.Current
        {
            get
            {
                return new GridIndex(iX, iY);
            }
        }

        void IDisposable.Dispose()
        {
        }

        bool IEnumerator.MoveNext()
        {
            iX++;
            if (iX >= Indicies.iMaxX)
            {
                iX = Indicies.iMinX;
                iY++;
                if(iY >= Indicies.iMaxY)
                {
                    return false;
                }
            }

            return true;
        }

        void Reset()
        {
            iX = Indicies.iMinX - 1; //Subtract one because MoveNext must be called before we request the current value
            iY = Indicies.iMinY;
        }

        void IEnumerator.Reset()
        {
            this.Reset();   
        }
    }

    public interface IRegionPyramidLevel<T> where T : class
    {
        T GetOrAddCell(GridIndex key, Func<GridIndex, T> valueFactory);

        T AddOrUpdateCell(GridIndex key, T addValue, Func<GridIndex, T, T> updateFunction);

        T AddOrUpdateCell(GridIndex key, Func<GridIndex, T> addFunction, Func<GridIndex, T, T> updateFunction);

        bool TryUpdateCell(GridIndex i, T value, T comparisonValue);

        T[] ArrayForRegion(GridRectangle volumeBounds);

        GridRange<T> SubGridForRegion(GridRectangle? volumeBounds);

        GridRectangle CellBounds(int iX, int iY);

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

        public int Level
        {
            get
            {
                return _Level;
            }
        }

        private readonly double _MinRadius;

        public double MinRadius
        {
            get { return _MinRadius; }
        }
           
        public RegionPyramidLevel(int Level, GridDimensions gridDim, GridCellDimensions cellDim, double minRadius)
        {
            this._Level = Level;
            this.CellDimensions = cellDim;
            this.GridDimensions = gridDim;
            this.Cells = new T[GridDimensions.Width, GridDimensions.Height];
            this._MinRadius = minRadius;
        } 

        public T[] ArrayForRegion(GridRectangle volumeBounds)
        {
            GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds, this.CellDimensions);
            iGrid.CropToBounds(this.GridDimensions);

            return RegionPyramidLevel<T>.ToArray(Cells, iGrid);
        }

        public GridRange<T> SubGridForRegion(GridRectangle? volumeBounds)
        {
            if(volumeBounds.HasValue)
            {
                GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds.Value, this.CellDimensions);
                iGrid.CropToBounds(this.GridDimensions);

                return RegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);
            }
            else
            {
                GridIndicies iGrid = GridIndicies.FromGridDimensions(this.GridDimensions);
                return RegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);

            }
            
        }

        protected static T[] ToArray(T[,] grid, GridIndicies iGrid)
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

        protected static GridRange<T> ToSubGrid(T[,] grid, GridIndicies iGrid)
        {
            T[,] output = new T[iGrid.Width, iGrid.Height];
            int i = 0;
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[iX - iGrid.iMinX, iY - iGrid.iMinY] = grid[iX, iY];
                }
            }

            return new GridRange<T>(output, iGrid);
        }

        public GridRectangle CellBounds(int iX, int iY)
        {
            return new GridRectangle(new GridVector2(iX * CellDimensions.Width, iY * CellDimensions.Height),
                                      CellDimensions.Width, CellDimensions.Height);
        }

        public T GetOrAddCell(GridIndex key, Func<GridIndex, T> valueFactory)
        {
            lock(this.Cells)
            {
                if (this.Cells[key.X, key.Y] == null)
                    this.Cells[key.X, key.Y] = valueFactory(key);

                return this.Cells[key.X, key.Y];
            }
        }

        public T AddOrUpdateCell(GridIndex key, T addValue, Func<GridIndex, T, T> updateFunction)
        {
            lock(this.Cells)
            {
                if (this.Cells[key.X, key.Y] == null)
                    this.Cells[key.X, key.Y] = addValue;
                else
                    this.Cells[key.X, key.Y] = updateFunction(key, this.Cells[key.X, key.Y]);

                return this.Cells[key.X, key.Y];
            }
        }

        public T AddOrUpdateCell(GridIndex key, Func<GridIndex, T> addFunction, Func<GridIndex, T, T> updateFunction)
        {
            lock (this.Cells)
            {
                if (this.Cells[key.X, key.Y] == null)
                    this.Cells[key.X, key.Y] = addFunction(key);
                else
                    this.Cells[key.X, key.Y] = updateFunction(key, this.Cells[key.X, key.Y]);

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
    public class RegionPyramid<T> where T : class
    {
        /// <summary>
        /// Width & Height of a grid cell in the RegionPyramid
        /// </summary>
        public GridCellDimensions CellDimensions;

        ConcurrentDictionary<int, RegionPyramidLevel<T>> Levels = new ConcurrentDictionary<int, RegionPyramidLevel<T>>();

        public GridRectangle RegionBounds;

        public RegionPyramid(GridRectangle Boundaries, GridCellDimensions cellDimensions)
        {
            //Figure out the dimensions of our grid
            this.RegionBounds = Boundaries;

            //Level 0 cell dimensions match the boundary dimensions
            CellDimensions = cellDimensions;//new GridCellDimensions(Boundaries.Width, Boundaries.Height); 
        }

        public int LevelForVisibleBounds(GridRectangle visibleBounds)
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

        private double MinRadiusForLevel(GridRectangle screenBounds, int Level)
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

        public IRegionPyramidLevel<T> GetLevelForScreenBounds(GridRectangle screenBounds, double SinglePixelRadius)
        {
            GridRectangle volumeBounds = new GridRectangle(screenBounds.Left, screenBounds.Right, screenBounds.Bottom, screenBounds.Top);
            volumeBounds.Scale(SinglePixelRadius);
            return GetLevelForBounds(screenBounds, volumeBounds, SinglePixelRadius);
        }

        public IRegionPyramidLevel<T> GetLevelForVolumeBounds(GridRectangle volumeBounds, double SinglePixelRadius)
        {
            GridRectangle screenBounds = new GridRectangle(volumeBounds.Left, volumeBounds.Right, volumeBounds.Bottom, volumeBounds.Top);
            screenBounds.Scale(1.0 / SinglePixelRadius);
            return GetLevelForBounds(screenBounds, volumeBounds, SinglePixelRadius);
        }

        private RegionPyramidLevel<T> GetLevelForBounds(GridRectangle screenBounds, GridRectangle volumeBounds, double SinglePixelRadius)
        {
            int iLevel = LevelForVisibleBounds(volumeBounds);
            return GetOrAddLevel(iLevel, MinRadiusForLevel(screenBounds, iLevel));
        }
    }
}