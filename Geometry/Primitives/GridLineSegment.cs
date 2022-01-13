using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    public readonly struct GridLineSegment : IComparable, ICloneable, IComparer<GridLineSegment>, ILineSegment2D, IEquatable<GridLineSegment>, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {
        public readonly GridVector2 A;
        public readonly GridVector2 B;
         
        public GridLineSegment(IPoint2D A, IPoint2D B) : this(A.Convert(), B.Convert())
        {
        }

        public GridLineSegment(GridVector2 A, GridVector2 B)
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
        public static GridLineSegment[] SegmentsFromPoints(GridVector2[] points)
        {
            if (points.Length < 2)
                throw new ArgumentException("Not enough points to create GridLineSegment array");
             
            GridLineSegment[] segs = new GridLineSegment[points.Length - 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                segs[i] = new GridLineSegment(points[i], points[i + 1]);
            }

            return segs;
        }

        public override string ToString()
        {
            
            //if(MinX == A.X)
            return "A-B: " + A.X.ToString("F") + " " + A.Y.ToString("F2") + " , " + B.X.ToString("F2") + " " + B.Y.ToString("F2");
            //else
            //    return "B-A: " + B.X.ToString("F") + " " + B.Y.ToString("F2") + " , " + A.X.ToString("F2") + " " + A.Y.ToString("F2");            
        }

        object ICloneable.Clone()
        {
            return new GridLineSegment(A, B);
        }

        public int Compare(GridLineSegment SegA, GridLineSegment SegB)
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
            GridLineSegment SegB = (GridLineSegment)obj;

            return Compare(this,SegB); 
        }

        public override int GetHashCode()
        {
            return (int)MinX;  
        }

        public override bool Equals(object obj)
        {
            if (obj is GridLineSegment otherGS)
            {
                return (this.A.Equals(otherGS.A) && this.B.Equals(otherGS.B)) ||
                       (this.B.Equals(otherGS.A) && this.A.Equals(otherGS.B));
            } 
            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false; 
        }

        public bool Equals(ILineSegment2D other)
        {
            return (this.A.Equals(other.A) && this.B.Equals(other.B)) ||
                   (this.B.Equals(other.A) && this.A.Equals(other.B));
        }

        public bool Equals(IPolyLine2D other)
        {
            if (other.Points.Count != 2)
                return false;

            return (A.Equals(other.Points[0]) && B.Equals(other.Points[1])) ||
                   (B.Equals(other.Points[0]) && A.Equals(other.Points[1]));
        }

        public bool Equals(IShape2D other)
        {
            if (other is ILineSegment2D otherLine)
                return Equals(otherLine);
            if (other is IPolyLine2D otherPoly)
                return Equals(otherPoly);
            
            return false;
        }

        public static bool operator ==(GridLineSegment A, GridLineSegment B)
        {
            if (A.A == B.A && A.B == B.B)
                return true;

            if (A.A == B.B && A.B == B.A)
                return true;
            
            return false; 
        }

        public static bool operator !=(GridLineSegment A, GridLineSegment B)
        {
            return !(A == B);
        }


        public double Length => GridVector2.Distance(A, B);

        /// <summary>
        /// The change in Y for values of X.
        /// Returns NAN if the line is vertical
        /// </summary>
        public double slope
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
        public double intercept
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
        public double yslope => 1 / slope;

        /// <summary>
        /// The point where the line intercepts the y-axis, returns NAN if the line is vertical
        /// </summary>
        public double yintercept
        {
            get
            {
                if (A.Y == B.Y)
                    return double.NaN;

                return A.X - (yslope * A.Y);

            }
        }

        /// <summary>
        /// Return true if either point at each end of the line matches an endpoint of the passed segment
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public bool SharedEndPoint(in GridLineSegment seg)
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
        public bool SharedEndPoint(in GridLineSegment seg, out GridVector2 Endpoint)
        {
            bool AMatch = A == seg.A || A == seg.B;
            bool BMatch = B == seg.A || B == seg.B;
             
            if(AMatch || BMatch)
            {
                Endpoint = AMatch ? A : B;
                return true;
            }
            else
            {
                Endpoint = GridVector2.Zero;
                return false;
            }
        }

        public bool IsEndpoint(in IPoint2D p)
        {
            return A == p || B == p;
        }

        /// <summary>
        /// Return true if point p is to left when standing at A looking towards B
        /// </summary>
        /// <param name="p"></param>
        /// <returns> 1 for left
        ///           0 for on the line
        ///           -1 for right
        /// </returns>
        public int IsLeft(in GridVector2 p)
        {
            double result = (B.X - A.X) * (p.Y - A.Y) - (B.Y - A.Y) * (p.X - A.X);
            if (result == 0)
                return 0; 

            if(Math.Abs(result) < Global.EpsilonSquared)
            {
                GridTriangle tri;
                try
                {
                    tri = new GridTriangle(A, B, p);
                }
                catch (ArgumentException)
                {
                    return 0; //This means the points are on a line
                }

                if (double.IsNaN(tri.Area))
                    return 0; 

                if(tri.Area < Global.Epsilon)
                {
                    return 0;
                }

            }

            return Math.Sign(result);
        }

        public GridVector2 OppositeEndpoint(in GridVector2 p)
        {
            return A == p ? B : A;
        }

        /// <summary>
        /// Returns the midpoint of the segment
        /// </summary>
        /// <returns></returns>
        public GridVector2 Bisect()
        {
            double x = (A.X + B.X) / 2.0;
            double y = (A.Y + B.Y) / 2.0;

            return new GridVector2(x, y); 
        }
        IPoint2D ICentroid.Centroid => Bisect();

        public GridVector2 Direction
        {
            get
            {
                GridVector2 D = B - A;
                return GridVector2.Normalize(D);
            }
        }


        public bool Contains(in GridVector2 p)
        {
            return Math.Abs(this.DistanceToPoint(p)) < Global.Epsilon;
        }

        /// <summary>
        /// Project the point p onto the line
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Dot(in GridVector2 p)
        {
            return GridVector2.Dot(p - A, B - A);
        }

        /// <summary>
        /// Return a normal to the line, the returned vector is normalized
        /// </summary>
        public GridVector2 Normal
        {
            get
            {
                GridVector2 delta = B - A;

                GridVector2 normal = new GridVector2(-delta.Y, delta.X);
                normal.Normalize();
                return normal;
            }
        }


        public double DistanceToPoint(in GridVector2 point)
        {
            return DistanceToPoint(point, out GridVector2 temp);
        }

        /// <summary>
        /// The point on the segment at a fractional distance between A & B
        /// </summary>
        /// <param name="fraction"></param>
        /// <returns></returns>
        public GridVector2 PointAlongLine(double fraction)
        {
            GridVector2 delta = B - A;
            delta *= fraction;

            return A + delta;
        }
          
        internal static bool NearlyZero(double value)
        {
            return (value < Global.Epsilon && value > -Global.Epsilon);
        }

        /// <summary>
        /// To find the nearest point to a line we project the point onto the infinite line along the line segment.  This function indicates if the point falls beyond the boundaries of the line segment.
        /// </summary>
        /// <param name="point"></param>
        /// <returns>True if proejected point lands within line segment</returns>
        public bool IsNearestPointWithinLineSegment(in GridVector2 point)
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
        public double DistanceToPoint(in GridVector2 point, out GridVector2 Intersection)
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
                    Intersection = new GridVector2(A.X, point.Y);
                    return Math.Abs(point.X - A.X);
                }
                if (point.Y > MaxY) //Point is above line segment, calculate distance
                {
                    Intersection = new GridVector2(A.X, MaxY);
                    return GridVector2.Distance(point, Intersection);
                }
                else //(Point.Y < MinY) //Point is below line segment, calculate distance
                {
                    Intersection = new GridVector2(A.X, MinY);
                    return GridVector2.Distance(point, Intersection);
                }
            }
            else if (NearlyZero(DY))
            {
                //Point is between line segment
                if (point.X <= MaxX &&
                   point.X >= MinX)
                {
                    Intersection = new GridVector2(point.X, A.Y);
                    return Math.Abs(point.Y - A.Y);
                }
                if (point.X > MaxX) //Point is to right of line segment, calculate distance
                {
                    Intersection = new GridVector2(MaxX, A.Y);
                    return GridVector2.Distance(point, new GridVector2(MaxX, A.Y));
                }
                else //(Point.X < MinX) //Point is to left of line segment, calculate distance
                {
                    Intersection = new GridVector2(MinX, A.Y);
                    return GridVector2.Distance(point, new GridVector2(MinX, A.Y));
                }
            }

            //Line is at an angle.  Find the intersection
            double t = ((point.X - A.X) * DX + (point.Y - A.Y) * DY) / (DX * DX + DY * DY); 

            //Make sure t value is on the line
            double tOnTheLine = Math.Min(Math.Max(0, t), 1);

            if (tOnTheLine > 0 && tOnTheLine < 1.0)
            {
                Intersection = new GridVector2(A.X + t * DX, A.Y + t * DY);
                return GridVector2.Distance(point, Intersection);
            }
            else //Return the endpoint of the segment the point is closest to
            {
                double DistA = GridVector2.Distance(point, A);
                double DistB = GridVector2.Distance(point, B);
                Intersection = DistA < DistB ? A : B; 
                return DistA < DistB ? DistA : DistB; 
            }
        }

        public bool Intersects(in GridLineSegment seg)
        {
            return this.Intersects(seg, out IShape2D intersection);
        }

        public bool Intersects(in GridLineSegment seg, bool EndpointsOnRingDoNotIntersect)
        {
            return this.Intersects(seg, EndpointsOnRingDoNotIntersect, out IShape2D intersection);
        }

        public bool Intersects(in GridLineSegment seg, bool EndpointsOnRingDoNotIntersect, out IShape2D Intersection)
        { 
            bool intersects = this.Intersects(seg, out Intersection); 

            if(intersects && EndpointsOnRingDoNotIntersect)
            {
                if (Intersection.ShapeType == ShapeType2D.POINT)
                { 
                    return !(seg.IsEndpoint((IPoint2D)Intersection) || this.IsEndpoint((IPoint2D)Intersection));
                }
                else if(Intersection.ShapeType == ShapeType2D.LINE)
                {
                    return true;
                }

                Debug.Fail("We should not be able to reach this case, a line intersection is either a point or a line");
                return true;
            }

            return intersects;
        }

        public bool Intersects(in GridLineSegment seg, out GridVector2 Intersection)
        {
            Intersection = new GridVector2();
            bool intersects = this.Intersects(seg, out IShape2D shape);
            if(intersects)
            {
                if (shape.ShapeType == ShapeType2D.POINT)
                {
                    Intersection = (GridVector2)shape;
                    return true;
                }
                else if (shape.ShapeType == ShapeType2D.LINE)
                {
                    Intersection = (GridVector2)(((ILineSegment2D)shape).A);
                    return true;
                }

                Debug.Fail("We should not be able to reach this case, a line intersection is either a point or a line");
                return true;
            }

            return intersects;
        }

        public bool Intersects(GridLineSegment seg, out IShape2D Intersection)
        {
            //Don't do the full check if the bounding boxes don't overlap

            if (this.MaxX < seg.MinX ||
                this.MaxY < seg.MinY ||
                this.MinX > seg.MaxX ||
                this.MinY > seg.MaxY)
            {
                Intersection = new GridVector2();
                return false;
            }

            
            //****
            //Profiling showed using BoundingBox implementation was slow because the GridRectangle was being allocated in the property.
            /*
            if (!this.BoundingBox.Intersects(seg.BoundingBox))
            {
                Intersection = new GridVector2();
                return false;
            }*/
            

            //Function for each line
            //Ax + By = C

            double A1 = B.Y - A.Y;
            double A2 = seg.B.Y - seg.A.Y;

            double B1 = A.X - B.X;
            double B2 = seg.A.X - seg.B.X;
            
            double C1 = A1*A.X + B1 * A.Y; 
            double C2 = A2*seg.A.X + B2 * seg.A.Y; 

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            {
                Intersection = GridVector2.Zero;

                //Find the bounding box of the overlapping region
                GridRectangle? overlapRect = this.BoundingBox.Intersection(seg.BoundingBox);
                if (!overlapRect.HasValue)
                {
                    //Should never occur because we test bounding box overlap at the beginning of this function
                    return false;
                }

                //If they perfectly overlap at least two endpoints must be on the line.
                double[] distances = {overlapRect.Value.Contains(this.A) ? seg.DistanceToPoint(this.A) : double.MaxValue,
                                      overlapRect.Value.Contains(this.B) ? seg.DistanceToPoint(this.B) : double.MaxValue,
                                      overlapRect.Value.Contains(seg.A) ? this.DistanceToPoint(seg.A) : double.MaxValue,
                                      overlapRect.Value.Contains(seg.B) ? this.DistanceToPoint(seg.B) : double.MaxValue};

                //If there are two points on the line, those are the intersecting points
                if(distances.Count(d => d == 0) >= 2)
                {
                    GridVector2[] endpoints = new GridVector2[] { seg.A, seg.B, this.A, this.B }.Distinct().ToArray();
                    GridVector2[] endpointsOnLineCandidates = endpoints.Where(e => overlapRect.Value.Contains(e) && seg.DistanceToPoint(e) < Global.Epsilon).ToArray();

                    //Debug.Assert(endpointsOnLine.Length > 0, "Must have intersecting points if the bounding boxes overlap for parallel line intersection test");
                    if (endpointsOnLineCandidates.Length == 0)
                    {
                        return false;
                    }
                    else if (endpointsOnLineCandidates.Length == 1)
                    {
                        Intersection = endpointsOnLineCandidates[0];
                        return true;
                    }
                    else if (endpointsOnLineCandidates.Length == 2)
                    {
                        Intersection = new GridLineSegment(endpointsOnLineCandidates[0], endpointsOnLineCandidates[1]);
                        return true;
                    }
                    else
                    {
                        GridVector2[] endpointsOnOverlapRect = endpointsOnLineCandidates.Where(e => overlapRect.Value.Corners.Contains(e)).ToArray();
                        Intersection = new GridLineSegment(endpointsOnOverlapRect[0], endpointsOnOverlapRect[1]);
                        return true;
                    }
                }

                //Parallel lines without a zero distance measurement do not intersect
                return false;
                
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new GridVector2(x, y);

                double minX = Math.Min(A.X, B.X) - Global.EpsilonSquared;
                double minSegX = Math.Min(seg.A.X, seg.B.X) - Global.EpsilonSquared;

                if (minX > x || minSegX > x)
                    return false;

                double maxX = Math.Max(A.X, B.X) + Global.EpsilonSquared; 
                double maxSegX = Math.Max(seg.A.X, seg.B.X) + Global.EpsilonSquared;

                if (maxX < x || maxSegX < x)
                    return false;

                double minY = Math.Min(A.Y, B.Y)- Global.EpsilonSquared;
                double minSegY = Math.Min(seg.A.Y, seg.B.Y) - Global.EpsilonSquared;

                if (minY > y || minSegY > y)
                    return false;

                double maxY = Math.Max(A.Y, B.Y) + Global.EpsilonSquared;
                double maxSegY = Math.Max(seg.A.Y, seg.B.Y) + Global.EpsilonSquared;

                if (maxY < y || maxSegY < y)
                    return false;

                return true;
            }
        }

        public bool Intersects(in IEnumerable<GridLineSegment> seg)
        {
            GridLineSegment line = this;
            return seg.Any(ls => line.Intersects(ls));
        }

        public bool Intersects(in IShape2D shape)
        {
            return ShapeExtensions.LineIntersects(this, shape);
        }

        public bool Intersects(in ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(in GridCircle circle)
        {
            return LineIntersectionExtensions.Intersects(this, circle);
        } 

        public bool Intersects(in ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }
         
        public bool Intersects(in ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(in GridTriangle tri)
        {
            return LineIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(in IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(in GridPolygon poly)
        {
            return LineIntersectionExtensions.Intersects(this, poly);
        }

        public double MinX => A.X < B.X ? A.X : B.X;

        public double MaxX => A.X > B.X ? A.X : B.X;

        public double MinY => A.Y < B.Y ? A.Y : B.Y;

        public double MaxY => A.Y > B.Y ? A.Y : B.Y; 

        public GridRectangle BoundingBox => new GridRectangle(MinX, MaxX, MinY, MaxY);
        
        IPoint2D ILineSegment2D.A => this.A;

        IPoint2D ILineSegment2D.B => this.B;

        public double Area => throw new NotImplementedException();

        public ShapeType2D ShapeType => ShapeType2D.LINE;

        GridVector2 IShape2D.Centroid => PointAlongLine(0.5);

        public GridLine ToLine()
        {
            return new GridLine(this.A, this.Direction);
        }

        public bool Contains(in IPoint2D p)
        {
            return Contains(new GridVector2(p.X,p.Y));
        }

        public IShape2D Translate(in IPoint2D offset)
        {
            return this.Translate(offset.Convert());
        }

        public GridLineSegment Translate(in GridVector2 offset)
        {
            return new GridLineSegment(this.A + offset, this.B + offset);
        }

        bool IEquatable<GridLineSegment>.Equals(GridLineSegment other)
        {
            return this == other;
        }
    }
}
