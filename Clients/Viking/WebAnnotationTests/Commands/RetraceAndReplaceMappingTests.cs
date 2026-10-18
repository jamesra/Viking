using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The pen retrace's finished cut and hole must come back as null, not throw, when part of the shape
    /// lies outside the section mapping, so the command can skip the cut instead of crashing on pen-up.
    /// </summary>
    [TestClass]
    public class RetraceAndReplaceMappingTests
    {
        private const double Tolerance = 1.0;

        /// <summary>
        /// A 3x3 identity grid covering section and volume 0..200; volume points far outside it cannot be mapped.
        /// </summary>
        private static IVolumeToSectionTransform CreateBoundedTransform()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(x * 100, y * 100);
                    points[x + (y * 3)] = new MappingVector2(mapped, mapped);
                }
            }

            return new VolumeToSectionTransform("grid", new GridTransform(points, new Rectangle(0, 200, 0, 200), 3, 3, new TransformBasicInfo()));
        }

        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        [TestMethod]
        public void UnmappableExteriorReturnsNull()
        {
            Polygon volume = new(Square(-1000, -900));

            Assert.IsNull(RetraceAndReplacePathCommand.MapToSectionSimplified(CreateBoundedTransform(), volume, Tolerance));
        }

        [TestMethod]
        public void PartlyUnmappableExteriorReturnsNull()
        {
            Polygon volume = new(Square(50, 500));

            Assert.IsNull(RetraceAndReplacePathCommand.MapToSectionSimplified(CreateBoundedTransform(), volume, Tolerance));
        }

        /// <summary>
        /// A mappable shape gives the same result as mapping then simplifying directly.
        /// </summary>
        [TestMethod]
        public void MappableShapeMatchesMapThenSimplify()
        {
            IVolumeToSectionTransform mapping = CreateBoundedTransform();
            Polygon volume = new([new(10, 10), new(150, 20), new(180, 120), new(90, 190), new(15, 140), new(10, 10)]);
            volume.AddInteriorRing(new Polygon(Square(60, 100)));

            Polygon expected = mapping.TryMapShapeVolumeToSection(volume).Simplify(Tolerance);
            Polygon actual = RetraceAndReplacePathCommand.MapToSectionSimplified(mapping, volume, Tolerance);

            Assert.IsNotNull(actual);
            CollectionAssert.AreEqual(expected.ExteriorRing, actual.ExteriorRing);
            Assert.AreEqual(1, actual.InteriorRings.Count);
            CollectionAssert.AreEqual(expected.InteriorRings[0], actual.InteriorRings[0]);
        }
    }
}
