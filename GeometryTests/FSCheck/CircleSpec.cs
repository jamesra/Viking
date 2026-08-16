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
                    return bb.Contains(c.Center) &&
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
                    return exclusive && (c.Contains((IPoint2D)p) == (rel != ShapeRelation.None));
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
    }
}
