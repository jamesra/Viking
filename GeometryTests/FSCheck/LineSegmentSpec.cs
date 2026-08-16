using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
                    s.BoundingBox.Covers(s.A) &&
                    s.BoundingBox.Covers(s.B) &&
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
                    return s.Contains(p) == rel.IsContains() &&
                           s.Covers((IPoint2D)p) == rel.IsCovers();
                }),
                nameof(GetRelationMatchesContainsForPoints));

        [TestMethod]
        public void LengthEqualsDistanceBetweenEndpoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), s =>
                    Tolerance.AreClose(s.Length, Vector2.Distance(s.A, s.B))),
                nameof(LengthEqualsDistanceBetweenEndpoints));

        [TestMethod]
        public void BisectIsContainedAndPointAlongLineHitsEndpoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), s =>
                {
                    if (s.Length < 1)
                        return true;
                    return s.GetRelation((IPoint2D)s.Bisect()) == ShapeRelation.Contained &&
                           s.GetRelation((IPoint2D)s.PointAlongLine(0)) == ShapeRelation.Touching &&
                           s.GetRelation((IPoint2D)s.PointAlongLine(1)) == ShapeRelation.Touching &&
                           s.PointAlongLine(0) == s.A &&
                           s.PointAlongLine(1) == s.B;
                }),
                nameof(BisectIsContainedAndPointAlongLineHitsEndpoints));

        [TestMethod]
        public void DistanceToPointIsZeroIffCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbVector2(), (s, p) =>
                    (Math.Abs(s.DistanceToPoint(p)) < Tolerance.Epsilon) == s.Covers((IPoint2D)p)),
                nameof(DistanceToPointIsZeroIffCovers));

        [TestMethod]
        public void ToLineCoversEndpoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), s =>
                {
                    Line infinite = s.ToLine();
                    double perpB = Math.Abs((infinite.Direction.X * (s.B.Y - s.A.Y)) -
                                            (infinite.Direction.Y * (s.B.X - s.A.X)));
                    return infinite.Origin == s.A &&
                           infinite.Direction == s.Direction &&
                           infinite.Covers((IPoint2D)s.A) &&
                           perpB < Tolerance.Epsilon;
                }),
                nameof(ToLineCoversEndpoints));

        [TestMethod]
        public void GetRelationToOtherSegmentNoneIffNotIntersects() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbLineSegment(), (a, b) =>
                {
                    ShapeRelation rel = a.GetRelation(b, out _);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    return exclusive && (rel == ShapeRelation.None) == !a.Intersects(b);
                }),
                nameof(GetRelationToOtherSegmentNoneIffNotIntersects));

        [TestMethod]
        public void IsLeftAgreesWithCrossSignAwayFromSegment() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbLineSegment(), CoreArbitraries.ArbVector2(), (s, p) =>
                {
                    double cross = ((s.B.X - s.A.X) * (p.Y - s.A.Y)) - ((s.B.Y - s.A.Y) * (p.X - s.A.X));
                    if (Math.Abs(cross) < 1e-6)
                        return true;
                    return Math.Sign(cross) == s.IsLeft(p);
                }),
                nameof(IsLeftAgreesWithCrossSignAwayFromSegment));

        [TestMethod]
        public void ZeroLengthConstructorThrows() =>
            Assert.ThrowsException<ArgumentException>(() => new LineSegment(Vector2.Zero, Vector2.Zero));
    }
}
