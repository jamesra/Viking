using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class QuadSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbVector2(), (q, offset) =>
                    (Quad)q.Translate(offset).Translate(-offset) == q),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsCornersAndAreaIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), q =>
                    q.BoundingBox.Contains(q.BottomLeft) &&
                    q.BoundingBox.Contains(q.TopRight) &&
                    q.Area >= 0),
                nameof(BoundingBoxContainsCornersAndAreaIsNonNegative));

        [TestMethod]
        public void GetRelationMatchesContains() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbVector2(), (q, p) =>
                {
                    ShapeRelation rel = q.GetRelation((IPoint2D)p);
                    return q.Contains(p) == (rel != ShapeRelation.None);
                }),
                nameof(GetRelationMatchesContains));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbQuad(),
                    (a, b) => a.Intersects((IShape2D)b) == b.Intersects((IShape2D)a)),
                nameof(IntersectsIsSymmetric));
    }
}
