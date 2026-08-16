using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class Shape2DTest
    {
        [TestMethod]
        public void IntersectsIsSymmetricAcrossMixedShapes() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbMixedShape(),
                    (a, b) => a.Intersects(b) == b.Intersects(a)),
                nameof(IntersectsIsSymmetricAcrossMixedShapes));

        [TestMethod]
        public void ContainsMatchesGetRelationForPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbVector2(), (shape, p) =>
                {
                    ShapeRelation rel = shape.GetRelation((IPoint2D)p);
                    return shape.Contains((IPoint2D)p) == (rel != ShapeRelation.None);
                }),
                nameof(ContainsMatchesGetRelationForPoints));

        [TestMethod]
        public void PolygonGetRelationCircleWhenDiskIsInside()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Circle inside = new(Vector2.Zero, 1);
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(inside));
            Assert.IsTrue(box.Contains(inside));
        }

        [TestMethod]
        public void PolygonGetRelationCircleWhenDiskIsOutside()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Circle outside = new(new Vector2(50, 50), 1);
            Assert.AreEqual(ShapeRelation.None, box.GetRelation(outside));
            Assert.IsFalse(box.Contains(outside));
        }

        [TestMethod]
        public void CircleContainsExtPolygonWhenPolygonIsInside()
        {
            Polygon box = Primitives.BoxPolygon(1);
            Circle circle = new(Vector2.Zero, 10);
            ShapeRelation rel = CircleIntersectionExtensions.ContainsExt(circle, box);
            Assert.AreEqual(ShapeRelation.Contained, rel);
        }

        [TestMethod]
        public void DistanceFromCenterNormalizedAtCentroidIsZero()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Assert.AreEqual(0, box.DistanceFromCenterNormalized(box.Centroid), 1e-6);
        }

        [TestMethod]
        public void DistanceFromCenterNormalizedOnBoundaryIsOne()
        {
            Polygon box = Primitives.BoxPolygon(10);
            double n = box.DistanceFromCenterNormalized(new Vector2(10, 0));
            Assert.AreEqual(1.0, n, 0.05);
        }
    }
}
