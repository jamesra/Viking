using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationMaskPolygonizerTests
    {
        [TestMethod]
        public void CShapePreservesConcaveBay()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 8, 7, 17, 12);

            GridPolygon polygon = CreatePolygon(mask, 20, 20, 0.03);

            Assert.IsFalse(polygon.Contains(new GridVector2(13, 10)));
            Assert.IsTrue(polygon.Contains(new GridVector2(5, 10)));
        }

        [TestMethod]
        public void HoleThresholdKeepsLargeHoleAndDropsSmallHole()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 7, 7, 12, 12);

            GridPolygon keepHole = CreatePolygon(mask, 20, 20, 0.10);
            GridPolygon dropHole = CreatePolygon(mask, 20, 20, 0.20);

            Assert.AreEqual(1, keepHole.InteriorRings.Count);
            Assert.AreEqual(0, dropHole.InteriorRings.Count);
        }

        [TestMethod]
        public void SmallHoleWithBackgroundPointIsNotDropped()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 7, 7, 12, 12);

            GridVector2 backgroundPoint = new(9.5, 10.5);
            GridPolygon kept = CreatePolygon(mask, 20, 20, 0.20, [backgroundPoint]);
            GridPolygon dropped = CreatePolygon(mask, 20, 20, 0.20);

            Assert.AreEqual(1, kept.InteriorRings.Count);
            Assert.IsFalse(kept.Contains(backgroundPoint));
            Assert.AreEqual(0, dropped.InteriorRings.Count);
        }

        [TestMethod]
        public void SimplificationRemovesSinglePixelSpike()
        {
            byte[] mask = FilledRectangleMask(24, 24, 3, 3, 18, 18);
            mask[(10 * 24) + 19] = 255;

            GridPolygon polygon = CreatePolygon(mask, 24, 24, 0.03);

            Assert.IsTrue(polygon.TotalUniqueVerticies < 12);
        }

        [TestMethod]
        public void EdgeCleanupRemovesOutwardWisp()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(12 * 24) + 20] = 255;

            byte[] cleaned = SegmentationMaskPolygonizer.CleanMask(mask, 24, 24, 1);
            GridPolygon cleanedPolygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);
            GridVector2 wispPoint = new(20.5, 11.5);

            Assert.AreEqual(0, cleaned[(12 * 24) + 20]);
            Assert.IsFalse(cleanedPolygon.Contains(wispPoint));
        }

        [TestMethod]
        public void EdgeCleanupFillsInwardNotch()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(12 * 24) + 4] = 0;

            byte[] cleaned = SegmentationMaskPolygonizer.CleanMask(mask, 24, 24, 1);
            GridPolygon cleanedPolygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);
            GridVector2 notchPoint = new(4.5, 11.5);

            Assert.AreEqual(255, cleaned[(12 * 24) + 4]);
            Assert.IsTrue(cleanedPolygon.Contains(notchPoint));
        }

        [TestMethod]
        public void EdgeCleanupKeepsSubstantialHole()
        {
            byte[] mask = FilledRectangleMask(24, 24, 2, 2, 21, 21);
            ClearRectangle(mask, 24, 8, 8, 15, 15);

            GridPolygon polygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 2);

            Assert.AreEqual(1, polygon.InteriorRings.Count);
            Assert.IsFalse(polygon.Contains(new GridVector2(12, 12)));
        }

        [TestMethod]
        public void DisabledEdgeCleanupPreservesWisp()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(12 * 24) + 20] = 255;

            byte[] cleaned = SegmentationMaskPolygonizer.CleanMask(mask, 24, 24, 0);

            Assert.AreEqual(255, cleaned[(12 * 24) + 20]);
            Assert.AreSame(mask, cleaned);
        }

        [TestMethod]
        public void ShouldSkipHugeMaskWhenForegroundCoversMostOfALargeImage()
        {
            Assert.IsTrue(SegmentationMaskPolygonizer.ShouldSkipHugeMask(6_060_000, 3596, 2004));
            Assert.IsFalse(SegmentationMaskPolygonizer.ShouldSkipHugeMask(911_000, 3596, 2004));
            Assert.IsFalse(SegmentationMaskPolygonizer.ShouldSkipHugeMask(256, 20, 20));
        }

        [TestMethod]
        public void DownsampledMaskStillProducesAPolygon()
        {
            const int width = 800;
            const int height = 800;
            byte[] mask = FilledRectangleMask(width, height, 200, 200, 399, 399);

            GridPolygon polygon = CreatePolygon(mask, width, height, 0.03);

            Assert.IsTrue(polygon.Area > 0);
            Assert.IsTrue(polygon.Contains(new GridVector2(300, 500)));
            Assert.IsFalse(polygon.Contains(new GridVector2(50, 50)));
            Assert.IsTrue(polygon.TotalUniqueVerticies < 16);
        }

        [TestMethod]
        public void EdgeCleanupProducesSimplePolygon()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(10 * 24) + 20] = 255;
            mask[(14 * 24) + 3] = 255;
            mask[(12 * 24) + 4] = 0;

            GridPolygon polygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);

            Assert.IsFalse(polygon.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED));
            Assert.IsTrue(polygon.Area > 0);
        }

        private static GridPolygon CreatePolygon(
            byte[] mask,
            int width,
            int height,
            double holeDropFraction,
            IReadOnlyList<GridVector2> preserveHolesContainingWorldPoints = null,
            int edgeCleanupRadius = 0)
        {
            var polygons = SegmentationMaskPolygonizer.CreatePolygons(
                mask,
                width,
                height,
                0,
                0,
                width,
                height,
                new GridRectangle(new GridVector2(0, 0), new GridVector2(width, height)),
                holeDropFraction,
                preserveHolesContainingWorldPoints,
                edgeCleanupRadius);

            Assert.AreEqual(1, polygons.Count);
            return polygons[0];
        }

        private static byte[] FilledRectangleMask(
            int width,
            int height,
            int left,
            int top,
            int right,
            int bottom)
        {
            byte[] mask = new byte[width * height];
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                    mask[(y * width) + x] = 255;
            }

            return mask;
        }

        private static void ClearRectangle(
            byte[] mask,
            int width,
            int left,
            int top,
            int right,
            int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                    mask[(y * width) + x] = 0;
            }
        }
    }
}
