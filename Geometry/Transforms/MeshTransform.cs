using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    /// <summary>
    /// This is the parent class of all transforms.  All transforms, even continuos, are implemented by placing a grid 
    /// of control points on the mapped and control section.  
    /// </summary>
    [Serializable]
    public class MeshTransform : TriangulationTransform, ICloneable, ISerializable
    {
        #region Edges

            protected ReaderWriterLockSlim rwLockEdges = new ReaderWriterLockSlim();
            private PairedLineSearchGrid _LineSegmentGrid;

            private List<int>[] _edges; 
            public override List<int>[] Edges
            {
                get
                {
                    //Try read lock first since only one thread can be in upgradeable mode
                    try
                    {
                        rwLockEdges.EnterReadLock();
                        if (_edges != null)
                            return _edges;
                    }
                    finally
                    {
                        if (rwLockEdges.IsReadLockHeld)
                            rwLockEdges.ExitReadLock();
                    }

                    //Get in line to populate _edges
                    //CaclulateEdges will take a write lock
                    return CalculateEdges();
                }
                protected set
                {
                    try
                    {
                        rwLockEdges.EnterWriteLock();

                        _edges = value;
                    }
                    finally
                    {
                        if (rwLockEdges.IsWriteLockHeld)
                            rwLockEdges.ExitWriteLock();
                    }


                }
            }
        
        #endregion
   
        public MeshTransform(MappingGridVector2[] points, TransformInfo info) : base(points, info) 
        {
            
        }

        protected MeshTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            this._edges = info.GetValue("_Edges", typeof(List<int>[])) as List<int>[]; 
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            info.AddValue("_Edges", _edges);

            base.GetObjectData(info, context);
        }

        public void BuildDataStructures()
        {
            CalculateEdges();
            BuildTriangleRTree();
        }

        #region ISerializable Members

        

        #endregion

        #region ICloneable

            public MeshTransform Copy()
            {
                return ((ICloneable)this).Clone() as MeshTransform; 
            }

            object ICloneable.Clone()
            {
                MeshTransform newObj;

                newObj = this.MemberwiseClone() as MeshTransform;

                List<MappingGridVector2> TempList = new List<MappingGridVector2>();
            
                foreach (MappingGridVector2 pt in MapPoints)
                {
                    TempList.Add((MappingGridVector2)pt.Copy());
                }

                //Setting the mapPoints will sort and recalculate triangles
                newObj.MapPoints = TempList.ToArray(); 

                return newObj;
            }

        #endregion

        #region Transforms

            /// <summary>
            /// Return the mapping triangle which can map the point
            /// </summary>
            /// <param name="Point"></param>
            /// <returns></returns>
            internal override MappingGridTriangle GetTransform(GridVector2 Point)
            {
                //TODO: Optimize the search

                //Having a smaller epsilon caused false positives.  
                //We just want to know if we are close enough to check with the more time consuming math
                double epsilon = 5;

                if (!MappedBounds.Contains(Point, epsilon))
                    return null;

                //Fetch a list of triangles from the nearest point
                //double distance;
                List<MappingGridTriangle> triangles = mapTrianglesRTree.Intersects(Point.ToRTreeRect(0));//mapTriangles.FindNearest(Point, out distance);

                if (triangles == null)
                    return null;


                foreach (MappingGridTriangle t in triangles)
                {
                    if (!t.MappedBoundingBox.Contains(Point))
                        continue; 

                    if (t.IntersectsMapped(Point))
                        return t;
                }
                
                return null;
            }

            /// <summary>
            /// Return the mapping triangle which can map the point
            /// </summary>
            /// <param name="Point"></param>
            /// <returns></returns>
            internal override MappingGridTriangle GetInverseTransform(GridVector2 Point)
            {
                //TODO: Optimize the search

                //Having a smaller epsilon caused false positives.  
                //We just want to know if we are close enough to check with the more time consuming math
                double epsilon = 5;

                if (!ControlBounds.Contains(Point, epsilon))
                    return null;

                //Fetch a list of triangles from the nearest point
                double distance;
                List<MappingGridTriangle> triangles = controlTrianglesRTree.Intersects(Point.ToRTreeRect(0));

                if (triangles == null)
                    return null;


                foreach (MappingGridTriangle t in triangles)
                {
                    if (!t.ControlBoundingBox.Contains(Point))
                        continue;

                    if (t.IntersectsControl(Point))
                            return t;
                }
                
                return null;
            }

        #endregion

        protected virtual List<int>[] CalculateEdges()
        {
            try
            {
                //Make sure someone hasn't populated _edges already
                rwLockEdges.EnterUpgradeableReadLock();
                if (_edges != null)
                    return _edges;

                try
                {
                    rwLockEdges.EnterWriteLock();

                    //In this case someone went through this routine ahead of us exit if the data structure is built
                    if (_edges != null)
                        return _edges; 

                    _edges = new List<int>[MapPoints.Length];
                    for (int i = 0; i < MapPoints.Length; i++)
                    {
                        _edges[i] = new List<int>(8); //Estimated number of edges per point
                    }

                    for (int i = 0; i < this.TriangleIndicies.Length; i += 3)
                    {
                        int iOne = TriangleIndicies[i];
                        int iTwo = TriangleIndicies[i + 1];
                        int iThree = TriangleIndicies[i + 2];

                        //Add edges to the lists for each node
                        _edges[iOne].Add(iTwo);
                        _edges[iOne].Add(iThree);

                        _edges[iTwo].Add(iOne);
                        _edges[iTwo].Add(iThree);

                        _edges[iThree].Add(iOne);
                        _edges[iThree].Add(iTwo);
                    }

                    //Remove duplicates from edge list
                    foreach (List<int> indexList in _edges)
                    {
                        //Sort indicies and remove duplicates
                        indexList.Sort();
                        for (int iTest = 1; iTest < indexList.Count; iTest++)
                        {
                            if (Math.Abs(indexList[iTest - 1] - indexList[iTest]) < Global.Epsilon)
                            {
                                indexList.RemoveAt(iTest);
                                iTest--;
                            }
                        }

                        indexList.TrimExcess();
                    }

                    _LineSegmentGrid = new PairedLineSearchGrid(this.MapPoints, MappedBounds, _edges);

                    return _edges;
                }
                finally
                {
                    if (rwLockEdges.IsWriteLockHeld)
                        rwLockEdges.ExitWriteLock();
                }
            }
            finally
            {
                if (rwLockEdges.IsUpgradeableReadLockHeld)
                    rwLockEdges.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Returns the line on the convex hull which intersects the line L which intersects the convex hull.
        /// Returns double.MaxVal if the line L does not intersect the Convex hull
        /// </summary>
        /// <param name="L">Line intersecting convex hull</param>
        /// <param name="OutsidePoint">Point on line outside the convex hull</param>
        /// <param name="foundCtrlLine"></param>
        /// <param name="foundMapLine"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public override double ConvexHullIntersection(GridLineSegment L, GridVector2 OutsidePoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection)
        {
            double nearestIntersect = double.MaxValue;

            //Make sure the point we are testing is actually on the line
            Debug.Assert(OutsidePoint == L.A || OutsidePoint == L.B);

            //FIXME, multiple threads can get stuck here
            CalculateEdges(); 

            //For debugging only
            double nearestFailedIntersect = double.MaxValue;
            GridVector2 nearestFailedPoint = new GridVector2();

            foundCtrlLine = new GridLineSegment();
            foundMapLine = new GridLineSegment();
            intersection = new GridVector2();

            IEnumerable<GridLineSegmentPair> _linePairs = _LineSegmentGrid.GetPotentialIntersections(L);

            foreach (GridLineSegmentPair pair in _linePairs)
            {
                //Build the edge and find out if it intersects
                GridLineSegment mapLine = pair.mapLine;

                if (mapLine.MinX > L.MaxX)
                    continue;
                if (mapLine.MaxX < L.MinX)
                    continue;
                if (mapLine.MinY > L.MaxY)
                    continue;
                if (mapLine.MaxY < L.MinY)
                    continue;

                GridVector2 result;
                bool bIntersected = mapLine.Intersects(L, out result);
                double distance = GridVector2.Distance(OutsidePoint, result);
                if (distance < nearestIntersect && bIntersected)
                {
                    nearestIntersect = distance;
                    intersection = result;
                    foundMapLine = mapLine;
                    foundCtrlLine = pair.ctrlLine;
                }
                if (distance < nearestFailedIntersect && !bIntersected)
                {
                    nearestFailedPoint = result;
//                    nearestFailedSegment = mapLine;
                    nearestFailedIntersect = distance;
                }
            }

            return nearestIntersect;
        }

        public override void  MinimizeMemory()
       {
           base.MinimizeMemory();

           _LineSegmentGrid = null;
       }
        

       protected override void Dispose(bool disposing)
       {
           if (disposing)
           {
               if (rwLockEdges != null)
               {
                   rwLockEdges.Dispose();
                   rwLockEdges = null;
               }

               
           }

           base.Dispose(disposing); 

       }

    }
}
