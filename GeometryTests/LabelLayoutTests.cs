using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Maximum inscribed circle, inscribed runs and the label layout that chooses between them for polygon annotations.
    /// </summary>
    [TestClass]
    public class LabelLayoutTests
    {
        private const double Degree = Math.PI / 180.0;

        private static Polygon Box(double left, double bottom, double right, double top) =>
            new([new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top), new Vector2(left, bottom)]);

        private static Polygon Rotated(Polygon polygon, double angle)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            return new Polygon(polygon.ExteriorRing.Select(p => new Vector2((p.X * cos) - (p.Y * sin), (p.X * sin) + (p.Y * cos))).ToArray());
        }

        /// <summary>A "C" open to the right: its centroid lies in the gap.</summary>
        private static Polygon CShape() =>
            new([new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 20), new Vector2(20, 20), new Vector2(20, 80),
                 new Vector2(100, 80), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0)]);

        /// <summary>A long horizontal arm and a shorter vertical arm.</summary>
        private static Polygon LShape() =>
            new([new Vector2(0, 0), new Vector2(200, 0), new Vector2(200, 20), new Vector2(20, 20), new Vector2(20, 100),
                 new Vector2(0, 100), new Vector2(0, 0)]);

        /// <summary>One row of text four units wide at an ID font size of 1.</summary>
        private static LabelContent Content(int up = 0, int down = 0) => new([new LabelRow(4, 1.0)], up, down);

        [TestMethod]
        public void InscribedCircleOfSquareIsExact()
        {
            Circle circle = Box(0, 0, 10, 10).InscribedCircle();
            Assert.AreEqual(5, circle.Center.X, 0.05);
            Assert.AreEqual(5, circle.Center.Y, 0.05);
            Assert.AreEqual(5, circle.Radius, 0.015);
        }

        [TestMethod]
        public void InscribedCircleOfCShapeIsInsideWhereCentroidIsNot()
        {
            Polygon c = CShape();
            Assert.IsFalse(c.Contains(c.Centroid), "Test shape should have its centroid outside");

            Circle circle = c.InscribedCircle();
            Assert.IsTrue(c.Contains(circle.Center));
            // Deepest in a corner of the spine, touching both outer walls and the inner corner at (20, 20)
            Assert.AreEqual(20 * Math.Sqrt(2) / (1 + Math.Sqrt(2)), circle.Radius, 0.15);
        }

        [TestMethod]
        public void InscribedCircleAvoidsHole()
        {
            Polygon outer = Box(0, 0, 20, 20);
            Polygon donut = new(outer.ExteriorRing, [Box(8, 8, 12, 12).ExteriorRing]);

            Circle circle = donut.InscribedCircle();
            Assert.IsTrue(donut.Contains(circle.Center));
            RingSegmentSet rings = new(donut);
            Assert.IsTrue(rings.DistanceToBoundary(circle.Center.X, circle.Center.Y) >= circle.Radius - 1e-9);
            Assert.IsTrue(circle.Radius > 3, "The circle should sit in the ring, not shrink beside the hole");
        }

        [TestMethod]
        public void InscribedCircleOfLongRectangleIsHalfItsHeight()
        {
            Circle circle = Box(0, 0, 200, 10).InscribedCircle();
            Assert.AreEqual(5, circle.Radius, 0.25);
            Assert.AreEqual(5, circle.Center.Y, 0.25);
        }

        [TestMethod]
        public void LevelRunSpansLongRectangle()
        {
            LabelPlacement placement = new LabelLayout(Box(0, 0, 200, 10)).Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(0, placement.Angle, 1e-9);
            Assert.AreEqual(200, placement.Region.Width, 200.0 / InscribedRuns.GridSize + 1e-6);
        }

        [TestMethod]
        public void DiagonalRunFollowsRectangle()
        {
            LabelPlacement placement = new LabelLayout(Rotated(Box(-100, -5, 100, 5), 45 * Degree)).Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(45 * Degree, placement.Angle, 1 * Degree);
            Assert.IsTrue(placement.Region.Width > 150, $"Run {placement.Region}");
        }

        [TestMethod]
        public void VerticalRunReadsBottomToTop()
        {
            LabelPlacement placement = new LabelLayout(Box(0, 0, 10, 200)).Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(90 * Degree, placement.Angle, 1e-9);
            Assert.IsTrue(placement.Region.AxisU.Y > 0.99, "Text should run up the screen");
        }

        [TestMethod]
        public void LShapeRunTakesLongerArm()
        {
            LabelPlacement placement = new LabelLayout(LShape()).Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(0, placement.Angle, 1e-9);
            Assert.IsTrue(placement.Region.Center.Y < 20, $"Run {placement.Region}");
            Assert.IsTrue(placement.Region.Width > 150, $"Run {placement.Region}");
        }

        [TestMethod]
        public void CompactShapesUseTheCircle()
        {
            LabelPlacement square = new LabelLayout(Box(0, 0, 100, 100)).Fit(Content(up: 2, down: 2));
            Assert.AreEqual(LabelPlacementKind.Circle, square.Kind);
            Assert.AreEqual(50, square.Circle.Center.X, 0.2);
            Assert.AreEqual(50, square.Circle.Center.Y, 0.2);
            Assert.AreEqual(square.Circle.Radius / 2.0, square.FontSize, 1e-9);
            Assert.AreEqual(0, square.Markers.Count);

            Vector2[] rim = [.. Enumerable.Range(0, 64).Select(i => new Vector2(50 * Math.Cos(i * Math.PI / 32), 50 * Math.Sin(i * Math.PI / 32)))];
            Polygon disc = new([.. rim, rim[0]]);
            Assert.AreEqual(LabelPlacementKind.Circle, new LabelLayout(disc).Fit(Content(up: 1, down: 1)).Kind);
        }

        [TestMethod]
        public void RunPutsDownMarkersLeftAndUpMarkersRight()
        {
            LabelPlacement placement = new LabelLayout(Box(0, 0, 400, 20)).Fit(Content(up: 2, down: 2));
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(4, placement.Markers.Count);

            OrientedRectangle region = placement.Region;
            double textLeft = region.WorldToLocal(placement.TextBox.Center).X - (placement.TextBox.Width / 2.0);
            double textRight = textLeft + placement.TextBox.Width;
            foreach (MarkerSlot marker in placement.Markers)
            {
                double u = region.WorldToLocal(marker.Center).X;
                if (marker.IsUp)
                    Assert.IsTrue(u - marker.Radius >= textRight - 1e-9, $"Up marker at {u} overlaps text ending at {textRight}");
                else
                    Assert.IsTrue(u + marker.Radius <= textLeft + 1e-9, $"Down marker at {u} overlaps text starting at {textLeft}");
            }

            Assert.AreEqual(2, placement.Markers.Count(m => m.IsUp));
        }

        [TestMethod]
        public void ExtraMarkersSpillInsideAndClearOfText()
        {
            Polygon box = Box(0, 0, 120, 20);
            LabelPlacement placement = new LabelLayout(box).Fit(Content(down: 10));
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(10, placement.Markers.Count);

            MarkerSlot[] spilled = [.. placement.Markers.Where(m => Math.Abs(placement.Region.WorldToLocal(m.Center).Y) > 1e-9)];
            Assert.IsTrue(spilled.Length > 0, "Some markers should not fit at the run's ends");
            foreach (MarkerSlot marker in spilled)
            {
                Assert.IsTrue(box.Contains(marker.Center), $"Marker {marker.Circle} outside the polygon");
                Assert.IsTrue(placement.TextBox.SignedDistance(marker.Center) > 0, $"Marker {marker.Circle} inside the text box");
            }
        }

        [TestMethod]
        public void RowsStackTopToBottomAlongTheRun()
        {
            LabelContent content = new([new LabelRow(6, 0.5), new LabelRow(4, 1.0), new LabelRow(2, 0.5)], 0, 0);
            LabelPlacement placement = new LabelLayout(Box(0, 0, 10, 300)).Fit(content);
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);

            double[] v = [.. placement.RowCenters.Select(c => placement.Region.WorldToLocal(c).Y)];
            Assert.IsTrue(v[0] > v[1] && v[1] > v[2], "Rows should stack down the text's up direction");
            Assert.AreEqual(0, (v[0] + v[2]) / 2.0, placement.FontSize * 0.01, "Rows should centre on the axis");
        }
    }
}
