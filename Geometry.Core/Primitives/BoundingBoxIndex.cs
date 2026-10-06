using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// AABB index used by Core polygon/polyline queries. Replaces the RTree dependency
    /// for primitives; repo Geometry keeps RTree for transforms and large spatial indexes.
    /// </summary>
    /// <remarks>
    /// Queries scan the entry list linearly. Once an index holds more than <see cref="MinEntriesForSpatialIndex"/>
    /// entries and has answered <see cref="QueriesBeforeSpatialIndex"/> queries without being structurally changed,
    /// a uniform grid is built lazily and used for queries that touch a small part of it.
    /// <para>
    /// The grid never changes what a query returns. Hits always come back in entry-list order, which is the order the
    /// linear scan produces, so callers that stop at the first hit or key results by arrival order see no difference.
    /// </para>
    /// <para>
    /// Not safe for concurrent mutation. Concurrent queries are safe: the grid is immutable once built and is
    /// published through a single reference write, so racing threads at worst build it twice.
    /// </para>
    /// </remarks>
    internal sealed class BoundingBoxIndex<T> where T : IEquatable<T>
    {
        /// <summary>Indexes with this many entries or fewer are always scanned linearly; measured, a grid does not pay off below about this size.</summary>
        internal const int MinEntriesForSpatialIndex = 64;

        /// <summary>
        /// Queries answered by linear scan after the last Add or Delete before the grid is built. Building costs a
        /// few linear scans, so an index that is edited and queried once per edit never pays for it.
        /// </summary>
        internal const int QueriesBeforeSpatialIndex = 8;

        private readonly List<(Rectangle Bounds, T Item)> _entries = [];
        private readonly Dictionary<T, int> _indexByItem;

        /// <summary>Null when not built or invalidated. Positions in <see cref="_entries"/> are baked into the grid.</summary>
        private SpatialGrid? _grid;
        private int _queriesSinceChange;

        [ThreadStatic]
        private static int[]? t_queryBuffer;

        public BoundingBoxIndex()
        {
            _indexByItem = new Dictionary<T, int>();
        }

        public BoundingBoxIndex(IEqualityComparer<T> comparer)
        {
            _indexByItem = new Dictionary<T, int>(comparer);
        }

        public IList<T> Items
        {
            get
            {
                T[] items = new T[_entries.Count];
                for (int i = 0; i < _entries.Count; i++)
                    items[i] = _entries[i].Item;
                return items;
            }
        }

        /// <summary>True when a grid is currently built. Intended for tests.</summary>
        internal bool HasSpatialIndex => _grid is not null;

        public void Add(Rectangle bounds, T item)
        {
            if (_indexByItem.ContainsKey(item))
                throw new ArgumentException($"{item} is already in the index");

            _indexByItem[item] = _entries.Count;
            _entries.Add((bounds, item));
            InvalidateSpatialIndex();
        }

        /// <summary>
        /// Replaces the item stored under <paramref name="oldValue"/> keeping its bounds and list position, so the
        /// grid stays valid.
        /// </summary>
        public void Update(T oldValue, T newValue)
        {
            if (_indexByItem.ContainsKey(newValue))
                throw new ArgumentException($"{newValue} is already in the index and cannot replace {oldValue}");

            if (!_indexByItem.TryGetValue(oldValue, out int i))
                throw new KeyNotFoundException($"{oldValue} is not in the index and cannot be replaced");

            Rectangle bounds = _entries[i].Bounds;
            _indexByItem.Remove(oldValue);
            _indexByItem[newValue] = i;
            _entries[i] = (bounds, newValue);
        }

        /// <summary>
        /// Removes <paramref name="item"/> by moving the last entry into its slot, which changes list positions.
        /// </summary>
        public bool Delete(T item, out T removedItem)
        {
            removedItem = default;
            if (!_indexByItem.TryGetValue(item, out int i))
                return false;

            removedItem = _entries[i].Item;
            int last = _entries.Count - 1;
            if (i != last)
            {
                (Rectangle Bounds, T Item) moved = _entries[last];
                _entries[i] = moved;
                _indexByItem[moved.Item] = i;
            }

            _entries.RemoveAt(last);
            _indexByItem.Remove(item);
            InvalidateSpatialIndex();
            return true;
        }

        /// <summary>All items whose bounds intersect <paramref name="query"/>, in entry-list order.</summary>
        public List<T> Intersects(Rectangle query)
        {
            if (_entries.Count > MinEntriesForSpatialIndex)
            {
                int[]? buffer = t_queryBuffer;
                int count = QueryGrid(query, ref buffer);
                if (count >= 0)
                {
                    t_queryBuffer = buffer;
                    List<T> gridHits = new(count);
                    for (int i = 0; i < count; i++)
                        gridHits.Add(_entries[buffer![i]].Item);

                    return gridHits;
                }
            }

            return IntersectsLinear(query);
        }

        /// <summary>
        /// The linear scan, regardless of whether a grid exists. This is the reference behavior the grid must match.
        /// </summary>
        internal List<T> IntersectsLinear(Rectangle query)
        {
            List<T> hits = [];
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Bounds.Intersects(query))
                    hits.Add(_entries[i].Item);
            }

            return hits;
        }

        /// <summary>
        /// Lazily yields items whose bounds intersect <paramref name="query"/>, in entry-list order. Matches are
        /// found when enumeration starts; later edits to the index do not change the sequence.
        /// </summary>
        public IEnumerable<T> IntersectionGenerator(Rectangle query)
        {
            int[]? buffer = null;
            int count = QueryGrid(query, ref buffer);
            if (count >= 0)
            {
                T[] items = new T[count];
                for (int i = 0; i < count; i++)
                    items[i] = _entries[buffer![i]].Item;

                foreach (T item in items)
                    yield return item;

                yield break;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Bounds.Intersects(query))
                    yield return _entries[i].Item;
            }
        }

        /// <summary>Builds the grid now if the index is large enough, ignoring the query-count delay. Intended for tests.</summary>
        internal void BuildSpatialIndex()
        {
            _grid = SpatialGrid.TryBuild(_entries);
        }

        private void InvalidateSpatialIndex()
        {
            _grid = null;
            _queriesSinceChange = 0;
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> with the sorted list positions of entries intersecting
        /// <paramref name="query"/> using the grid.
        /// </summary>
        /// <returns>The hit count, or -1 when the caller must scan linearly (small index, grid not built yet,
        /// non-finite query, or a query that covers much of the grid).</returns>
        private int QueryGrid(in Rectangle query, ref int[]? buffer)
        {
            if (_entries.Count <= MinEntriesForSpatialIndex)
                return -1;

            SpatialGrid? grid = _grid;
            if (grid is null)
            {
                if (++_queriesSinceChange < QueriesBeforeSpatialIndex)
                    return -1;

                grid = SpatialGrid.TryBuild(_entries);
                _grid = grid;
                if (grid is null)
                    return -1;
            }

            return grid.Query(_entries, query, ref buffer);
        }

        /// <summary>
        /// Immutable uniform grid over a snapshot of the entry list. Each entry is listed in every cell its bounds
        /// overlap, except entries that are huge or non-finite, which are always tested directly.
        /// </summary>
        private sealed class SpatialGrid
        {
            /// <summary>Entries spanning more cells than this are tested on every query instead of being listed per cell.</summary>
            private const int MaxCellsPerEntry = 16;

            private readonly double _minX;
            private readonly double _minY;
            private readonly double _invCellWidth;
            private readonly double _invCellHeight;
            private readonly int _columns;
            private readonly int _rows;

            /// <summary>CSR layout: entries of cell c are <c>_cellItems[_cellStart[c] .. _cellStart[c + 1])</c>.</summary>
            private readonly int[] _cellStart;
            private readonly int[] _cellItems;

            /// <summary>Lower-left cell of each entry, used to report an entry only once per query.</summary>
            private readonly int[] _firstColumn;
            private readonly int[] _firstRow;

            /// <summary>Ascending list positions of entries not stored in cells.</summary>
            private readonly int[] _alwaysTest;

            private SpatialGrid(double minX, double minY, double invCellWidth, double invCellHeight, int columns, int rows,
                int[] cellStart, int[] cellItems, int[] firstColumn, int[] firstRow, int[] alwaysTest)
            {
                _minX = minX;
                _minY = minY;
                _invCellWidth = invCellWidth;
                _invCellHeight = invCellHeight;
                _columns = columns;
                _rows = rows;
                _cellStart = cellStart;
                _cellItems = cellItems;
                _firstColumn = firstColumn;
                _firstRow = firstRow;
                _alwaysTest = alwaysTest;
            }

            private static bool IsFinite(in Rectangle r) =>
                IsFinite(r.Left) && IsFinite(r.Right) && IsFinite(r.Bottom) && IsFinite(r.Top);

            private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

            /// <summary>Returns null when the entries cannot be usefully gridded (too few finite entries or an overflowing extent).</summary>
            internal static SpatialGrid? TryBuild(List<(Rectangle Bounds, T Item)> entries)
            {
                int total = entries.Count;
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                int finite = 0;
                for (int i = 0; i < total; i++)
                {
                    Rectangle b = entries[i].Bounds;
                    if (!IsFinite(b))
                        continue;

                    finite++;
                    if (b.Left < minX) minX = b.Left;
                    if (b.Bottom < minY) minY = b.Bottom;
                    if (b.Right > maxX) maxX = b.Right;
                    if (b.Top > maxY) maxY = b.Top;
                }

                if (finite == 0)
                    return null;

                double width = maxX - minX;
                double height = maxY - minY;
                if (!IsFinite(width) || !IsFinite(height))
                    return null;

                int columns, rows;
                if (width > 0 && height > 0)
                {
                    double cellSize = Math.Sqrt(width * height / finite);
                    columns = ClampDimension(Math.Ceiling(width / cellSize), finite);
                    rows = ClampDimension(Math.Ceiling(height / cellSize), finite);
                }
                else if (width > 0)
                {
                    columns = finite;
                    rows = 1;
                }
                else if (height > 0)
                {
                    columns = 1;
                    rows = finite;
                }
                else
                {
                    columns = 1;
                    rows = 1;
                }

                long cellCount = (long)columns * rows;
                if (cellCount > 4L * finite + 16)
                    return null;

                double invWidth = width > 0 ? columns / width : 0;
                double invHeight = height > 0 ? rows / height : 0;

                // Only the cell mapping of this instance is used while counting; the CSR arrays are filled below.`n
                SpatialGrid shape = new(minX, minY, invWidth, invHeight, columns, rows, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
                int[] firstColumn = new int[total];
                int[] firstRow = new int[total];
                int[] cellStart = new int[(int)cellCount + 1];
                List<int>? alwaysTest = null;

                // Pass 1: count entries per cell.
                for (int i = 0; i < total; i++)
                {
                    Rectangle b = entries[i].Bounds;
                    if (!shape.TryGetCellRange(b, out int x0, out int x1, out int y0, out int y1))
                    {
                        (alwaysTest ??= []).Add(i);
                        continue;
                    }

                    firstColumn[i] = x0;
                    firstRow[i] = y0;
                    for (int cy = y0; cy <= y1; cy++)
                        for (int cx = x0; cx <= x1; cx++)
                            cellStart[(cy * columns) + cx + 1]++;
                }

                for (int c = 0; c < cellCount; c++)
                    cellStart[c + 1] += cellStart[c];

                // Pass 2: fill cells. Entries are visited in list order, so each cell lists positions ascending.
                int[] cellItems = new int[cellStart[(int)cellCount]];
                int[] fill = new int[(int)cellCount];
                for (int i = 0; i < total; i++)
                {
                    Rectangle b = entries[i].Bounds;
                    if (!shape.TryGetCellRange(b, out int x0, out int x1, out int y0, out int y1))
                        continue;

                    for (int cy = y0; cy <= y1; cy++)
                    {
                        for (int cx = x0; cx <= x1; cx++)
                        {
                            int cell = (cy * columns) + cx;
                            cellItems[cellStart[cell] + fill[cell]++] = i;
                        }
                    }
                }

                return new SpatialGrid(minX, minY, invWidth, invHeight, columns, rows, cellStart, cellItems,
                    firstColumn, firstRow, alwaysTest is null ? Array.Empty<int>() : alwaysTest.ToArray());
            }

            private static int ClampDimension(double value, int max)
            {
                if (!(value >= 1))
                    return 1;
                if (!(value <= max))
                    return max;

                return (int)value;
            }

            /// <summary>Grid column of <paramref name="x"/>, clamped to the grid. Non-decreasing in x.</summary>
            private int Column(double x)
            {
                double t = (x - _minX) * _invCellWidth;
                if (!(t > 0))
                    return 0;
                if (t >= _columns)
                    return _columns - 1;

                return (int)t;
            }

            /// <summary>Grid row of <paramref name="y"/>, clamped to the grid. Non-decreasing in y.</summary>
            private int Row(double y)
            {
                double t = (y - _minY) * _invCellHeight;
                if (!(t > 0))
                    return 0;
                if (t >= _rows)
                    return _rows - 1;

                return (int)t;
            }

            /// <summary>False when the rectangle must be tested directly (non-finite or overlapping too many cells).</summary>
            private bool TryGetCellRange(in Rectangle r, out int x0, out int x1, out int y0, out int y1)
            {
                x0 = x1 = y0 = y1 = 0;
                if (!IsFinite(r))
                    return false;

                x0 = Column(r.Left);
                x1 = Column(r.Right);
                y0 = Row(r.Bottom);
                y1 = Row(r.Top);
                return (long)(x1 - x0 + 1) * (y1 - y0 + 1) <= MaxCellsPerEntry;
            }

            /// <summary>
            /// Sorted list positions of entries whose bounds intersect <paramref name="query"/>, or -1 when the
            /// caller should scan linearly instead.
            /// </summary>
            /// <remarks>
            /// Rectangle intersection is closed (touching counts). Entry and query ranges come from the same
            /// monotone cell mapping, so overlapping rectangles always share a cell. An entry listed in several cells
            /// is reported only in the lower-left cell of the overlap of its range and the query range.
            /// </remarks>
            internal int Query(List<(Rectangle Bounds, T Item)> entries, in Rectangle query, ref int[]? buffer)
            {
                if (!IsFinite(query))
                    return -1;

                int qx0 = Column(query.Left);
                int qx1 = Column(query.Right);
                int qy0 = Row(query.Bottom);
                int qy1 = Row(query.Top);

                long touched = (long)(qx1 - qx0 + 1) * (qy1 - qy0 + 1);
                if (touched * 4 > (long)_columns * _rows)
                    return -1;

                int count = 0;
                for (int cy = qy0; cy <= qy1; cy++)
                {
                    for (int cx = qx0; cx <= qx1; cx++)
                    {
                        int cell = (cy * _columns) + cx;
                        int end = _cellStart[cell + 1];
                        for (int k = _cellStart[cell]; k < end; k++)
                        {
                            int position = _cellItems[k];
                            if ((_firstColumn[position] > qx0 ? _firstColumn[position] : qx0) != cx ||
                                (_firstRow[position] > qy0 ? _firstRow[position] : qy0) != cy)
                                continue;

                            if (entries[position].Bounds.Intersects(query))
                                Append(ref buffer, ref count, position);
                        }
                    }
                }

                foreach (int position in _alwaysTest)
                {
                    if (entries[position].Bounds.Intersects(query))
                        Append(ref buffer, ref count, position);
                }

                if (count > 1)
                    Array.Sort(buffer!, 0, count);

                return count;
            }

            private static void Append(ref int[]? buffer, ref int count, int position)
            {
                if (buffer is null)
                    buffer = new int[16];
                else if (count == buffer.Length)
                    Array.Resize(ref buffer, count * 2);

                buffer[count++] = position;
            }
        }
    }

    internal static class BoundingBoxIndexExtensions
    {
        public static BoundingBoxIndex<LineSegment> ToBoundingBoxIndex(this IEnumerable<LineSegment> lines)
        {
            if (lines is null)
                throw new ArgumentNullException(nameof(lines));

            BoundingBoxIndex<LineSegment> index = new();
            foreach (LineSegment line in lines)
                index.Add(line.BoundingBox, line);
            return index;
        }
    }
}
