using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Linear AABB index used by Core polygon/polyline queries. Replaces the RTree dependency
    /// for primitives; repo Geometry keeps RTree for transforms and large spatial indexes.
    /// </summary>
    internal sealed class BoundingBoxIndex<T> where T : IEquatable<T>
    {
        private readonly List<(Rectangle Bounds, T Item)> _entries = [];
        private readonly Dictionary<T, int> _indexByItem;

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

        public void Add(Rectangle bounds, T item)
        {
            if (_indexByItem.ContainsKey(item))
                throw new ArgumentException($"{item} is already in the index");

            _indexByItem[item] = _entries.Count;
            _entries.Add((bounds, item));
        }

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
            return true;
        }

        public List<T> Intersects(Rectangle query)
        {
            List<T> hits = [];
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Bounds.Intersects(query))
                    hits.Add(_entries[i].Item);
            }

            return hits;
        }

        public IEnumerable<T> IntersectionGenerator(Rectangle query)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Bounds.Intersects(query))
                    yield return _entries[i].Item;
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
