using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RTree;
using MathNet.Numerics.LinearAlgebra;

namespace Geometry
{
    public static class ArrayToStringExtensions
    {
        public static string ToCSV(this double[] array, char delimiter = ',', string format = "F2")
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < array.Count(); i++)
            {
                sb.Append(array[i].ToString(format));
                if (i < array.Count() - 1)
                    sb.Append(",");
            }

            return sb.ToString();
        }

        public static string ToMatlab(this double[] array, string format = "F2")
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            sb.Append(array.ToCSV(' '));
            sb.Append("]");

            return sb.ToString();
        }
    }

    public static class GeometryRTreeExtensions
    {
        public static RTree.Point ToRTreePoint(this GridVector2 p, float Z)
        {
            return new RTree.Point((float)p.X, (float)p.Y, Z);
        }

        public static RTree.Point ToRTreePoint(this GridVector3 p)
        {
            return new RTree.Point((float)p.coords[0],
                                   (float)p.coords[1],
                                   (float)p.coords[2]);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float MinZ, float MaxZ)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, MinZ, MaxZ);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridBox bbox)
        {
            return new RTree.Rectangle(bbox.minVals, 
                                       bbox.maxVals);
        }

    }

    public static class GeometryMathNetNumerics
    {
        public static Matrix<double> ToMatrix(this GridVector2 point)
        {
            return (new GridVector2[] { point }).ToMatrix();
        }

        public static Vector<double> ToVector(this GridVector2 point)
        {
            return Vector<double>.Build.Dense(new double[] { point.X, point.Y, 0 });
        }

        public static Matrix<double> ToMatrix(this ICollection<GridVector2> points)
        { 
            return Matrix<double>.Build.DenseOfColumns(points.Select(p => new double[] { p.X, p.Y, 0, 1 }));
        }

        public static GridVector2 ToGridVector2(this Vector<double> m)
        {
            return new GridVector2(m[0], m[1]);
        }

        public static ICollection<GridVector2> ToGridVector2(this Matrix<double> m)
        {
            GridVector2[] points = new GridVector2[m.ColumnCount];
            int icol = 0; 
            foreach(Vector<double> col in m.EnumerateColumns())
            {
                points[icol] = new GridVector2(col[0], col[1]);
                icol++; 
            }

            return points;
        }

        public static Matrix<double> CreateTranslationMatrix(this GridVector2 p)
        {
            double[,] translation = {{1, 0, 0, p.X },
                                     {0, 1, 0, p.Y },
                                     {0, 0, 1, 0   },
                                     {0, 0, 0, 1   } };

            return Matrix<double>.Build.DenseOfArray(translation);
        }

        public static Matrix<double> CreateRotationMatrix(this double angle)
        {
            double[,] rotation = {{ Math.Cos(angle), -Math.Sin(angle), 0, 0 },
                                  { Math.Sin(angle),  Math.Cos(angle), 0, 0 },
                                  {0, 0, 1, 0},
                                  {0, 0, 0, 1} };
            Matrix<double> rotationMatrix = Matrix<double>.Build.DenseOfArray(rotation);
            return rotationMatrix;
        }

        public static Matrix<double> CreateScaleMatrix(double X, double Y, double Z)
        {
            Vector<double> v = Vector<double>.Build.Dense(new double[] { X, Y, Z });
            return CreateScaleMatrix(v);
        }

        public static Matrix<double> CreateScaleMatrix(this Vector<double> scalars)
        {
            if(scalars.Count == 2)
            {
                scalars = Vector<double>.Build.Dense(new double[] { scalars[0], scalars[1], 1.0 });
            }

            if (scalars.Count != 3)
                throw new ArgumentException("Expected 3D vector of scalar values");

            double[,] m = {{ scalars[0], 0,          0,          0 },
                                  { 0,          scalars[1], 0,          0 },
                                  {0,           0,          scalars[2], 0},
                                  {0,           0,          0,          1} };
            Matrix<double> scaleMatrix = Matrix<double>.Build.DenseOfArray(m);
            return scaleMatrix;
        }


        public static ICollection<GridVector2> Rotate(this ICollection<GridVector2> points, double angle, GridVector2 centerOfRotation)
        {
            Matrix<double> pointMatrix = points.ToMatrix();

            Matrix<double> rotationMatrix = angle.CreateRotationMatrix();

            Matrix<double> translationMatrix = (-centerOfRotation).CreateTranslationMatrix();
            Matrix<double> inverseTranslationMatrix = (centerOfRotation).CreateTranslationMatrix();

            Matrix<double> translatedPoints = translationMatrix * pointMatrix;
            Matrix<double> rotatedPoints = rotationMatrix * translatedPoints;
            Matrix<double> finalPoints = inverseTranslationMatrix * rotatedPoints;

            ICollection<GridVector2> results = finalPoints.ToGridVector2();
            return results;
        }

        /// <summary>
        /// Scale distance of points from a centerpoint by a scalar value
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scale"></param>
        /// <param name="centerOfScale"></param>
        /// <returns></returns>
        public static ICollection<GridVector2> Scale(this ICollection<GridVector2> points, double scale, GridVector2 origin)
        {
            Matrix<double> pointMatrix = points.ToMatrix();

            Matrix<double> scaleMatrix = CreateScaleMatrix(scale,scale,1);

            Matrix<double> translationMatrix = (-origin).CreateTranslationMatrix();
            Matrix<double> inverseTranslationMatrix = (origin).CreateTranslationMatrix();

            Matrix<double> translatedPoints = translationMatrix * pointMatrix;
            Matrix<double> rotatedPoints = scaleMatrix * translatedPoints;
            Matrix<double> finalPoints = inverseTranslationMatrix * rotatedPoints;

            ICollection<GridVector2> results = finalPoints.ToGridVector2();
            return results;
        }

        public static ICollection<GridVector2> Translate(this ICollection<GridVector2> points, GridVector2 offset)
        {
            Matrix<double> pointMatrix = points.ToMatrix();  
            Matrix<double> translationMatrix = offset.CreateTranslationMatrix(); 

            Matrix<double> translatedPoints = translationMatrix * pointMatrix; 

            ICollection<GridVector2> results = translatedPoints.ToGridVector2();
            return results;
        } 
    }
    
    public static class GridPoint2Extensions
    {
        public static GridVector2 Centroid(this ICollection<GridVector2> points)
        {
            double mX = 0;
            double mY = 0;
            foreach(GridVector2 p in points)
            {
                mX += p.X;
                mY += p.Y;
            }

            return new GridVector2(mX / (double)points.Count, mY / (double)points.Count);
        }

        /// <summary>
        /// Return the index of a point in the array we know is on the convex hull
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static int FindPointOnConvexHull(this GridVector2[] points)
        {
            double minX = points.Min(p => p.X);
            double minY = points.Where(p => p.X == minX).Min(p => p.Y);

            for(int i = 0; i < points.Length; i++)
            {
                if(points[i].X == minX && points[i].Y == minY)
                {
                    return i;
                }
            }

            throw new ArgumentException("Could not find point on convex hull!");
        }

        /// <summary>
        /// Return true if the points are placed in clockwise order.  Assumes points do not cross over themselves
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool AreClockwise(this GridVector2[] points)
        {
            if (points.Length < 3)
                throw new ArgumentException("Insufficient points to determine AreClockwise()");

            //We need to make sure our center vertex is on the convex hull

            int iConvexHullPoint = FindPointOnConvexHull(points);
            int iBefore = iConvexHullPoint - 1 > 0 ? iConvexHullPoint - 1 : points.Length - 1;
            int iAfter = iConvexHullPoint + 1 < points.Length ? iConvexHullPoint + 1 : 0;

            GridVector2 A = points[iBefore];
            GridVector2 B = points[iConvexHullPoint];
            GridVector2 C = points[iAfter]; 

            /*
            Matrix<double> m = Matrix<double>.Build.DenseOfArray(new double[,] { { 1, 1, 1},
                                                                                { U.X, U.Y, 0 },
                                                                                { V.X, V.Y, 0 } });
            */

            Matrix<double> m = Matrix<double>.Build.DenseOfArray(new double[,] { { 1, A.X, A.Y },
                                                                                { 1, B.X, B.Y},
                                                                                { 1, C.X, C.Y} });


            double det = m.Determinant();

            return det < 0;
        }

        public static GridRectangle BoundingBox(this GridVector2[] points)
        {
            if(points == null)
                throw new ArgumentNullException("points");

            if (points.Length == 0)
                throw new ArgumentException("GridRectangle Border is empty", "points"); 

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                minX = Math.Min(minX, points[i].X);
                maxX = Math.Max(maxX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxY = Math.Max(maxY, points[i].Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY); 
        }

        public static double MinDistanceBetweenPoints(this GridVector2[] points)
        {
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    if(points[i] != points[j])
                        minVal = Math.Min(minVal, GridVector2.Distance(points[i], points[j]));
                }
            }

            return minVal;
        }

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        static public GridVector2? IntersectionPoint(this ICollection<GridVector2> Verticies, GridLineSegment testSeg)
        { 
            GridLineSegment[] segments = GridLineSegment.SegmentsFromPoints(Verticies.ToArray());
            return segments.IntersectionPoint(testSeg);
        }
    }

    public static class GridLineSegmentExtensions
    {

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        static public GridVector2? IntersectionPoint(this ICollection<GridLineSegment> segments, GridLineSegment testSeg)
        {
            GridVector2 intersection;
             
            foreach (GridLineSegment existingLine in segments)
            {
                if (existingLine.Intersects(testSeg, out intersection))
                {
                    return new GridVector2?(intersection);
                }
            }

            return new GridVector2?();
        }
    }

    public static class MappingGridVector2Extensions
    {
        public static GridRectangle ControlBounds(this MappingGridVector2[] mapPoints)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < mapPoints.Length; i++)
            {
                minX = Math.Min(minX, mapPoints[i].ControlPoint.X);

                maxX = Math.Max(maxX, mapPoints[i].ControlPoint.X);

                minY = Math.Min(minY, mapPoints[i].ControlPoint.Y);

                maxY = Math.Max(maxY, mapPoints[i].ControlPoint.Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }


        public static GridRectangle MappedBounds(this MappingGridVector2[] mapPoints)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //   Debug.Assert(mapPoints.Length > 0); 

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < mapPoints.Length; i++)
            {
                minX = Math.Min(minX, mapPoints[i].MappedPoint.X);
                maxX = Math.Max(maxX, mapPoints[i].MappedPoint.X);
                minY = Math.Min(minY, mapPoints[i].MappedPoint.Y);
                maxY = Math.Max(maxY, mapPoints[i].MappedPoint.Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        } 
    }

    public static class MathHelpers
    {
        /// <summary>
        /// Given a scalar value from 0 to 1, return the linearly interpolated value between min/max.
        /// </summary>
        /// <param name="Fraction"></param>
        /// <param name="MinVal"></param>
        /// <param name="MaxVal"></param>
        /// <returns></returns>
        public static double Interpolate(this double Fraction, double MinVal, double MaxVal)
        {
            if (Fraction < 0)
                return MinVal;
            if (Fraction > 1)
                return MaxVal;

            return ((MaxVal - MinVal) * Fraction) + MinVal;
        }
    }

}
