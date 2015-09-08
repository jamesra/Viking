using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public struct GridLineSegment : IComparable, ICloneable, IComparer<GridLineSegment>
    {
        public readonly GridVector2 A;
        public readonly GridVector2 B;

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

        public override string ToString()
        {
            if(MinX == A.X)
                return A.X.ToString("F") + " " + A.Y.ToString("F2") + " , " + B.X.ToString("F2") + " " + B.Y.ToString("F2");
            else
                return B.X.ToString("F") + " " + B.Y.ToString("F2") + " , " + A.X.ToString("F2") + " " + A.Y.ToString("F2");
            
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
            /*
            int CodeA = A.GetHashCode();
            int CodeB = B.GetHashCode();
            int Code;

            try
            {
                Code = CodeA + CodeB;
            }
            catch (OverflowException e)
            {
                Code = CodeA; 
            }

            return Code; */
        }

        public override bool Equals(object obj)
        {
            GridLineSegment ls;
//            try
//            {
                ls = (GridLineSegment)obj;
//            }
//            catch(System.InvalidCastException e)
//            {
//                return false; 
//            }
            /*
            if (A.X == ls.A.X &&
                A.Y == ls.A.Y &&
                B.X == ls.B.X &&
                B.Y == ls.B.Y)
                return true;
            */
                if (MaxX == ls.MaxX &&
                   MaxY == ls.MaxY &&
                   MinX == ls.MinX &&
                   MinY == ls.MinY)
                    return true; 

            return false; 
        }

        static public bool operator ==(GridLineSegment A, GridLineSegment B)
        {
//            if (A.A == B.A && A.B == B.B)
//                    return true;
            if (A.MaxX == B.MaxX &&
                   A.MaxY == B.MaxY &&
                   A.MinX == B.MinX &&
                   A.MinY == B.MinY)
                return true; 
            /*
            if (A.A.X == B.A.X &&
                A.A.Y == B.A.Y &&
                A.B.X == B.B.X &&
                A.B.Y == B.B.Y)
                return true;
            */
            return false; 
        }

        static public bool operator !=(GridLineSegment A, GridLineSegment B)
        {
            return !(A == B);
        }


        public double Length
        {
            get
            {
                double d1 = (A.X - B.X);
                d1 = d1 * d1;
                double d2 = (A.Y - B.Y);
                d2 = d2 * d2; 

                return Math.Sqrt(d1+d2); 
            }
        }

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
        public double yslope
        {
            get
            {
                return 1 / slope; 
            }
        }

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
        public bool SharedEndPoint(GridLineSegment seg, out GridVector2 Endpoint)
        {
            throw new NotImplementedException("SharedEndPoint not implemented");
        }

        public GridVector2 Bisect()
        {
            double x = (A.X + B.X) / 2.0;
            double y = (A.Y + B.Y) / 2.0;

            return new GridVector2(x, y); 
        }

        public GridVector2 Direction
        {
            get
            {
                GridVector2 D = B - A;
                return GridVector2.Normalize(D);
            }
        }

        public double DistanceToPoint(GridVector2 point)
        {
            GridVector2 temp;
            return DistanceToPoint(point, out temp);
        }

        /// <summary>
        /// Returns the distance of the line to the specified point
        /// </summary>
        /// <param name="point"></param>
        /// <param name="Intersection"></param>
        /// <returns></returns>
        public double DistanceToPoint(GridVector2 point, out GridVector2 Intersection)
        {
            double DX = B.X - A.X;
            double DY = B.Y - A.Y;

            if (DX < Global.Epsilon && DX > -Global.Epsilon)
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
            else if (DY < Global.Epsilon && DY > Global.Epsilon)
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
                else //(Point.X < MinX) //Point is below line segment, calculate distance
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

        public bool Intersects(GridLineSegment seg, out GridVector2 Intersection)
        {
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
                Intersection = new GridVector2();
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new GridVector2(x, y);

                double minX = Math.Min(A.X, B.X) - Global.Epsilon;
                double minSegX = Math.Min(seg.A.X, seg.B.X) - Global.Epsilon;

                if (minX > x || minSegX > x)
                    return false;

                double maxX = Math.Max(A.X, B.X) + Global.Epsilon; 
                double maxSegX = Math.Max(seg.A.X, seg.B.X) + Global.Epsilon;

                if (maxX < x || maxSegX < x)
                    return false;

                double minY = Math.Min(A.Y, B.Y)- Global.Epsilon;
                double minSegY = Math.Min(seg.A.Y, seg.B.Y) - Global.Epsilon;

                if (minY > y || minSegY > y)
                    return false;

                double maxY = Math.Max(A.Y, B.Y) + Global.Epsilon;
                double maxSegY = Math.Max(seg.A.Y, seg.B.Y) + Global.Epsilon;

                if (maxY < y || maxSegY < y)
                    return false;

                return true;
            }
        }

        public double MinX
        {
            get
            {
                return A.X < B.X ? A.X : B.X;
            }
        }

        public double MaxX
        {
            get
            {
                return A.X > B.X ? A.X : B.X;
            }
        }

        public double MinY
        {
            get
            {
                return A.Y < B.Y ? A.Y : B.Y;
            }
        }

        public double MaxY
        {
            get
            {
                return A.Y > B.Y ? A.Y : B.Y;
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
        }
    }
}
