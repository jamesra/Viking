using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// One side of a pen edit spliced into a control ring (see <see cref="ControlRingCurve.TrySplice"/>).
    /// </summary>
    public sealed class SplicedControlRing
    {
        /// <summary>The new control ring, closed (the first point repeats at the end).</summary>
        public Vector2[] ControlRing { get; }

        /// <summary>
        /// For each point of <see cref="ControlRing"/>, the index of the original control point it is, or -1 for a new point.
        /// A caller that stored the original ring in another space can reuse those points exactly instead of mapping them back.
        /// </summary>
        public int[] OriginalIndex { get; }

        /// <summary>
        /// The dense outline the control ring was fitted to, closed: the kept part of the original curve and the pen stretch.
        /// </summary>
        public Vector2[] ReferenceRing { get; }

        internal SplicedControlRing(Vector2[] controlRing, int[] originalIndex, Vector2[] referenceRing)
        {
            ControlRing = controlRing;
            OriginalIndex = originalIndex;
            ReferenceRing = referenceRing;
        }
    }

    /// <summary>
    /// A closed ring of Catmull-Rom control points and the curve drawn through them. A pen stroke that crosses the curve twice
    /// replaces the stretch of curve between the crossings: control points on the kept side are kept exactly as they are, and
    /// new control points are taken only from the pen stroke and from the old curve next to the crossings, as many as the
    /// curve needs to stay within a tolerance of the stroke and of the old curve (<see cref="CatmullRomControlPointSimplification.RefineControlPoints"/>).
    /// Fitting the saved points again from the drawn outline instead moves the whole outline a little at every edit.
    /// </summary>
    public sealed class ControlRingCurve
    {
        private readonly Vector2[] controls;
        private readonly List<Vector2> dense = [];
        private readonly Dictionary<int, int> controlAtDenseIndex = new();
        private readonly uint numInterpolations;

        /// <param name="controlRing">The control points. A closing repeat of the first point is ignored.</param>
        /// <param name="numInterpolations">Samples per span, as the curve is drawn (more are added where it bends).</param>
        /// <exception cref="ArgumentException">Fewer than three distinct control points.</exception>
        public ControlRingCurve(IReadOnlyList<Vector2> controlRing, uint numInterpolations)
        {
            int count = controlRing.Count;
            if (count > 1 && controlRing[0] == controlRing[count - 1])
                count--;

            controls = [.. controlRing.Take(count)];
            if (controls.Length < 3)
                throw new ArgumentException("A control ring needs at least three points.", nameof(controlRing));

            this.numInterpolations = numInterpolations;
            int n = controls.Length;
            for (int k = 0; k < n; k++)
            {
                Vector2[] span = CatmullRom.RecursivelyFitCurveSegment(controls[(k - 1 + n) % n], controls[k], controls[(k + 1) % n], controls[(k + 2) % n], numInterpolations);
                controlAtDenseIndex[dense.Count] = k;
                dense.Add(controls[k]);
                // The span's first and last samples are its two control points; the next span adds the last one exactly.
                for (int i = 1; i < span.Length - 1; i++)
                    dense.Add(span[i]);
            }
        }

        /// <summary>The curve as drawn, closed, with every control point on it exactly.</summary>
        public Vector2[] Curve => [.. dense, dense[0]];

        /// <summary>The index of the control point at point <paramref name="curveIndex"/> of <see cref="Curve"/>, or -1 when that point is between control points.</summary>
        public int ControlIndexAt(int curveIndex) =>
            controlAtDenseIndex.TryGetValue(curveIndex % dense.Count, out int control) ? control : -1;

        /// <summary>Where the pen crosses the curve: along the pen (vertex index plus fraction) and on which curve segment.</summary>
        private readonly struct Crossing(double penPosition, int denseSegment, double denseFraction, Vector2 point)
        {
            public readonly double PenPosition = penPosition;
            public readonly int DenseSegment = denseSegment;
            public readonly double DenseFraction = denseFraction;
            public readonly Vector2 Point = point;
        }

        /// <summary>
        /// Splices the stretch of <paramref name="penPath"/> between its first two crossings of the curve into the ring, once for
        /// each side of the cut.
        /// </summary>
        /// <param name="penPath">The pen stroke, in the ring's units.</param>
        /// <param name="tolerance">Largest distance allowed between the new curve and the stroke or the kept old curve.</param>
        /// <param name="keepsArcAfterFirstCrossing">
        /// The ring that keeps the old curve running forward (in control point order) from the first crossing to the second:
        /// the side <see cref="Polygon.WalkPolygonCut(Polygon, RotationDirection, IList{Vector2})"/> calls counterclockwise.
        /// </param>
        /// <param name="keepsArcAfterSecondCrossing">The ring that keeps the rest of the old curve.</param>
        /// <returns>False when the stroke does not cross the curve twice.</returns>
        public bool TrySplice(IReadOnlyList<Vector2> penPath, double tolerance, out SplicedControlRing keepsArcAfterFirstCrossing, out SplicedControlRing keepsArcAfterSecondCrossing)
        {
            keepsArcAfterFirstCrossing = null;
            keepsArcAfterSecondCrossing = null;
            if (penPath is null || penPath.Count < 2 || !TryFindFirstTwoCrossings(penPath, out Crossing first, out Crossing second))
                return false;

            keepsArcAfterFirstCrossing = BuildSide(penPath, first, second, tolerance, keepArcAfterFirst: true);
            keepsArcAfterSecondCrossing = BuildSide(penPath, first, second, tolerance, keepArcAfterFirst: false);
            return keepsArcAfterFirstCrossing is not null || keepsArcAfterSecondCrossing is not null;
        }

        private bool TryFindFirstTwoCrossings(IReadOnlyList<Vector2> penPath, out Crossing first, out Crossing second)
        {
            first = default;
            second = default;
            int d = dense.Count;
            List<Crossing> found = [];
            for (int j = 0; j < penPath.Count - 1 && found.Count < 2; j++)
            {
                Vector2 a = penPath[j];
                Vector2 b = penPath[j + 1];
                double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X), minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
                List<Crossing> onSegment = [];
                for (int e = 0; e < d; e++)
                {
                    Vector2 c = dense[e];
                    Vector2 f = dense[(e + 1) % d];
                    if (Math.Max(c.X, f.X) < minX || Math.Min(c.X, f.X) > maxX || Math.Max(c.Y, f.Y) < minY || Math.Min(c.Y, f.Y) > maxY)
                        continue;

                    if (TryIntersect(a, b, c, f, out double t, out double u))
                        onSegment.Add(new Crossing(j + t, e, u, new Vector2(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y))));
                }

                // A crossing through a curve vertex is found on both segments that meet there; keep one
                foreach (Crossing crossing in onSegment.OrderBy(c => c.PenPosition))
                {
                    if (found.Count == 0 || Vector2.DistanceSquared(found[found.Count - 1].Point, crossing.Point) > Tolerance.EpsilonSquared)
                        found.Add(crossing);
                }
            }

            if (found.Count < 2)
                return false;

            first = found[0];
            second = found[1];
            return true;
        }

        private static bool TryIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out double t, out double u)
        {
            t = u = 0;
            double rx = b.X - a.X, ry = b.Y - a.Y;
            double sx = d.X - c.X, sy = d.Y - c.Y;
            double denominator = rx * sy - ry * sx;
            if (Math.Abs(denominator) < 1e-12)
                return false;

            double qx = c.X - a.X, qy = c.Y - a.Y;
            t = (qx * sy - qy * sx) / denominator;
            u = (qx * ry - qy * rx) / denominator;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        private sealed class RingBuilder
        {
            public readonly List<Vector2> Points = [];
            public readonly List<int> Original = [];
            public readonly List<bool> IsControl = [];

            /// <summary>Appends a point, merging it into the previous one when they coincide.</summary>
            public void Add(Vector2 point, int original, bool isControl)
            {
                int last = Points.Count - 1;
                if (last >= 0 && Vector2.DistanceSquared(Points[last], point) <= Tolerance.EpsilonSquared)
                {
                    if (original >= 0)
                    {
                        Points[last] = point;
                        Original[last] = original;
                    }

                    IsControl[last] |= isControl;
                    return;
                }

                Points.Add(point);
                Original.Add(original);
                IsControl.Add(isControl);
            }
        }

        /// <summary>
        /// Builds one side: one crossing, the kept arc of the old curve to the other crossing, then the pen stretch back. The
        /// crossings and the kept control points start as control points; the refinement adds the rest.
        /// </summary>
        /// <returns>Null when the side has no area.</returns>
        private SplicedControlRing BuildSide(IReadOnlyList<Vector2> penPath, Crossing first, Crossing second, double tolerance, bool keepArcAfterFirst)
        {
            Crossing from = keepArcAfterFirst ? first : second;
            Crossing to = keepArcAfterFirst ? second : first;
            int d = dense.Count;

            RingBuilder ring = new();
            ring.Add(from.Point, -1, isControl: true);

            // The old curve forward from one crossing to the other
            int arcCount = (to.DenseSegment - from.DenseSegment + d) % d;
            if (arcCount == 0 && to.DenseFraction < from.DenseFraction)
                arcCount = d;

            for (int i = 1; i <= arcCount; i++)
            {
                int index = (from.DenseSegment + i) % d;
                bool isControl = controlAtDenseIndex.TryGetValue(index, out int original);
                ring.Add(dense[index], isControl ? original : -1, isControl);
            }

            ring.Add(to.Point, -1, isControl: true);

            // The pen stretch, walked from the second crossing back to the first or from the first to the second
            int firstVertex = (int)Math.Floor(first.PenPosition) + 1;
            int lastVertex = (int)Math.Ceiling(second.PenPosition) - 1;
            IEnumerable<int> penVertices = Enumerable.Range(firstVertex, Math.Max(0, lastVertex - firstVertex + 1));
            if (keepArcAfterFirst)
                penVertices = penVertices.Reverse();

            foreach (int i in penVertices)
                ring.Add(penPath[i], -1, isControl: false);

            // Closing onto the starting crossing
            int closing = ring.Points.Count - 1;
            if (closing > 0 && Vector2.DistanceSquared(ring.Points[closing], ring.Points[0]) <= Tolerance.EpsilonSquared)
            {
                ring.Points.RemoveAt(closing);
                ring.Original.RemoveAt(closing);
                ring.IsControl.RemoveAt(closing);
            }

            if (ring.Points.Count < 3)
                return null;

            // The crossings are corners where the stroke meets the old curve
            bool[] isCrossing = [.. Enumerable.Range(0, ring.Points.Count).Select(i => ring.IsControl[i] && ring.Original[i] < 0)];
            List<Vector2> points = CatmullRomControlPointSimplification.DensifyNextToCorners(ring.Points, isCrossing, tolerance, out List<int> source);

            List<int> start = [.. Enumerable.Range(0, points.Count).Where(i => source[i] >= 0 && ring.IsControl[source[i]])];
            List<int> indices = CatmullRomControlPointSimplification.RefineControlPoints(points, start, isClosed: true, tolerance, numInterpolations);
            if (indices.Count < 3)
                return null;

            int OriginalOf(int i) => source[i] >= 0 ? ring.Original[source[i]] : -1;
            Vector2[] controlRing = [.. indices.Select(i => points[i]), points[indices[0]]];
            int[] originalIndex = [.. indices.Select(OriginalOf), OriginalOf(indices[0])];
            Vector2[] referenceRing = [.. points, points[0]];
            return new SplicedControlRing(controlRing, originalIndex, referenceRing);
        }
    }
}
