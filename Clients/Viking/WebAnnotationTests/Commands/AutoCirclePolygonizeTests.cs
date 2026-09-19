using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
            GridRectangle view = new(0, 100, 0, 200);
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(view);

            Assert.AreEqual(5, inset.Left, 1e-6);
            Assert.AreEqual(95, inset.Right, 1e-6);
            Assert.AreEqual(10, inset.Bottom, 1e-6);
            Assert.AreEqual(190, inset.Top, 1e-6);
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
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CIRCLE, new GridVector2(2, 50), inset));
        }

        [TestMethod]
        public void NonCirclesAreNotEligible()
        {
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(new GridRectangle(0, 100, 0, 100));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.POLYGON, new GridVector2(50, 50), inset));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(LocationType.CURVEPOLYGON, new GridVector2(50, 50), inset));
        }

        [TestMethod]
        public void OrderByDistanceFromCenterIsNearestFirst()
        {
            GridRectangle view = new(0, 100, 0, 100);
            GridVector2 center = view.Center;
            GridVector2 onCenter = new(50, 50);
            GridVector2 near = new(60, 50);
            GridVector2 far = new(90, 90);

            List<GridVector2> ordered = AutoPolygonizeSelection.OrderByDistanceFromCenter(
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
            GridVector2 center = new(50, 50);
            GridVector2 right = new(60, 50);
            GridVector2 left = new(40, 50);

            List<GridVector2> ordered = AutoPolygonizeSelection.OrderByDistanceFromCenter(
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
            GridRectangle view = new(-1000, 1000, -1000, 1000);
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                inset,
                view,
                75,
                nmPerWorld,
                minRadiusNm));
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                inset,
                view,
                74.9,
                nmPerWorld,
                minRadiusNm));
        }

        [TestMethod]
        public void CircleThatExtendsOffViewIsNotEligible()
        {
            GridRectangle view = new(0, 100, 0, 100);
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsFalse(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                inset,
                view,
                51,
                1,
                0));
        }

        [TestMethod]
        public void FullyVisibleCircleInsideFivePercentInsetIsEligible()
        {
            GridRectangle view = new(0, 100, 0, 100);
            GridRectangle inset = AutoPolygonizeSelection.InsetBounds(view);
            Assert.IsTrue(AutoPolygonizeSelection.IsEligibleCircle(
                LocationType.CIRCLE,
                new GridVector2(50, 50),
                inset,
                view,
                10,
                1,
                0));
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

            Assert.IsTrue(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            cache.RememberProposal(7, 1, first, LocationType.CIRCLE);
            Assert.IsFalse(cache.ShouldProcess(7, 1, first, LocationType.CIRCLE));
            Assert.IsTrue(cache.ShouldProcess(7, 1, second, LocationType.CIRCLE));
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
        public void BackgroundPointOnForegroundCenterIsDropped()
        {
            GridVector2 center = new(10, 20);
            IReadOnlyList<GridVector2> foreground = [center, new GridVector2(14, 20)];
            IReadOnlyList<GridVector2> background =
            [
                center,
                new GridVector2(10.2, 20.1),
                new GridVector2(100, 100)
            ];

            var kept = CircleSegmentationPrompts.ExceptNearForeground(background, foreground, 1.0);

            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual(new GridVector2(100, 100), kept[0]);
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

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(square);
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

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(concave);

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

            var points = AnnotationPointExtensions.GetPolygonTriangleCentroidPoints(outer);

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
            AutoPolygonizeUploadContext upload = new(42, 4, new GridRectangle(0, 100, 0, 100), 64, 64);
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
            AutoPolygonizeUploadContext context = new(9, 4, new GridRectangle(0, 100, 0, 100), 32, 32);
            Assert.IsTrue(AutoPolygonizeSelection.CanReuseUploadedImage(context, 4, new GridVector2(50, 50)));
            Assert.IsTrue(AutoPolygonizeSelection.CanReuseUploadedImage(context, 6, new GridVector2(50, 50)));
        }

        [TestMethod]
        public void FactorOfTwoZoomOrOutsideBoundsRequiresRecapture()
        {
            AutoPolygonizeUploadContext context = new(9, 4, new GridRectangle(0, 100, 0, 100), 32, 32);
            Assert.IsTrue(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(8, 4));
            Assert.IsTrue(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(2, 4));
            Assert.IsFalse(AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(4, 4));
            Assert.IsFalse(AutoPolygonizeSelection.CanReuseUploadedImage(context, 8, new GridVector2(50, 50)));
            Assert.IsFalse(AutoPolygonizeSelection.CanReuseUploadedImage(context, 4, new GridVector2(200, 200)));
        }

        [TestMethod]
        public void LastImageLeaseReleaseIsReported()
        {
            AutoPolygonizeCache cache = new();
            DateTime first = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AutoPolygonizeUploadContext upload = new(42, 1, new GridRectangle(0, 100, 0, 100), 16, 16);
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

        private static string CreateUnsignedJwt(string payloadJson)
        {
            return $"{ToBase64Url("{}")}.{ToBase64Url(payloadJson)}.sig";
        }

        private static string ToBase64Url(string value)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
