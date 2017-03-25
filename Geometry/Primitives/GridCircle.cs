using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public struct GridCircle : IShape2D
    {
        public GridVector2 Center;
        public double Radius;
        public double RadiusSquared;

        public GridCircle(GridVector2 center, double radius)
        {
            this.Center = center;
            this.Radius = radius;

            if (double.IsInfinity(radius) || double.IsNaN(radius))
                throw new ArgumentException("Radius cannot be infinite or NaN");

            this.RadiusSquared = radius * radius;
            _HashCode = new int?();
        }

        public override string ToString()
        {
            return Center.ToString() + " Radius: " + Radius.ToString("F2");
        }

        static public GridCircle CircleFromThreePoints(GridVector2[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");

            }

            Debug.Assert(points.Length == 3);
            if (points.Length != 3)
                throw new ArgumentException("GridCircle: Expected an array with three elements");

            GridVector2 A = points[0];
            GridVector2 B = points[1];
            GridVector2 C = points[2];

            return CircleFromThreePoints(A, B, C);
        }

        static public GridCircle CircleFromThreePoints(GridVector2 One, GridVector2 Two, GridVector2 Three)
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

            GridVector2 Center = new GridVector2();
            Center.X = (D * E - B * F) / G;
            Center.Y = (A * F - C * E) / G;

            return new GridCircle(Center, GridVector2.Distance(Center, One));
        }

        /// <summary>
        /// This exists because the Delaunay algorithm creates a ton of circles.  Allocating memory for them
        /// means taking the allocation lock twice instead of one (Circle is created by triangle object)
        /// </summary>
        /// <param name="One"></param>
        /// <param name="Two"></param>
        /// <param name="Three"></param>
        /// <returns></returns>
        static public void CircleFromThreePoints(GridVector2[] points, ref GridCircle circle)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");

            }

            Debug.Assert(points.Length == 3);
            if (points.Length != 3)
                throw new ArgumentException("GridCircle: Expected an array with three elements");

            GridCircle.CircleFromThreePoints(points[0], points[1], points[2], ref circle);
        }

        /// <summary>
        /// This exists because the Delaunay algorithm creates a ton of circles.  Allocating memory for them
        /// means taking the allocation lock twice instead of one (Circle is created by triangle object)
        /// </summary>
        /// <param name="One"></param>
        /// <param name="Two"></param>
        /// <param name="Three"></param>
        /// <returns></returns>
        static public void CircleFromThreePoints(GridVector2 One, GridVector2 Two, GridVector2 Three, ref GridCircle circle)
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

            GridVector2 Center = new GridVector2();
            Center.X = (D * E - B * F) / G;
            Center.Y = (A * F - C * E) / G;

            circle.Center = Center;
            circle.Radius = GridVector2.Distance(Center, One);
            circle.RadiusSquared = circle.Radius * circle.Radius;
            //return new GridCircle(Center, GridVector2.Distance(Center, One));
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return new GridRectangle(this.Center, this.Radius);
            }
        }

        public double Area
        {
            get
            {
                return this.RadiusSquared * Math.PI;
            }
        }

        public bool Contains(GridVector2 p)
        {
            //return GridVector2.Distance(p, this.Center) <= this.Radius;
            
            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            return (XDist * XDist) + (YDist * YDist) <= this.RadiusSquared; 
        }

        /// <summary>
        /// True if the circle intersects the circle with center c and radius r
        /// </summary>
        /// <param name="c"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public bool Intersects(GridVector2 p, double radius)
        {

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + radius;
            CombinedRadiusSquared *= CombinedRadiusSquared; 
            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(GridCircle c)
        {
            
            double XDist = c.Center.X - this.Center.X;
            double YDist = c.Center.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + c.Radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;

            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }


        public override bool Equals(object obj)
        {
            return (GridCircle)obj == this; 
        }

        int? _HashCode; 

        public override int GetHashCode()
        {
            if (!_HashCode.HasValue)
            {
                _HashCode = Center.GetHashCode(); 
            }

            return _HashCode.Value; 
        }

        public static bool operator ==(GridCircle A, GridCircle B)
        {
            return ((A.Center == B.Center) &&
                   (A.Radius == B.Radius));
        }

        public static bool operator !=(GridCircle A, GridCircle B)
        {
            return !(A == B);
        }

        
    }
}
