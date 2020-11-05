using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Geometry
{
    public struct GridLineSegmentPair : IComparable
    {
        public GridLineSegment mapLine;
        public GridLineSegment ctrlLine;

        public override bool Equals(object obj)
        {
            GridLineSegmentPair linePair = (GridLineSegmentPair)obj;

            return this == linePair;
        }

        public override int GetHashCode()
        {
            return mapLine.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            GridLineSegmentPair linePair = (GridLineSegmentPair)obj;

            int result = mapLine.CompareTo(linePair.mapLine);
            if (result == 0)
            {
                result = ctrlLine.CompareTo(linePair.ctrlLine);
            }

            return result;
        }

        public static bool operator ==(GridLineSegmentPair A, GridLineSegmentPair B)
        {
            return (A.mapLine == B.mapLine) && (A.ctrlLine == B.ctrlLine);
        }

        public static bool operator !=(GridLineSegmentPair A, GridLineSegmentPair B)
        {
            return !((A.mapLine == B.mapLine) && (A.ctrlLine == B.ctrlLine));
        }
    }

    /// <summary>
    /// The line search grid divides space into a regular grid.  Each line segment defines a rectangle.  If the rectangle intersects
    /// a grid cell a pointer is added to that cell.  When a point is passed in the lines in the cell containing the point and all adjacent cells
    /// are checked for intersection.
    /// </summary>
    public class PairedLineSearchGrid
    {
        #region PairedLineSearchGridCoordListEnumerator

        /// <summary>
        /// Enumerates over a range of cells, only returning unique values.
        /// We use these because returning a list would copy a massive amount of memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class PairedLineSearchGridCoordListEnumerator : IEnumerable<GridLineSegmentPair>, IEnumerator<GridLineSegmentPair>
        {
            private PairedLineSearchGrid searchGrid;
            private List<GridLineSegmentPair> currentCell;
            private IEnumerable<Coord> coords;
            private IEnumerator<Coord> coordEnum;
            private int iGridIndex = -1;

            //Only return unique values
            //           SortedSet<GridLineSegmentPair> UniqueLines = new SortedSet<GridLineSegmentPair>();

            public PairedLineSearchGridCoordListEnumerator(PairedLineSearchGrid SearchGrid, IEnumerable<Coord> Coords)
            {
                this.searchGrid = SearchGrid;
                this.coords = Coords;
                Reset();
            }

            public GridLineSegmentPair Current
            {
                get
                {
                    GridLineSegmentPair segment = currentCell[iGridIndex];
                    //                   Debug.Assert(UniqueLines.Contains(segment) == false);
                    //                   UniqueLines.Add(segment);
                    return segment;
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    GridLineSegmentPair segment = currentCell[iGridIndex];
                    //                    Debug.Assert(UniqueLines.Contains(segment) == false);
                    //                    UniqueLines.Add(segment);
                    return segment;
                }
            }

            public bool MoveNext()
            {
                iGridIndex++;
                if (currentCell == null)
                {
                    bool success = coordEnum.MoveNext();
                    if (!success)
                        return false;

                    currentCell = searchGrid._LineGrid[coordEnum.Current.iX, coordEnum.Current.iY];
                }

                while (iGridIndex >= currentCell.Count)// || UniqueLines.Contains(currentCell[iGridIndex]))
                {
                    //Figure out if we are advancing because of a repeat value or grid index rollover
                    if (iGridIndex >= currentCell.Count)
                    {
                        iGridIndex = 0;
                        bool success = coordEnum.MoveNext();
                        if (!success)
                            return false;

                        currentCell = searchGrid._LineGrid[coordEnum.Current.iX, coordEnum.Current.iY];

                    }
                    else //we advanced because of a repeat value, increment iGridIndex
                    {
                        iGridIndex++;
                    }
                }

                return true;
            }

            public void Reset()
            {
                //              UniqueLines.Clear();
                iGridIndex = -1; //-1 because MoveNext is called before the first value is read
                coordEnum = coords.GetEnumerator();
            }

            public void Dispose()
            {
                return;
            }

            public IEnumerator<GridLineSegmentPair> GetEnumerator()
            {
                this.Reset();
                return this;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                this.Reset();
                return this;
            }
        }

        #endregion

        #region PairedLineSearchGridEnumerator
        /*
        private class PairedLineSearchGridRectangleEnumerator : IEnumerable<GridLineSegmentPair>, IEnumerator<GridLineSegmentPair>
        {
            private PairedLineSearchGrid searchGrid;
            private List<GridLineSegmentPair> currentCell;
            private Coord start;
            private Coord end;
            private int iX = 0;
            private int iY = 0; 
            private int iGridIndex = -1;

            //Only return unique values
 //           SortedSet<GridLineSegmentPair> UniqueLines = new SortedSet<GridLineSegmentPair>();

            public PairedLineSearchGridRectangleEnumerator(PairedLineSearchGrid SearchGrid, Coord Start, Coord End)
            {
                this.searchGrid = SearchGrid; 
                this.start = Start;
                this.end = End;
                Reset(); 
            }

            public GridLineSegmentPair Current
            {
                get {
                    GridLineSegmentPair segment = currentCell[iGridIndex]; 
//                    UniqueLines.Add(segment);
                    return segment;
                }
            }
            
            object System.Collections.IEnumerator.Current
            {
                get
                {
                    GridLineSegmentPair segment = currentCell[iGridIndex];
//                    UniqueLines.Add(segment);
                    return segment;
                }
            }
            
            public bool MoveNext()
            {
                iGridIndex++;
                while (iGridIndex >= currentCell.Count)// || UniqueLines.Contains(currentCell[iGridIndex]))
                {
                    iGridIndex = 0;
                    iX++;
                    if (iX > end.iX)
                    {
                        iX = start.iX;
                        iY++;
                        if (iY > end.iY)
                        {
                            return false;
                        }
                        else
                        {
                            currentCell = searchGrid._LineGrid[iX, iY];
                        }
                    }
                    else
                    {
                        currentCell = searchGrid._LineGrid[iX, iY];
                    }
                }

                return true; 
            }
            
            public void Reset()
            {
 //               UniqueLines.Clear();
                iX = start.iX;
                iY = start.iY;
                iGridIndex = -1; //-1 because MoveNext is called before the first value is read
                currentCell = searchGrid._LineGrid[iX, iY];
            }

            public void Dispose()
            {
                return; 
            }

            public IEnumerator<GridLineSegmentPair> GetEnumerator()
            {
                return this; 
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this; 
            }
        }
        */
        #endregion

        GridRectangle Bounds;
        List<GridLineSegmentPair>[,] _LineGrid = new List<GridLineSegmentPair>[0, 0];

        double GridWidth;
        double GridHeight;

        int NumGridsX;
        int NumGridsY;

        int EstimatedLinesPerCell;


        public PairedLineSearchGrid(MappingGridVector2[] _mapPoints, GridRectangle bounds, List<int>[] edges)
        {
            if (_mapPoints == null || edges == null)
                throw new ArgumentNullException("PairedLineSearchGrid Constructor");

            this.Bounds = bounds;

            int numPoints = _mapPoints.Length;
            if (numPoints == 0)
                numPoints = 1;

            //Calculate number of grid cells based on num points and boundaries
            double NumGrids = Math.Ceiling(System.Math.Sqrt(numPoints));

            int NumGridsEachDimension = (int)Math.Ceiling(Math.Sqrt(NumGrids));

            this.GridWidth = bounds.Width / NumGridsEachDimension;
            this.GridHeight = bounds.Width / NumGridsEachDimension;

            if (GridWidth < GridHeight)
                GridWidth = GridHeight;
            else
                GridHeight = GridWidth;

            if (GridWidth < 1)
                GridWidth = 1;
            if (GridHeight < 1)
                GridHeight = 1;

            NumGridsX = (int)Math.Ceiling(bounds.Width / GridWidth);
            NumGridsY = (int)Math.Ceiling(bounds.Height / GridHeight);

            this.EstimatedLinesPerCell = (int)(numPoints / NumGrids);

            _LineGrid = new List<GridLineSegmentPair>[NumGridsX + 1, NumGridsY + 1];
            //Initialize the grid
            for (int iX = 0; iX < NumGridsX + 1; iX++)
            {
                for (int iY = 0; iY < NumGridsY + 1; iY++)
                {
                    _LineGrid[iX, iY] = new List<GridLineSegmentPair>(this.EstimatedLinesPerCell);
                }
            }

            //Populate the grid by adding each intersecting GridLineSegment to the proper cells
            for (int iPoint = 0; iPoint < edges.Length; iPoint++)
            {
                //Get the list of edges
                List<int> edgeList = edges[iPoint];

                for (int iEdge = 0; iEdge < edgeList.Count; iEdge++)
                {
                    int iEdgePoint = edgeList[iEdge];
                    if (iEdgePoint <= iPoint) //We would have tested this point already so skip it
                        continue;

                    if (GridVector2.DistanceSquared(_mapPoints[iPoint].MappedPoint, _mapPoints[iEdgePoint].MappedPoint) <= Global.EpsilonSquared)
                    {
                        Debug.Fail("Map points are equal");
                        continue;
                    }

                    if (GridVector2.DistanceSquared(_mapPoints[iPoint].ControlPoint, _mapPoints[iEdgePoint].ControlPoint) <= Global.EpsilonSquared)
                    {
                        Debug.Fail("Control points are equal");
                        continue;
                    }

                    //Build the edge and find out if it intersects
                    GridLineSegmentPair pair = new GridLineSegmentPair();
                    try
                    {
                        GridLineSegment mapLine = new GridLineSegment(_mapPoints[iPoint].MappedPoint, _mapPoints[iEdgePoint].MappedPoint);
                        GridLineSegment ctrlLine = new GridLineSegment(_mapPoints[iPoint].ControlPoint, _mapPoints[iEdgePoint].ControlPoint);

                        pair.mapLine = mapLine;
                        pair.ctrlLine = ctrlLine;

                        IEnumerable<Coord> Coords = GetCoordsForLine(mapLine);
                        foreach (Coord coord in Coords)
                        {
                            _LineGrid[coord.iX, coord.iY].Add(pair);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        Trace.WriteLine("Error creating GridLineSegment\n" + e.ToString());
                        continue;
                    }





                }
            }
        }

        private Coord GetCoord(GridVector2 position)
        {
            return GetCoord(position.X, position.Y);
        }

        private Coord GetCoord(double x, double y)
        {
            int iX = (int)((x - Bounds.Left) / this.GridWidth);
            int iY = (int)((y - Bounds.Bottom) / this.GridHeight);
            if (iX < 0)
                iX = 0;
            if (iY < 0)
                iY = 0;
            if (iX > NumGridsX)
                iX = NumGridsX;
            if (iY > NumGridsY)
                iY = NumGridsY;

            return new Coord(iX, iY);
        }


        private IEnumerable<Coord> GetCoordsForLine(GridLineSegment line)
        {
            Coord start;
            Coord end;
            if (line.A.X < line.B.X)
            {
                start = GetCoord(line.A);
                end = GetCoord(line.B);
            }
            else
            {
                start = GetCoord(line.B);
                end = GetCoord(line.A);
            }

            //Find the min indicies so loops function
            int iStartX = Math.Min(start.iX, end.iX);
            int iStartY = Math.Min(start.iY, end.iY);
            int iEndX = Math.Max(start.iX, end.iX);
            int iEndY = Math.Max(start.iY, end.iY);

            List<Coord> listCoords = new List<Coord>(Math.Abs(end.iX - start.iX) * Math.Abs(end.iY - start.iY));
            if (start.iX == end.iX)
            {

                for (int iY = iStartY; iY <= iEndY; iY++)
                {
                    listCoords.Add(new Coord(start.iX, iY));
                }
            }
            else if (start.iY == end.iY)
            {
                for (int iX = iStartX; iX <= iEndX; iX++)
                {
                    listCoords.Add(new Coord(iX, start.iY));
                }
            }
            else
            {
                //Figure out the line function
                double m = line.slope;
                double b = line.intercept;

                listCoords.Add(start);

                double minX = Math.Min(line.A.X, line.B.X);
                double maxX = Math.Max(line.A.X, line.B.X);
                double minY = Math.Min(line.A.Y, line.B.Y);
                double maxY = Math.Max(line.A.Y, line.B.Y);

                //Add the line to all cells it belongs in 
                for (int iX = iStartX; iX <= iEndX; iX++)
                {
                    double x = ((iX) * this.GridWidth) + Bounds.Left;

                    if (x < line.A.X || x > line.B.X)
                        continue;

                    double y = m * x + b;

                    if (y < minY || y > maxY)
                        continue;

                    Coord intersect = GetCoord(x, y);

                    if (intersect.iX - 1 >= 0)
                        listCoords.Add(new Coord(intersect.iX - 1, intersect.iY));

                    listCoords.Add(new Coord(intersect.iX, intersect.iY));
                }

                m = line.yslope;
                b = line.yintercept;



                for (int iY = iStartY; iY <= iEndY; iY++)
                {
                    double y = (iY * this.GridHeight) + Bounds.Bottom;
                    if (y < minY || y > maxY)
                        continue;

                    double x = m * y + b;

                    if (x < line.A.X || x > line.B.X)
                        continue;

                    Coord intersect = GetCoord(x, y);

                    if (intersect.iY - 1 >= 0)
                        listCoords.Add(new Coord(intersect.iX, intersect.iY - 1));

                    listCoords.Add(new Coord(intersect.iX, intersect.iY));
                }

                listCoords.Add(end);
                listCoords.Sort();

                List<Coord> uniqueCoords = new List<Coord>(listCoords.Count);

                //Remove duplicates
                foreach (Coord coord in listCoords)
                {
                    int numUnique = uniqueCoords.Count;
                    if (numUnique == 0)
                        uniqueCoords.Add(coord);
                    else
                    {
                        if (uniqueCoords[numUnique - 1] != coord)
                        {
                            uniqueCoords.Add(coord);
                        }
                    }
                }

                listCoords = uniqueCoords;
            }

#if DEBUG
            //          listCoords.Sort(); 
#endif

            return listCoords;
        }


        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        public IEnumerable<GridLineSegmentPair> GetPotentialIntersections(GridLineSegment line)
        {
            //     List<GridLineSegmentPair> LineList;
            //Coord start = GetCoord(new GridVector2(line.MinX, line.MinY));
            //Coord end = GetCoord(new GridVector2(line.MaxX, line.MaxY));

            //No sense testing if it doesn't intersect our boundary
            if (this.Bounds.Intersects(line.BoundingBox) == false)
            {
                return new GridLineSegmentPair[0];
            }

            return new PairedLineSearchGridCoordListEnumerator(this, GetCoordsForLine(line));

        }
    }
}
