using Geometry;
using Geometry.Transforms;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A change-type action whose new volume shape cannot be mapped to the section must leave the location untouched
    /// instead of throwing after its type was already changed.
    /// </summary>
    [TestClass]
    public class ChangeTypeActionTests
    {
        /// <summary>
        /// The action constructors read smoothing settings from WebAnnotation's Global, whose type initializer needs a volume name.
        /// </summary>
        [TestInitialize]
        public void EnsureGlobalCanInitialize() => SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();

        /// <summary>
        /// A 3x3 grid covering section and volume 0..200; volume points far outside it cannot be mapped.
        /// </summary>
        internal static IVolumeToSectionTransform CreateBoundedTransform()
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

        internal static LocationObj NewLocation(LocationType type, string wkt) => TestModel.Location(1, 0, type, wkt);

        internal static Vector2[] UnmappableRing() =>
            [new(-1000, -1000), new(-900, -1000), new(-900, -900), new(-1000, -900), new(-1000, -1000)];

        [TestMethod]
        public void ChangeToPolygonWithUnmappableShapeKeepsLocationTypeAndShape()
        {
            LocationObj location = NewLocation(LocationType.POLYLINE, "LINESTRING(10 10, 50 50, 90 10)");
            string originalShape = location.MosaicShape.ToString();

            ChangeToPolygonAction action = new(location, new Polygon(UnmappableRing()), CreateBoundedTransform());
            action.OnExecute();

            Assert.AreEqual(LocationType.POLYLINE, location.TypeCode);
            Assert.AreEqual(originalShape, location.MosaicShape.ToString());
        }

        [TestMethod]
        public void ChangeToPolylineWithUnmappableShapeKeepsLocationTypeAndShape()
        {
            LocationObj location = NewLocation(LocationType.CURVEPOLYGON, "POLYGON((10 10, 90 10, 90 90, 10 90, 10 10))");
            string originalShape = location.MosaicShape.ToString();

            ChangeToPolylineAction action = new(location, new Polyline(UnmappableRing()), CreateBoundedTransform());
            action.OnExecute();

            Assert.AreEqual(LocationType.CURVEPOLYGON, location.TypeCode);
            Assert.AreEqual(originalShape, location.MosaicShape.ToString());
        }
    }
}
