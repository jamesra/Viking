using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    /// <summary>
    /// A transform that uses a triangulation
    /// </summary>
    [Serializable]
    public abstract class TriangulationTransform : ReferencePointBasedTransform, IDisposable
    {
        internal RBFTransform FallBackTransform = null; 

        /// <summary>
        /// Return the control triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingGridTriangle GetTransform(GridVector2 Point);

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingGridTriangle GetInverseTransform(GridVector2 Point);

        /// <summary>
        /// This stores the output of the Delaunay triangulation.  Every group of three integers represents a triangle
        /// </summary>
        #region Triangles

        /// <summary>
        /// This stores the output of the Delaunay triangulation.  Every group of three integers represents a triangle
        /// </summary>
        private int[] _TriangleIndicies = null;
        public virtual int[] TriangleIndicies
        {
            get
            {
                if (_TriangleIndicies == null)
                {
                    try
                    {
                        int[] triangles = Delaunay.Triangulate(MappingGridVector2.MappedPoints(this.MapPoints), MappedBounds);
                        _TriangleIndicies = triangles;
                    }
                    catch (ArgumentException )
                    {
                        _TriangleIndicies = null;
                    }
                }

                return _TriangleIndicies;
            }

            protected set
            {
                _TriangleIndicies = value;
            }
        }

        #endregion

        /// <summary>
        /// This stores the list of edges connected to each point in the triangulation.
        /// </summary>
        /// <param name="mapPoints"></param>
        /// <param name="info"></param>
        public abstract List<int>[] Edges { get; protected set; }

        protected TriangulationTransform(MappingGridVector2[] mapPoints, TransformInfo info) : base(mapPoints, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");

            FallBackTransform = new RBFTransform(mapPoints, info); 
        }

        protected TriangulationTransform(MappingGridVector2[] mapPoints, GridRectangle mappedBounds, TransformInfo info)
            : base(mapPoints, mappedBounds, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points"); 

            FallBackTransform = new RBFTransform(mapPoints, info); 
        }

        #region ISerializable Members

        protected TriangulationTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(); 

            _TriangleIndicies = info.GetValue("_TriangleIndicies", typeof(int[])) as int[]; 
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(); 


            info.AddValue("_TriangleIndicies", _TriangleIndicies);

            base.GetObjectData(info, context);
        }

        #endregion

        #region Transform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanTransform(GridVector2 Point)
        {
            if (GetTransform(Point) == null)
                return false;

            return true;
        }
        
        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 Transform(GridVector2 Point)
        {
            MappingGridTriangle t = GetTransform(Point);
            if (t == null)
            {
                //return FallBackTransform.Transform(Point); 
                throw new ArgumentOutOfRangeException("Point", "Transform: Point could not be mapped");
            }

            return t.Transform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2[] Transform(GridVector2[] Points)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetTransform(Point)).ToArray();
            if (triangles.Any(t => t == null))
            {
                //return FallBackTransform.Transform(Point); 
                throw new ArgumentOutOfRangeException("Point", "Transform: Point could not be mapped");
            }

            return triangles.Select((tri, i) => tri.Transform(Points[i])).ToArray();
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();
            MappingGridTriangle t = GetTransform(Point);
            if (t == null)
            {
                //return FallBackTransform.TryTransform(Point, out v);
                return false;
            }

            v = t.Transform(Point);
            return true;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(GridVector2[] Points, out GridVector2[] output)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetTransform(Point)).ToArray();
            if (triangles.Any(t => t == null))
            {
                output = new GridVector2[0]; 
                return false;
            }

            output = triangles.Select((tri, i) => tri.Transform(Points[i])).ToArray();
            return true; 
        }

        private GridVector2[] TransformWithRBFFallback(GridVector2[] Points, MappingGridTriangle[] triangles)
        {
            return triangles.Select((t, i) =>
            {
                if (t == null)
                    return FallBackTransform.Transform(Points[i]);

                return t.Transform(Points[i]);
            }).ToArray(); 
        }

        #endregion

        #region InverseTransform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanInverseTransform(GridVector2 Point)
        {
            if (GetInverseTransform(Point) == null)
                return false;

            return true;
        }
        
        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 InverseTransform(GridVector2 Point)
        {
            MappingGridTriangle t = GetInverseTransform(Point);
            if (t == null)
            {
                //return FallBackTransform.InverseTransform(Point); 
                throw new ArgumentOutOfRangeException("Point", "InverseTransform: Point could not be mapped");
            }

            return t.InverseTransform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2[] InverseTransform(GridVector2[] Points)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetInverseTransform(Point)).ToArray();
            if (triangles.Any(t => t == null))
            {
                //return FallBackTransform.Transform(Point); 
                throw new ArgumentOutOfRangeException("Point", "InverseTransform: Point could not be mapped");
            }

            return triangles.Select((tri, i) => tri.InverseTransform(Points[i])).ToArray();
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();

            MappingGridTriangle t = GetInverseTransform(Point);
            if (t == null)
            {
//                return FallBackTransform.TryInverseTransform(Point, out v); 
                return false;
            }

            v = t.InverseTransform(Point);

            return true;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(GridVector2[] Points, out GridVector2[] output)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetInverseTransform(Point)).ToArray();
            if (triangles.Any(t => t == null))
            {
                output = new GridVector2[0];
                return false;
            }

            output = triangles.Select((tri, i) => tri.InverseTransform(Points[i])).ToArray();
            return true;
        }

        private GridVector2[] InverseTransformWithRBFFallback(GridVector2[] Points, MappingGridTriangle[] triangles)
        {
            return triangles.Select((t, i) =>
            {
                if (t == null)
                    return FallBackTransform.InverseTransform(Points[i]);

                return t.Transform(Points[i]);
            }).ToArray();
        }

        /// <summary>
        /// Takes two transforms and transforms the control grid of this section into the control grid space of the passed transfrom. Requires control section
        /// of this transform to match mapped section of adding transform
        /// </summary>
        public static TriangulationTransform Transform(TriangulationTransform FixedTransform, TriangulationTransform WarpingTransform, TransformInfo info)
        {
            if (FixedTransform == null || WarpingTransform == null)
            {
                throw new ArgumentNullException("TriangulationTransform Transform"); 
            }

            //We can't map if we don't have a triangle, return a copy of the triangle we were trying to transform
            if (WarpingTransform.MapPoints.Length < 3)
            {
                Debug.Fail("Can't transform with Triangulation with fewer than three points");
                return null;
            }

            //If they don't overlap lets save ourselves a lot of time...
            if (FixedTransform.MappedBounds.Intersects(WarpingTransform.ControlBounds) == false)
            {
                
                return null;
            }

            //FixedTransform.CalculateEdges();
            //WarpingTransform.BuildDataStructures();

            //Reset boundaries since they will be changed
            //filter.ControlBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);
            //filter.MappedBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);

            List<AddTransformThreadObj> threadObjList = new List<AddTransformThreadObj>();

            List<ManualResetEvent> doneEvents = new List<ManualResetEvent>();
            List<MappingGridVector2> newPoints = new List<MappingGridVector2>(WarpingTransform.MapPoints.Length);

#if DEBUG
//            List<GridVector2> mapPointList = new List<GridVector2>(newPoints.Count);
#endif

            int MinThreadPoints = 64; 

            //            Trace.WriteLine("Starting with " + mapPoints.Length + " points", "Geometry"); 

            //    List<MappingGridVector2> newPoints = new List<MappingGridVector2>(); 

            //           Trace.WriteLine("Started GridTransform.Add with " + mapPoints.Length.ToString() + " points", "Geometry"); 

            //Search all mapping triangles and update control points, if they fall outside the grid then discard the triangle
            //Give each thread a lot of work to do
            int PointsPerThread = WarpingTransform.MapPoints.Length / (System.Environment.ProcessorCount * 8);
            if (PointsPerThread < MinThreadPoints)
            {
                PointsPerThread = MinThreadPoints;
            }

            for (int iPoint = 0; iPoint < WarpingTransform.MapPoints.Length; iPoint += PointsPerThread)
            {
                //Create a series of points for the thread to process so they aren't constantly hitting the queue lock looking for new work. 
                List<int> listPoints = new List<int>(PointsPerThread);
                for (int iAddPoint = iPoint; iAddPoint < iPoint + PointsPerThread; iAddPoint++)
                {
                    //Don't add if the point is out of range
                    if (iAddPoint >= WarpingTransform.MapPoints.Length)
                        break;

                    listPoints.Add(iAddPoint);
                }

                //MappingGridVector2 mapPoint = mapPoints[iPoint];
                AddTransformThreadObj AddThreadObj = null;
                try
                {
                    AddThreadObj = new AddTransformThreadObj(listPoints.ToArray(), WarpingTransform, FixedTransform);
                    
                    threadObjList.Add(AddThreadObj);

                    if (WarpingTransform.MapPoints.Length <= MinThreadPoints)
                    {
                        AddThreadObj.DoneEvent.Set();
                        AddThreadObj.ThreadPoolCallback(System.Threading.Thread.CurrentContext);
                    }
                    else
                    {
                        doneEvents.Add(AddThreadObj.DoneEvent);
                        //For single threaded debug, comment out threadpool and uncomment AddThreadObj.ThreadPoolCallback line
                        ThreadPool.QueueUserWorkItem(AddThreadObj.ThreadPoolCallback);

                    }

                    AddThreadObj = null;
                }
                catch (Exception)
                {
                    if (AddThreadObj != null)
                    {
                        AddThreadObj.Dispose();
                        AddThreadObj = null;
                    }

                    throw;
                }

                /*
                MappingGridVector2 UnmappedPoint = mapPoints[iPoint];
                MappingGridTriangle mapTri = transform.GetTransform(mapPoints[iPoint].ControlPoint);

                if (mapTri != null)
                {
                    GridVector2 newControl = mapTri.Transform(UnmappedPoint.ControlPoint);
                    newPoints.AddRange(new MappingGridVector2[] { new MappingGridVector2(newControl, UnmappedPoint.MappedPoint) });

                    List<MappingGridVector2> sortPoints = new List<MappingGridVector2>(newPoints);
                    sortPoints.Sort();
                    for (int i = 1; i < sortPoints.Count; i++)
                    {
                        Debug.Assert(sortPoints[i - 1].ControlPoint != sortPoints[i].ControlPoint);
                    }
                }
                else
                {
                    //In this case we need to test each edge connecting this point to other points.
                    //newPoints = new MappingGridVector2[0];
                    for(int i = 0; i < TriangleIndicies.Length; i += 3)
                    {
                        if(TriangleIndicies[i] == iPoint)
                        {
                            
                    int[] EdgesIndicies = Array.Find<int>(this.TriangleIndicies, iPoint);
                }
                */

                
                //                AddThreadObj.ThreadPoolCallback(null);


                //newPoints.AddRange(AddThreadObj.newPoints);

                //newPoints.Sort();


#if false
                for (int iTest = 1; iTest < newPoints.Count; iTest++)
                {
                    Debug.Assert(newPoints[iTest - 1].ControlPoint != newPoints[iTest].ControlPoint); 
                }

                for (int iMap = 0; iMap < AddThreadObj.newPoints.Length; iMap++)
                {
                    mapPointList.Add(AddThreadObj.newPoints[iMap].MappedPoint);
                }

                mapPointList.Sort();

                for (int iMap = 1; iMap < mapPointList.Count; iMap++)
                {
                    Debug.Assert(GridVector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
                }
#endif
            }

            //Wait for the threads to finish processing.  There is a 64 handle limit for WaitAll so we wait on one at a time
            foreach (ManualResetEvent doneEvent in doneEvents)
            {
                doneEvent.WaitOne();
            }

            newPoints.Clear();

            //This indicates if every original point was transformable.  If it is true and we started with a grid transform we then know the output can also be a grid transform
            bool AllPointsTransformed = true;
            foreach (AddTransformThreadObj obj in threadObjList)
            {
                AllPointsTransformed = AllPointsTransformed && obj.AllPointsTransformed;
                if (obj.newPoints != null)
                    newPoints.AddRange(obj.newPoints);

                obj.Dispose();
            }

            //            Trace.WriteLine("Mapped " + newPoints.Count + " points", "Geometry"); 

#if false

            mapPointList.Clear(); 
            for (int iMap = 0; iMap < newPoints.Count; iMap++)
            {
                mapPointList.Add(newPoints[iMap].MappedPoint);
            }

            mapPointList.Sort();

            for (int iMap = 1; iMap < mapPointList.Count; iMap++)
            {
                Debug.Assert(GridVector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
            }
#endif

            MappingGridVector2.RemoveDuplicates(newPoints);

            //Cannot make a transform with fewer than 3 points
            if (newPoints.Count < 3)
            {
                return null; 
            }

            TriangulationTransform newTransform = null;

            //If we started with a grid transform and all the control points mapped then we can create a new grid transform
            GridTransform gridTransform = WarpingTransform as GridTransform;
            if (gridTransform != null && AllPointsTransformed)
            {
                Debug.Assert(WarpingTransform.MapPoints.Length == newPoints.Count);

                //Used to set mapped bounds to WarpingTransform.MappedBounds, but it was incorrect.  Setting mapped bounds to null so it is calculated.
                newTransform = new GridTransform(newPoints.ToArray(), new GridRectangle(), gridTransform.GridSizeX, gridTransform.GridSizeY, info);
            }
            else
            {
                newTransform = new MeshTransform(newPoints.ToArray(), info);
            }

            //Optional, but useful step. In rare cases we lose some mappable space when the fixed transform are inside the control space of the mapped transform, but the triangulation of the mapped control points would eliminate these points
            //in these cases we can test if they can be added back in. 
            /*
            System.Collections.Concurrent.ConcurrentBag<MappingGridVector2> MappableFixedPoints = new System.Collections.Concurrent.ConcurrentBag<MappingGridVector2>();
            Parallel.ForEach<MappingGridVector2>(FixedTransform.MapPoints, FixedPointPair =>
            {
                if (!newTransform.CanInverseTransform(FixedPointPair.ControlPoint) &&
                    WarpingTransform.CanInverseTransform(FixedPointPair.MappedPoint))
                {
                    GridVector2 NewMapPoint = WarpingTransform.InverseTransform(FixedPointPair.MappedPoint);

                    MappableFixedPoints.Add(new MappingGridVector2(FixedPointPair.ControlPoint, NewMapPoint));
                    
                }
            }
            );

            if (MappableFixedPoints.Count > 0)
            {
                foreach (MappingGridVector2 newPoint in MappableFixedPoints)
                {
                    bool add = true; 
                    foreach (MappingGridVector2 oldPoint in newPoints)
                    {
                        if (newPoint.ControlPoint == oldPoint.ControlPoint ||
                           newPoint.MappedPoint == oldPoint.MappedPoint)
                        {
                            add = false;
                            break;
                        }
                    }
                    
                    if(add)
                    {
                        newPoints.Add(newPoint);
                    }
                }

                //MappingGridVector2.RemoveDuplicates(newPoints);
                newTransform = new MeshTransform(newPoints.ToArray(), info);
            }
            */
            /*
             
            //            Trace.WriteLine("Ended with " + newPoints.Count + " points", "Geometry");
            this.MapPoints = newPoints.ToArray();

            //Edges are build on mapPoints, so we need to remove them so they'll be recalculates
            _edges = null;
            //Other datastructures are dependent on edges, so minimize memory will delete them
            MinimizeMemory();

            //            Trace.WriteLine("Finished GridTransform.Add with " + newPoints.Count.ToString() + " points", "Geometry"); 

            //Check whether these have been set yet or if I don't need to clear them again
            this.Info.ControlSection = WarpingTransform.Info.ControlSection;
            
            */

            return newTransform;
        }

        #endregion

        #region Edges


        
        /// <summary>
        /// Find the edge which intersects the passed edge L.
        /// Return the distance to the intersection point.  If they exist the out parameters are intersection point and the Control and Mapped Line.
        /// </summary>
        /// <param name="L">Line to test for intersection with the transform</param>
        /// <param name="OutsidePoint">Point on line which is outside the convex hull from which distance is calculated</param>
        /// <param name="foundCtrlLine"></param>
        /// <param name="foundMapLine"></param>
        /// <param name="intersection">Intersection point</param>
        /// <returns>Distance to intersection or double.MaxValue if no intersection is found</returns>
        public abstract double ConvexHullIntersection(GridLineSegment L, GridVector2 OutsidePoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection);

        #endregion

        #region Extra data cruft

        public List<MappingGridVector2> IntersectingControlRectangle(GridRectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangle(gridRect, this.controlTriangles);
            if (!IncludeAdjacent)
            {
                for (int i = 0; i < foundPoints.Count; i++)
                {
                    if (!gridRect.Contains(foundPoints[i].ControlPoint))
                    {
                        foundPoints.RemoveAt(i);
                        i--;
                    }
                }
            }

            return foundPoints;
        }

        public List<MappingGridVector2> IntersectingMappedRectangle(GridRectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangle(gridRect, this.mapTriangles);
            if (!IncludeAdjacent)
            {
                for (int i = 0; i < foundPoints.Count; i++)
                {
                    if (!gridRect.Contains(foundPoints[i].MappedPoint))
                    {
                        foundPoints.RemoveAt(i);
                        i--;
                    }
                }
            }

            return foundPoints;
        }

                /// <summary>
        /// You need to take this lock when building or changing the QuadTrees managing the triangles of the mesh
        /// </summary>
        ReaderWriterLockSlim rwLockTriangles = new ReaderWriterLockSlim(); 
        private QuadTree<List<MappingGridTriangle>> _mapTriangles = null; 

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public QuadTree<List<MappingGridTriangle>> mapTriangles
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_mapTriangles != null)
                    {
                        return _mapTriangles; 
                    }
                }
                finally
                {
                    if(rwLockTriangles.IsReadLockHeld)
                        rwLockTriangles.ExitReadLock(); 
                }

                //_mapTriangles was null, so get in line to populate it
                try
                {
                    rwLockTriangles.EnterUpgradeableReadLock();
                    if(_mapTriangles == null)
                        BuildTriangleQuadTree(); //Locks internally

                    Debug.Assert(_mapTriangles != null);
                    return _mapTriangles;
                }
                finally
                {
                    if(rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock(); 
                }
            }
        }

        private QuadTree<List<MappingGridTriangle>> _controlTriangles = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public QuadTree<List<MappingGridTriangle>> controlTriangles
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_controlTriangles != null)
                    {
                        return _controlTriangles;
                    }
                }
                finally
                {
                    if (rwLockTriangles.IsReadLockHeld)
                        rwLockTriangles.ExitReadLock();
                }

                //_mapTriangles was null, so get in line to populate it
                try
                {
                    rwLockTriangles.EnterUpgradeableReadLock();
                    if (_controlTriangles == null)
                        BuildTriangleQuadTree(); //Locks internally

                    Debug.Assert(_controlTriangles != null);
                    return _controlTriangles;
                }
                finally
                {
                    if (rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock();
                }
            }
        }

        private List<MappingGridTriangle>[] _TriangleList;
        List<MappingGridTriangle>[] TriangleList
        {
            get
            {
                if (_TriangleList == null)
                {
                    BuildTriangleList();
                }

                Debug.Assert(_TriangleList != null);
                return _TriangleList;
            }
        }

        protected void BuildTriangleList()
        {
            if (_TriangleList != null)
                return;

            _TriangleList = new List<MappingGridTriangle>[this.MapPoints.Length];

            for (int i = 0; i < TriangleIndicies.Length; i += 3)
            {
                int iOne = TriangleIndicies[i];
                int iTwo = TriangleIndicies[i + 1];
                int iThree = TriangleIndicies[i + 2];

                //Safe to go straight into the cache since we looked at TriangleIndicies to initialize list
                MappingGridTriangle newTri = new MappingGridTriangle(MapPoints,
                                                     TriangleIndicies[i],
                                                     TriangleIndicies[i + 1],
                                                     TriangleIndicies[i + 2]);

                //Get the list for each point and add a reference to the triangle

                if (_TriangleList[iOne] == null)
                {
                    _TriangleList[iOne] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iOne].Add(newTri);

                if (_TriangleList[iTwo] == null)
                {
                    _TriangleList[iTwo] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iTwo].Add(newTri);

                if (_TriangleList[iThree] == null)
                {
                    _TriangleList[iThree] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iThree].Add(newTri);
            }
        }

        //
        /// <summary>
        /// Build a quad tree for both mapping and control triangles, takes the rwLockTriangles write lock
        /// </summary>
        protected void BuildTriangleQuadTree()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();

                //Don't rebuild if they already exist, someone probably calculated them while we waited for the lock
                if(_mapTriangles != null)
                    return; 

                GridVector2[] quadMapPoints = new GridVector2[MapPoints.Length];
                GridVector2[] quadControlPoints = new GridVector2[MapPoints.Length];
                //Build the list map points
                for(int i = 0; i < MapPoints.Length; i++)
                {
                    quadMapPoints[i] = MapPoints[i].MappedPoint;
                    quadControlPoints[i] = MapPoints[i].ControlPoint;
                }

                //Build the quad tree for mapping points
                _mapTriangles = new QuadTree<List<MappingGridTriangle>>(quadMapPoints, TriangleList, MappedBounds);

                //Build the quad tree for control points
                _controlTriangles = new QuadTree<List<MappingGridTriangle>>(quadControlPoints, TriangleList, ControlBounds);
            }
            finally 
            {
                if(rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }
        }

        

        /// <summary>
        /// Returns all points inside the requested region.  
        /// If include adjacent is set to true we include points with an edge that crosses the border of the requested rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        private List<MappingGridVector2> IntersectingRectangle(GridRectangle gridRect,
                                                               QuadTree<List<MappingGridTriangle>> PointTree)
        {
            List<GridVector2> Points; 
            List<List<MappingGridTriangle>> ListofListTriangles;

            List<MappingGridVector2> MappingPointList= null;

            if (gridRect.Contains(PointTree.Border))
            {
                MappingPointList = new List<MappingGridVector2>(MapPoints);
                return MappingPointList; 
            }

            PointTree.Intersect(gridRect, out Points, out ListofListTriangles);

            bool[] Added = new bool[MapPoints.Length];
            MappingPointList = new List<MappingGridVector2>(Points.Count * 2);
            List<List<MappingGridTriangle>> MappingTriangleList = new List<List<MappingGridTriangle>>(Points.Count * 2);

            //Add all the unique points bordering the requested rectangle
            for (int iPoint = 0; iPoint < Points.Count; iPoint++)
            {
                List<MappingGridTriangle> FoundTriangleList = ListofListTriangles[iPoint];
                for (int iTri = 0; iTri < FoundTriangleList.Count; iTri++)
                {
                    MappingGridTriangle Triangle = FoundTriangleList[iTri];
                    if (!Added[Triangle.N1])
                    {
                        Added[Triangle.N1] = true;
                        MappingPointList.Add(this.MapPoints[Triangle.N1]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                    if (!Added[Triangle.N2])
                    {
                        Added[Triangle.N2] = true;
                        MappingPointList.Add(this.MapPoints[Triangle.N2]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                    if (!Added[Triangle.N3])
                    {
                        Added[Triangle.N3] = true;
                        MappingPointList.Add(this.MapPoints[Triangle.N3]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                }
            }

            return MappingPointList; 
            

           
        }

        

        /// <summary>
        /// This call removes cached data from the transform to reduce memory footprint.  Called when we only expect Transform and Inverse transform calls in the future
        /// </summary>
        public virtual void MinimizeMemory()
        {
            
            try
            {
                rwLockTriangles.EnterWriteLock();

                _mapTriangles = null;
                _controlTriangles = null;
                _TriangleList = null; 
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }

            Edges = null;

            //this._LineSegmentGrid = null; 
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (rwLockTriangles != null)
                {
                    rwLockTriangles.Dispose();
                    rwLockTriangles = null;
                }

                if (_controlTriangles != null)
                {
                    _controlTriangles.Dispose();
                    _controlTriangles = null;
                }

                if (_mapTriangles != null)
                {
                    _mapTriangles.Dispose();
                    _mapTriangles = null;
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
