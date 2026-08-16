using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Sorts points in clockwise order around a line from A to B, with A as the origin
    /// </summary>
    public class CompareAngle : IComparer<Vector2>, IComparer<IPoint2D>
    {
        /// <summary>
        /// A line we are ordering points around by angle.  A is the origin.
        /// </summary>
        private readonly Line Line;
        private readonly Vector2 ComparisonPoint;

        public readonly bool ClockwiseOrder = false;

        public CompareAngle(LineSegment line, bool clockwise = false)
        {
            Line = new Line(line.A, line.Direction);
            ComparisonPoint = Line.Origin + Line.Direction;
            ClockwiseOrder = clockwise;
        }

        public CompareAngle(Line line, bool clockwise = false)
        {
            Line = line;
            ComparisonPoint = Line.Origin + Line.Direction;
            ClockwiseOrder = clockwise;
        }

        public int Compare(Vector2 A, Vector2 B)
        {
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            double angleA = Vector2.AbsArcAngle(Line.Origin, A, ComparisonPoint, ClockwiseOrder);
            double angleB = Vector2.AbsArcAngle(Line.Origin, B, ComparisonPoint, ClockwiseOrder);

            //return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
            return angleA.CompareTo(angleB);
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            //We are measuring the angle from the line in one direction, so don't allow negative angles

            double angleA = Vector2.AbsArcAngle(Line.Origin, A, ComparisonPoint, ClockwiseOrder);
            double angleB = Vector2.AbsArcAngle(Line.Origin, B, ComparisonPoint, ClockwiseOrder);

            //return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
            return angleA.CompareTo(angleB);
        }
    }

    /// <summary>
    /// Infinite line through <see cref="Origin"/> along unit <see cref="Direction"/>.
    /// Empty boundary: on-line points are <see cref="ShapeRelation.Contained"/>, never Touching.
    /// </summary>
    [Serializable]
    public readonly struct Line : ILine2D, IEquatable<ILine2D>
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;

        public Line(Vector2 O, Vector2 dir)
        {
            Origin = O;
            this.Direction = Vector2.Normalize(dir);
        }

        public Line(IPoint2D O, IPoint2D dir)
        {
            Origin = new Vector2(O.X, O.Y);
            this.Direction = Vector2.Normalize(new Vector2(dir.X, dir.Y));
        }

        public override string ToString() => $"Line Origin {Origin} Direction {Direction}";

        public bool Intersects(Line seg, out Vector2 Intersection)
        {
            //Function for each line
            //Ax + By = C
            Intersection = new Vector2();

            //if (seg is null)
            //    throw new ArgumentNullException("seg");

            if (this.Direction == seg.Direction)
                return false;

            Vector2 A = Origin;
            Vector2 B = Origin + Direction;

            Vector2 segA = seg.Origin;
            Vector2 segB = seg.Origin + seg.Direction;

            double A1 = B.Y - A.Y;
            double A2 = segB.Y - segA.Y;

            double B1 = A.X - B.X;
            double B2 = segA.X - segB.X;

            double C1 = A1 * A.X + B1 * A.Y;
            double C2 = A2 * segA.X + B2 * segA.Y;

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            {
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new Vector2(x, y);
                return true;
            }
        }

        public Line Perpendicular() => new Line(this.Origin, Vector2.Rotate90(Direction));

        public bool Intersects(LineSegment seg, out Vector2 Intersection)
        {
            //Function for each line
            //Ax + By = C
            Intersection = new Vector2();

            if (this.Direction == seg.Direction)
                return false;

            Vector2 A = Origin;
            Vector2 B = Origin + Direction;

            Vector2 segA = seg.A;
            Vector2 segB = seg.B;

            double A1 = B.Y - A.Y;
            double A2 = segB.Y - segA.Y;

            double B1 = A.X - B.X;
            double B2 = segA.X - segB.X;

            double C1 = A1 * A.X + B1 * A.Y;
            double C2 = A2 * segA.X + B2 * segA.Y;

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            {
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new Vector2(x, y);

                return seg.BoundingBox.Covers(Intersection);
            }
        }

        /// <summary>
        /// Returns a line starting at origin of the specified length
        /// </summary>
        /// <param name="Length"></param>
        /// <returns></returns>
        public LineSegment ToLine(double Length)
        {
            Vector2 endpoint = this.Direction * Length;
            endpoint += this.Origin;
            LineSegment output = new(this.Origin, endpoint);
            System.Diagnostics.Debug.Assert(Math.Abs(output.Length - Length) < Tolerance.Epsilon, "Created line does not match requested length");
            return output;
        }

        /// <summary>
        /// Return true if point p is to left when standing at A looking towards B
        /// </summary>
        /// <param name="p"></param>
        /// <returns> 1 for left
        ///           0 for on the line
        ///           -1 for right
        /// </returns>
        public int IsLeft(Vector2 p)
        {
            double result = (Direction.X * (p.Y - Origin.Y)) - (Direction.Y * (p.X - Origin.X));
            if (result == 0)
                return 0;

            if (Math.Abs(result) < Tolerance.EpsilonSquared)
            {
                //                if (Vector2.Distance(p, A) < Tolerance.Epsilon || Vector2.Distance(p, B) < Tolerance.Epsilon)
                //                  return 0; 
                Triangle tri;
                try
                {
                    tri = new Triangle(Origin, Origin + Direction, p);
                }
                catch (ArgumentException)
                {
                    return 0; //This means the points are on a line
                }

                if (double.IsNaN(tri.Area) || tri.Area == 0)
                    return 0;
            }

            return Math.Sign(result);
        }

        /// <summary>
        /// Infinite lines have no finite AABB. This is a NaN rectangle: AABB culling treats it as intersecting every finite box, while <see cref="Rectangle.Covers(in IPoint2D)"/> on that box is false.
        /// </summary>
        public Rectangle BoundingBox => new(double.NaN, double.NaN, double.NaN, double.NaN);

        public double Area => 0;

        public ShapeType2D ShapeType => ShapeType2D.InfiniteLine;

        IPoint2D ILine2D.Origin => Origin;

        IPoint2D ILine2D.Direction => Direction;

        public bool Contains(in IPoint2D p) => GetRelation(p).IsContains();

        public bool Covers(in IPoint2D p) => GetRelation(p).IsCovers();

        /// <summary>
        /// On the line is Contained (no boundary). Off the line is None.
        /// </summary>
        public ShapeRelation GetRelation(in IPoint2D p) => ContainsOnLine(p) ? ShapeRelation.Contained : ShapeRelation.None;

        bool ContainsOnLine(in IPoint2D p) => IsLeft(new Vector2(p.X, p.Y)) == 0;

        public ShapeRelation GetRelation(in ILineSegment2D line)
        {
            LineSegment seg = line.Convert();
            bool aOn = Covers((IPoint2D)seg.A);
            bool bOn = Covers((IPoint2D)seg.B);
            if (aOn && bOn)
                return ShapeRelation.Contained;
            if (Intersects(seg, out _))
                return aOn || bOn ? ShapeRelation.Touching : ShapeRelation.Intersecting;
            return ShapeRelation.None;
        }

        public bool Intersects(in IShape2D shape)
        {
            if (shape is null)
                throw new ArgumentNullException(nameof(shape));

            Line self = this;
            return shape.ShapeType switch
            {
                ShapeType2D.Point => self.Covers((IPoint2D)shape),
                ShapeType2D.Line => self.Intersects(((ILineSegment2D)shape).Convert(), out _),
                ShapeType2D.InfiniteLine => shape is Line otherLine && self.Intersects(otherLine, out _),
                ShapeType2D.Circle => self.DistanceToPoint(((ICircle2D)shape).Center.Convert()) <= ((ICircle2D)shape).Radius,
                ShapeType2D.Rectangle => self.IntersectsRectangle(((IRectangle2D)shape).Convert()),
                ShapeType2D.Triangle => ((ITriangle2D)shape).Convert().Segments.Any(s => self.Intersects(s, out _)),
                ShapeType2D.Quad => self.IntersectsQuad((Quad)shape),
                ShapeType2D.Polygon => ((IPolygon2D)shape).Convert().AllSegments.Any(s => self.Intersects(s, out _)),
                ShapeType2D.Polyline => ((IPolyLine2D)shape).LineSegments.Any(s => self.Intersects(s.Convert(), out _)),
                ShapeType2D.Collection => ((IShapeCollection2D)shape).Geometries.Any(g => self.Intersects(g)),
                _ => false,
            };
        }

        public IShape2D Translate(in IPoint2D offset) => new Line(Origin + offset.Convert(), Direction);

        public bool Equals(IShape2D other) => other is ILine2D line && Equals(line);

        public bool Equals(ILine2D other)
        {
            if (other is null)
                return false;
            return Origin.Equals(other.Origin) && Direction.Equals(other.Direction);
        }

        public override bool Equals(object obj) => obj is Line line && this == line;

        public override int GetHashCode() => GeometryHashCode.Combine(Origin.GetHashCode(), Direction.GetHashCode());

        public static bool operator ==(in Line a, in Line b) => a.Origin == b.Origin && a.Direction == b.Direction;

        public static bool operator !=(in Line a, in Line b) => !(a == b);

        double DistanceToPoint(Vector2 p) =>
            Math.Abs((Direction.X * (p.Y - Origin.Y)) - (Direction.Y * (p.X - Origin.X)));

        bool IntersectsRectangle(Rectangle rect) =>
            Intersects(rect.LeftEdge, out _) || Intersects(rect.RightEdge, out _) ||
            Intersects(rect.TopEdge, out _) || Intersects(rect.BottomEdge, out _);

        bool IntersectsQuad(Quad quad) =>
            Intersects(new LineSegment(quad.BottomLeft, quad.BottomRight), out _) ||
            Intersects(new LineSegment(quad.BottomRight, quad.TopRight), out _) ||
            Intersects(new LineSegment(quad.TopRight, quad.TopLeft), out _) ||
            Intersects(new LineSegment(quad.TopLeft, quad.BottomLeft), out _) ||
            quad.Covers(Origin);
    }
}
