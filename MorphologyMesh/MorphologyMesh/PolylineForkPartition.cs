using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Allocates contiguous vertex ranges of a forking polyline to its linked partners on the opposite band.
    ///
    /// Where a polyline is linked to two or more polylines across the slice, the generator otherwise assigns
    /// verticies by nearest XY, so whichever partner happens to lie closer can absorb nearly the whole contour and
    /// the other partner is left with a sliver or nothing.  Cutting the contour where two neighbouring partners are
    /// equidistant from it instead gives every partner the stretch of contour it is actually nearest to, and leaves
    /// exactly one untiled contour segment between neighbouring ranges so the fork reads as a fork rather than a
    /// crease.
    ///
    /// Polygons are deliberately out of scope.  Their branch behavior already emerges from the region graph,
    /// correspondence, and the Theorem 2 orientation constraint, and a second allocator would compete with it.
    /// </summary>
    public sealed class PolylineForkPartition
    {
        /// <summary>
        /// Minimum verticies per partner range.  Two verticies span one contour segment, which is what a partner
        /// needs to receive a full quad (two triangles) rather than a single triangle.
        /// </summary>
        private const int MinVerticiesPerPartner = 2;

        /// <summary>
        /// Where a partner sits along the forking polyline, as the arc-length interval its contour projects onto.
        ///
        /// A partner is an extended contour, not a point, so the two ends of that interval are what decide the cut
        /// against its neighbours: the boundary belongs between the end of one partner and the start of the next.
        /// <see cref="Position"/> is the interval's midpoint and orders the partners along the contour.
        /// </summary>
        private readonly record struct PartnerAnchor(int Partner, double First, double Last, double Weight)
        {
            /// <summary>Continuous arc length representing the partner as a whole, used for ordering and tie detection.</summary>
            public double Position => (First + Last) / 2;
        }

        /// <summary>
        /// Two partners whose projections differ by less than this fraction of the contour's length are treated as
        /// sitting in the same place, so the midpoint rule between them would be meaningless noise.
        /// </summary>
        private const double PositionTieFraction = 1e-6;

        /// <summary>
        /// How far, in verticies, a boundary may be dragged by <see cref="EnforceMinimumRangeSizes"/> before the
        /// partition is worth reporting.  A displacement of a segment or two is the ordinary cost of snapping to
        /// whole verticies; more than that means the contour is too coarse for the fork it is carrying.
        /// </summary>
        private const int BoundaryDisplacementTraceThreshold = 2;

        /// <summary>
        /// For a forking shape, the inclusive vertex range each partner shape owns.
        /// Key is the forking shape index; the inner key is the partner shape index.
        /// </summary>
        private readonly Dictionary<int, Dictionary<int, (int First, int Last)>> _ranges = [];

        /// <summary>
        /// Verticies sitting on either side of a fork gap, keyed by shape index.  The contour segment between a
        /// consecutive pair of these is intentionally left untiled.
        /// </summary>
        private readonly Dictionary<int, HashSet<int>> _boundaryVerticies = [];

        /// <summary>
        /// Centre of the arc-length interval each partner projected onto.  Retained because the ranges
        /// are only meaningful against the positions that produced them, and a fork that hands a partner a range
        /// excluding its own projection is the failure this allocator exists to prevent.
        /// </summary>
        private readonly Dictionary<int, Dictionary<int, double>> _partnerArcLength = [];

        /// <summary>Arc length of each vertex of a forking polyline, keyed by shape index.</summary>
        private readonly Dictionary<int, double[]> _vertexArcLength = [];

        /// <summary>True when at least one shape was actually partitioned.</summary>
        public bool HasForks => _ranges.Count > 0;

        /// <summary>The shape indices that were partitioned.</summary>
        public IReadOnlyCollection<int> ForkedShapes => _ranges.Keys;

        /// <summary>
        /// True when the vertex of a forking polyline is allowed to chord to this partner.
        ///
        /// Shapes that were not partitioned, and partners that are not part of a partitioned shape's fork, are
        /// unaffected: the answer is yes, so the geometric tests decide as they always have.
        /// </summary>
        public bool AllowsChord(int iShape, int iVertex, int iPartnerShape)
        {
            if (_ranges.TryGetValue(iShape, out Dictionary<int, (int First, int Last)> byPartner) == false)
                return true;

            if (byPartner.TryGetValue(iPartnerShape, out (int First, int Last) range) == false)
                return true;

            return iVertex >= range.First && iVertex <= range.Last;
        }

        /// <summary>
        /// True when this vertex borders a deliberate fork gap, so the manifold report can tell the resulting
        /// single-face edge apart from a hole.
        /// </summary>
        public bool IsForkBoundaryVertex(int iShape, int iVertex) =>
            _boundaryVerticies.TryGetValue(iShape, out HashSet<int> verts) && verts.Contains(iVertex);

        /// <summary>Inclusive vertex range owned by a partner, for tests and diagnostics.</summary>
        public bool TryGetRange(int iShape, int iPartnerShape, out int first, out int last)
        {
            first = 0;
            last = 0;

            if (_ranges.TryGetValue(iShape, out Dictionary<int, (int First, int Last)> byPartner) == false)
                return false;

            if (byPartner.TryGetValue(iPartnerShape, out (int First, int Last) range) == false)
                return false;

            first = range.First;
            last = range.Last;
            return true;
        }

        /// <summary>
        /// Centre of the arc-length interval a partner projects onto, for tests and diagnostics.
        /// </summary>
        public bool TryGetPartnerArcLength(int iShape, int iPartnerShape, out double arcLength)
        {
            arcLength = 0;

            return _partnerArcLength.TryGetValue(iShape, out Dictionary<int, double> byPartner)
                && byPartner.TryGetValue(iPartnerShape, out arcLength);
        }

        /// <summary>Arc length of a vertex of a forking polyline, for tests and diagnostics.</summary>
        public bool TryGetVertexArcLength(int iShape, int iVertex, out double arcLength)
        {
            arcLength = 0;

            if (_vertexArcLength.TryGetValue(iShape, out double[] cumulative) == false)
                return false;

            if (iVertex < 0 || iVertex >= cumulative.Length)
                return false;

            arcLength = cumulative[iVertex];
            return true;
        }

        /// <summary>
        /// Build the partition for every polyline in the topology that forks to two or more linked polylines on the
        /// opposite band.  Returns null when nothing forks, so callers can skip the test entirely.
        /// </summary>
        /// <param name="shapes">Shapes indexed as in the topology.</param>
        /// <param name="isUpper">Band membership, indexed in lockstep with <paramref name="shapes"/>.</param>
        /// <param name="isLinked">Whether two shape indices are joined by a LocationLink.</param>
        /// <param name="sliceKey">Only used to make trace output identifiable.</param>
        public static PolylineForkPartition Create(IReadOnlyList<IShape2D> shapes, IReadOnlyList<bool> isUpper, Func<int, int, bool> isLinked, ulong sliceKey = 0)
        {
            if (shapes is null || isUpper is null || isLinked is null)
                return null;

            PolylineForkPartition partition = new();

            for (int iShape = 0; iShape < shapes.Count; iShape++)
            {
                if (shapes[iShape] is not Polyline line)
                    continue;

                //Only polyline partners are partitioned.  A polygon partner is handled by the region graph, and
                //carving up the polyline against it would fight that machinery.
                List<int> partners = [];
                for (int iOther = 0; iOther < shapes.Count; iOther++)
                {
                    if (iOther == iShape || isUpper[iOther] == isUpper[iShape])
                        continue;

                    if (shapes[iOther] is Polyline && isLinked(iShape, iOther))
                        partners.Add(iOther);
                }

                if (partners.Count < 2)
                    continue;

                partition.AddFork(iShape, line, partners, shapes, sliceKey);
            }

            return partition.HasForks ? partition : null;
        }

        private void AddFork(int iShape, Polyline line, List<int> partners, IReadOnlyList<IShape2D> shapes, ulong sliceKey)
        {
            double[] cumulative = CumulativeArcLength(line);
            int vertexCount = cumulative.Length;
            double totalLength = cumulative[vertexCount - 1];

            if (totalLength <= 0)
                return;

            //Without room for two verticies per partner plus a one-segment gap between neighbours, a fork would
            //hand somebody a lone triangle or an empty range.  Leave the shape alone and say so: link gating still
            //applies, so the result is the previous behavior rather than a broken partition.
            int required = (MinVerticiesPerPartner * partners.Count) + (partners.Count - 1);
            if (vertexCount < required)
            {
                Trace.WriteLine($"Slice {sliceKey}: polyline shape {iShape} forks to {partners.Count} partners but has only {vertexCount} verticies ({required} needed).  Not partitioning; nearest-vertex assignment still applies.");
                return;
            }

            List<PartnerAnchor> ordered = [];
            foreach (int iPartner in partners)
            {
                (double extentFirst, double extentLast) = ProjectExtentOntoArcLength(line, cumulative, shapes[iPartner]);
                double weight = shapes[iPartner] is Polyline partnerLine ? partnerLine.Length : 0;
                ordered.Add(new PartnerAnchor(iPartner, extentFirst, extentLast, Math.Max(weight, double.Epsilon)));
            }

            //Sorting by position keeps the ranges in contour order, so each partner's range is adjacent to the
            //partner it is actually next to in space.  Shape index breaks exact ties so the partition is
            //deterministic rather than dependent on the sort's handling of equal keys.
            ordered.Sort((a, b) =>
            {
                int byPosition = a.Position.CompareTo(b.Position);
                return byPosition != 0 ? byPosition : a.Partner.CompareTo(b.Partner);
            });

            double[] boundaryLength = ComputeBoundaryArcLengths(ordered, totalLength);

            //Partner k ends at the last vertex on its side of the boundary, partner k+1 starts at the next one,
            //leaving the segment between them untiled.
            int[] lastVertex = new int[ordered.Count];
            for (int k = 0; k < ordered.Count - 1; k++)
                lastVertex[k] = LastVertexAtOrBefore(cumulative, boundaryLength[k]);

            lastVertex[ordered.Count - 1] = vertexCount - 1;

            int[] requestedVertex = [.. lastVertex];

            if (EnforceMinimumRangeSizes(lastVertex, vertexCount) == false)
            {
                Trace.WriteLine($"Slice {sliceKey}: polyline shape {iShape} forks to {partners.Count} partners but its {vertexCount} verticies cannot be split with {MinVerticiesPerPartner} per partner.  Not partitioning.");
                return;
            }

            ReportDisplacedBoundaries(iShape, ordered, requestedVertex, lastVertex, sliceKey);

            Dictionary<int, (int First, int Last)> byPartner = [];
            HashSet<int> boundaries = [];

            int first = 0;
            for (int k = 0; k < ordered.Count; k++)
            {
                int last = lastVertex[k];
                byPartner[ordered[k].Partner] = (first, last);

                if (k < ordered.Count - 1)
                {
                    //Both ends of the untiled segment are fork boundary verticies.
                    boundaries.Add(last);
                    boundaries.Add(last + 1);
                }

                first = last + 1;
            }

            _ranges[iShape] = byPartner;
            _boundaryVerticies[iShape] = boundaries;
            _vertexArcLength[iShape] = cumulative;
            _partnerArcLength[iShape] = ordered.ToDictionary(o => o.Partner, o => o.Position);

            Trace.WriteLine($"Slice {sliceKey}: polyline shape {iShape} partitioned across {ordered.Count} partners: " +
                            string.Join(", ", byPartner.Select(kvp => $"{kvp.Key}=[{kvp.Value.First}..{kvp.Value.Last}]")));
        }

        /// <summary>
        /// Arc length of the cut between each pair of neighbouring partners.
        ///
        /// The cut sits halfway between the end of one partner's projected extent and the start of the next, which
        /// is where the two partner contours are equidistant from the forking polyline.  Every vertex therefore
        /// goes to whichever partner is genuinely nearer, so no chord is longer than it needs to be, while
        /// restricting the assignment to contiguous ranges keeps the anti-sliver guarantee an unconstrained
        /// nearest-partner rule would lose.
        ///
        /// Taking the midpoint of the partners' centres instead would cut short of the true equidistant point
        /// whenever the partners are long, because a long partner's near edge reaches much further along the
        /// contour than its centre does.
        ///
        /// Partners projecting to the same place carry no positional information, so a run of them splits the span
        /// its neighbours leave free in proportion to partner length instead.
        /// </summary>
        private static double[] ComputeBoundaryArcLengths(List<PartnerAnchor> ordered, double totalLength)
        {
            int count = ordered.Count;
            double[] boundaryLength = new double[Math.Max(count - 1, 0)];
            double epsilon = totalLength * PositionTieFraction;

            int k = 0;
            while (k < count)
            {
                int end = k;
                while (end + 1 < count && ordered[end + 1].Position - ordered[end].Position <= epsilon)
                    end++;

                if (end > k)
                {
                    double spanStart = k == 0 ? 0 : FacingMidpoint(ordered[k - 1], ordered[k]);
                    double spanEnd = end == count - 1 ? totalLength : FacingMidpoint(ordered[end], ordered[end + 1]);

                    double weightSum = 0;
                    for (int t = k; t <= end; t++)
                        weightSum += ordered[t].Weight;

                    double runningWeight = 0;
                    for (int t = k; t < end; t++)
                    {
                        runningWeight += ordered[t].Weight;
                        boundaryLength[t] = spanStart + ((spanEnd - spanStart) * (runningWeight / weightSum));
                    }
                }

                if (end < count - 1)
                    boundaryLength[end] = FacingMidpoint(ordered[end], ordered[end + 1]);

                k = end + 1;
            }

            //Partners whose projected extents overlap can produce a cut behind the previous one.  Clamping keeps the
            //cuts ordered so the ranges below stay contiguous rather than inverted.
            for (int t = 1; t < boundaryLength.Length; t++)
                boundaryLength[t] = Math.Max(boundaryLength[t], boundaryLength[t - 1]);

            return boundaryLength;
        }

        /// <summary>Halfway between the trailing end of the earlier partner and the leading end of the later one.</summary>
        private static double FacingMidpoint(PartnerAnchor earlier, PartnerAnchor later) => (earlier.Last + later.First) / 2;

        /// <summary>
        /// Trace boundaries that the minimum-size sweeps had to drag well away from the position they were computed
        /// at, so a fork forced onto a contour too coarse to carry it is diagnosable rather than silent.
        /// </summary>
        private static void ReportDisplacedBoundaries(int iShape, List<PartnerAnchor> ordered, int[] requestedVertex, int[] lastVertex, ulong sliceKey)
        {
            for (int k = 0; k < lastVertex.Length - 1; k++)
            {
                int displacement = Math.Abs(lastVertex[k] - requestedVertex[k]);
                if (displacement <= BoundaryDisplacementTraceThreshold)
                    continue;

                Trace.WriteLine($"Slice {sliceKey}: polyline shape {iShape} fork boundary between partners {ordered[k].Partner} and {ordered[k + 1].Partner} moved {displacement} verticies (vertex {requestedVertex[k]} -> {lastVertex[k]}) to satisfy the {MinVerticiesPerPartner} vertex minimum.  Chords to these partners will be longer than the contour positions ask for.");
            }
        }

        /// <summary>
        /// Push boundaries apart until every range holds at least <see cref="MinVerticiesPerPartner"/> verticies.
        /// Returns false when the polyline simply is not long enough for that to be possible.
        /// </summary>
        private static bool EnforceMinimumRangeSizes(int[] lastVertex, int vertexCount)
        {
            int count = lastVertex.Length;

            //Forward sweep: guarantee each range starts far enough after the previous boundary.
            int minLast = MinVerticiesPerPartner - 1;
            for (int k = 0; k < count; k++)
            {
                if (lastVertex[k] < minLast)
                    lastVertex[k] = minLast;

                minLast = lastVertex[k] + MinVerticiesPerPartner;
            }

            //Backward sweep: the last range must end on the final vertex, so pull boundaries back if the forward
            //sweep pushed them past the end.
            lastVertex[count - 1] = vertexCount - 1;
            for (int k = count - 2; k >= 0; k--)
            {
                int maxLast = lastVertex[k + 1] - MinVerticiesPerPartner;
                if (lastVertex[k] > maxLast)
                    lastVertex[k] = maxLast;
            }

            //Validate rather than trust the sweeps: a polyline that is too short fails here instead of emitting
            //an inverted or empty range that would silently drop a partner.
            int first = 0;
            for (int k = 0; k < count; k++)
            {
                if (lastVertex[k] - first + 1 < MinVerticiesPerPartner)
                    return false;

                first = lastVertex[k] + 1;
            }

            return true;
        }

        /// <summary>Arc length from the first vertex to each vertex, so index i maps to a distance along the line.</summary>
        private static double[] CumulativeArcLength(Polyline line)
        {
            IReadOnlyList<IPoint2D> points = line.Points;
            double[] cumulative = new double[points.Count];

            for (int i = 1; i < points.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1].ToVector2(), points[i].ToVector2());

            return cumulative;
        }

        /// <summary>
        /// Where a partner sits along the polyline, expressed as an arc length.
        ///
        /// Every vertex of the partner is projected perpendicularly onto the nearest segment of the forking
        /// polyline, and the extent is the span those projections cover.  The nearest approach between two contours
        /// is not usable on its own here: where a partner runs alongside the polyline, every point of that stretch
        /// is an equally near approach, so the minimum is a whole interval and picking one point out of it would be
        /// arbitrary.  The interval is the answer.
        ///
        /// Arc length is interpolated inside the winning segment rather than snapped to a vertex, so the result is
        /// continuous and two partners cannot collapse onto one index and then sort arbitrarily.
        ///
        /// A shape with no vertices to walk, a polygon partner among polylines, falls back to its centre, which
        /// gives a zero-width extent and the old midpoint-of-centres behavior for that partner.
        /// </summary>
        private static (double First, double Last) ProjectExtentOntoArcLength(Polyline line, double[] cumulative, IShape2D partner)
        {
            if (partner is not Polyline partnerLine)
            {
                double centre = ProjectPointOntoArcLength(line, cumulative, SliceTopology.ShapeCenter(partner));
                return (centre, centre);
            }

            double first = double.MaxValue;
            double last = double.MinValue;

            foreach (IPoint2D point in partnerLine.Points)
            {
                double projected = ProjectPointOntoArcLength(line, cumulative, point.ToVector2());
                first = Math.Min(first, projected);
                last = Math.Max(last, projected);
            }

            return (first, last);
        }

        /// <summary>
        /// Arc length of the point on the polyline nearest the target, interpolated within the winning segment.
        /// </summary>
        private static double ProjectPointOntoArcLength(Polyline line, double[] cumulative, Vector2 target)
        {
            IReadOnlyList<IPoint2D> points = line.Points;

            double nearestDistance = double.MaxValue;
            double nearestArcLength = 0;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 start = points[i - 1].ToVector2();
                Vector2 end = points[i].ToVector2();

                double dX = end.X - start.X;
                double dY = end.Y - start.Y;
                double lengthSquared = (dX * dX) + (dY * dY);

                double t = lengthSquared <= 0 ? 0 : ((((target.X - start.X) * dX) + ((target.Y - start.Y) * dY)) / lengthSquared);
                t = Math.Min(1, Math.Max(0, t));

                Vector2 closest = new(start.X + (dX * t), start.Y + (dY * t));
                double distance = Vector2.DistanceSquared(closest, target);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestArcLength = cumulative[i - 1] + ((cumulative[i] - cumulative[i - 1]) * t);
                }
            }

            return nearestArcLength;
        }

        /// <summary>
        /// Last vertex lying on the near side of a boundary arc length.
        ///
        /// Snapping to the closest vertex in either direction would be wrong here: a vertex past the boundary is by
        /// definition nearer the next partner, so handing it to this one lengthens its chords.  Taking the last
        /// vertex at or before the cut assigns every vertex to the partner it is actually closer to.
        /// </summary>
        private static int LastVertexAtOrBefore(double[] cumulative, double targetLength)
        {
            int last = 0;
            for (int i = 0; i < cumulative.Length; i++)
            {
                if (cumulative[i] > targetLength)
                    break;

                last = i;
            }

            return last;
        }
    }
}
