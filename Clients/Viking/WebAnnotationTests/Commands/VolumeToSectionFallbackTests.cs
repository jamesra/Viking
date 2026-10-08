using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Covers <see cref="VolumeToSectionMappingExtensions.WithContinuousFallback"/>: edits that cross the edge of a
    /// registration grid must still map, while the unwrapped mapper keeps reporting "cannot map".
    /// </summary>
    [TestClass]
    public class VolumeToSectionFallbackTests
    {
        private static readonly Vector2 Offset = new(10, 20);

        private static GridTransform CreateTranslatedGrid()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(x * 100, y * 100);
                    points[x + (y * 3)] = new MappingVector2(mapped + Offset, mapped);
                }
            }

            return new GridTransform(points, new Rectangle(0, 200, 0, 200), 3, 3, new TransformBasicInfo());
        }

        [TestMethod]
        public void PlainGridCannotMapPointOutsideItsHull()
        {
            IVolumeToSectionTransform mapper = new VolumeToSectionTransform("grid", CreateTranslatedGrid());

            Assert.IsFalse(mapper.TrySectionToVolume(new Vector2(-500, -500), out _));
            Assert.IsFalse(mapper.TryVolumeToSection(new Vector2(-490, -480), out _));
        }

        [TestMethod]
        public void FallbackMapsPointOutsideHullInBothDirections()
        {
            IVolumeToSectionTransform mapper = new VolumeToSectionTransform("grid", CreateTranslatedGrid()).WithContinuousFallback();

            Assert.IsTrue(mapper.TrySectionToVolume(new Vector2(-500, -500), out Vector2 volume));
            Assert.AreEqual(-490, volume.X, 1.0);
            Assert.AreEqual(-480, volume.Y, 1.0);

            Assert.IsTrue(mapper.TryVolumeToSection(new Vector2(-490, -480), out Vector2 section));
            Assert.AreEqual(-500, section.X, 1.0);
            Assert.AreEqual(-500, section.Y, 1.0);
        }

        [TestMethod]
        public void FallbackAgreesWithGridInsideHull()
        {
            IVolumeToSectionTransform plain = new VolumeToSectionTransform("grid", CreateTranslatedGrid());
            IVolumeToSectionTransform fallback = plain.WithContinuousFallback();

            Assert.IsTrue(plain.TrySectionToVolume(new Vector2(50, 150), out Vector2 expected));
            Assert.IsTrue(fallback.TrySectionToVolume(new Vector2(50, 150), out Vector2 actual));
            Assert.AreEqual(expected.X, actual.X, 1e-6);
            Assert.AreEqual(expected.Y, actual.Y, 1e-6);
        }

        [TestMethod]
        public void IdentityTransformIsReturnedUnchanged()
        {
            IVolumeToSectionTransform identity = new VolumeToSectionTransform("identity", new IdentityTransform());

            Assert.AreSame(identity, identity.WithContinuousFallback());
        }
    }
}
