using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
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

    public struct GridIndicies
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

        /// <summary>
        /// Return the number of grid cells covered by the indicies
        /// </summary>
        /// <returns></returns>
        public int NumberOfCells
        {
            get { return Width * Height; }
        }

        
    }

    public class RegionPyramidLevel<T> where T : class
    {
        public T[,] Cells;

        public readonly GridCellDimensions CellDimensions;
        public readonly GridDimensions GridDimensions;

        public readonly int Level; 
        
        public readonly double MinRadius;
        double MaxRadius;

        public RegionPyramidLevel(int Level, GridDimensions gridDim, GridCellDimensions cellDim, double minRadius)
        {
            this.Level = Level;
            this.CellDimensions = cellDim;
            this.GridDimensions = gridDim;
            this.Cells = new T[GridDimensions.Width, GridDimensions.Height];
            this.MinRadius = minRadius;
        }

        public T[] ArrayForRegion(GridRectangle volumeBounds)
        {
            GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds, this.CellDimensions);
            iGrid.CropToBounds(this.GridDimensions);

            return RegionPyramidLevel<T>.ToArray(Cells, iGrid);
        }

        public GridRange<T> SubGridForRegion(GridRectangle volumeBounds)
        {
            GridIndicies iGrid = GridIndicies.FromRectangle(volumeBounds, this.CellDimensions);
            iGrid.CropToBounds(this.GridDimensions);

            return RegionPyramidLevel<T>.ToSubGrid(Cells, iGrid);
        }

        public static T[] ToArray(T[,] grid, GridIndicies iGrid)
        {
            T[] output = new T[iGrid.NumberOfCells];
            int i = 0;
            for(int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[i++] = grid[iX, iY];
                }
            }

            return output;
        }

        public static GridRange<T> ToSubGrid(T[,] grid, GridIndicies iGrid)
        {
            T[,] output = new T[iGrid.Width, iGrid.Height];
            int i = 0;
            for (int iY = iGrid.iMinY; iY < iGrid.iMaxY; iY++)
            {
                for (int iX = iGrid.iMinX; iX < iGrid.iMaxX; iX++)
                {
                    output[iX-iGrid.iMinX, iY-iGrid.iMinY] = grid[iX, iY];
                }
            }

            return new GridRange<T>(output, iGrid);
        }

        public GridRectangle CellBounds(int iX, int iY)
        {
            return new GridRectangle(new GridVector2(iX * CellDimensions.Width, iY * CellDimensions.Height),
                                      CellDimensions.Width, CellDimensions.Height);
        }
    }

    /// <summary>
    /// Divides a volume up into a grid of evenly sized cells.  Allows a client to determine which cells are visible
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
    /// 
    /// </summary>
    public class RegionPyramid<T> where T : class
    {
        /// <summary>
        /// Width & Height of a grid cell in the RegionPyramid
        /// </summary>
        public GridCellDimensions CellDimensions;
          
        ConcurrentDictionary<int, RegionPyramidLevel<T>> Levels = new ConcurrentDictionary<int, RegionPyramidLevel<T>>();

        public GridRectangle RegionBounds;

        private static double RoundToPowerOfTwo(double downsample)
        {
            return Math.Pow(Math.Floor(Math.Log(downsample, 2)), 2);
        }

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
                 
       public RegionPyramidLevel<T> GetLevelForScreenBounds(GridRectangle screenBounds, double SinglePixelRadius)
        {
            GridRectangle volumeBounds = new GridRectangle(screenBounds.Left, screenBounds.Right, screenBounds.Bottom, screenBounds.Top);
            volumeBounds.Scale(SinglePixelRadius);
            return GetLevelForBounds(screenBounds, volumeBounds, SinglePixelRadius);
        }

        public RegionPyramidLevel<T> GetLevelForVolumeBounds(GridRectangle volumeBounds, double SinglePixelRadius)
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
