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
                    return shape.Contains((IPoint2D)p) == rel.IsContains() &&
                           shape.Covers((IPoint2D)p) == rel.IsCovers();
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
        public void CircleGetRelationPolygonWhenPolygonIsInside()
        {
            Polygon box = Primitives.BoxPolygon(1);
            Circle circle = new(Vector2.Zero, 10);
            ShapeRelation rel = circle.GetRelation(box);
            Assert.AreEqual(ShapeRelation.Contained, rel);
        }

        [TestMethod]
        public void PointRelationIsExclusiveNoneContainedOrTouching() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbVector2(), (shape, p) =>
                    shape.ShapeType == ShapeType2D.Collection ||
                    CoreShapeProperties.IsExclusivePointRelation(shape.GetRelation((IPoint2D)p))),
                nameof(PointRelationIsExclusiveNoneContainedOrTouching));

        [TestMethod]
        public void ContainsImpliesCoversForPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbVector2(), (shape, p) =>
                    !shape.Contains((IPoint2D)p) || shape.Covers((IPoint2D)p)),
                nameof(ContainsImpliesCoversForPoints));

        [TestMethod]
        public void CoversImpliesIntersectsForPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbVector2(), (shape, p) =>
                    !shape.Covers((IPoint2D)p) || shape.Intersects((IShape2D)p)),
                nameof(CoversImpliesIntersectsForPoints));

        [TestMethod]
        public void IntersectsAgreesWithShapeExtensionsDispatch() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbMixedShape(),
                    (a, b) => a.Intersects(b) == ShapeExtensions.Intersects(a, b)),
                nameof(IntersectsAgreesWithShapeExtensionsDispatch));

        [TestMethod]
        public void BoundingBoxCoversControlPointsOrSamples() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreShapeProperties.BoundingBoxCoversSamples),
                nameof(BoundingBoxCoversControlPointsOrSamples));

        [TestMethod]
        public void TranslateRoundTripEqualsWhenComparable() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbMixedShape(), CoreArbitraries.ArbVector2(),
                    CoreShapeProperties.TranslateRoundTripEquals),
                nameof(TranslateRoundTripEqualsWhenComparable));

        [TestMethod]
        public void CircleGetRelationLineMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbLineSegment(), (c, line) =>
                {
                    ShapeRelation rel = c.GetRelation(line);
                    return c.Contains(line) == rel.IsContains() && c.Covers(line) == rel.IsCovers();
                }),
                nameof(CircleGetRelationLineMatchesContainsAndCovers));

        [TestMethod]
        public void PolygonGetRelationLineMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbLineSegment(), (p, line) =>
                {
                    ShapeRelation rel = p.GetRelation(line);
                    return p.Contains(line) == rel.IsContains() && p.Covers(line) == rel.IsCovers();
                }),
                nameof(PolygonGetRelationLineMatchesContainsAndCovers));

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
