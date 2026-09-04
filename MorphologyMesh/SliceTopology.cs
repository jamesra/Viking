using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMesh
{
    /// <summary>
    /// Describes the shapes and relationships for a given slice
    /// </summary>
    public readonly struct SliceTopology
    {
        /// <summary>
        /// The slice key if this topology was generated from a Slice node in a SliceGraph
        /// </summary>
        public readonly ulong SliceKey;

        /// <summary>
        /// All shapes in the topology
        /// </summary>
        public readonly IShape2D[] Shapes;

        /// <summary>
        /// True if the polygon belongs to the upper set of shapes
        /// </summary>
        public readonly bool[] IsUpper;

        /// <summary>
        /// Z level of each Polygon
        /// </summary>
        public readonly double[] ShapeZ;

        /// <summary>
        /// Map Polygons[] index from this topology to the morphology node key generating that polygon in the parent SliceGraph
        /// </summary>
        public readonly ulong[] ShapeIndexToMorphNodeIndex;

        /// <summary>
        /// Location type of each shape in <see cref="Shapes"/>, indexed in lockstep.
        /// Circle annotations stay polygons in <see cref="Shapes"/> but are capped with a scaled circle, not a medial-axis dome.
        /// </summary>
        public readonly LocationType[] ShapeLocationTypes;

        /// <summary>
        /// Volume-space circle for each shape when <see cref="ShapeLocationTypes"/> is <see cref="LocationType.CIRCLE"/>,
        /// in the same translated XY frame as <see cref="Shapes"/>. Null when no circle metadata was supplied.
        /// </summary>
        public readonly Circle[] ShapeCircles;

        /// <summary>
        /// Map a UpperPolygons index to Polygons index
        /// </summary>
        public readonly ImmutableSortedSet<int> UpperShapeIndicies;

        /// <summary>
        /// Map a LowerPolygons index to Polygons index
        /// </summary>
        public readonly ImmutableSortedSet<int> LowerShapeIndicies;

        /// <summary>
        /// Set of Polygons in the upper set
        /// </summary>
        internal readonly IShape2D[] UpperShapes;

        /// <summary>
        /// Set of Polygons in the lower set
        /// </summary>
        internal readonly IShape2D[] LowerShapes;

        /// <summary>
        /// How thick this slice is
        /// </summary>
        public readonly double SliceThickness;

        /// <summary>
        /// True when this topology was constructed with valid shape data.
        /// A default-constructed SliceTopology (produced when topology initialisation fails) has Shapes = null and IsValid = false.
        /// </summary>
        public bool IsValid => Shapes is not null;

        /// <summary>
        /// Center of the slice in Z axis
        /// </summary>
        public readonly double SliceCenterZ;

        /// <summary>
        /// Per-shape XY translation applied so linked shapes that do not overlap can still generate Bajaj slice
        /// chords, indexed in lockstep with <see cref="Shapes"/>. The inverse is applied to the verticies of each
        /// shape after faces exist. Null when no shape in this slice was translated.
        ///
        /// A fork translates each non-overlapping partner by a different amount, so this cannot be a single vector:
        /// pulling every partner onto the forking shape's centroid would stack the partners on each other.
        /// </summary>
        public readonly Vector2[] VirtualOverlapOffsets;

        /// <summary>
        /// True when any shape in this slice was moved to create a virtual overlap.
        /// </summary>
        public bool HasVirtualOverlapTranslation => VirtualOverlapOffsets is not null;

        /// <summary>
        /// The translation applied to a single shape, or <see cref="Vector2.Zero"/> when that shape did not move.
        /// </summary>
        public Vector2 GetVirtualOverlapOffset(int shapeIndex)
        {
            if (VirtualOverlapOffsets is null || shapeIndex < 0 || shapeIndex >= VirtualOverlapOffsets.Length)
                return Vector2.Zero;

            return VirtualOverlapOffsets[shapeIndex];
        }

        /// <summary>
        /// [i,j] is true when shapes i and j are joined by a LocationLink.
        ///
        /// A slice is built by expanding along Z links until no new nodes appear, so a chain that doubles back
        /// (A z1 - B z2 - C z1 - D z2 - E z1) collapses into one slice with A, C, E below and B, D above.  Bajaj
        /// then sees five shapes with no record of which pairs the annotator actually joined and is free to tile
        /// A to D.  Polygons are partly shielded from this because a chord over empty space is classified FLYING
        /// and discarded, but that protection disappears once the unlinked pair overlaps in XY, and polylines have
        /// no equivalent protection at all: any chord between two polylines that misses every other shape is
        /// SURFACE regardless of length.
        ///
        /// Null when the topology was built without a Slice, in which case every pair is treated as linked so
        /// hand-constructed topologies keep their previous behavior.
        /// </summary>
        private readonly bool[,] ShapesAreLinked;

        /// <summary>
        /// Vertex ranges allocated to each partner where a polyline forks to two or more linked polylines across the
        /// slice, or null when nothing in this topology forks.  See <see cref="PolylineForkPartition"/>.
        /// </summary>
        public readonly PolylineForkPartition ForkPartition;

        /// <summary>
        /// True when a chord between these two shapes is permitted by the annotator's LocationLinks.
        /// </summary>
        public bool IsLinked(int iA, int iB)
        {
            if (ShapesAreLinked is null)
                return true;

            if (iA < 0 || iB < 0 || iA >= Shapes.Length || iB >= Shapes.Length)
                return false;

            return ShapesAreLinked[iA, iB];
        }

        /// <summary>
        /// True when the link matrix is known, so callers can tell "every pair allowed because we have no data"
        /// apart from "every pair allowed because the annotator linked them all".
        /// </summary>
        public bool HasLinkData => ShapesAreLinked is not null;

        /// <summary>
        /// True when a chord or face may join these two shapes.
        ///
        /// Only cross-band pairs are gated.  A LocationLink joins annotations on adjacent sections, so two shapes
        /// sitting in the same band are almost never linked to each other, and rejecting them would throw away every
        /// legitimate same-level face: region closing, medial-axis caps, and the end caps all build faces whose
        /// verticies come from one band.  Tiling across the slice is the only decision LocationLinks speak to.
        /// </summary>
        public bool MayTile(int iA, int iB)
        {
            if (iA == iB)
                return true;

            if (ShapesAreLinked is null)
                return true;

            if (iA < 0 || iB < 0 || iA >= Shapes.Length || iB >= Shapes.Length)
                return false;

            if (IsUpper[iA] == IsUpper[iB])
                return true;

            return ShapesAreLinked[iA, iB];
        }

        /// <param name="virtualOverlapOffsets">
        /// Per-shape virtual overlap translations already applied to <paramref name="shapes"/>, indexed in lockstep
        /// with them, or null to let the constructor compute them.
        /// </param>
        public SliceTopology(ulong key, IEnumerable<IShape2D> shapes, IEnumerable<bool> isUpper, IEnumerable<double> shapeZ, IEnumerable<ulong> shapeIndexToMorphNodeIndex, double sliceThickness = double.NaN, Vector2[] virtualOverlapOffsets = null, bool[,] shapesAreLinked = null, bool buildForkPartition = false, IEnumerable<LocationType> shapeLocationTypes = null, IEnumerable<Circle> shapeCircles = null)
            : this(shapes, isUpper, shapeZ, shapeIndexToMorphNodeIndex, sliceThickness, virtualOverlapOffsets, shapesAreLinked, buildForkPartition, key, shapeLocationTypes, shapeCircles)
        {
            SliceKey = key;
        }

        /// <param name="virtualOverlapOffsets">
        /// Per-shape virtual overlap translations already applied to <paramref name="shapes"/>, indexed in lockstep
        /// with them, or null to let the constructor compute them.
        /// </param>
        public SliceTopology(IEnumerable<IShape2D> shapes, IEnumerable<bool> isUpper, IEnumerable<double> shapeZ, IEnumerable<ulong> shapeIndexToMorphNodeIndex = null, double sliceThickness = double.NaN, Vector2[] virtualOverlapOffsets = null, bool[,] shapesAreLinked = null, bool buildForkPartition = false, ulong sliceKeyForTrace = 0, IEnumerable<LocationType> shapeLocationTypes = null, IEnumerable<Circle> shapeCircles = null)
        {
            SliceKey = 0;

            //Vector2 Center = shapes.BoundingBox().Center;

            //Translate all shapes as close to the origin as possible.  We'll move them back once we assemble the mesh.
            //Polygons = shapes.Select(p => p.Translate(-Center)).ToArray();
            //Offset = Center;
            Shapes = [.. shapes];
            IsUpper = [.. isUpper];
            ShapeZ = [.. shapeZ];

            ShapeIndexToMorphNodeIndex = shapeIndexToMorphNodeIndex?.ToArray();

            int shapeCount = Shapes.Length;
            ShapeLocationTypes = shapeLocationTypes?.ToArray()
                ?? [.. Enumerable.Repeat(LocationType.POLYGON, shapeCount)];
            ShapeCircles = shapeCircles?.ToArray();

            //These arrays are indexed in lockstep by shape index.  A mismatch means a caller filtered one of them
            //without filtering the others, which silently attributes a shape's Z or upper/lower flag to a different
            //shape.  Fail loudly rather than build a mesh from scrambled correspondence.
            if (IsUpper.Length != Shapes.Length)
                throw new ArgumentException($"IsUpper has {IsUpper.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(isUpper));

            if (ShapeZ.Length != Shapes.Length)
                throw new ArgumentException($"ShapeZ has {ShapeZ.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeZ));

            if (ShapeIndexToMorphNodeIndex is not null && ShapeIndexToMorphNodeIndex.Length != Shapes.Length)
                throw new ArgumentException($"ShapeIndexToMorphNodeIndex has {ShapeIndexToMorphNodeIndex.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeIndexToMorphNodeIndex));

            if (ShapeLocationTypes.Length != Shapes.Length)
                throw new ArgumentException($"ShapeLocationTypes has {ShapeLocationTypes.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeLocationTypes));

            if (ShapeCircles is not null && ShapeCircles.Length != Shapes.Length)
                throw new ArgumentException($"ShapeCircles has {ShapeCircles.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeCircles));

            if (shapesAreLinked is not null &&
                (shapesAreLinked.GetLength(0) != Shapes.Length || shapesAreLinked.GetLength(1) != Shapes.Length))
                throw new ArgumentException($"shapesAreLinked is {shapesAreLinked.GetLength(0)}x{shapesAreLinked.GetLength(1)} for {Shapes.Length} shapes.  It must be square and indexed in lockstep.", nameof(shapesAreLinked));

            ShapesAreLinked = shapesAreLinked;

            if (virtualOverlapOffsets is not null && virtualOverlapOffsets.Length != Shapes.Length)
                throw new ArgumentException($"virtualOverlapOffsets has {virtualOverlapOffsets.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(virtualOverlapOffsets));

            VirtualOverlapOffsets = virtualOverlapOffsets ?? TryTranslateNonOverlappingShapes(Shapes, IsUpper, shapesAreLinked);

            //Assign polys to sets for convenience later
            CalculateUpperAndLowerPolygons(IsUpper, Shapes, out UpperShapes, out UpperShapeIndicies, out LowerShapes, out LowerShapeIndicies);

            //Use the calculated value if we can, otherwise use the default if it is provided, if we have neither, then throw an exception
            var calculatedThickness = CalculateSliceThickness(ShapeZ);
            SliceThickness = double.IsNaN(calculatedThickness) ? sliceThickness : calculatedThickness;
            if (double.IsNaN(SliceThickness))
                throw new ArgumentException("A slice thickness must be specified if it cannot be calculated");

            this.SliceCenterZ = CalculateSliceCenter(SliceThickness, LowerShapeIndicies, UpperShapeIndicies, ShapeZ);

            //Built last because it reads the finished shape array, band flags, and link matrix.  Shapes must already
            //have every corresponding and intersection vertex inserted, or the ranges would refer to indices that
            //shift out from under them.
            ForkPartition = buildForkPartition
                ? PolylineForkPartition.Create(Shapes, IsUpper, MayTile, sliceKeyForTrace)
                : null;
        }

        private static double CalculateSliceThickness(IEnumerable<double> polyZ)
        {
            double MinZ = polyZ.Min(); //Pick the largest of the low-end Z values
            double MaxZ = polyZ.Max(); //Pick the smallest of the high-end Z values

            return MinZ == MaxZ ? double.NaN : Math.Abs(MaxZ - MinZ);
        }

        private static double CalculateSliceCenter(double SliceThickness, ImmutableSortedSet<int> LowerPolyIndicies, ImmutableSortedSet<int> UpperPolyIndicies, double[] PolyZ)
        {
            if (LowerPolyIndicies.Count == 0)
            {
                double MinZ = UpperPolyIndicies.Select(i => PolyZ[i]).Min(); //Pick the largest of the low-end Z values
                return MinZ - (SliceThickness / 2.0);
            }
            else if (UpperPolyIndicies.Count == 0)
            {
                double MaxZ = LowerPolyIndicies.Select(i => PolyZ[i]).Max(); //Pick the largest of the low-end Z values
                return MaxZ + (SliceThickness / 2.0);
            }
            else
            {
                //Center the slice on the mid-plane between the top of the lower set and the bottom of the upper set.
                double LowerMaxZ = LowerPolyIndicies.Select(i => PolyZ[i]).Max(); //Top of the lower set
                double UpperMinZ = UpperPolyIndicies.Select(i => PolyZ[i]).Min(); //Bottom of the upper set
                return (LowerMaxZ + UpperMinZ) / 2.0;
            }
        }

        /// <summary>
        /// The delaunay implementation floating point rounding errors are most common on colinear points.  To mitigate this I nudge corresponding points to match the expected curvature of the shape the correlate with
        /// </summary>
        internal static List<Vector2> NudgeCorrespondingVerticies(Polygon[] Polygons, List<Vector2> correspondingPoints)
        {
            Dictionary<Vector2, List<PolygonIndex>> pointToIndexList = new();
            //Polygon[] Polygons = this.Polygons;

            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)// Polygon poly in Polygons)
            {
                Polygon poly = Polygons[iPoly];
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndices(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                for (int i = 0; i < correspondingIndicies.Count; i++)
                {
                    PolygonIndex pi = correspondingIndicies[i];
                    Vector2 cp = pi.Point(poly);

                    if (pointToIndexList.TryGetValue(cp, out var indexList))
                    {
                        indexList.Add(pi.Reindex(iPoly));
                    }
                    else
                    {
                        indexList =
                        [
                            pi.Reindex(iPoly)
                        ];
                        pointToIndexList.Add(cp, indexList);
                    }
                }
            }

            List<Vector2> UpdatedPoints = [];

            foreach (Vector2 cp in pointToIndexList.Keys)
            {
                List<PolygonIndex> correspondingIndicies = pointToIndexList[cp];

                Vector2[] points = [.. correspondingIndicies.Select(ci => ci.PredictPoint(Polygons))];
                Vector2 avg = points.Average();

                try
                {
                    foreach (PolygonIndex pi in correspondingIndicies)
                    {
                        pi.SetPoint(Polygons, avg);
                    }

                    UpdatedPoints.Add(avg);
                }
                catch (ArgumentException e)
                {
                    foreach (PolygonIndex pi in correspondingIndicies)
                    {
                        pi.SetPoint(Polygons, cp);
                    }

                    UpdatedPoints.Add(cp);
                }

            }

            return UpdatedPoints;
        }
        /*
                SortedList<PointIndex, Vector2> PointsToInsert = new SortedList<PointIndex, Vector2>();

                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                IIndexSet loopingIndex = new InfiniteSequentialIndexSet(0, correspondingIndicies.Count, 0);
                PointIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count; i++)
                {
                    int iNext = (int)loopingIndex[i + 1];
                    PointIndex Next = correspondingIndicies[iNext];

                    if (Current.Next == Next)
                    {
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        Vector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
                                                   Current.Point(poly),
                                                   Next.Point(poly),
                                                   Next.Next.Point(poly),
                                                   new double[] { 0.5 });

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current, midPoint[0]);
                    }

                    Current = Next;
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach (var addition in PointsToInsert.Reverse())
                {
                    poly.AddVertex(addition.Value, addition.Key);
                }
            }
        }
        */


        /// <summary>
        /// We need to handle the case where a single vertex is on the other side of the contour boundary and creates
        /// two corresponding vertices which are tightly grouped
        /// 
        //       3
        ///     / \
        /// A--2-B-4--C
        ///   /     \
        ///  1       5
        /// </summary>
        internal static void RemoveAdjacentCorrespondingVerticies(Polygon[] Polygons, List<Vector2> correspondingPoints)
        {
            foreach (Polygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndices(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

            }
        }

        /// <summary>
        /// We need to handle the case where the face generated for a corresponding edge will contain other verticies.
        /// We can do this by subdividing the edge between 1-2 and A-B
        /// 
        ///        3---4
        ///       /     \
        /// A----2B---C--D5
        /// | X /         \
        /// |  /           \
        /// | /             \
        /// 1                6
        /// </summary>
        internal static void HandleCorrespondingFaceContainsVertex(Polygon[] Polygons, List<Vector2> correspondingPoints)
        {
            Rectangle bbox = Polygons.BoundingBox();
            bbox = Rectangle.Scale(bbox, 1.05); //Grow the box slightly so the QuadTreeWithUniqueValues will never resize for a rounding error
            QuadTreeWithUniqueValues<List<PolygonIndex>> treeWithUniqueValues = new(bbox);

            PolySetVertexEnum indexEnum = new(Polygons);
            foreach (PolygonIndex index in indexEnum)
            {
                Vector2 p = index.Point(Polygons);

                treeWithUniqueValues.TryFindNearest(p, out var existing, out double distance);
                if (distance < Global.Epsilon) //A corresponding point has already been added
                {
                    existing.Add(index);
                }
                else
                {
                    existing = new List<PolygonIndex>(2)
                    {
                        index
                    };
                    treeWithUniqueValues.Add(p, existing);
                }
            }

            var PointIndexArrays = Polygons.IndicesForPoints(correspondingPoints);

            foreach (PolygonIndex[] indicies in PointIndexArrays)
            {
                if (indicies.Length == 0)
                    continue;

                double minDistance;
                Vector2 vertexPosition = indicies[0].Point(Polygons); //The corresponding point position
                var nearestIndexList = treeWithUniqueValues.FindNearestPoints(vertexPosition, 2); //Find the nearest two points. The first should be ourselves at 0 distance.  The 2nd should be the closest point to us.

                if (nearestIndexList.Count < 2)
                {
                    throw new InvalidOperationException("We should be able to find at least two points when searching a QuadTreeWithUniqueValues containing multiple shapes");
                }

                Debug.Assert(nearestIndexList[0].Point == vertexPosition, "I expected the vertex to be the closest vertex to itself, why wasn't it found?");

                minDistance = nearestIndexList[1].Distance;
                var nearestIndex = nearestIndexList[1].Value;

                foreach (PolygonIndex pi in indicies)
                {
                    Polygon poly = pi.Polygon(Polygons);

                    Vector2 vertex = pi.Point(Polygons);
                    Vector2 next = pi.Next.Point(Polygons);
                    Vector2 prev = pi.Previous.Point(Polygons);

                    if (nearestIndex.Contains(pi.Next) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        LineSegment lineToNext = new Line(vertex, next).ToLine(minDistance);
                        poly.AddVertex(lineToNext.B);
                    }

                    if (nearestIndex.Contains(pi.Previous) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        LineSegment lineToPrev = new Line(vertex, prev).ToLine(minDistance);
                        poly.AddVertex(lineToPrev.B);
                    }
                }
            }
        }

        /// <summary>
        /// We need to handle the case where the face generated for a corresponding edge will contain other verticies.
        /// We can do this by subdividing the edge between 1-2 and A-B
        /// 
        ///        3---4
        ///       /     \
        /// A----2B---C--D5
        /// | X /         \
        /// |  /           \
        /// | /             \
        /// 1                6
        /// 
        /// This implementation simply adds additional verticies that bracket the corresponding vertex at equidistant points to the nearest non-corresponding vertex
        /// </summary>
        internal static void BracketCorrespondingPoints(Polygon[] Polygons, List<Vector2> correspondingPoints)
        {
            foreach (Polygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndices(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, Vector2> PointsToInsert = [];
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search. 
            }
        }

        /// <summary>
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(Polygon[] Polygons, List<Vector2> correspondingPoints)
        {
            foreach (Polygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndices(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, Vector2> PointsToInsert = [];
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                IIndexSet loopingIndex = new InfiniteSequentialIndexSet(0, correspondingIndicies.Count, 0);
                PolygonIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count; i++)
                {
                    int iNext = (int)loopingIndex[i + 1];
                    PolygonIndex Next = correspondingIndicies[iNext];

                    if (Current.Next == Next)
                    {
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        Vector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
                            Current.Point(poly),
                            Next.Point(poly),
                            Next.Next.Point(poly),
                            [0.5]);

                        if (midPoint[0] == Current.Previous.Point(poly) ||
                            midPoint[0] == Current.Point(poly) ||
                            midPoint[0] == Next.Point(poly))
                        {
                            //TODO: Explore this fairly rare case and understand why we get identical points in the corresponding points
                            Current = Next;
                            continue;
                            //throw new ArgumentException("Midpoint is a duplicate");
                        }

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current.Next, midPoint[0]);
                    }

                    Current = Next;
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach (var addition in PointsToInsert.Reverse())
                {
                    //Trace.WriteLine(string.Format("Add vertex after {0}", addition));
                    //Insert the vertex, adjust the size of the ring in case we've already inserted into it.
                    poly.InsertVertex(addition.Value, addition.Key.ReindexToSize(poly));
                }
            }
        }

        /// <summary>
        /// Bajaj tiling needs a vertex between two corresponding points that already share a contour edge.
        /// Inserts the chord midpoint before Next. Inserting at Current would split the previous edge;
        /// a chord stays on the existing segment so a simple polyline cannot self-intersect.
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(Polyline[] Polylines, List<Vector2> correspondingPoints)
        {
            foreach (Polyline line in Polylines)
            {
                List<PolylineIndex> correspondingIndicies = line.TryGetIndices(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<int, Vector2> PointsToInsert = [];
                correspondingIndicies.Sort();

                PolylineIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count - 1; i++)
                {
                    PolylineIndex Next = correspondingIndicies[(int)i + 1];

                    if (Current.Next == Next)
                    {
                        Vector2 mid = (Current.Point(line) + Next.Point(line)) * 0.5;
                        if (mid == Current.Point(line) || mid == Next.Point(line))
                        {
                            Current = Next;
                            continue;
                        }

                        PointsToInsert.Add(Next.VertexIndex, mid);
                    }

                    Current = Next;
                }

                foreach (var addition in PointsToInsert.Reverse())
                {
                    line.Insert(addition.Key, addition.Value);
                }
            }
        }

        private static void CalculateUpperAndLowerPolygons(bool[] IsUpper, IShape2D[] Shapes, out IShape2D[] UpperPolygons, out ImmutableSortedSet<int> UpperShapeIndicies, out IShape2D[] LowerPolygons, out ImmutableSortedSet<int> LowerShapeIndicies)
        {
            int nUpper = IsUpper.Count(u => u == true);
            int nLower = IsUpper.Count(u => u == false);

            UpperPolygons = new IShape2D[nUpper];
            LowerPolygons = new IShape2D[nLower];

            int[] UpperShapeIndex = new int[nUpper];
            int[] LowerShapeIndex = new int[nLower];

            int iUpper = 0;
            int iLower = 0;
            for (int i = 0; i < IsUpper.Length; i++)
            {
                if (IsUpper[i])
                {
                    UpperPolygons[iUpper] = Shapes[i];
                    UpperShapeIndex[iUpper] = i;
                    iUpper += 1;
                }
                else
                {
                    LowerPolygons[iLower] = Shapes[i];
                    LowerShapeIndex[iLower] = i;
                    iLower += 1;
                }
            }

            UpperShapeIndicies = [.. UpperShapeIndex];
            LowerShapeIndicies = [.. LowerShapeIndex];

            return;
        }

        /// <summary>
        /// How deeply the initial placement drives a partner's bounding box into its parent's, as a fraction of the
        /// smaller box's extent on the axis being closed.  Also the step by which the depth escalates when the boxes
        /// overlap but the contours inside them still do not.
        /// </summary>
        private const double VirtualOverlapBoxDepth = 0.25;

        /// <summary>
        /// Ceiling on the escalated depth.  Past a few multiples of the smaller box the shapes are interlocking rather
        /// than merely offset, and pushing further distorts the arrangement more than an untiled region costs.
        /// </summary>
        private const double VirtualOverlapMaxBoxDepth = 4.0;

        /// <summary>
        /// Shapes joined by a LocationLink have unambiguous correspondence even when they do not overlap in XY, but
        /// Bajaj classifies every chord across the gap as FLYING and leaves a break.  Translate the non-overlapping
        /// shapes toward the shape they are linked to so tiling can run.  Callers restore vertex XY after faces exist.
        ///
        /// Two arrangements are handled:
        ///  - One shape linked to a single partner: the partner moves onto that shape's centroid, since with nothing
        ///    else in the slice there is no arrangement to preserve.
        ///  - One shape linked to N partners (a fork): each non-overlapping partner moves only far enough to overlap
        ///    the forking shape.  Collapsing them onto its centroid would stack the partners on top of each other and
        ///    produce crossing chords, whereas a minimal move keeps their angular separation around the fork.
        ///
        /// The slice is left untouched whenever the outcome would be ambiguous: a shape that both forks and is a
        /// partner of another fork, or partners that would collide with each other once moved.
        /// </summary>
        /// <param name="shapes">Translated in place.  Untouched when the routine declines.</param>
        /// <param name="shapesAreLinked">LocationLink matrix, or null to treat every cross-band pair as linked.</param>
        /// <returns>Per-shape offsets indexed in lockstep with <paramref name="shapes"/>, or null when nothing moved.</returns>
        internal static Vector2[] TryTranslateNonOverlappingShapes(IShape2D[] shapes, bool[] isUpper, bool[,] shapesAreLinked = null)
        {
            if (shapes is null || isUpper is null || shapes.Length != isUpper.Length || shapes.Length < 2)
                return null;

            if (shapesAreLinked is not null &&
                (shapesAreLinked.GetLength(0) != shapes.Length || shapesAreLinked.GetLength(1) != shapes.Length))
                return null;

            List<int>[] partners = LinkedPartnersByShape(shapes.Length, isUpper, shapesAreLinked);

            int[] forks = [.. Enumerable.Range(0, shapes.Length).Where(i => partners[i].Count >= 2)];

            //A shape that forks and is itself a partner of another fork has no fixed frame to be measured against:
            //moving it invalidates the offsets computed for the other fork's partners.  Pinning the fork centres
            //instead of declining was tried and is worse: in a polyline chain it leaves the middle shape tiling to
            //neither of its linked partners (PolylineForkTests.WSequence_LinkedPairsStillTile).
            for (int a = 0; a < forks.Length; a++)
            {
                for (int b = a + 1; b < forks.Length; b++)
                {
                    if (partners[forks[a]].Contains(forks[b]) == false)
                        continue;

                    System.Diagnostics.Trace.WriteLine($"Virtual overlap declined: shapes {forks[a]} and {forks[b]} both fork and are linked to each other.");
                    return null;
                }
            }

            IShape2D[] working = [.. shapes];
            Vector2[] offsets = new Vector2[shapes.Length];
            bool anyMoved = false;

            //Iterate in shape order so the offsets a slice produces do not depend on enumeration order.
            int[] centers = forks.Length > 0 ? forks : DegenerateLinkedPair(shapes.Length, isUpper, partners);
            bool moveToCentroid = forks.Length == 0;

            foreach (int center in centers)
            {
                if (TryTranslatePartners(center, partners[center], shapes, working, offsets, moveToCentroid) == false)
                    return null;

                anyMoved |= partners[center].Any(p => offsets[p] != Vector2.Zero);
            }

            if (anyMoved == false)
                return null;

            for (int i = 0; i < shapes.Length; i++)
                shapes[i] = working[i];

            System.Diagnostics.Trace.WriteLine($"Virtual overlap: translated {offsets.Count(o => o != Vector2.Zero)} non-overlapping shape(s) so Bajaj can tile, then restore after faces.");
            return offsets;
        }

        /// <summary>
        /// Cross-band shapes the annotator linked to each shape, ascending.  Same-band pairs are never candidates:
        /// virtual overlap exists to make a chord across the slice possible.
        /// </summary>
        private static List<int>[] LinkedPartnersByShape(int count, bool[] isUpper, bool[,] shapesAreLinked)
        {
            List<int>[] partners = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                partners[i] = [];
                for (int j = 0; j < count; j++)
                {
                    if (i == j || isUpper[i] == isUpper[j])
                        continue;

                    if (shapesAreLinked is null || shapesAreLinked[i, j])
                        partners[i].Add(j);
                }
            }

            return partners;
        }

        /// <summary>
        /// The historical 1:1 case: exactly one upper and one lower shape, linked.  The upper shape is the fixed
        /// frame so the lower shape is the one that moves, matching what the restore pass has always undone.
        /// </summary>
        private static int[] DegenerateLinkedPair(int count, bool[] isUpper, List<int>[] partners)
        {
            if (count != 2 || partners[0].Count != 1 || partners[1].Count != 1)
                return [];

            return isUpper[0] ? [0] : [1];
        }

        /// <summary>
        /// Translates every non-overlapping partner of <paramref name="center"/>, or reports false when the result
        /// would be ambiguous and the whole slice should be left alone.
        /// </summary>
        private static bool TryTranslatePartners(int center, List<int> partners, IShape2D[] original, IShape2D[] working, Vector2[] offsets, bool moveToCentroid)
        {
            Vector2 centerPoint = ShapeCenter(working[center]);

            //The depth each partner ended up at, so a partner that has to be re-placed searches below the depth that
            //actually reached the parent rather than below the depth the search started from.
            Dictionary<int, double> placedDepth = [];

            foreach (int partner in partners)
            {
                if (offsets[partner] != Vector2.Zero)
                {
                    System.Diagnostics.Trace.WriteLine($"Virtual overlap declined: shape {partner} is claimed by more than one fork.");
                    return false;
                }

                if (working[partner].Intersects(working[center]))
                    continue;

                if (TryPlaceOverlapping(working[partner], working[center], centerPoint, moveToCentroid, out Vector2 offset, out double depth) == false)
                {
                    System.Diagnostics.Trace.WriteLine($"Virtual overlap declined: shape {partner} cannot be made to overlap shape {center}.");
                    return false;
                }

                if (offset == Vector2.Zero)
                    continue;

                working[partner] = working[partner].Translate(offset);
                offsets[partner] = offset;
                placedDepth[partner] = depth;
            }

            ReduceDepthUntilSiblingsClear(center, partners, original, working, offsets, placedDepth);

            //Two contours on one section do not tile to each other, so an overlap the annotator did not draw invents
            //correspondence verticies between siblings and corrupts the region graph.  Declining the slice is the
            //lesser evil.  The box placement makes the smallest move that works, so this is now a rare backstop
            //rather than the common outcome it was when every partner was driven along the centroid line.
            foreach (int partner in partners)
            {
                foreach (int other in partners)
                {
                    if (other <= partner)
                        continue;

                    if (original[partner].Intersects(original[other]))
                        continue;

                    if (working[partner].Intersects(working[other]) == false)
                        continue;

                    System.Diagnostics.Trace.WriteLine($"Virtual overlap declined: shapes {partner} and {other} would collide after translation toward shape {center}.");

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Granularity of the search for a shallower placement.  Fixed so the result does not vary between runs.
        /// </summary>
        private const int VirtualOverlapDepthSteps = 16;

        /// <summary>
        /// Re-places any partner that landed on a sibling, using a shallower box overlap.
        ///
        /// Escalation only ever pushes deeper, so a partner whose usable window is narrower than the depth it settled
        /// at has no way back on its own.  Backing the depth off keeps the direction the boxes chose and gives up only
        /// the penetration that was not needed.  The search runs below that settled depth rather than below the depth
        /// the original search started from: an escalated partner needed the extra depth to reach its parent at all,
        /// so anything under the starting depth cannot reach it either.  A partner with no workable depth is left
        /// where it is for the caller to reject the slice.
        /// </summary>
        private static void ReduceDepthUntilSiblingsClear(
            int center,
            List<int> partners,
            IShape2D[] original,
            IShape2D[] working,
            Vector2[] offsets,
            Dictionary<int, double> placedDepth)
        {
            foreach (int partner in partners)
            {
                if (offsets[partner] == Vector2.Zero)
                    continue;

                //A partner placed by the centroid fallback has no depth to reduce.
                if (placedDepth.TryGetValue(partner, out double settledDepth) == false || double.IsNaN(settledDepth))
                    continue;

                int[] obstacles = [.. partners
                    .Where(other => other != partner)
                    .Where(other => original[partner].Intersects(original[other]) == false)
                    .Where(other => working[partner].Intersects(working[other]))];

                if (obstacles.Length == 0)
                    continue;

                for (int step = VirtualOverlapDepthSteps - 1; step >= 1; step--)
                {
                    Vector2 candidateOffset = AabbOverlapTranslation(
                        original[partner], working[center], settledDepth * step / VirtualOverlapDepthSteps);

                    IShape2D candidate = original[partner].Translate(candidateOffset);
                    if (OverlapsInterior(candidate, working[center]) == false)
                        continue;

                    if (obstacles.Any(other => candidate.Intersects(working[other])))
                        continue;

                    working[partner] = candidate;
                    offsets[partner] = candidateOffset;
                    break;
                }
            }
        }

        /// <summary>
        /// Finds the offset that makes <paramref name="moving"/> genuinely overlap <paramref name="target"/>.
        ///
        /// The placement is derived from the two bounding boxes rather than searched for along the line joining their
        /// centres.  Closing the boxes on the axes that are actually separated is the shortest move that can work, and
        /// the shortest move is what keeps a forked partner from being driven across a sibling parked on the parent.
        /// A centroid-directed move has no such property: it aims every partner at the same point.
        ///
        /// Boxes overlapping does not mean the contours inside them do, so the depth escalates until the shapes report
        /// a real crossing.  Escalation is bounded, and a partner the boxes cannot resolve falls back to the centroid
        /// move before the slice is given up on.
        /// </summary>
        /// <param name="targetCenter">Centre of <paramref name="target"/>, already computed by the caller.</param>
        /// <param name="moveToCentroid">
        /// Set for the 1:1 case, where there is no sibling arrangement to preserve and maximum overlap is safe.
        /// </param>
        /// <returns>False when no offset produces an overlap and the slice should be left alone.</returns>
        private static bool TryPlaceOverlapping(IShape2D moving, IShape2D target, Vector2 targetCenter, bool moveToCentroid, out Vector2 offset, out double depthUsed)
        {
            depthUsed = double.NaN;

            if (moveToCentroid == false)
            {
                for (double depth = VirtualOverlapBoxDepth; depth <= VirtualOverlapMaxBoxDepth; depth += VirtualOverlapBoxDepth)
                {
                    Vector2 candidate = AabbOverlapTranslation(moving, target, depth);
                    if (candidate == Vector2.Zero)
                        break;

                    if (OverlapsInterior(moving.Translate(candidate), target))
                    {
                        offset = candidate;
                        depthUsed = depth;
                        return true;
                    }
                }
            }

            offset = targetCenter - ShapeCenter(moving);
            if (offset == Vector2.Zero)
                return true;

            return OverlapsInterior(moving.Translate(offset), target);
        }

        /// <summary>
        /// True when the shapes share area rather than merely touching at a boundary.  <see cref="IShape2D.Intersects"/>
        /// is <c>GetRelation != None</c>, so it accepts tangency, and a tangent pair yields the near-coincident
        /// correspondence verticies that crash divide-and-conquer Delaunay.  Asking the relation directly is what
        /// removes the need to guess a safety margin on top of a first-contact answer.
        /// </summary>
        private static bool OverlapsInterior(IShape2D a, IShape2D b)
        {
            ShapeRelation relation = a.GetRelation(b);
            return relation != ShapeRelation.None && relation != ShapeRelation.Touching;
        }

        /// <summary>
        /// The translation that drives <paramref name="moving"/>'s bounding box into <paramref name="target"/>'s by
        /// <paramref name="depthFraction"/> of the smaller box's extent, on each axis where the two are separated.
        /// Closed form, so no search over candidate positions is needed.
        /// </summary>
        private static Vector2 AabbOverlapTranslation(IShape2D moving, IShape2D target, double depthFraction)
        {
            var a = moving.BoundingBox;
            var b = target.BoundingBox;

            //A polyline's box is flat on one axis, which drives that axis' span to zero and lands the shapes exactly
            //tangent instead of overlapping.  The smaller diagonal stands in as a size no real contour can zero out.
            double degenerateScale = Math.Min(ShapeDiagonal(moving), ShapeDiagonal(target));

            double dx = AxisOverlapMove(a.Left, a.Right, b.Left, b.Right, depthFraction, degenerateScale);
            double dy = AxisOverlapMove(a.Bottom, a.Top, b.Bottom, b.Top, depthFraction, degenerateScale);

            return new Vector2(dx, dy);
        }

        /// <summary>
        /// Signed move along one axis that takes [aMin,aMax] from disjoint to overlapping [bMin,bMax].  Zero when the
        /// intervals already overlap, since box overlap needs both axes and an axis that already overlaps costs
        /// nothing to keep.  Depth is measured against the shorter of the two spans so a small contour is not driven
        /// through a large one.
        /// </summary>
        private static double AxisOverlapMove(double aMin, double aMax, double bMin, double bMax, double depthFraction, double degenerateScale)
        {
            double span = Math.Min(aMax - aMin, bMax - bMin);
            double depth = depthFraction * (span > 0 ? span : degenerateScale);

            if (aMax <= bMin)
                return (bMin - aMax) + depth;

            if (aMin >= bMax)
                return (bMax - aMin) - depth;

            return 0;
        }

        private static double ShapeDiagonal(IShape2D shape)
        {
            var bounds = shape.BoundingBox;
            return Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height));
        }

        internal static Vector2 ShapeCenter(IShape2D shape) =>
            shape is ICentroid centroid ? centroid.Centroid.ToVector2() : shape.BoundingBox.Center;
    }
}
