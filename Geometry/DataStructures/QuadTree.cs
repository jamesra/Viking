using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace Geometry
{


    /// <summary>
    /// Stores a quadtree.  Should be safe for concurrent access
    /// </summary>
    public class QuadTree<T> : IDisposable //, IDictionary<GridVector2,T>
    {
        /// <summary>
        /// Used by QuadTree when a duplicate point is added
        /// </summary>
        internal class DuplicateItemException : ArgumentException
        {
            public DuplicateItemException()
            {
            }

            public DuplicateItemException(GridVector2 point) : base("The point being inserted into the quad tree is a duplicate point: " + point.ToString())
            {
            }

            public DuplicateItemException(string message) : base(message)
            {
            }

            public DuplicateItemException(string message, Exception innerException) : base(message, innerException)
            {
            }

            public DuplicateItemException(string message, string paramName) : base(message, paramName)
            {
            }

            public DuplicateItemException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
            {
            }

            protected DuplicateItemException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        /// <summary>
        /// Used by QuadTree when a duplicate value (two points with the same value) is added
        /// </summary>
        internal class DuplicateValueException : ArgumentException
        {
            public DuplicateValueException()
            {
            }

            public DuplicateValueException(GridVector2 point, object value) : base("Value {value}, associated with the point {point}, being inserted into the quad tree is a duplicate value")
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

        //GridVector2[] _points;
        QuadTreeNode<T> Root;

        public GridRectangle Border => Root.Border;


        readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Maps the values to the node containing the values. Populated by the QuadTreeNode class.
        /// </summary>
        internal readonly Dictionary<T, QuadTreeNode<T>> ValueToNodeTable = new Dictionary<T, QuadTreeNode<T>>();

        public QuadTree()
        {
            //Create a root centered at 0,0
            this.Root = new QuadTreeNode<T>(this);
        }


        public QuadTree(GridRectangle border)
        {
            //Create a root centered at 0,0
            this.Root = new QuadTreeNode<T>(this, border);
        }

        public QuadTree(GridVector2[] points, T[] values)
        {
            CreateTree(points, values, points.BoundingBox());
        }

        public QuadTree(GridVector2[] keys, T[] values, in GridRectangle border)
        {
            CreateTree(keys, values, in border);
        }

        public IEnumerable<GridVector2> Keys => Root?.Keys ?? Array.Empty<GridVector2>();

        //ICollection<GridVector2> IDictionary<GridVector2, T>.Keys => Keys.ToArray();
         
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

        //ICollection<T> IDictionary<GridVector2, T>.Values => Values;

        /// <summary>
        /// Returns the point associated with the value T
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public GridVector2 this[T value]
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

        /// <summary>
        /// Returns the value nearest to the point p
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public T this[GridVector2 p]
        {
            get
            {
                
                if (false == TryFindNearest(p, out var foundPoint, out T val, out double distance) ||
                    distance > Global.Epsilon)
                    throw new KeyNotFoundException(
                        $"{p} does not have an exact match in the quad tree.  Use of the index operator requires an exact match be present.");

                return val;
            }
            set => Add(p, value);
        }

        public int Count
        {
            get
            {
                try
                {
                    rwLock.EnterReadLock();
                    return ValueToNodeTable.Count;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Insert a new point within the borders into the tree
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public void Add(GridVector2 point, T value)
        {
            /*
            try
            {
                rwLock.EnterUpgradeableReadLock();
                */
            /*
            if (Root.Border.Contains(point) == false)
            {
                throw new ArgumentOutOfRangeException("point", "The passed point for insertion was out of range of the QuadTree");
            }
            */
            try
            {
                rwLock.EnterWriteLock();

                if (ValueToNodeTable.ContainsKey(value))
                    throw new QuadTree<T>.DuplicateValueException(point, value);
                
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
        /// Insert a new point within the borders into the tree
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public bool TryAdd(GridVector2 point, in T value)
        {

            try
            {
                rwLock.EnterUpgradeableReadLock();

                /*if (Root.Border.Contains(point) == false)
                {
                    return false;
                }
                */
                //Do not add the value if we already have it in our data structure
                if (ValueToNodeTable.ContainsKey(value))
                    return false;

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
                catch (DuplicateItemException)
                {
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

        public bool Contains(GridVector2 p)
        {
            if(this.TryFindNearest(p, out GridVector2 foundPoint, out var val, out double distance))
                return foundPoint.Equals(p);

            return false;
        }

        public bool ContainsKey(GridVector2 p)
        {
            return Contains(p);
        }

        

        /// <summary>
        /// Updates the position of the passed value with the new value
        /// Creates the node if it does not exist
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        /// <returns>True if position was updated</returns>
        public bool TryAddUpdatePosition(GridVector2 point, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();
                /*
                if (Root.Border.Contains(point) == false)
                {
                    return false;
                }
                */
                //Remove the value if it exists and is not equal to the passed point.
                if (ValueToNodeTable.TryGetValue(value, out var node))
                {
                    if (GridVector2.Distance(in node.Point, in point) == 0)
                        return false;
                    else
                    {
                        //Update the position
                        try
                        {
                            rwLock.EnterWriteLock();

                            Remove(value);

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
                ValueToNodeTable.Remove(node.Value);
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
        private T Remove(GridVector2 toRemove)
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
                {
                    return false;
                }

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

        /*
        public bool TryRemove(GridVector2 point, out T RemovedValue)
        { 
            RemovedValue = default;
            try
            {
                double distance = double.MaxValue;
                rwLock.EnterUpgradeableReadLock();

                if (Root == null)
                {
                    return false;
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return false;
                }

                var foundValue = Root.FindNearest(point, out var foundPoint, ref distance);

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
        */

        private void CreateTree(GridVector2[] keys, T[] values, in GridRectangle border)
        {
            try
            {
                rwLock.EnterWriteLock();

                //Create a node centered in the border
                //this.Root = new QuadTreeNode<T>(this, new GridRectangle(double.MinValue, double.MaxValue, double.MinValue, double.MaxValue));
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

        public bool TryGetValue(GridVector2 p, out T result)
        {
            try
            {
                result = default;
                rwLock.EnterReadLock(); 
                var found = TryFindNearest(p, out var foundPoint, out result, out double distance);
                if(found)
                    return distance <= Global.Epsilon;

                return false;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public bool TryFindNearest(GridVector2 point, out T val, out double distance)
        {
            return TryFindNearest(point, out GridVector2 found_point, out val, out distance);
        }

        public bool TryFindNearest(GridVector2 point, out GridVector2 foundPoint, out T val, out double distance)
        {
            val = default;
            try
            {
                foundPoint = GridVector2.Zero;
                distance = double.MaxValue;

                rwLock.EnterReadLock();

                if (Root == null)
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

        public List<DistanceToPoint<T>> FindNearestPoints(GridVector2 point, int nPoints)
        {
            List<DistanceToPoint<T>> listResults = null;

            if (nPoints < 0)
            {
                throw new ArgumentException("Attempting to find a negative number of points");
            }

            try
            {
                rwLock.EnterReadLock();

                if (Root == null)
                {
                    return new List<DistanceToPoint<T>>();
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return new List<DistanceToPoint<T>>();
                }

                //SortedList<double, List<DistanceToPoint<T>>> pointList = new SortedList<double, List<DistanceToPoint<T>>>(nPoints + 1);
                FixedSizeDistanceList<T> pointList = new FixedSizeDistanceList<T>(nPoints + 1);
                Root.FindNearestPoints(point, nPoints, ref pointList);

                listResults = new List<DistanceToPoint<T>>();
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

        public bool TryGetPosition(T value, out GridVector2 position)
        {
            try
            {
                rwLock.EnterReadLock();

                if (ValueToNodeTable.ContainsKey(value) == false)
                {
                    position = new GridVector2();
                    return false;
                    //throw new ArgumentException("Quadtree does not contains requested value");
                }

                QuadTreeNode<T> node = ValueToNodeTable[value];

                position = node.Point;
                return true;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Return all points and values in the quadtree which fall inside the rectangle. Indicies correspond
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public void Intersect(in GridRectangle gridRect, out List<GridVector2> outPoints, out List<T> outValues)
        {
            try
            {
                rwLock.EnterReadLock();

                outPoints = new List<GridVector2>(this.ValueToNodeTable.Count);
                outValues = new List<T>(this.ValueToNodeTable.Count);

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
}
