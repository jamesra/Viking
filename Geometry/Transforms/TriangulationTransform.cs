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
        internal abstract MappingTriangle GetTransform(in Vector2 Point);

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingTriangle GetInverseTransform(in Vector2 Point);

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
                if (_TriangleIndicies is null)
                {
                    try
                    {
                        int[] triangles = Delaunay2D.Triangulate(MappingVector2.MappedPoints(this.MapPoints), MappedBounds);
                        _TriangleIndicies = triangles;
                    }
                    catch (ArgumentException)
                    {
                        _TriangleIndicies = [];
                    }
                }

                return _TriangleIndicies ?? [];
            }

            protected set => _TriangleIndicies = value;
        }

        #endregion

        /// <summary>
        /// This stores the list of edges connected to each point in the triangulation.
        /// </summary>
        /// <param name="mapPoints"></param>
        /// <param name="info"></param>
        public abstract List<int>[] Edges { get; protected set; }

        protected TriangulationTransform(MappingVector2[] mapPoints, TransformBasicInfo info) : base(mapPoints, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");
        }

        protected TriangulationTransform(MappingVector2[] mapPoints, Rectangle mappedBounds, TransformBasicInfo info)
            : base(mapPoints, mappedBounds, info)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");
        }

        protected TriangulationTransform(MappingVector2[] mapPoints, Rectangle mappedBounds, TransformBasicInfo info, bool preserveMapPointOrder)
            : base(mapPoints, mappedBounds, info, preserveMapPointOrder)
        {
            Debug.Assert(mapPoints.Length >= 3, "Triangulation transform requires at least 3 points");
        }

        #region ISerializable Members

        protected TriangulationTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            _TriangleIndicies = info.GetValue("_TriangleIndicies", typeof(int[])) as int[];
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
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
        public override bool CanTransform(in Vector2 Point) => GetTransform(Point) != null;

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2 Transform(in Vector2 Point)
        {
            MappingTriangle t = GetTransform(Point);
            return t is null
                ? throw new ArgumentOutOfRangeException(nameof(Point), string.Format("Transform: Point could not be mapped {0}", Point.ToString()))
                : t.Transform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2[] Transform(in Vector2[] Points)
        {
            MappingTriangle[] triangles = [.. Points.Select(Point => GetTransform(Point))];
            return [.. Points.Select(p =>
            {
                MappingTriangle t = GetTransform(p);
                if (t is null)
                {
                    throw new ArgumentOutOfRangeException(nameof(Points), string.Format("Transform: Point could not be mapped {0}", p.ToString()));
                }
                else
                {
                    return t.Transform(p);
                }
            })];
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = new Vector2();
            MappingTriangle t = GetTransform(Point);
            if (t is null)
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
        public override bool[] TryTransform(in Vector2[] Points, out Vector2[] output)
        {
            MappingTriangle[] triangles = [.. Points.Select(Point => GetTransform(Point))];
            bool[] IsTransformed = [.. triangles.Select(t => t != null)];
            var inputPoints = Points;

            output = [.. triangles.Select((tri, i) =>
            {
                if (tri != null)
                {
                    return tri.Transform(inputPoints[i]);
                }
                else
                    return default;

            }
            )];

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
        public override bool CanInverseTransform(in Vector2 Point) => GetInverseTransform(Point) != null;

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2 InverseTransform(in Vector2 Point)
        {
            MappingTriangle t = GetInverseTransform(Point);
            return t is null
                ? throw new ArgumentOutOfRangeException(nameof(Point), string.Format("InverseTransform: Point could not be mapped {0}", Point.ToString()))
                : t.InverseTransform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2[] InverseTransform(in Vector2[] Points)
        {
            MappingTriangle[] triangles = [.. Points.Select(Point => GetInverseTransform(Point))];
            return [.. Points.Select(p =>
            {
                MappingTriangle t = GetInverseTransform(p);
                if (t is null)
                {
                    throw new ArgumentOutOfRangeException(nameof(Points), string.Format("InverseTransform: Point could not be mapped {0}", p.ToString()));
                }
                else
                {
                    return t.InverseTransform(p);
                }
            })];
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = new Vector2();
            MappingTriangle t = GetInverseTransform(Point);
            if (t is null)
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
        public override bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] output)
        {
            MappingTriangle[] triangles = [.. Points.Select(Point => GetInverseTransform(Point))];
            bool[] IsTransformed = [.. triangles.Select(t => t != null)];

            var inputPoints = Points;
            output = [.. triangles.Select((tri, i) =>
            {
                if (tri != null)
                {
                    return tri.InverseTransform(inputPoints[i]);
                }
                else
                    return default;

            }
            )];

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
        public abstract double ConvexHullIntersection(LineSegment L, Vector2 OutsidePoint, out LineSegment foundCtrlLine, out LineSegment foundMapLine, out Vector2 intersection);

        #endregion

        #region Extra data cruft

        public List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingVector2> foundPoints = IntersectingRectangleRTree(gridRect, this.controlTrianglesRTree);
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

        public List<MappingVector2> IntersectingMappedRectangle(in Rectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingVector2> foundPoints = IntersectingRectangleRTree(gridRect, this.mapTrianglesRTree);
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
        ReaderWriterLockSlim rwLockTriangles = new();
        private RTree.RTree<MappingTriangle> _mapTrianglesRTree = null;

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingTriangle> mapTrianglesRTree
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
                    if (_mapTrianglesRTree is null)
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

        private RTree.RTree<MappingTriangle> _controlTrianglesRTree = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingTriangle> controlTrianglesRTree
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
                    if (_controlTrianglesRTree is null)
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

        private List<MappingTriangle>[] _TriangleList;
        List<MappingTriangle>[] TriangleList
        {
            get
            {
                if (_TriangleList is null)
                {
                    BuildTriangleList();
                }

                Debug.Assert(_TriangleList != null);
                return _TriangleList;
            }
        }

        protected void BuildTriangleList()
        {
            if (_TriangleList is not null)
                return;

            _TriangleList = new List<MappingTriangle>[this.MapPoints.Length];

            for (int i = 0; i < TriangleIndicies.Length; i += 3)
            {
                int iOne = TriangleIndicies[i];
                int iTwo = TriangleIndicies[i + 1];
                int iThree = TriangleIndicies[i + 2];

                //Safe to go straight into the cache since we looked at TriangleIndicies to initialize list
                MappingTriangle newTri = new(MapPoints,
                                                     TriangleIndicies[i],
                                                     TriangleIndicies[i + 1],
                                                     TriangleIndicies[i + 2]);

                //Get the list for each point and add a reference to the triangle

                if (_TriangleList[iOne] is null)
                {
                    _TriangleList[iOne] = new List<MappingTriangle>(6);
                }
                _TriangleList[iOne].Add(newTri);

                if (_TriangleList[iTwo] is null)
                {
                    _TriangleList[iTwo] = new List<MappingTriangle>(6);
                }
                _TriangleList[iTwo].Add(newTri);

                if (_TriangleList[iThree] is null)
                {
                    _TriangleList[iThree] = new List<MappingTriangle>(6);
                }
                _TriangleList[iThree].Add(newTri);
            }
        }

        protected void BuildTriangleRTree()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();

                this._mapTrianglesRTree = new RTree.RTree<MappingTriangle>();
                this._controlTrianglesRTree = new RTree.RTree<MappingTriangle>();

                for (int i = 0; i < this.TriangleIndicies.Length; i += 3)
                {
                    MappingTriangle t = new(this.MapPoints,
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

        private List<MappingVector2> IntersectingRectangleRTree(in Rectangle gridRect,
                                                               RTree.RTree<MappingTriangle> TriangleRTree)
        {
            List<MappingTriangle> intersectingTriangles = TriangleRTree.Intersects(gridRect.ToRTreeRect(0));
            SortedSet<long> sortedIndices = [];

            foreach (MappingTriangle t in intersectingTriangles)
            {
                sortedIndices.Add(t.N1);
                sortedIndices.Add(t.N2);
                sortedIndices.Add(t.N3);
            }

            IEnumerable<long> distinctIndicies = sortedIndices.Distinct();

            return [.. distinctIndicies.Select(i => this.MapPoints[i])];
        }

        /// <summary>
        /// Returns all points inside the requested region.  
        /// If include adjacent is set to true we include points with an edge that crosses the border of the requested rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        private List<MappingVector2> IntersectingRectangle(in Rectangle gridRect,
                                                               QuadTreeWithUniqueValues<List<MappingTriangle>> pointTreeWithUniqueValues)
        {

            List<MappingVector2> MappingPointList = null;

            if (gridRect.Contains(pointTreeWithUniqueValues.Border))
            {
                MappingPointList = [.. MapPoints];
                return MappingPointList;
            }

            pointTreeWithUniqueValues.Intersect(gridRect, out List<Vector2> Points, out List<List<MappingTriangle>> ListofListTriangles);

            bool[] Added = new bool[MapPoints.Length];
            MappingPointList = new List<MappingVector2>(Points.Count * 2);
            List<List<MappingTriangle>> MappingTriangleList = new(Points.Count * 2);

            //Add all the unique points bordering the requested rectangle
            for (int iPoint = 0; iPoint < Points.Count; iPoint++)
            {
                List<MappingTriangle> FoundTriangleList = ListofListTriangles[iPoint];
                for (int iTri = 0; iTri < FoundTriangleList.Count; iTri++)
                {
                    MappingTriangle Triangle = FoundTriangleList[iTri];
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
                        MappingTriangleList.Add(this._TriangleList[Triangle.N2]);
                    }
                    if (!Added[Triangle.N3])
                    {
                        Added[Triangle.N3] = true;
                        MappingPointList.Add(this.MapPoints[Triangle.N3]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N3]);
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
                throw new ArgumentNullException(nameof(BtoC), "TriangulationTransform Transform");

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
            //filter.ControlBounds = new Rectangle(double.MinValue, double.MinValue, 0, 0);
            //filter.MappedBounds = new Rectangle(double.MinValue, double.MinValue, 0, 0);

            List<AddTransformThreadObj> threadObjList = [];

            List<ManualResetEvent> doneEvents = [];
            List<MappingVector2> newPoints = new(AtoB.MapPoints.Length);

#if DEBUG
            //            List<Vector2> mapPointList = new List<Vector2>(newPoints.Count);
#endif

            int MinThreadPoints = 64;

            //            Trace.WriteLine("Starting with " + mapPoints.Length + " points", "Geometry"); 

            //    List<MappingVector2> newPoints = new List<MappingVector2>(); 

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
                List<int> listPoints = new(PointsPerThread);
                for (int iAddPoint = iPoint; iAddPoint < iPoint + PointsPerThread; iAddPoint++)
                {
                    //Don't add if the point is out of range
                    if (iAddPoint >= AtoB.MapPoints.Length)
                        break;

                    listPoints.Add(iAddPoint);
                }

                //MappingVector2 mapPoint = mapPoints[iPoint];
                AddTransformThreadObj AddThreadObj = null;
                try
                {
                    AddThreadObj = new AddTransformThreadObj([.. listPoints], AtoB, BtoC);

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
                    AddThreadObj?.Dispose();
                    AddThreadObj = null;

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
                    Debug.Assert(Vector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
                }
#endif
            }

            //Wait for the threads to finish processing.  There is a 64 handle limit for WaitAll so we wait on one at a time
            if (doneEvents.Count > 0)
                ManualResetEvent.WaitAll([.. doneEvents]);

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
                Debug.Assert(Vector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
            }
#endif

            MappingVector2.RemoveControlSpaceDuplicates(newPoints);
            MappingVector2.RemoveMappedSpaceDuplicates(newPoints);

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
                newTransform = new GridTransform([.. newPoints], new Rectangle(), gridTransform.GridSizeX, gridTransform.GridSizeY, info);
            }
            else
            {
                newTransform = new MeshTransform([.. newPoints], info);
            }

            //Optional, but useful step. In rare cases we lose some mappable space when the fixed transform are inside the control space of the mapped transform, but the triangulation of the mapped control points would eliminate these points
            //in these cases we can test if they can be added back in.

            System.Collections.Concurrent.ConcurrentBag<MappingVector2> MappableFixedPoints = [];

            if (BtoC is ITransformControlPoints BtoCTriTransform)
            {
                //We only check for points on the convex hull, this eliminates losing mappable area, but may not retain high warp correction areas.
                var BtoC_ControlPoints = BtoCTriTransform.MapPoints.Select(mp => mp.ControlPoint).ToArray();
                var BtoC_ConvexHullControlPoints = BtoC_ControlPoints.ConvexHull(out var originalIndicies);
                var BtoC_PointsOfConcern = originalIndicies.Select(i => BtoCTriTransform.MapPoints[i]).ToArray();

                Parallel.ForEach<MappingVector2>(BtoC_PointsOfConcern, FixedPointPair =>
                {
                    if (!newTransform.CanInverseTransform(FixedPointPair.ControlPoint) &&
                        AtoB.CanInverseTransform(FixedPointPair.MappedPoint))
                    {
                        Vector2 NewMapPoint = AtoB.InverseTransform(FixedPointPair.MappedPoint);
                        MappableFixedPoints.Add(new MappingVector2(FixedPointPair.ControlPoint, NewMapPoint));
                    }
                }
                );

                if (!MappableFixedPoints.IsEmpty)
                {
                    foreach (MappingVector2 newPoint in MappableFixedPoints)
                    {
                        bool add = true;
                        foreach (MappingVector2 oldPoint in newPoints)
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

                    //MappingVector2.RemoveDuplicates(newPoints);
                    newTransform = new MeshTransform([.. newPoints], info);
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
