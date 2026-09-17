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
                    r.Covers(r.LowerLeft) &&
                    r.Covers(r.UpperRight) &&
                    r.Area >= 0),
                nameof(BoundingBoxIsSelfAndContainsCorners));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbVector2(), (r, p) =>
                {
                    ShapeRelation rel = r.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    bool contains = r.Contains((IPoint2D)p) == rel.IsContains();
                    bool covers = r.Covers((IPoint2D)p) == rel.IsCovers();
                    return exclusive && contains && covers;
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

        [TestMethod]
        public void GetRelationRectangleMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbRectangle(), (a, b) =>
                {
                    ShapeRelation rel = a.GetRelation(b);
                    return a.Contains(b) == rel.IsContains() && a.Covers(b) == rel.IsCovers();
                }),
                nameof(GetRelationRectangleMatchesContainsAndCovers));

        [TestMethod]
        public void PadCoversOriginal() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), r =>
                    Rectangle.Pad(r, 1).Covers(r) && Rectangle.Pad(r, 1).Contains(r.Center)),
                nameof(PadCoversOriginal));

        [TestMethod]
        public void ScaleAboutCenterIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), r =>
                {
                    Rectangle back = Rectangle.Scale(Rectangle.Scale(r, 2), 0.5);
                    return Tolerance.AreClose(back.Width, r.Width) &&
                           Tolerance.AreClose(back.Height, r.Height) &&
                           Tolerance.AreClose(back.Center.X, r.Center.X) &&
                           Tolerance.AreClose(back.Center.Y, r.Center.Y);
                }),
                nameof(ScaleAboutCenterIsInvertible));

        [TestMethod]
        public void EdgesCoverCorners() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), r =>
                    r.LeftEdge.Covers(r.LowerLeft) && r.LeftEdge.Covers(r.UpperLeft) &&
                    r.RightEdge.Covers(r.LowerRight) && r.RightEdge.Covers(r.UpperRight) &&
                    r.BottomEdge.Covers(r.LowerLeft) && r.BottomEdge.Covers(r.LowerRight) &&
                    r.TopEdge.Covers(r.UpperLeft) && r.TopEdge.Covers(r.UpperRight)),
                nameof(EdgesCoverCorners));

        [TestMethod]
        public void CoversWithEpsilonAgreesAwayFromBoundary() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbVector2(), (r, p) =>
                {
                    const double band = 0.1;
                    bool closed = r.Covers(p, Tolerance.Epsilon);
                    bool rel = r.Covers((IPoint2D)p);
                    bool clearlyInside = p.X > r.Left + band && p.X < r.Right - band &&
                                         p.Y > r.Bottom + band && p.Y < r.Top - band;
                    bool clearlyOutside = p.X < r.Left - band || p.X > r.Right + band ||
                                          p.Y < r.Bottom - band || p.Y > r.Top + band;
                    if (clearlyInside)
                        return closed && rel && r.Contains((IPoint2D)p);
                    if (clearlyOutside)
                        return !closed && !rel;
                    return true;
                }),
                nameof(CoversWithEpsilonAgreesAwayFromBoundary));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRectangle(), r =>
                    r.Equals(r) && r == new Rectangle(r.LowerLeft, r.UpperRight) &&
                    r.GetHashCode() == new Rectangle(r.LowerLeft, r.UpperRight).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
