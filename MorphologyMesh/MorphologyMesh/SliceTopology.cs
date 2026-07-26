using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Geometry;

namespace MorphologyMesh
{
    /// <summary>
    /// Describes the shapes and relationships for a given slice
    /// </summary>
    public readonly struct SliceTopology
    {
        //TODO: Document limitations for shapes we know should not link to each other in the final model by using LocationLink entries.

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
        /// The translation vector to position this slice in world space.
        /// </summary>
        //public readonly GridVector2 Offset;

        public SliceTopology(ulong key, IEnumerable<IShape2D> shapes, IEnumerable<bool> isUpper, IEnumerable<double> shapeZ, IEnumerable<ulong> shapeIndexToMorphNodeIndex, double sliceThickness = double.NaN)
            : this(shapes, isUpper, shapeZ, shapeIndexToMorphNodeIndex, sliceThickness)
        {
            SliceKey = key;
        }

        public SliceTopology(IEnumerable<IShape2D> shapes, IEnumerable<bool> isUpper, IEnumerable<double> shapeZ, IEnumerable<ulong> shapeIndexToMorphNodeIndex = null, double sliceThickness = double.NaN)
        {
            SliceKey = 0;

            //GridVector2 Center = shapes.BoundingBox().Center;

            //Translate all shapes as close to the origin as possible.  We'll move them back once we assemble the mesh.
            //Polygons = shapes.Select(p => p.Translate(-Center)).ToArray();
            //Offset = Center;
            Shapes = [.. shapes];
            IsUpper = [.. isUpper];
            ShapeZ = [.. shapeZ];

            ShapeIndexToMorphNodeIndex = shapeIndexToMorphNodeIndex?.ToArray();

            //These arrays are indexed in lockstep by shape index.  A mismatch means a caller filtered one of them
            //without filtering the others, which silently attributes a shape's Z or upper/lower flag to a different
            //shape.  Fail loudly rather than build a mesh from scrambled correspondence.
            if (IsUpper.Length != Shapes.Length)
                throw new ArgumentException($"IsUpper has {IsUpper.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(isUpper));

            if (ShapeZ.Length != Shapes.Length)
                throw new ArgumentException($"ShapeZ has {ShapeZ.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeZ));

            if (ShapeIndexToMorphNodeIndex is not null && ShapeIndexToMorphNodeIndex.Length != Shapes.Length)
                throw new ArgumentException($"ShapeIndexToMorphNodeIndex has {ShapeIndexToMorphNodeIndex.Length} entries for {Shapes.Length} shapes.  These must be indexed in lockstep.", nameof(shapeIndexToMorphNodeIndex));

            //Assign polys to sets for convenience later
            CalculateUpperAndLowerPolygons(IsUpper, Shapes, out UpperShapes, out UpperShapeIndicies, out LowerShapes, out LowerShapeIndicies);

            //Use the calculated value if we can, otherwise use the default if it is provided, if we have neither, then throw an exception
            var calculatedThickness = CalculateSliceThickness(ShapeZ);
            SliceThickness = double.IsNaN(calculatedThickness) ? sliceThickness : calculatedThickness;
            if (double.IsNaN(SliceThickness))
                throw new ArgumentException("A slice thickness must be specified if it cannot be calculated");

            this.SliceCenterZ = CalculateSliceCenter(SliceThickness, LowerShapeIndicies, UpperShapeIndicies, ShapeZ);
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
        internal static List<GridVector2> NudgeCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            Dictionary<GridVector2, List<PolygonIndex>> pointToIndexList = new(GridVector2EqualityComparer.Default);
            //GridPolygon[] Polygons = this.Polygons;

            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)// GridPolygon poly in Polygons)
            {
                GridPolygon poly = Polygons[iPoly];
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                for (int i = 0; i < correspondingIndicies.Count; i++)
                {
                    PolygonIndex pi = correspondingIndicies[i];
                    GridVector2 cp = pi.Point(poly);

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

            List<GridVector2> UpdatedPoints = [];

            foreach (GridVector2 cp in pointToIndexList.Keys)
            {
                List<PolygonIndex> correspondingIndicies = pointToIndexList[cp];

                GridVector2[] points = [.. correspondingIndicies.Select(ci => ci.PredictPoint(Polygons))];
                GridVector2 avg = points.Average();

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
                SortedList<PointIndex, GridVector2> PointsToInsert = new SortedList<PointIndex, GridVector2>();

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
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
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
        internal static void RemoveAdjacentCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
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
        internal static void HandleCorrespondingFaceContainsVertex(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            GridRectangle bbox = Polygons.BoundingBox();
            bbox = GridRectangle.Scale(bbox, 1.05); //Grow the box slightly so the QuadTreeWithUniqueValues will never resize for a rounding error
            QuadTreeWithUniqueValues<List<PolygonIndex>> treeWithUniqueValues = new(bbox);

            PolySetVertexEnum indexEnum = new(Polygons);
            foreach (PolygonIndex index in indexEnum)
            {
                GridVector2 p = index.Point(Polygons);

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

            var PointIndexArrays = Polygons.IndiciesForPoints(correspondingPoints);

            foreach (PolygonIndex[] indicies in PointIndexArrays)
            {
                if (indicies.Length == 0)
                    continue;

                double minDistance;
                GridVector2 vertexPosition = indicies[0].Point(Polygons); //The corresponding point position
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
                    GridPolygon poly = pi.Polygon(Polygons);

                    GridVector2 vertex = pi.Point(Polygons);
                    GridVector2 next = pi.Next.Point(Polygons);
                    GridVector2 prev = pi.Previous.Point(Polygons);

                    if (nearestIndex.Contains(pi.Next) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        GridLineSegment lineToNext = new GridLine(vertex, next).ToLine(minDistance);
                        poly.AddVertex(lineToNext.B);
                    }

                    if (nearestIndex.Contains(pi.Previous) == false) //Don't add a vertex that is already there and risk a rounding error
                    {
                        GridLineSegment lineToPrev = new GridLine(vertex, prev).ToLine(minDistance);
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
        internal static void BracketCorrespondingPoints(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, GridVector2> PointsToInsert = [];
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search. 
            }
        }

        /// <summary>
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(GridPolygon[] Polygons, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolygon poly in Polygons)
            {
                List<PolygonIndex> correspondingIndicies = poly.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<PolygonIndex, GridVector2> PointsToInsert = [];
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
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(Current.Previous.Point(poly),
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
        /// Due to details of the implementation of our bajaj algorithm we need to add a point between adjacent corresponding points on a polygon
        /// </summary>
        internal static void AddPointsBetweenAdjacentCorrespondingVerticies(GridPolyline[] Polylines, List<GridVector2> correspondingPoints)
        {
            foreach (GridPolyline line in Polylines)
            {
                List<PolylineIndex> correspondingIndicies = line.TryGetIndicies(correspondingPoints);
                if (correspondingIndicies is null || correspondingIndicies.Count == 0)
                    continue;

                SortedList<int, GridVector2> PointsToInsert = [];
                correspondingIndicies.Sort(); //Sort the indicies so we can simplify our search.

                PolylineIndex Current = correspondingIndicies[0];
                for (long i = 0; i < correspondingIndicies.Count - 1; i++)
                {
                    PolylineIndex Next = correspondingIndicies[(int)i + 1];

                    if (Current.Next == Next)
                    {
                        //This means two corresponding points are adjacent and we need to insert a midpoint into the polygon between them.
                        GridVector2[] midPoint = CatmullRom.FitCurveSegment(line.Points,
                            Current.iVertex,
                            [0.5]
                        );

                        //Adding the point will change index of all PointIndex values so we wait until the end
                        PointsToInsert.Add(Current.iVertex, midPoint[0]);
                    }

                    Current = Next;
                }

                //Reverse the order of our list of points to add so we do not break polygon indicies.  Then insert our points
                foreach (var addition in PointsToInsert.Reverse())
                {
                    //Trace.WriteLine(string.Format("Add vertex after {0}", addition));
                    //Insert the vertex, adjust the size of the ring in case we've already inserted into it.
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

    }
}