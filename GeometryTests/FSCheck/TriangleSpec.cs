using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class TriangleSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbVector2(), (t, offset) =>
                    (Triangle)t.Translate(offset).Translate(-offset) == t),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsVerticesAndAreaIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                    t.BoundingBox.Contains(t.P1) &&
                    t.BoundingBox.Contains(t.P2) &&
                    t.BoundingBox.Contains(t.P3) &&
                    t.Area >= 0),
                nameof(BoundingBoxContainsVerticesAndAreaIsNonNegative));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbVector2(), (t, p) =>
                {
                    ShapeRelation rel = t.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    return exclusive && (t.Contains(p) == (rel != ShapeRelation.None));
                }),
                nameof(GetRelationPartitionsPoints));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbTriangle(),
                    (a, b) => a.Intersects((IShape2D)b) == b.Intersects((IShape2D)a)),
                nameof(IntersectsIsSymmetric));
    }
}
