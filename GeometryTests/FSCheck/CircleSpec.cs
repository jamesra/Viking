using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class CircleSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, offset) =>
                    c.Translate(offset).Translate(-offset) == c),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsCircle() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                {
                    Rectangle bb = c.BoundingBox;
                    return bb.Covers(c.Center) &&
                           bb.Width + Tolerance.Epsilon >= 2 * c.Radius &&
                           bb.Height + Tolerance.Epsilon >= 2 * c.Radius &&
                           c.Area >= 0;
                }),
                nameof(BoundingBoxContainsCircle));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    ShapeRelation rel = c.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    return exclusive && (c.Contains((IPoint2D)p) == rel.IsContains()) &&
                           (c.Covers((IPoint2D)p) == rel.IsCovers());
                }),
                nameof(GetRelationPartitionsPoints));

        [TestMethod]
        public void IntersectsRectangleIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbRectangle(),
                    (c, r) => c.Intersects((IShape2D)r) == r.Intersects((IShape2D)c)),
                nameof(IntersectsRectangleIsSymmetric));

        [TestMethod]
        public void CircleFromThreePointsReconstructs() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                {
                    Vector2 a = c.Center + new Vector2(c.Radius, 0);
                    Vector2 b = c.Center + new Vector2(0, c.Radius);
                    Vector2 d = c.Center + new Vector2(-c.Radius, 0);
                    Circle rebuilt = Circle.CircleFromThreePoints(a, b, d);
                    return rebuilt.Center == c.Center && Tolerance.AreClose(rebuilt.Radius, c.Radius);
                }),
                nameof(CircleFromThreePointsReconstructs));

        [TestMethod]
        public void CircleFromThreeCollinearPointsThrows()
        {
            Vector2 a = new(0, 0);
            Vector2 b = new(1, 0);
            Vector2 c = new(2, 0);
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(a, b, c));

            Vector2 v1 = new(3, 1);
            Vector2 v2 = new(3, 2);
            Vector2 v3 = new(3, 5);
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(v1, v2, v3));
        }

        /// <summary>
        /// A vertex interpolated on a 9,200 px straight edge near (-55000, 33000): the triple is collinear to within
        /// rounding, so the old guard (|G| at or below double.Epsilon) let through a ~4e20 px circle whose center
        /// ULP is 65,536 px and which missed the second and third points by that much.
        /// </summary>
        private static readonly Vector2[] VertexOnLongStraightEdge =
        [
            new(-59220.669166753367, 33137.059134029332),
            new(-53703.398056636848, 32718.464452773431),
            new(-50034.433528796959, 32440.100564826313),
        ];

        /// <summary>Same failure on a 0.6 px edge near (-27594, -101161): the old circle missed by 0.5 px.</summary>
        private static readonly Vector2[] VertexOnShortStraightEdge =
        [
            new(-27593.476058725952, -101161.2614156498),
            new(-27593.953240249346, -101160.99571248138),
            new(-27594.004010104109, -101160.96744292229),
        ];

        [TestMethod]
        public void CircleFromVertexOnStraightEdgeAtVolumeScaleThrows()
        {
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(VertexOnLongStraightEdge));
            Assert.ThrowsException<ArgumentException>(() => Circle.CircleFromThreePoints(VertexOnShortStraightEdge));
        }

        /// <summary>
        /// Contract for every caller (Delaunay in-circle tests, medial-axis circumcenters): a returned circle passes
        /// through all three input points at the library's resolution, so each one is Touching it. Inputs are
        /// straight-edge triples at volume magnitudes pushed 0 to 1 px off the edge.
        /// </summary>
        [TestMethod]
        public void CircleFromThreePointsThrowsOrPassesThroughEveryPoint() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(StraightEdgeTriple(Gen.Elements(0.0, 1e-9, 1e-6, 1e-3, 0.01, 1.0))), points =>
                {
                    Circle circle;
                    try
                    {
                        circle = Circle.CircleFromThreePoints(points);
                    }
                    catch (ArgumentException)
                    {
                        return true.Label("threw");
                    }

                    return points.All(p => circle.GetRelation(p) == ShapeRelation.Touching)
                        .Label($"Radius {circle.Radius:R} misses a point: {Describe(points)}");
                }),
                nameof(CircleFromThreePointsThrowsOrPassesThroughEveryPoint));

        /// <summary>
        /// Rejecting numerically collinear triples must not reject real slivers: the Delaunay merge builds circles of
        /// ~10^5-10^9 px from vertices 0.01-1 px off a straight run (see DelaunayNearCollinearSpec).
        /// </summary>
        [TestMethod]
        public void CircleFromSliverAtLeastOneHundredthPixelOffEdgeDoesNotThrow() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(StraightEdgeTriple(Gen.Choose(10, 1000).Select(i => i / 1000.0))), points =>
                {
                    Circle circle = Circle.CircleFromThreePoints(points);
                    return points.All(p => circle.GetRelation(p) == ShapeRelation.Touching).Label(Describe(points));
                }),
                nameof(CircleFromSliverAtLeastOneHundredthPixelOffEdgeDoesNotThrow));

        /// <summary>
        /// Endpoints anywhere within ±200,000 px, 1-10,000 px apart; the middle point is interpolated on the segment
        /// between them (a vertex on a straight polygon edge) and pushed off it along the normal by a distance from
        /// <paramref name="offset"/> to either side. The array is rotated so the off-edge point can come first, second or third.
        /// </summary>
        private static Gen<Vector2[]> StraightEdgeTriple(Gen<double> offset) =>
            from x0 in VolumeCoordinate()
            from y0 in VolumeCoordinate()
            from degrees in Gen.Choose(0, 35999).Select(i => i / 100.0)
            from length in Gen.Choose(1, 10000).SelectMany(whole => Gen.Choose(0, 999999).Select(frac => whole + (frac / 1e6)))
            from t in Gen.Choose(1, 999).Select(i => i / 1000.0)
            from off in offset
            from side in Gen.Elements(-1.0, 1.0)
            from rotation in Gen.Choose(0, 2)
            select BuildStraightEdgeTriple(new Vector2(x0, y0), degrees * Math.PI / 180, length, t, side * off, rotation);

        private static Gen<double> VolumeCoordinate() =>
            from whole in Gen.Choose(-200000, 200000)
            from frac in Gen.Choose(0, 999999)
            select whole + (frac / 1e6);

        private static Vector2[] BuildStraightEdgeTriple(Vector2 a, double angle, double length, double t, double off, int rotation)
        {
            Vector2 direction = new(Math.Cos(angle), Math.Sin(angle));
            Vector2 normal = new(-direction.Y, direction.X);
            Vector2 b = a + (direction * length);
            Vector2 onEdge = a + ((b - a) * t);
            Vector2[] triple = [a, onEdge + (normal * off), b];
            return [.. Enumerable.Range(0, 3).Select(i => triple[(i + rotation) % 3])];
        }

        private static string Describe(Vector2[] points) => string.Join("; ", points.Select(p => p.X.ToString("R") + "," + p.Y.ToString("R")));

        [TestMethod]
        public void DistanceIsZeroIffCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    double d = c.Distance(p);
                    bool covers = c.Covers(p);
                    return covers ? d <= Tolerance.Epsilon : d > 0;
                }),
                nameof(DistanceIsZeroIffCovers));

        [TestMethod]
        public void WidthAtHeightIsUnitCircleChord() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(Gen.Choose(-100, 100).Select(i => i / 100.0)), n =>
                {
                    if (Math.Abs(n) > 1)
                        return true;
                    double w = Circle.WidthAtHeight(n);
                    return Tolerance.AreClose((w * w) + (n * n), 1.0);
                }),
                nameof(WidthAtHeightIsUnitCircleChord));

        [TestMethod]
        public void GetRelationPolygonMatchesContainsAndCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbSimplePolygon(), (c, p) =>
                {
                    ShapeRelation rel = c.GetRelation(p);
                    return c.Contains(p) == rel.IsContains() && c.Covers(p) == rel.IsCovers();
                }),
                nameof(GetRelationPolygonMatchesContainsAndCovers));

        [TestMethod]
        public void GetRelationRectangleAndTriangleMatchContains() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbRectangle(), CoreArbitraries.ArbTriangle(),
                    (c, r, t) =>
                    {
                        ShapeRelation rr = c.GetRelation(r);
                        ShapeRelation tr = c.GetRelation(t);
                        return c.Contains(r) == rr.IsContains() && c.Covers((IShape2D)r) == rr.IsCovers() &&
                               c.Contains(t) == tr.IsContains() && c.Covers((IShape2D)t) == tr.IsCovers();
                    }),
                nameof(GetRelationRectangleAndTriangleMatchContains));

        [TestMethod]
        public void StaticContainsAgreesWithInstanceOnInteriorAndFarExterior() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    ShapeRelation instance = c.GetRelation(p);
                    ShapeRelation stat = Circle.Contains(c.Center, c.Radius, p);
                    if (instance == ShapeRelation.Contained)
                        return stat == ShapeRelation.Contained;
                    if (instance == ShapeRelation.None && Vector2.Distance(c.Center, p) > c.Radius + 0.1)
                        return stat == ShapeRelation.None;
                    return true;
                }),
                nameof(StaticContainsAgreesWithInstanceOnInteriorAndFarExterior));

        [TestMethod]
        public void IntersectsCircleAndPolygonAreSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbSimplePolygon(),
                    (c, p) => c.Intersects((IShape2D)p) == p.Intersects((IShape2D)c)),
                nameof(IntersectsCircleAndPolygonAreSymmetric));

        [TestMethod]
        public void IntersectsPointWithZeroRadiusAgreesWithClosedDisk() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), CoreArbitraries.ArbVector2(), (c, p) =>
                {
                    double d = Vector2.Distance(c.Center, p);
                    if (Math.Abs(d - c.Radius) <= 0.1)
                        return true;
                    bool closed = c.Intersects(p, 0);
                    return d < c.Radius ? closed && c.Covers(p) : !closed && !c.Covers(p);
                }),
                nameof(IntersectsPointWithZeroRadiusAgreesWithClosedDisk));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbCircle(), c =>
                    c.Equals(c) && c == new Circle(c.Center, c.Radius) && c.GetHashCode() == new Circle(c.Center, c.Radius).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
