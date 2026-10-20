using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.Tools;
using WebAnnotationModel.Objects;
using WebAnnotationTests.Actions;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins Jotunn's vertex editing, which reuses Viking's add/remove control point geometry: the shape changes in both
    /// spaces, a shape never drops below its minimum points, and circles have no control points to edit.
    /// </summary>
    [TestClass]
    public class VertexEditTargetTests
    {
        static readonly IVolumeToSectionTransform Mapper = ChangeTypeActionTests.CreateBoundedTransform();

        static LocationObj Square() =>
            ChangeTypeActionTests.NewLocation(LocationType.POLYGON, "POLYGON((10 10, 90 10, 90 90, 10 90, 10 10))");

        static int Vertices(LocationObj location) => location.MosaicShape.ToPolygon().ExteriorRing.Length - 1;

        [TestMethod]
        public void AddingOnAnEdgeInsertsAVertex()
        {
            LocationObj location = Square();
            VertexEditTarget target = VertexEditTarget.TryCreate(Mapper, location);

            Assert.IsTrue(target.TryAdd(new Vector2(50, 10)));

            Assert.AreEqual(5, Vertices(location));
            Assert.AreEqual(5, location.VolumeShape.ToPolygon().ExteriorRing.Length - 1);
        }

        [TestMethod]
        public void RemovingAVertexKeepsTheRestOfTheRing()
        {
            LocationObj location = ChangeTypeActionTests.NewLocation(LocationType.POLYGON, "POLYGON((10 10, 50 5, 90 10, 90 90, 10 90, 10 10))");
            VertexEditTarget target = VertexEditTarget.TryCreate(Mapper, location);

            Assert.IsTrue(target.TryRemove(new Vector2(50, 5)));

            Assert.AreEqual(4, Vertices(location));
        }

        [TestMethod]
        public void ATriangleKeepsItsThreeVertices()
        {
            LocationObj location = ChangeTypeActionTests.NewLocation(LocationType.POLYGON, "POLYGON((10 10, 90 10, 50 90, 10 10))");
            VertexEditTarget target = VertexEditTarget.TryCreate(Mapper, location);

            Assert.IsFalse(target.TryRemove(new Vector2(90, 10)));
            Assert.AreEqual(3, Vertices(location));
        }

        [TestMethod]
        public void AnOpenLineKeepsTwoPoints()
        {
            LocationObj location = ChangeTypeActionTests.NewLocation(LocationType.POLYLINE, "LINESTRING(10 10, 90 90)");
            VertexEditTarget target = VertexEditTarget.TryCreate(Mapper, location);

            Assert.IsFalse(target.TryRemove(new Vector2(10, 10)));
            Assert.IsTrue(target.TryAdd(new Vector2(50, 50)));
            Assert.AreEqual(3, location.MosaicShape.ToPoints().Length);
        }

        [TestMethod]
        public void RestorePutsTheShapeBack()
        {
            LocationObj location = Square();
            string original = location.MosaicShape.ToString();
            VertexEditTarget target = VertexEditTarget.TryCreate(Mapper, location);

            target.TryAdd(new Vector2(50, 10));
            target.Restore();

            Assert.AreEqual(original, location.MosaicShape.ToString());
        }

        [TestMethod]
        public void CirclesHaveNoControlPoints()
        {
            LocationObj circle = ChangeTypeActionTests.NewLocation(LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(40 50, 50 40, 60 50, 50 60, 40 50))");

            Assert.IsNull(VertexEditTarget.TryCreate(Mapper, circle));
        }
    }
}
