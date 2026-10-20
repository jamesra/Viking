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
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or (ShapeRelation.Touching | ShapeRelation.Exterior) or ShapeRelation.Intersecting;
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

        static readonly LineSegment Host = new(new Vector2(0, 0), new Vector2(10, 0));

        /// <summary>
        /// Asserts <paramref name="other"/> lies on <see cref="Host"/>: Contained, Covers and Contains true, and the
        /// reported overlap is <paramref name="other"/> itself in its own direction (endpoint-scan order, see decision
        /// collinear-overlap-orientation).
        /// </summary>
        static void AssertCoveredByHost(LineSegment other)
        {
            ShapeRelation rel = Host.GetRelation(other, out IShape2D overlap);
            Assert.AreEqual(ShapeRelation.Contained, rel, other.ToString());
            Assert.IsTrue(Host.Covers((IShape2D)other), other.ToString());
            Assert.IsTrue(Host.Contains((IShape2D)other), other.ToString());
            Assert.AreEqual(ShapeType2D.Line, overlap.ShapeType);
            Assert.AreEqual(other, (LineSegment)overlap);
        }

        [TestMethod]
        public void IdenticalSegmentIsCovered()
        {
            AssertCoveredByHost(Host);
            AssertCoveredByHost(new LineSegment(Host.B, Host.A));
        }

        [TestMethod]
        public void InteriorSubSegmentIsCovered() =>
            AssertCoveredByHost(new LineSegment(new Vector2(2, 0), new Vector2(5, 0)));

        [TestMethod]
        public void SubSegmentSharingAnEndpointIsCovered()
        {
            AssertCoveredByHost(new LineSegment(new Vector2(0, 0), new Vector2(5, 0)));
            AssertCoveredByHost(new LineSegment(new Vector2(10, 0), new Vector2(4, 0)));
        }

        [TestMethod]
        public void CollinearOverlapReachingPastThisSegmentIsNotCovered()
        {
            LineSegment longer = new(new Vector2(-1, 0), new Vector2(11, 0));
            LineSegment partial = new(new Vector2(5, 0), new Vector2(15, 0));
            LineSegment sharedEndLonger = new(new Vector2(0, 0), new Vector2(12, 0));
            foreach (LineSegment other in new[] { longer, partial, sharedEndLonger })
            {
                Assert.AreEqual(ShapeRelation.Intersecting, Host.GetRelation(other, out _), other.ToString());
                Assert.IsFalse(Host.Covers((IShape2D)other), other.ToString());
            }

            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior,
                Host.GetRelation(new LineSegment(new Vector2(10, 0), new Vector2(15, 0)), out _));
        }

        [TestMethod]
        public void CollinearPolylineInsideSegmentIsCovered()
        {
            Polyline chain = new([new Vector2(1, 0), new Vector2(4, 0), new Vector2(10, 0)]);
            Assert.AreEqual(ShapeRelation.Contained, Host.GetRelation((IShape2D)chain));
            Assert.IsTrue(Host.Covers((IShape2D)chain));
        }

        /// <summary>
        /// Collinear pairs on a horizontal, vertical, or 45 degree line at volume-scale coordinates (same family as
        /// LineTest.CollinearOverlapMatchesIntervalOverlap). Against the interval reference: b inside a's interval is
        /// Contained and covered; any other overlap of positive length is Intersecting; a single shared point is
        /// Touching | Exterior. Two segments cover each other only when they are the same undirected segment.
        /// </summary>
        [TestMethod]
        public void CollinearRelationMatchesIntervalContainment()
        {
            Gen<int[]> gen = from axis in Gen.Choose(0, 2)
                             from c in Gen.Choose(-500000, 500000)
                             from origin in Gen.Choose(-500000, 500000)
                             from a1 in Gen.Choose(-6, 6)
                             from a2 in Gen.Choose(-6, 6)
                             from b1 in Gen.Choose(-6, 6)
                             from b2 in Gen.Choose(-6, 6)
                             where a1 != a2 && b1 != b2
                             select new[] { axis, c, origin + a1, origin + a2, origin + b1, origin + b2 };

            CoreCheck.Run(
                Prop.ForAll(Arb.From(gen), v =>
                {
                    int axis = v[0];
                    double c = v[1];
                    Vector2 At(double t) => axis switch
                    {
                        0 => new Vector2(t, c),
                        1 => new Vector2(c, t),
                        _ => new Vector2(t, c + t),
                    };

                    LineSegment a = new(At(v[2]), At(v[3]));
                    LineSegment b = new(At(v[4]), At(v[5]));
                    double aLo = Math.Min(v[2], v[3]), aHi = Math.Max(v[2], v[3]);
                    double bLo = Math.Min(v[4], v[5]), bHi = Math.Max(v[4], v[5]);
                    double lo = Math.Max(aLo, bLo), hi = Math.Min(aHi, bHi);
                    bool bInsideA = aLo <= bLo && bHi <= aHi;

                    ShapeRelation expected = lo > hi ? ShapeRelation.None
                        : lo == hi ? ShapeRelation.Touching | ShapeRelation.Exterior
                        : bInsideA ? ShapeRelation.Contained
                        : ShapeRelation.Intersecting;

                    ShapeRelation rel = a.GetRelation(b, out _);
                    bool mutual = a.Covers((IShape2D)b) && b.Covers((IShape2D)a);
                    return (rel == expected &&
                            a.Covers((IShape2D)b) == bInsideA &&
                            a.Contains((IShape2D)b) == bInsideA &&
                            mutual == a.EquivalentUndirected(b))
                        .Label($"{a} vs {b}: expected {expected}, covers {bInsideA}; got {rel}");
                }),
                nameof(CollinearRelationMatchesIntervalContainment));
        }
    }
}
