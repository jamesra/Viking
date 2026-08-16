using Geometry;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A collection of points/circles that notifies about changes
    /// </summary>
    public class PointSet : INotifyCollectionChanged, ICollection<Vector2>
    {
        public double PointRadius = 2.0;
        public List<Circle> Circles = [];

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public PointSet()
        {
        }

        public PointSet(IEnumerable<Vector2> input)
        {
            Circles.AddRange(input.Select(p => new Circle(p, PointRadius)));
        }

        public ICollection<Vector2> Points => [.. Circles.Select(c => c.Center)];

        public int Count => Points.Count;

        public bool IsReadOnly => Points.IsReadOnly;

        /// <summary>
        /// Add or remove a point from the list
        /// </summary>
        /// <param name="p"></param>
        public void Toggle(Vector2 p)
        {
            Circle newCircle = new(p, PointRadius);
            if (Circles.Any(c => c.Intersects(newCircle)))
            {
                Circle[] removedCircles = [.. Circles.Where(c => c.Intersects(newCircle))];
                Circles.RemoveAll(c => c.Intersects(newCircle));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedCircles));
            }
            else
            {
                Circles.Add(newCircle);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newCircle));
            }
        }

        public void Add(Vector2 item)
        {
            Circle newCircle = new(item, PointRadius);
            Circles.Add(newCircle);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newCircle));
        }

        public void AddRange(IEnumerable<Vector2> items)
        {
            IEnumerable<Circle> circles = items.Select(i => new Circle(i, PointRadius));
            Circles.AddRange(circles);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, circles));
        }

        public void Clear()
        {
            Points.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(Vector2 item) => Points.Contains(item);

        public void CopyTo(Vector2[] array, int arrayIndex) => Points.CopyTo(array, arrayIndex);

        public bool Remove(Vector2 item)
        {
            Circle[] remove = [.. Circles.Where(c => c.Contains(item))];
            bool nRemoved = Circles.RemoveAll(c => c.Contains(item)) > 0;
            if (CollectionChanged != null && remove.Length > 0)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, remove));
            }

            return nRemoved;
        }

        public IEnumerator<Vector2> GetEnumerator() => Points.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Points.GetEnumerator();
    }
}
