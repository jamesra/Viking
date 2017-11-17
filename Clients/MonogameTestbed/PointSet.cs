using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace MonogameTestbed
{ 
    class PointSet : INotifyCollectionChanged, ICollection<GridVector2>
    {
        public double PointRadius = 2.0;
        public List<GridCircle> Circles = new List<GridCircle>();

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public PointSet()
        {
        }

        public PointSet(IEnumerable<GridVector2> input)
        {
            Circles.AddRange(input.Select(p => new GridCircle(p, PointRadius)));
        }

        public ICollection<GridVector2> Points
        {
            get
            {
                return Circles.Select(c => c.Center).ToList();
            }
        }

        public int Count
        {
            get
            {
                return Points.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return Points.IsReadOnly;
            }
        }

        /// <summary>
        /// Add or remove a point from the list
        /// </summary>
        /// <param name="p"></param>
        public void Toggle(GridVector2 p)
        {
            GridCircle newCircle = new GridCircle(p, PointRadius);
            if (Circles.Any(c => c.Intersects(newCircle)))
            {
                GridCircle[] removedCircles = Circles.Where(c => c.Intersects(newCircle)).ToArray();
                Circles.RemoveAll(c => c.Intersects(newCircle));
                if(CollectionChanged != null)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedCircles));
                }
            }
            else
            {
                Circles.Add(newCircle);
                if (CollectionChanged != null)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newCircle));
                }
            }
        }

        public void Add(GridVector2 item)
        {
            GridCircle newCircle = new GridCircle(item, PointRadius);
            Circles.Add(newCircle);
        }

        public void Clear()
        {
            Points.Clear();
        }

        public bool Contains(GridVector2 item)
        {
            return Points.Contains(item);
        }

        public void CopyTo(GridVector2[] array, int arrayIndex)
        {
            Points.CopyTo(array, arrayIndex);
        }

        public bool Remove(GridVector2 item)
        {
            return Circles.RemoveAll(c => c.Contains(item)) > 0;
        }

        public IEnumerator<GridVector2> GetEnumerator()
        {
            return Points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Points.GetEnumerator();
        }
    }
}
