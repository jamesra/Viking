using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class CircleSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, offset) =>
                    c.Translate(offset).Translate(-offset) == c),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsCircle() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                {
                    Rectangle bb = c.BoundingBox;
                    return bb.Covers(c.Center) &&
                           bb.Width + Tolerance.Epsilon >= 2 * c.Radius &&
                           bb.Height + Tolerance.Epsilon >= 2 * c.Radius &&
                           c.Area >= 0;
                }),
                nameof(BoundingBoxContainsCircle));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    ShapeRelation rel = c.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    return exclusive && (c.Contains((IPoint2D)p) == rel.IsContains()) &&
                           (c.Covers((IPoint2D)p) == rel.IsCovers());
                }),
                nameof(GetRelationPartitionsPoints));

        [TestMethod]
        public void IntersectsRectangleIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbRectangle(),
                    (c, r) => c.Intersects((IShape2D)r) == r.Intersects((IShape2D)c)),
                nameof(IntersectsRectangleIsSymmetric));

        [TestMethod]
        public void CircleFromThreePointsReconstructs() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                {
                    Vector2 a = c.Center + new Vector2(c.Radius, 0);
                    Vector2 b = c.Center + new Vector2(0, c.Radius);
                    Vector2 d = c.Center + new Vector2(-c.Radius, 0);
                    Circle rebuilt = Circle.CircleFromThreePoints(a, b, d);
                    return rebuilt.Center == c.Center && Tolerance.AreClose(rebuilt.Radius, c.Radius);
                }),
                nameof(CircleFromThreePointsReconstructs));

        [TestMethod]
        public void CircleFromThreeCollinearPointsThrows()
        {
            Vector2 a = new(0, 0);
            Vector2 b = new(1, 0);
            Vector2 c = new(2, 0);
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(a, b, c));

            Vector2 v1 = new(3, 1);
            Vector2 v2 = new(3, 2);
            Vector2 v3 = new(3, 5);
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(v1, v2, v3));
        }

        [TestMethod]
        public void DistanceIsZeroIffCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    double d = c.Distance(p);
                    bool covers = c.Covers(p);
                    return covers ? d <= Tolerance.Epsilon : d > 0;
                }),
                nameof(DistanceIsZeroIffCovers));

        [TestMethod]
        public void WidthAtHeightIsUnitCircleChord() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(Gen.Choose(-100, 100).Select(i => i / 100.0)), n =>
                {
                    if (Math.Abs(n) > 1)
                        return true;
                    double w = Circle.WidthAtHeight(n);
                    return Tolerance.AreClose((w * w) + (n * n), 1.0);
                }),
                nameof(WidthAtHeightIsUnitCircleChord));

        [TestMethod]
        public void GetRelationPolygonMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbSimplePolygon(), (c, p) =>
                {
                    ShapeRelation rel = c.GetRelation(p);
                    return c.Contains(p) == rel.IsContains() && c.Covers(p) == rel.IsCovers();
                }),
                nameof(GetRelationPolygonMatchesContainsAndCovers));

        [TestMethod]
        public void GetRelationRectangleAndTriangleMatchContains() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbTriangle(),
                    (c, r, t) =>
                    {
                        ShapeRelation rr = c.GetRelation(r);
                        ShapeRelation tr = c.GetRelation(t);
                        return c.Contains(r) == rr.IsContains() && c.Covers((IShape2D)r) == rr.IsCovers() &&
                               c.Contains(t) == tr.IsContains() && c.Covers((IShape2D)t) == tr.IsCovers();
                    }),
                nameof(GetRelationRectangleAndTriangleMatchContains));

        [TestMethod]
        public void StaticContainsAgreesWithInstanceOnInteriorAndFarExterior() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    ShapeRelation instance = c.GetRelation(p);
                    ShapeRelation stat = Circle.Contains(c.Center, c.Radius, p);
                    if (instance == ShapeRelation.Contained)
                        return stat == ShapeRelation.Contained;
                    if (instance == ShapeRelation.None && Vector2.Distance(c.Center, p) > c.Radius + 0.1)
                        return stat == ShapeRelation.None;
                    return true;
                }),
                nameof(StaticContainsAgreesWithInstanceOnInteriorAndFarExterior));

        [TestMethod]
        public void IntersectsCircleAndPolygonAreSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbSimplePolygon(),
                    (c, p) => c.Intersects((IShape2D)p) == p.Intersects((IShape2D)c)),
                nameof(IntersectsCircleAndPolygonAreSymmetric));

        [TestMethod]
        public void IntersectsPointWithZeroRadiusAgreesWithClosedDisk() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    double d = Vector2.Distance(c.Center, p);
                    if (Math.Abs(d - c.Radius) <= 0.1)
                        return true;
                    bool closed = c.Intersects(p, 0);
                    return d < c.Radius ? closed && c.Covers(p) : !closed && !c.Covers(p);
                }),
                nameof(IntersectsPointWithZeroRadiusAgreesWithClosedDisk));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                    c.Equals(c) && c == new Circle(c.Center, c.Radius) && c.GetHashCode() == new Circle(c.Center, c.Radius).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
