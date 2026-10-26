using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation;
using SqlGeometryUtils;
using WebAnnotationModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Editing a circle in the viewer never copies a radius between mosaic and volume space: the shape drawn in one space is
    /// mapped into the other with the five-point scale rule (.cursor/rules/circle-mapping-scale.mdc).
    /// </summary>
    [TestClass]
    public class CircleLocationScaleTests
    {
        /// <summary>volume = scale * mosaic + offset, in both directions.</summary>
        private sealed class ScaleTransform(double scale, Vector2 offset) : IVolumeToSectionTransform
        {
            public long ID => 0;
            public Rectangle? SectionBounds => null;
            public Rectangle? VolumeBounds => null;

            private Vector2 Forward(Vector2 p) => new(p.X * scale + offset.X, p.Y * scale + offset.Y);
            private Vector2 Inverse(Vector2 p) => new((p.X - offset.X) / scale, (p.Y - offset.Y) / scale);

            public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = Forward(P); return true; }
            public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = Inverse(P); return true; }
            public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Forward)]; return [.. Points.Select(_ => true)]; }
            public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Inverse)]; return [.. Points.Select(_ => true)]; }
            public Vector2 SectionToVolume(Vector2 P) => Forward(P);
            public Vector2 VolumeToSection(Vector2 P) => Inverse(P);
            public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(Forward)];
            public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(Inverse)];
        }

        private static double Radius(Microsoft.SqlServer.Types.SqlGeometry shape) => shape.ToCircle().Radius;

        [TestMethod]
        public void ACircleDrawnInVolumeSpaceGetsAMosaicRadiusThatFollowsTheScale()
        {
            ScaleTransform section = new(scale: 1.25, offset: new Vector2(500, -200));
            LocationObj location = new(new AnnotationService.Types.Location { ID = 1 });

            bool updated = LocationActions.UpdateCircleLocationNoSaveCallback(location, section, new Vector2(3000, 4000), 100);

            Assert.IsTrue(updated);
            Assert.AreEqual(100, Radius(location.VolumeShape), 0.01);
            Assert.AreEqual(80, Radius(location.MosaicShape), 0.01, "100 volume units at scale 1.25 is 80 mosaic pixels");
            Assert.AreEqual(2000, location.Position.X, 0.01);
            Assert.AreEqual(3360, location.Position.Y, 0.01);
            Assert.AreEqual(80, location.Radius, 0.01, "Location.Radius is the mosaic radius");
        }

        [TestMethod]
        public void ACircleStartedInMosaicSpaceGetsAVolumeRadiusThatFollowsTheScale()
        {
            ScaleTransform section = new(scale: 0.5, offset: new Vector2(10, 20));
            LocationObj location = new(new AnnotationService.Types.Location { ID = 2 });

            bool updated = LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, section, new Vector2(1000, 2000), 60);

            Assert.IsTrue(updated);
            Assert.AreEqual(60, Radius(location.MosaicShape), 0.01);
            Assert.AreEqual(30, Radius(location.VolumeShape), 0.01, "60 mosaic pixels at scale 0.5 is 30 volume units");
        }

        [TestMethod]
        public void TheVolumeCircleOfALocationIsMappedFromItsMosaicCircleNotItsStoredVolumeShape()
        {
            ScaleTransform section = new(scale: 2, offset: new Vector2(0, 0));
            LocationObj location = new(new AnnotationService.Types.Location { ID = 3 });
            LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, section, new Vector2(100, 100), 10);
            // A stored VolumeShape that carries the mosaic radius (as the gRPC read does) must not be trusted.
            location.VolumeShape = new Circle(new Vector2(200, 200), 10).ToSqlGeometry();

            Circle? volume = LocationActions.TryGetVolumeCircle(location, section);

            Assert.IsNotNull(volume);
            Assert.AreEqual(20, volume.Value.Radius, 0.01);
        }
    }
}
