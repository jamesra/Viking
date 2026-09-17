using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class PolygonSpec
    {
        [TestMethod]
        public void ClosedRingInvariant() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    p.ExteriorRing[0] == p.ExteriorRing[p.ExteriorRing.Length - 1] &&
                    p.ExteriorRing.IsValidClosedRing()),
                nameof(ClosedRingInvariant));

        [TestMethod]
        public void ExteriorRingIsCounterClockwiseAndAreaIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    !p.ExteriorRing.AreClockwise() && p.Area >= 0),
                nameof(ExteriorRingIsCounterClockwiseAndAreaIsNonNegative));

        [TestMethod]
        public void ContainsCentroid() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    p.Contains(p.Centroid) && p.Covers((IPoint2D)p.Centroid) && p.GetRelation((IPoint2D)p.Centroid) == ShapeRelation.Contained),
                nameof(ContainsCentroid));

        [TestMethod]
        public void TranslatePreservesArea() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbVector2(), (p, offset) =>
                    Tolerance.AreClose(p.Translate(offset).Area, p.Area)),
                nameof(TranslatePreservesArea));

        [TestMethod]
        public void PointRelationMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbConvexPolygon(), CoreArbitraries.ArbVector2(),
                    CoreShapeProperties.ContainsCoversMatchRelation),
                nameof(PointRelationMatchesContainsAndCovers));

        [TestMethod]
        public void HoledPointRelationMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbHoledPolygon(), CoreArbitraries.ArbVector2(),
                    CoreShapeProperties.ContainsCoversMatchRelation),
                nameof(HoledPointRelationMatchesContainsAndCovers));

        [TestMethod]
        public void GetRelationPolygonMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbSimplePolygon(), (a, b) =>
                {
                    ShapeRelation rel = a.GetRelation(b);
                    return a.Contains(b) == rel.IsContains() && a.Covers(b) == rel.IsCovers();
                }),
                nameof(GetRelationPolygonMatchesContainsAndCovers));

        [TestMethod]
        public void GetRelationCircleMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbCircle(), (p, c) =>
                {
                    ShapeRelation rel = p.GetRelation(c);
                    return p.Contains(c) == rel.IsContains() && p.Covers(c) == rel.IsCovers();
                }),
                nameof(GetRelationCircleMatchesContainsAndCovers));

        [TestMethod]
        public void NestedScaleIsContained() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    if (p.BoundingBox.Width < 1 || p.BoundingBox.Height < 1)
                        return true;
                    Polygon inner = p.Scale(0.3, p.Centroid);
                    return p.GetRelation(inner) == ShapeRelation.Contained && p.Contains(inner);
                }),
                nameof(NestedScaleIsContained));

        [TestMethod]
        public void DisjointTranslateIsNone() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    Vector2 shift = new(p.BoundingBox.Width + p.BoundingBox.Height + 10, 0);
                    Polygon moved = p.Translate(shift);
                    return p.GetRelation(moved) == ShapeRelation.None && !p.Intersects(moved);
                }),
                nameof(DisjointTranslateIsNone));

        [TestMethod]
        public void PerimeterEqualsSumOfExteriorSegments() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbConvexPolygon(), p =>
                    Tolerance.AreClose(p.Perimeter, p.ExteriorSegments.Sum(s => s.Length))),
                nameof(PerimeterEqualsSumOfExteriorSegments));

        [TestMethod]
        public void DistanceIsZeroOnExteriorVertices() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    p.ExteriorRing.Take(p.ExteriorRing.Length - 1).All(v =>
                        Math.Abs(p.Distance(v)) < Tolerance.Epsilon && p.Covers((IPoint2D)v))),
                nameof(DistanceIsZeroOnExteriorVertices));

        [TestMethod]
        public void IsVertexAndTryGetIndexRoundTrip() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbHoledPolygon(), p =>
                {
                    foreach (Vector2 v in p.AllVertices)
                    {
                        if (!p.IsVertex(v) || !p.TryGetIndex(v, out PolygonIndex idx))
                            return false;
                        if (p[idx] != v)
                            return false;
                    }

                    return true;
                }),
                nameof(IsVertexAndTryGetIndexRoundTrip));

        [TestMethod]
        public void ConvexGeneratorIsConvex() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbConvexPolygon(), p => p.IsConvex()),
                nameof(ConvexGeneratorIsConvex));

        [TestMethod]
        public void ConcaveCheckPolygonIsNotConvex() =>
            Assert.IsFalse(Primitives.ConcaveCheckPolygon(10).IsConvex());

        [TestMethod]
        public void RotateAndScaleAreInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    Polygon rotated = p.Rotate(0.25).Rotate(-0.25);
                    Polygon scaled = p.Scale(2, p.Centroid).Scale(0.5, p.Centroid);
                    return RingsClose(p.ExteriorRing, rotated.ExteriorRing) &&
                           RingsClose(p.ExteriorRing, scaled.ExteriorRing) &&
                           Tolerance.AreClose(rotated.Area, p.Area) &&
                           Tolerance.AreClose(scaled.Area, p.Area);
                }),
                nameof(RotateAndScaleAreInvertible));

        [TestMethod]
        public void CloneEqualsAndRoundIsIdempotent() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    Polygon clone = (Polygon)p.Clone();
                    Polygon rounded = p.Round(2);
                    return clone.Equals(p) && rounded.Equals(rounded.Round(2));
                }),
                nameof(CloneEqualsAndRoundIsIdempotent));

        [TestMethod]
        public void InteriorPolygonContainsHoleCentroid() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbHoledPolygon(), p =>
                    p.HasInteriorRings && p.InteriorPolygonContains(p.InteriorPolygons[0].Centroid)),
                nameof(InteriorPolygonContainsHoleCentroid));

        [TestMethod]
        public void AllSegmentsAreExteriorOrInteriorIncludingReverse() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbHoledPolygon(), p =>
                    p.AllSegments.All(s =>
                        p.IsExteriorOrInteriorSegment(s) &&
                        p.IsExteriorOrInteriorSegment(new LineSegment(s.B, s.A)))),
                nameof(AllSegmentsAreExteriorOrInteriorIncludingReverse));

        [TestMethod]
        public void InscribedCircleCenterIsContained() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    Circle inscribed = p.InscribedCircle();
                    return p.Contains((IPoint2D)inscribed.Center) && inscribed.Radius > 0;
                }),
                nameof(InscribedCircleCenterIsContained));

        [TestMethod]
        public void IntersectsCircleIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbCircle(),
                    (p, c) => p.Intersects((IShape2D)c) == c.Intersects((IShape2D)p)),
                nameof(IntersectsCircleIsSymmetric));

        [TestMethod]
        public void WalkPolygonCutHorizontalThroughBoxCenter() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                {
                    Rectangle bb = p.BoundingBox;
                    if (bb.Height < 1 || bb.Width < 1)
                        return true;
                    Vector2 a = new(bb.Left - 1, bb.Center.Y);
                    Vector2 b = new(bb.Right + 1, bb.Center.Y);
                    Polygon cut = Polygon.WalkPolygonCut(p, RotationDirection.Clockwise, [a, b]);
                    return cut.IsValid() && cut.Area > 0 && cut.Area < p.Area;
                }),
                nameof(WalkPolygonCutHorizontalThroughBoxCenter));

        static bool RingsClose(Vector2[] a, Vector2[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (Vector2.Distance(a[i], b[i]) > 0.05)
                    return false;
            }

            return true;
        }
    }

    [TestClass]
    public class PolylineSpecCore
    {
        [TestMethod]
        public void TranslatePreservesLength() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), CoreArbitraries.ArbVector2(), (line, offset) =>
                    Tolerance.AreClose(line.Translate(offset).Length, line.Length)),
                nameof(TranslatePreservesLength));

        [TestMethod]
        public void DisallowsSelfIntersectionWhenConfigured() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                    !line.AllowsSelfIntersection && !line.HasSelfIntersection),
                nameof(DisallowsSelfIntersectionWhenConfigured));

        [TestMethod]
        public void LengthEqualsSumOfSegments() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                    Tolerance.AreClose(line.Length, line.LineSegments.Sum(s => s.Length))),
                nameof(LengthEqualsSumOfSegments));

        [TestMethod]
        public void EndpointsTouchingMidpointContained() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                {
                    Vector2 first = line.Points[0].ToVector2();
                    Vector2 last = line.Points[line.PointCount - 1].ToVector2();
                    Vector2 mid = line.LineSegments[0].Bisect();
                    return line.GetRelation((IPoint2D)first) == ShapeRelation.Touching &&
                           line.GetRelation((IPoint2D)last) == ShapeRelation.Touching &&
                           line.GetRelation((IPoint2D)mid) == ShapeRelation.Contained;
                }),
                nameof(EndpointsTouchingMidpointContained));

        [TestMethod]
        public void CanAddRejectsExistingVertex() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                    !line.CanAdd(line.Points[0])),
                nameof(CanAddRejectsExistingVertex));

        [TestMethod]
        public void RoundAtGeneratorPrecisionPreservesCount() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                    line.Round(2).PointCount == line.PointCount),
                nameof(RoundAtGeneratorPrecisionPreservesCount));

        [TestMethod]
        public void CloneEquals() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line => line.Clone().Equals(line)),
                nameof(CloneEquals));

        [TestMethod]
        public void IntersectsLineSegmentIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), CoreArbitraries.ArbLineSegment(),
                    (line, seg) => line.Intersects((IShape2D)seg) == seg.Intersects((IShape2D)line)),
                nameof(IntersectsLineSegmentIsSymmetric));

        [TestMethod]
        public void AreaThrows() =>
            Assert.ThrowsException<ArgumentException>(() =>
            {
                double unused = new Polyline([new Vector2(0, 0), new Vector2(1, 0)]).Area;
            });
    }
}
