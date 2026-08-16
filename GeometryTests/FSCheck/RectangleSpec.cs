using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class RectangleSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbVector2(), (r, offset) =>
                {
                    Rectangle moved = r.Translate(offset);
                    Rectangle back = moved.Translate(-offset);
                    return back.LowerLeft == r.LowerLeft && back.UpperRight == r.UpperRight;
                }),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxIsSelfAndContainsCorners() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), r =>
                    r.BoundingBox == r &&
                    r.Contains(r.LowerLeft) &&
                    r.Contains(r.UpperRight) &&
                    r.Area >= 0),
                nameof(BoundingBoxIsSelfAndContainsCorners));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbVector2(), (r, p) =>
                {
                    ShapeRelation rel = r.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    bool contains = r.Contains((IPoint2D)p) == (rel != ShapeRelation.None);
                    return exclusive && contains;
                }),
                nameof(GetRelationPartitionsPoints));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbRectangle(),
                    (a, b) => a.Intersects(b) == b.Intersects(a)),
                nameof(IntersectsIsSymmetric));

        [TestMethod]
        public void UnionContainsBoth() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbRectangle(), (a, b) =>
                {
                    Rectangle u = Rectangle.Union(a, b);
                    return u.Contains(a) && u.Contains(b);
                }),
                nameof(UnionContainsBoth));

        [TestMethod]
        public void IntersectionIsInsideBothWhenPresent() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbRectangle(), (a, b) =>
                {
                    Rectangle? overlap = a.Intersection(b);
                    if (!overlap.HasValue)
                        return !a.Intersects(b);
                    return a.Contains(overlap.Value) && b.Contains(overlap.Value);
                }),
                nameof(IntersectionIsInsideBothWhenPresent));
    }
}
