using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Geometry.Transforms
{
    /// <summary>
    /// A transform that uses a triangulation
    /// </summary>
    [Serializable]
    public abstract class TriangulationTransform : ReferencePointBasedTransform, IDisposable, IDiscreteTransform, IControlPointTriangulation
    {
        /// <summary>
        /// Return the control triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingGridTriangle GetTransform(in GridVector2 Point);

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingGridTriangle GetInverseTransform(in GridVector2 Point);

        /// <summary>
        /// This stores the output of the Delaunay triangulation.  Every group of three integers represents a triangle
        /// </summary>
        #region Triangles

        /// <summary>
        /// This stores the output of the Delaunay triangulation.  Every group of three integers represents a triangle
        /// </summary>
        protected int[] _TriangleIndicies = null;
        public virtual int[] TriangleIndicies
        {
            get
            {
                if (_TriangleIndicies == null)
                {
                    try
                    {
                        int[] triangles = Delaunay2D.Triangulate(MappingGridVector2.MappedPoints(this.MapPoints), MappedBounds);
                        _TriangleIndicies = triangles;
                    }
                    catch (ArgumentException)
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

        protected TriangulationTransform(MappingGridVector2[] mapPoints, TransformBasicInfo info) : base(mapPoints, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");
        }

        protected TriangulationTransform(MappingGridVector2[] mapPoints, GridRectangle mappedBounds, TransformBasicInfo info)
            : base(mapPoints, mappedBounds, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");
        }

        #region ISerializable Members

        protected TriangulationTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            _TriangleIndicies = info.GetValue("_TriangleIndicies", typeof(int[])) as int[];
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

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
        public override bool CanTransform(in GridVector2 Point)
        {
            return GetTransform(Point) != null;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 Transform(in GridVector2 Point)
        {
            MappingGridTriangle t = GetTransform(Point);
            return t == null
                ? throw new ArgumentOutOfRangeException(nameof(Point), string.Format("Transform: Point could not be mapped {0}", Point.ToString()))
                : t.Transform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2[] Transform(in GridVector2[] Points)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetTransform(Point)).ToArray();
            return Points.Select(p =>
            {
                MappingGridTriangle t = GetTransform(p);
                if (t == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(Points), string.Format("Transform: Point could not be mapped {0}", p.ToString()));
                }
                else
                {
                    return t.Transform(p);
                }
            }).ToArray();
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();
            MappingGridTriangle t = GetTransform(Point);
            if (t == null)
            {
                v = default;
                return false;
            }

            v = t.Transform(Point);
            return true;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="output"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool[] TryTransform(in GridVector2[] Points, out GridVector2[] output)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetTransform(Point)).ToArray();
            bool[] IsTransformed = triangles.Select(t => t != null).ToArray();
            var inputPoints = Points;

            output = triangles.Select((tri, i) =>
            {
                if (tri != null)
                {
                    return tri.Transform(inputPoints[i]);
                }
                else
                    return default;

            }
            ).ToArray();

            //return IsTransformed; 
            return IsTransformed;
        }

        #endregion

        #region InverseTransform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanInverseTransform(in GridVector2 Point)
        {
            return GetInverseTransform(Point) != null;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 InverseTransform(in GridVector2 Point)
        {
            MappingGridTriangle t = GetInverseTransform(Point);
            return t == null
                ? throw new ArgumentOutOfRangeException(nameof(Point), string.Format("InverseTransform: Point could not be mapped {0}", Point.ToString()))
                : t.InverseTransform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetInverseTransform(Point)).ToArray();
            return Points.Select(p =>
            {
                MappingGridTriangle t = GetInverseTransform(p);
                if (t == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(Points), string.Format("InverseTransform: Point could not be mapped {0}", p.ToString()));
                }
                else
                {
                    return t.InverseTransform(p);
                }
            }).ToArray();
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();
            MappingGridTriangle t = GetInverseTransform(Point);
            if (t == null)
            {
                v = default;
                return false;
            }

            v = t.InverseTransform(Point);
            return true;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="output"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool[] TryInverseTransform(in GridVector2[] Points, out GridVector2[] output)
        {
            MappingGridTriangle[] triangles = Points.Select(Point => GetInverseTransform(Point)).ToArray();
            bool[] IsTransformed = triangles.Select(t => t != null).ToArray();

            var inputPoints = Points;
            output = triangles.Select((tri, i) =>
            {
                if (tri != null)
                {
                    return tri.InverseTransform(inputPoints[i]);
                }
                else
                    return default;

            }
            ).ToArray();

            //return IsTransformed; 
            return IsTransformed;
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

        public List<MappingGridVector2> IntersectingControlRectangle(in GridRectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangleRTree(gridRect, this.controlTrianglesRTree);
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

        public List<MappingGridVector2> IntersectingMappedRectangle(in GridRectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangleRTree(gridRect, this.mapTrianglesRTree);
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
        ///
        [NonSerialized]
        ReaderWriterLockSlim rwLockTriangles = new ReaderWriterLockSlim();
        private RTree.RTree<MappingGridTriangle> _mapTrianglesRTree = null;

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingGridTriangle> mapTrianglesRTree
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_mapTrianglesRTree != null)
                    {
                        return _mapTrianglesRTree;
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
                    if (_mapTrianglesRTree == null)
                        BuildTriangleRTree(); //Locks internally

                    Debug.Assert(_mapTrianglesRTree != null);
                    return _mapTrianglesRTree;
                }
                finally
                {
                    if (rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock();
                }
            }
        }

        private RTree.RTree<MappingGridTriangle> _controlTrianglesRTree = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingGridTriangle> controlTrianglesRTree
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_controlTrianglesRTree != null)
                    {
                        return _controlTrianglesRTree;
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
                    if (_controlTrianglesRTree == null)
                        BuildTriangleRTree(); //Locks internally

                    Debug.Assert(_controlTrianglesRTree != null);
                    return _controlTrianglesRTree;
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

        protected void BuildTriangleRTree()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();

                this._mapTrianglesRTree = new RTree.RTree<MappingGridTriangle>();
                this._controlTrianglesRTree = new RTree.RTree<MappingGridTriangle>();

                for (int i = 0; i < this.TriangleIndicies.Length; i += 3)
                {
                    MappingGridTriangle t = new MappingGridTriangle(this.MapPoints,
                                                                    _TriangleIndicies[i],
                                                                    _TriangleIndicies[i + 1],
                                                                    _TriangleIndicies[i + 2]);

                    this._mapTrianglesRTree.Add(t.Mapped.BoundingBox.ToRTreeRect(0), t);
                    this._controlTrianglesRTree.Add(t.Control.BoundingBox.ToRTreeRect(0), t);
                }
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }
        }

        private List<MappingGridVector2> IntersectingRectangleRTree(in GridRectangle gridRect,
                                                               RTree.RTree<MappingGridTriangle> TriangleRTree)
        {
            List<MappingGridTriangle> intersectingTriangles = TriangleRTree.Intersects(gridRect.ToRTreeRect(0));
            SortedSet<long> sortedIndicies = new SortedSet<long>();

            foreach (MappingGridTriangle t in intersectingTriangles)
            {
                sortedIndicies.Add(t.N1);
                sortedIndicies.Add(t.N2);
                sortedIndicies.Add(t.N3);
            }

            IEnumerable<long> distinctIndicies = sortedIndicies.Distinct();

            return distinctIndicies.Select(i => this.MapPoints[i]).ToList();
        }

        /// <summary>
        /// Returns all points inside the requested region.  
        /// If include adjacent is set to true we include points with an edge that crosses the border of the requested rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        private List<MappingGridVector2> IntersectingRectangle(in GridRectangle gridRect,
                                                               QuadTree<List<MappingGridTriangle>> PointTree)
        {

            List<MappingGridVector2> MappingPointList = null;

            if (gridRect.Contains(PointTree.Border))
            {
                MappingPointList = new List<MappingGridVector2>(MapPoints);
                return MappingPointList;
            }

            PointTree.Intersect(gridRect, out List<GridVector2> Points, out List<List<MappingGridTriangle>> ListofListTriangles);

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
        public override void MinimizeMemory()
        {

            try
            {
                rwLockTriangles.EnterWriteLock();

                _mapTrianglesRTree = null;
                _controlTrianglesRTree = null;
                _TriangleList = null;
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }

            Edges = null;

            base.MinimizeMemory();
            //this._LineSegmentGrid = null; 
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (rwLockTriangles is null == false)
                {
                    rwLockTriangles.Dispose();
                    rwLockTriangles = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }



        /// <summary>
        /// Takes two transforms and transforms the control grid of this section into the control grid space of the passed transfrom. Requires control section
        /// of this transform to match mapped section of adding transform
        /// </summary>
        public static ITransformControlPoints Transform(ITransform BtoC, IControlPointTriangulation AtoB, TransformBasicInfo info)
        {
            if (BtoC is null)
                throw new ArgumentNullException(nameof(BtoC),"TriangulationTransform Transform");

            if (AtoB is null)
                throw new ArgumentNullException(nameof(AtoB), "TriangulationTransform Transform");

            //We can't map if we don't have a triangle, return a copy of the triangle we were trying to transform
            if (AtoB.MapPoints.Length < 3)
            {
                Debug.Fail("Can't transform with Triangulation with fewer than three points");
                return null;
            }

            //If they don't overlap lets save ourselves a lot of time...
            if (BtoC is IDiscreteTransform DiscreteBtoC)
            {
                if (DiscreteBtoC.MappedBounds.Intersects(AtoB.ControlBounds) == false)
                    return null;
            }

            //FixedTransform.CalculateEdges();
            //WarpingTransform.BuildDataStructures();

            //Reset boundaries since they will be changed
            //filter.ControlBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);
            //filter.MappedBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);

            List<AddTransformThreadObj> threadObjList = new List<AddTransformThreadObj>();

            List<ManualResetEvent> doneEvents = new List<ManualResetEvent>();
            List<MappingGridVector2> newPoints = new List<MappingGridVector2>(AtoB.MapPoints.Length);

#if DEBUG
            //            List<GridVector2> mapPointList = new List<GridVector2>(newPoints.Count);
#endif

            int MinThreadPoints = 64;

            //            Trace.WriteLine("Starting with " + mapPoints.Length + " points", "Geometry"); 

            //    List<MappingGridVector2> newPoints = new List<MappingGridVector2>(); 

            //           Trace.WriteLine("Started GridTransform.Add with " + mapPoints.Length.ToString() + " points", "Geometry"); 

            //Search all mapping triangles and update control points, if they fall outside the grid then discard the triangle
            //Give each thread a lot of work to do
            int PointsPerThread = AtoB.MapPoints.Length / (System.Environment.ProcessorCount * 8);
            if (PointsPerThread < MinThreadPoints)
            {
                PointsPerThread = MinThreadPoints;
            }

            for (int iPoint = 0; iPoint < AtoB.MapPoints.Length; iPoint += PointsPerThread)
            {
                //Create a series of points for the thread to process so they aren't constantly hitting the queue lock looking for new work. 
                List<int> listPoints = new List<int>(PointsPerThread);
                for (int iAddPoint = iPoint; iAddPoint < iPoint + PointsPerThread; iAddPoint++)
                {
                    //Don't add if the point is out of range
                    if (iAddPoint >= AtoB.MapPoints.Length)
                        break;

                    listPoints.Add(iAddPoint);
                }

                //MappingGridVector2 mapPoint = mapPoints[iPoint];
                AddTransformThreadObj AddThreadObj = null;
                try
                {
                    AddThreadObj = new AddTransformThreadObj(listPoints.ToArray(), AtoB, BtoC);

                    threadObjList.Add(AddThreadObj);

                    if (AtoB.MapPoints.Length <= MinThreadPoints)
                    {
                        AddThreadObj.DoneEvent.Set();
                        AddThreadObj.ThreadPoolCallback(System.Threading.Thread.CurrentThread);
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
            if(doneEvents.Count > 0)
                ManualResetEvent.WaitAll(doneEvents.ToArray());

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

            MappingGridVector2.RemoveControlSpaceDuplicates(newPoints);
            MappingGridVector2.RemoveMappedSpaceDuplicates(newPoints);

            //Cannot make a transform with fewer than 3 points
            if (newPoints.Count < 3)
            {
                return null;
            }

            ITransformControlPoints newTransform = null;

            //If we started with a grid transform and all the control points mapped then we can create a new grid transform
            if (AtoB is GridTransform gridTransform && AllPointsTransformed)
            {
                Debug.Assert(AtoB.MapPoints.Length == newPoints.Count);

                //Used to set mapped bounds to WarpingTransform.MappedBounds, but it was incorrect.  Setting mapped bounds to null so it is calculated.
                newTransform = new GridTransform(newPoints.ToArray(), new GridRectangle(), gridTransform.GridSizeX, gridTransform.GridSizeY, info);
            }
            else
            {
                newTransform = new MeshTransform(newPoints.ToArray(), info);
            }

            //Optional, but useful step. In rare cases we lose some mappable space when the fixed transform are inside the control space of the mapped transform, but the triangulation of the mapped control points would eliminate these points
            //in these cases we can test if they can be added back in.
            
            System.Collections.Concurrent.ConcurrentBag<MappingGridVector2> MappableFixedPoints = new System.Collections.Concurrent.ConcurrentBag<MappingGridVector2>();

            if (BtoC is ITransformControlPoints BtoCTriTransform)
            {
                //We only check for points on the convex hull, this eliminates losing mappable area, but may not retain high warp correction areas.
                var BtoC_ControlPoints = BtoCTriTransform.MapPoints.Select(mp => mp.ControlPoint).ToArray();
                var BtoC_ConvexHullControlPoints = BtoC_ControlPoints.ConvexHull(out var originalIndicies);
                var BtoC_PointsOfConcern = originalIndicies.Select(i => BtoCTriTransform.MapPoints[i]).ToArray();

                Parallel.ForEach<MappingGridVector2>(BtoC_PointsOfConcern, FixedPointPair =>
                {
                    if (!newTransform.CanInverseTransform(FixedPointPair.ControlPoint) &&
                        AtoB.CanInverseTransform(FixedPointPair.MappedPoint))
                    {
                        GridVector2 NewMapPoint = AtoB.InverseTransform(FixedPointPair.MappedPoint); 
                        MappableFixedPoints.Add(new MappingGridVector2(FixedPointPair.ControlPoint, NewMapPoint)); 
                    }
                }
                );

                if (!MappableFixedPoints.IsEmpty)
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

                        if (add)
                        {
                            newPoints.Add(newPoint);
                        }
                    }

                    //MappingGridVector2.RemoveDuplicates(newPoints);
                    newTransform = new MeshTransform(newPoints.ToArray(), info);
                }
            }
             
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
    }

}
