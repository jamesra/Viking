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
    public abstract class TriangulationTransform : ReferencePointBasedTransform, IDisposable, IDiscreteTransform, IControlPointTriangulation, ISpatialIndexPrewarm
    {
        /// <summary>
        /// Return the control triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingTriangle? GetTransform(in Vector2 Point);

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal abstract MappingTriangle? GetInverseTransform(in Vector2 Point);

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
        public override bool CanTransform(in Vector2 Point) => GetTransform(Point).HasValue;

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2 Transform(in Vector2 Point)
        {
            return GetTransform(Point) is MappingTriangle t
                ? t.Transform(Point)
                : throw new ArgumentOutOfRangeException(nameof(Point), string.Format("Transform: Point could not be mapped {0}", Point.ToString()));
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2[] Transform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 p = Points[i];
                output[i] = GetTransform(p) is MappingTriangle t
                    ? t.Transform(p)
                    : throw new ArgumentOutOfRangeException(nameof(Points), string.Format("Transform: Point could not be mapped {0}", p.ToString()));
            }
            return output;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            if (GetTransform(Point) is not MappingTriangle t)
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
            output = new Vector2[Points.Length];
            bool[] IsTransformed = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                if (GetTransform(Points[i]) is MappingTriangle triangle)
                {
                    output[i] = triangle.Transform(Points[i]);
                    IsTransformed[i] = true;
                }
            }

            return IsTransformed;
        }

        #endregion

        #region InverseTransform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanInverseTransform(in Vector2 Point) => GetInverseTransform(Point).HasValue;

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2 InverseTransform(in Vector2 Point)
        {
            return GetInverseTransform(Point) is MappingTriangle t
                ? t.InverseTransform(Point)
                : throw new ArgumentOutOfRangeException(nameof(Point), string.Format("InverseTransform: Point could not be mapped {0}", Point.ToString()));
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Points"></param>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override Vector2[] InverseTransform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 p = Points[i];
                output[i] = GetInverseTransform(p) is MappingTriangle t
                    ? t.InverseTransform(p)
                    : throw new ArgumentOutOfRangeException(nameof(Points), string.Format("InverseTransform: Point could not be mapped {0}", p.ToString()));
            }
            return output;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            if (GetInverseTransform(Point) is not MappingTriangle t)
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
            output = new Vector2[Points.Length];
            bool[] IsTransformed = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                if (GetInverseTransform(Points[i]) is MappingTriangle triangle)
                {
                    output[i] = triangle.InverseTransform(Points[i]);
                    IsTransformed[i] = true;
                }
            }

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
        public abstract double ConvexHullIntersection(in LineSegment L, Vector2 OutsidePoint, out LineSegment foundCtrlLine, out LineSegment foundMapLine, out Vector2 intersection);

        #endregion

        #region Extra data cruft

        public List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingVector2> foundPoints = IntersectingRectangleRTree(gridRect, this.controlTrianglesRTree);
            if (!IncludeAdjacent)
            {
                for (int i = 0; i < foundPoints.Count; i++)
                {
                    if (!gridRect.Covers(foundPoints[i].ControlPoint))
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
                    if (!gridRect.Covers(foundPoints[i].MappedPoint))
                    {
                        foundPoints.RemoveAt(i);
                        i--;
                    }
                }
            }

            return foundPoints;
        }

        /// <summary>
        /// RTrees over the triangles, one per space, each built the first time it is needed and then read without a lock.
        /// </summary>
        /// <remarks>
        /// The fields are null until first use and are reset to null by <see cref="MinimizeMemory"/> and after
        /// deserialization ([NonSerialized]). A <see cref="Lazy{T}"/> in ExecutionAndPublication mode makes concurrent first
        /// callers wait for a single build. Grid transforms map forward by grid arithmetic and never touch the mapped-space
        /// tree for that, so the two trees are built independently.
        /// </remarks>
        [NonSerialized]
        private Lazy<RTree.RTree<MappingTriangle>> _mapTrianglesRTree;

        [NonSerialized]
        private Lazy<RTree.RTree<MappingTriangle>> _controlTrianglesRTree;

        /// <summary>
        /// RTree of triangles by their mapped-space bounding boxes
        /// </summary>
        public RTree.RTree<MappingTriangle> mapTrianglesRTree => GetOrCreateTree(ref _mapTrianglesRTree, mappedSpace: true).Value;

        /// <summary>
        /// RTree of triangles by their control-space bounding boxes
        /// </summary>
        public RTree.RTree<MappingTriangle> controlTrianglesRTree => GetOrCreateTree(ref _controlTrianglesRTree, mappedSpace: false).Value;

        private Lazy<RTree.RTree<MappingTriangle>> GetOrCreateTree(ref Lazy<RTree.RTree<MappingTriangle>> field, bool mappedSpace)
        {
            Lazy<RTree.RTree<MappingTriangle>> existing = Volatile.Read(ref field);
            if (existing != null)
                return existing;

            Lazy<RTree.RTree<MappingTriangle>> created = new(() => BuildTriangleRTree(mappedSpace), LazyThreadSafetyMode.ExecutionAndPublication);
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }

        /// <summary>
        /// Drops both RTrees so they are rebuilt from the current points on next use. Call after replacing <c>MapPoints</c>
        /// on a copy, which would otherwise share the original's trees.
        /// </summary>
        protected void ResetTriangleRTrees()
        {
            Interlocked.Exchange(ref _mapTrianglesRTree, null);
            Interlocked.Exchange(ref _controlTrianglesRTree, null);
        }

        /// <summary>
        /// The trees and the per-point triangle lists hold triangle bounds computed from the old points, for example after
        /// <c>Translate</c> moves the control points in place.
        /// </summary>
        protected override void OnMapPointsChanged()
        {
            base.OnMapPointsChanged();
            ResetTriangleRTrees();
            _TriangleList = null;
        }

        /// <summary>
        /// Starts building both RTrees on the thread pool, for a transform that is about to be queried. The returned task
        /// completes when both exist. Queries made before then wait for the build in progress instead of starting another.
        /// </summary>
        public Task PrewarmSpatialIndexAsync() => Task.Run(() =>
        {
            _ = controlTrianglesRTree;
            _ = mapTrianglesRTree;
        });

        /// <summary>One triangle in one of the two triangle RTrees (about 1,070 bytes per triangle for both).</summary>
        protected const long TriangleRTreeBytesPerTriangle = 535;
        /// <summary>The per-point triangle lists built by <see cref="BuildTriangleList"/>.</summary>
        protected const long TriangleListBytesPerPoint = 216;

        /// <summary>
        /// Bytes of triangle topology this transform owns: its triangle index array, plus edges in subclasses that keep
        /// them. Grid transforms share both per grid size and count none.
        /// </summary>
        protected virtual long EstimatedTopologyBytes => (_TriangleIndicies?.Length ?? 0) * (long)sizeof(int);

        /// <summary>Adds the triangle topology, per-point triangle lists and whichever triangle RTrees are built.</summary>
        public override long EstimatedMemoryBytes
        {
            get
            {
                long bytes = base.EstimatedMemoryBytes + EstimatedTopologyBytes;
                if (_TriangleList != null)
                    bytes += MapPoints.Length * TriangleListBytesPerPoint;

                long triangles = (_TriangleIndicies?.Length ?? 0) / 3;
                if (Volatile.Read(ref _mapTrianglesRTree) is { IsValueCreated: true })
                    bytes += triangles * TriangleRTreeBytesPerTriangle;
                if (Volatile.Read(ref _controlTrianglesRTree) is { IsValueCreated: true })
                    bytes += triangles * TriangleRTreeBytesPerTriangle;
                return bytes;
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

        /// <summary>
        /// Builds both RTrees now, if they do not exist yet.
        /// </summary>
        protected void BuildTriangleRTree()
        {
            _ = mapTrianglesRTree;
            _ = controlTrianglesRTree;
        }

        /// <summary>
        /// Builds the RTree for one space. Throws for a degenerate triangle, as building each triangle's <see cref="Triangle"/>
        /// for its bounding box used to.
        /// </summary>
        private RTree.RTree<MappingTriangle> BuildTriangleRTree(bool mappedSpace)
        {
            int[] triangleIndicies = this.TriangleIndicies;
            MappingVector2[] mapPoints = this.MapPoints;
            RTree.RTree<MappingTriangle> tree = new();

            for (int i = 0; i < triangleIndicies.Length; i += 3)
            {
                MappingTriangle t = new(mapPoints, triangleIndicies[i], triangleIndicies[i + 1], triangleIndicies[i + 2]);
                Vector2 a = mappedSpace ? mapPoints[t.N1].MappedPoint : mapPoints[t.N1].ControlPoint;
                Vector2 b = mappedSpace ? mapPoints[t.N2].MappedPoint : mapPoints[t.N2].ControlPoint;
                Vector2 c = mappedSpace ? mapPoints[t.N3].MappedPoint : mapPoints[t.N3].ControlPoint;
                Triangle.ThrowIfDegenerate(a, b, c);

                tree.Add((mappedSpace ? t.MappedBoundingBox : t.ControlBoundingBox).ToRTreeRect(0), t);
            }

            return tree;
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

            if (gridRect.Covers(pointTreeWithUniqueValues.Border))
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
            //A query already holding a tree keeps using it; the next query builds a new one.
            ResetTriangleRTrees();
            _TriangleList = null;

            Edges = null;

            base.MinimizeMemory();
            //this._LineSegmentGrid = null; 
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ResetTriangleRTrees();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }



        /// <summary>
        /// Below this many unmapped points, hull crossings are resolved on the calling thread. Section warps already run tiles
        /// in parallel, and most tiles have no unmapped points or only a handful.
        /// </summary>
        private const int ParallelEdgeResolutionThreshold = 256;

        /// <summary>
        /// Padding for the bounds test that filters hull candidates. Containment is inclusive at triangle edges, so the bounds
        /// test is padded to stay a strict superset of <see cref="ITransform.CanInverseTransform"/>.
        /// </summary>
        private const double HullCandidateBoundsPadding = 1.0;

        /// <summary>
        /// Takes two transforms and transforms the control grid of this section into the control grid space of the passed transfrom. Requires control section
        /// of this transform to match mapped section of adding transform
        /// </summary>
        /// <remarks>
        /// Each control point of <paramref name="AtoB"/> is mapped through <paramref name="BtoC"/> once. Points that do not map are
        /// replaced by the points where their edges cross BtoC's hull (<see cref="UnmappedEdgeResolver"/>). Safe to call
        /// concurrently for different <paramref name="AtoB"/> transforms sharing one <paramref name="BtoC"/>.
        /// </remarks>
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

            //Map every control point of the warping transform through the fixed transform once
            MappingVector2[] warpingPoints = AtoB.MapPoints;
            Vector2[] warpingControlPoints = new Vector2[warpingPoints.Length];
            for (int i = 0; i < warpingPoints.Length; i++)
                warpingControlPoints[i] = warpingPoints[i].ControlPoint;

            bool[] mapped = BtoC.TryTransform(warpingControlPoints, out Vector2[] mappedControlPoints);

            List<MappingVector2> newPoints = new(warpingPoints.Length);
            List<int> unmappedPoints = [];
            for (int i = 0; i < warpingPoints.Length; i++)
            {
                if (mapped[i])
                    newPoints.Add(new MappingVector2(mappedControlPoints[i], warpingPoints[i].MappedPoint));
                else
                    unmappedPoints.Add(i);
            }

            //This indicates if every original point was transformable.  If it is true and we started with a grid transform we then know the output can also be a grid transform
            bool AllPointsTransformed = unmappedPoints.Count == 0;

            //Edges from unmapped points are cut where they leave the fixed transform. Only a discrete fixed transform has a hull to cut against.
            if (!AllPointsTransformed && BtoC is IDiscreteTransform discreteBtoC)
            {
                if (unmappedPoints.Count < ParallelEdgeResolutionThreshold)
                {
                    foreach (int iPoint in unmappedPoints)
                        UnmappedEdgeResolver.AddHullCrossings(iPoint, AtoB, discreteBtoC, newPoints);
                }
                else
                {
                    object mergeLock = new();
                    Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, unmappedPoints.Count),
                        () => new List<MappingVector2>(),
                        (range, _, local) =>
                        {
                            for (int k = range.Item1; k < range.Item2; k++)
                                UnmappedEdgeResolver.AddHullCrossings(unmappedPoints[k], AtoB, discreteBtoC, local);
                            return local;
                        },
                        local =>
                        {
                            lock (mergeLock)
                                newPoints.AddRange(local);
                        });
                }
            }

            //Both passes sort, so the result does not depend on the order hull crossings were added in
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

            List<MappingVector2> MappableFixedPoints = [];

            if (BtoC is ITransformControlPoints BtoCTriTransform)
            {
                //We only check for points on the convex hull, this eliminates losing mappable area, but may not retain high warp correction areas.
                var BtoC_ControlPoints = BtoCTriTransform.MapPoints.Select(mp => mp.ControlPoint).ToArray();
                var BtoC_ConvexHullControlPoints = BtoC_ControlPoints.ConvexHull(out var originalIndicies);

                //A hull point can only be added if AtoB can inverse-map it, which needs it inside AtoB's control bounds. Testing
                //the bounds first skips the containment tests, and the RTree builds they trigger, for the hull points far from AtoB.
                Rectangle AtoBControlBounds = AtoB.ControlBounds;
                foreach (int iHull in originalIndicies)
                {
                    MappingVector2 FixedPointPair = BtoCTriTransform.MapPoints[iHull];
                    if (!AtoBControlBounds.Covers(FixedPointPair.MappedPoint, HullCandidateBoundsPadding))
                        continue;

                    if (!newTransform.CanInverseTransform(FixedPointPair.ControlPoint) &&
                        AtoB.CanInverseTransform(FixedPointPair.MappedPoint))
                    {
                        Vector2 NewMapPoint = AtoB.InverseTransform(FixedPointPair.MappedPoint);
                        MappableFixedPoints.Add(new MappingVector2(FixedPointPair.ControlPoint, NewMapPoint));
                    }
                }

                if (MappableFixedPoints.Count > 0)
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
