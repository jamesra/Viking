using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Unique 2D points with nearest-neighbor lookup. Used by polygon intersection vertex insertion
    /// without taking a QuadTree dependency in Core.
    /// </summary>
    internal sealed class UniquePointSet
    {
        private readonly List<Vector2> _points = [];

        public void Add(Vector2 point)
        {
            if (!TryAdd(point))
                throw new ArgumentException($"Duplicate point {point}");
        }

        public bool TryAdd(Vector2 point)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i].Equals(point))
                    return false;
            }

            _points.Add(point);
            return true;
        }

        public bool TryFindNearest(Vector2 point, out Vector2 nearest, out double distance)
        {
            nearest = default;
            distance = double.PositiveInfinity;
            if (_points.Count == 0)
                return false;

            for (int i = 0; i < _points.Count; i++)
            {
                double d = Vector2.Distance(point, _points[i]);
                if (d < distance)
                {
                    distance = d;
                    nearest = _points[i];
                }
            }

            return true;
        }

        public bool TryRemove(Vector2 point, out Vector2 removed)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i].Equals(point))
                {
                    removed = _points[i];
                    _points.RemoveAt(i);
                    return true;
                }
            }

            removed = default;
            return false;
        }
    }

    /// <summary>
    /// Unique 2D points mapped to a value, with nearest-neighbor lookup.
    /// </summary>
    internal sealed class UniquePointMap<T>
    {
        private readonly List<(Vector2 Point, T Value)> _entries = [];

        public bool Contains(Vector2 point)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Point.Equals(point))
                    return true;
            }

            return false;
        }

        public void Add(Vector2 point, T value)
        {
            if (Contains(point))
                throw new ArgumentException($"Duplicate point {point}");
            _entries.Add((point, value));
        }

        public bool TryFindNearest(Vector2 point, out Vector2 nearest, out T value, out double distance)
        {
            nearest = default;
            value = default;
            distance = double.PositiveInfinity;
            if (_entries.Count == 0)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                double d = Vector2.Distance(point, _entries[i].Point);
                if (d < distance)
                {
                    distance = d;
                    nearest = _entries[i].Point;
                    value = _entries[i].Value;
                }
            }

            return true;
        }
    }
}
