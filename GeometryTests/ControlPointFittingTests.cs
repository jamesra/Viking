using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins the control point fitting behind pen annotations: <see cref="CatmullRomControlPointSimplification.RefineControlPoints"/>
    /// keeps the points it is given and stays within the tolerance both ways, and <see cref="ControlRingCurve"/> splices a pen
    /// stroke into a ring without moving the control points it keeps.
    /// </summary>
    [TestClass]
    public class ControlPointFittingTests
    {
        /// <summary>A wobbly closed outline, radius about 300, as a dense ring without its closing point.</summary>
        private static Vector2[] WobblyRing(int count)
        {
            Vector2[] ring = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count;
                double radius = 300 * (1 + (0.05 * Math.Sin(3 * angle)) + (0.03 * Math.Sin(7 * angle)));
                ring[i] = new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            return ring;
        }

        private static double DistanceToClosedPolyline(Vector2 p, IReadOnlyList<Vector2> ring)
        {
            double best = double.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i];
                Vector2 b = ring[(i + 1) % ring.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y, lengthSquared = (dx * dx) + (dy * dy);
                double t = lengthSquared > 0 ? Math.Max(0, Math.Min(1, (((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared)) : 0;
                double ex = a.X + (t * dx) - p.X, ey = a.Y + (t * dy) - p.Y;
                best = Math.Min(best, Math.Sqrt((ex * ex) + (ey * ey)));
            }

            return best;
        }

        /// <summary>The curve through the control points, sampled as Viking draws it (<paramref name="interpolations"/> per span, more where it bends).</summary>
        private static Vector2[] Drawn(IReadOnlyList<Vector2> closedControlRing, uint interpolations = 16) => closedControlRing.ToArray().CalculateCurvePoints(interpolations, true);

        [TestMethod]
        public void RefinementKeepsTheGivenPointsAndMeetsTheToleranceBothWays()
        {
            Vector2[] reference = WobblyRing(400);
            int[] start = [0, 100, 200, 300];
            const double tolerance = 1.0;

            List<int> indices = CatmullRomControlPointSimplification.RefineControlPoints(reference, start, isClosed: true, tolerance, 8);

            CollectionAssert.IsSubsetOf(start, indices);
            Vector2[] controls = [.. indices.Select(i => reference[i]), reference[indices[0]]];
            Vector2[] drawn = Drawn(controls);
            // A little slack for measuring against the drawn polyline instead of the exact curve
            Assert.IsTrue(reference.Max(p => DistanceToClosedPolyline(p, drawn)) <= tolerance + 0.25, "every reference point is near the curve");
            Assert.IsTrue(drawn.Max(p => DistanceToClosedPolyline(p, reference)) <= tolerance + 0.25, "every point of the curve is near the reference");
        }

        [TestMethod]
        public void AClosedRingStartingWithTwoPointsGetsAThird()
        {
            List<int> indices = CatmullRomControlPointSimplification.RefineControlPoints(WobblyRing(100), [0, 50], isClosed: true, 1.0, 8);

            Assert.IsTrue(indices.Count >= 3);
            CollectionAssert.IsSubsetOf(new[] { 0, 50 }, indices);
        }

        /// <summary>
        /// A stroke that dips 20 px inside the outline over a quarter of it replaces only that quarter: every control point of the
        /// kept side is kept exactly, with its original index.
        /// </summary>
        [TestMethod]
        public void SplicingKeepsTheUntouchedControlPointsExactly()
        {
            Vector2[] controls = [.. WobblyRing(400).Where((p, i) => i % 16 == 0)];
            ControlRingCurve curve = new(controls, 3);

            List<Vector2> stroke = [new(340 * Math.Cos(-0.1), 340 * Math.Sin(-0.1))];
            for (double angle = 0; angle <= Math.PI / 2; angle += 0.02)
            {
                double radius = (300 * (1 + (0.05 * Math.Sin(3 * angle)) + (0.03 * Math.Sin(7 * angle)))) - 20;
                stroke.Add(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
            }

            stroke.Add(new Vector2(340 * Math.Cos((Math.PI / 2) + 0.1), 340 * Math.Sin((Math.PI / 2) + 0.1)));

            Assert.IsTrue(curve.TrySplice(stroke, 1.0, out SplicedControlRing afterFirst, out SplicedControlRing afterSecond));

            SplicedControlRing kept = Area(afterFirst.ReferenceRing) > Area(afterSecond.ReferenceRing) ? afterFirst : afterSecond;
            for (int k = 0; k < kept.ControlRing.Length; k++)
            {
                if (kept.OriginalIndex[k] >= 0)
                    Assert.AreEqual(controls[kept.OriginalIndex[k]], kept.ControlRing[k], "a kept point is the stored point");
            }

            // The control points well away from the quarter that was cut (index 25 is angle 5 pi / 4) are all kept
            int[] kepts = [.. kept.OriginalIndex.Where(i => i >= 0).Distinct()];
            CollectionAssert.IsSubsetOf(Enumerable.Range(8, 15).ToArray(), kepts);

            // Sampled as the ring was, so the kept stretch of old curve is compared with its own samples
            Vector2[] drawn = Drawn(kept.ControlRing, 3);
            Vector2[] reference = [.. kept.ReferenceRing.Take(kept.ReferenceRing.Length - 1)];
            Vector2 worst = drawn.OrderByDescending(p => DistanceToClosedPolyline(p, reference)).First();
            double gap = DistanceToClosedPolyline(worst, reference);
            Assert.IsTrue(gap <= 1.25, $"the new outline follows the stroke and the kept curve: {gap:F2} px at {worst}");
        }

        private static double Area(Vector2[] closedRing)
        {
            double twice = 0;
            for (int i = 0; i < closedRing.Length - 1; i++)
                twice += (closedRing[i].X * closedRing[i + 1].Y) - (closedRing[i + 1].X * closedRing[i].Y);

            return Math.Abs(twice / 2);
        }
    }
}
