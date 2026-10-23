using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// A circle carried between mosaic and volume space through a transform keeps its mapped centre and takes the scale of the
    /// transform (VikingAU's five-point rule, see .cursor/rules/circle-mapping-scale.mdc). The radius is never copied across spaces.
    /// </summary>
    [TestClass]
    public class TryMapCircleScaleTests
    {
        /// <summary>
        /// volume = scale * R(angle) * mosaic + offset, in both directions. <c>canMap</c> says which points can be mapped (default: all).
        /// </summary>
        private sealed class SimilarityTransform(double scale, double angleRadians, Vector2 offset, Func<Vector2, bool> canMap = null) : IVolumeToSectionTransform
        {
            private readonly Func<Vector2, bool> _canMap = canMap ?? (_ => true);

            public long ID => 0;
            public Rectangle? SectionBounds => null;
            public Rectangle? VolumeBounds => null;

            private Vector2 Forward(Vector2 p) => new(
                scale * (p.X * Math.Cos(angleRadians) - p.Y * Math.Sin(angleRadians)) + offset.X,
                scale * (p.X * Math.Sin(angleRadians) + p.Y * Math.Cos(angleRadians)) + offset.Y);

            private Vector2 Inverse(Vector2 p)
            {
                double x = (p.X - offset.X) / scale;
                double y = (p.Y - offset.Y) / scale;
                return new Vector2(x * Math.Cos(angleRadians) + y * Math.Sin(angleRadians), -x * Math.Sin(angleRadians) + y * Math.Cos(angleRadians));
            }

            public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = Forward(P); return _canMap(P); }
            public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = Inverse(P); return _canMap(P); }
            public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Forward)]; return [.. Points.Select(_canMap)]; }
            public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Inverse)]; return [.. Points.Select(_canMap)]; }
            public Vector2 SectionToVolume(Vector2 P) => Forward(P);
            public Vector2 VolumeToSection(Vector2 P) => Inverse(P);
            public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(Forward)];
            public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(Inverse)];
        }

        [TestMethod]
        public void SectionToVolume_ScaledAndRotatedTransform_ScalesTheRadiusAndMapsTheCentre()
        {
            SimilarityTransform transform = new(scale: 1.25, angleRadians: 0.6, offset: new Vector2(1000, -500));
            Circle mosaic = new(new Vector2(2000, 3000), 40);

            Circle? volume = transform.TryMapShapeSectionToVolume(mosaic);

            Assert.IsNotNull(volume);
            Vector2 expectedCentre = transform.SectionToVolume(mosaic.Center);
            Assert.AreEqual(expectedCentre.X, volume.Value.Center.X, 1e-9);
            Assert.AreEqual(expectedCentre.Y, volume.Value.Center.Y, 1e-9);
            Assert.AreEqual(50, volume.Value.Radius, 1e-9, "the radius must follow the scale (40 * 1.25), not be copied");
        }

        [TestMethod]
        public void VolumeToSection_ReturnsTheInverseScale()
        {
            SimilarityTransform transform = new(scale: 0.8, angleRadians: -1.1, offset: new Vector2(-30, 70));
            Circle volume = new(new Vector2(500, 600), 24);

            Circle? mosaic = transform.TryMapShapeVolumeToSection(volume);

            Assert.IsNotNull(mosaic);
            Assert.AreEqual(30, mosaic.Value.Radius, 1e-9, "24 / 0.8");
        }

        [TestMethod]
        public void RoundTripRecoversTheCircle()
        {
            SimilarityTransform transform = new(scale: 1.7, angleRadians: 2.4, offset: new Vector2(12, 34));
            Circle start = new(new Vector2(250, -80), 15);

            Circle? volume = transform.TryMapShapeSectionToVolume(start);
            Circle? back = transform.TryMapShapeVolumeToSection(volume.Value);

            Assert.AreEqual(start.Center.X, back.Value.Center.X, 1e-6);
            Assert.AreEqual(start.Center.Y, back.Value.Center.Y, 1e-6);
            Assert.AreEqual(start.Radius, back.Value.Radius, 1e-6);
        }

        [TestMethod]
        public void RimPointsThatCannotBeMappedAreLeftOutOfTheScale()
        {
            // The east rim point is outside the mapped area; the centre and the other three rim points map.
            Vector2 centre = new(100, 100);
            SimilarityTransform transform = new(scale: 2, angleRadians: 0, offset: new Vector2(0, 0), canMap: p => p.X < 109);

            Circle? volume = transform.TryMapShapeSectionToVolume(new Circle(centre, 10));

            Assert.IsNotNull(volume);
            Assert.AreEqual(20, volume.Value.Radius, 1e-9);
        }

        [TestMethod]
        public void ACentreThatCannotBeMappedGivesNull()
        {
            SimilarityTransform transform = new(scale: 2, angleRadians: 0, offset: new Vector2(0, 0), canMap: p => p.X != 100 || p.Y != 100);
            Assert.IsNull(transform.TryMapShapeSectionToVolume(new Circle(new Vector2(100, 100), 10)));
        }

        [TestMethod]
        public void WhenNoRimPointCanBeMappedTheCircleIsNullRatherThanKeepingItsRadius()
        {
            SimilarityTransform transform = new(scale: 2, angleRadians: 0, offset: new Vector2(0, 0), canMap: p => p.X == 100 && p.Y == 100);
            Assert.IsNull(transform.TryMapShapeSectionToVolume(new Circle(new Vector2(100, 100), 10)));
        }
    }
}
