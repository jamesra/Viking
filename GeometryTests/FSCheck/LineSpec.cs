using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class LineSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), CoreArbitraries.ArbVector2(), (line, offset) =>
                    (Line)line.Translate(offset).Translate(-offset) == line),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void DirectionIsUnitLength() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), line =>
                    Tolerance.AreClose(line.Direction.Magnitude, 1.0)),
                nameof(DirectionIsUnitLength));

        [TestMethod]
        public void GetRelationMatchesContainsForPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), CoreArbitraries.ArbVector2(), (line, p) =>
                {
                    ShapeRelation rel = line.GetRelation((IPoint2D)p);
                    return line.Contains((IPoint2D)p) == rel.IsContains() &&
                           line.Covers((IPoint2D)p) == rel.IsCovers();
                }),
                nameof(GetRelationMatchesContainsForPoints));

        [TestMethod]
        public void IntersectsIsSymmetricForFiniteSegments() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), CoreArbitraries.ArbLineSegment(),
                    (line, seg) => line.Intersects((IShape2D)seg) == seg.Intersects((IShape2D)line)),
                nameof(IntersectsIsSymmetricForFiniteSegments));

        [TestMethod]
        public void InfiniteLineBoundingBoxIsNaNAndCullsAsIntersecting()
        {
            Line line = new(new Vector2(0, 0), Vector2.UnitX);
            Rectangle bbox = line.BoundingBox;
            Assert.IsTrue(double.IsNaN(bbox.Left));
            Assert.IsTrue(double.IsNaN(bbox.Right));
            Assert.IsTrue(double.IsNaN(bbox.Bottom));
            Assert.IsTrue(double.IsNaN(bbox.Top));

            Rectangle finite = new(-10, 10, -10, 10);
            Assert.IsTrue(finite.Intersects(bbox), "AABB culling treats a NaN line box as intersecting every finite box.");
            Assert.IsFalse(bbox.Contains(new Vector2(0, 0)), "Contains on a NaN box is false.");
        }

        [TestMethod]
        public void PerpendicularPassesThroughOriginAndIsOrthogonal() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), line =>
                {
                    Line perp = line.Perpendicular();
                    return perp.Origin == line.Origin &&
                           Tolerance.AreClose(Vector2.Dot(line.Direction, perp.Direction), 0);
                }),
                nameof(PerpendicularPassesThroughOriginAndIsOrthogonal));

        [TestMethod]
        public void IsLeftAgreesWithCrossSignAwayFromLine() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), CoreArbitraries.ArbVector2(), (line, p) =>
                {
                    double cross = (line.Direction.X * (p.Y - line.Origin.Y)) -
                                   (line.Direction.Y * (p.X - line.Origin.X));
                    if (Math.Abs(cross) < 1e-6)
                        return true;
                    return Math.Sign(cross) == line.IsLeft(p);
                }),
                nameof(IsLeftAgreesWithCrossSignAwayFromLine));

        [TestMethod]
        public void ToLineStartsAtOriginWithRequestedLength() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), line =>
                {
                    LineSegment seg = line.ToLine(5);
                    return seg.A == line.Origin && Tolerance.AreClose(seg.Length, 5);
                }),
                nameof(ToLineStartsAtOriginWithRequestedLength));

        [TestMethod]
        public void IntersectsRectangleWhenOriginIsCovered() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), CoreArbitraries.ArbRectangle(), (line, rect) =>
                    !rect.Covers(line.Origin) || line.Intersects((IShape2D)rect)),
                nameof(IntersectsRectangleWhenOriginIsCovered));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLine(), line =>
                    line.Equals(line) && line == new Line(line.Origin, line.Direction) &&
                    line.GetHashCode() == new Line(line.Origin, line.Direction).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
