using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="Vector2Extensions.Average(System.Collections.Generic.ICollection{Vector2})"/> and the
    /// <see cref="Vector3"/> overload on closed rings: the duplicate closing vertex is left out of both the sum and the
    /// count, so a closed ring averages to the mean of its distinct vertices. Polygon.CalculateCentroid and
    /// PolygonArea pass closed rings and translate by this average to keep volume-scale coordinates near the origin.
    /// </summary>
    [TestClass]
    public class AverageClosedRingSpec
    {
        /// <summary>Volume coordinates reach the hundreds of thousands of pixels.</summary>
        private const int VolumeOffsetRange = 500_000;

        [TestMethod]
        public void ClosedRightTriangleAveragesToVertexMean()
        {
            Vector2[] ring = [new(0, 0), new(3, 0), new(0, 3), new(0, 0)];
            Assert.AreEqual(new Vector2(1, 1), ring.Average());
        }

        [TestMethod]
        public void ClosedSquareAtVolumeScaleAveragesToCenter()
        {
            Vector2[] ring = [new(200_000, 300_000), new(200_010, 300_000), new(200_010, 300_010), new(200_000, 300_010), new(200_000, 300_000)];
            Assert.AreEqual(new Vector2(200_005, 300_005), ring.Average());
        }

        [TestMethod]
        public void OpenRingAveragesEveryVertex()
        {
            Vector2[] points = [new(0, 0), new(3, 0), new(0, 3)];
            Assert.AreEqual(new Vector2(1, 1), points.Average());
        }

        [TestMethod]
        public void SinglePointAveragesToItself()
        {
            Assert.AreEqual(new Vector2(7, -4), new[] { new Vector2(7, -4) }.Average());
            Assert.AreEqual(new Vector3(7, -4, 2), new[] { new Vector3(7, -4, 2) }.Average());
        }

        [TestMethod]
        public void EmptyCollectionThrows()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Array.Empty<Vector2>().Average());
            Assert.ThrowsException<InvalidOperationException>(() => Array.Empty<Vector3>().Average());
        }

        [TestMethod]
        public void ClosedRightTriangle3DAveragesToVertexMean()
        {
            Vector3[] ring = [new(0, 0, 0), new(3, 0, 6), new(0, 3, 3), new(0, 0, 0)];
            Assert.AreEqual(new Vector3(1, 1, 3), ring.Average());
        }

        [TestMethod]
        public void ClosedRingAverageIsMeanOfDistinctVertices() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(OpenRingAtVolumeScale()), open =>
                {
                    Vector2[] closed = [.. open, open[0]];
                    Vector2 expected = new(
                        (double)(open.Sum(p => (decimal)p.X) / open.Length),
                        (double)(open.Sum(p => (decimal)p.Y) / open.Length));

                    return closed.Average() == expected && open.Average() == expected;
                }),
                nameof(ClosedRingAverageIsMeanOfDistinctVertices));

        [TestMethod]
        public void ClosedRing3DAverageIsMeanOfDistinctVertices() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(OpenRing3DAtVolumeScale()), open =>
                {
                    Vector3[] closed = [.. open, open[0]];
                    Vector3 expected = new(
                        (double)(open.Sum(p => (decimal)p.X) / open.Length),
                        (double)(open.Sum(p => (decimal)p.Y) / open.Length),
                        (double)(open.Sum(p => (decimal)p.Z) / open.Length));

                    return closed.Average() == expected && open.Average() == expected;
                }),
                nameof(ClosedRing3DAverageIsMeanOfDistinctVertices));

        /// <summary>
        /// A centered ring is within ~20 px of the origin, so the centroid should carry only the rounding of the volume
        /// coordinates themselves (ULP at 500,000 px is ~6e-11) and of the decimal reference conversion (~1e-10). This
        /// leaves room for both while staying 1,000 times finer than the library's 0.001 px resolution.
        /// </summary>
        private const double CenteredCentroidTolerance = 1e-6;

        /// <summary>
        /// Regression input: a 1.2 px triangle at (500000, 500000). The old divisor left the "centered" ring about 125,000 px
        /// from the origin, and the centroid's cross-product terms cancelled to a result 0.034 px off.
        /// </summary>
        [TestMethod]
        public void SmallTriangleCentroidAtVolumeScaleIsVertexMean()
        {
            Vector2[] ring = [new(500_000.1, 500_000.2), new(500_001.3, 500_000.2), new(500_000.1, 500_001.3), new(500_000.1, 500_000.2)];
            Vector2 expected = new(500_000.5, 500_000.5666666666666667);
            Assert.AreEqual(0.0, Vector2.Distance(expected, Polygon.CalculateCentroid(ring)), CenteredCentroidTolerance);
        }

        /// <summary>
        /// The area centroid of a triangle is its vertex mean, so it has an exact decimal reference. Small triangles at
        /// volume offsets are where centering by the ring average matters for precision.
        /// </summary>
        [TestMethod]
        public void TriangleCentroidAtVolumeScaleMatchesVertexMean() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(SmallTriangleAtVolumeScale()), triangle =>
                {
                    Vector2[] ring = [.. triangle, triangle[0]];
                    Vector2 expected = new(
                        (double)(triangle.Sum(p => (decimal)p.X) / 3),
                        (double)(triangle.Sum(p => (decimal)p.Y) / 3));

                    double error = Vector2.Distance(Polygon.CalculateCentroid(ring), expected);
                    return (error <= CenteredCentroidTolerance).Label($"centroid error {error:E3} px");
                }),
                nameof(TriangleCentroidAtVolumeScaleMatchesVertexMean));

        private static Gen<Vector2> VolumeOffset() =>
            from x in Gen.Choose(-VolumeOffsetRange, VolumeOffsetRange)
            from y in Gen.Choose(-VolumeOffsetRange, VolumeOffsetRange)
            select new Vector2(x, y);

        private static Gen<Vector2[]> OpenRingAtVolumeScale() =>
            from offset in VolumeOffset()
            from n in Gen.Choose(2, 12)
            from local in CoreArbitraries.FiniteVector2().ArrayOf(n)
            where local[0] != local[n - 1]
            select local.Select(p => p + offset).ToArray();

        private static Gen<Vector3[]> OpenRing3DAtVolumeScale() =>
            from offset in VolumeOffset()
            from z in Gen.Choose(0, 5000)
            from n in Gen.Choose(2, 12)
            from local in CoreArbitraries.FiniteVector3().ArrayOf(n)
            where local[0] != local[n - 1]
            select local.Select(p => new Vector3(p.X + offset.X, p.Y + offset.Y, p.Z + z)).ToArray();

        /// <summary>
        /// Non-degenerate triangles up to 20 px across on a 0.01 px grid. Hundredths are not binary fractions, so the
        /// shoelace products round; a power-of-two grid would keep them exact and hide the precision loss.
        /// </summary>
        private static Gen<Vector2[]> SmallTriangleAtVolumeScale() =>
            from offset in VolumeOffset()
            from coords in Gen.Choose(0, 2000).ArrayOf(6)
            let a = new Vector2(coords[0] / 100.0, coords[1] / 100.0)
            let b = new Vector2(coords[2] / 100.0, coords[3] / 100.0)
            let c = new Vector2(coords[4] / 100.0, coords[5] / 100.0)
            where Math.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y))) >= 1.0
            select new[] { a + offset, b + offset, c + offset };
    }
}
