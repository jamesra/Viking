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
