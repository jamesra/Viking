using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
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
        /// Given a center and radius, returns true if p1 is contained in the circle
        /// </summary>
        /// <param name="cp">Circle center</param>
        /// <param name="radius">Circle radius </param>
        /// <param name="p1">Test point position</param>
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
        /// Given three points on a circle, return true if the p1 is inside the circle.  Exactly on the circle is not 
        /// </summary>
        /// <param name="cp"></param>
        /// <param name="p1"></param>
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
        /// Given three points on a circle, return true if the p1 is inside the circle.  Exactly on the circle is not 
        /// </summary>
        /// <param name="cp"></param>
        /// <param name="p1"></param>
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

        public ShapeRelation GetRelation(in IPoint2D p) => GetRelation(p.Convert());

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

        ShapeRelation IShape2D.GetRelation(in ILineSegment2D line) => GetRelation(line.Convert());

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

        public ShapeRelation GetRelation(in Rectangle rect) =>
            ClassifyCoveredVertices(rect.Corners, rect.Center);

        public ShapeRelation GetRelation(in Triangle tri) =>
            ClassifyCoveredVertices(tri.Points, tri.Centroid);

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
        public bool Contains(in IShape2D shape) => RelationTo(shape).IsContains();

        /// <summary>
        /// OGC Covers: every point of <paramref name="shape"/> lies in this closed disk.
        /// </summary>
        public bool Covers(in IShape2D shape) => RelationTo(shape).IsCovers();

        ShapeRelation RelationTo(in IShape2D shape)
        {
            if (shape is null)
                throw new ArgumentNullException(nameof(shape));

            Circle self = this;
            return shape.ShapeType switch
            {
                ShapeType2D.Point => self.GetRelation((IPoint2D)shape),
                ShapeType2D.Circle => self.RelationToCircle((ICircle2D)shape),
                ShapeType2D.Rectangle => self.GetRelation(((IRectangle2D)shape).Convert()),
                ShapeType2D.Triangle => self.GetRelation(((ITriangle2D)shape).Convert()),
                ShapeType2D.Quad => self.ClassifyCoveredVertices(
                    [((Quad)shape).BottomLeft, ((Quad)shape).BottomRight, ((Quad)shape).TopLeft, ((Quad)shape).TopRight],
                    ((Quad)shape).BoundingBox.Center),
                ShapeType2D.Line => self.GetRelation(((ILineSegment2D)shape).Convert()),
                ShapeType2D.Polyline => self.ClassifyCoveredVertices(
                    [.. ((IPolyLine2D)shape).Points.Select(p => p.Convert())],
                    ((IPolyLine2D)shape).Points[0].Convert()),
                ShapeType2D.Polygon => self.GetRelation(((IPolygon2D)shape).Convert()),
                ShapeType2D.Collection => RelationToCollection((IShapeCollection2D)shape),
                ShapeType2D.InfiniteLine => ShapeRelation.None,
                _ => ShapeRelation.None,
            };
        }

        ShapeRelation RelationToCircle(ICircle2D other)
        {
            double distance = Vector2.Distance(Center, other.Center.Convert());
            double sum = distance + other.Radius;
            if (Math.Abs(sum - Radius) <= Tolerance.Epsilon)
                return ShapeRelation.Touching;
            if (sum < Radius)
                return ShapeRelation.Contained;
            if (distance <= Radius + other.Radius)
                return ShapeRelation.Intersecting;
            return ShapeRelation.None;
        }

        ShapeRelation RelationToCollection(IShapeCollection2D collection)
        {
            ShapeRelation combined = ShapeRelation.Contained;
            foreach (IShape2D g in collection.Geometries)
            {
                ShapeRelation rel = RelationTo(g);
                if (rel == ShapeRelation.None)
                    return ShapeRelation.None;
                if (rel == ShapeRelation.Intersecting)
                    return ShapeRelation.Intersecting;
                if (rel == ShapeRelation.Touching)
                    combined = ShapeRelation.Touching;
            }

            return combined;
        }

        /// <summary>
        /// True if the circle intersects the circle with center c and radius r
        /// </summary>
        /// <param name="c"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public bool Intersects(in Vector2 p, double radius)
        {

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;
            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ICircle2D c) => this.Intersects(c.Convert());

        public bool Intersects(in Circle c)
        {
            double XDist = c.Center.X - this.Center.X;
            double YDist = c.Center.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + c.Radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;

            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in LineSegment line) => CircleIntersectionExtensions.Intersects(in this, in line);

        public bool Intersects(in ITriangle2D t) => this.Intersects(t.Convert());

        public bool Intersects(in Triangle tri) => CircleIntersectionExtensions.Intersects(in this, in tri);

        public bool Intersects(in IPolygon2D p)
        {
            Polygon poly = p.Convert();
            return this.Intersects(in poly);
        }

        public bool Intersects(in Polygon poly) => CircleIntersectionExtensions.Intersects(in this, in poly);

        public bool Intersects(in IRectangle2D r)
        {
            Rectangle rect = r.Convert();
            return this.Intersects(in rect);
        }

        public bool Intersects(in Rectangle rect) => CircleIntersectionExtensions.Intersects(in this, in rect);

        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero if anywhere inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
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


        public bool Intersects(in IShape2D shape) => ShapeExtensions.CircleIntersects(in this, in shape);

        public IShape2D Translate(in IPoint2D offset) => this.Translate(offset.Convert());

        public Circle Translate(in Vector2 offset) => new Circle(this.Center + offset, this.Radius);

        public static bool operator ==(in Circle A, in Circle B)
        {
            return ((A.Center == B.Center) &&
                   (A.Radius == B.Radius));
        }

        public static bool operator !=(in Circle A, in Circle B) => !(A == B);

        /// <summary>
        /// Given a normalized height in the range -1,1 on the Y-axis return how wide the circle is in the X-axis
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static double WidthAtHeight(double n)
        {
            double angle = Math.Asin(n);
            double width = Math.Cos(angle);
            return Math.Abs(width);
        }

        public IPoint2D Centroid => Center;
    }
}
