using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="RectangleMappingExtensions.ApproximateVisibleMosaicBounds"/>, which picks the mosaic-space
    /// region to load tiles and annotations for when only some corners of the visible volume rectangle map into the section.
    /// With an identity mapping, mosaic space equals volume space, so every estimate must still cover the visible rectangle.
    /// </summary>
    [TestClass]
    public class ApproximateVisibleMosaicBoundsTests
    {
        /// <summary>Identity transform that can only map volume points for which <c>canMap</c> is true.</summary>
        private sealed class MaskedIdentityTransform(Func<Vector2, bool> canMap, Rectangle? volumeBounds = null) : IVolumeToSectionTransform
        {
            public long ID => 0;
            public Rectangle? SectionBounds => volumeBounds;
            public Rectangle? VolumeBounds => volumeBounds;

            public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP)
            {
                transformedP = P;
                return canMap(P);
            }

            public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP)
            {
                transformedP = [.. Points];
                return [.. Points.Select(canMap)];
            }

            public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) => throw new NotSupportedException();
            public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) => throw new NotSupportedException();
            public Vector2 SectionToVolume(Vector2 P) => throw new NotSupportedException();
            public Vector2 VolumeToSection(Vector2 P) => throw new NotSupportedException();
            public Vector2[] SectionToVolume(Vector2[] Points) => throw new NotSupportedException();
            public Vector2[] VolumeToSection(Vector2[] Points) => throw new NotSupportedException();
        }

        /// <summary>Corner order used by the implementation: LowerLeft, LowerRight, UpperLeft, UpperRight.</summary>
        private static Vector2[] Corners(Rectangle r) => [r.LowerLeft, r.LowerRight, r.UpperLeft, r.UpperRight];

        private static MaskedIdentityTransform MapOnlyCorners(Rectangle visible, bool[] mappedCorners)
        {
            Vector2[] corners = Corners(visible);
            return new MaskedIdentityTransform(p => Enumerable.Range(0, 4).Any(i => mappedCorners[i] && corners[i] == p));
        }

        [TestMethod]
        public void AllCornersMapped_ReturnsTheirBoundingBox()
        {
            Rectangle visible = new(10, 90, 20, 70);
            MaskedIdentityTransform mapper = new(_ => true);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(mapper);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(10, result.Value.Left, 1e-9);
            Assert.AreEqual(90, result.Value.Right, 1e-9);
            Assert.AreEqual(20, result.Value.Bottom, 1e-9);
            Assert.AreEqual(70, result.Value.Top, 1e-9);
        }

        [TestMethod]
        public void LeftEdgeCornersMapped_BoundsStillCoverTheWholeVisibleRectangle()
        {
            // LowerLeft and UpperLeft share the left edge; they are not opposite corners.
            Rectangle visible = new(0, 100, 0, 100);
            MaskedIdentityTransform mapper = MapOnlyCorners(visible, [true, false, true, false]);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(mapper);

            Assert.IsTrue(result.HasValue);
            Assert.IsTrue(result.Value.Contains(visible), $"Estimate {result.Value} must contain visible {visible}.");
        }

        private static void AssertBounds(Rectangle? actual, double left, double right, double bottom, double top)
        {
            Assert.IsTrue(actual.HasValue);
            Assert.AreEqual(left, actual.Value.Left, 1e-6);
            Assert.AreEqual(right, actual.Value.Right, 1e-6);
            Assert.AreEqual(bottom, actual.Value.Bottom, 1e-6);
            Assert.AreEqual(top, actual.Value.Top, 1e-6);
        }

        // Unit square scaled to 100: the circle through opposite corners has center (50,50) and radius 50*sqrt(2).
        private static readonly double DiagonalHalf = 50 * Math.Sqrt(2);

        [TestMethod]
        public void DiagonalCornersMapped_UseCircleThroughThem_LowerLeftUpperRight()
        {
            Rectangle visible = new(0, 100, 0, 100);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, [true, false, false, true]));

            AssertBounds(result, 50 - DiagonalHalf, 50 + DiagonalHalf, 50 - DiagonalHalf, 50 + DiagonalHalf);
        }

        [TestMethod]
        public void DiagonalCornersMapped_UseCircleThroughThem_LowerRightUpperLeft()
        {
            Rectangle visible = new(0, 100, 0, 100);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, [false, true, true, false]));

            AssertBounds(result, 50 - DiagonalHalf, 50 + DiagonalHalf, 50 - DiagonalHalf, 50 + DiagonalHalf);
        }

        [TestMethod]
        public void ThreeCornersMapped_UseCircumcircleOfTheCorners()
        {
            Rectangle visible = new(0, 100, 0, 100);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, [true, true, true, false]));

            AssertBounds(result, 50 - DiagonalHalf, 50 + DiagonalHalf, 50 - DiagonalHalf, 50 + DiagonalHalf);
        }

        [TestMethod]
        public void AdjacentCornersMapped_UseFirstMappedCornerWithWorstCaseRadius()
        {
            // UpperLeft and UpperRight share the top edge; the first mapped corner in order is UpperLeft (0,100).
            Rectangle visible = new(0, 100, 0, 100);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, [false, false, true, true]));

            AssertBounds(result, -144, 144, -44, 244);
        }

        [TestMethod]
        public void SingleCornerMapped_UsesThatCornerWithWorstCaseRadius()
        {
            // Radius is 1.44 * max(width, height), so the taller side drives it.
            Rectangle visible = new(0, 100, 0, 50);

            Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, [false, false, false, true]));

            AssertBounds(result, 100 - 144, 100 + 144, 50 - 144, 50 + 144);
        }

        [TestMethod]
        public void NoCornerMappedAndWideViewNotTouchingVolume_ReturnsNull()
        {
            // Wider than half the volume but disjoint from it: no tiles to load.
            Rectangle visible = new(0, 1000, 0, 10);
            MaskedIdentityTransform mapper = new(_ => false, new Rectangle(0, 100, 500, 600));

            Assert.IsFalse(visible.ApproximateVisibleMosaicBounds(mapper).HasValue);
        }

        [TestMethod]
        public void NoCornerMappedAndWideViewOverlappingVolume_ReturnsSectionBounds()
        {
            Rectangle visible = new(-1000, 50, 20, 30);
            Rectangle volume = new(0, 100, 0, 100);
            MaskedIdentityTransform mapper = new(_ => false, volume);

            Assert.AreEqual(volume, visible.ApproximateVisibleMosaicBounds(mapper));
        }

        [TestMethod]
        public void NoCornerMappedAndViewWidthBetweenHalfAndDoubleVolume_ReturnsSectionBounds()
        {
            Rectangle visible = new(0, 150, 20, 30);
            Rectangle volume = new(0, 100, 0, 100);

            Assert.AreEqual(volume, visible.ApproximateVisibleMosaicBounds(new MaskedIdentityTransform(_ => false, volume)));
        }

        [TestMethod]
        public void NoCornerMappedAndViewExactlyHalfVolumeWidth_ReturnsNull()
        {
            // The comparison is strictly greater than half the volume extent.
            Rectangle visible = new(0, 50, 20, 30);

            Assert.IsFalse(visible.ApproximateVisibleMosaicBounds(new MaskedIdentityTransform(_ => false, new Rectangle(0, 100, 0, 100))).HasValue);
        }

        [TestMethod]
        public void NoCornerMappedAndSmallViewOverlappingVolume_ReturnsNull()
        {
            Rectangle visible = new(40, 60, 40, 60);
            MaskedIdentityTransform mapper = new(_ => false, new Rectangle(0, 100, 0, 100));

            Assert.IsFalse(visible.ApproximateVisibleMosaicBounds(mapper).HasValue);
        }

        [TestMethod]
        public void NoCornerMappedAndVolumeInsideView_ReturnsSectionBounds()
        {
            Rectangle visible = new(0, 100, 0, 100);
            Rectangle volume = new(20, 80, 20, 80);
            MaskedIdentityTransform mapper = new(_ => false, volume);

            Assert.AreEqual(volume, visible.ApproximateVisibleMosaicBounds(mapper));
        }

        [TestMethod]
        public void NoCornerMappedAndNoOverlap_ReturnsNull()
        {
            Rectangle visible = new(0, 10, 0, 10);
            MaskedIdentityTransform mapper = new(_ => false, new Rectangle(500, 600, 500, 600));

            Assert.IsFalse(visible.ApproximateVisibleMosaicBounds(mapper).HasValue);
        }

        [TestMethod]
        public void AnyPartialCornerMapping_BoundsCoverTheVisibleRectangle()
        {
            var inputs =
                from x in Gen.Choose(-100_000, 500_000)
                from y in Gen.Choose(-100_000, 500_000)
                from w in Gen.Choose(1, 200_000)
                from h in Gen.Choose(1, 200_000)
                from mask in Gen.Choose(1, 14)
                select (x, y, w, h, mask);

            var prop = Prop.ForAll(
                inputs.ToArbitrary(),
                input =>
                {
                    Rectangle visible = new(input.x, input.x + input.w, input.y, input.y + input.h);
                    bool[] mapped = [(input.mask & 1) != 0, (input.mask & 2) != 0, (input.mask & 4) != 0, (input.mask & 8) != 0];
                    Rectangle? result = visible.ApproximateVisibleMosaicBounds(MapOnlyCorners(visible, mapped));
                    return result.HasValue && result.Value.Contains(visible);
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }
    }
}
