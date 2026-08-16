using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                    return line.Contains((IPoint2D)p) == (rel != ShapeRelation.None);
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
    }
}
