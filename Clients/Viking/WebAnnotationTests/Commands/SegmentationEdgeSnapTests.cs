using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The edge snap moves a traced contour onto a bright-to-dark image edge and otherwise leaves it alone.
    /// Images here are synthetic: a bright cell, a dark membrane band, grain, and a contour deliberately placed inside.
    /// </summary>
    [TestClass]
    public class SegmentationEdgeSnapTests
    {
        private static readonly Vector2 Centre = new(0, 0);

        /// <summary>
        /// Brightness of a cell of radius <paramref name="cellRadius"/> centred on the origin: <paramref name="inside"/> within
        /// it, a dark band <paramref name="bandWidth"/> wide outside it, <paramref name="outside"/> beyond. The edges are
        /// two pixels soft, as a blurred capture is, with deterministic grain of +/- <paramref name="grain"/> levels.
        /// </summary>
        private static Func<Vector2, double> Membrane(
            double cellRadius,
            double inside = 180,
            double band = 60,
            double outside = 130,
            double bandWidth = 4,
            double grain = 5)
        {
            return point =>
            {
                double r = Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
                double toBand = Smooth(r - cellRadius);
                double toOutside = Smooth(r - (cellRadius + bandWidth));
                double value = inside + ((band - inside) * toBand) + ((outside - band) * toOutside);
                return value + Grain(point) * grain;
            };
        }

        private static double Smooth(double distance) => 0.5 + (0.5 * Math.Tanh(distance / 1.2));

        private static double Grain(Vector2 point)
        {
            int x = (int)Math.Floor(point.X * 2);
            int y = (int)Math.Floor(point.Y * 2);
            uint hash = (uint)((x * 73856093) ^ (y * 19349663));
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            return ((hash & 0xFFFF) / 65535.0 * 2) - 1;
        }

        private static Polygon Circle(double radius, int points = 160, bool clockwise = false)
        {
            List<Vector2> ring = [];
            for (int i = 0; i < points; i++)
            {
                double angle = 2 * Math.PI * i / points * (clockwise ? -1 : 1);
                ring.Add(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
            }

            ring.Add(ring[0]);
            return new Polygon(ring);
        }

        private static double MeanRadius(IEnumerable<Vector2> ring) =>
            ring.Average(point => Math.Sqrt((point.X * point.X) + (point.Y * point.Y)));

        [TestMethod]
        public void AContourEightPixelsInsideAMembraneIsMovedOntoIt()
        {
            Polygon traced = Circle(100);   // the membrane's inner edge is at 108
            Func<Vector2, double> image = Membrane(cellRadius: 108);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, image, 1.0, Options(), out EdgeSnapStats stats);

            double radius = MeanRadius(snapped.ExteriorRing);
            Assert.AreEqual(108, radius, 1.5, $"mean radius {radius}");
            Assert.IsTrue(stats.Snapped >= 1);
            Assert.IsTrue(stats.MeanOffsetPixels > 5, $"moved outward by {stats.MeanOffsetPixels}");
        }

        [TestMethod]
        public void AWeakGrainyEdgeStillAttractsTheContour()
        {
            Polygon traced = Circle(100);
            Func<Vector2, double> image = Membrane(cellRadius: 106, inside: 150, band: 120, outside: 140, grain: 6);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, image, 1.0, Options(reference: 12));

            double radius = MeanRadius(snapped.ExteriorRing);
            Assert.AreEqual(106, radius, 2.5, $"mean radius {radius}");
        }

        [TestMethod]
        public void TheSnapIsInDifferentWorldUnitsWhenTheDownsampleIs()
        {
            // The same picture drawn at downsample 4: a 108 pixel cell is 432 world units.
            Polygon traced = Circle(400);
            Func<Vector2, double> image = point => Membrane(cellRadius: 108)(point / 4.0);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, image, 4.0, Options());

            Assert.AreEqual(432, MeanRadius(snapped.ExteriorRing), 6.0);
        }

        [TestMethod]
        public void AContourStaysPutWhereTheImageHasNoEdge()
        {
            Polygon traced = Circle(100);
            Func<Vector2, double> flat = point => 140 + (Grain(point) * 5);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, flat, 1.0, Options(), out EdgeSnapStats stats);

            Assert.AreSame(traced, snapped, "an image with no edge returns the polygon it was given");
            Assert.AreEqual(0, stats.Snapped);
        }

        [TestMethod]
        public void AnEdgeFainterThanTheThresholdLeavesTheContourAlone()
        {
            Polygon traced = Circle(100);
            Func<Vector2, double> faint = Membrane(cellRadius: 108, inside: 142, band: 138, outside: 140, grain: 0.1);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, faint, 1.0, Options());

            Assert.AreSame(traced, snapped);
        }

        [TestMethod]
        public void OnlyABrightToDarkDropGoingOutwardAttractsTheContour()
        {
            // Dark cell, brighter surroundings: brightness rises going outward, so this is not a membrane.
            Polygon traced = Circle(100);
            Func<Vector2, double> reversed = point =>
            {
                double r = Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
                return 70 + (110 * Smooth(r - 108)) + (Grain(point) * 4);
            };

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, reversed, 1.0, Options(), out EdgeSnapStats stats);

            Assert.AreEqual(100, MeanRadius(snapped.ExteriorRing), 2.0, "it did not chase the rising edge");
            Assert.IsTrue(Math.Abs(stats.MeanOffsetPixels) < 1.5, $"mean offset {stats.MeanOffsetPixels}");
        }

        [TestMethod]
        public void AClockwiseRingIsSnappedTheSameAsACounterClockwiseOne()
        {
            Func<Vector2, double> image = Membrane(cellRadius: 108);

            Polygon counterClockwise = SegmentationEdgeSnap.Refine(Circle(100), image, 1.0, Options());
            Polygon clockwise = SegmentationEdgeSnap.Refine(Circle(100, clockwise: true), image, 1.0, Options());

            Assert.AreEqual(MeanRadius(counterClockwise.ExteriorRing), MeanRadius(clockwise.ExteriorRing), 0.5);
        }

        [TestMethod]
        public void AHoleIsSnappedIntoTheHoleAwayFromTheObject()
        {
            // Bright object between r = 20 and r = 80; the hole (r < 18) is darker. The hole ring was traced at 24, six
            // pixels into the object, so it should move toward the centre, onto the bright-to-dark edge at about 19.
            Polygon polygon = Circle(80);
            polygon.AddInteriorRing(CircleRing(24, clockwise: true));
            Func<Vector2, double> image = point =>
            {
                double r = Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
                double toHole = 1 - Smooth(r - 19);
                return 180 - (120 * toHole) - (50 * Smooth(r - 82)) + (Grain(point) * 4);
            };

            Polygon snapped = SegmentationEdgeSnap.Refine(polygon, image, 1.0, Options());

            Assert.AreEqual(1, snapped.InteriorRings.Count);
            double holeRadius = MeanRadius(snapped.InteriorRings[0]);
            Assert.IsTrue(holeRadius < 22.0 && holeRadius > 16.0, $"hole radius {holeRadius}");
        }

        [TestMethod]
        public void AnUnknownImageLeavesThePolygonUntouched()
        {
            Polygon traced = Circle(100);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, _ => double.NaN, 1.0, Options());

            Assert.AreSame(traced, snapped);
        }

        [TestMethod]
        public void AnImageThatIsKnownOnlyInPartMovesOnlyThatPart()
        {
            Polygon traced = Circle(100);
            Func<Vector2, double> full = Membrane(cellRadius: 108);
            Func<Vector2, double> rightHalf = point => point.X > 0 ? full(point) : double.NaN;

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, rightHalf, 1.0, Options());

            double right = MeanRadius(snapped.ExteriorRing.Where(point => point.X > 40));
            double left = MeanRadius(snapped.ExteriorRing.Where(point => point.X < -40));
            Assert.AreEqual(108, right, 2.0, "where the image is known the contour reaches the membrane");
            Assert.AreEqual(100, left, 2.0, "where it is unknown the contour stays");
        }

        [TestMethod]
        public void TheRingStaysAValidPolygonWhenTheSearchRangeIsLarge()
        {
            Polygon traced = Circle(30);
            Func<Vector2, double> image = Membrane(cellRadius: 90);

            Polygon snapped = SegmentationEdgeSnap.Refine(traced, image, 1.0, new EdgeSnapOptions { OutwardPixels = 40, ReferenceEdgeLevelsPerPixel = 25 });

            Assert.IsTrue(snapped.Area > 0);
            Assert.IsFalse(snapped.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));
        }

        [TestMethod]
        public void ResamplingGivesEvenlySpacedPointsAroundTheRing()
        {
            Polygon square = new(
            [
                new Vector2(0, 0), new Vector2(40, 0), new Vector2(40, 40), new Vector2(0, 40), new Vector2(0, 0)
            ]);

            Vector2[] points = SegmentationEdgeSnap.Resample(square.ExteriorRing, 4.0);

            Assert.AreEqual(40, points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                double gap = Vector2.Distance(points[i], points[(i + 1) % points.Length]);
                Assert.AreEqual(4.0, gap, 0.01, $"gap at {i}");
            }
        }

        [TestMethod]
        public void NormalsPointAwayFromTheObjectForAnExteriorAndIntoAHole()
        {
            Vector2[] ring = SegmentationEdgeSnap.Resample(Circle(50).ExteriorRing, 5.0);
            Vector2[] holeRing = SegmentationEdgeSnap.Resample(CircleRing(50, clockwise: true), 5.0);

            Vector2[] exteriorNormals = SegmentationEdgeSnap.OutwardNormals(ring, isHole: false, out _);
            Vector2[] holeNormals = SegmentationEdgeSnap.OutwardNormals(holeRing, isHole: true, out _);

            for (int i = 0; i < ring.Length; i++)
                Assert.IsTrue(Dot(exteriorNormals[i], ring[i]) > 0, "an exterior normal points away from the centre");
            for (int i = 0; i < holeRing.Length; i++)
                Assert.IsTrue(Dot(holeNormals[i], holeRing[i]) < 0, "a hole normal points toward the centre of the hole");
        }

        [TestMethod]
        public void TheClosedViterbiFollowsAStrongMinimumAndIgnoresOneStrayVertex()
        {
            const int count = 60;
            const int levels = 16;
            double[] offsets = Enumerable.Range(0, levels).Select(k => -3.0 + k).ToArray();
            double[,] cost = new double[count, levels];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < levels; k++)
                    cost[i, k] = Math.Abs(k - 9) * 1.0;
            }

            for (int k = 0; k < levels; k++)
                cost[30, k] = Math.Abs(k - 1) * 0.3;   // one vertex that prefers a very different level, weakly

            int[] choice = SegmentationEdgeSnap.ClosedViterbi(cost, offsets, new EdgeSnapOptions());

            Assert.IsTrue(choice.Where((_, i) => i != 30).All(level => level == 9), "the ring follows its strong minimum");
            Assert.IsTrue(Math.Abs(choice[30] - 9) <= 3, "one vertex cannot jump away from its neighbours");
        }

        [TestMethod]
        public void ASmoothedRingKeepsItsMeanAndRemovesASpike()
        {
            double[] values = new double[20];
            values[10] = 8;

            double[] smoothed = SegmentationEdgeSnap.SmoothCircular(values);

            Assert.AreEqual(values.Sum(), smoothed.Sum(), 1e-9);
            Assert.IsTrue(smoothed[10] < values[10]);
        }

        /// <summary>Options for an image whose salient edges are about <paramref name="reference"/> levels per pixel.</summary>
        private static EdgeSnapOptions Options(double reference = 25) =>
            new() { ReferenceEdgeLevelsPerPixel = reference };

        private static Vector2[] CircleRing(double radius, bool clockwise = false, int points = 120)
        {
            List<Vector2> ring = [];
            for (int i = 0; i < points; i++)
            {
                double angle = 2 * Math.PI * i / points * (clockwise ? -1 : 1);
                ring.Add(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
            }

            ring.Add(ring[0]);
            return [.. ring];
        }

        private static double Dot(Vector2 a, Vector2 b) => (a.X * b.X) + (a.Y * b.Y);
    }
}
