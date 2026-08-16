using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class LineSegmentSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbVector2(), (s, offset) =>
                    s.Translate(offset).Translate(-offset) == s),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsEndpointsAndAreaIsZero() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), s =>
                    s.BoundingBox.Contains(s.A) &&
                    s.BoundingBox.Contains(s.B) &&
                    s.Length > 0),
                nameof(BoundingBoxContainsEndpointsAndAreaIsZero));

        [TestMethod]
        public void DirectedEqualityDistinguishesReverse() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), s =>
                {
                    LineSegment reverse = new(s.B, s.A);
                    bool directed = s == reverse == (s.A == s.B);
                    bool undirected = s.EquivalentUndirected(reverse);
                    bool hash = s.Equals(s) && (!s.Equals(reverse) || s.GetHashCode() == reverse.GetHashCode());
                    return undirected && hash && (s.A == s.B || s != reverse);
                }),
                nameof(DirectedEqualityDistinguishesReverse));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbLineSegment(),
                    (a, b) => a.Intersects(b) == b.Intersects(a)),
                nameof(IntersectsIsSymmetric));

        [TestMethod]
        public void GetRelationMatchesContainsForPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbVector2(), (s, p) =>
                {
                    ShapeRelation rel = s.GetRelation((IPoint2D)p);
                    return s.Contains(p) == (rel != ShapeRelation.None);
                }),
                nameof(GetRelationMatchesContainsForPoints));
    }
}
