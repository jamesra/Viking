using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// The line search grid divides space into a regular grid.  Each line segment defines a rectangle.  If the rectangle intersects
    /// a grid cell a pointer is added to that cell.  When a point is passed in the lines in the cell containing the point and all adjacent cells
    /// are checked for intersection.  The matching line and object are returned
    /// </summary>
    public class LineSearchGrid<T> : IDisposable
    {
        GridRectangle Bounds;
        readonly List<GridLineSegment>[,] _LineGrid;

        readonly Dictionary<GridLineSegment, T> tableLineToValue;
        readonly Dictionary<T, GridLineSegment> tableValueToLine;

        readonly double GridWidth;
        readonly double GridHeight;

        readonly int NumGridsX;
        readonly int NumGridsY;

        readonly int EstimatedLinesPerCell;

        /// <summary>
        /// I base the size of the list to allocate based on how large it was the last time a query was run
        /// </summary>
        private int _LastIntersectingLineCount;

        public int Count { get { return tableLineToValue.Count; } }

        System.Threading.ReaderWriterLockSlim rwLock = new System.Threading.ReaderWriterLockSlim();

        public LineSearchGrid(GridRectangle bounds, int EstimatedLineCount)
        {
            if (EstimatedLineCount <= 1)
            {
                EstimatedLineCount = 1000;
            }

            this.Bounds = bounds;
            tableLineToValue = new Dictionary<GridLineSegment, T>(EstimatedLineCount / 10);
            tableValueToLine = new Dictionary<T, GridLineSegment>(EstimatedLineCount / 10);

            //Calculate number of grid cells based on num points and boundaries
            double NumGrids = Math.Ceiling(System.Math.Sqrt(EstimatedLineCount));
            int NumGridsEachDimension = (int)Math.Ceiling(Math.Sqrt(NumGrids));

            this.GridWidth = bounds.Width / NumGridsEachDimension;
            this.GridHeight = bounds.Width / NumGridsEachDimension;

            if (GridWidth < GridHeight)
                GridWidth = GridHeight;
            else
                GridHeight = GridWidth;

            NumGridsX = (int)Math.Ceiling(bounds.Width / GridWidth);
            NumGridsY = (int)Math.Ceiling(bounds.Height / GridHeight);

            if (NumGridsX < 0 || NumGridsY < 0)
            {
                throw new ArgumentException("NumGridsX and Y must be positive");
            }

            this.EstimatedLinesPerCell = (int)((double)EstimatedLineCount / NumGrids);
            _LastIntersectingLineCount = this.EstimatedLinesPerCell;

            _LineGrid = new List<GridLineSegment>[NumGridsX + 1, NumGridsY + 1];
            //Initialize the grid
            for (int iX = 0; iX < NumGridsX + 1; iX++)
            {
                for (int iY = 0; iY < NumGridsY + 1; iY++)
                {
                    _LineGrid[iX, iY] = new List<GridLineSegment>(this.EstimatedLinesPerCell);
                }
            }
        }

        #region LineSearchGridCoordListEnumerator

        /// <summary>
        /// Enumerates over a range of cells, only returning unique values.
        /// We use these because returning a list would copy a massive amount of memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class LineSearchGridCoordListEnumerator : IEnumerable<GridLineSegment>, IEnumerator<GridLineSegment>
        {
            private readonly LineSearchGrid<T> searchGrid;
            private List<GridLineSegment> currentCell;
            private readonly IEnumerable<Coord> coords;
            private IEnumerator<Coord> coordEnum;
            private int iGridIndex = -1;

            private GridLineSegment CurrentGridLineSegment;

            /// <summary>
            /// Set to true if the enumerator should take a read lock as it walks the collection
            /// </summary>
            private readonly bool UseLock;

            //Only return unique values
            readonly SortedSet<GridLineSegment> UniqueLines = new SortedSet<GridLineSegment>();

            public LineSearchGridCoordListEnumerator(LineSearchGrid<T> SearchGrid, IEnumerable<Coord> Coords, bool uselock = false)
            {
                this.searchGrid = SearchGrid;
                this.coords = Coords;
                this.UseLock = uselock;
                Reset();
            }

            public GridLineSegment Current
            {
                get
                {
                    Debug.Assert(UniqueLines.Contains(CurrentGridLineSegment) == false);
                    UniqueLines.Add(CurrentGridLineSegment);
                    return CurrentGridLineSegment;
                    /*
                    GridLineSegment segment = currentCell[iGridIndex];
                    Debug.Assert(UniqueLines.Contains(segment) == false);
                    UniqueLines.Add(segment);
                    return segment;
                     */
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    Debug.Assert(UniqueLines.Contains(CurrentGridLineSegment) == false);
                    UniqueLines.Add(CurrentGridLineSegment);
                    return CurrentGridLineSegment;
                    /*
                    GridLineSegment segment = currentCell[iGridIndex];
                    Debug.Assert(UniqueLines.Contains(segment) == false);
                    UniqueLines.Add(segment);
                    return segment;
                     */
                }
            }

            public bool MoveNext()
            {
                try
                {
                    if (UseLock)
                        searchGrid.rwLock.EnterReadLock();

                    iGridIndex++;
                    if (currentCell == null)
                    {
                        bool success = coordEnum.MoveNext();
                        if (!success)
                            return false;

                        currentCell = searchGrid._LineGrid[coordEnum.Current.iX, coordEnum.Current.iY];
                    }

                    while (iGridIndex >= currentCell.Count || UniqueLines.Contains(currentCell[iGridIndex]))
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

                    this.CurrentGridLineSegment = currentCell[iGridIndex];
                }
                finally
                {
                    if (UseLock && searchGrid.rwLock.IsReadLockHeld)
                    {
                        searchGrid.rwLock.ExitReadLock();
                    }
                }

                return true;
            }

            public void Reset()
            {
                UniqueLines.Clear();
                iGridIndex = -1; //-1 because MoveNext is called before the first value is read
                coordEnum = coords.GetEnumerator();
            }

            public void Dispose()
            {
                return;
            }

            public IEnumerator<GridLineSegment> GetEnumerator()
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

        #region LineSearchGridRectangleEnumerator

        /// <summary>
        /// Enumerates over a range of cells, only returning unique values.
        /// We use these because returning a list would copy a massive amount of memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class LineSearchGridRectangleEnumerator : IEnumerable<GridLineSegment>, IEnumerator<GridLineSegment>
        {
            private readonly LineSearchGrid<T> searchGrid;
            private List<GridLineSegment> currentCell;
            private readonly Coord start;
            private readonly Coord end;
            private int iX = 0;
            private int iY = 0;
            private int iGridIndex = -1;

            private GridLineSegment CurrentGridLineSegment;

            /// <summary>
            /// Set to true if the enumerator should take a read lock as it walks the collection
            /// </summary>
            private readonly bool UseLock;

            //Only return unique values
            readonly SortedSet<GridLineSegment> UniqueLines = new SortedSet<GridLineSegment>();

            public LineSearchGridRectangleEnumerator(LineSearchGrid<T> SearchGrid, Coord Start, Coord End, bool uselock = false)
            {
                this.searchGrid = SearchGrid;
                this.start = Start;
                this.end = End;
                this.UseLock = uselock;
                Reset();

            }

            public GridLineSegment Current
            {
                get
                {
                    Debug.Assert(UniqueLines.Contains(CurrentGridLineSegment) == false);
                    UniqueLines.Add(CurrentGridLineSegment);
                    return CurrentGridLineSegment;
                    /*
                    GridLineSegment segment = currentCell[iGridIndex];
                    Debug.Assert(UniqueLines.Contains(segment) == false);
                    UniqueLines.Add(segment);
                    return segment;
                     */
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {

                    Debug.Assert(UniqueLines.Contains(CurrentGridLineSegment) == false);
                    UniqueLines.Add(CurrentGridLineSegment);
                    return CurrentGridLineSegment;

                    /*
                    GridLineSegment segment = currentCell[iGridIndex];
                    Debug.Assert(UniqueLines.Contains(segment) == false);
                    UniqueLines.Add(segment);
                    return segment;
                     */
                }
            }

            public bool MoveNext()
            {
                try
                {
                    if (UseLock)
                        searchGrid.rwLock.EnterReadLock();

                    iGridIndex++;
                    while (iGridIndex >= currentCell.Count || UniqueLines.Contains(currentCell[iGridIndex]))
                    {
                        //Figure out if we are advancing because of a repeat value or grid index rollover
                        if (iGridIndex >= currentCell.Count)
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
                        else //we advanced because of a repeat value, increment iGridIndex
                        {
                            iGridIndex++;
                        }
                    }

                    CurrentGridLineSegment = currentCell[iGridIndex];
                }
                finally
                {
                    if (UseLock && searchGrid.rwLock.IsReadLockHeld)
                    {
                        searchGrid.rwLock.ExitReadLock();
                    }
                }

                return true;
            }

            public void Reset()
            {
                UniqueLines.Clear();
                iX = start.iX;
                iY = start.iY;
                iGridIndex = -1; //-1 because MoveNext is called before the first value is read
                currentCell = searchGrid._LineGrid[iX, iY];
            }

            public void Dispose()
            {
                return;
            }

            public IEnumerator<GridLineSegment> GetEnumerator()
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

        public void Clear()
        {
            try
            {
                rwLock.EnterWriteLock();

                tableLineToValue.Clear();

                for (int iX = 0; iX < NumGridsX; iX++)
                {
                    for (int iY = 0; iY < NumGridsY; iY++)
                    {
                        _LineGrid[iX, iY].Clear();
                    }
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool Contains(GridLineSegment line)
        {
            try
            {
                rwLock.EnterReadLock();
                return tableLineToValue.ContainsKey(line);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public bool Contains(T key)
        {
            try
            {
                rwLock.EnterReadLock();
                return tableValueToLine.ContainsKey(key);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public GridLineSegment[] Lines
        {
            get { return tableLineToValue.Keys.ToArray(); }
        }

        public T[] Values
        {
            get { return tableValueToLine.Keys.ToArray(); }
        }


        public void Add(GridLineSegment line, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                Debug.Assert(tableLineToValue.ContainsKey(line) == false);
                if (tableLineToValue.ContainsKey(line))
                {
                    tableLineToValue[line] = value;
                    return;
                }

                try
                {
                    rwLock.EnterWriteLock();

                    Debug.Assert(tableLineToValue.ContainsKey(line) == false);
                    if (tableLineToValue.ContainsKey(line))
                    {
                        tableLineToValue[line] = value;
                        return;
                    }

                    IEnumerable<Coord> coords = GetCoordsForLine(line);
                    foreach (Coord coord in coords)
                    {
                        List<GridLineSegment> lines = _LineGrid[coord.iX, coord.iY];
                        Debug.Assert(lines.Contains(line) == false);
                        lines.Add(line);
                    }

                    tableLineToValue.Add(line, value);
                    tableValueToLine.Add(value, line);
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }



        public bool TryAdd(GridLineSegment line, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                if (tableValueToLine.ContainsKey(value))
                    return false;

                try
                {
                    rwLock.EnterWriteLock();

                    if (tableValueToLine.ContainsKey(value))
                        return false;

                    //This happens when structurelinks or location links are duplicated in the database.f
                    if (tableLineToValue.ContainsKey(line))
                        return false;

                    //Add the line to all cells it belongs in 
                    IEnumerable<Coord> coords = GetCoordsForLine(line);
                    foreach (Coord coord in coords)
                    {
                        List<GridLineSegment> lines = _LineGrid[coord.iX, coord.iY];

                        //This happens when structurelinks or location links are duplicated in the database.f
                        bool ContainsLine = lines.Contains(line);
                        Debug.Assert(ContainsLine == false);
                        if (!ContainsLine)
                            lines.Add(line);
                    }

                    Debug.Assert(tableLineToValue.ContainsKey(line) == false);
                    if (!tableLineToValue.ContainsKey(line))
                        tableLineToValue.Add(line, value);

                    tableValueToLine.Add(value, line);
                }
                catch (ArgumentException e)
                {
                    System.Diagnostics.Trace.WriteLine("Exception in LineSearchGrid.TryAdd", e.ToString());
                    return false;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }

            return true;

        }

        public void Remove(GridLineSegment line)
        {
            try
            {
                rwLock.EnterWriteLock();

                //Add the line to all cells it belongs in 
                IEnumerable<Coord> coords = GetCoordsForLine(line);
                foreach (Coord coord in coords)
                {
                    List<GridLineSegment> lines = _LineGrid[coord.iX, coord.iY];
                    Debug.Assert(lines.Contains(line));
                    lines.Remove(line);
                }

                T value = tableLineToValue[line];
                bool LineRemove = tableLineToValue.Remove(line);
                bool ValueRemove = tableValueToLine.Remove(value);
                Debug.Assert(LineRemove == ValueRemove);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool TryRemove(GridLineSegment line, out T value)
        {
            value = default;
            try
            {
                rwLock.EnterUpgradeableReadLock();
                if (!tableLineToValue.ContainsKey(line))
                    return false;

                try
                {
                    rwLock.EnterWriteLock();

                    //Add the line to all cells it belongs in 
                    IEnumerable<Coord> coords = GetCoordsForLine(line);
                    foreach (Coord coord in coords)
                    {
                        List<GridLineSegment> lines = _LineGrid[coord.iX, coord.iY];
                        Debug.Assert(lines.Contains(line));
                        lines.Remove(line);
                    }

                    value = tableLineToValue[line];
                    bool LineRemove = tableLineToValue.Remove(line);
                    bool ValueRemove = tableValueToLine.Remove(value);
                    Debug.Assert(LineRemove == ValueRemove);
                    return true;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public bool TryRemove(T value, out GridLineSegment OldLine)
        {
            try
            {

                rwLock.EnterUpgradeableReadLock();

                bool TableHasValue = tableValueToLine.TryGetValue(value, out OldLine);
                if (!TableHasValue)
                {
                    OldLine = new GridLineSegment();
                    return false;
                }

                try
                {
                    rwLock.EnterWriteLock();

                    //Add the line to all cells it belongs in 
                    IEnumerable<Coord> coords = GetCoordsForLine(OldLine);
                    foreach (Coord coord in coords)
                    {
                        List<GridLineSegment> lines = _LineGrid[coord.iX, coord.iY];
                        Debug.Assert(lines.Contains(OldLine));
                        lines.Remove(OldLine);
                    }

                    bool LineRemove = tableLineToValue.Remove(OldLine);
                    bool ValueRemove = tableValueToLine.Remove(value);
                    Debug.Assert(LineRemove == ValueRemove);
                    return true;
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
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

                return UniqueItems<Coord>(listCoords);
                /*
                listCoords.Sort(); 
    
                
                List<Coord> uniqueCoords = new List<Coord>(listCoords.Count);
                
                //Remove duplicates
                foreach(Coord coord in listCoords)
                {
                    int numUnique = uniqueCoords.Count;
                    if(numUnique == 0)
                        uniqueCoords.Add(coord);
                    else
                    {
                        if(uniqueCoords[numUnique-1] != coord)
                        {
                            uniqueCoords.Add(coord);
                        }
                    }   
                }

                listCoords = uniqueCoords; 
                */
            }

#if DEBUG
            //          listCoords.Sort(); 
#endif

            return listCoords;
        }




        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// Search size, number of cells to search
        /// Border Only, only search cells around the edge
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        private List<GridLineSegment> GetPotentialIntersections(GridVector2 position, int SearchSize, bool BorderOnly)
        {
            Coord coord = GetCoord(position);


            int start_iX = coord.iX - SearchSize;
            int start_iY = coord.iY - SearchSize;
            int end_iX = coord.iX + SearchSize;
            int end_iY = coord.iY + SearchSize;


            if (start_iX < 0)
                start_iX = 0;
            if (start_iY < 0)
                start_iY = 0;
            if (end_iX > NumGridsX)
                end_iX = NumGridsX;
            if (end_iY > NumGridsY)
                end_iY = NumGridsY;

            //            int numCells = (end_iX - start_iX) * (end_iY - start_iY);
            //TODO
            //SortedSet<GridLineSegment> 
            List<GridLineSegment> LineList = new List<GridLineSegment>(_LastIntersectingLineCount);

            Coord start = new Coord(start_iX, start_iY);
            Coord end = new Coord(end_iX, end_iY);

            if (BorderOnly)
            {
                for (int iX = start.iX; iX <= end.iX; iX++)
                {
                    if (coord.iY - SearchSize >= 0)
                        LineList.AddRange(_LineGrid[iX, start.iY]);

                    if (coord.iY + SearchSize < NumGridsY)
                        LineList.AddRange(_LineGrid[iX, end.iY]);
                }

                for (int iY = start.iY + 1; iY <= end.iY - 1; iY++)
                {
                    if (coord.iX - SearchSize >= 0)
                        LineList.AddRange(_LineGrid[start.iX, iY]);


                    if (coord.iX + SearchSize < NumGridsX)
                        LineList.AddRange(_LineGrid[end.iX, iY]);
                }
            }
            else
            {
                //Add the line to all cells it belongs in 
                for (int iX = start.iX; iX <= end.iX; iX++)
                {
                    for (int iY = start.iY; iY <= end.iY; iY++)
                    {
                        LineList.AddRange(_LineGrid[iX, iY]);
                    }
                }
            }

            _LastIntersectingLineCount = LineList.Count;

            return UniqueItems<GridLineSegment>(LineList);
        }

        protected List<U> UniqueItems<U>(List<U> LineList)
        {
            //Only return unique values
            List<U> unique_list = new List<U>(LineList.Count);
            LineList.Sort();
            U lastItem = default;
            foreach (U item in LineList)
            {
                if (item.Equals(lastItem))
                    continue;
                else
                {
                    unique_list.Add(item);
                    lastItem = item;
                }
            }

            return unique_list;
        }

        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        public IEnumerable<GridLineSegment> GetPotentialIntersections(GridLineSegment line)
        {
            return GetPotentialIntersections(line, true);
        }

        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// Called internally.  When the spinlock is held set TakeSpinLock to false
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        private IEnumerable<GridLineSegment> GetPotentialIntersections(GridLineSegment line, bool TakeSpinLock)
        {
            //If the line doesn't intersect our bounding box then skip the search
            if (!Bounds.Intersects(line.BoundingBox))
                return Array.Empty<GridLineSegment>();

            try
            {
                if (TakeSpinLock)
                    rwLock.EnterReadLock();

                //                List<GridLineSegment> LineList = new List<GridLineSegment>();
                //                Coord start = GetCoord(new GridVector2(line.MinX, line.MinY));
                //                Coord end = GetCoord(new GridVector2(line.MaxX, line.MaxY));

                IEnumerable<Coord> coords = GetCoordsForLine(line);
                return new LineSearchGridCoordListEnumerator(this, coords, true);
            }
            finally
            {
                if (TakeSpinLock)
                    rwLock.ExitReadLock();
            }
        }

        public T this[GridLineSegment key]
        {
            get { return tableLineToValue[key]; }
        }

        public GridLineSegment this[T key]
        {
            get { return tableValueToLine[key]; }
        }

        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        public T[] GetValues(GridLineSegment line)
        {
            try
            {
                rwLock.EnterReadLock();

                IEnumerable<GridLineSegment> LineList = GetPotentialIntersections(line, false);
                List<T> values = new List<T>();
                foreach (GridLineSegment gridLine in LineList)
                {
                    values.Add(tableLineToValue[gridLine]);
                }

                if (LineList is IDisposable ListDispose)
                    ListDispose.Dispose();

                // Trace.WriteLine("Enumerator: " + values.Count.ToString()); 
                return values.ToArray();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns a list of GridLineSegments that could possible intersect the passed line
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        public T[] GetValues(in GridRectangle rect)
        {
            try
            {
                Coord start = GetCoord(new GridVector2(rect.Left, rect.Bottom));
                Coord end = GetCoord(new GridVector2(rect.Right, rect.Top));

                rwLock.EnterReadLock();

                List<T> values = new List<T>();
                using (LineSearchGridRectangleEnumerator LineList = new LineSearchGridRectangleEnumerator(this, start, end, false))
                {
                    foreach (GridLineSegment gridLine in LineList)
                    {
                        values.Add(tableLineToValue[gridLine]);
                    }
                }

                // Trace.WriteLine("Enumerator: " + values.Count.ToString()); 
                return values.ToArray();
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Return the object nearest to the passed line
        /// </summary>
        /// <param name="TestLine"></param>
        /// <param name="intersection"></param>
        /// <param name="nearestIntersect"></param>
        /// <returns></returns>
        public T FindNearest(GridLineSegment TestLine, out GridVector2 intersection, out double nearestIntersect)
        {
            bool LockTaken = false;
            try
            {
                rwLock.EnterReadLock();

                intersection = default;
                nearestIntersect = double.MinValue;
                IEnumerable<GridLineSegment> potentialIntersections = GetPotentialIntersections(TestLine, !LockTaken);
                GridLineSegment BestLine = default;
                foreach (GridLineSegment l in potentialIntersections)
                {
                    //Build the edge and find out if it intersects
                    if (l.MinX > TestLine.MaxX)
                        continue;
                    if (l.MaxX < TestLine.MinX)
                        continue;
                    if (l.MinY > TestLine.MaxY)
                        continue;
                    if (l.MaxY < TestLine.MinY)
                        continue;

                    GridVector2 result;
                    bool bIntersected = l.Intersects(in TestLine, out result);
                    double distance = GridVector2.Distance(in TestLine.A, in result);
                    if (distance < nearestIntersect && bIntersected)
                    {
                        nearestIntersect = distance;
                        intersection = result;
                        BestLine = l;
                    }
                }

                if (potentialIntersections is IDisposable ListDispose)
                    ListDispose.Dispose();

                potentialIntersections = null;

                if (tableLineToValue.ContainsKey(BestLine))
                    return tableLineToValue[BestLine];

                return default;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }


        /// <summary>
        /// Return the object nearest to the passed line
        /// </summary>
        /// <param name="TestLine"></param>
        /// <param name="intersection"></param>
        /// <param name="nearestIntersect"></param>
        /// <returns></returns>
        public T GetNearest(GridVector2 Position, out GridVector2 BestIntersection, out double ClosestDistance)
        {
            try
            {
                rwLock.EnterReadLock();

                BestIntersection = default;
                ClosestDistance = double.MaxValue;
                int SearchSize = 1;
                List<GridLineSegment> potentialIntersections = GetPotentialIntersections(Position, SearchSize, false);

                //Expand search until we've found a line to test
                while (potentialIntersections.Count == 0 &&
                    SearchSize < (GridWidth / 2) &&
                    SearchSize < (GridHeight / 2))
                {
                    SearchSize++;
                    potentialIntersections = GetPotentialIntersections(Position, SearchSize, true);
                }

                GridLineSegment BestLine = default;
                foreach (GridLineSegment l in potentialIntersections)
                {
                    GridVector2 thisIntersection;
                    double distance = l.DistanceToPoint(in Position, out thisIntersection);
                    if (distance < ClosestDistance)
                    {
                        ClosestDistance = distance;
                        BestIntersection = thisIntersection;
                        BestLine = l;
                    }
                }

                if (tableLineToValue.ContainsKey(BestLine))
                    return tableLineToValue[BestLine];

                return default;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        protected virtual void Dispose(bool freeManagedObjectsAlso)
        {
            if (freeManagedObjectsAlso)
            {
                if (rwLock != null)
                {
                    rwLock.Dispose();
                    rwLock = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
