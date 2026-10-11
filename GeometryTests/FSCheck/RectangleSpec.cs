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
                    return a.Contains(b) == rel.IsContains() && a.Covers(b) == (rel == ShapeRelation.Contained);
                }),
                nameof(GetRelationRectangleMatchesContainsAndCovers));

        /// <summary>
        /// Pairs of rectangles whose edges sit on one shared lattice, so equal Left/Right/Bottom/Top
        /// coordinates (shared edges, nested spans with one common end) are common rather than rare.
        /// The origin spans real volume magnitudes; lattice points are exact in double at these sizes.
        /// </summary>
        private static Gen<(Rectangle A, Rectangle B)> LatticeRectanglePairs() =>
            from originX in Gen.Choose(-500000, 500000)
            from originY in Gen.Choose(-500000, 500000)
            from step in Gen.Elements(0.5, 1.0, 256.0)
            from aXs in Gen.Choose(-4, 4).Two()
            from aYs in Gen.Choose(-4, 4).Two()
            from bXs in Gen.Choose(-4, 4).Two()
            from bYs in Gen.Choose(-4, 4).Two()
            select (Lattice(originX, originY, step, aXs, aYs), Lattice(originX, originY, step, bXs, bYs));

        private static Arbitrary<(Rectangle A, Rectangle B)> ArbLatticeRectanglePair() =>
            Arb.From(LatticeRectanglePairs().Where(p => p.A.Area > 0 && p.B.Area > 0));

        /// <summary>Lattice pairs where at least one rectangle has zero width or zero height (a segment or a point).</summary>
        private static Arbitrary<(Rectangle A, Rectangle B)> ArbDegenerateLatticeRectanglePair() =>
            Arb.From(LatticeRectanglePairs().Where(p => p.A.Area == 0 || p.B.Area == 0));

        private static Rectangle Lattice(int originX, int originY, double step, Tuple<int, int> xs, Tuple<int, int> ys) =>
            new(originX + Math.Min(xs.Item1, xs.Item2) * step, originX + Math.Max(xs.Item1, xs.Item2) * step,
                originY + Math.Min(ys.Item1, ys.Item2) * step, originY + Math.Max(ys.Item1, ys.Item2) * step);

        /// <summary>
        /// Closed-set reference for two non-degenerate rectangles: disjoint is None, <paramref name="b"/> in
        /// closed <paramref name="a"/> is Contained, positive-area overlap is Intersecting, and contact along
        /// an edge or at a corner only is Touching.
        /// </summary>
        private static ShapeRelation ReferenceRelation(Rectangle a, Rectangle b)
        {
            double overlapLeft = Math.Max(a.Left, b.Left);
            double overlapRight = Math.Min(a.Right, b.Right);
            double overlapBottom = Math.Max(a.Bottom, b.Bottom);
            double overlapTop = Math.Min(a.Top, b.Top);

            if (overlapLeft > overlapRight || overlapBottom > overlapTop)
                return ShapeRelation.None;
            if (b.Left >= a.Left && b.Right <= a.Right && b.Bottom >= a.Bottom && b.Top <= a.Top)
                return ShapeRelation.Contained;
            if (overlapLeft < overlapRight && overlapBottom < overlapTop)
                return ShapeRelation.Intersecting;
            return ShapeRelation.Touching;
        }

        [TestMethod]
        public void GetRelationRectangleMatchesClosedSetReferenceOnSharedLattice() =>
            CoreCheck.Run(
                Prop.ForAll(ArbLatticeRectanglePair(), pair =>
                {
                    (Rectangle a, Rectangle b) = pair;
                    ShapeRelation expected = ReferenceRelation(a, b);
                    return (a.GetRelation(b) == expected)
                        .Label($"{a} vs {b}: GetRelation {a.GetRelation(b)}, expected {expected}")
                        .And((a.Covers(b) == (expected == ShapeRelation.Contained))
                        .Label($"{a} vs {b}: Covers {a.Covers(b)}"));
                }),
                nameof(GetRelationRectangleMatchesClosedSetReferenceOnSharedLattice));

        /// <summary>
        /// <see cref="Rectangle.GetRelation(in Rectangle)"/> as it was before shared-endpoint spans were classified.
        /// Valid as a reference only when a rectangle is degenerate: that path never reaches its Debug.Assert,
        /// and degenerate results must not change.
        /// </summary>
        private static ShapeRelation LegacyRelation(Rectangle a, Rectangle rect)
        {
            if (rect.Right < a.Left || rect.Top < a.Bottom || rect.Left > a.Right || rect.Bottom > a.Top)
                return ShapeRelation.None;

            if (rect.Right <= a.Right && rect.Top <= a.Top && rect.Left >= a.Left && rect.Bottom >= a.Bottom)
                return ShapeRelation.Contained;

            bool LRIntersect = (a.Left < rect.Left && a.Right > rect.Left) ||
                               (a.Right > rect.Left && a.Right < rect.Right) ||
                               (a.Left > rect.Left && a.Right < rect.Right) ||
                               (a.Left > rect.Left && a.Left < rect.Right);

            bool UDIntersect = (a.Bottom < rect.Bottom && a.Top > rect.Bottom) ||
                               (a.Top > rect.Bottom && a.Top < rect.Top) ||
                               (a.Bottom > rect.Bottom && a.Top < rect.Top) ||
                               (a.Bottom > rect.Bottom && a.Bottom < rect.Top);

            if (LRIntersect && UDIntersect)
                return ShapeRelation.Intersecting;

            bool LRTouch = a.Left == rect.Right || a.Right == rect.Left;
            bool UDTouch = a.Bottom == rect.Top || a.Top == rect.Bottom;

            if ((LRTouch && UDIntersect) || (UDTouch && LRIntersect) || (LRTouch && UDTouch))
                return ShapeRelation.Touching;

            if (LRIntersect || UDIntersect)
                return ShapeRelation.Intersecting;

            if (LRTouch || UDTouch)
                return ShapeRelation.Touching;

            return ShapeRelation.None;
        }

        [TestMethod]
        public void GetRelationRectangleUnchangedForDegenerateRectangles() =>
            CoreCheck.Run(
                Prop.ForAll(ArbDegenerateLatticeRectanglePair(), pair =>
                {
                    (Rectangle a, Rectangle b) = pair;
                    ShapeRelation expected = LegacyRelation(a, b);
                    return (a.GetRelation(b) == expected)
                        .Label($"{a} vs {b}: GetRelation {a.GetRelation(b)}, legacy {expected}");
                }),
                nameof(GetRelationRectangleUnchangedForDegenerateRectangles));

        /// <summary>Two zero-width rectangles on the same x overlapping in y: rare in the lattice generator.</summary>
        [TestMethod]
        public void GetRelationCollinearZeroWidthRectanglesAreTouching()
        {
            Rectangle a = new(5, 5, 0, 10);
            Rectangle b = new(5, 5, 2, 20);
            Assert.AreEqual(LegacyRelation(a, b), a.GetRelation(b));
            Assert.AreEqual(ShapeRelation.Touching, a.GetRelation(b));
        }

        /// <summary>QuadTreeTestOne's request rectangle against the lower-right quadrant: they share only the edge y = 0.</summary>
        [TestMethod]
        public void GetRelationSharedEdgeWithCommonLeftIsTouching()
        {
            Rectangle request = new(0, 15, 0, 15);
            Rectangle quadrant = new(0, 10, -10, 0);
            Assert.AreEqual(ShapeRelation.Touching, request.GetRelation(quadrant));
            Assert.IsFalse(request.Covers(quadrant));
        }

        /// <summary>Overlapping in area with a common Left coordinate; the X spans are nested, not staggered.</summary>
        [TestMethod]
        public void GetRelationOverlapWithCommonLeftIsIntersecting()
        {
            Rectangle a = new(0, 15, 0, 15);
            Rectangle b = new(0, 10, 5, 20);
            Assert.AreEqual(ShapeRelation.Intersecting, a.GetRelation(b));
            Assert.AreEqual(ShapeRelation.Intersecting, b.GetRelation(a));
        }

        /// <summary>A rectangle touching from outside along part of an edge is not covered.</summary>
        [TestMethod]
        public void CoversIsFalseForExternallyTouchingRectangle()
        {
            Rectangle request = new(0, 15, 1, 15);
            Rectangle neighbor = new(-10, 0, 0, 10);
            Assert.AreEqual(ShapeRelation.Touching, request.GetRelation(neighbor));
            Assert.IsFalse(request.Covers(neighbor));
        }

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
