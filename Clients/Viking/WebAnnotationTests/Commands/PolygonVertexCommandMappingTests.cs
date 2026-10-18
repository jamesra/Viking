using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands;
using WebAnnotationTests.Actions;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The add, remove and adjust polygon vertex commands apply an edited polygon through
    /// <see cref="AnnotationCommandBase.TryApplyInSection"/>. A polygon that reaches past the section mapping
    /// must skip the apply step instead of handing it a null polygon (a NullReferenceException in the save callback).
    /// Reuses the bounded transform from <see cref="ChangeTypeActionTests"/>.
    /// </summary>
    [TestClass]
    public class PolygonVertexCommandMappingTests
    {
        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        [TestMethod]
        public void UnmappablePolygonIsNotApplied()
        {
            bool applied = false;

            bool result = AnnotationCommandBase.TryApplyInSection(ChangeTypeActionTests.CreateBoundedTransform(), new Polygon(ChangeTypeActionTests.UnmappableRing()), _ => applied = true, nameof(PolygonVertexCommandMappingTests));

            Assert.IsFalse(result);
            Assert.IsFalse(applied);
        }

        /// <summary>
        /// A vertex dragged past the mapped area while the rest of the polygon stays inside it.
        /// </summary>
        [TestMethod]
        public void PartlyUnmappablePolygonIsNotApplied()
        {
            bool applied = false;
            Polygon volume = new([new(10, 10), new(150, 20), new(5000, 120), new(15, 140), new(10, 10)]);

            Assert.IsFalse(AnnotationCommandBase.TryApplyInSection(ChangeTypeActionTests.CreateBoundedTransform(), volume, _ => applied = true, nameof(PolygonVertexCommandMappingTests)));
            Assert.IsFalse(applied);
        }

        [TestMethod]
        public void MappablePolygonIsAppliedInSectionSpace()
        {
            IVolumeToSectionTransform mapping = ChangeTypeActionTests.CreateBoundedTransform();
            Polygon volume = new([new(10, 10), new(150, 20), new(180, 120), new(15, 140), new(10, 10)]);
            volume.AddInteriorRing(new Polygon(Square(60, 100)));
            Polygon applied = null;

            Assert.IsTrue(AnnotationCommandBase.TryApplyInSection(mapping, volume, mosaic => applied = mosaic, nameof(PolygonVertexCommandMappingTests)));

            Polygon expected = mapping.TryMapShapeVolumeToSection(volume);
            Assert.IsNotNull(applied);
            CollectionAssert.AreEqual(expected.ExteriorRing, applied.ExteriorRing);
            Assert.AreEqual(1, applied.InteriorRings.Count);
            CollectionAssert.AreEqual(expected.InteriorRings[0], applied.InteriorRings[0]);
        }
    }
}
