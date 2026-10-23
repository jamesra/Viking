using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// A circle carried between mosaic and volume space keeps its centre and takes the mean scale of the mapped rim points
    /// (VikingAU's rule; see .cursor/rules/circle-mapping-scale.mdc).
    /// </summary>
    [TestClass]
    public class CircleMappingTests
    {
        static readonly bool[] AllMapped = [true, true, true, true, true];

        static Vector2[] Map(Vector2[] points, Func<Vector2, Vector2> transform) => [.. points.Select(transform)];

        [TestMethod]
        public void SamplePointsAreTheCentreThenEastNorthWestSouth()
        {
            Vector2[] points = CircleMapping.SamplePoints(new Vector2(100, 200), 10);
            CollectionAssert.AreEqual(
                new[] { new Vector2(100, 200), new Vector2(110, 200), new Vector2(100, 210), new Vector2(90, 200), new Vector2(100, 190) },
                points);
        }

        [TestMethod]
        public void UniformScaleScalesTheRadiusAndMapsTheCentre()
        {
            Vector2[] source = CircleMapping.SamplePoints(new Vector2(100, 200), 10);
            Vector2[] mapped = Map(source, p => new Vector2(p.X * 1.5 + 7, p.Y * 1.5 - 3));

            Assert.IsTrue(CircleMapping.TryCircleFromMappedPoints(10, mapped, AllMapped, out Circle circle));
            Assert.AreEqual(157, circle.Center.X, 1e-9);
            Assert.AreEqual(297, circle.Center.Y, 1e-9);
            Assert.AreEqual(15, circle.Radius, 1e-9);
        }

        [TestMethod]
        public void ARotationDoesNotShrinkTheRadiusTheWayABoundingBoxWould()
        {
            // Rotating a ring by 45 degrees makes the bounding box of its four rim points r * sqrt(2) wide, not 2r.
            double angle = Math.PI / 4;
            Vector2[] source = CircleMapping.SamplePoints(new Vector2(0, 0), 10);
            Vector2[] mapped = Map(source, p => new Vector2(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle), p.X * Math.Sin(angle) + p.Y * Math.Cos(angle)));

            Assert.IsTrue(CircleMapping.TryCircleFromMappedPoints(10, mapped, AllMapped, out Circle circle));
            Assert.AreEqual(10, circle.Radius, 1e-9);
        }

        [TestMethod]
        public void TheRadiusIsTheMeanOfTheScalesNotTheMedian()
        {
            // Rim scales 1, 1, 1, 5: the mean is 2 (the median would be 1).
            Vector2[] mapped = [new(0, 0), new(10, 0), new(0, 10), new(-10, 0), new(0, -50)];
            Assert.IsTrue(CircleMapping.TryCircleFromMappedPoints(10, mapped, AllMapped, out Circle circle));
            Assert.AreEqual(20, circle.Radius, 1e-9);
        }

        [TestMethod]
        public void OnlyTheRimPointsThatMappedContributeToTheScale()
        {
            // The north point failed (and holds garbage); the other three give a scale of 2.
            Vector2[] mapped = [new(5, 5), new(25, 5), new(1e9, 1e9), new(-15, 5), new(5, -15)];
            bool[] ok = [true, true, false, true, true];
            Assert.IsTrue(CircleMapping.TryCircleFromMappedPoints(10, mapped, ok, out Circle circle));
            Assert.AreEqual(5, circle.Center.X, 1e-9);
            Assert.AreEqual(20, circle.Radius, 1e-9);
        }

        [TestMethod]
        public void ACentreThatDoesNotMapMakesTheCircleUnmappable()
        {
            Vector2[] mapped = CircleMapping.SamplePoints(new Vector2(0, 0), 10);
            Assert.IsFalse(CircleMapping.TryCircleFromMappedPoints(10, mapped, [false, true, true, true, true], out _));
        }

        [TestMethod]
        public void NoMappedRimPointMakesTheCircleUnmappableRatherThanReusingTheRadius()
        {
            Vector2[] mapped = CircleMapping.SamplePoints(new Vector2(0, 0), 10);
            Assert.IsFalse(CircleMapping.TryCircleFromMappedPoints(10, mapped, [true, false, false, false, false], out _));
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(-1.0)]
        [DataRow(double.NaN)]
        public void ANonPositiveSourceRadiusIsRefused(double radius)
        {
            Vector2[] mapped = CircleMapping.SamplePoints(new Vector2(0, 0), 10);
            Assert.IsFalse(CircleMapping.TryCircleFromMappedPoints(radius, mapped, AllMapped, out _));
        }

        [TestMethod]
        public void TheMappedPointsMustBeTheFiveSamplePoints()
        {
            Assert.ThrowsException<ArgumentException>(() => CircleMapping.TryCircleFromMappedPoints(10, [new Vector2(0, 0)], [true], out _));
        }
    }
}
