using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Code that holds a location but no transform (link previews, labels, flag feedback, navigation) draws at the
    /// location's <see cref="LocationVolumeGeometry"/> under the open volume, not at the stored VolumeX/VolumeY/VolumeShape
    /// cache or the mosaic radius.
    /// </summary>
    [TestClass]
    public class StoredVolumeCacheReaderTests
    {
        private sealed class OneTransform(IVolumeToSectionTransform transform) : IVolumeTransformProvider
        {
            public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber) => transform;
        }

        private static readonly AffineTransform Transform = AffineTransform.RotateScaleShear();

        [TestInitialize]
        public void OpenAVolume() => LocationVolumeGeometryExtensions.Provider = () => new OneTransform(Transform);

        [TestCleanup]
        public void CloseTheVolume() => LocationVolumeGeometryExtensions.Provider = () => Viking.UI.State.volume;

        /// <summary>A circle whose stored cache is stale: it still holds the mosaic circle.</summary>
        private static LocationObj StaleCircle(long id, double x)
        {
            LocationObj circle = LocationVolumeGeometryTests.NewLocation(
                LocationType.CIRCLE, SqlGeometryUtils.Extensions.ToCircle(x, 3000, 0, 80), id: id);
            circle.VolumeShape = circle.MosaicShape;
            return circle;
        }

        [TestMethod]
        public void TheVolumeCentreAndRadiusAreTheOwnersNotTheStoredCache()
        {
            LocationObj circle = StaleCircle(1, 2000);
            LocationVolumeGeometry owner = LocationVolumeGeometry.For(circle, Transform);

            Assert.AreEqual(owner.Center, circle.VolumeCenter());
            Assert.AreEqual(owner.Radius, circle.VolumeRadius(), 1e-9);
            Assert.AreNotEqual(circle.VolumePosition, circle.VolumeCenter(), "the stored cache is stale on purpose");
        }

        [TestMethod]
        public void ALinkPreviewJoinsTheDrawnCentresAtTheSmallerDrawnRadius()
        {
            LocationObj a = StaleCircle(1, 2000);
            LocationObj b = StaleCircle(2, 2600);
            LocationVolumeGeometry ownerA = LocationVolumeGeometry.For(a, Transform);
            LocationVolumeGeometry ownerB = LocationVolumeGeometry.For(b, Transform);

            LineView preview = (LineView)new LinkLocationAction(a, b).Passive!;

            Assert.AreEqual(ownerA.Center.X, preview.Source.X, 1e-3, "LineView keeps single precision");
            Assert.AreEqual(ownerB.Center.Y, preview.Destination.Y, 1e-3);
            Assert.AreEqual(System.Math.Min(ownerA.Radius, ownerB.Radius), preview.LineWidth, 1e-3, "80 mosaic pixels scaled by the warp");
        }
    }
}
