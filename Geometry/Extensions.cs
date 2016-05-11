using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RTree;
using MathNet.Numerics.LinearAlgebra;

namespace Geometry
{
    public static class GeometryRTreeExtensions
    {
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
    }

    public static class GeometryMathNetNumerics
    {
        public static Matrix<double> ToMatrix(this GridVector2 point)
        {
            return (new GridVector2[] { point }).ToMatrix();
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
            List<GridVector2> listPoints = new List<GridVector2>(m.ColumnCount);
            foreach(Vector<double> col in m.EnumerateColumns())
            {
                listPoints.Add(new GridVector2(col[0], col[1]));
            }

            return listPoints;
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
