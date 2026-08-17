using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Disk in the plane. <see cref="RadiusSquared"/> is derived in the constructor; do not treat it as independently settable.
    /// </summary>
    [Serializable]
    public readonly struct Circle : IShape2D, ICircle2D, IHasControlPoints, IEquatable<ICircle2D>
    {
        public readonly Vector2 Center;
        public readonly double Radius;
        public readonly double RadiusSquared;

        public Circle(double X, double Y, double radius) : this(new Vector2(X, Y), radius)
        { }

        public Circle(Vector2 center, double radius)
        {
            this.Center = center;
            this.Radius = radius;

            if (double.IsInfinity(radius) || double.IsNaN(radius))
                throw new ArgumentException("Radius cannot be infinite or NaN");

            this.RadiusSquared = radius * radius;
        }

        public Circle(IPoint2D center, double radius) : this(new Vector2(center.X, center.Y), radius)
        {
        }

        public override string ToString() => Center.ToString() + " Radius: " + Radius.ToString("F2");

        public static Circle CircleFromThreePoints(Vector2[] points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));

            }

            Debug.Assert(points.Length == 3);
            if (points.Length != 3)
                throw new ArgumentException("Circle: Expected an array with three elements");

            Vector2 A = points[0];
            Vector2 B = points[1];
            Vector2 C = points[2];

            return CircleFromThreePoints(A, B, C);
        }

        /// <summary>
        /// Circumcircle of three non-collinear points (intersection of perpendicular bisectors).
        /// Collinear triples throw.
        /// </summary>
        public static Circle CircleFromThreePoints(Vector2 One, Vector2 Two, Vector2 Three)
        {
            if (One.X == Two.X && Two.X == Three.X)
            {
                throw new ArgumentException("Circle from three points with three points on a vertical line");
            }

            double A = Two.X - One.X;
            double B = Two.Y - One.Y;
            double C = Three.X - One.X;
            double D = Three.Y - One.Y;
            double E = A * (One.X + Two.X) + B * (One.Y + Two.Y);
            double F = C * (One.X + Three.X) + D * (One.Y + Three.Y);
            double G = 2 * (A * (Three.Y - Two.Y) - B * (Three.X - Two.X));

            //Check for colinear
            //         Debug.Assert(false == (G <= double.Epsilon && G >= -double.Epsilon));
            if (G <= double.Epsilon && G >= -double.Epsilon)
            {
                throw new ArgumentException("Circle from three points with three points on a line");
            }

            Vector2 Center = new(
                x: (D * E - B * F) / G,
                y: (A * F - C * E) / G
            );

            return new Circle(Center, Vector2.Distance(in Center, in One));
        }

        /*
        /// <summary>
        /// This exists because the Delaunay algorithm creates a ton of circles.  Allocating memory for them
        /// means taking the allocation lock twice instead of one (Circle is created by triangle object)
        /// </summary>
        /// <param name="One"></param>
        /// <param name="Two"></param>
        /// <param name="Three"></param>
        /// <returns></returns>
        static public void CircleFromThreePoints(Vector2[] points, ref Circle circle)
        {
            if (points is null)
            {
                throw new ArgumentNullException("points");

            }

            Debug.Assert(points.Length == 3);
            if (points.Length != 3)
                throw new ArgumentException("Circle: Expected an array with three elements");

            Circle.CircleFromThreePoints(points[0], points[1], points[2], ref circle);
        }
        */

        /*
        /// <summary>
        /// This exists because the Delaunay algorithm creates a ton of circles.  Allocating memory for them
        /// means taking the allocation lock twice instead of one (Circle is created by triangle object)
        /// </summary>
        /// <param name="One"></param>
        /// <param name="Two"></param>
        /// <param name="Three"></param>
        /// <returns></returns>
        static public void CircleFromThreePoints(Vector2 One, Vector2 Two, Vector2 Three, ref Circle circle)
        {
            if (One.X == Two.X && Two.X == Three.X)
            {
                throw new ArgumentException("Circle from three points with three points on a vertical line");
            }

            double A = Two.X - One.X;
            double B = Two.Y - One.Y;
            double C = Three.X - One.X;
            double D = Three.Y - One.Y;
            double E = A * (One.X + Two.X) + B * (One.Y + Two.Y);
            double F = C * (One.X + Three.X) + D * (One.Y + Three.Y);
            double G = 2 * (A * (Three.Y - Two.Y) - B * (Three.X - Two.X));

            //Check for colinear
            //         Debug.Assert(false == (G <= double.Epsilon && G >= -double.Epsilon));
            if (G <= double.Epsilon && G >= -double.Epsilon)
            {
                throw new ArgumentException("Circle from three points with three points on a line");
            }

            Vector2 Center = new Vector2(
                    x:(D * E - B * F) / G,
                    y: (A * F - C * E) / G);

            circle.Center = Center;
            circle.Radius = Vector2.Distance(Center, One);
            circle.RadiusSquared = circle.Radius * circle.Radius;
            //return new Circle(Center, Vector2.Distance(Center, One));
        }*/

        private static double[] CreateDeterminateMatrixRow(Vector2 p) => [p.X, p.Y, (p.X * p.X) + (p.Y * p.Y), 1];

        private static double[][] CreateContainsDeterminateMatrixComponents(Vector2[] cp) =>
            //if (cp.AreClockwise())
            //    cp = cp.Reverse().ToArray();
            //Debug.Assert(cp.AreClockwise() == false, "Determinate matrix for circle contains expects circle points to be passed in counter-clockwise order");

            [.. cp.Select(v => CreateDeterminateMatrixRow(v))];

        /// <summary>
        /// Relation of <paramref name="p1"/> to the disk of center <paramref name="cp"/> and <paramref name="radius"/>.
        /// Interior is Contained; on the circumference is Touching.
        /// </summary>
        public static ShapeRelation Contains(Vector2 cp, double radius, Vector2 p1)
        {
            var distance = Vector2.Distance(cp, p1);
            if (distance < radius)
                return ShapeRelation.Contained;
            else if (distance == radius)
                return ShapeRelation.Touching;
            else
                return ShapeRelation.None;
        }

        private static double Determinant4x4(double[] r0, double[] r1, double[] r2, double[] r3)
        {
            static double Det3(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
                (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));

            return (r0[0] * Det3(r1[1], r1[2], r1[3], r2[1], r2[2], r2[3], r3[1], r3[2], r3[3]))
                 - (r0[1] * Det3(r1[0], r1[2], r1[3], r2[0], r2[2], r2[3], r3[0], r3[2], r3[3]))
                 + (r0[2] * Det3(r1[0], r1[1], r1[3], r2[0], r2[1], r2[3], r3[0], r3[1], r3[3]))
                 - (r0[3] * Det3(r1[0], r1[1], r1[2], r2[0], r2[1], r2[2], r3[0], r3[1], r3[2]));
        }

        /// <summary>
        /// In-circle test: relation of <paramref name="p1"/> to the circumcircle of the three points in <paramref name="cp"/>.
        /// Named Contains for historical Delaunay call sites; it is not the instance <see cref="Contains(in IPoint2D)"/> predicate.
        /// </summary>
        /// <remarks>
        /// Uses the 4×4 determinant of Guibas and Stolfi, "Primitives for the manipulation of general
        /// subdivisions and the computation of Voronoi diagrams," ACM Trans. Graphics 4(2):74–123 (1985).
        /// Positive determinant (CCW triangle) is interior. This implementation is ordinary double arithmetic;
        /// for floating-point robustness see Shewchuk, "Adaptive Precision Floating-Point Arithmetic and Fast
        /// Robust Geometric Predicates," Discrete Comput. Geom. 18:305–363 (1997).
        /// </remarks>
        public static ShapeRelation Contains(Vector2[] cp, Vector2 p1)
        {
            double[][] cmat = CreateContainsDeterminateMatrixComponents(cp);
            double det = Determinant4x4(cmat[0], cmat[1], cmat[2], CreateDeterminateMatrixRow(p1));

            if (det >= Tolerance.EpsilonSquared)
                return ShapeRelation.Contained;
            else if (det > -Tolerance.EpsilonSquared && det < Tolerance.EpsilonSquared)
                return ShapeRelation.Touching;
            else
                return ShapeRelation.None;
        }

        /// <summary>
        /// In-circle test for each point against the circumcircle of <paramref name="cp"/> (same determinant as the scalar overload).
        /// </summary>
        public static ShapeRelation[] Contains(Vector2[] cp, IEnumerable<Vector2> points)
        {
            double[][] cmat = CreateContainsDeterminateMatrixComponents(cp);

            if (points is null)
                return null;

            int numPoints = points.Count();
            if (numPoints == 0)
                return [];

            ShapeRelation[] results = new ShapeRelation[numPoints];

            int i = 0;
            foreach (Vector2 p in points)
            {
                double det = Determinant4x4(cmat[0], cmat[1], cmat[2], CreateDeterminateMatrixRow(p));

                if (det < 0)
                    results[i] = ShapeRelation.None;
                else if (det <= Tolerance.EpsilonSquared)
                    results[i] = ShapeRelation.Touching;
                else if (det > 0)
                    results[i] = ShapeRelation.Contained;

                i++;
            }

            return results;
        }

        public static ShapeRelation Contains(Vector2 c1, Vector2 c2, Vector2 c3, Vector2 p1) => Contains([c1, c2, c3], p1);

        public Rectangle BoundingBox => new(this.Center, this.Radius);

        public double Area => this.RadiusSquared * Math.PI;

        public ShapeType2D ShapeType => ShapeType2D.Circle;

        IPoint2D ICircle2D.Center => this.Center;

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => [Center];

        double ICircle2D.Radius => this.Radius;

        public bool Contains(in IPoint2D p) => GetRelation(p).IsContains();

        public bool Covers(in IPoint2D p) => GetRelation(p).IsCovers();

        public bool Contains(in Vector2 p) => GetRelation(p).IsContains();

        public bool Covers(in Vector2 p) => GetRelation(p).IsCovers();

        public ShapeRelation GetRelation(in IPoint2D p) => GetRelation(p.ToVector2());

        public ShapeRelation GetRelation(in Vector2 p)
        {
            double xDist = p.X - Center.X;
            double yDist = p.Y - Center.Y;
            double distance = Math.Sqrt((xDist * xDist) + (yDist * yDist));
            if (Math.Abs(distance - Radius) <= Tolerance.Epsilon)
                return ShapeRelation.Touching;
            if (distance < Radius)
                return ShapeRelation.Contained;
            return ShapeRelation.None;
        }

        public bool Contains(in Polygon poly) => GetRelation(poly).IsContains();

        public bool Covers(in Polygon poly) => GetRelation(poly).IsCovers();

        public bool Contains(in LineSegment line) => GetRelation(line).IsContains();

        public bool Covers(in LineSegment line) => GetRelation(line).IsCovers();

        ShapeRelation IShape2D.GetRelation(in ILineSegment2D line) => GetRelation(line.ToLineSegment());

        public ShapeRelation GetRelation(in LineSegment line)
        {
            ShapeRelation oA = GetRelation(line.A);
            ShapeRelation oB = GetRelation(line.B);
            if (oA.IsCovers() && oB.IsCovers())
                return ShapeRelation.Contained;

            double distance = line.DistanceToPoint(Center);
            if (Math.Abs(distance - Radius) <= Tolerance.Epsilon)
                return ShapeRelation.Touching;
            if (distance < Radius)
                return ShapeRelation.Intersecting;

            return ShapeRelation.None;
        }

        public ShapeRelation GetRelation(in Polygon poly)
        {
            if (!BoundingBox.Intersects(poly.BoundingBox))
                return ShapeRelation.None;

            bool allCovered = true;
            bool anyContained = false;
            foreach (Vector2 p in poly.ExteriorRing)
            {
                ShapeRelation rel = GetRelation(p);
                if (rel == ShapeRelation.None)
                    allCovered = false;
                else if (rel == ShapeRelation.Contained)
                    anyContained = true;
            }

            if (allCovered)
                return GetRelation(poly.Centroid) == ShapeRelation.Contained || anyContained
                    ? ShapeRelation.Contained
                    : ShapeRelation.Touching;

            if (CircleIntersectionExtensions.Intersects(this, poly))
                return ShapeRelation.Intersecting;

            return ShapeRelation.None;
        }

        public ShapeRelation GetRelation(in Rectangle rect)
        {
            ShapeRelation covered = ClassifyCoveredVertices(rect.Corners, rect.Center);
            if (covered != ShapeRelation.None)
                return covered;
            return CircleIntersectionExtensions.Intersects(this, rect)
                ? ShapeRelation.Intersecting
                : ShapeRelation.None;
        }

        public ShapeRelation GetRelation(in Triangle tri)
        {
            ShapeRelation covered = ClassifyCoveredVertices(tri.Points, tri.Centroid);
            if (covered != ShapeRelation.None)
                return covered;
            return CircleIntersectionExtensions.Intersects(this, tri)
                ? ShapeRelation.Intersecting
                : ShapeRelation.None;
        }

        ShapeRelation ClassifyCoveredVertices(IReadOnlyList<Vector2> vertices, Vector2 centroid)
        {
            bool allCovered = true;
            bool anyContained = false;
            foreach (Vector2 p in vertices)
            {
                ShapeRelation rel = GetRelation(p);
                if (rel == ShapeRelation.None)
                    allCovered = false;
                else if (rel == ShapeRelation.Contained)
                    anyContained = true;
            }

            if (allCovered)
                return GetRelation(centroid) == ShapeRelation.Contained || anyContained
                    ? ShapeRelation.Contained
                    : ShapeRelation.Touching;

            return ShapeRelation.None;
        }

        /// <summary>
        /// OGC Contains: every point of <paramref name="shape"/> lies in this disk's interior.
        /// </summary>
        public bool Contains(in IShape2D shape) => GetRelation(shape).IsContains();

        /// <summary>
        /// OGC Covers: every point of <paramref name="shape"/> lies in this closed disk.
        /// </summary>
        public bool Covers(in IShape2D shape) => GetRelation(shape).IsCovers();

        public ShapeRelation GetRelation(in IShape2D shape)
        {
            if (shape is null)
                throw new ArgumentNullException(nameof(shape));

            Circle self = this;
            return shape.ShapeType switch
            {
                ShapeType2D.Point => self.GetRelation((IPoint2D)shape),
                ShapeType2D.Circle => self.RelationToCircle((ICircle2D)shape),
                ShapeType2D.Rectangle => self.GetRelation(((IRectangle2D)shape).ToRectangle()),
                ShapeType2D.Triangle => self.GetRelation(((ITriangle2D)shape).ToTriangle()),
                ShapeType2D.Quad => self.RelationToQuad((Quad)shape),
                ShapeType2D.Line => self.GetRelation(((ILineSegment2D)shape).ToLineSegment()),
                ShapeType2D.Polyline => self.RelationToPolyline((IPolyLine2D)shape),
                ShapeType2D.Polygon => self.GetRelation(((IPolygon2D)shape).ToPolygon()),
                ShapeType2D.Collection => ShapeRelationHelpers.RelationToCollection(self, (IShapeCollection2D)shape),
                ShapeType2D.InfiniteLine => self.RelationToInfiniteLine((Line)shape),
                _ => ShapeRelation.None,
            };
        }

        ShapeRelation RelationToCircle(ICircle2D other)
        {
            double distance = Vector2.Distance(Center, other.Center.ToVector2());
            double sum = distance + other.Radius;
            if (Math.Abs(sum - Radius) <= Tolerance.Epsilon)
                return ShapeRelation.Touching;
            if (sum < Radius)
                return ShapeRelation.Contained;
            if (distance <= Radius + other.Radius)
                return ShapeRelation.Intersecting;
            return ShapeRelation.None;
        }

        ShapeRelation RelationToQuad(in Quad quad)
        {
            Vector2[] verts = [quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft];
            ShapeRelation covered = ClassifyCoveredVertices(verts, quad.BoundingBox.Center);
            if (covered != ShapeRelation.None)
                return covered;
            return Intersects(ShapeRelationHelpers.QuadAsPolygon(quad))
                ? ShapeRelation.Intersecting
                : ShapeRelation.None;
        }

        ShapeRelation RelationToPolyline(IPolyLine2D line)
        {
            Circle self = this;
            Vector2[] verts = [.. line.Points.Select(p => p.ToVector2())];
            ShapeRelation covered = ClassifyCoveredVertices(verts, verts[0]);
            if (covered != ShapeRelation.None)
                return covered;
            return line.LineSegments.Any(s => self.Intersects(s.ToLineSegment()))
                ? ShapeRelation.Intersecting
                : ShapeRelation.None;
        }

        ShapeRelation RelationToInfiniteLine(in Line line)
        {
            double dist = Math.Abs((line.Direction.X * (Center.Y - line.Origin.Y)) -
                                   (line.Direction.Y * (Center.X - line.Origin.X)));
            if (Math.Abs(dist - Radius) <= Tolerance.Epsilon)
                return ShapeRelation.Touching;
            if (dist < Radius)
                return ShapeRelation.Intersecting;
            return ShapeRelation.None;
        }

        /// <summary>
        /// True if the circle intersects the disk of center <paramref name="p"/> and <paramref name="radius"/>.
        /// </summary>
        public bool Intersects(in Vector2 p, double radius)
        {

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;
            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ICircle2D c) => this.Intersects(c.ToCircle());

        public bool Intersects(in Circle c)
        {
            double XDist = c.Center.X - this.Center.X;
            double YDist = c.Center.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + c.Radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;

            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ILineSegment2D l) => Intersects(l.ToLineSegment());

        public bool Intersects(in LineSegment line) => CircleIntersectionExtensions.Intersects(in this, in line);

        public bool Intersects(in ITriangle2D t) => this.Intersects(t.ToTriangle());

        public bool Intersects(in Triangle tri) => CircleIntersectionExtensions.Intersects(in this, in tri);

        public bool Intersects(in IPolygon2D p)
        {
            Polygon poly = p.ToPolygon();
            return this.Intersects(in poly);
        }

        public bool Intersects(in Polygon poly) => CircleIntersectionExtensions.Intersects(in this, in poly);

        public bool Intersects(in IRectangle2D r)
        {
            Rectangle rect = r.ToRectangle();
            return this.Intersects(in rect);
        }

        public bool Intersects(in Rectangle rect) => CircleIntersectionExtensions.Intersects(in this, in rect);

        /// <summary>
        /// Distance to the circumference if outside; zero if the point is inside or on the circle.
        /// </summary>
        public double Distance(in Vector2 position)
        {
            double Distance = Vector2.Distance(position, this.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }


        public override bool Equals(object obj)
        {
            if (obj is Circle other)
                return this == other;

            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false;
        }

        public bool Equals(IShape2D other)
        {
            if (other is ICircle2D otherCircle)
                return Equals(otherCircle);

            return false;
        }

        public bool Equals(ICircle2D other) => this.Center.Equals(other.Center) && this.Radius.Equals(other.Radius);


        public override int GetHashCode() =>
            GeometryHashCode.Combine(Center.GetHashCode(), GeometryHashCode.QuantizedCoord(Radius));


        public bool Intersects(in IShape2D shape) => GetRelation(shape) != ShapeRelation.None;

        public IShape2D Translate(in IPoint2D offset) => this.Translate(offset.ToVector2());

        public Circle Translate(in Vector2 offset) => new Circle(this.Center + offset, this.Radius);

        public static bool operator ==(in Circle A, in Circle B)
        {
            return ((A.Center == B.Center) &&
                   (A.Radius == B.Radius));
        }

        public static bool operator !=(in Circle A, in Circle B) => !(A == B);

        /// <summary>
        /// Half-width of the unit circle at normalized height <paramref name="n"/> in [-1, 1] (chord length / 2).
        /// </summary>
        public static double WidthAtHeight(double n)
        {
            double angle = Math.Asin(n);
            double width = Math.Cos(angle);
            return Math.Abs(width);
        }

        public IPoint2D Centroid => Center;
    }
}
