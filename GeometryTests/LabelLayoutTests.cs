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

        /// <summary>
        /// A "V" open to the right: a level arm 10 thick along +X and an arm <paramref name="tiltedThickness"/> thick at
        /// <paramref name="degrees"/>, both 100 long and joined at the origin.
        /// </summary>
        private static Polygon LevelAndTiltedArms(double tiltedThickness, double degrees = 30)
        {
            double cos = Math.Cos(degrees * Degree), sin = Math.Sin(degrees * Degree);
            Vector2 across = new(-sin * tiltedThickness, cos * tiltedThickness);
            Vector2 tip = new(100 * cos, 100 * sin);
            return new([new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 10), new Vector2(10 / Math.Tan(degrees * Degree), 10),
                        tip, tip + across, across, new Vector2(0, 0)]);
        }

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
            Assert.AreEqual(4, square.Markers.Count, "The circle placement lays its markers out too");

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

        /// <summary>
        /// Markers that fit no packing, not even the smallest markers in the most rows, go to free space: inside the
        /// polygon, clear of the text, after the packed ones, and they cost the placement score.
        /// </summary>
        [TestMethod]
        public void ExtraMarkersSpillInsideAndClearOfText()
        {
            Polygon box = Box(0, 0, 120, 20);
            LabelPlacement placement = new LabelLayout(box).Fit(Content(down: 60));
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(60, placement.Markers.Count);
            Assert.IsTrue(placement.FreeMarkerCount > 0, "Some markers should not fit at the run's ends");
            Assert.AreEqual(LabelLayout.MinimumMarkerScale, placement.Packing.Scale, "Free space only after the smallest packing");
            Assert.IsTrue(placement.MarkerReadability < 1 - (LabelLayout.MarkerShrinkCost * (1 - LabelLayout.MinimumMarkerScale)) + 1e-12);

            RingSegmentSet rings = new(box);
            foreach (MarkerSlot marker in placement.Markers.Skip(placement.Markers.Count - placement.FreeMarkerCount))
            {
                Assert.IsTrue(rings.SignedDistance(marker.Center.X, marker.Center.Y) >= marker.Radius - 1e-9, $"Marker {marker.Circle} crosses the outline");
                Assert.IsTrue(placement.TextBox.SignedDistance(marker.Center) > 0, $"Marker {marker.Circle} inside the text box");
            }
        }

        /// <summary>Rows like the screenshot's: "CB OFF" (structure label), "CNR" (tags), "C 45733" (ID), in Arial widths.</summary>
        private static LabelContent ScreenshotContent(int up, int down) =>
            new([new LabelRow(3.25, 0.5), new LabelRow(1.91, 0.25), new LabelRow(3.36, 1.0)], up, down);

        /// <summary>True when no two of <paramref name="markers"/> overlap.</summary>
        private static bool Disjoint(System.Collections.Generic.IReadOnlyList<MarkerSlot> markers)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                for (int j = i + 1; j < markers.Count; j++)
                {
                    if (Vector2.Distance(markers[i].Center, markers[j].Center) < markers[i].Radius + markers[j].Radius - 1e-9)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Six markers do not fit one row at the ends of a level 110 × 40 run without costing more than 40% of the font
        /// (W/9.74 against 20.8), but two staggered rows of full-size markers do (W/7.74): each end holds its three in a
        /// zigzag, the second row half a marker in and √3/2 of a marker across, still inside the run.
        /// </summary>
        [TestMethod]
        public void LevelRunStaggersMarkersThatOverflowOneRow()
        {
            Polygon box = Box(0, 0, 110, 40);
            LabelPlacement placement = new LabelLayout(box).Fit(ScreenshotContent(up: 3, down: 3), LabelLayout.NeverRotate);
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(2, placement.Packing.Rows);
            Assert.AreEqual(1.0, placement.Packing.Scale);
            Assert.AreEqual(0, placement.FreeMarkerCount);
            Assert.AreEqual(1.0, placement.MarkerReadability, 1e-12, "Full-size markers cost nothing");

            OrientedRectangle region = placement.Region;
            double textLeft = region.WorldToLocal(placement.TextBox.Center).X - (placement.TextBox.Width / 2.0);
            double textRight = textLeft + placement.TextBox.Width;
            double d = placement.FontSize;
            foreach (MarkerSlot marker in placement.Markers)
            {
                Vector2 local = region.WorldToLocal(marker.Center);
                Assert.AreEqual(LabelLayout.EndMarkerRadius * d, marker.Radius, 1e-9);
                Assert.AreEqual(LabelLayout.MarkerRowSpacing * d / 2.0, Math.Abs(local.Y), 1e-9, "Two rows either side of the axis");
                Assert.IsTrue(Math.Abs(local.Y) + marker.Radius <= (region.Height / 2.0) + 1e-9, "Marker sticks out of the run");
                if (marker.IsUp)
                    Assert.IsTrue(local.X - marker.Radius >= textRight - 1e-9, $"Up marker at {local.X} overlaps text ending at {textRight}");
                else
                    Assert.IsTrue(local.X + marker.Radius <= textLeft + 1e-9, $"Down marker at {local.X} overlaps text starting at {textLeft}");
            }

            Assert.IsTrue(Disjoint(placement.Markers), "Packed markers overlap");
            Assert.IsTrue(placement.FontSize >= LabelLayout.MarkerFontRetention * LabelLayout.RunFontSize(110, 40, placement.Content, 0) - 1e-9);

            // One row of text in a 22-thick run: the two-row block (1.77 fonts) is thicker than the text (1.1), so the
            // font is set by the block's height, 22 / 1.77, and the staggered markers stay inside the run.
            LabelPlacement thin = new LabelLayout(Box(0, 0, 110, 22)).Fit(Content(up: 3, down: 3), LabelLayout.NeverRotate);
            Assert.AreEqual(LabelPlacementKind.Run, thin.Kind);
            Assert.AreEqual(2, thin.Packing.Rows);
            Assert.AreEqual(1.0, thin.Packing.Scale);
            Assert.AreEqual(thin.Region.Height / (LabelLayout.MarkerRowSpacing + (2 * LabelLayout.EndMarkerRadius)), thin.FontSize, 1e-9);
            foreach (MarkerSlot marker in thin.Markers)
                Assert.IsTrue(Math.Abs(thin.Region.WorldToLocal(marker.Center).Y) + marker.Radius <= (thin.Region.Height / 2.0) + 1e-9, "Marker sticks out of the run");
        }

        /// <summary>
        /// Sixteen markers in the same run fit no full-size packing, so they shrink (here to 0.75 in four rows): all at
        /// the ends, none in free space, and the placement's score pays for the smaller markers.
        /// </summary>
        [TestMethod]
        public void LevelRunShrinksMarkersThatOverflowStaggeredRows()
        {
            Polygon box = Box(0, 0, 110, 40);
            LabelPlacement placement = new LabelLayout(box).Fit(ScreenshotContent(up: 8, down: 8), LabelLayout.NeverRotate);
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.IsTrue(placement.Packing.Scale < 1, $"Packing {placement.Packing}");
            Assert.IsTrue(placement.Packing.Scale >= LabelLayout.MinimumMarkerScale);
            Assert.IsTrue(placement.Packing.Rows > 2, $"Packing {placement.Packing}");
            Assert.AreEqual(0, placement.FreeMarkerCount);
            Assert.AreEqual(1 - (LabelLayout.MarkerShrinkCost * (1 - placement.Packing.Scale)), placement.MarkerReadability, 1e-12);

            RingSegmentSet rings = new(box);
            foreach (MarkerSlot marker in placement.Markers)
            {
                Assert.AreEqual(LabelLayout.EndMarkerRadius * placement.Packing.Scale * placement.FontSize, marker.Radius, 1e-9);
                Assert.IsTrue(rings.SignedDistance(marker.Center.X, marker.Center.Y) >= marker.Radius - 1e-9, $"Marker {marker.Circle} crosses the outline");
            }

            Assert.IsTrue(Disjoint(placement.Markers), "Packed markers overlap");
            Assert.AreEqual(8, placement.Markers.Count(m => m.IsUp));
        }

        /// <summary>
        /// In the circle, up-links go above the text and down-links below, as circle annotations draw them: full size on
        /// the 0.75 R arc with radius R/6 while seven fit a side. More than seven shrink and stagger into further rows,
        /// still each in its own half, inside the polygon and clear of one another.
        /// </summary>
        [TestMethod]
        public void CirclePutsUpLinksAboveAndDownLinksBelow()
        {
            LabelLayout square = new(Box(0, 0, 100, 100));
            LabelPlacement few = square.Fit(Content(up: 3, down: 2));
            Assert.AreEqual(LabelPlacementKind.Circle, few.Kind);
            Circle c = few.Circle;
            Assert.AreEqual(5, few.Markers.Count);
            Assert.AreEqual(1.0, few.Packing.Scale);
            foreach (MarkerSlot marker in few.Markers)
            {
                Assert.AreEqual(c.Radius * LabelLayout.CircleMarkerRadius, marker.Radius, 1e-9);
                Assert.AreEqual(c.Radius * LabelLayout.CircleMarkerArc, Vector2.Distance(marker.Center, c.Center), 1e-9);
                double y = marker.Center.Y - c.Center.Y;
                Assert.IsTrue(marker.IsUp ? y > c.Radius * 0.5 : y < -c.Radius * 0.5, $"Marker {marker.Circle} up={marker.IsUp}");
            }

            Assert.AreEqual(c.Center.X, few.Markers.Single(m => m.IsUp && Math.Abs(m.Center.X - c.Center.X) < 1e-9).Center.X, 1e-9,
                "Three up-links centre one on the vertical");

            Vector2[] rim = [.. Enumerable.Range(0, 96).Select(i => new Vector2(50 * Math.Cos(i * Math.PI / 48), 50 * Math.Sin(i * Math.PI / 48)))];
            Polygon disc = new([.. rim, rim[0]]);
            LabelLayout round = new(disc);
            RingSegmentSet rings = new(disc);
            LabelPlacement many = round.Fit(Content(up: 14, down: 9));
            Assert.AreEqual(LabelPlacementKind.Circle, many.Kind);
            Assert.AreEqual(23, many.Markers.Count);
            Assert.AreEqual(0, many.FreeMarkerCount);
            Assert.IsTrue(many.Packing.Scale < 1 && many.Packing.Rows >= 2, $"Packing {many.Packing}");
            foreach (MarkerSlot marker in many.Markers)
            {
                double y = marker.Center.Y - many.Circle.Center.Y;
                Assert.IsTrue(marker.IsUp ? y - marker.Radius >= -1e-9 : y + marker.Radius <= 1e-9, $"Marker {marker.Circle} crosses into the other half");
                Assert.IsTrue(rings.SignedDistance(marker.Center.X, marker.Center.Y) >= marker.Radius - 1e-9, $"Marker {marker.Circle} crosses the outline");
            }

            Assert.IsTrue(Disjoint(many.Markers), "Circle markers overlap");
            Assert.IsTrue(many.MarkerReadability < 1);
        }

        /// <summary>
        /// James's report, RC2 location 386775 (cell C 45733, section 645, two links down to 643 and three up to 646):
        /// with the label level, one row at the ends held only four markers (five cost 42% of the font, beyond
        /// <see cref="LabelLayout.MarkerFontRetention"/>), so the fifth went to the deepest free spot, the bulb at the
        /// cell's other end, over 1100 units from the other up-links. Now the level label staggers both ends into two
        /// rows and keeps all five beside it, down-links left and up-links right, inside the cell. Outline: the
        /// location's control points (offset by 46000, 62000), smoothed as the viewer does.
        /// </summary>
        [TestMethod]
        public void ScreenshotCellKeepsAllFiveMarkersBesideTheLevelLabel()
        {
            double[] xy =
            [
                1491, 790, 1435, 814, 1407, 817, 1397, 814, 1316, 733, 1290, 719, 1213, 709, 1097, 710,
                1060, 716, 1016, 748, 1014, 758, 1002, 768, 997, 788, 973, 819, 978, 893, 955, 940,
                916, 950, 848, 920, 755, 914, 692, 963, 604, 994, 580, 1018, 540, 1083, 527, 1111,
                516, 1161, 508, 1170, 472, 1196, 413, 1225, 291, 1264, 284, 1278, 277, 1333, 252, 1351,
                234, 1356, 125, 1344, 55, 1358, 0, 1384, -79, 1391, -151, 1388, -180, 1380, -202, 1363,
                -230, 1311, -233, 1285, -213, 1249, -187, 1172, -152, 1101, -148, 1017, -118, 974, -21, 929,
                51, 839, 148, 763, 178, 747, 420, 719, 467, 704, 496, 670, 516, 633, 525, 584,
                523, 522, 533, 499, 679, 432, 779, 347, 838, 328, 872, 295, 926, 219, 948, 203,
                1056, 235, 1220, 210, 1285, 211, 1355, 225, 1405, 249, 1530, 278, 1532, 306, 1616, 446,
                1618, 460, 1596, 538, 1526, 671, 1524, 703, 1491, 789,
            ];
            Vector2[] ring = [.. Enumerable.Range(0, xy.Length / 2).Select(i => new Vector2(46000 + xy[2 * i], 62000 + xy[(2 * i) + 1]))];
            Polygon cell = new Polygon([.. ring, ring[0]]).Smooth(3);
            LabelLayout layout = new(cell);
            RingSegmentSet rings = new(cell);
            LabelContent content = ScreenshotContent(up: 3, down: 2);

            foreach (double gain in new[] { LabelLayout.NeverRotate, LabelLayout.DefaultRotationGain })
            {
                LabelPlacement placement = layout.Fit(content, gain);
                Assert.AreEqual(LabelPlacementKind.Run, placement.Kind, $"G = {gain}");
                Assert.AreEqual(5, placement.Markers.Count);
                Assert.AreEqual(0, placement.FreeMarkerCount, $"G = {gain}: every marker beside the label");
                Assert.AreEqual(1.0, placement.Packing.Scale, $"G = {gain}: full-size markers");

                OrientedRectangle region = placement.Region;
                double textCenter = region.WorldToLocal(placement.TextBox.Center).X;
                foreach (MarkerSlot marker in placement.Markers)
                {
                    Assert.IsTrue(rings.SignedDistance(marker.Center.X, marker.Center.Y) >= marker.Radius, $"G = {gain}: marker {marker.Circle} crosses the outline");
                    double u = region.WorldToLocal(marker.Center).X;
                    Assert.IsTrue(marker.IsUp ? u > textCenter : u < textCenter, $"G = {gain}: marker on the wrong side");
                    Assert.IsTrue(Math.Abs(u) + marker.Radius <= (region.Width / 2.0) + 1e-9, $"G = {gain}: marker off the run's end");
                }
            }

            LabelPlacement level = layout.Fit(content, LabelLayout.NeverRotate);
            Assert.AreEqual(0, level.Angle, 1e-12);
            Assert.AreEqual(2, level.Packing.Rows, "The level label staggers its markers into two rows");
            Assert.AreEqual(3, level.Markers.Count(m => m.IsUp));
        }

        /// <summary>
        /// The 30° arm is thicker than the level arm, so tilting gains a little font (about 17% once rasterised): less
        /// than the 25% the default asks at 30°, more than the 11% the old flat 0.9 penalty asked. Level at the default,
        /// tilted at "Freely". Thicknesses from 12 to 12.5 all gain 17-18%; the raster makes the gain step, not slide.
        /// </summary>
        [TestMethod]
        public void SmallGainAtThirtyDegreesStaysLevelUntilFreely()
        {
            LabelLayout layout = new(LevelAndTiltedArms(12.25));

            LabelPlacement level = layout.Fit(Content(), LabelLayout.NeverRotate);
            LabelPlacement free = layout.Fit(Content(), LabelLayout.RotationGainForSetting(1));
            Assert.AreEqual(LabelPlacementKind.Run, free.Kind);
            Assert.AreEqual(30 * Degree, free.Angle, 1 * Degree);

            double gain = free.FontSize / level.FontSize;
            Assert.IsTrue(gain > 1 / 0.9 && gain < LabelLayout.RequiredGain(30 * Degree, LabelLayout.DefaultRotationGain),
                $"Test shape should gain between 11% and 25% at 30°, gains {gain:F3}");

            LabelPlacement standard = layout.Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, standard.Kind);
            Assert.AreEqual(0, standard.Angle, 1e-9);
        }

        /// <summary>At "Never" thin tilted shapes get level text: here the inscribed circle, which beats any level box.</summary>
        [TestMethod]
        public void NeverKeepsDiagonalAndVerticalShapesLevel()
        {
            foreach (Polygon shape in new[] { Rotated(Box(-100, -5, 100, 5), 45 * Degree), Box(0, 0, 10, 200) })
            {
                LabelLayout layout = new(shape);
                LabelPlacement placement = layout.Fit(Content(), LabelLayout.RotationGainForSetting(0));
                Assert.AreEqual(0, placement.Angle, 1e-12);
                Assert.AreEqual(LabelPlacementKind.Circle, placement.Kind);
                Assert.AreEqual(LabelLayout.CircleFontSize(layout.InscribedCircle), placement.FontSize, 1e-9);
            }
        }

        /// <summary>At "Never" a shape whose widest level box beats the circle still gets that box.</summary>
        [TestMethod]
        public void NeverUsesTheWidestLevelRun()
        {
            LabelPlacement placement = new LabelLayout(LShape()).Fit(Content(), LabelLayout.NeverRotate);
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(0, placement.Angle, 1e-12);
            Assert.IsTrue(placement.Region.Width > 150, $"Run {placement.Region}");
        }

        /// <summary>
        /// A long box 1.2° off level is laid along its principal axis: the tilt costs about 1%, where the old flat 0.9
        /// penalty preferred the shorter level box that fits inside it.
        /// </summary>
        [TestMethod]
        public void NearLevelPrincipalAxisIsNotPenalised()
        {
            Assert.IsTrue(LabelLayout.TiltFactor(1.2 * Degree, LabelLayout.DefaultRotationGain) > 0.985);

            LabelPlacement placement = new LabelLayout(Rotated(Box(-100, -5, 100, 5), 1.2 * Degree)).Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(1.2 * Degree, placement.Angle, 0.1 * Degree);
        }

        [TestMethod]
        public void RotationGainForSettingMapsNeverDefaultAndFreely()
        {
            Assert.IsTrue(double.IsPositiveInfinity(LabelLayout.RotationGainForSetting(0)));
            Assert.IsTrue(double.IsPositiveInfinity(LabelLayout.RotationGainForSetting(-0.5)));
            Assert.AreEqual(LabelLayout.DefaultRotationGain, LabelLayout.RotationGainForSetting(0.5), 1e-12);
            Assert.AreEqual(0, LabelLayout.RotationGainForSetting(1), 1e-12);
            Assert.AreEqual(0, LabelLayout.RotationGainForSetting(2), 1e-12);
            Assert.AreEqual(LabelLayout.MaxRotationGain * 0.99, LabelLayout.RotationGainForSetting(0.01), 1e-12);
        }

        [TestMethod]
        public void RequiredGainMatchesTheDocumentedTable()
        {
            double g = LabelLayout.DefaultRotationGain;
            Assert.AreEqual(1.0, LabelLayout.RequiredGain(0, g), 1e-12);
            Assert.AreEqual(1.13, LabelLayout.RequiredGain(15 * Degree, g), 0.005);
            Assert.AreEqual(1.25, LabelLayout.RequiredGain(30 * Degree, g), 1e-12);
            Assert.AreEqual(1.35, LabelLayout.RequiredGain(45 * Degree, g), 0.005);
            Assert.AreEqual(1.5, LabelLayout.RequiredGain(90 * Degree, g), 1e-12);
            Assert.AreEqual(1.5, LabelLayout.RequiredGain(-90 * Degree, g), 1e-12);
            Assert.AreEqual(1.0, LabelLayout.RequiredGain(0, LabelLayout.NeverRotate), 1e-12);
            Assert.IsTrue(double.IsPositiveInfinity(LabelLayout.RequiredGain(15 * Degree, LabelLayout.NeverRotate)));
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

        [TestMethod]
        public void RunAnglesAreEveryFiveDegreesPlusThePrincipalAxis()
        {
            double[] steps = [.. Enumerable.Range(0, 36).Select(i => (-85 + (5 * i)) * Degree)];

            double[] offStep = [.. InscribedRuns.Angles(Rotated(Box(-100, -5, 100, 5), 22 * Degree))];
            Assert.AreEqual(37, offStep.Length, "36 steps and the 22° principal axis");
            CollectionAssert.AreEqual(steps, offStep.Take(36).ToArray(), new ToleranceComparer(1e-12));
            Assert.AreEqual(22 * Degree, offStep[36], 1e-6);
            Assert.IsTrue(offStep.All(a => a > -Math.PI / 2 && a <= Math.PI / 2), "Angles must be in (−90°, 90°]");

            double[] nearStep = [.. InscribedRuns.Angles(Rotated(Box(-100, -5, 100, 5), 20.5 * Degree))];
            Assert.AreEqual(36, nearStep.Length, "A principal axis within 1° of a step is not added");
        }

        /// <summary>
        /// A thick arm at 25°, between the old 15° steps and not the shape's principal axis, gets its own angle: at 15°
        /// steps the text sat at 30° or 15° in a shorter box across the arm.
        /// </summary>
        [TestMethod]
        public void ArmBetweenFifteenDegreeStepsGetsItsOwnAngle()
        {
            Polygon shape = LevelAndTiltedArms(14, 25);
            double principal = InscribedRuns.NormalizeAngle(InscribedRuns.PrincipalAxisAngle(shape));
            Assert.IsTrue(Math.Abs(principal - (25 * Degree)) > 2 * Degree, $"Test shape's principal axis is {principal / Degree:F1}°");

            LabelPlacement placement = new LabelLayout(shape).Fit(Content(), LabelLayout.RotationGainForSetting(1));
            Assert.AreEqual(LabelPlacementKind.Run, placement.Kind);
            Assert.AreEqual(25 * Degree, placement.Angle, 1e-9);
        }

        /// <summary>
        /// The 30° arm gains a little font over the level one. Rotation gains G just either side of the tie flip a plain fit
        /// between level and tilted, but with the previous placement passed back the label stays where it is; at "Freely"
        /// the tilt clearly wins and the label moves, and then stays tilted back across the tie.
        /// </summary>
        [TestMethod]
        public void HysteresisKeepsANearTieAndSwitchesWhenTheRivalClearlyWins()
        {
            LabelLayout layout = new(LevelAndTiltedArms(12.25));
            LabelContent content = Content();
            double gain = layout.Fit(content, 0).FontSize / layout.Fit(content, LabelLayout.NeverRotate).FontSize;

            // Tie where gain = 1 + G·sin 30°
            double tie = (gain - 1) / Math.Sin(30 * Degree);
            double levelSide = tie + 0.03, tiltedSide = tie - 0.03;
            Assert.AreEqual(0, layout.Fit(content, levelSide).Angle, 1e-9, "Plain fit above the tie is level");
            Assert.AreEqual(30 * Degree, layout.Fit(content, tiltedSide).Angle, 1 * Degree, "Plain fit below the tie tilts");
            Assert.IsTrue(gain > 1 + (2 * LabelLayout.Hysteresis), $"Test shape should gain clearly more than the hysteresis, gains {gain:F3}");

            LabelPlacement level = layout.Fit(content, levelSide, null);
            foreach (double g in new[] { tiltedSide, levelSide, tiltedSide })
                Assert.AreSame(level, layout.Fit(content, g, level), $"A near tie at G = {g:F3} should keep the level text");

            LabelPlacement tilted = layout.Fit(content, 0, level);
            Assert.AreEqual(30 * Degree, tilted.Angle, 1 * Degree, "At Freely the tilt clearly wins");

            foreach (double g in new[] { levelSide, tiltedSide, levelSide })
                Assert.AreSame(tilted, layout.Fit(content, g, tilted), $"A near tie at G = {g:F3} should keep the tilted text");

            Assert.AreEqual(0, layout.Fit(content, LabelLayout.NeverRotate, tilted).Angle, 1e-12, "Never always levels the text");
        }

        /// <summary>
        /// Between the circle and a run, hysteresis is measured against the circle test: just past the rotation gain where
        /// a thin diagonal box's run beats its circle, the circle is kept, and the run is kept just short of it. A
        /// placement from another layout is never kept.
        /// </summary>
        [TestMethod]
        public void HysteresisKeepsTheCircleAgainstAMarginalRun()
        {
            Polygon shape = Rotated(Box(-100, -5, 100, 5), 45 * Degree);
            LabelLayout layout = new(shape);
            LabelContent content = Content();
            LabelPlacement free = layout.Fit(content, 0);
            double ratio = LabelLayout.RunFontSize(free.Region.Width, free.Region.Height, content, 0) / LabelLayout.CircleFontSize(layout.InscribedCircle);

            // The run wins while ratio >= 1 + G·sin 45°, which is above the 1.25 circle preference here
            double tie = (ratio - 1) / Math.Sin(free.Angle);
            double circleSide = tie * 1.01, runSide = tie * 0.99;
            Assert.AreEqual(LabelPlacementKind.Circle, layout.Fit(content, circleSide).Kind, "Plain fit above the tie takes the circle");
            Assert.AreEqual(LabelPlacementKind.Run, layout.Fit(content, runSide).Kind, "Plain fit below the tie takes the run");

            LabelPlacement circle = layout.Fit(content, circleSide, null);
            Assert.AreSame(circle, layout.Fit(content, runSide, circle), "A marginal run should not replace the circle");

            LabelPlacement run = layout.Fit(content, 0, circle);
            Assert.AreEqual(LabelPlacementKind.Run, run.Kind, "A run several times the circle's font replaces it");
            Assert.AreSame(run, layout.Fit(content, circleSide, run), "A marginal circle should not replace the run");

            LabelPlacement foreign = new LabelLayout(shape).Fit(content, 0);
            Assert.AreEqual(LabelPlacementKind.Circle, layout.Fit(content, circleSide, foreign).Kind, "Another layout's placement is not kept");
        }

        /// <summary>
        /// Hysteresis never keeps a placement the content no longer fits: longer text that would overflow the run at its
        /// font, or a new link that has no marker slot, gets a fresh fit. Shorter text still fits, so the run is kept.
        /// </summary>
        [TestMethod]
        public void HysteresisDropsAPlacementThatNoLongerFits()
        {
            LabelLayout layout = new(Box(0, 0, 400, 20));
            LabelPlacement run = layout.Fit(Content());
            Assert.AreEqual(LabelPlacementKind.Run, run.Kind);

            LabelContent longer = new([new LabelRow(40, 1.0)], 0, 0);
            Assert.IsTrue(LabelLayout.TextPadding * longer.TextWidth * run.FontSize > run.MaxLineWidth, "Longer text should overflow the run's font");
            LabelPlacement refit = layout.Fit(longer, LabelLayout.DefaultRotationGain, run);
            Assert.AreNotSame(run, refit);
            Assert.IsTrue(LabelLayout.TextPadding * longer.TextWidth * refit.FontSize <= refit.MaxLineWidth + 1e-9);

            LabelPlacement linked = layout.Fit(Content(up: 1), LabelLayout.DefaultRotationGain, run);
            Assert.AreNotSame(run, linked);
            Assert.AreEqual(1, linked.Markers.Count);

            Assert.AreSame(run, layout.Fit(new LabelContent([new LabelRow(3, 1.0)], 0, 0), LabelLayout.DefaultRotationGain, run));
        }

        /// <summary>Compares angles within a tolerance for <see cref="CollectionAssert"/>.</summary>
        private sealed class ToleranceComparer(double tolerance) : System.Collections.IComparer
        {
            public int Compare(object x, object y) => Math.Abs((double)x - (double)y) <= tolerance ? 0 : ((double)x).CompareTo((double)y);
        }
    }
}
