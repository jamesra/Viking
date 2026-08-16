using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    public readonly struct LineSegment : IComparable, ICloneable, IComparer<LineSegment>, ILineSegment2D, IHasControlPoints, IEquatable<LineSegment>, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {
        public readonly Vector2 A;
        public readonly Vector2 B;

        public LineSegment(IPoint2D A, IPoint2D B) : this(A.Convert(), B.Convert())
        {
        }

        public LineSegment(Vector2 A, Vector2 B)
        {
            /* This is a bad idea because callers expect A and B to maintain position
            int diff = A.Compare(A, B);
            this.A = diff <= 0 ? A : B;
            this.B = diff <= 0 ? B : A;
            */
            this.A = A;
            this.B = B;

            if (A == B)
            {
                throw new ArgumentException("Can't create line with two identical points");
            }
        }

        /// <summary>
        /// Create an array of grid line segments connecting the array of points in order
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static LineSegment[] SegmentsFromPoints(Vector2[] points)
        {
            if (points.Length < 2)
                throw new ArgumentException("Not enough points to create LineSegment array");

            LineSegment[] segs = new LineSegment[points.Length - 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                segs[i] = new LineSegment(points[i], points[i + 1]);
            }

            return segs;
        }

        public override string ToString() =>

            //if(MinX == A.X)
            $"A-B: {A.X:F2} {A.Y:F2} , {B.X:F2} {B.Y:F2}";//else//    return "B-A: " + B.X.ToString("F") + " " + B.Y.ToString("F2") + " , " + A.X.ToString("F2") + " " + A.Y.ToString("F2");            

        object ICloneable.Clone() => new LineSegment(A, B);

        public int Compare(LineSegment SegA, LineSegment SegB)
        {
            double diff = SegA.MinX - SegB.MinX;

            if (diff == 0.0)
            {
                diff = SegA.MinY - SegB.MinY;
                if (diff == 0.0)
                {
                    diff = SegA.MaxX - SegB.MaxX;
                    if (diff == 0.0)
                    {
                        diff = SegA.MaxY - SegB.MaxY;
                    }
                }
            }

            if (diff > 0)
                return 1;
            if (diff < 0)
                return -1;

            return 0;
        }

        public int CompareTo(object obj)
        {
            LineSegment SegB = (LineSegment)obj;

            return Compare(this, SegB);
        }

        public override int GetHashCode() => GeometryHashCode.LineSegmentDirected(A, B);

        public override bool Equals(object obj)
        {
            if (obj is LineSegment otherGS)
                return Equals(otherGS);
            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false;
        }

        public bool Equals(LineSegment other) => A.Equals(other.A) && B.Equals(other.B);

        /// <summary>
        /// True if the segments occupy the same undirected geometry (A-B equals B-A).
        /// </summary>
        public bool EquivalentUndirected(in LineSegment other) =>
            (A.Equals(other.A) && B.Equals(other.B)) || (A.Equals(other.B) && B.Equals(other.A));

        public bool Equals(ILineSegment2D other)
        {
            if (other is null)
                return false;

            return A.Equals(other.A) && B.Equals(other.B);
        }

        public bool Equals(IPolyLine2D other)
        {
            if (other is null || other.Points.Count != 2)
                return false;

            return A.Equals(other.Points[0]) && B.Equals(other.Points[1]);
        }

        public bool Equals(IShape2D other)
        {
            if (other is ILineSegment2D otherLine)
                return Equals(otherLine);
            if (other is IPolyLine2D otherPoly)
                return Equals(otherPoly);

            return false;
        }

        public static bool operator ==(LineSegment A, LineSegment B) => A.Equals(B);

        public static bool operator !=(LineSegment A, LineSegment B) => !A.Equals(B);


        public double Length => Vector2.Distance(A, B);

        /// <summary>
        /// The change in Y for values of X.
        /// Returns NAN if the line is vertical
        /// </summary>
        public double Slope
        {
            get
            {
                if (A.X == B.X)
                    return double.NaN;
                else
                {
                    double YDelta = B.Y - A.Y;
                    double XDelta = B.X - A.X;
                    return YDelta / XDelta;
                }
            }
        }

        /// <summary>
        /// The point where the line intercepts the y-axis, returns NAN if the line is vertical
        /// </summary>
        public double Intercept
        {
            get
            {
                if (A.X == B.X)
                    return double.NaN;

                return ((A.Y * B.X) - (B.Y * A.X)) / (B.X - A.X);

            }
        }

        /// <summary>
        /// The change in Y for values of X.
        /// Returns NAN if the line is vertical
        /// </summary>
        public double YSlope => 1 / Slope;

        /// <summary>
        /// The point where the line intercepts the y-axis, returns NAN if the line is vertical
        /// </summary>
        public double YIntercept
        {
            get
            {
                if (A.Y == B.Y)
                    return double.NaN;

                return A.X - (YSlope * A.Y);

            }
        }

        /// <summary>
        /// Return true if either point at each end of the line matches an endpoint of the passed segment
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public bool SharedEndPoint(in LineSegment seg)
        {
            bool AMatch = A == seg.A || A == seg.B;
            bool BMatch = B == seg.A || B == seg.B;

            return AMatch || BMatch;
        }

        /// <summary>
        /// Return true if either point at each end of the line matches an endpoint of the passed segment
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public bool SharedEndPoint(in LineSegment seg, out Vector2 Endpoint)
        {
            bool AMatch = A == seg.A || A == seg.B;
            bool BMatch = B == seg.A || B == seg.B;

            if (AMatch || BMatch)
            {
                Endpoint = AMatch ? A : B;
                return true;
            }
            else
            {
                Endpoint = Vector2.Zero;
                return false;
            }
        }

        public bool IsEndpoint(in IPoint2D p) => A == p || B == p;

        /// <summary>
        /// Return true if point p is to left when standing at A looking towards B
        /// </summary>
        /// <param name="p"></param>
        /// <returns> 1 for left
        ///           0 for on the line
        ///           -1 for right
        /// </returns>
        public int IsLeft(in Vector2 p)
        {
            double result = (B.X - A.X) * (p.Y - A.Y) - (B.Y - A.Y) * (p.X - A.X);
            if (result == 0)
                return 0;

            if (Math.Abs(result) < Tolerance.EpsilonSquared)
            {
                Triangle tri;
                try
                {
                    tri = new Triangle(A, B, p);
                }
                catch (ArgumentException)
                {
                    return 0; //This means the points are on a line
                }

                if (double.IsNaN(tri.Area))
                    return 0;

                if (tri.Area < Tolerance.Epsilon)
                {
                    return 0;
                }

            }

            return Math.Sign(result);
        }

        public Vector2 OppositeEndpoint(in Vector2 p) => A == p ? B : A;

        /// <summary>
        /// Returns the midpoint of the segment
        /// </summary>
        /// <returns></returns>
        public Vector2 Bisect()
        {
            double x = (A.X + B.X) / 2.0;
            double y = (A.Y + B.Y) / 2.0;

            return new Vector2(x, y);
        }
        IPoint2D ICentroid.Centroid => Bisect();

        public Vector2 Direction
        {
            get
            {
                Vector2 D = B - A;
                return Vector2.Normalize(D);
            }
        }


        public bool Contains(in Vector2 p) => GetRelation((IPoint2D)p).IsContains();

        public bool Covers(in Vector2 p) => Math.Abs(DistanceToPoint(p)) < Tolerance.Epsilon;

        /// <summary>
        /// Project the point p onto the line
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Dot(in Vector2 p) => Vector2.Dot(p - A, B - A);

        /// <summary>
        /// Return a normal to the line, the returned vector is normalized
        /// </summary>
        public Vector2 Normal
        {
            get
            {
                Vector2 delta = B - A;
                Vector2 normal = new(-delta.Y, delta.X);
                return Vector2.Normalize(normal);
            }
        }


        public double DistanceToPoint(in Vector2 point) => DistanceToPoint(point, out Vector2 temp);

        /// <summary>
        /// The point on the segment at a fractional distance between A & B
        /// </summary>
        /// <param name="fraction"></param>
        /// <returns></returns>
        public Vector2 PointAlongLine(double fraction)
        {
            Vector2 delta = B - A;
            delta *= fraction;

            return A + delta;
        }

        internal static bool NearlyZero(double value) => (value < Tolerance.Epsilon && value > -Tolerance.Epsilon);

        /// <summary>
        /// To find the nearest point to a line we project the point onto the infinite line along the line segment.  This function indicates if the point falls beyond the boundaries of the line segment.
        /// </summary>
        /// <param name="point"></param>
        /// <returns>True if proejected point lands within line segment</returns>
        public bool IsNearestPointWithinLineSegment(in Vector2 point)
        {
            double DX = B.X - A.X;
            double DY = B.Y - A.Y;

            /*Special case for horizontal or vertical lines*/
            if (NearlyZero(DX))
            {
                //Point is between line segment
                return point.Y <= MaxY && point.Y >= MinY;
            }
            else if (NearlyZero(DY))
            {
                //Point is between line segment
                return point.X <= MaxX && point.X >= MinX;
            }

            //Line is at an angle.  Find the intersection
            double t = ((point.X - A.X) * DX + (point.Y - A.Y) * DY) / (DX * DX + DY * DY);

            //Make sure t value is on the line 
            return t >= 0 && t <= 1.0;
        }

        /// <summary>
        /// Returns the distance of the line to the specified point
        /// </summary>
        /// <param name="point"></param>
        /// <param name="Intersection"></param>
        /// <returns></returns>
        public double DistanceToPoint(in Vector2 point, out Vector2 Intersection)
        {
            double DX = B.X - A.X;
            double DY = B.Y - A.Y;

            /*Special case for horizontal or vertical lines*/
            if (NearlyZero(DX))
            {
                //Point is between line segment
                if (point.Y <= MaxY &&
                   point.Y >= MinY)
                {
                    Intersection = new Vector2(A.X, point.Y);
                    return Math.Abs(point.X - A.X);
                }
                if (point.Y > MaxY) //Point is above line segment, calculate distance
                {
                    Intersection = new Vector2(A.X, MaxY);
                    return Vector2.Distance(point, Intersection);
                }
                else //(Point.Y < MinY) //Point is below line segment, calculate distance
                {
                    Intersection = new Vector2(A.X, MinY);
                    return Vector2.Distance(point, Intersection);
                }
            }
            else if (NearlyZero(DY))
            {
                //Point is between line segment
                if (point.X <= MaxX &&
                   point.X >= MinX)
                {
                    Intersection = new Vector2(point.X, A.Y);
                    return Math.Abs(point.Y - A.Y);
                }
                if (point.X > MaxX) //Point is to right of line segment, calculate distance
                {
                    Intersection = new Vector2(MaxX, A.Y);
                    return Vector2.Distance(point, new Vector2(MaxX, A.Y));
                }
                else //(Point.X < MinX) //Point is to left of line segment, calculate distance
                {
                    Intersection = new Vector2(MinX, A.Y);
                    return Vector2.Distance(point, new Vector2(MinX, A.Y));
                }
            }

            //Line is at an angle.  Find the intersection
            double t = ((point.X - A.X) * DX + (point.Y - A.Y) * DY) / (DX * DX + DY * DY);

            //Make sure t value is on the line
            double tOnTheLine = Math.Min(Math.Max(0, t), 1);

            if (tOnTheLine > 0 && tOnTheLine < 1.0)
            {
                Intersection = new Vector2(A.X + t * DX, A.Y + t * DY);
                return Vector2.Distance(point, Intersection);
            }
            else //Return the endpoint of the segment the point is closest to
            {
                double DistA = Vector2.Distance(point, A);
                double DistB = Vector2.Distance(point, B);
                Intersection = DistA < DistB ? A : B;
                return DistA < DistB ? DistA : DistB;
            }
        }

        public bool Intersects(in LineSegment seg) => this.Intersects(seg, out IShape2D intersection);

        public bool Intersects(in LineSegment seg, bool EndpointsOnRingDoNotIntersect) => this.Intersects(seg, EndpointsOnRingDoNotIntersect, out IShape2D intersection);

        public bool Intersects(in LineSegment seg, bool EndpointsOnRingDoNotIntersect, out IShape2D Intersection)
        {
            bool intersects = this.Intersects(seg, out Intersection);

            if (intersects && EndpointsOnRingDoNotIntersect)
            {
                if (Intersection.ShapeType == ShapeType2D.Point)
                {
                    return !(seg.IsEndpoint((IPoint2D)Intersection) || this.IsEndpoint((IPoint2D)Intersection));
                }
                else if (Intersection.ShapeType == ShapeType2D.Line)
                {
                    return true;
                }

                Debug.Fail("We should not be able to reach this case, a line intersection is either a point or a line");
                return true;
            }

            return intersects;
        }

        public bool Intersects(in LineSegment seg, out Vector2 Intersection)
        {
            Intersection = new Vector2();
            bool intersects = this.Intersects(seg, out IShape2D shape);
            if (intersects)
            {
                if (shape.ShapeType == ShapeType2D.Point)
                {
                    Intersection = (Vector2)shape;
                    return true;
                }
                else if (shape.ShapeType == ShapeType2D.Line)
                {
                    Intersection = (Vector2)(((ILineSegment2D)shape).A);
                    return true;
                }

                Debug.Fail("We should not be able to reach this case, a line intersection is either a point or a line");
                return true;
            }

            return intersects;
        }

        public bool Intersects(LineSegment seg, out IShape2D Intersection) => GetRelation(seg, out Intersection) != ShapeRelation.None;

        public ShapeRelation GetRelation(LineSegment seg, out IShape2D Intersection)
        {
            //Don't do the full check if the bounding boxes don't overlap

            if (this.MaxX < seg.MinX ||
                this.MaxY < seg.MinY ||
                this.MinX > seg.MaxX ||
                this.MinY > seg.MaxY)
            {
                Intersection = Vector2.NaN;
                return ShapeRelation.None;
            }


            //****
            //Profiling showed using BoundingBox implementation was slow because the Rectangle was being allocated in the property.
            /*
            if (!this.BoundingBox.Intersects(seg.BoundingBox))
            {
                Intersection = new Vector2();
                return false;
            }*/


            //Function for each line
            //Ax + By = C

            double A1 = B.Y - A.Y;
            double A2 = seg.B.Y - seg.A.Y;

            double B1 = A.X - B.X;
            double B2 = seg.A.X - seg.B.X;

            double C1 = A1 * A.X + B1 * A.Y;
            double C2 = A2 * seg.A.X + B2 * seg.A.Y;

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (Math.Abs(det) < Tolerance.EpsilonSquared)
            {
                Intersection = Vector2.NaN;

                //Find the bounding box of the overlapping region
                Rectangle? overlapRect = this.BoundingBox.Intersection(seg.BoundingBox);
                if (!overlapRect.HasValue)
                {
                    //Should never occur because we test bounding box overlap at the beginning of this function                    
                    return ShapeRelation.None;
                }

                //If they perfectly overlap at least two endpoints must be on the line.
                double[] distances = [overlapRect.Value.Covers(this.A) ? seg.DistanceToPoint(this.A) : double.MaxValue,
                                      overlapRect.Value.Covers(this.B) ? seg.DistanceToPoint(this.B) : double.MaxValue,
                                      overlapRect.Value.Covers(seg.A) ? this.DistanceToPoint(seg.A) : double.MaxValue,
                                      overlapRect.Value.Covers(seg.B) ? this.DistanceToPoint(seg.B) : double.MaxValue];

                //If there are two points on the line, those are the intersecting points
                if (distances.Count(d => d == 0) >= 2)
                {
                    Vector2[] endpoints = [.. new Vector2[] { seg.A, seg.B, this.A, this.B }.Distinct()];
                    Vector2[] endpointsOnLineCandidates = [.. endpoints.Where(e => overlapRect.Value.Covers(e) && seg.DistanceToPoint(e) < Tolerance.Epsilon)];

                    //Debug.Assert(endpointsOnLine.Length > 0, "Must have intersecting points if the bounding boxes overlap for parallel line intersection test");
                    if (endpointsOnLineCandidates.Length == 0)
                    {
                        return ShapeRelation.None;
                    }
                    else if (endpointsOnLineCandidates.Length == 1)
                    {
                        Intersection = endpointsOnLineCandidates[0];
                        return ShapeRelation.Touching;
                    }
                    else if (endpointsOnLineCandidates.Length == 2)
                    {
                        Intersection = new LineSegment(endpointsOnLineCandidates[0], endpointsOnLineCandidates[1]);
                        return ShapeRelation.Intersecting;
                    }
                    else
                    {
                        Vector2[] endpointsOnOverlapRect = [.. endpointsOnLineCandidates.Where(e => overlapRect.Value.Corners.Contains(e))];
                        Intersection = new LineSegment(endpointsOnOverlapRect[0], endpointsOnOverlapRect[1]);
                        if (endpointsOnLineCandidates.Length == 4)
                        {
                            return ShapeRelation.Contained;
                        }
                        else
                            return ShapeRelation.Intersecting;
                    }
                }

                //Parallel lines without a zero distance measurement do not intersect
                return ShapeRelation.None;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Vector2 intersection_point = new(x, y);
                Intersection = intersection_point;

                double minX = Math.Min(A.X, B.X) - Tolerance.EpsilonSquared;
                double minSegX = Math.Min(seg.A.X, seg.B.X) - Tolerance.EpsilonSquared;

                if (minX > x || minSegX > x)
                    return ShapeRelation.None;

                double maxX = Math.Max(A.X, B.X) + Tolerance.EpsilonSquared;
                double maxSegX = Math.Max(seg.A.X, seg.B.X) + Tolerance.EpsilonSquared;

                if (maxX < x || maxSegX < x)
                    return ShapeRelation.None;

                double minY = Math.Min(A.Y, B.Y) - Tolerance.EpsilonSquared;
                double minSegY = Math.Min(seg.A.Y, seg.B.Y) - Tolerance.EpsilonSquared;

                if (minY > y || minSegY > y)
                    return ShapeRelation.None;

                double maxY = Math.Max(A.Y, B.Y) + Tolerance.EpsilonSquared;
                double maxSegY = Math.Max(seg.A.Y, seg.B.Y) + Tolerance.EpsilonSquared;

                if (maxY < y || maxSegY < y)
                    return ShapeRelation.None;

                if (intersection_point == seg.A || intersection_point == seg.B || intersection_point == this.A || intersection_point == this.B)
                {
                    //Contact is on the endpoint of a tested line
                    return ShapeRelation.Touching;
                }
                else
                {
                    //Contact is somewhere in the middle of the tested line
                    return ShapeRelation.Intersecting;
                }
            }
        }

        public bool Intersects(in IEnumerable<LineSegment> seg)
        {
            LineSegment line = this;
            return seg.Any(ls => line.Intersects(ls));
        }

        public bool Intersects(in IShape2D shape) => ShapeExtensions.LineIntersects(this, shape);

        public bool Intersects(in ICircle2D c)
        {
            Circle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(in Circle circle) => LineIntersectionExtensions.Intersects(this, circle);

        public bool Intersects(in ILineSegment2D l)
        {
            LineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(in ITriangle2D t)
        {
            Triangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(in Triangle tri) => LineIntersectionExtensions.Intersects(this, tri);

        public bool Intersects(in IPolygon2D p)
        {
            Polygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(in Polygon poly) => LineIntersectionExtensions.Intersects(this, poly);

        public double MinX => A.X < B.X ? A.X : B.X;

        public double MaxX => A.X > B.X ? A.X : B.X;

        public double MinY => A.Y < B.Y ? A.Y : B.Y;

        public double MaxY => A.Y > B.Y ? A.Y : B.Y;

        public Rectangle BoundingBox => new(MinX, MaxX, MinY, MaxY);

        IPoint2D ILineSegment2D.A => this.A;

        IPoint2D ILineSegment2D.B => this.B;

        public double Area => 0;

        public ShapeType2D ShapeType => ShapeType2D.Line;

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => [A, B];

        public Line ToLine() => new(this.A, this.Direction);

        public bool Contains(in IPoint2D p) => GetRelation(p).IsContains();

        public bool Covers(in IPoint2D p) => GetRelation(p).IsCovers();

        public ShapeRelation GetRelation(in IPoint2D p)
        {
            Vector2 v = new(p.X, p.Y);
            if (!Covers(v))
                return ShapeRelation.None;

            if (Vector2.DistanceSquared(v, A) <= Tolerance.EpsilonSquared ||
                Vector2.DistanceSquared(v, B) <= Tolerance.EpsilonSquared)
                return ShapeRelation.Touching;

            return ShapeRelation.Contained;
        }

        public ShapeRelation GetRelation(in ILineSegment2D l) => GetRelation(l.Convert());

        public IShape2D Translate(in IPoint2D offset) => this.Translate(offset.Convert());

        public LineSegment Translate(in Vector2 offset) => new(this.A + offset, this.B + offset);
    }

    /// <summary>
    /// Treats A-B and B-A as the same segment. Use with Distinct/HashSet when undirected uniqueness is required.
    /// </summary>
    public sealed class LineSegmentUndirectedComparer : IEqualityComparer<LineSegment>
    {
        public static LineSegmentUndirectedComparer Default { get; } = new();

        public bool Equals(LineSegment x, LineSegment y) => x.EquivalentUndirected(y);

        public int GetHashCode(LineSegment obj) => GeometryHashCode.LineSegmentUndirected(obj.A, obj.B);
    }
}
