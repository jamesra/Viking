using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Unique 2D points with nearest-neighbor lookup. Used by polygon intersection vertex insertion
    /// without taking a QuadTree dependency in Core. A <see cref="UniquePointMap{T}"/> with no values.
    /// </summary>
    internal sealed class UniquePointSet
    {
        private readonly UniquePointMap<bool> _map = new();

        public int Count => _map.Count;

        public void Add(Vector2 point) => _map.Add(point, true);

        public bool TryAdd(Vector2 point) => _map.TryAdd(point, true);

        public bool Contains(Vector2 point) => _map.Contains(point);

        public bool TryFindNearest(Vector2 point, out Vector2 nearest, out double distance) =>
            _map.TryFindNearest(point, out nearest, out _, out distance);

        public bool TryRemove(Vector2 point, out Vector2 removed) => _map.TryRemove(point, out removed, out _);
    }

    /// <summary>
    /// Unique 2D points mapped to a value, with nearest-neighbor lookup and removal. Use it wherever points are kept
    /// unique by <see cref="Vector2.Equals(Vector2)"/> and searched by distance; a list scanned point by point makes
    /// such code quadratic.
    /// <para>
    /// Points are bucketed in square cells of <see cref="CellSize"/>, so <see cref="Contains"/>, <see cref="TryRemove"/>
    /// and <see cref="TryFindNearest"/> look only at nearby cells. A linear scan made curve fitting quadratic: a fitted
    /// segmentation outline has thousands of points, and fitting it on the UI thread stalled Viking for seconds.
    /// Equality is still <see cref="Vector2.Equals(Vector2)"/> (within <see cref="Tolerance.Epsilon"/>), and where
    /// several points qualify (equal within epsilon, or equally near) the one added first wins, so results match a
    /// linear scan of the points in the order they were added.
    /// </para>
    /// </summary>
    internal sealed class UniquePointMap<T>
    {
        /// <summary>Cell size, more than twice <see cref="Tolerance.Epsilon"/> so equal points are never more than one cell apart.</summary>
        internal const double CellSize = 1.0;

        private struct Entry
        {
            public Vector2 Point;
            public T Value;
            public bool Removed;
        }

        // Entries keep their index for life (removal only marks them), so a lower index always means added earlier.
        private readonly List<Entry> _entries = [];
        private readonly Dictionary<(long X, long Y), List<int>> _cells = [];
        private long _minX = long.MaxValue, _maxX = long.MinValue, _minY = long.MaxValue, _maxY = long.MinValue;

        public int Count { get; private set; }

        private static (long X, long Y) CellOf(in Vector2 point) =>
            ((long)Math.Floor(point.X / CellSize), (long)Math.Floor(point.Y / CellSize));

        public bool Contains(Vector2 point) => IndexOf(point) >= 0;

        public void Add(Vector2 point, T value)
        {
            if (!TryAdd(point, value))
                throw new ArgumentException($"Duplicate point {point}");
        }

        /// <summary>Adds the point unless an equal one is already present.</summary>
        public bool TryAdd(Vector2 point, T value)
        {
            if (Contains(point))
                return false;

            (long cx, long cy) = CellOf(point);
            if (!_cells.TryGetValue((cx, cy), out List<int> cell))
            {
                cell = [];
                _cells.Add((cx, cy), cell);
            }

            cell.Add(_entries.Count);
            _entries.Add(new Entry { Point = point, Value = value });
            Count++;
            _minX = Math.Min(_minX, cx);
            _maxX = Math.Max(_maxX, cx);
            _minY = Math.Min(_minY, cy);
            _maxY = Math.Max(_maxY, cy);
            return true;
        }

        /// <summary>Removes the earliest-added point equal to <paramref name="point"/>.</summary>
        public bool TryRemove(Vector2 point, out Vector2 removed, out T value)
        {
            int index = IndexOf(point);
            if (index < 0)
            {
                removed = default;
                value = default;
                return false;
            }

            Entry entry = _entries[index];
            List<int> cell = _cells[CellOf(entry.Point)];
            cell.Remove(index);
            if (cell.Count == 0)
                _cells.Remove(CellOf(entry.Point));

            removed = entry.Point;
            value = entry.Value;
            entry.Removed = true;
            entry.Value = default;
            _entries[index] = entry;
            Count--;
            return true;
        }

        /// <summary>Index of the earliest-added point equal to <paramref name="point"/>, or -1.</summary>
        private int IndexOf(in Vector2 point)
        {
            int best = -1;
            (long cx, long cy) = CellOf(point);
            for (long x = cx - 1; x <= cx + 1; x++)
            {
                for (long y = cy - 1; y <= cy + 1; y++)
                {
                    if (!_cells.TryGetValue((x, y), out List<int> cell))
                        continue;

                    foreach (int i in cell)
                    {
                        if ((best < 0 || i < best) && _entries[i].Point.Equals(point))
                            best = i;
                    }
                }
            }

            return best;
        }

        public bool TryFindNearest(Vector2 point, out Vector2 nearest, out T value, out double distance)
        {
            nearest = default;
            value = default;
            distance = double.PositiveInfinity;
            if (Count == 0)
                return false;

            // Search square rings of cells outward from the query's cell. Every point in ring r+1 or beyond is at least
            // r * CellSize away, so once the best distance is within that, no farther ring can hold a closer point.
            // Stop scanning rings when they cover more cells than there are points, and finish with a linear scan instead.
            (long cx, long cy) = CellOf(point);
            long maxRing = Math.Max(Math.Max(cx - _minX, _maxX - cx), Math.Max(cy - _minY, _maxY - cy));
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            long cellsVisited = 0;
            for (long r = 0; r <= maxRing; r++)
            {
                // Strict, so a point at exactly that distance in a farther ring can still win a tie by being added first.
                if (best >= 0 && bestDistance < (r - 1) * CellSize)
                    break;

                cellsVisited += r == 0 ? 1 : 8 * r;
                if (cellsVisited > 4L * Count + 64)
                {
                    best = -1;
                    bestDistance = double.PositiveInfinity;
                    break;
                }

                for (long x = cx - r; x <= cx + r; x++)
                {
                    bool edgeColumn = x == cx - r || x == cx + r;
                    for (long y = cy - r; y <= cy + r; y += edgeColumn || r == 0 ? 1 : 2 * r)
                    {
                        if (!_cells.TryGetValue((x, y), out List<int> cell))
                            continue;

                        foreach (int i in cell)
                        {
                            double d = Vector2.Distance(point, _entries[i].Point);
                            if (d < bestDistance || (d == bestDistance && i < best))
                            {
                                bestDistance = d;
                                best = i;
                            }
                        }
                    }
                }
            }

            if (best < 0)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Removed)
                        continue;

                    double d = Vector2.Distance(point, _entries[i].Point);
                    if (best < 0 || d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
            }

            nearest = _entries[best].Point;
            value = _entries[best].Value;
            distance = bestDistance;
            return true;
        }
    }
}
