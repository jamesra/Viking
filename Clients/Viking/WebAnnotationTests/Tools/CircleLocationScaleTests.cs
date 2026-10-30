using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Editing a circle in the viewer never copies a radius between mosaic and volume space: the shape drawn in one space is
    /// mapped into the other with the five-point scale rule (.cursor/rules/circle-mapping-scale.mdc).
    /// </summary>
    [TestClass]
    public class CircleLocationScaleTests
    {
        /// <summary>volume = scale * mosaic + offset, in both directions. Also used by <see cref="Commands.SameStructureMergeTests"/>.</summary>
        internal sealed class ScaleTransform(double scale, Vector2 offset) : IVolumeToSectionTransform
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

        /// <summary>
        /// LocationActions reads WebAnnotation.Global, whose static initializer needs an open volume's cache path; without
        /// this the class only passes when an earlier test happened to open one.
        /// </summary>
        [TestInitialize]
        public void OpenAVolumeForGlobal() => SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();

        private static double Radius(IShape2D shape) => ((ICircle2D)shape).Radius;

        [TestMethod]
        public void ACircleDrawnInVolumeSpaceGetsAMosaicRadiusThatFollowsTheScale()
        {
            ScaleTransform section = new(scale: 1.25, offset: new Vector2(500, -200));
            LocationObj location = new(1) { TypeCode = LocationType.CIRCLE };

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
            LocationObj location = new(2) { TypeCode = LocationType.CIRCLE };

            bool updated = LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, section, new Vector2(1000, 2000), 60);

            Assert.IsTrue(updated);
            Assert.AreEqual(60, Radius(location.MosaicShape), 0.01);
            Assert.AreEqual(30, Radius(location.VolumeShape), 0.01, "60 mosaic pixels at scale 0.5 is 30 volume units");
        }

        [TestMethod]
        public void TheVolumeCircleOfALocationIsMappedFromItsMosaicCircleNotItsStoredVolumeShape()
        {
            ScaleTransform section = new(scale: 2, offset: new Vector2(0, 0));
            LocationObj location = new(3) { TypeCode = LocationType.CIRCLE };
            LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, section, new Vector2(100, 100), 10);
            // A stored VolumeShape that carries the mosaic radius (as the gRPC read does) must not be trusted.
            location.VolumeShape = new Circle(new Vector2(200, 200), 10);

            Circle? volume = LocationActions.TryGetVolumeCircle(location, section);

            Assert.IsNotNull(volume);
            Assert.AreEqual(20, volume.Value.Radius, 0.01);
        }

        [TestMethod]
        public void TheRenderedVolumeCircleHasTheSameRadiusAsThePreviewedOne()
        {
            ScaleTransform section = new(scale: 1.25, offset: new Vector2(500, -200));
            LocationObj location = new(4) { TypeCode = LocationType.CIRCLE };
            // Saving a 100-unit volume circle stores the mosaic circle UpdateCircleLocationNoSaveCallback maps it to.
            LocationActions.UpdateCircleLocationNoSaveCallback(location, section, new Vector2(3000, 4000), 100);

            Circle rendered = LocationVolumeGeometry.For(location, section).Circle!.Value;

            Assert.AreEqual(100, rendered.Radius, 0.01, "the saved circle is drawn at the radius that was previewed");
            Assert.AreEqual(3000, rendered.Center.X, 0.01);
            Assert.AreEqual(4000, rendered.Center.Y, 0.01);
        }

        [TestMethod]
        public void TheShapeConstructorKeepsMosaicAndVolumeShapesApart()
        {
            Circle mosaic = new(new Vector2(10, 20), 5);
            Circle volume = new(new Vector2(100, 200), 50);

            LocationObj location = new(null, mosaic, volume, 1, LocationType.CIRCLE);

            Assert.AreEqual(5, Radius(location.MosaicShape), 1e-9);
            Assert.AreEqual(50, Radius(location.VolumeShape), 1e-9);
        }

        private static readonly IVolumeToSectionTransform[] NonRigid = [AffineTransform.RotateScaleShear(), new WarpTransform()];

        private static LocationObj CircleAt(IVolumeToSectionTransform transform, Vector2 mosaicCenter, double mosaicRadius, long id)
        {
            LocationObj location = new(id) { TypeCode = LocationType.CIRCLE };
            Assert.IsTrue(LocationActions.UpdateCircleLocationFromMosaicNoSaveCallback(location, transform, mosaicCenter, mosaicRadius));
            return location;
        }

        /// <summary>
        /// Resize and create: the circle the user sized on screen is drawn back at that size. Under shear the old one-step
        /// inverse five-point mean saved a mosaic circle that the forward rule drew about 10% larger.
        /// </summary>
        [TestMethod]
        public void ACircleSizedInVolumeSpaceIsDrawnAtThatSizeUnderANonRigidTransform()
        {
            foreach (IVolumeToSectionTransform transform in NonRigid)
            {
                LocationObj location = new(5) { TypeCode = LocationType.CIRCLE };

                Assert.IsTrue(LocationActions.UpdateCircleLocationNoSaveCallback(location, transform, new Vector2(3000, 2500), 150));

                Circle drawn = LocationVolumeGeometry.For(location, transform).Circle!.Value;
                Assert.AreEqual(150, drawn.Radius, 0.01, transform.GetType().Name);
                Assert.AreEqual(0, Vector2.Distance(new Vector2(3000, 2500), drawn.Center), 0.01, transform.GetType().Name);
            }
        }

        /// <summary>
        /// Translate: the command starts from the circle the forward owner draws and saves the circle left on screen, so the
        /// mosaic circle moves by the inverse of the volume drag and the drawn circle keeps its size.
        /// </summary>
        [TestMethod]
        public void TranslatingACircleMovesTheMosaicCircleByTheInverseOfTheVolumeDrag()
        {
            foreach (IVolumeToSectionTransform transform in NonRigid)
            {
                LocationObj location = CircleAt(transform, new Vector2(2000, 2000), 90, 6);
                Circle start = LocationActions.TryGetVolumeCircle(location, transform)!.Value;
                Vector2 drag = new(350, -220);

                Assert.IsTrue(LocationActions.UpdateCircleLocationNoSaveCallback(location, transform, start.Center + drag, start.Radius));

                Circle drawn = LocationVolumeGeometry.For(location, transform).Circle!.Value;
                Assert.AreEqual(0, Vector2.Distance(start.Center + drag, drawn.Center), 0.01, transform.GetType().Name);
                Assert.AreEqual(start.Radius, drawn.Radius, 0.01, transform.GetType().Name);
                Assert.AreEqual(0, Vector2.Distance(transform.VolumeToSection(start.Center + drag), location.Position), 0.01, "the mosaic centre is the dragged point mapped back");
            }
        }

        [TestMethod]
        public void ACircleWhoseCentreCannotBeMappedIsRefusedWithAReasonAndKeepsItsShape()
        {
            IVolumeToSectionTransform transform = new ClippedTransform(new ScaleTransform(1, Vector2.Zero), new Rectangle(0, 5000, 0, 5000));
            LocationObj location = CircleAt(transform, new Vector2(2000, 2000), 90, 7);
            IShape2D before = location.MosaicShape;

            Assert.IsFalse(LocationActions.UpdateCircleLocationNoSaveCallback(location, transform, new Vector2(6000, 2000), 90, out string failure));

            StringAssert.Contains(failure, "outside");
            Assert.AreSame(before, location.MosaicShape);
        }

        [TestMethod]
        public void ACircleSetFromMosaicTakesItsVolumeShapeFromTheForwardOwner()
        {
            IVolumeToSectionTransform transform = new WarpTransform();
            LocationObj location = CircleAt(transform, new Vector2(2000, 2000), 90, 8);

            Assert.AreEqual(LocationVolumeGeometry.For(location, transform).Circle!.Value.Radius, Radius(location.VolumeShape), 0.01);
        }
    }
}
