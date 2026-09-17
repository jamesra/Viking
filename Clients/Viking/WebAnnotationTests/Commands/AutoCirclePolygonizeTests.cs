using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class AutoCirclePolygonizeTests
    {
        [TestMethod]
        public void InsetBoundsShrinksTenPercentOnEachEdge()
        {
            GridRectangle view = new(0, 100, 0, 200);
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(view);

            Assert.AreEqual(10, inset.Left, 1e-6);
            Assert.AreEqual(90, inset.Right, 1e-6);
            Assert.AreEqual(20, inset.Bottom, 1e-6);
            Assert.AreEqual(180, inset.Top, 1e-6);
        }

        [TestMethod]
        public void CircleCenterInsideInsetIsEligible()
        {
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100));
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CIRCLE, new GridVector2(50, 50), inset));
        }

        [TestMethod]
        public void CircleCenterInMarginIsNotEligible()
        {
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CIRCLE, new GridVector2(5, 50), inset));
        }

        [TestMethod]
        public void NonCirclesAreNotEligible()
        {
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.POLYGON, new GridVector2(50, 50), inset));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CURVEPOLYGON, new GridVector2(50, 50), inset));
        }

        [TestMethod]
        public void ZeroPercentMinScreenAreaAcceptsAnyPositiveRadius()
        {
            Assert.IsTrue(AutoPolygonizeSelection.MeetsMinScreenArea(0.01, 1, 1920, 1080, 0));
            Assert.IsFalse(AutoPolygonizeSelection.MeetsMinScreenArea(0, 1, 1920, 1080, 0));
        }

        [TestMethod]
        public void OnePercentMinScreenAreaRejectsSmallerCircles()
        {
            const double viewportWidth = 1000;
            const double viewportHeight = 1000;
            const double pixelSize = 1;
            double minArea = 0.01 * viewportWidth * viewportHeight;
            double thresholdRadius = Math.Sqrt(minArea / Math.PI);

            Assert.IsTrue(AutoPolygonizeSelection.MeetsMinScreenArea(thresholdRadius, pixelSize, viewportWidth, viewportHeight, 1));
            Assert.IsFalse(AutoPolygonizeSelection.MeetsMinScreenArea(thresholdRadius * 0.99, pixelSize, viewportWidth, viewportHeight, 1));
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100)),
                thresholdRadius,
                pixelSize,
                viewportWidth,
                viewportHeight,
                1));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100)),
                thresholdRadius * 0.99,
                pixelSize,
                viewportWidth,
                viewportHeight,
                1));
        }

        [TestMethod]
        public void ShouldUploadEncodedCaptureWhenBoundsUnchanged()
        {
            GridRectangle bounds = new(0, 100, 0, 100);
            Assert.IsTrue(SegmentationViewportSession.ShouldUploadEncodedCapture(bounds, bounds));
        }

        [TestMethod]
        public void ShouldNotUploadEncodedCaptureWhenViewMovedAfterEncode()
        {
            GridRectangle captured = new(0, 100, 0, 100);
            GridRectangle current = new(50, 150, 0, 100);
            Assert.IsFalse(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, current));
        }

        [TestMethod]
        public void ShouldUploadEncodedCaptureWhenViewNudgeIsWithinTolerance()
        {
            GridRectangle captured = new(0, 100, 0, 100);
            GridRectangle current = new(0.5, 100.5, 0.5, 100.5);
            Assert.IsTrue(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, current));
        }

        [TestMethod]
        public void ShouldNotPublishWhenViewMovedAfterCapture()
        {
            GridRectangle captured = new(0, 100, 0, 100);
            GridRectangle live = new(40, 140, 0, 100);
            Assert.IsFalse(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, live));
        }

        [TestMethod]
        public void CacheSkipsUnchangedCircleAndRerunsAfterUpdate()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime second = first.AddMinutes(1);

            Assert.IsTrue(cache.ShouldProcess(7, first, LocationType.CIRCLE));
            cache.RememberProposal(7, first, LocationType.CIRCLE);
            Assert.IsFalse(cache.ShouldProcess(7, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.ShouldProcess(7, second, LocationType.CIRCLE));
        }

        [TestMethod]
        public void DismissedCircleIsSkippedUntilUpdated()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime second = first.AddMinutes(1);

            cache.Dismiss(9, first);
            Assert.IsFalse(cache.ShouldProcess(9, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.ShouldProcess(9, second, LocationType.CIRCLE));
        }

        [TestMethod]
        public void ForegroundPromptHasCenterAndTwoRingsOfEight()
        {
            GridCircle circle = new(new GridVector2(10, 20), 8);
            var points = CircleSegmentationPrompts.CreateMosaicForegroundPoints(circle);

            Assert.AreEqual(17, points.Count);
            Assert.AreEqual(circle.Center, points[0]);

            GridVector2 innerEast = points[1];
            Assert.AreEqual(14, innerEast.X, 1e-6);
            Assert.AreEqual(20, innerEast.Y, 1e-6);

            GridVector2 outerEast = points[9];
            Assert.AreEqual(16, outerEast.X, 1e-6);
            Assert.AreEqual(20, outerEast.Y, 1e-6);
        }

        [TestMethod]
        public void ProposalLineWidthIsHalfResizeBandClampedToTwoAndSixTimesOriginal()
        {
            Assert.AreEqual(4.0, AutoPolygonizeSelection.ProposalLineWidth(16, 1), 1e-6);
            Assert.AreEqual(5.0, AutoPolygonizeSelection.ProposalLineWidth(80, 1), 1e-6);
            Assert.AreEqual(10.0, AutoPolygonizeSelection.ProposalLineWidth(160, 1), 1e-6);
            Assert.AreEqual(12.0, AutoPolygonizeSelection.ProposalLineWidth(256, 1), 1e-6);
            Assert.AreEqual(40.0, AutoPolygonizeSelection.ProposalLineWidth(80, 10), 1e-6);
            Assert.AreEqual(120.0, AutoPolygonizeSelection.ProposalLineWidth(2000, 10), 1e-6);
        }

        [TestMethod]
        public void SimplifyProposalRemovesRedundantVertices()
        {
            GridPolygon polygon = new(
            [
                new GridVector2(0, 0),
                new GridVector2(2, 0.1),
                new GridVector2(4, -0.1),
                new GridVector2(6, 0.1),
                new GridVector2(8, -0.1),
                new GridVector2(10, 0),
                new GridVector2(10.1, 2),
                new GridVector2(9.9, 4),
                new GridVector2(10.1, 6),
                new GridVector2(9.9, 8),
                new GridVector2(10, 10),
                new GridVector2(8, 10.1),
                new GridVector2(6, 9.9),
                new GridVector2(4, 10.1),
                new GridVector2(2, 9.9),
                new GridVector2(0, 10),
                new GridVector2(-0.1, 8),
                new GridVector2(0.1, 6),
                new GridVector2(-0.1, 4),
                new GridVector2(0.1, 2),
                new GridVector2(0, 0)
            ]);

            GridPolygon simplified = AutoPolygonizeSelection.SimplifyProposal(polygon, 1.0);

            Assert.IsTrue(simplified.ExteriorRing.Length < polygon.ExteriorRing.Length);
            Assert.IsTrue(simplified.TotalUniqueVerticies <= 6);
            Assert.IsFalse(simplified.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED));
        }

        [TestMethod]
        public void SimplifyRingsCollapsesColinearEdgeSamples()
        {
            List<GridVector2> ring = [];
            for (int x = 0; x < 40; x++)
                ring.Add(new GridVector2(x, 0));
            for (int y = 0; y < 40; y++)
                ring.Add(new GridVector2(40, y));
            for (int x = 40; x > 0; x--)
                ring.Add(new GridVector2(x, 40));
            for (int y = 40; y > 0; y--)
                ring.Add(new GridVector2(0, y));
            ring.Add(ring[0]);

            GridPolygon dense = new(ring);
            GridPolygon simplified = SegmentationMaskPolygonizer.SimplifyRings(dense, 2.0);

            Assert.IsTrue(simplified.TotalUniqueVerticies <= 4);
            Assert.IsTrue(simplified.TotalUniqueVerticies < dense.TotalUniqueVerticies / 4);
            Assert.IsFalse(simplified.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED));
        }

        [TestMethod]
        public void BackgroundPointsEmptyWhenNoOtherAnnotations()
        {
            var points = CircleSegmentationPrompts.CreateBackgroundVolumePoints([], null);
            Assert.AreEqual(0, points.Count);
        }

        [TestMethod]
        public void SimplePolygonProducesOneNegativePointPerTriangle()
        {
            GridPolygon square = new(
            [
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 10),
                new GridVector2(0, 10),
                new GridVector2(0, 0)
            ]);

            var points = AnnotationPointExtensions.GetPolygonNegativePromptPoints(square);
            var mesh = square.Triangulate();

            Assert.AreEqual(mesh.Faces.Count, points.Count);
            Assert.IsTrue(points.Count >= 2);
            foreach (GridVector2 point in points)
                Assert.IsTrue(square.Contains(point));
        }

        [TestMethod]
        public void ConcavePolygonNegativePointsStayInside()
        {
            GridPolygon concave = new(
            [
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 3),
                new GridVector2(3, 3),
                new GridVector2(3, 10),
                new GridVector2(0, 10),
                new GridVector2(0, 0)
            ]);

            var points = AnnotationPointExtensions.GetPolygonNegativePromptPoints(concave);

            Assert.IsTrue(points.Count >= 2);
            foreach (GridVector2 point in points)
                Assert.IsTrue(concave.Contains(point));
        }

        [TestMethod]
        public void HolePolygonNegativePointsAvoidInteriorHole()
        {
            GridPolygon outer = new(
            [
                new GridVector2(0, 0),
                new GridVector2(10, 0),
                new GridVector2(10, 10),
                new GridVector2(0, 10),
                new GridVector2(0, 0)
            ]);
            GridPolygon hole = new(
            [
                new GridVector2(3, 3),
                new GridVector2(7, 3),
                new GridVector2(7, 7),
                new GridVector2(3, 7),
                new GridVector2(3, 3)
            ]);
            outer.AddInteriorRing(hole);

            var points = AnnotationPointExtensions.GetPolygonNegativePromptPoints(outer);

            Assert.IsTrue(points.Count >= 1);
            foreach (GridVector2 point in points)
            {
                Assert.IsTrue(outer.Contains(point));
                Assert.IsFalse(hole.Contains(point));
            }
        }

        [TestMethod]
        public void PolygonPromptCacheSkipsFactoryUntilLocationUpdates()
        {
            PolygonNegativePromptCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime second = first.AddMinutes(1);
            int factoryCalls = 0;
            GridVector2[] firstPoints = [new GridVector2(1, 1)];
            GridVector2[] secondPoints = [new GridVector2(2, 2)];

            IReadOnlyList<GridVector2> Factory()
            {
                factoryCalls++;
                return factoryCalls == 1 ? firstPoints : secondPoints;
            }

            Func<IReadOnlyList<GridVector2>> factory = Factory;
            var cached = cache.GetOrAdd(12, first, LocationType.CURVEPOLYGON, factory);
            var reused = cache.GetOrAdd(12, first, LocationType.CURVEPOLYGON, factory);
            var updated = cache.GetOrAdd(12, second, LocationType.CURVEPOLYGON, factory);

            Assert.AreEqual(2, factoryCalls);
            Assert.AreEqual(firstPoints[0], cached[0]);
            Assert.AreEqual(firstPoints[0], reused[0]);
            Assert.AreEqual(secondPoints[0], updated[0]);
        }
    }
}
