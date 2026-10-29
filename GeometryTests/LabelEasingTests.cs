using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>The ease-out and pose interpolation a polygon label uses to move to a new placement.</summary>
    [TestClass]
    public class LabelEasingTests
    {
        private const double Degree = Math.PI / 180.0;

        [TestMethod]
        public void EaseOutHitsItsEndpointsAndClamps()
        {
            Assert.AreEqual(0, LabelEasing.EaseOut(0), 1e-15);
            Assert.AreEqual(1, LabelEasing.EaseOut(1), 1e-15);
            Assert.AreEqual(0, LabelEasing.EaseOut(-0.5), 1e-15);
            Assert.AreEqual(1, LabelEasing.EaseOut(3), 1e-15);
            Assert.AreEqual(0, LabelEasing.EaseOut(double.NaN), 1e-15);
            Assert.AreEqual(0, LabelEasing.Progress(0), 1e-15);
            Assert.AreEqual(1, LabelEasing.Progress(LabelEasing.DurationSeconds), 1e-15);
            Assert.AreEqual(1, LabelEasing.Progress(10), 1e-15);
        }

        /// <summary>Strictly increasing, and ahead of linear throughout: an ease-out moves fast first and settles.</summary>
        [TestMethod]
        public void EaseOutIsMonotoneAndFrontLoaded()
        {
            double previous = 0;
            for (int i = 1; i <= 1000; i++)
            {
                double t = i / 1000.0;
                double e = LabelEasing.EaseOut(t);
                Assert.IsTrue(e > previous, $"Not increasing at t = {t}");
                Assert.IsTrue(e >= t - 1e-15, $"Behind linear at t = {t}");
                previous = e;
            }

            Assert.AreEqual(0.875, LabelEasing.EaseOut(0.5), 1e-15);
        }

        [TestMethod]
        public void AngleTurnsTheShortWayAcrossVertical()
        {
            // 80° to −80° is 20° through vertical, not 160° through level
            Assert.AreEqual(90 * Degree, LabelEasing.TextAngle(80 * Degree, -80 * Degree, 0.5), 1e-12);
            Assert.AreEqual(-85 * Degree, LabelEasing.TextAngle(80 * Degree, -80 * Degree, 0.75), 1e-12);
            Assert.AreEqual(-80 * Degree, LabelEasing.TextAngle(80 * Degree, -80 * Degree, 1), 1e-15);

            Assert.AreEqual(0, LabelEasing.TextAngle(10 * Degree, -10 * Degree, 0.5), 1e-12);
            Assert.AreEqual(15 * Degree, LabelEasing.TextAngle(0, 30 * Degree, 0.5), 1e-12);

            // An exact quarter turn goes the direct way, so it does not cross vertical
            Assert.AreEqual(45 * Degree, LabelEasing.TextAngle(90 * Degree, 0, 0.5), 1e-12);
            Assert.AreEqual(45 * Degree, LabelEasing.TextAngle(0, 90 * Degree, 0.5), 1e-12);
        }

        /// <summary>
        /// For angles on a 5° grid, every step of the turn stays in (−90°, 90°], where text reads left to right, and the
        /// turn never covers more than a quarter turn: the text never passes through upside down.
        /// </summary>
        [TestMethod]
        public void AngleNeverReadsUpsideDown()
        {
            double[] angles = [.. Enumerable.Range(0, 36).Select(i => (-85 + (5 * i)) * Degree)];
            foreach (double from in angles)
            {
                foreach (double to in angles)
                {
                    double turned = 0, last = from;
                    for (int i = 1; i <= 50; i++)
                    {
                        double a = LabelEasing.TextAngle(from, to, i / 50.0);
                        Assert.IsTrue(a > -Math.PI / 2 && a <= Math.PI / 2, $"{from / Degree}° to {to / Degree}° reads upside down at {a / Degree}°");
                        turned += Math.Abs(InscribedRuns.NormalizeAngle(a - last));
                        last = a;
                    }

                    Assert.IsTrue(turned <= (Math.PI / 2) + 1e-9, $"{from / Degree}° to {to / Degree}° turns {turned / Degree}°");
                    Assert.AreEqual(to, last, 1e-12);
                }
            }
        }

        [TestMethod]
        public void PoseInterpolatesCentreAndScalesFontGeometrically()
        {
            LabelPose from = new(new Vector2(0, 0), 0, 4);
            LabelPose to = new(new Vector2(10, -20), 30 * Degree, 16);

            LabelPose start = LabelEasing.Interpolate(from, to, 0);
            Assert.AreEqual(from.Center, start.Center);
            Assert.AreEqual(from.FontSize, start.FontSize, 1e-12);
            Assert.AreEqual(from.Angle, start.Angle, 1e-12);

            LabelPose middle = LabelEasing.Interpolate(from, to, 0.5);
            Assert.AreEqual(5, middle.Center.X, 1e-12);
            Assert.AreEqual(-10, middle.Center.Y, 1e-12);
            Assert.AreEqual(8, middle.FontSize, 1e-12);
            Assert.AreEqual(15 * Degree, middle.Angle, 1e-12);

            LabelPose end = LabelEasing.Interpolate(from, to, 1);
            Assert.AreEqual(to.Center, end.Center);
            Assert.AreEqual(to.FontSize, end.FontSize);
            Assert.AreEqual(to.Angle, end.Angle);

            double previous = from.FontSize;
            for (int i = 1; i <= 20; i++)
            {
                double font = LabelEasing.Interpolate(from, to, i / 20.0).FontSize;
                Assert.IsTrue(font > previous, "Font should grow steadily");
                previous = font;
            }
        }
    }
}
