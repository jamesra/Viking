using FsCheck;
using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests.FSCheck
{
    /// <summary>
    /// Finite, two-decimal generators so epsilon equality and SignificantDigits hashing agree.
    /// </summary>
    public static class CoreArbitraries
    {
        public static Gen<double> Hundredths() =>
            Gen.Choose(-5000, 5000).Select(i => i / 100.0);

        public static Gen<double> PositiveHundredths() =>
            Gen.Choose(1, 2000).Select(i => i / 100.0);

        public static Gen<Vector2> FiniteVector2() =>
            from x in Hundredths()
            from y in Hundredths()
            select new Vector2(x, y);

        public static Gen<Vector3> FiniteVector3() =>
            from x in Hundredths()
            from y in Hundredths()
            from z in Hundredths()
            select new Vector3(x, y, z);

        public static Gen<VectorN> FiniteVectorN() =>
            from n in Gen.Choose(2, 6)
            from coords in Hundredths().ArrayOf(n)
            select new VectorN(coords);

        public static Gen<Tuple<VectorN, VectorN>> FiniteVectorNPair() =>
            from n in Gen.Choose(2, 6)
            from a in Hundredths().ArrayOf(n)
            from b in Hundredths().ArrayOf(n)
            select Tuple.Create(new VectorN(a), new VectorN(b));

        public static Gen<Rectangle> Rectangle() =>
            from a in FiniteVector2()
            from b in FiniteVector2()
            where Math.Abs(a.X - b.X) >= 0.01 && Math.Abs(a.Y - b.Y) >= 0.01
            select new Geometry.Rectangle(a, b);

        public static IEnumerable<Geometry.Rectangle> ShrinkRectangle(Geometry.Rectangle r)
        {
            if (r.Width > 0.04 && r.Height > 0.04)
                yield return new Geometry.Rectangle(r.LowerLeft, r.Width * 0.5, r.Height * 0.5);
        }

        public static Gen<Circle> Circle() =>
            from c in FiniteVector2()
            from r in PositiveHundredths()
            select new Circle(c, r);

        public static IEnumerable<Circle> ShrinkCircle(Circle c)
        {
            if (c.Radius > 0.04)
                yield return new Circle(c.Center, c.Radius * 0.5);
            if (c.Center != Vector2.Zero)
                yield return new Circle(Vector2.Zero, c.Radius);
        }

        public static Gen<LineSegment> LineSegment() =>
            from a in FiniteVector2()
            from b in FiniteVector2()
            where a != b
            select new LineSegment(a, b);

        public static Gen<Line> Line() =>
            from o in FiniteVector2()
            from d in FiniteVector2()
            where d.Magnitude > 0.1
            select new Line(o, d);

        public static Gen<Triangle> Triangle() =>
            from a in FiniteVector2()
            from b in FiniteVector2()
            where a != b
            let edge = b - a
            where edge.Magnitude > 0.1
            from h in Gen.Choose(10, 2000)
            select new Triangle(a, b, a + (edge * 0.5) + (Vector2.Rotate90(edge).Normalize() * (h / 100.0)));

        public static Gen<Quad> AxisAlignedQuad() => Rectangle().Select(r => new Quad(r));

        /// <summary>
        /// Parallelogram quad from two non-parallel edges (positive area, not a bowtie).
        /// </summary>
        public static Gen<Quad> GeneralQuad() =>
            from origin in FiniteVector2()
            from u in FiniteVector2()
            from v in FiniteVector2()
            where u.Magnitude >= 0.1 && v.Magnitude >= 0.1
            where Math.Abs((u.X * v.Y) - (u.Y * v.X)) >= 0.05
            select new Quad(origin, origin + u, origin + v, origin + u + v);

        public static Gen<Quad> Quad() => Gen.OneOf(AxisAlignedQuad(), GeneralQuad());

        public static Gen<Box> Box() =>
            from a in FiniteVector3()
            from b in FiniteVector3()
            where Math.Abs(a.X - b.X) >= 0.01 && Math.Abs(a.Y - b.Y) >= 0.01 && Math.Abs(a.Z - b.Z) >= 0.01
            select new Box(a, b);

        public static Gen<Geometry.Range> Range() =>
            from a in Hundredths()
            from b in Hundredths()
            select a <= b ? new Geometry.Range(a, b) : new Geometry.Range(b, a);

        public static Gen<Polygon> SimplePolygon() =>
            Rectangle().Select(r => new Polygon(
            [
                r.LowerLeft, r.LowerRight, r.UpperRight, r.UpperLeft, r.LowerLeft
            ]));

        /// <summary>
        /// Regular-ish convex n-gon on a circle of hundredths coordinates.
        /// </summary>
        public static Gen<Polygon> ConvexPolygon() =>
            from n in Gen.Choose(3, 8)
            from cx in Hundredths()
            from cy in Hundredths()
            from radius in Gen.Choose(100, 2000)
            select RegularPolygon(new Vector2(cx, cy), radius / 100.0, n);

        public static IEnumerable<Polygon> ShrinkPolygon(Polygon p)
        {
            if (p.HasInteriorRings)
                yield return new Polygon(p.ExteriorRing);

            Rectangle bb = p.BoundingBox;
            if (bb.Width > 0.5 && bb.Height > 0.5)
            {
                yield return new Polygon(
                [
                    bb.LowerLeft, bb.LowerRight, bb.UpperRight, bb.UpperLeft, bb.LowerLeft
                ]);
            }
        }

        public static Gen<Polygon> HoledPolygon() =>
            from outer in ConvexPolygon()
            where outer.BoundingBox.Width >= 2 && outer.BoundingBox.Height >= 2
            let inner = ScaledInteriorHole(outer, 0.3)
            where inner is not null
            let holed = WithHole(outer, inner)
            where holed.HasInteriorRings
            select holed;

        public static Gen<Polyline> OpenPolyline() =>
            from n in Gen.Choose(2, 8)
            from xs in Gen.Choose(-5000, 5000).ArrayOf(n)
            from ys in Gen.Choose(-5000, 5000).ArrayOf(n)
            where xs.Distinct().Count() == n
            select new Polyline(
                xs.OrderBy(x => x).Zip(ys, (x, y) => new Vector2(x / 100.0, y / 100.0)),
                AllowSelfIntersection: false);

        public static IEnumerable<Polyline> ShrinkPolyline(Polyline line)
        {
            if (line.PointCount <= 2)
                yield break;

            Vector2[] pts = [.. line.Points.Select(p => p.ToVector2())];
            yield return new Polyline(pts.Take(pts.Length - 1), line.AllowsSelfIntersection);
            if (pts.Length > 3)
                yield return new Polyline(new[] { pts[0] }.Concat(pts.Skip(2)), line.AllowsSelfIntersection);
        }

        /// <summary>
        /// Finite-area shapes safe to put in a collection (no NaN bbox, no throwing Area).
        /// </summary>
        public static Gen<IShape2D> BoundedAreaShape() =>
            Gen.OneOf(
                Rectangle().Select(r => (IShape2D)r),
                Circle().Select(c => (IShape2D)c),
                Triangle().Select(t => (IShape2D)t),
                SimplePolygon().Select(p => (IShape2D)p));

        public static Gen<Shape2DCollection> ShapeCollection() =>
            from n in Gen.Choose(1, 3)
            from shapes in BoundedAreaShape().ArrayOf(n)
            select new Shape2DCollection(shapes);

        public static Gen<IShape2D> MixedShape() =>
            Gen.OneOf(
                FiniteVector2().Select(p => (IShape2D)p),
                Rectangle().Select(r => (IShape2D)r),
                Circle().Select(c => (IShape2D)c),
                Triangle().Select(t => (IShape2D)t),
                LineSegment().Select(s => (IShape2D)s),
                Line().Select(l => (IShape2D)l),
                Quad().Select(q => (IShape2D)q),
                SimplePolygon().Select(p => (IShape2D)p),
                OpenPolyline().Select(p => (IShape2D)p),
                ShapeCollection().Select(c => (IShape2D)c));

        public static Gen<PolygonIndex> PolygonIndex() =>
            from ring in Gen.Choose(3, 12)
            from iVert in Gen.Choose(0, ring - 1)
            from shape in Gen.Choose(0, 3)
            select new PolygonIndex(shape, iVert, ring);

        static Polygon RegularPolygon(Vector2 center, double radius, int n)
        {
            Vector2[] ring = new Vector2[n + 1];
            for (int i = 0; i < n; i++)
            {
                double angle = (2.0 * Math.PI * i) / n;
                ring[i] = new Vector2(
                    Math.Round(center.X + (radius * Math.Cos(angle)), 2),
                    Math.Round(center.Y + (radius * Math.Sin(angle)), 2));
            }

            ring[n] = ring[0];
            if (ring.Distinct().Count() != n)
                return SimpleBoxAt(center, radius);

            try
            {
                Polygon poly = new(ring);
                return poly.IsValid() ? poly : SimpleBoxAt(center, radius);
            }
            catch (ArgumentException)
            {
                return SimpleBoxAt(center, radius);
            }
        }

        static Polygon SimpleBoxAt(Vector2 center, double radius)
        {
            double r = Math.Max(radius, 0.5);
            return new Polygon(
            [
                new Vector2(center.X - r, center.Y - r),
                new Vector2(center.X + r, center.Y - r),
                new Vector2(center.X + r, center.Y + r),
                new Vector2(center.X - r, center.Y + r),
                new Vector2(center.X - r, center.Y - r)
            ]);
        }

        static Polygon ScaledInteriorHole(Polygon outer, double fraction)
        {
            Vector2 origin = outer.Centroid;
            try
            {
                Polygon scaled = outer.Scale(fraction, origin);
                if (!scaled.IsValid() || scaled.Area <= Tolerance.Epsilon)
                    return null;
                if (!outer.Contains((IPoint2D)scaled.Centroid))
                    return null;
                return scaled;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static Polygon WithHole(Polygon outer, Polygon inner)
        {
            Polygon copy = (Polygon)outer.Clone();
            try
            {
                copy.AddInteriorRing(inner);
                return copy.IsValid() ? copy : outer;
            }
            catch (ArgumentException)
            {
                return outer;
            }
        }

        public static Arbitrary<Vector2> ArbVector2() => Arb.From(FiniteVector2());
        public static Arbitrary<Vector3> ArbVector3() => Arb.From(FiniteVector3());
        public static Arbitrary<VectorN> ArbVectorN() => Arb.From(FiniteVectorN());
        public static Arbitrary<Geometry.Rectangle> ArbRectangle() => Arb.From(Rectangle(), ShrinkRectangle);
        public static Arbitrary<Circle> ArbCircle() => Arb.From(Circle(), ShrinkCircle);
        public static Arbitrary<LineSegment> ArbLineSegment() => Arb.From(LineSegment());
        public static Arbitrary<Line> ArbLine() => Arb.From(Line());
        public static Arbitrary<Triangle> ArbTriangle() => Arb.From(Triangle());
        public static Arbitrary<Quad> ArbQuad() => Arb.From(Quad());
        public static Arbitrary<Box> ArbBox() => Arb.From(Box());
        public static Arbitrary<Geometry.Range> ArbRange() => Arb.From(Range());
        public static Arbitrary<Polygon> ArbSimplePolygon() => Arb.From(SimplePolygon(), ShrinkPolygon);
        public static Arbitrary<Polygon> ArbConvexPolygon() => Arb.From(ConvexPolygon(), ShrinkPolygon);
        public static Arbitrary<Polygon> ArbHoledPolygon() => Arb.From(HoledPolygon(), ShrinkPolygon);
        public static Arbitrary<Polyline> ArbOpenPolyline() => Arb.From(OpenPolyline(), ShrinkPolyline);
        public static Arbitrary<Shape2DCollection> ArbShapeCollection() => Arb.From(ShapeCollection());
        public static Arbitrary<PolygonIndex> ArbPolygonIndex() => Arb.From(PolygonIndex());
        public static Arbitrary<IShape2D> ArbMixedShape() => Arb.From(MixedShape());
        public static Arbitrary<Tuple<VectorN, VectorN>> ArbVectorNPair() => Arb.From(FiniteVectorNPair());
        public static Arbitrary<double> ArbScalar() => Arb.From(Gen.Choose(-50, 50).Select(i => i / 10.0));
    }
}
