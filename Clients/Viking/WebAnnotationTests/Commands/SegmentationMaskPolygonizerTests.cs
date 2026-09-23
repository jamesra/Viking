using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationMaskPolygonizerTests
    {
        [TestMethod]
        public void SoftProbabilityEdgeIsNotSnappedToThePixelGrid()
        {
            const int width = 8;
            const int height = 8;
            byte[] mask = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x <= 5; x++)
                    mask[(y * width) + x] = 255;
                mask[(y * width) + 6] = 180;
            }

            Polygon polygon = CreatePolygon(mask, width, height, 0.03);
            double maxX = polygon.ExteriorRing.Max(point => point.X);
            double crossing = 6 + ((127.5 - 180.0) / (0.0 - 180.0));

            Assert.AreEqual(crossing, maxX, 0.02);
        }

        [TestMethod]
        public void CShapePreservesConcaveBay()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 8, 7, 17, 12);

            Polygon polygon = CreatePolygon(mask, 20, 20, 0.03);

            Assert.IsFalse(polygon.Contains(new Vector2(13, 10)));
            Assert.IsTrue(polygon.Contains(new Vector2(5, 10)));
        }

        [TestMethod]
        public void HoleThresholdKeepsLargeHoleAndDropsSmallHole()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 7, 7, 12, 12);

            Polygon keepHole = CreatePolygon(mask, 20, 20, 0.10);
            Polygon dropHole = CreatePolygon(mask, 20, 20, 0.20);

            Assert.AreEqual(1, keepHole.InteriorRings.Count);
            Assert.AreEqual(0, dropHole.InteriorRings.Count);
        }

        [TestMethod]
        public void SmallHoleWithBackgroundPointIsNotDropped()
        {
            byte[] mask = FilledRectangleMask(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 7, 7, 12, 12);

            Vector2 backgroundPoint = new(9.5, 10.5);
            Polygon kept = CreatePolygon(mask, 20, 20, 0.20, [backgroundPoint]);
            Polygon dropped = CreatePolygon(mask, 20, 20, 0.20);

            Assert.AreEqual(1, kept.InteriorRings.Count);
            Assert.IsFalse(kept.Contains(backgroundPoint));
            Assert.AreEqual(0, dropped.InteriorRings.Count);
        }

        [TestMethod]
        public void FullResolutionContourKeepsSinglePixelSpikeUntilPenSimplify()
        {
            byte[] mask = FilledRectangleMask(24, 24, 3, 3, 18, 18);
            mask[(10 * 24) + 19] = 255;

            Polygon raw = CreatePolygon(mask, 24, 24, 0.03);
            Polygon simplified = WebAnnotation.UI.AutoPolygonize.AutoPolygonizeSelection.SimplifyProposal(raw, 2.0);

            Assert.IsTrue(raw.TotalUniqueVertices > 16);
            Assert.IsTrue(simplified.TotalUniqueVertices < raw.TotalUniqueVertices);
            Assert.IsTrue(simplified.TotalUniqueVertices < 12);
            Assert.IsFalse(simplified.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));
        }

        [TestMethod]
        public void EdgeCleanupRemovesOutwardWisp()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(12 * 24) + 20] = 255;

            byte[] cleaned = SegmentationMaskPolygonizer.CleanMask(mask, 24, 24, 1);
            Polygon cleanedPolygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);
            Vector2 wispPoint = new(20.5, 11.5);

            Assert.AreEqual(0, cleaned[(12 * 24) + 20]);
            Assert.IsFalse(cleanedPolygon.Contains(wispPoint));
        }

        [TestMethod]
        public void EdgeCleanupFillsInwardNotch()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(12 * 24) + 4] = 0;

            byte[] cleaned = SegmentationMaskPolygonizer.CleanMask(mask, 24, 24, 1);
            Polygon cleanedPolygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);
            Vector2 notchPoint = new(4.5, 11.5);

            Assert.AreEqual(255, cleaned[(12 * 24) + 4]);
            Assert.IsTrue(cleanedPolygon.Contains(notchPoint));
        }

        [TestMethod]
        public void EdgeCleanupKeepsSubstantialHole()
        {
            byte[] mask = FilledRectangleMask(24, 24, 2, 2, 21, 21);
            ClearRectangle(mask, 24, 8, 8, 15, 15);

            Polygon polygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 2);

            Assert.AreEqual(1, polygon.InteriorRings.Count);
            Assert.IsFalse(polygon.Contains(new Vector2(12, 12)));
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
        public void LargeMaskContourFollowsTheMask()
        {
            const int width = 800;
            const int height = 800;
            byte[] mask = FilledRectangleMask(width, height, 200, 200, 399, 399);

            Polygon polygon = CreatePolygon(mask, width, height, 0.03);

            Assert.IsTrue(polygon.Area > 0);
            Assert.IsTrue(polygon.Contains(new Vector2(300, 500)));
            Assert.IsFalse(polygon.Contains(new Vector2(50, 50)));
            Assert.IsTrue(polygon.TotalUniqueVertices > 16);
        }

        [TestMethod]
        public void EdgeCleanupProducesSimplePolygon()
        {
            byte[] mask = FilledRectangleMask(24, 24, 4, 4, 19, 19);
            mask[(10 * 24) + 20] = 255;
            mask[(14 * 24) + 3] = 255;
            mask[(12 * 24) + 4] = 0;

            Polygon polygon = CreatePolygon(mask, 24, 24, 0.03, edgeCleanupRadius: 1);

            Assert.IsFalse(polygon.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));
            Assert.IsTrue(polygon.Area > 0);
        }

        private static Polygon CreatePolygon(
            byte[] mask,
            int width,
            int height,
            double holeDropFraction,
            IReadOnlyList<Vector2> preserveHolesContainingWorldPoints = null,
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
                new Rectangle(new Vector2(0, 0), new Vector2(width, height)),
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
