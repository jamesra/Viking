using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotationModel;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class AutoCirclePolygonizeTests
    {
        [TestMethod]
        public void InsetBoundsShrinksFivePercentOnEachEdge()
        {
            Rectangle view = new(0, 100, 0, 200);
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(view);

            Assert.AreEqual(5, inset.Left, 1e-6);
            Assert.AreEqual(95, inset.Right, 1e-6);
            Assert.AreEqual(10, inset.Bottom, 1e-6);
            Assert.AreEqual(190, inset.Top, 1e-6);
        }

        [TestMethod]
        public void CircleCenterInsideInsetIsEligible()
        {
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(new Rectangle(0, 100, 0, 100));
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CIRCLE, new Vector2(50, 50), inset));
        }

        [TestMethod]
        public void CircleCenterInMarginIsNotEligible()
        {
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(new Rectangle(0, 100, 0, 100));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CIRCLE, new Vector2(2, 50), inset));
        }

        [TestMethod]
        public void NonCirclesAreNotEligible()
        {
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(new Rectangle(0, 100, 0, 100));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.POLYGON, new Vector2(50, 50), inset));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CURVEPOLYGON, new Vector2(50, 50), inset));
        }

        [TestMethod]
        public void OrderByDistanceFromCenterIsNearestFirst()
        {
            Rectangle view = new(0, 100, 0, 100);
            Vector2 center = view.Center;
            Vector2 onCenter = new(50, 50);
            Vector2 near = new(60, 50);
            Vector2 far = new(90, 90);

            List<Vector2> ordered = AutoPolygonizeSelection.OrderByDistanceFromCenter(
                [far, near, onCenter],
                position => position,
                center);

            Assert.AreEqual(onCenter, ordered[0]);
            Assert.AreEqual(near, ordered[1]);
            Assert.AreEqual(far, ordered[2]);
            Assert.IsTrue(AutoPolygonizeSelection.CompareDistanceFromCenter(onCenter, far, center) < 0);
        }

        [TestMethod]
        public void OrderByDistanceFromCenterKeepsEqualDistanceInputOrder()
        {
            Vector2 center = new(50, 50);
            Vector2 right = new(60, 50);
            Vector2 left = new(40, 50);

            List<Vector2> ordered = AutoPolygonizeSelection.OrderByDistanceFromCenter(
                [right, left],
                position => position,
                center);

            Assert.AreEqual(0, AutoPolygonizeSelection.CompareDistanceFromCenter(right, left, center));
            Assert.AreEqual(right, ordered[0]);
            Assert.AreEqual(left, ordered[1]);
        }

        [TestMethod]
        public void ZeroNanometerMinRadiusAcceptsAnyPositiveRadius()
        {
            Assert.IsTrue(AutoPolygonizeSelection.MeetsMinRadiusNanometers(0.01, 1, 0));
            Assert.IsFalse(AutoPolygonizeSelection.MeetsMinRadiusNanometers(0, 1, 0));
        }

        [TestMethod]
        public void SeventyFiveNanometerMinRadiusRejectsSmallerCircles()
        {
            const double nmPerWorld = 1;
            const double minRadiusNm = 75;

            Assert.IsTrue(AutoPolygonizeSelection.MeetsMinRadiusNanometers(75, nmPerWorld, minRadiusNm));
            Assert.IsFalse(AutoPolygonizeSelection.MeetsMinRadiusNanometers(74.9, nmPerWorld, minRadiusNm));
            Rectangle view = new(-1000, 1000, -1000, 1000);
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new Vector2(50, 50),
                inset,
                view,
                75,
                nmPerWorld,
                minRadiusNm));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new Vector2(50, 50),
                inset,
                view,
                74.9,
                nmPerWorld,
                minRadiusNm));
        }

        [TestMethod]
        public void CircleThatExtendsOffViewIsNotEligible()
        {
            Rectangle view = new(0, 100, 0, 100);
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new Vector2(50, 50),
                inset,
                view,
                51,
                1,
                0));
        }

        [TestMethod]
        public void FullyVisibleCircleInsideFivePercentInsetIsEligible()
        {
            Rectangle view = new(0, 100, 0, 100);
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new Vector2(50, 50),
                inset,
                view,
                10,
                1,
                0));
        }

        [TestMethod]
        public void LargeCircleThatFitsOnScreenIsEligible()
        {
            Rectangle view = new(0, 1000, 0, 1000);
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            const double radius = 400;

            Assert.IsTrue(AutoPolygonizeSelection.IsCircleEntirelyInside(new Vector2(500, 500), radius, view));
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new Vector2(500, 500),
                inset,
                view,
                radius,
                1,
                0));
        }

        [TestMethod]
        public void ShouldUploadEncodedCaptureWhenBoundsUnchanged()
        {
            Rectangle bounds = new(0, 100, 0, 100);
            Assert.IsTrue(SegmentationViewportSession.ShouldUploadEncodedCapture(bounds, bounds));
        }

        [TestMethod]
        public void ShouldNotUploadEncodedCaptureWhenViewMovedAfterEncode()
        {
            Rectangle captured = new(0, 100, 0, 100);
            Rectangle current = new(50, 150, 0, 100);
            Assert.IsFalse(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, current));
        }

        [TestMethod]
        public void ShouldUploadEncodedCaptureWhenViewNudgeIsWithinTolerance()
        {
            Rectangle captured = new(0, 100, 0, 100);
            Rectangle current = new(0.5, 100.5, 0.5, 100.5);
            Assert.IsTrue(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, current));
        }

        [TestMethod]
        public void ShouldNotPublishWhenViewMovedAfterCapture()
        {
            Rectangle captured = new(0, 100, 0, 100);
            Rectangle live = new(40, 140, 0, 100);
            Assert.IsFalse(SegmentationViewportSession.ShouldUploadEncodedCapture(captured, live));
        }

        [TestMethod]
        public void CacheSkipsUnchangedCircleAndRerunsAfterUpdate()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime second = first.AddMinutes(1);

            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.ShouldProcess(7, 1, second, LocationType.CIRCLE));
        }

        [TestMethod]
        public void InvalidateProposalRerunsCircleAndLeavesDismissedCircleSkipped()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            cache.InvalidateProposal(7);
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));

            cache.Dismiss(9, 1, first);
            cache.InvalidateProposal(9);
            Assert.IsFalse(cache.ShouldProcess(9, 1, first, LocationType.CIRCLE));
        }

        [TestMethod]
        public void ConvertedPolygonIsNotProcessedAndRemoveClearsProposal()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CURVEPOLYGON));
            cache.Remove(7);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CURVEPOLYGON));
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
        }

        [TestMethod]
        public void DismissedCircleIsSkippedUntilUpdated()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime second = first.AddMinutes(1);

            cache.Dismiss(9, 1, first);
            Assert.IsFalse(cache.ShouldProcess(9, 1, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.ShouldProcess(9, 1, second, LocationType.CIRCLE));
        }

        [TestMethod]
        public void ForegroundPromptHasCenterAndTwoRingsOfEight()
        {
            Circle circle = new(new Vector2(10, 20), 8);
            var points = CircleSegmentationPrompts.CreateMosaicForegroundPoints(circle);

            Assert.AreEqual(1 + (2 * CircleSegmentationPrompts.ForegroundRingPointCount), points.Count);
            Assert.AreEqual(circle.Center, points[0]);

            Vector2 innerEast = points[1];
            Assert.AreEqual(14, innerEast.X, 1e-6);
            Assert.AreEqual(20, innerEast.Y, 1e-6);

            Vector2 outerEast = points[1 + CircleSegmentationPrompts.ForegroundRingPointCount];
            Assert.AreEqual(16.4, outerEast.X, 1e-6);
            Assert.AreEqual(20, outerEast.Y, 1e-6);
            Assert.AreEqual(
                circle.Radius * CircleSegmentationPrompts.OuterRingRadiusFraction,
                Vector2.Distance(circle.Center, outerEast),
                1e-6);
        }

        [TestMethod]
        public void OtherStructureFilterKeepsDifferentParentsAndTypes()
        {
            long[] excludeSelf = [22974];
            Assert.IsFalse(CircleSegmentationPrompts.IsOtherStructure(22974, 100, excludeSelf, 100));
            Assert.IsFalse(CircleSegmentationPrompts.IsOtherStructure(1, 100, excludeSelf, 100));
            Assert.IsTrue(CircleSegmentationPrompts.IsOtherStructure(169829, 200, excludeSelf, 100));
            Assert.IsTrue(CircleSegmentationPrompts.IsOtherStructure(32227, null, excludeSelf, 100));
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
        public void ProposalKeepsACopyOfLastSentPrompts()
        {
            Polygon square = Square(0, 0, 10);
            List<Vector2> foreground = [new Vector2(1, 1)];
            List<Vector2> background = [new Vector2(2, 2), new Vector2(8, 8)];
            AutoPolygonizeProposal proposal = new(
                null!,
                7,
                1,
                DateTime.UtcNow,
                8,
                square,
                [],
                foregroundPrompts: foreground,
                backgroundPrompts: background);

            Assert.AreEqual(1, proposal.ForegroundPrompts.Count);
            Assert.AreEqual(2, proposal.BackgroundPrompts.Count);
            Assert.AreEqual(new Vector2(1, 1), proposal.ForegroundPrompts[0]);
            Assert.AreEqual(new Vector2(8, 8), proposal.BackgroundPrompts[1]);

            foreground.Add(new Vector2(9, 9));
            background.Clear();
            Assert.AreEqual(1, proposal.ForegroundPrompts.Count);
            Assert.AreEqual(2, proposal.BackgroundPrompts.Count);
        }

        [TestMethod]
        public void SimplifyProposalRemovesRedundantVertices()
        {
            Polygon polygon = new(
            [
                new Vector2(0, 0),
                new Vector2(2, 0.1),
                new Vector2(4, -0.1),
                new Vector2(6, 0.1),
                new Vector2(8, -0.1),
                new Vector2(10, 0),
                new Vector2(10.1, 2),
                new Vector2(9.9, 4),
                new Vector2(10.1, 6),
                new Vector2(9.9, 8),
                new Vector2(10, 10),
                new Vector2(8, 10.1),
                new Vector2(6, 9.9),
                new Vector2(4, 10.1),
                new Vector2(2, 9.9),
                new Vector2(0, 10),
                new Vector2(-0.1, 8),
                new Vector2(0.1, 6),
                new Vector2(-0.1, 4),
                new Vector2(0.1, 2),
                new Vector2(0, 0)
            ]);

            Polygon simplified = AutoPolygonizeSelection.SimplifyProposal(polygon, 1.0);

            Assert.IsTrue(simplified.ExteriorRing.Length < polygon.ExteriorRing.Length);
            Assert.IsTrue(simplified.TotalUniqueVertices <= 6);
            Assert.IsFalse(simplified.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));
        }

        [TestMethod]
        public void MaskContourToleranceKeepsALobeThePenThresholdCuts()
        {
            Polygon polygon = new(
            [
                new Vector2(0, 0),
                new Vector2(40, 0),
                new Vector2(40, 20),
                new Vector2(24, 20),
                new Vector2(20, 26),
                new Vector2(16, 20),
                new Vector2(0, 20),
                new Vector2(0, 0)
            ]);
            Vector2 lobeTip = new(20, 26);

            Polygon tight = AutoPolygonizeSelection.SimplifyProposal(
                polygon,
                AutoPolygonizeSelection.MaskContourTolerancePixels);
            Polygon loose = AutoPolygonizeSelection.SimplifyProposal(polygon, 12);
            Polygon tightCurve = new(tight.ExteriorRing.CalculateCurvePoints(8, true));
            Polygon looseCurve = new(loose.ExteriorRing.CalculateCurvePoints(8, true));

            Assert.IsTrue(tightCurve.Distance(lobeTip) <= AutoPolygonizeSelection.MaskContourTolerancePixels + 0.5);
            Assert.IsTrue(looseCurve.Distance(lobeTip) > 4);
        }

        [TestMethod]
        public void CreatedShapeSimplifyKeepsALobeTheLooserThresholdCuts()
        {
            Polygon polygon = new(
            [
                new Vector2(0, 0),
                new Vector2(40, 0),
                new Vector2(40, 20),
                new Vector2(24, 20),
                new Vector2(20, 30),
                new Vector2(16, 20),
                new Vector2(0, 20),
                new Vector2(0, 0)
            ]);
            Vector2 lobeTip = new(20, 30);

            Polygon created = new(AutoPolygonizeSelection.SimplifyProposal(
                polygon,
                AutoPolygonizeSelection.CreatedShapeSimplifyPixels).ExteriorRing.CalculateCurvePoints(8, true));
            Polygon penDefault = new(AutoPolygonizeSelection.SimplifyProposal(polygon, 12)
                .ExteriorRing.CalculateCurvePoints(8, true));

            Assert.IsTrue(created.Distance(lobeTip) <= AutoPolygonizeSelection.CreatedShapeSimplifyPixels + 0.5);
            Assert.IsTrue(penDefault.Distance(lobeTip) > created.Distance(lobeTip));
        }

        [TestMethod]
        public void SimplifyProposalFitsCurveControlPointsWithinTolerance()
        {
            List<Vector2> ring = [];
            const int samples = 80;
            for (int i = 0; i < samples; i++)
            {
                double t = 2 * Math.PI * i / samples;
                ring.Add(new Vector2(50 * Math.Cos(t), 30 * Math.Sin(t)));
            }

            ring.Add(ring[0]);
            Polygon original = new(ring);

            const double tolerance = 1.0;
            Polygon dpOnly = SegmentationMaskPolygonizer.SimplifyRings(original, tolerance);
            Polygon fitted = AutoPolygonizeSelection.SimplifyProposal(original, tolerance);

            Assert.IsTrue(fitted.TotalUniqueVertices < dpOnly.TotalUniqueVertices,
                $"Fit should reduce DP vertices {dpOnly.TotalUniqueVertices} -> {fitted.TotalUniqueVertices}");
            Assert.IsFalse(fitted.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));

            Polygon fittedCurve = new(fitted.ExteriorRing.CalculateCurvePoints(8, true));
            double maxDistance = 0;
            foreach (Vector2 p in original.ExteriorRing)
            {
                double distance = fittedCurve.Distance(p);
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            Assert.IsTrue(maxDistance <= tolerance * 2,
                $"Fitted curve drifted {maxDistance} from original (limit {tolerance * 2})");
        }

        [TestMethod]
        public void SimplifyRingsCollapsesColinearEdgeSamples()
        {
            List<Vector2> ring = [];
            for (int x = 0; x < 40; x++)
                ring.Add(new Vector2(x, 0));
            for (int y = 0; y < 40; y++)
                ring.Add(new Vector2(40, y));
            for (int x = 40; x > 0; x--)
                ring.Add(new Vector2(x, 40));
            for (int y = 40; y > 0; y--)
                ring.Add(new Vector2(0, y));
            ring.Add(ring[0]);

            Polygon dense = new(ring);
            Polygon simplified = SegmentationMaskPolygonizer.SimplifyRings(dense, 2.0);

            Assert.IsTrue(simplified.TotalUniqueVertices <= 4);
            Assert.IsTrue(simplified.TotalUniqueVertices < dense.TotalUniqueVertices / 4);
            Assert.IsFalse(simplified.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed));
        }

        [TestMethod]
        public void BackgroundPointsEmptyWhenNoOtherAnnotations()
        {
            var points = CircleSegmentationPrompts.CreateBackgroundVolumePoints([], null);
            Assert.AreEqual(0, points.Count);
        }

        [TestMethod]
        public void BackgroundPointOnForegroundCenterIsDropped()
        {
            Vector2 center = new(10, 20);
            IReadOnlyList<Vector2> foreground = [center, new Vector2(14, 20)];
            IReadOnlyList<Vector2> background =
            [
                center,
                new Vector2(10.2, 20.1),
                new Vector2(100, 100)
            ];

            var kept = CircleSegmentationPrompts.ExceptNearForeground(background, foreground, 1.0);

            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual(new Vector2(100, 100), kept[0]);
        }

        [TestMethod]
        public void SimplePolygonProducesOneNegativePointPerTriangle()
        {
            Polygon square = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ]);

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(square);
            var mesh = square.Triangulate();

            Assert.AreEqual(mesh.Faces.Count, points.Count);
            Assert.IsTrue(points.Count >= 2);
            foreach (Vector2 point in points)
                Assert.IsTrue(square.Contains(point));
        }

        [TestMethod]
        public void PolygonAvoidMarkIsLargestTriangleCentroid()
        {
            Polygon lShape = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 3),
                new Vector2(3, 3),
                new Vector2(3, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ]);

            IReadOnlyList<Vector2> points = AnnotationPointExtensions.GetLargestTriangleCentroidPoint(lShape);
            Assert.AreEqual(1, points.Count);
            Assert.IsTrue(lShape.Contains(points[0]));

            var mesh = lShape.Triangulate();
            Vector2 origin = lShape.Centroid;
            double maxArea = double.NegativeInfinity;
            Vector2 expected = default;
            foreach (IFace face in mesh.Faces)
            {
                if (!face.IsTriangle())
                    continue;

                Vector2 centroid = mesh.Centroid(face) + origin;
                if (!lShape.Contains(centroid))
                    continue;

                double area = mesh.ToTriangle(face).Area;
                if (area <= maxArea)
                    continue;

                maxArea = area;
                expected = centroid;
            }

            Assert.AreEqual(expected.X, points[0].X, 1e-6);
            Assert.AreEqual(expected.Y, points[0].Y, 1e-6);
        }

        [TestMethod]
        public void ConcavePolygonNegativePointsStayInside()
        {
            Polygon concave = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 3),
                new Vector2(3, 3),
                new Vector2(3, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ]);

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(concave);

            Assert.IsTrue(points.Count >= 2);
            foreach (Vector2 point in points)
                Assert.IsTrue(concave.Contains(point));
        }

        [TestMethod]
        public void DecimateCentroidsKeepsDistantSmallPointsAndEqualAreas()
        {
            double largestArea = 2 * Math.PI;
            PolygonTriangleCentroid[] samples =
            [
                new(new Vector2(0, 0), largestArea),
                new(new Vector2(0.5, 0), 1),
                new(new Vector2(10, 0), 1),
                new(new Vector2(0.1, 0), largestArea)
            ];

            IReadOnlyList<Vector2> kept = PolygonSegmentationPrompts.DecimateCentroids(samples);

            Assert.AreEqual(3, kept.Count);
            Assert.AreEqual(new Vector2(0, 0), kept[0]);
            Assert.AreEqual(new Vector2(0.1, 0), kept[1]);
            Assert.AreEqual(new Vector2(10, 0), kept[2]);
        }

        [TestMethod]
        public void PolygonSegmentationPointsUseUnsmoothedControlRingNotSmoothedVolumeShape()
        {
            Polygon unsmoothed = CreateRegularPolygon(6, 20);
            Polygon smoothed = unsmoothed.Smooth(Geometry.Global.NumClosedCurveInterpolationPoints);

            IReadOnlyList<Vector2> prompts = PolygonSegmentationPrompts.CreateMosaicForegroundPoints(unsmoothed);
            IReadOnlyList<Vector2> fromControlRing = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(unsmoothed);
            IReadOnlyList<Vector2> ifSmoothed = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(smoothed);
            IReadOnlyList<Vector2> largest = AnnotationPointExtensions.GetLargestTriangleCentroidPoint(unsmoothed);

            Assert.IsTrue(prompts.Count > 0);
            Assert.IsTrue(prompts.Count <= fromControlRing.Count);
            foreach (Vector2 prompt in prompts)
            {
                Assert.IsTrue(fromControlRing.Any(raw =>
                    Math.Abs(raw.X - prompt.X) < 1e-6 && Math.Abs(raw.Y - prompt.Y) < 1e-6));
            }

            Assert.AreEqual(1, largest.Count);
            Assert.IsTrue(prompts.Any(prompt =>
                Math.Abs(prompt.X - largest[0].X) < 1e-6 && Math.Abs(prompt.Y - largest[0].Y) < 1e-6));
            Assert.IsTrue(ifSmoothed.Count > prompts.Count);
        }

        [TestMethod]
        public void PolygonNegativePointsUseUnsmoothedMosaicShapeNotSmoothedVolumeShape()
        {
            AnnotationPointExtensions.PolygonPromptCache.Clear();
            try
            {
                Polygon unsmoothed = CreateRegularPolygon(6, 20);
                Polygon smoothed = unsmoothed.Smooth(Geometry.Global.NumClosedCurveInterpolationPoints);

                LocationObj loc = new();
                loc.TypeCode = LocationType.CURVEPOLYGON;
                loc.MosaicShape = unsmoothed.ToSqlGeometry();
                loc.VolumeShape = smoothed.ToSqlGeometry();

                IReadOnlyList<Vector2> points = AnnotationPointExtensions.GetAnnotationRepresentativePoints([loc]);
                IReadOnlyList<Vector2> expected = AnnotationPointExtensions.GetLargestTriangleCentroidPoint(unsmoothed);
                IReadOnlyList<Vector2> ifSmoothed = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(smoothed);

                Assert.AreEqual(1, points.Count);
                Assert.AreEqual(1, expected.Count);
                Assert.AreEqual(expected[0].X, points[0].X, 1e-6);
                Assert.AreEqual(expected[0].Y, points[0].Y, 1e-6);
                Assert.IsTrue(ifSmoothed.Count > expected.Count);
            }
            finally
            {
                AnnotationPointExtensions.PolygonPromptCache.Clear();
            }
        }

        [TestMethod]
        public void HolePolygonNegativePointsAvoidInteriorHole()
        {
            Polygon outer = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0)
            ]);
            Polygon hole = new(
            [
                new Vector2(3, 3),
                new Vector2(7, 3),
                new Vector2(7, 7),
                new Vector2(3, 7),
                new Vector2(3, 3)
            ]);
            outer.AddInteriorRing(hole);

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(outer);

            Assert.IsTrue(points.Count >= 1);
            foreach (Vector2 point in points)
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
            Vector2[] firstPoints = [new Vector2(1, 1)];
            Vector2[] secondPoints = [new Vector2(2, 2)];

            IReadOnlyList<Vector2> Factory()
            {
                factoryCalls++;
                return factoryCalls == 1 ? firstPoints : secondPoints;
            }

            Func<IReadOnlyList<Vector2>> factory = Factory;
            var cached = cache.GetOrAdd(12, first, LocationType.CURVEPOLYGON, factory);
            var reused = cache.GetOrAdd(12, first, LocationType.CURVEPOLYGON, factory);
            var updated = cache.GetOrAdd(12, second, LocationType.CURVEPOLYGON, factory);

            Assert.AreEqual(2, factoryCalls);
            Assert.AreEqual(firstPoints[0], cached[0]);
            Assert.AreEqual(firstPoints[0], reused[0]);
            Assert.AreEqual(secondPoints[0], updated[0]);
        }

        [TestMethod]
        public void CacheRemoveAllowsSameLastModifiedAgain()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            cache.Remove(7);
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
        }

        [TestMethod]
        public void GeometryPropertyNamesAreRecognized()
        {
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(nameof(LocationObj.MosaicShape)));
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(nameof(LocationObj.VolumeShape)));
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(nameof(LocationObj.Position)));
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(nameof(LocationObj.Radius)));
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(null));
            Assert.IsTrue(AutoPolygonizeSelection.IsGeometryProperty(""));
            Assert.IsFalse(AutoPolygonizeSelection.IsGeometryProperty(nameof(LocationObj.TypeCode)));
        }

        [TestMethod]
        public void GeometryCommitDropsSkipAndKeepsUploadContext()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AutoPolygonizeUploadContext upload = new(42, 4, new Rectangle(0, 100, 0, 100), 64, 64);
            int invalidated = 0;
            cache.GeometryInvalidated += (_, _, _) => invalidated++;

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE, upload: upload);
            cache.ProcessPropertyChanged(7, nameof(LocationObj.MosaicShape), LocationType.CIRCLE);

            Assert.AreEqual(1, invalidated);
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.TryGetUploadContext(7, out AutoPolygonizeUploadContext kept));
            Assert.AreEqual(42ul, kept.ImageId);
            Assert.AreEqual(4, kept.Downsample, 1e-6);
        }

        [TestMethod]
        public void ClearSectionDoesNotTouchOtherSections()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            cache.RememberProposal(8, 2, first, LocationType.CIRCLE);

            cache.ClearSection(1);

            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            Assert.IsFalse(cache.ShouldProcess(8, 2, first, LocationType.CIRCLE));
        }

        [TestMethod]
        public void RememberSubscribesAndRemoveUnsubscribes()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            LocationObj location = new();
            location.TypeCode = LocationType.CIRCLE;

            cache.RememberProposal(location.ID, location.Section, first, LocationType.CIRCLE, location);
            Assert.IsTrue(cache.IsSubscribed(location.ID));

            cache.Remove(location.ID);
            Assert.IsFalse(cache.IsSubscribed(location.ID));
            Assert.IsFalse(cache.Contains(location.ID));
        }

        [TestMethod]
        public void TypeCodeLeavingCircleForgetsLocation()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            LocationObj location = new();
            location.TypeCode = LocationType.CIRCLE;
            int forgotten = 0;
            cache.LocationForgotten += _ => forgotten++;

            cache.RememberProposal(location.ID, location.Section, first, LocationType.CIRCLE, location);
            location.TypeCode = LocationType.POLYGON;

            Assert.AreEqual(1, forgotten);
            Assert.IsFalse(cache.Contains(location.ID));
        }

        [TestMethod]
        public void SameZoomAndBoundsReusesUploadedImage()
        {
            AutoPolygonizeUploadContext context = new(9, 4, new Rectangle(0, 100, 0, 100), 32, 32);
            Assert.IsTrue(AutoPolygonizeSelection.CanReuseUploadedImage(context, 4, new Vector2(50, 50)));
            Assert.IsTrue(AutoPolygonizeSelection.CanReuseUploadedImage(context, 6, new Vector2(50, 50)));
        }

        [TestMethod]
        public void FinerViewByFactorOfTwoResubmitsAndCoarserViewKeepsProposal()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE, completedDownsample: 4);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 4));
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 3));
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 8));
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 2));
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 1));

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE, completedDownsample: 2);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 2));
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 4));
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 1));
        }

        [TestMethod]
        public void DismissedCircleStaysSkippedWhenViewGetsFiner()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            cache.Dismiss(9, 1, first);
            Assert.IsFalse(cache.ShouldProcess(9, 1, first, LocationType.CIRCLE, liveDownsample: 1));
        }

        [TestMethod]
        public void UploadDownsampleIsTheCompletionBaselineWhenCallerOmitsIt()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AutoPolygonizeUploadContext upload = new(42, 8, new Rectangle(0, 100, 0, 100), 16, 16);

            cache.RememberProposal(7, 1, first, LocationType.CIRCLE, upload: upload);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 8));
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 16));
            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE, liveDownsample: 4));
        }

        [TestMethod]
        public void ResolutionIncreasedByFactorOfTwoIsOneDirection()
        {
            Assert.IsTrue(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(2, 4));
            Assert.IsTrue(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(1, 4));
            Assert.IsFalse(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(2.1, 4));
            Assert.IsFalse(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(8, 4));
            Assert.IsFalse(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(4, 4));
            Assert.IsFalse(AutoPolygonizeSelection.ResolutionIncreasedByFactorOfTwo(2, 0));
        }

        [TestMethod]
        public void FactorOfTwoZoomOrOutsideBoundsRequiresRecapture()
        {
            AutoPolygonizeUploadContext context = new(9, 4, new Rectangle(0, 100, 0, 100), 32, 32);
            Assert.IsTrue(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(8, 4));
            Assert.IsTrue(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(2, 4));
            Assert.IsFalse(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(4, 4));
            Assert.IsFalse(AutoPolygonizeSelection.CanReuseUploadedImage(context, 8, new Vector2(50, 50)));
            Assert.IsFalse(AutoPolygonizeSelection.CanReuseUploadedImage(context, 4, new Vector2(200, 200)));
        }

        [TestMethod]
        public void LastImageLeaseReleaseIsReported()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AutoPolygonizeUploadContext upload = new(42, 1, new Rectangle(0, 100, 0, 100), 16, 16);
            List<ulong> released = [];
            cache.ImageLeaseReleased += id => released.Add(id);

            cache.AcquireBatchHold(42);
            cache.RememberProposal(7, 1, first, LocationType.CIRCLE, upload: upload);
            cache.ReleaseBatchHold(42);
            Assert.AreEqual(0, released.Count);
            Assert.IsTrue(cache.IsImageHeld(42));

            cache.Remove(7);
            Assert.AreEqual(1, released.Count);
            Assert.AreEqual(42ul, released[0]);
            Assert.IsFalse(cache.IsImageHeld(42));
        }

        [TestMethod]
        public void OverlappingSameCellPolygonsFormOneComponentAndExcludeDisjointSibling()
        {
            Polygon left = Square(0, 0, 10);
            Polygon overlap = Square(5, 0, 10);
            Polygon far = Square(100, 0, 10);
            OverlapCandidate seed = new([1], 9, 1, left);
            List<OverlapCandidate> pool =
            [
                seed,
                new([2], 9, 1, overlap),
                new([3], 9, 1, far)
            ];

            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(seed, pool);
            long[] ids = [.. component.SelectMany(item => item.LocationIds).Distinct().OrderBy(id => id)];

            CollectionAssert.AreEqual(new long[] { 1, 2 }, ids);
        }

        [TestMethod]
        public void OverlappingDifferentCellsAreNotGrouped()
        {
            Polygon left = Square(0, 0, 10);
            Polygon overlap = Square(5, 0, 10);
            OverlapCandidate seed = new([1], 9, 1, left);
            List<OverlapCandidate> pool =
            [
                seed,
                new([2], 10, 1, overlap)
            ];

            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(seed, pool);
            Assert.AreEqual(1, component.Count);
            Assert.AreEqual(1, component[0].LocationIds[0]);
        }

        [TestMethod]
        public void OrphanProposalIsNotGrouped()
        {
            Polygon left = Square(0, 0, 10);
            Polygon overlap = Square(5, 0, 10);
            OverlapCandidate seed = new([1], null, 1, left);
            List<OverlapCandidate> pool =
            [
                seed,
                new([2], null, 1, overlap)
            ];

            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(seed, pool);
            Assert.AreEqual(1, component.Count);
        }

        [TestMethod]
        public void OverlapChainIncludesTransitiveSiblings()
        {
            OverlapCandidate a = new([1], 9, 1, Square(0, 0, 10));
            OverlapCandidate b = new([2], 9, 1, Square(8, 0, 10));
            OverlapCandidate c = new([3], 9, 1, Square(16, 0, 10));
            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(
                a,
                [a, b, c]);
            long[] ids = [.. component.SelectMany(item => item.LocationIds).Distinct().OrderBy(id => id)];
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, ids);
        }

        [TestMethod]
        public void NestedContainedSameCellPolygonsFormOneComponent()
        {
            OverlapCandidate outer = new([1], 9, 1, Square(0, 0, 20));
            OverlapCandidate inner = new([2], 9, 1, Square(6, 6, 8));
            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(
                outer,
                [outer, inner]);
            long[] ids = [.. component.SelectMany(item => item.LocationIds).Distinct().OrderBy(id => id)];
            CollectionAssert.AreEqual(new long[] { 1, 2 }, ids);

            List<OverlapCandidate> fromInner = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(
                inner,
                [outer, inner]);
            long[] fromInnerIds = [.. fromInner.SelectMany(item => item.LocationIds).Distinct().OrderBy(id => id)];
            CollectionAssert.AreEqual(new long[] { 1, 2 }, fromInnerIds);
        }

        [TestMethod]
        public void EdgeTouchingSameCellPolygonsAreNotGrouped()
        {
            OverlapCandidate left = new([1], 9, 1, Square(0, 0, 10));
            OverlapCandidate right = new([2], 9, 1, Square(10, 0, 10));
            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(
                left,
                [left, right]);
            Assert.AreEqual(1, component.Count);
            Assert.AreEqual(1, component[0].LocationIds[0]);
        }

        [TestMethod]
        public void SurvivorIsMostLinksThenLowestId()
        {
            Assert.AreEqual(2, LocationSiblingMerge.ChooseSurvivorId([(5, 1), (2, 3), (9, 3)]));
            Assert.AreEqual(4, LocationSiblingMerge.ChooseSurvivorId([(7, 2), (4, 2)]));
        }

        [TestMethod]
        public void UniqueNeighborTransferSkipsSharedAndSameSectionIds()
        {
            IReadOnlyList<long> created = LocationSiblingMerge.UniqueNeighborIdsToTransfer(
                survivorLinks: [20],
                groupIds: [1, 2],
                victimLinkSets:
                [
                    [20, 30, 2, 1],
                    [30, 40]
                ]);

            CollectionAssert.AreEqual(new long[] { 30, 40 }, created.ToArray());
        }

        [TestMethod]
        public void PolygonForegroundPointsIncludeCentroidAndSubsampledRing()
        {
            Polygon square = Square(0, 0, 10);
            IReadOnlyList<Vector2> points = CircleSegmentationPrompts.CreateForegroundPointsFromPolygons([square], 16);

            Assert.IsTrue(points.Count >= 5);
            Assert.AreEqual(square.Centroid, points[0]);
            Assert.IsTrue(square.Contains(points[0]));
            Assert.IsTrue(points.Skip(1).Any(p => p == new Vector2(0, 0) || p == new Vector2(10, 0)));
        }

        [TestMethod]
        public void PolygonForegroundPointsRejectEmptyAndDegenerate()
        {
            Assert.AreEqual(0, CircleSegmentationPrompts.CreateForegroundPointsFromPolygons(null).Count);
            Assert.AreEqual(0, CircleSegmentationPrompts.CreateForegroundPointsFromPolygons([]).Count);
        }

        [TestMethod]
        public void AnnotateScopeDoesNotGrantReviewAccess()
        {
            string token = CreateUnsignedJwt("{\"scope\":\"openid Viking.Annotation RC2.annotate\"}");
            Assert.IsFalse(VolumeAccessRoles.TokenGrantsReviewAccess(token, "RC2"));
        }

        [TestMethod]
        public void ReviewAndAdminScopesGrantReviewAccess()
        {
            string review = CreateUnsignedJwt("{\"scope\":\"openid Viking.Annotation RC2.review\"}");
            string admin = CreateUnsignedJwt("{\"scope\":\"openid Viking.Annotation RC2.admin\"}");
            Assert.IsTrue(VolumeAccessRoles.TokenGrantsReviewAccess(review, "RC2"));
            Assert.IsTrue(VolumeAccessRoles.TokenGrantsReviewAccess(admin, "RC2"));
        }

        [TestMethod]
        public void RoleClaimReviewGrantsReviewAccess()
        {
            string token = CreateUnsignedJwt("{\"role\":\"Review\"}");
            Assert.IsTrue(VolumeAccessRoles.TokenGrantsReviewAccess(token, "RC2"));
        }

        [TestMethod]
        public void MissingTokenDoesNotGrantReviewAccess()
        {
            Assert.IsFalse(VolumeAccessRoles.TokenGrantsReviewAccess(null, "RC2"));
            Assert.IsFalse(VolumeAccessRoles.TokenGrantsReviewAccess("", "RC2"));
        }

        [TestMethod]
        public void OnlyHotkeyStartedPlacementLeavesPenMode()
        {
            Assert.IsFalse(PlacementInput.UsePenStroke(penMode: true, startedByHotkey: true));
            Assert.IsTrue(PlacementInput.UsePenStroke(penMode: true, startedByHotkey: false));
            Assert.IsFalse(PlacementInput.UsePenStroke(penMode: false, startedByHotkey: true));
            Assert.IsFalse(PlacementInput.UsePenStroke(penMode: false, startedByHotkey: false));
        }

        [TestMethod]
        public void SubtractLeavesProposalUnchangedWhenExistingDoesNotOverlap()
        {
            Polygon proposed = Square(0, 0, 10);
            Polygon far = Square(100, 0, 10);

            Polygon result = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [far],
                new Vector2(5, 5));

            Assert.AreSame(proposed, result);
        }

        [TestMethod]
        public void SubtractIgnoresBoundaryOnlyTouch()
        {
            Polygon proposed = Square(0, 0, 10);
            Polygon touching = Square(10, 0, 10);

            Polygon result = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [touching],
                new Vector2(5, 5));

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains(new Vector2(5, 5)));
            Assert.AreEqual(proposed.Area, result.Area, 1e-2);
        }

        [TestMethod]
        public void SubtractPartialOverlapRemovesExistingArea()
        {
            Polygon proposed = Square(0, 0, 10);
            Polygon overlap = Square(5, 0, 10);

            Polygon result = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [overlap],
                new Vector2(2, 5));

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains(new Vector2(2, 5)));
            Assert.IsFalse(result.Contains(new Vector2(7, 5)));
            Assert.IsTrue(result.Area < proposed.Area);
        }

        [TestMethod]
        public void SubtractContainedExistingBecomesInteriorHole()
        {
            Polygon proposed = Square(0, 0, 20);
            Polygon inside = Square(6, 6, 8);

            Polygon result = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [inside],
                new Vector2(1, 1));

            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasInteriorRings);
            Assert.IsTrue(result.Contains(new Vector2(1, 1)));
            Assert.IsFalse(result.Contains(new Vector2(10, 10)));
        }

        [TestMethod]
        public void SubtractReturnsNullWhenExistingCoversProposal()
        {
            Polygon proposed = Square(0, 0, 10);
            Polygon cover = Square(-1, -1, 20);

            Polygon result = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [cover],
                new Vector2(5, 5));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SubtractSplitKeepsPartContainingKeepPoint()
        {
            Polygon proposed = Square(0, 0, 20);
            Polygon cutter = new(
            [
                new Vector2(8, -1),
                new Vector2(12, -1),
                new Vector2(12, 21),
                new Vector2(8, 21),
                new Vector2(8, -1)
            ]);

            Polygon left = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [cutter],
                new Vector2(2, 10));
            Polygon right = AutoPolygonizeSelection.SubtractOverlappingPolygons(
                proposed,
                [cutter],
                new Vector2(16, 10));

            Assert.IsNotNull(left);
            Assert.IsNotNull(right);
            Assert.IsTrue(left.Contains(new Vector2(2, 10)));
            Assert.IsFalse(left.Contains(new Vector2(16, 10)));
            Assert.IsTrue(right.Contains(new Vector2(16, 10)));
            Assert.IsFalse(right.Contains(new Vector2(2, 10)));
        }

        [TestMethod]
        public void TryGetVolumePolygonAcceptsPolygonAndCurvePolygonOnly()
        {
            LocationObj polygon = new();
            polygon.TypeCode = LocationType.POLYGON;
            polygon.VolumeShape = Square(0, 0, 10).ToSqlGeometry();

            LocationObj curve = new();
            curve.TypeCode = LocationType.CURVEPOLYGON;
            curve.VolumeShape = Square(20, 0, 10).ToSqlGeometry();

            LocationObj circle = new();
            circle.TypeCode = LocationType.CIRCLE;
            circle.VolumeShape = Square(40, 0, 10).ToSqlGeometry();

            Assert.IsTrue(AutoPolygonizeSelection.TryGetVolumePolygon(polygon, null, out Polygon? fromPolygon));
            Assert.IsNotNull(fromPolygon);
            Assert.IsTrue(fromPolygon.Contains(new Vector2(5, 5)));

            Assert.IsTrue(AutoPolygonizeSelection.TryGetVolumePolygon(curve, null, out Polygon? fromCurve));
            Assert.IsNotNull(fromCurve);

            Assert.IsFalse(AutoPolygonizeSelection.TryGetVolumePolygon(circle, null, out _));
        }

        [TestMethod]
        public void CollectOverlappingExistingPolygonsSkipsCirclesAndNonOverlapping()
        {
            LocationObj overlap = new();
            overlap.TypeCode = LocationType.POLYGON;
            overlap.VolumeShape = Square(5, 0, 10).ToSqlGeometry();

            LocationObj far = new();
            far.TypeCode = LocationType.POLYGON;
            far.VolumeShape = Square(100, 0, 10).ToSqlGeometry();

            LocationObj circle = new();
            circle.TypeCode = LocationType.CIRCLE;
            circle.VolumeShape = Square(0, 0, 10).ToSqlGeometry();

            List<Polygon> collected = AutoPolygonizeSelection.CollectOverlappingExistingPolygons(
                [overlap, far, circle],
                Square(0, 0, 10));

            Assert.AreEqual(1, collected.Count);
            Assert.IsTrue(collected[0].Contains(new Vector2(7, 5)));
        }

        [TestMethod]
        public void CollectOverlappingExistingPolygonsSkipsSameParentSiblings()
        {
            LocationObj sibling = new();
            sibling.TypeCode = LocationType.POLYGON;
            sibling.VolumeShape = Square(5, 0, 10).ToSqlGeometry();

            List<Polygon> carved = AutoPolygonizeSelection.CollectOverlappingExistingPolygons(
                [sibling],
                Square(0, 0, 10),
                excludeParentId: 0);
            Assert.AreEqual(0, carved.Count);

            List<Polygon> otherCell = AutoPolygonizeSelection.CollectOverlappingExistingPolygons(
                [sibling],
                Square(0, 0, 10),
                excludeParentId: 9);
            Assert.AreEqual(1, otherCell.Count);
        }

        [TestMethod]
        public void CollectSavedSameCellPolygonCandidatesSkipsCircles()
        {
            LocationObj polygon = new();
            polygon.TypeCode = LocationType.POLYGON;
            polygon.VolumeShape = Square(0, 0, 10).ToSqlGeometry();

            LocationObj circle = new();
            circle.TypeCode = LocationType.CIRCLE;
            circle.VolumeShape = Square(0, 0, 10).ToSqlGeometry();

            List<OverlapCandidate> saved = AutoPolygonizeSelection.CollectSavedSameCellPolygonCandidates(
                [polygon, circle],
                parentId: 0,
                sectionNumber: 0);

            Assert.AreEqual(1, saved.Count);
            Assert.IsTrue(saved[0].Polygon.Contains(new Vector2(5, 5)));
        }

        [TestMethod]
        public void CollectOverlappingSameCellComponentsFindsNestedAndChainGroups()
        {
            OverlapCandidate outer = new([1], 9, 1, Square(0, 0, 20));
            OverlapCandidate inner = new([2], 9, 1, Square(6, 6, 8));
            OverlapCandidate chainA = new([3], 9, 1, Square(30, 0, 10));
            OverlapCandidate chainB = new([4], 9, 1, Square(38, 0, 10));
            OverlapCandidate lone = new([5], 9, 1, Square(100, 0, 10));

            List<List<OverlapCandidate>> components = AutoPolygonizeSelection.CollectOverlappingSameCellComponents(
                [outer, inner, chainA, chainB, lone]);

            Assert.AreEqual(2, components.Count);
            HashSet<long>[] idSets =
            [
                [.. components[0].SelectMany(item => item.LocationIds)],
                [.. components[1].SelectMany(item => item.LocationIds)]
            ];
            Assert.IsTrue(idSets.Any(ids => ids.SetEquals([1, 2])));
            Assert.IsTrue(idSets.Any(ids => ids.SetEquals([3, 4])));
        }

        private static string CreateUnsignedJwt(string payloadJson)
        {
            return $"{ToBase64Url("{}")}.{ToBase64Url(payloadJson)}.sig";
        }

        private static string ToBase64Url(string value)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static Polygon Square(double left, double bottom, double size)
        {
            return new(
            [
                new Vector2(left, bottom),
                new Vector2(left + size, bottom),
                new Vector2(left + size, bottom + size),
                new Vector2(left, bottom + size),
                new Vector2(left, bottom)
            ]);
        }

        /// <summary>Closed regular n-gon centered at the origin. Used to contrast Smooth() vertex growth.</summary>
        private static Polygon CreateRegularPolygon(int sides, double radius)
        {
            Vector2[] ring = new Vector2[sides + 1];
            for (int i = 0; i < sides; i++)
            {
                double angle = 2.0 * Math.PI * i / sides;
                ring[i] = new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            ring[sides] = ring[0];
            return new Polygon(ring);
        }
    }
}
