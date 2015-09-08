using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using Geometry;

namespace Geometry
{
    /// <summary>
    /// Stores a quadtree.  Should be safe for concurrent access
    /// </summary>
     public class QuadTree<T>  : IDisposable
    {
        //GridVector2[] _points;
        QuadTreeNode<T> Root;

        public GridRectangle Border
        {
            get { return Root.Border; }
        }
        

        ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(); 

        /// <summary>
        /// Maps the values to the node containing the values. Populated by the QuadTreeNode class.
        /// </summary>
        internal Dictionary<T, QuadTreeNode<T>> ValueToNodeTable = new Dictionary<T, QuadTreeNode<T>>(); 


        public QuadTree(GridRectangle border)
        {
            //Create a root centered at 0,0
            this.Root = new QuadTreeNode<T>(this, border);
        }

        public QuadTree(GridVector2[] points, T[] values)
        {
            CreateTree(points, values, GridVector2.Border(points));
        }

        public QuadTree(GridVector2[] keys, T[] values, GridRectangle border)
        {
            CreateTree(keys, values,  border); 
        }

        public T[] Values
        {
            get 
            {
                T[] values = new T[0];
                try
                {
                    rwLock.EnterReadLock();
                    values = new T[ValueToNodeTable.Count];
                    ValueToNodeTable.Keys.CopyTo(values, 0); 
                    return values;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        public GridVector2 this [T value] 
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
        /// Insert a new point within the borders into the tree
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public void Add(GridVector2 point, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                if (Root.Border.Contains(point) == false)
                {
                    throw new ArgumentOutOfRangeException("point", "The passed point for insertion was out of range of the QuadTree");
                }

                try
                {
                    rwLock.EnterWriteLock();
                    this.Root.Insert(point, value);
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

        /// <summary>
        /// Insert a new point within the borders into the tree
        /// </summary>
        /// <param name="point"></param>
        /// <param name="value"></param>
        public bool TryAdd(GridVector2 point, T value)
        {
            try
            {
                rwLock.EnterUpgradeableReadLock();

                if (Root.Border.Contains(point) == false)
                {
                    return false;
                }

                //Do not add the value if we already have it in our data structure
                if (ValueToNodeTable.ContainsKey(value))
                    return false; 

                try
                {
                    rwLock.EnterWriteLock();

                    this.Root.Insert(point, value);
                    return true;
                }
                catch (Exception)
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

        public bool Contains(T value)
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

                if (Root.Border.Contains(point) == false)
                {
                    return false;
                }
                
                //Remove the value if it exists and is not equal to the passed point.
                if (ValueToNodeTable.ContainsKey(value))
                {
                    QuadTreeNode<T> node = ValueToNodeTable[value];
                    if (GridVector2.Distance(node.Point, point) == 0)
                        return false;
                    else
                    {
                        //Update the position
                        try
                        {
                            rwLock.EnterWriteLock();

                            Remove(value);
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

                        QuadTreeNode<T> node = this.Root.Insert(point, value);
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

            if (node.Parent != null)
                node.Parent.Remove(node);
            else
            {
                //We are removing the root node.  State that it has no value and return
                ValueToNodeTable.Remove(node.Value);
                node.HasValue = false;
            }

            node.Parent = null;
            node.Value = default(T);
            node.Tree = null; 

            return retVal;
        }

        public bool TryRemove(T value, out T RemovedValue)
        {
            RemovedValue = default(T); 
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
                catch (Exception )
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

            return true; 
        }

        private void CreateTree(GridVector2[] keys, T[] values, GridRectangle border)
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

        public T FindNearest(GridVector2 point, out double distance)
        { 
            try
            {
                rwLock.EnterReadLock(); 

                GridVector2 nodePoint;
                distance = double.MaxValue;
                if (Root == null)
                {
                    return default(T); 
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return default(T); 
                }

                return Root.FindNearest(point, out nodePoint, ref distance);
            }
            finally
            {
                rwLock.ExitReadLock(); 
            }
        }

        public SortedList<double, T> FindNearestPoints(GridVector2 point, int nPoints)
        {
            SortedList<double, T> pointList = new SortedList<double, T>(nPoints + 1);

            try
            {
                rwLock.EnterReadLock();

                if (Root == null)
                {
                    return pointList;
                }
                else if (Root.IsLeaf == true && Root.HasValue == false)
                {
                    return pointList;
                }

                Root.FindNearestPoints(point, nPoints, ref pointList);
            }
            finally
            {
                rwLock.ExitReadLock();
            }

            return pointList; 
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
        public void Intersect(GridRectangle gridRect, out List<GridVector2> outPoints, out List<T> outValues)
        {
            try
            {
                rwLock.EnterReadLock();

                outPoints = new List<GridVector2>(this.ValueToNodeTable.Count);
                outValues = new List<T>(this.ValueToNodeTable.Count);

                this.Root.Intersect(gridRect, true, ref outPoints, ref outValues);
            }
            finally
            {
                rwLock.ExitReadLock(); 
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
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
