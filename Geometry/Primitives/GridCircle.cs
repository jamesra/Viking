using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    public readonly struct GridCircle : IShape2D, ICircle2D, IEquatable<ICircle2D>
    {
        public readonly GridVector2 Center;
        public readonly double Radius;
        public readonly double RadiusSquared;

        public GridCircle(double X, double Y, double radius) : this(new GridVector2(X, Y), radius)
        { }

        public GridCircle(GridVector2 center, double radius)
        {
            this.Center = center;
            this.Radius = radius;

            if (double.IsInfinity(radius) || double.IsNaN(radius))
                throw new ArgumentException("Radius cannot be infinite or NaN");

            this.RadiusSquared = radius * radius;
        }

        public GridCircle(IPoint2D center, double radius) : this(new GridVector2(center.X, center.Y), radius)
        {
        }

        public override string ToString()
        {
            return Center.ToString() + " Radius: " + Radius.ToString("F2");
        }

        public static GridCircle CircleFromThreePoints(GridVector2[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));

            }

            Debug.Assert(points.Length == 3);
            if (points.Length != 3)
                throw new ArgumentException("GridCircle: Expected an array with three elements");

            GridVector2 A = points[0];
            GridVector2 B = points[1];
            GridVector2 C = points[2];

            return CircleFromThreePoints(A, B, C);
        }

        public static GridCircle CircleFromThreePoints(GridVector2 One, GridVector2 Two, GridVector2 Three)
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

            GridVector2 Center = new GridVector2(
                x: (D * E - B * F) / G,
                y: (A * F - C * E) / G
            );

            return new GridCircle(Center, GridVector2.Distance(in Center, in One));
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

            GridVector2 Center = new GridVector2(
                    x:(D * E - B * F) / G,
                    y: (A * F - C * E) / G);

            circle.Center = Center;
            circle.Radius = GridVector2.Distance(Center, One);
            circle.RadiusSquared = circle.Radius * circle.Radius;
            //return new GridCircle(Center, GridVector2.Distance(Center, One));
        }*/

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

            if (det >= Global.EpsilonSquared)
                return OverlapType.CONTAINED;
            else if (det > -Global.EpsilonSquared && det < Global.EpsilonSquared)
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
                return Array.Empty<OverlapType>();

            OverlapType[] results = new OverlapType[numPoints];

            MathNet.Numerics.LinearAlgebra.Matrix<double> matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfRowArrays(
                                new double[][] { cmat[0],
                                cmat[1],
                                cmat[2],
                                new double[]{0,0,0,1} });

            int i = 0;
            foreach (GridVector2 p in points)
            {
                matrix.SetRow(3, CreateDeterminateMatrixRow(p));
                double det = matrix.Determinant();

                if (det < 0)
                    results[i] = OverlapType.NONE;
                else if (det <= Global.EpsilonSquared)
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

        public bool Contains(in IPoint2D p)
        {
            //return GridVector2.Distance(p, this.Center) <= this.Radius;

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            return (XDist * XDist) + (YDist * YDist) <= this.RadiusSquared;
        }

        public bool Contains(in GridVector2 p)
        {
            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            return (XDist * XDist) + (YDist * YDist) <= this.RadiusSquared;
        }

        public OverlapType ContainsExt(in IPoint2D p)
        {
            //return GridVector2.Distance(p, this.Center) <= this.Radius;

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            double DistanceSquared = (XDist * XDist) + (YDist * YDist);
            if (DistanceSquared < this.RadiusSquared)
                return OverlapType.CONTAINED;
            if (DistanceSquared == this.RadiusSquared)
                return OverlapType.TOUCHING;

            return OverlapType.NONE;
        }

        public OverlapType ContainsExt(in GridVector2 p)
        {
            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;

            double DistanceSquared = (XDist * XDist) + (YDist * YDist);
            if (DistanceSquared < this.RadiusSquared)
                return OverlapType.CONTAINED;
            if (DistanceSquared == this.RadiusSquared)
                return OverlapType.TOUCHING;

            return OverlapType.NONE;
        }

        public bool Contains(in GridPolygon poly)
        {
            //if (this.BoundingBox.ContainsExt(poly.BoundingBox) == OverlapType.CONTAINED)
            //    return true;

            foreach (GridVector2 p in poly.ExteriorRing)
            {
                if (!this.Contains(p))
                    return false;
            }

            return false;
        }

        public bool Contains(in GridLineSegment line)
        {
            if (!this.Contains(line.A))
                return false;

            if (!this.Contains(line.B))
                return false;


            return false;
        }

        public OverlapType ContainsExt(in GridLineSegment line)
        {
            OverlapType oA = this.ContainsExt(line.A);
            OverlapType oB = this.ContainsExt(line.B);

            if (oA == oB)
            {
                switch (oA)
                {
                    case OverlapType.NONE:
                        return OverlapType.NONE;
                    case OverlapType.CONTAINED:
                        return OverlapType.CONTAINED;
                    case OverlapType.TOUCHING:
                        return OverlapType.CONTAINED; //If both endpoints touch the edge of the circle the line is contained within 
                    default:
                        throw new ArgumentException("Unexpected ContainsExt Result for point in circle");
                }
            }
            else
            {
                if (oA == OverlapType.NONE || oB == OverlapType.NONE)
                {
                    var NotNoneResult = oA == OverlapType.NONE ? oB : oA;
                    //If it is touching the answer gets complicated.  We need to know if the outside endpoint is on the other side of the circle or not
                    if (NotNoneResult == OverlapType.CONTAINED)
                        return OverlapType.INTERSECTING;

                }
            }

            throw new NotImplementedException();
            /*
            if (!this.Contains(line.A))
                return false;

            if (!this.Contains(line.B))
                return false;


            return false;
            */
        }

        /// <summary>
        /// Returns true if the shape is entirely inside the circle
        /// </summary>
        /// <param name="shape"></param>
        /// <returns></returns>
        public bool Contains(in IShape2D shape)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// True if the circle intersects the circle with center c and radius r
        /// </summary>
        /// <param name="c"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public bool Intersects(in GridVector2 p, double radius)
        {

            double XDist = p.X - this.Center.X;
            double YDist = p.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;
            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ICircle2D c)
        {
            return this.Intersects(c.Convert());
        }

        public bool Intersects(in GridCircle c)
        {
            double XDist = c.Center.X - this.Center.X;
            double YDist = c.Center.Y - this.Center.Y;
            double CombinedRadiusSquared = this.Radius + c.Radius;
            CombinedRadiusSquared *= CombinedRadiusSquared;

            return (XDist * XDist) + (YDist * YDist) <= CombinedRadiusSquared;
        }

        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in GridLineSegment line) => CircleIntersectionExtensions.Intersects(in this, in line);

        public bool Intersects(in ITriangle2D t) => this.Intersects(t.Convert());

        public bool Intersects(in GridTriangle tri) => CircleIntersectionExtensions.Intersects(in this, in tri);

        public bool Intersects(in IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(in poly);
        }

        public bool Intersects(in GridPolygon poly)
        {
            return CircleIntersectionExtensions.Intersects(in this, in poly);
        }

        public bool Intersects(in IRectangle r)
        {
            GridRectangle rect = r.Convert();
            return this.Intersects(in rect);
        }

        public bool Intersects(in GridRectangle rect)
        {
            return CircleIntersectionExtensions.Intersects(in this, in rect);
        }

        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero if anywhere inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public double Distance(in GridVector2 position)
        {
            double Distance = GridVector2.Distance(position, this.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }


        public override bool Equals(object obj)
        {
            if (obj is GridCircle other)
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

        public bool Equals(ICircle2D other)
        {
            return this.Center.Equals(other.Center) && this.Radius.Equals(other.Radius);
        }


        public override int GetHashCode()
        {
            throw new InvalidOperationException($"It is not mathematically possible to implement {nameof(GetHashCode)} for a point where equality is epsilon based");
            return 0;
            //return _HashCode;
            /*
            if (!_HashCode.HasValue)
            {
                _HashCode = Center.GetHashCode();
            }

            return _HashCode.Value;
            */
        }


        public bool Intersects(in IShape2D shape)
        {
            return ShapeExtensions.CircleIntersects(in this, in shape);
        }

        public IShape2D Translate(in IPoint2D offset)
        {
            return this.Translate(offset.Convert());
        }

        public GridCircle Translate(in GridVector2 offset)
        {
            return new GridCircle(this.Center + offset, this.Radius);
        }

        public static bool operator ==(in GridCircle A, in GridCircle B)
        {
            return ((A.Center == B.Center) &&
                   (A.Radius == B.Radius));
        }

        public static bool operator !=(in GridCircle A, in GridCircle B)
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

        public IPoint2D Centroid => Center;
    }
}
