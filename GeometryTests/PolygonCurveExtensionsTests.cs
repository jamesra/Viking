using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins the documented guarantees of <see cref="PolygonCurveExtensions.Smooth(Polygon, uint, double)"/>: it never
    /// throws on a pen-drawn ring, <c>minPointSpacing</c> only drops control points when at least three remain, a hole
    /// whose smoothed ring crosses the smoothed outline keeps its unsmoothed ring, and a polygon that cannot be
    /// smoothed at all comes back unsmoothed. <c>CurveFitting</c> pins the clustered-point, minimum-spacing and
    /// crossing-hole cases at one input each; these tests pin what those leave open (which ring a crossing hole keeps,
    /// holes crossing each other, spacing that would leave too few points, jitter in the thousands of shapes).
    /// </summary>
    [TestClass]
    public class PolygonCurveExtensionsTests
    {
        private const double MatchTolerance = 1e-9;

        private static Vector2[] Box(double x0, double y0, double x1, double y1) =>
            [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)];

        private static void AssertSameRing(Vector2[] expected, Vector2[] actual, string message)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"{message}: ring length");
            for (int i = 0; i < expected.Length; i++)
                Assert.IsTrue(Vector2.Distance(expected[i], actual[i]) < MatchTolerance, $"{message}: point {i} is {actual[i]}, expected {expected[i]}");
        }

        private static void AssertClosedDistinctRing(Polygon poly, string message)
        {
            Vector2[] ring = poly.ExteriorRing;
            Assert.IsTrue(ring.Length >= 4, $"{message}: a ring needs three distinct points and the closing point");
            Assert.IsTrue(Vector2.Distance(ring[0], ring[ring.Length - 1]) < MatchTolerance, $"{message}: ring is closed");
            Assert.AreEqual(ring.Length - 1, ring.Take(ring.Length - 1).Distinct().Count(), $"{message}: no repeated point");
        }

        /// <summary>A notch cut into the top edge, with a hole just under its tip.</summary>
        private static Polygon NotchWithHoleBelowTip(double gap)
        {
            Polygon poly = new(new Vector2[]
            {
                new(0, 0), new(100, 0), new(100, 100), new(55, 100), new(50, 60), new(45, 100), new(0, 100), new(0, 0)
            });
            poly.AddInteriorRing(Box(40, 60 - gap - 20, 60, 60 - gap));
            return poly;
        }

        private static Polygon WithTwoHoles(double gapBetweenHoles)
        {
            Polygon poly = new(Box(0, 0, 300, 100));
            poly.AddInteriorRing(Box(20, 30, 80, 70));
            poly.AddInteriorRing(Box(80 + gapBetweenHoles, 30, 140 + gapBetweenHoles, 70));
            return poly;
        }

        [TestMethod]
        public void HoleWhoseSmoothedRingCrossesTheOutlineKeepsItsUnsmoothedRing()
        {
            foreach (double gap in new[] { 0.25, 1, 2 })
            {
                Polygon poly = NotchWithHoleBelowTip(gap);
                Vector2[] holeBefore = poly.InteriorPolygons[0].ExteriorRing;

                Polygon smoothed = poly.Smooth(8);

                Assert.AreNotSame(poly, smoothed, $"gap {gap}: the outline still smooths");
                Assert.IsTrue(smoothed.ExteriorRing.Length > poly.ExteriorRing.Length, $"gap {gap}: outline is smoothed");
                Assert.AreEqual(1, smoothed.InteriorPolygons.Count, $"gap {gap}");
                AssertSameRing(holeBefore, smoothed.InteriorPolygons[0].ExteriorRing, $"gap {gap}: hole keeps its unsmoothed ring");
            }
        }

        [TestMethod]
        public void HoleThatDoesNotCrossTheOutlineIsSmoothed()
        {
            Polygon poly = NotchWithHoleBelowTip(16);

            Polygon smoothed = poly.Smooth(8);

            Assert.AreEqual(1, smoothed.InteriorPolygons.Count);
            Assert.IsTrue(smoothed.InteriorPolygons[0].ExteriorRing.Length > poly.InteriorPolygons[0].ExteriorRing.Length);
        }

        [TestMethod]
        public void HoleCrossingAnotherHolesSmoothedRingKeepsItsUnsmoothedRing()
        {
            Polygon poly = WithTwoHoles(10);
            Vector2[] firstHole = poly.InteriorPolygons[0].ExteriorRing;
            Vector2[] secondHole = poly.InteriorPolygons[1].ExteriorRing;

            Polygon smoothed = poly.Smooth(8);

            Assert.AreNotSame(poly, smoothed);
            Assert.AreEqual(2, smoothed.InteriorPolygons.Count);
            Assert.IsTrue(smoothed.InteriorPolygons[0].ExteriorRing.Length > firstHole.Length, "the first hole is smoothed");
            AssertSameRing(secondHole, smoothed.InteriorPolygons[1].ExteriorRing, "the second hole keeps its unsmoothed ring");
        }

        [TestMethod]
        public void HolesFarApartAreAllSmoothed()
        {
            Polygon poly = WithTwoHoles(60);

            Polygon smoothed = poly.Smooth(8);

            Assert.AreEqual(2, smoothed.InteriorPolygons.Count);
            Assert.IsTrue(smoothed.ExteriorRing.Length > poly.ExteriorRing.Length);
            foreach (Polygon hole in smoothed.InteriorPolygons)
                Assert.IsTrue(hole.ExteriorRing.Length > 5, "each hole is smoothed");
        }

        [TestMethod]
        public void PolygonWhoseHolesCannotBothFitSmoothedIsReturnedUnsmoothed()
        {
            foreach (double gap in new[] { 0.05, 0.5, 2 })
            {
                Polygon poly = WithTwoHoles(gap);

                Polygon smoothed = poly.Smooth(8);

                Assert.AreSame(poly, smoothed, $"gap {gap}");
                Assert.AreEqual(5, smoothed.ExteriorRing.Length, $"gap {gap}: outline is not smoothed");
                Assert.AreEqual(2, smoothed.InteriorPolygons.Count, $"gap {gap}");
            }
        }

        /// <summary>
        /// A hole is smoothed with the caller's spacing too: the hole here has a cluster of points that
        /// spacing 5 must drop before its curve is fitted.
        /// </summary>
        [TestMethod]
        public void MinimumSpacingAppliesToHolesAsWellAsTheOutline()
        {
            Polygon poly = new(Box(0, 0, 200, 200));
            poly.AddInteriorRing(new Vector2[]
            {
                new(50, 50), new(150, 50), new(151, 51), new(152, 50.5), new(150, 150), new(50, 150), new(50.5, 100), new(50, 50)
            });

            Vector2[] hole = poly.Smooth(0, 5).InteriorPolygons.Single().ExteriorRing;

            for (int i = 0; i < hole.Length - 1; i++)
                Assert.IsTrue(Vector2.Distance(hole[i], hole[i + 1]) >= 5, $"hole points {i} and {i + 1}");
            Assert.IsTrue(hole.Length < 8, "the clustered points are dropped");
        }

        /// <summary>
        /// Control points A(0,0), B(100,0), then C, D, E within 60 of B, so spacing 60 keeps only A and B.
        /// A ring of fewer than three points cannot be a polygon, so the ring is smoothed as it was drawn.
        /// </summary>
        [TestMethod]
        public void SpacingThatWouldLeaveFewerThanThreePointsSmoothsTheRingUnchanged()
        {
            Polygon poly = new(new Vector2[] { new(0, 0), new(100, 0), new(100, 10), new(70, 10), new(50, 10), new(0, 0) });
            Vector2[] unchanged = poly.Smooth(4).ExteriorRing;

            AssertSameRing(unchanged, poly.Smooth(4, 60).ExteriorRing, "two points would remain");
            AssertSameRing(unchanged, poly.Smooth(4, 5000).ExteriorRing, "one point would remain");
        }

        [TestMethod]
        public void SpacingThatLeavesExactlyThreePointsDropsTheRest()
        {
            Polygon poly = new(new Vector2[] { new(0, 0), new(100, 0), new(100, 10), new(0, 10), new(0, 0) });

            Vector2[] ring = poly.Smooth(0, 60).ExteriorRing;

            AssertSameRing([new(0, 0), new(100, 0), new(0, 10), new(0, 0)], ring, "A, B and D remain");
        }

        [TestMethod]
        public void SpacingDropsTrailingPointsThatSitTooCloseToTheFirst()
        {
            Polygon poly = new(new Vector2[] { new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 5), new(0, 0) });

            Vector2[] ring = poly.Smooth(0, 20).ExteriorRing;

            AssertSameRing([new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 0)], ring, "the point 5 from the first is dropped");
        }

        [TestMethod]
        public void SpacingKeepsAPointExactlyOneSpacingAway()
        {
            Polygon poly = new(Box(0, 0, 100, 100));

            Assert.AreEqual(5, poly.Smooth(0, 100).ExteriorRing.Length, "points exactly 100 apart are all kept");
            AssertSameRing(poly.Smooth(0).ExteriorRing, poly.Smooth(0, 100.5).ExteriorRing, "spacing past every gap leaves the ring as drawn");

            Polygon strip = new(new Vector2[] { new(0, 0), new(100, 0), new(100, 10), new(0, 10), new(0, 0) });
            AssertSameRing([new(0, 0), new(100, 0), new(0, 10), new(0, 0)], strip.Smooth(0, 100).ExteriorRing, "B is exactly 100 from A, so it is kept and C, 10 from B, is dropped");
        }

        /// <summary>
        /// Regression rings found by random search: three control points within 0.005 of each other make the fitted
        /// curve revisit a point it already passed, so the polygon constructor rejects the raw curve with "Duplicate
        /// point found in exterior ring". Smooth drops the repeat and still returns a smoothed ring.
        /// </summary>
        [TestMethod]
        public void RepeatedPointInTheFittedCurveIsDroppedRatherThanThrown()
        {
            Vector2[] shortRing =
            [
                new(7.929021952640741, 3.941684842082525), new(16.823428656020283, 10.048802697672533),
                new(16.82165951682778, 10.053379884938348), new(16.820470130453103, 10.051123206527494),
                new(2.4009268826527643, 17.92844793630249), new(2.3996127594260557, 17.926662116277342),
                new(7.93045094840503, 3.941735315604152), new(7.930978444061298, 3.9408028286779575),
                new(7.929021952640741, 3.941684842082525)
            ];
            Vector2[] longRing =
            [
                new(6.522720207703635, 18.883418030516907), new(7.678402334087743, 3.4156824560153862),
                new(7.680710673884246, 3.4201616271971593), new(7.681341314539938, 3.416629658740307),
                new(0.43796244759620345, 9.16364854795558), new(0.4434827828042247, 9.157990090570744),
                new(0.4403213599884517, 9.160301177371434), new(4.696774857443186, 18.5897505369921),
                new(1.870244891322332, 4.3899976203171525), new(2.3946204870710246, 5.4469526123467364),
                new(2.393142361724268, 5.450619562203175), new(2.3942284204038926, 5.448590938676424),
                new(19.403767245332947, 3.083863879581882), new(19.40406619543399, 3.0825844142039234),
                new(6.523520702711227, 18.885092947898592), new(6.522720207703635, 18.883418030516907)
            ];

            foreach ((Vector2[] ring, uint interpolations) in new[] { (shortRing, 3u), (longRing, 3u), (longRing, 8u) })
            {
                string message = $"{ring.Length - 1} control points, {interpolations} interpolations";
                Polygon poly = new(ring);
                Vector2[] rawCurve = poly.ExteriorRing.CalculateCurvePoints(interpolations, true);
                bool rawCurveRejected = false;
                try
                {
                    _ = new Polygon(rawCurve);
                }
                catch (ArgumentException)
                {
                    rawCurveRejected = true;
                }

                Assert.IsTrue(rawCurveRejected, $"{message}: the raw fitted curve must be rejected by the Polygon constructor, or this regression pins nothing");

                Polygon smoothed = poly.Smooth(interpolations);

                Assert.AreNotSame(poly, smoothed, message);
                AssertClosedDistinctRing(smoothed, message);
                Assert.IsTrue(smoothed.ExteriorRing.Length > poly.ExteriorRing.Length, $"{message}: ring is smoothed");
            }
        }

        /// <summary>
        /// Pen jitter: a ring of 8 to 24 control points on a wobbly ellipse where about a third are followed by a point
        /// 1.5 to 150 vector tolerances away (closer ones are rejected by the polygon constructor itself). Their
        /// interpolated points fall within tolerance of each other. Smooth never throws, and returns a closed ring of distinct points whether or not
        /// the polygon constructor accepts the interpolated curve first time.
        /// </summary>
        [TestMethod]
        public void NearDuplicatePenJitterNeverThrows()
        {
            Random random = new(20261107);
            int constructed = 0;
            int smoothedRings = 0;
            for (int trial = 0; trial < 400; trial++)
            {
                int count = random.Next(8, 25);
                List<Vector2> ring = [];
                for (int i = 0; i < count; i++)
                {
                    double angle = 2 * Math.PI * i / count;
                    double radius = 50 + random.NextDouble() * 4;
                    Vector2 p = new(100 + (radius * 1.5 * Math.Cos(angle)), 100 + (radius * Math.Sin(angle)));
                    ring.Add(p);
                    if (random.NextDouble() < 0.33)
                    {
                        // Along the direction of travel, so the hair-width step cannot fold the ring back over itself.
                        double gap = Tolerance.Epsilon * 1.5 * Math.Pow(10, random.NextDouble() * 2);
                        double tangentX = -1.5 * Math.Sin(angle);
                        double tangentY = Math.Cos(angle);
                        double length = Math.Sqrt((tangentX * tangentX) + (tangentY * tangentY));
                        ring.Add(new Vector2(p.X + (gap * tangentX / length), p.Y + (gap * tangentY / length)));
                    }
                }

                ring.Add(ring[0]);
                Polygon poly;
                try
                {
                    poly = new Polygon([.. ring]);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                constructed++;
                foreach (uint interpolations in new uint[] { 0, 1, 4, 8 })
                {
                    foreach (double spacing in new[] { 0, 0.5 })
                    {
                        Polygon smoothed = poly.Smooth(interpolations, spacing);
                        string message = $"trial {trial}, {interpolations} interpolations, spacing {spacing}";
                        Assert.IsNotNull(smoothed, message);
                        AssertClosedDistinctRing(smoothed, message);
                        if (!ReferenceEquals(smoothed, poly))
                            smoothedRings++;
                    }
                }
            }

            Assert.IsTrue(constructed >= 100, $"only {constructed} of 400 jittered rings were valid polygons");
            Assert.IsTrue(smoothedRings > constructed, "most rings smooth rather than coming back as drawn");
        }
    }
}
