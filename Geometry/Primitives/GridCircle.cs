using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public struct GridCircle : IShape2D, ICircle2D
    {
        public GridVector2 Center;
        public double Radius;
        public double RadiusSquared;

        public GridCircle(double X, double Y, double radius) : this(new GridVector2(X, Y), radius)
        { }

        public GridCircle(GridVector2 center, double radius)
        {
            this.Center = center;
            this.Radius = radius;

            if (double.IsInfinity(radius) || double.IsNaN(radius))
                throw new ArgumentException("Radius cannot be infinite or NaN");

            this.RadiusSquared = radius * radius;
            _HashCode = new int?();
        }

        public GridCircle(IPoint2D center, double radius) : this(new GridVector2(center.X, center.Y), radius)
        {
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

        private static double[] CreateDeterminateMatrixRow(GridVector2 p)
        {
            return new double[] { p.X, p.Y, (p.X * p.X) + (p.Y * p.Y), 1 };
        }

        private static double[][] CreateContainsDeterminateMatrixComponents(GridVector2[] cp)
        {
            //if (cp.AreClockwise())
            //    cp = cp.Reverse().ToArray();
            //Debug.Assert(cp.AreClockwise() == false, "Determinate matrix for circle contains expects circle points to be passed in counter-clockwise order");

            return cp.Select(v => CreateDeterminateMatrixRow(v)).ToArray();
        }

        /// <summary>
        /// Given three points on a circle, return true if the p1 is inside the circle.  Exactly on the circle is not 
        /// </summary>
        /// <param name="cp"></param>
        /// <param name="p1"></param>
        public static OverlapType Contains(GridVector2[] cp, GridVector2 p1)
        {
            double[][] cmat = CreateContainsDeterminateMatrixComponents(cp);

            MathNet.Numerics.LinearAlgebra.Matrix<double> matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfRowArrays(
               new double[][] { cmat[0],
                                cmat[1],
                                cmat[2],
                                CreateDeterminateMatrixRow(p1)});

            double det = matrix.Determinant();

            if (det > Global.EpsilonSquared)
                return OverlapType.CONTAINED;
            else if (det >= 0 && det <= Global.EpsilonSquared)
                return OverlapType.TOUCHING;
            else
                return OverlapType.NONE;

        }

        /// <summary>
        /// Given three points on a circle, return true if the p1 is inside the circle.  Exactly on the circle is not 
        /// </summary>
        /// <param name="cp"></param>
        /// <param name="p1"></param>
        public static OverlapType[] Contains(GridVector2[] cp, IEnumerable<GridVector2> points)
        {
            double[][] cmat = CreateContainsDeterminateMatrixComponents(cp);
            

            if (points == null)
                return null;

            int numPoints = points.Count();
            if (numPoints == 0)
                return new OverlapType[0];

            OverlapType[] results = new OverlapType[numPoints];

            MathNet.Numerics.LinearAlgebra.Matrix<double> matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfRowArrays(
                                new double[][] { cmat[0],
                                cmat[1],
                                cmat[2],
                                new double[]{0,0,0,1} });

            int i = 0;  
            foreach(GridVector2 p in points)
            { 
                matrix.SetRow(3, CreateDeterminateMatrixRow(p));
                double det = matrix.Determinant();

                if (det < 0)
                    results[i] = OverlapType.NONE;
                else if (det < Global.EpsilonSquared)
                    results[i] = OverlapType.TOUCHING;
                else if (det > 0)
                    results[i] = OverlapType.CONTAINED;

                i++;
            }

            return results;
        }

        public static OverlapType Contains(GridVector2 c1, GridVector2 c2, GridVector2 c3, GridVector2 p1)
        {
            return Contains(new GridVector2[] { c1, c2, c3 }, p1);
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

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.CIRCLE;
            }
        }

        IPoint2D ICircle2D.Center
        {
            get
            {
                return this.Center;
            }
        }

        double ICircle2D.Radius
        {
            get
            {
                return this.Radius;
            }
        }
        
        public bool Contains(IPoint2D p)
        {
            //return GridVector2.Distance(p, this.Center) <= this.Radius;
            
            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            return (XDist * XDist) + (YDist * YDist) <= this.RadiusSquared; 
        }

        public bool Contains(GridVector2 p)
        {
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

        public bool Intersects(ICircle2D c)
        {
            return this.Intersects(c.Convert());
        }

        public bool Intersects(GridCircle c)
        { 
            double XDist = c.Center.X - this.Center.X;
            double YDist = c.Center.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + c.Radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;

            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return CircleIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle tri)
        {
            return CircleIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(GridPolygon poly)
        {
            return CircleIntersectionExtensions.Intersects(this, poly);
        }

        public bool Intersects(IRectangle r)
        {
            GridRectangle rect = r.Convert();
            return this.Intersects(rect);
        }

        public bool Intersects(GridRectangle rect)
        {
            return CircleIntersectionExtensions.Intersects(this, rect);
        }

        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero if anywhere inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
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
         

        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.CircleIntersects(this, shape);
        }

        public IShape2D Translate(IPoint2D offset)
        {
            return this.Translate(offset.Convert());
        }

        public GridCircle Translate(GridVector2 offset)
        {
            return new GridCircle(this.Center + offset, this.Radius);
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
    }
}
