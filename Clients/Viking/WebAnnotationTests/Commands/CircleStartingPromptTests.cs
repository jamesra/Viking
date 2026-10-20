using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class CircleStartingPromptTests
    {
        private static readonly double InscribedHalfSide = 1.0 / Math.Sqrt(2.0);

        [TestMethod]
        public void MosaicPointsAreTheCenterThenTheEastNorthWestSouthEndsOfTheRadius()
        {
            Circle circle = new(new Vector2(100, 200), 40);

            IReadOnlyList<Vector2> points = CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle);

            Assert.AreEqual(5, points.Count);
            Assert.AreEqual(circle.Center, points[0]);
            Assert.AreEqual(100 + 40.0, points[1].X, 1e-9);
            Assert.AreEqual(200.0, points[1].Y, 1e-9);
            Assert.AreEqual(100.0, points[2].X, 1e-9);
            Assert.AreEqual(200 + 40.0, points[2].Y, 1e-9);
            Assert.AreEqual(100 - 40.0, points[3].X, 1e-9);
            Assert.AreEqual(100.0, points[4].X, 1e-9);
            Assert.AreEqual(200 - 40.0, points[4].Y, 1e-9);
        }

        [TestMethod]
        public void TheClicksSitAtNinetyFivePercentOfTheRadiusAlongTheAxes()
        {
            Circle circle = new(new Vector2(100, 200), 40);
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle), out CircleSegmentationPrompts.StartingPrompt prompt));

            Assert.AreEqual(100 + 38.0, prompt.Points[0].X, 1e-9);
            Assert.AreEqual(200.0, prompt.Points[0].Y, 1e-9);
            Assert.AreEqual(100.0, prompt.Points[1].X, 1e-9);
            Assert.AreEqual(200 + 38.0, prompt.Points[1].Y, 1e-9);
            Assert.AreEqual(100 - 38.0, prompt.Points[2].X, 1e-9);
            Assert.AreEqual(200.0, prompt.Points[2].Y, 1e-9);
            Assert.AreEqual(100.0, prompt.Points[3].X, 1e-9);
            Assert.AreEqual(200 - 38.0, prompt.Points[3].Y, 1e-9);
        }

        [TestMethod]
        public void TheBoxIsTheSquareInscribedInTheCircle()
        {
            Circle circle = new(new Vector2(100, 200), 40);
            IReadOnlyList<Vector2> points = CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle);

            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(points, out CircleSegmentationPrompts.StartingPrompt prompt));

            double half = 40 * InscribedHalfSide;
            Assert.AreEqual(100 - half, prompt.Box.Left, 1e-9);
            Assert.AreEqual(100 + half, prompt.Box.Right, 1e-9);
            Assert.AreEqual(200 - half, prompt.Box.Bottom, 1e-9);
            Assert.AreEqual(200 + half, prompt.Box.Top, 1e-9);
            Assert.AreEqual(100.0, prompt.Center.X, 1e-9);
            Assert.AreEqual(200.0, prompt.Center.Y, 1e-9);
            // Its corners lie on the circle.
            Assert.AreEqual(40.0, Vector2.Distance(circle.Center, new Vector2(prompt.Box.Right, prompt.Box.Top)), 1e-9);
        }

        [TestMethod]
        public void TheFourClicksAreOutsideTheSquareAndInsideTheCircle()
        {
            Circle circle = new(new Vector2(0, 0), 100);
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle), out CircleSegmentationPrompts.StartingPrompt prompt));

            Assert.AreEqual(4, prompt.Points.Count);
            foreach (Vector2 point in prompt.Points)
            {
                Assert.IsFalse(
                    point.X >= prompt.Box.Left && point.X <= prompt.Box.Right &&
                    point.Y >= prompt.Box.Bottom && point.Y <= prompt.Box.Top,
                    "a click inside the square would say nothing the box does not");
                Assert.IsTrue(Vector2.Distance(circle.Center, point) < circle.Radius);
            }
        }

        [TestMethod]
        public void AScalingTransformScalesTheBox()
        {
            Circle circle = new(new Vector2(10, 20), 30);
            IReadOnlyList<Vector2> mosaic = CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle);
            Vector2[] volume = [.. mosaic.Select(point => new Vector2(point.X * 4, point.Y * 4))];

            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(volume, out CircleSegmentationPrompts.StartingPrompt prompt));

            Assert.AreEqual(4 * 2 * 30 * InscribedHalfSide, prompt.Box.Right - prompt.Box.Left, 1e-6);
            Assert.AreEqual(40.0, prompt.Center.X, 1e-9);
            Assert.AreEqual(80.0, prompt.Center.Y, 1e-9);
        }

        [TestMethod]
        public void ARotatingTransformStillGivesAnAxisAlignedBoxOfTheSameSize()
        {
            Circle circle = new(new Vector2(0, 0), 50);
            double angle = Math.PI / 5.0;
            Vector2[] volume =
            [
                .. CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle).Select(point => new Vector2(
                    (point.X * Math.Cos(angle)) - (point.Y * Math.Sin(angle)),
                    (point.X * Math.Sin(angle)) + (point.Y * Math.Cos(angle))))
            ];

            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(volume, out CircleSegmentationPrompts.StartingPrompt prompt));

            Assert.AreEqual(2 * 50 * InscribedHalfSide, prompt.Box.Right - prompt.Box.Left, 1e-6);
            Assert.AreEqual(2 * 50 * InscribedHalfSide, prompt.Box.Top - prompt.Box.Bottom, 1e-6);
        }

        [TestMethod]
        public void ARotatingTransformStillPutsTheClicksExactlyOnTheVolumeAxes()
        {
            Circle circle = new(new Vector2(30, 40), 50);
            double angle = Math.PI / 5.0;
            Vector2[] volume =
            [
                .. CircleSegmentationPrompts.CreateMosaicRadiusPoints(circle).Select(point => new Vector2(
                    (point.X * Math.Cos(angle)) - (point.Y * Math.Sin(angle)),
                    (point.X * Math.Sin(angle)) + (point.Y * Math.Cos(angle))))
            ];
            Vector2 center = volume[0];

            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(volume, out CircleSegmentationPrompts.StartingPrompt prompt));

            double reach = 50 * CircleSegmentationPrompts.StartingPointRadiusFraction;
            Assert.AreEqual(center.X + reach, prompt.Points[0].X, 1e-6);
            Assert.AreEqual(center.Y, prompt.Points[0].Y, 1e-6);
            Assert.AreEqual(center.X, prompt.Points[1].X, 1e-6);
            Assert.AreEqual(center.Y + reach, prompt.Points[1].Y, 1e-6);
            Assert.AreEqual(center.X - reach, prompt.Points[2].X, 1e-6);
            Assert.AreEqual(center.Y, prompt.Points[2].Y, 1e-6);
            Assert.AreEqual(center.X, prompt.Points[3].X, 1e-6);
            Assert.AreEqual(center.Y - reach, prompt.Points[3].Y, 1e-6);
        }

        [TestMethod]
        public void APointTheTransformCouldNotMapMeansNoPrompt()
        {
            IReadOnlyList<Vector2> four = [.. CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(new Vector2(0, 0), 10)).Take(4)];

            Assert.IsFalse(CircleSegmentationPrompts.TryCreateStartingPrompt(four, out _));
            Assert.IsFalse(CircleSegmentationPrompts.TryCreateStartingPrompt(null, out _));
        }

        [TestMethod]
        public void AZeroRadiusMeansNoPrompt()
        {
            IReadOnlyList<Vector2> points = CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(new Vector2(5, 5), 0));

            Assert.IsFalse(CircleSegmentationPrompts.TryCreateStartingPrompt(points, out _));
        }
    }
}
