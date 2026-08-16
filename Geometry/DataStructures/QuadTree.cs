using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace Geometry
{
    public class QuadTreeNodeEnumerator<T> : IEnumerator<QuadTreeNode<T>>
    {
        private readonly QuadTreeNode<T> _root;

        private Quadrant CurrentQuad = (Quadrant)0;
        QuadTreeNodeEnumerator<T> QuadEnumerator = null;

        internal QuadTreeNodeEnumerator(QuadTreeNode<T> root)
        {
            _root = root;
        }

        QuadTreeNode<T> Current = null;

        public void Dispose()
        {
            return;
        }

        public bool MoveNext()
        {
            if (Current is null)
            {
                if (_root.IsRoot)
                {
                    Current = _root;
                    return true;
                }
            }
            else if (QuadEnumerator is not null && QuadEnumerator.MoveNext())
            {
                Current = QuadEnumerator.Current;
            }
            else //Enumerate over each quadrant
            {
                //Time to iterate through quadrants
                while ((int)CurrentQuad < 4)
                {
                    if (_root[CurrentQuad] is null)
                    {
                        CurrentQuad += 1;
                        continue;
                    }
                    else
                    {
                        QuadEnumerator = new QuadTreeNodeEnumerator<T>(_root[CurrentQuad]);
                        if (QuadEnumerator.MoveNext())
                        {
                            Current = QuadEnumerator.Current;
                            return true;
                        }
                        else
                            continue; //Nothing to enumerate, move on to next quad
                    }
                }
            }

            return false;
        }

        public void Reset() => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        QuadTreeNode<T> IEnumerator<QuadTreeNode<T>>.Current => Current;
    }

    public class QuadTree<T> : IDisposable
    {
        protected QuadTreeNode<T> Root;
        protected readonly ReaderWriterLockSlim rwLock = new(LockRecursionPolicy.SupportsRecursion);

        public QuadTree()
        {
            //Create a root centered at 0,0
            this.Root = new QuadTreeNode<T>(this);
        }

        public QuadTree(Rectangle border)
        {
            //Create a root centered at 0,0
            this.Root = new QuadTreeNode<T>(this, border);
        }

        public QuadTree(Vector2[] points, T[] values)
        {
            CreateTree(points, values, points.BoundingBox());
        }

        public QuadTree(Vector2[] keys, T[] values, in Rectangle border)
        {
            CreateTree(keys, values, in border);
        }

        /// <summary>
        /// Used by QuadTreeWithUniqueValues when a duplicate point is added
        /// </summary>
        internal class DuplicatePointException : ArgumentException
        {
            public DuplicatePointException()
            {
            }

            public DuplicatePointException(Vector2 point) : base("The point being inserted into the quad treeWithUniqueValues is a duplicate point: " + point.ToString())
            {
            }

            public DuplicatePointException(string message) : base(message)
            {
            }

            public DuplicatePointException(string message, Exception innerException) : base(message, innerException)
            {
            }

            public DuplicatePointException(string message, string paramName) : base(message, paramName)
            {
            }

            public DuplicatePointException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
            {
            }

            protected DuplicatePointException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        public Rectangle Border => Root.Border;
        public IEnumerable<Vector2> Keys => Root?.Keys ?? Array.Empty<Vector2>();

        /*
        public T[] Values
        {
            get
            { 
                try
                {
                    rwLock.EnterReadLock();
                    var values = new T[ValueToNodeTable.Count];
                    ValueToNodeTable.Keys.CopyTo(values, 0);
                    return values;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }
        */

        public virtual int Count { get; protected set; }


        internal virtual void PointAdded(QuadTreeNode<T> node, Vector2 point, T value) => Count++;

        internal virtual void PointRemoved(QuadTreeNode<T> node, Vector2 point, T value) => Count--;

        /// <summary>
        /// Returns the value nearest to the point p
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public T this[Vector2 p]
        {
            get
            {

                if (false == TryFindNearest(p, out var foundPoint, out T val, out double distance) ||
                    distance > Global.Epsilon)
                    throw new KeyNotFoundException(
                        $"{p} does not have an exact match in the quad treeWithUniqueValues.  Use of the index operator requires an exact match be present.");

                return val;
            }
            set
            {
                try
                {
                    rwLock.EnterWriteLock();

                    SetValueAtPoint(p, value);
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Insert a new point within the borders into the treeWithUniqueValues
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public virtual void Add(Vector2 point, T value)
        {
            /*
            try
            {
                rwLock.EnterUpgradeableReadLock();
                */
            /*
            if (Root.Border.Contains(point) == false)
            {
                throw new ArgumentOutOfRangeException("point", "The passed point for insertion was out of range of the QuadTreeWithUniqueValues");
            }
            */
            try
            {
                rwLock.EnterWriteLock();

                if (this.Root.ExpandBorder(in point, out var new_root))
                {
                    this.Root = new_root;
                }

                this.Root.Insert(point, value);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            /*}
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
            */
        }

        /// <summary>
        /// Insert a new point within the borders into the treeWithUniqueValues
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public virtual bool TryAdd(Vector2 point, in T value)
        {
            try
            {
                rwLock.EnterWriteLock();

                if (this.Root.ExpandBorder(in point, out var new_root))
                {
                    this.Root = new_root;
                }

                this.Root.Insert(point, value);
                return true;
            }
            catch (DuplicatePointException)
            {
                return false;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        protected virtual void SetValueAtPoint(Vector2 p, T value) => Root.Update(p, value);

        public virtual void Update(Vector2 p, T value)
        {
            try
            {
                rwLock.EnterWriteLock();

                SetValueAtPoint(p, value);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool TryUpdate(Vector2 p, T value)
        {
            try
            {
                rwLock.EnterWriteLock();

                return Root.TryUpdate(p, value);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool Contains(Vector2 p)
        {
            if (this.TryFindNearest(p, out Vector2 foundPoint, out var val, out double distance))
                return foundPoint.Equals(p);

            return false;
        }

        public bool ContainsKey(Vector2 p) => Contains(p);

        public bool TryRemove(Vector2 point, out T RemovedValue)
        {
            RemovedValue = default;

            if (Root is null)
                return false;

            try
            {
                rwLock.EnterWriteLock();
                Root.Remove(point, out RemovedValue);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        /*
        /// <summary>
        /// Updates the position of the passed value with the new value
        /// Creates the node if it does not exist
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        /// <returns>True if position was updated</returns>
        public bool TryAddUpdatePosition(Vector2 point, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock(); 
                //Remove the value if it exists and is not equal to the passed point.
                if (ValueToNodeTable.TryGetValue(value, out var node))
                {
                    //If we are updating the same point, do nothing (Check for new value though?)
                    if (Vector2.Distance(in node.Point, in point) == 0)
                        return false;
                    else
                    {
                        //Update the position
                        try
                        {
                            rwLock.EnterWriteLock();

                            Remove(point);

                            if (this.Root.ExpandBorder(in point, out var new_root))
                            {
                                this.Root = new_root;
                            }

                            this.Root.Insert(point, value);
                            return true;
                        }
                        finally
                        {
                            rwLock.ExitWriteLock();
                        }
                    }
                }
                else
                {
                    try
                    {
                        rwLock.EnterWriteLock();

                        if (this.Root.ExpandBorder(in point, out var new_root))
                        {
                            this.Root = new_root;
                        }

                        QuadTreeNode<T> new_node = this.Root.Insert(point, value);
                        return true;
                    }
                    finally
                    {
                        rwLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }
        */

        private void CreateTree(Vector2[] keys, T[] values, in Rectangle border)
        {
            try
            {
                rwLock.EnterWriteLock();

                //Create a node centered in the border
                //this.Root = new QuadTreeNode<T>(this, new Rectangle(double.MinValue, double.MaxValue, double.MinValue, double.MaxValue));
                this.Root = new QuadTreeNode<T>(this, border);

                for (int iPoint = 0; iPoint < keys.Length; iPoint++)
                {
                    this.Root.Insert(keys[iPoint], values[iPoint]);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool TryGetValue(Vector2 p, out T result)
        {
            var found = TryFindNearest(p, out var foundPoint, out result, out double distance);
            if (found)
                return distance <= Global.Epsilon;

            return false;
        }

        public bool TryFindNearest(Vector2 point, out T val, out double distance) => TryFindNearest(point, out Vector2 found_point, out val, out distance);

        public bool TryFindNearest(Vector2 point, out Vector2 foundPoint, out T val, out double distance)
        {
            val = default;
            try
            {
                foundPoint = Vector2.Zero;
                distance = double.MaxValue;

                rwLock.EnterReadLock();

                if (Root is null)
                {
                    return false;
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return false;
                }

                val = Root.FindNearest(point, out foundPoint, ref distance);
                return true;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public List<DistanceToPoint<T>> FindNearestPoints(Vector2 point, int nPoints)
        {
            List<DistanceToPoint<T>> listResults = null;

            if (nPoints < 0)
            {
                throw new ArgumentException("Attempting to find a negative number of points");
            }

            try
            {
                rwLock.EnterReadLock();

                if (Root is null)
                {
                    return [];
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return [];
                }

                //SortedList<double, List<DistanceToPoint<T>>> pointList = new SortedList<double, List<DistanceToPoint<T>>>(nPoints + 1);
                FixedSizeDistanceList<T> pointList = new(nPoints + 1);
                Root.FindNearestPoints(point, nPoints, ref pointList);

                listResults = [];
                foreach (double distance in pointList.Data.Keys)
                {
                    listResults.AddRange(pointList[distance]);
                    if (listResults.Count >= nPoints)
                        break; //Stop adding after we pass nPoints because the implementation of FindNearestPoints is unreliable after it reaches the requested number.
                }


            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return listResults;
        }

        /// <summary>
        /// Return all points and values in the quadtree which fall inside the rectangle. Indices correspond
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public void Intersect(in Rectangle gridRect, out List<Vector2> outPoints, out List<T> outValues)
        {
            try
            {
                rwLock.EnterReadLock();

                outPoints = new List<Vector2>(this.Count);
                outValues = new List<T>(this.Count);

                this.Root.Intersect(in gridRect, true, ref outPoints, ref outValues);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                rwLock?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Stores a quadtree.  Should be safe for concurrent access.  In addition to each point being unique, each value associated with a point must also be unique.  This allows reverse lookup of points by value.
    /// </summary>
    public class QuadTreeWithUniqueValues<T> : QuadTree<T> //, IDictionary<Vector2,T>
    {
        /// <summary>
        /// Used by QuadTreeWithUniqueValues when a duplicate value (two points with the same value) is added
        /// </summary>
        internal class DuplicateValueException : ArgumentException
        {
            public DuplicateValueException()
            {
            }

            public DuplicateValueException(Vector2 point, object value) : base("Value {value}, associated with the point {point}, being inserted into the quad treeWithUniqueValues is a duplicate value")
            {
            }

            public DuplicateValueException(string message) : base(message)
            {
            }

            public DuplicateValueException(string message, Exception innerException) : base(message, innerException)
            {
            }

            public DuplicateValueException(string message, string paramName) : base(message, paramName)
            {
            }

            public DuplicateValueException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
            {
            }

            protected DuplicateValueException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        //Vector2[] _points;

        /// <summary>
        /// Maps the values to the node containing the values. Populated by the QuadTreeNode class.
        /// </summary>
        protected readonly Dictionary<T, QuadTreeNode<T>> ValueToNodeTable = [];


        public QuadTreeWithUniqueValues() : base()
        {
        }


        public QuadTreeWithUniqueValues(Rectangle border) : base(border)
        {
        }

        public QuadTreeWithUniqueValues(Vector2[] points, T[] values) : base(points, values)
        {
        }

        public QuadTreeWithUniqueValues(Vector2[] keys, T[] values, in Rectangle border) : base(keys, values, in border)
        {
        }

        internal override void PointAdded(QuadTreeNode<T> node, Vector2 point, T value)
        {
            if (ValueToNodeTable.ContainsKey(value))
                throw new QuadTreeWithUniqueValues<T>.DuplicateValueException(point, value);

            ValueToNodeTable.Add(value, node);
            base.PointAdded(node, point, value);
        }

        internal override void PointRemoved(QuadTreeNode<T> node, Vector2 point, T value)
        {
            bool success = ValueToNodeTable.Remove(value);
#if DEBUG
            if(!success)
                Trace.WriteLine("Could not remove {point}:{value} from ValueToNodeTable, sign of a problem?");
#endif
            base.PointRemoved(node, point, value);
        }

        public override void Update(Vector2 p, T value)
        {
            try
            {
                rwLock.EnterWriteLock();

                SetValueAtPoint(p, value);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        protected override void SetValueAtPoint(Vector2 p, T value)
        {
            if (!TryFindNearest(p, out Vector2 foundPoint, out T oldValue, out double distance) ||
                distance > Global.Epsilon ||
                !foundPoint.Equals(p))
            {
                Root.Update(p, value);
                return;
            }

            if (EqualityComparer<T>.Default.Equals(oldValue, value))
                return;

            if (!ValueToNodeTable.TryGetValue(oldValue, out QuadTreeNode<T> node))
            {
                Root.Update(p, value);
                return;
            }

            ValueToNodeTable.Remove(oldValue);
            if (ValueToNodeTable.ContainsKey(value))
                throw new DuplicateValueException(p, value);

            node.Value = value;
            ValueToNodeTable.Add(value, node);
        }

        //ICollection<Vector2> IDictionary<Vector2, T>.Keys => Keys.ToArray();

        //ICollection<T> IDictionary<Vector2, T>.Values => Values;

        /// <summary>
        /// Returns the point associated with the value T
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public Vector2 this[T value]
        {
            get
            {
                try
                {
                    rwLock.EnterReadLock();
                    QuadTreeNode<T> node = ValueToNodeTable[value];
                    return node.Point;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        public bool Contains(in T value)
        {
            try
            {
                rwLock.EnterReadLock();

                return ValueToNodeTable.ContainsKey(value);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public T[] Values
        {
            get
            {
                try
                {
                    rwLock.EnterReadLock();
                    T[] values = new T[ValueToNodeTable.Count];
                    ValueToNodeTable.Keys.CopyTo(values, 0);
                    return values;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Insert a new point within the borders into the treeWithUniqueValues
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public override void Add(Vector2 point, T value)
        {
            /*
            try
            {
                rwLock.EnterUpgradeableReadLock();
                */
            /*
            if (Root.Border.Contains(point) == false)
            {
                throw new ArgumentOutOfRangeException("point", "The passed point for insertion was out of range of the QuadTreeWithUniqueValues");
            }
            */
            try
            {
                rwLock.EnterWriteLock();

                if (ValueToNodeTable.ContainsKey(value))
                    throw new QuadTreeWithUniqueValues<T>.DuplicateValueException(point, value);

                base.Add(point, value);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            /*}
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
            */
        }

        /// <summary>
        /// Insert a new point within the borders into the treeWithUniqueValues
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public override bool TryAdd(Vector2 point, in T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                //Do not add the value if we already have the value in our data structure
                if (ValueToNodeTable.ContainsKey(value))
                    return false;

                return base.TryAdd(point, value);
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }

        }


        /// <summary>
        /// This is the internal remove function.
        /// CALLER MUST TAKE THE WRITE LOCK BEFORE CALLING THIS FUNCTION
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private T Remove(T value)
        {
            QuadTreeNode<T> node = ValueToNodeTable[value];

            T retVal = node.Value;

            if (node.IsRoot == false)
            {
                node.Parent.Remove(node);
            }
            else
            {
                //We are removing the root node.  State that it has no value and return
                //ValueToNodeTable.Remove(node.Value);
                Debug.Assert(node.Value.Equals(value));
                PointRemoved(node, node.Point, node.Value);
                node.HasValue = false;
            }

            node.Parent = null;
            node.Value = default;

            return retVal;
        }

        /*
        /// <summary>
        /// This is the internal remove function.
        /// CALLER MUST TAKE THE WRITE LOCK BEFORE CALLING THIS FUNCTION
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private T Remove(Vector2 toRemove)
        { 
            QuadTreeNode<T> node = ValueToNodeTable[toRemove];

            T retVal = node.Value;

            if (node.IsRoot == false)
            {
                node.Parent.Remove(node);
            }
            else
            {
                //We are removing the root node.  State that it has no value and return
                ValueToNodeTable.Remove(node.Value);
                node.HasValue = false;
            }

            node.Parent = null;
            node.Value = default;

            return retVal;
        }
        */

        public bool TryRemove(T value, out T RemovedValue)
        {
            RemovedValue = default;
            try
            {
                rwLock.EnterUpgradeableReadLock();

                if (ValueToNodeTable.ContainsKey(value) == false)
                    return false;

                try
                {
                    rwLock.EnterWriteLock();

                    RemovedValue = Remove(value);
                }
                catch (Exception)
                {
                    throw;
                    //return false;
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

        public bool TryGetPosition(T value, out Vector2 position)
        {
            try
            {
                rwLock.EnterReadLock();

                if (ValueToNodeTable.TryGetValue(value, out QuadTreeNode<T> node) == false)
                {
                    position = new Vector2();
                    return false;
                    //throw new ArgumentException("Quadtree does not contains requested value");
                }

                position = node.Point;
                return true;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }
    }
}
