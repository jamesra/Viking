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
                    q.BoundingBox.Covers(q.BottomLeft) &&
                    q.BoundingBox.Covers(q.TopRight) &&
                    q.Area >= 0),
                nameof(BoundingBoxContainsCornersAndAreaIsNonNegative));

        [TestMethod]
        public void GetRelationMatchesContains() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbVector2(), (q, p) =>
                {
                    ShapeRelation rel = q.GetRelation((IPoint2D)p);
                    return q.Contains(p) == rel.IsContains() &&
                           q.Covers(p) == rel.IsCovers();
                }),
                nameof(GetRelationMatchesContains));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbQuad(),
                    (a, b) => a.Intersects((IShape2D)b) == b.Intersects((IShape2D)a)),
                nameof(IntersectsIsSymmetric));

        [TestMethod]
        public void CoversAgreesWithComponentTriangles() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbVector2(), (q, p) =>
                {
                    Triangle t0 = new(q.BottomLeft, q.BottomRight, q.TopLeft);
                    Triangle t1 = new(q.BottomRight, q.TopRight, q.TopLeft);
                    return q.Covers(p) == (t0.Covers(p) || t1.Covers(p));
                }),
                nameof(CoversAgreesWithComponentTriangles));

        [TestMethod]
        public void ContainsRectangleWhenAllCornersCovered() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbRectangle(), (q, r) =>
                {
                    if (q.Covers(r.LowerLeft) && q.Covers(r.LowerRight) &&
                        q.Covers(r.UpperLeft) && q.Covers(r.UpperRight))
                        return q.Contains(r);
                    return true;
                }),
                nameof(ContainsRectangleWhenAllCornersCovered));

        [TestMethod]
        public void IntersectsCircleIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), CoreArbitraries.ArbCircle(),
                    (q, c) => q.Intersects((IShape2D)c) == c.Intersects((IShape2D)q)),
                nameof(IntersectsCircleIsSymmetric));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbQuad(), q =>
                    q.Equals(q) && q == new Quad(q.BottomLeft, q.BottomRight, q.TopLeft, q.TopRight) &&
                    q.GetHashCode() == new Quad(q.BottomLeft, q.BottomRight, q.TopLeft, q.TopRight).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
