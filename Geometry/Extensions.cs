using MathNet.Numerics.LinearAlgebra;
using RTree;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Geometry
{
    public static class StackExtensions<T>
    {
        public static List<T> Peek(Stack<T> stack, int count)
        {
            List<T> items = new(count);
            using (Stack<T>.Enumerator path_enumerator = stack.GetEnumerator())
            {
                while (items.Count < count)
                {
                    if (false == path_enumerator.MoveNext())
                        break;

                    items.Add(path_enumerator.Current);
                }
            }

            return items;
        }
    }

    public static class GeometryRTreeExtensions
    {
        public static RTree.Point ToRTreePoint(this Vector2 p, double Z = 0) => new RTree.Point(p.X, p.Y, Z);

        public static RTree.Point ToRTreePoint(this Vector3 p) => new RTree.Point(p.Coords);

        public static RTree.Rectangle ToRTreeRect(this in Rectangle rect, double MinZ, double MaxZ) => new RTree.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, MinZ, MaxZ);

        public static RTree.Rectangle ToRTreeRect(this in Rectangle rect, double Z = 0) => new RTree.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, Z, Z);

        /// <summary>
        /// Converts to an RTree.Rectangle, but pads an epsilon value to the bounding box
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static RTree.Rectangle ToRTreeRectEpsilonPadded(this in Rectangle rect, double Z = 0) => new RTree.Rectangle(rect.Left - Global.Epsilon, rect.Bottom - Global.Epsilon, rect.Right + Global.Epsilon, rect.Top + Global.Epsilon, (double)Z, (double)Z);

        public static RTree.Rectangle ToRTreeRect(this Vector2 p, double Z) => new RTree.Rectangle(p.X, p.Y, p.X, p.Y, Z, Z);

        public static RTree.Rectangle ToRTreeRect(this Vector2 p, int Z) => new RTree.Rectangle(p.X, p.Y, p.X, p.Y, (double)Z, (double)Z);

        public static RTree.Rectangle ToRTreeRect(this IPoint2D p, int Z) => new RTree.Rectangle(p.X, p.Y, p.X, p.Y, (double)Z, (double)Z);

        public static RTree.Rectangle ToRTreeRect(this Box bbox)
        {
            return new RTree.Rectangle(bbox.MinVals,
                                       bbox.MaxVals);
        }

        public static RTree.RTree<LineSegment> ToRTree(this IEnumerable<LineSegment> lines)
        {
            RTree.RTree<LineSegment> rTree = new();
            foreach (LineSegment l in lines)
            {
                rTree.Add(l.BoundingBox.ToRTreeRect(0), l);
            }

            return rTree;
        }

    }

    public static class GeometryMathNetNumerics
    {
        public static Matrix<double> ToMatrix(this Vector2 point) => (new Vector2[] { point }).ToMatrix();

        public static Matrix<double> ToMatrix(this Vector3 point) => (new Vector3[] { point }).ToMatrix();

        public static Vector<double> ToVector(this Vector2 point) => Vector<double>.Build.Dense([point.X, point.Y, 0]);

        public static Vector<double> ToVector(this Vector3 point) => Vector<double>.Build.Dense([point.X, point.Y, point.Z]);

        public static Matrix<double> ToMatrix(this ICollection<Vector2> points) => Matrix<double>.Build.DenseOfColumns(points.Select(p => new double[] { p.X, p.Y, 0, 1 }));

        public static Matrix<double> ToMatrix(this ICollection<Vector3> points) => Matrix<double>.Build.DenseOfColumns(points.Select(p => new double[] { p.X, p.Y, p.Z, 1 }));

        public static Vector2 ToVector2(this Vector<double> m) => new Vector2(m[0], m[1]);

        public static Vector3 ToVector3(this Vector<double> m) => new Vector3(m[0], m[1], m[2]);

        public static Vector2[] ToVector2(this Matrix<double> m)
        {
            Vector2[] points = new Vector2[m.ColumnCount];
            int icol = 0;
            foreach (Vector<double> col in m.EnumerateColumns())
            {
                points[icol] = new Vector2(col[0], col[1]);
                icol++;
            }

            return points;
        }

        public static Vector3[] ToVector3(this Matrix<double> m)
        {
            Vector3[] points = new Vector3[m.ColumnCount];
            int icol = 0;
            foreach (Vector<double> col in m.EnumerateColumns())
            {
                points[icol] = new Vector3(col[0], col[1], col[2]);
                icol++;
            }

            return points;
        }

        public static Matrix<double> CreateTranslationMatrix(this Vector2 p)
        {
            double[,] translation = {{1, 0, 0, p.X },
                                     {0, 1, 0, p.Y },
                                     {0, 0, 1, 0   },
                                     {0, 0, 0, 1   } };

            return Matrix<double>.Build.DenseOfArray(translation);
        }

        public static Matrix<double> CreateTranslationMatrix(this Vector3 p)
        {
            double[,] translation = {{1, 0, 0, p.X },
                                     {0, 1, 0, p.Y },
                                     {0, 0, 1, p.Z },
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
            Vector<double> v = Vector<double>.Build.Dense([X, Y, Z]);
            return CreateScaleMatrix(v);
        }

        public static Matrix<double> CreateScaleMatrix(this Vector<double> scalars)
        {
            if (scalars.Count == 2)
            {
                scalars = Vector<double>.Build.Dense([scalars[0], scalars[1], 1.0]);
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

        public static Vector2[] Transform(this ICollection<Vector2> points, Matrix<double> matrix)
        {
            Matrix<double> pointMatrix = points.ToMatrix();
            Matrix<double> transformedPoints = matrix * pointMatrix;
            return transformedPoints.ToVector2();
        }
    }

    public static class VectorExtensions
    {
        public static QuadTreeWithUniqueValues<TElement> ToQuadTree<TSource, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, Vector2> keySelector,
            Func<TSource, TElement> elementSelector)
        {
            var items = source.Select(item => new { Key = keySelector(item), Item = elementSelector(item) }).ToArray();
            var bbox = items.Select(item => item.Key).BoundingBox();
            QuadTreeWithUniqueValues<TElement> output = new(bbox * 1.5);
            foreach (var item in items)
            {
                output.Add(item.Key, item.Item);
            }

            return output;
        }

        public static QuadTreeWithUniqueValues<TSource> ToQuadTree<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Vector2> keySelector)
        {
            var items = source.Select(item => new { Key = keySelector(item), Item = item }).ToArray();
            var bbox = items.Select(item => item.Key).BoundingBox();
            QuadTreeWithUniqueValues<TSource> output = new(bbox * 1.5);
            foreach (var item in items)
            {
                output.Add(item.Key, item.Item);
            }

            return output;
        }
    }

    public static class MappingVector2SetExtensions
    {
        public static bool SetEquals(this IReadOnlyList<MappingVector2> A, IReadOnlyList<MappingVector2> B)
        {
            System.Collections.Immutable.ImmutableSortedSet<MappingVector2> sortedA = A.ToImmutableSortedSet(new MappingVector2Comparer());
            System.Collections.Immutable.ImmutableSortedSet<MappingVector2> sortedB = B.ToImmutableSortedSet(new MappingVector2Comparer());
            return sortedA.SetEquals(sortedB);
        }
    }

    public static class MappingVector2Extensions
    {
        public static Rectangle ControlBounds(this MappingVector2[] mapPoints)
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

            return new Rectangle(minX, maxX, minY, maxY);
        }


        public static Rectangle MappedBounds(this MappingVector2[] mapPoints)
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

            return new Rectangle(minX, maxX, minY, maxY);
        }
    }
}
