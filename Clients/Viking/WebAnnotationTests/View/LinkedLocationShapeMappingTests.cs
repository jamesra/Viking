using Geometry;
using Geometry.Transforms;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using Viking.VolumeModel;
using WebAnnotationTests.Actions;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Creating a linked location from a line, closed curve or polygon on an adjacent section warps its shape through
    /// <see cref="WebAnnotation.LocationActions.TryTransformMosaicShapeToSection"/>. A shape that either section cannot map
    /// must give null, so no command is created, instead of a NullReferenceException in the second mapping or in the command.
    /// </summary>
    [TestClass]
    public class LinkedLocationShapeMappingTests
    {
        /// <summary>
        /// A 3x3 grid covering section and volume 0..100, half the extent of <see cref="ChangeTypeActionTests.CreateBoundedTransform"/>.
        /// </summary>
        private static IVolumeToSectionTransform CreateSmallTransform()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(x * 50, y * 50);
                    points[x + (y * 3)] = new MappingVector2(mapped, mapped);
                }
            }

            return new VolumeToSectionTransform("small", new GridTransform(points, new Rectangle(0, 100, 0, 100), 3, 3, new TransformBasicInfo()));
        }

        private static SqlGeometry Wkt(string wkt) => SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), 0);

        /// <summary>
        /// The source section cannot map the shape: the volume shape is null and must not reach the destination mapping.
        /// </summary>
        [TestMethod]
        public void ShapeOutsideSourceMappingGivesNull()
        {
            SqlGeometry mosaic = WebAnnotation.LocationActions.TryTransformMosaicShapeToSection(
                ChangeTypeActionTests.CreateBoundedTransform(), ChangeTypeActionTests.CreateBoundedTransform(),
                Wkt("POLYGON((-1000 -1000, -900 -1000, -900 -900, -1000 -900, -1000 -1000))"), out SqlGeometry volume);

            Assert.IsNull(mosaic);
            Assert.IsNull(volume);
        }

        /// <summary>
        /// The source maps the line, the destination section cannot map its far end.
        /// </summary>
        [TestMethod]
        public void ShapeOutsideDestinationMappingGivesNull()
        {
            SqlGeometry mosaic = WebAnnotation.LocationActions.TryTransformMosaicShapeToSection(
                ChangeTypeActionTests.CreateBoundedTransform(), CreateSmallTransform(),
                Wkt("LINESTRING(10 10, 50 40, 150 150)"), out SqlGeometry volume);

            Assert.IsNull(mosaic);
            Assert.IsNotNull(volume);
        }

        [TestMethod]
        public void ShapeInsideBothMappingsIsWarped()
        {
            SqlGeometry source = Wkt("POLYGON((10 10, 80 10, 80 80, 10 80, 10 10))");

            SqlGeometry mosaic = WebAnnotation.LocationActions.TryTransformMosaicShapeToSection(
                ChangeTypeActionTests.CreateBoundedTransform(), CreateSmallTransform(), source, out SqlGeometry volume);

            Assert.IsNotNull(volume);
            Assert.IsNotNull(mosaic);
            Vector2[] expected = source.ToPoints();
            Vector2[] actual = mosaic.ToPoints();
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].X, actual[i].X, 1e-9);
                Assert.AreEqual(expected[i].Y, actual[i].Y, 1e-9);
            }
        }
    }
}
