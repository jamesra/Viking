using MathNet.Numerics.LinearAlgebra;
using RTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public static class StackExtensions<T>
    {
        public static List<T> Peek(Stack<T> stack, int count)
        {
            List<T> items = new List<T>(count);
            Stack<T>.Enumerator path_enumerator = stack.GetEnumerator();
            while (items.Count < count)
            {
                if (false == path_enumerator.MoveNext())
                    break;

                items.Add(path_enumerator.Current);
            }

            return items;
        }
    }

    public static class SortingExtensions
    {
        /// sort array 'rg', returning the original index positions
        public static int[] SortAndIndex<T>(this T[] rg, IComparer<T> comparer = null)
        {
            int i, c = rg.Length;
            var keys = new int[c];
            if (c > 1)
            {
                for (i = 0; i < c; i++)
                    keys[i] = i;

                if (comparer == null)
                {
                    System.Array.Sort(rg, keys /*, ... */);
                }
                else
                {
                    System.Array.Sort<T, int>(rg, keys, comparer);
                }
            }
            return keys;
        }

        /*
        /// sort array 'rg', returning the original index positions
        /// TODO: Need to sort RG after finding the sorted indicies. 
        public static int[] SortAndIndex<T>(this IEnumerable<T> rg, IComparer<T> comparer = null)
        {
            return rg.ToArray().SortAndIndex(comparer);
        }
        */

        /// <summary>
        /// Returns index of the item in the collection.  Returns -1 if the item is not in the collection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rg"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> rg, T value) where T : IEquatable<T>
        {
            int i = 0;
            foreach (T item in rg)
            {
                if (item.Equals(value))
                    return i;
                i += 1;
            }

            return -1;
        }
    }

    public static class ArrayToStringExtensions
    {
        public static string ToCSV(this double[] array, string delimiter = ", ", string format = "F2")
        {
            return string.Join(delimiter, array.Select(v => v.ToString(format)));
            /*
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < array.Count(); i++)
            {
                sb.Append(array[i].ToString(format));
                if (i < array.Count() - 1)
                    sb.Append(",");
            }

            return sb.ToString();
            */
        }

        public static string ToMatlab(this double[] array, string format = "F2")
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            sb.Append(array.ToCSV(" "));
            sb.Append("]");

            return sb.ToString();
        }
    }

    public static class GeometryRTreeExtensions
    {
        public static RTree.Point ToRTreePoint(this GridVector2 p, double Z = 0)
        {
            return new RTree.Point(p.X, p.Y, Z);
        }

        public static RTree.Point ToRTreePoint(this GridVector3 p)
        {
            return new RTree.Point(p.coords);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, double MinZ, double MaxZ)
        {
            return new RTree.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, MinZ, MaxZ);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, double Z = 0)
        {
            return new RTree.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, Z, Z);
        }

        /// <summary>
        /// Converts to an RTree.Rectangle, but pads an epsilon value to the bounding box
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static RTree.Rectangle ToRTreeRectEpsilonPadded(this GridRectangle rect, double Z = 0)
        {
            return new RTree.Rectangle(rect.Left - Global.Epsilon, rect.Bottom - Global.Epsilon, rect.Right + Global.Epsilon, rect.Top + Global.Epsilon, (double)Z, (double)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, double Z)
        {
            return new RTree.Rectangle(p.X, p.Y, p.X, p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle(p.X, p.Y, p.X, p.Y, (double)Z, (double)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this IPoint2D p, int Z)
        {
            return new RTree.Rectangle(p.X, p.Y, p.X, p.Y, (double)Z, (double)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridBox bbox)
        {
            return new RTree.Rectangle(bbox.minVals,
                                       bbox.maxVals);
        }

        public static RTree.RTree<GridLineSegment> ToRTree(this IEnumerable<GridLineSegment> lines)
        {
            RTree.RTree<GridLineSegment> rTree = new RTree<Geometry.GridLineSegment>();
            foreach (GridLineSegment l in lines)
            {
                rTree.Add(l.BoundingBox.ToRTreeRect(0), l);
            }

            return rTree;
        }

    }

    public static class GeometryMathNetNumerics
    {
        public static Matrix<double> ToMatrix(this GridVector2 point)
        {
            return (new GridVector2[] { point }).ToMatrix();
        }

        public static Matrix<double> ToMatrix(this GridVector3 point)
        {
            return (new GridVector3[] { point }).ToMatrix();
        }

        public static Vector<double> ToVector(this GridVector2 point)
        {
            return Vector<double>.Build.Dense(new double[] { point.X, point.Y, 0 });
        }

        public static Vector<double> ToVector(this GridVector3 point)
        {
            return Vector<double>.Build.Dense(new double[] { point.X, point.Y, point.Z });
        }

        public static Matrix<double> ToMatrix(this ICollection<GridVector2> points)
        {
            return Matrix<double>.Build.DenseOfColumns(points.Select(p => new double[] { p.X, p.Y, 0, 1 }));
        }

        public static Matrix<double> ToMatrix(this ICollection<GridVector3> points)
        {
            return Matrix<double>.Build.DenseOfColumns(points.Select(p => new double[] { p.X, p.Y, p.Z, 1 }));
        }

        public static GridVector2 ToGridVector2(this Vector<double> m)
        {
            return new GridVector2(m[0], m[1]);
        }

        public static GridVector3 ToGridVector3(this Vector<double> m)
        {
            return new GridVector3(m[0], m[1], m[2]);
        }

        public static GridVector2[] ToGridVector2(this Matrix<double> m)
        {
            GridVector2[] points = new GridVector2[m.ColumnCount];
            int icol = 0;
            foreach (Vector<double> col in m.EnumerateColumns())
            {
                points[icol] = new GridVector2(col[0], col[1]);
                icol++;
            }

            return points;
        }

        public static GridVector3[] ToGridVector3(this Matrix<double> m)
        {
            GridVector3[] points = new GridVector3[m.ColumnCount];
            int icol = 0;
            foreach (Vector<double> col in m.EnumerateColumns())
            {
                points[icol] = new GridVector3(col[0], col[1], col[2]);
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

        public static Matrix<double> CreateTranslationMatrix(this GridVector3 p)
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
            Vector<double> v = Vector<double>.Build.Dense(new double[] { X, Y, Z });
            return CreateScaleMatrix(v);
        }

        public static Matrix<double> CreateScaleMatrix(this Vector<double> scalars)
        {
            if (scalars.Count == 2)
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


        public static GridVector2[] Rotate(this ICollection<GridVector2> points, double angle, GridVector2 centerOfRotation)
        {
            Matrix<double> pointMatrix = points.ToMatrix();

            Matrix<double> rotationMatrix = angle.CreateRotationMatrix();

            Matrix<double> translationMatrix = (-centerOfRotation).CreateTranslationMatrix();
            Matrix<double> inverseTranslationMatrix = (centerOfRotation).CreateTranslationMatrix();

            Matrix<double> translatedPoints = translationMatrix * pointMatrix;
            Matrix<double> rotatedPoints = rotationMatrix * translatedPoints;
            Matrix<double> finalPoints = inverseTranslationMatrix * rotatedPoints;

            return finalPoints.ToGridVector2();
        }

        /// <summary>
        /// Scale distance of points from a centerpoint by a scalar value
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scale"></param>
        /// <param name="centerOfScale"></param>
        /// <returns></returns>
        public static GridVector2[] Scale(this ICollection<GridVector2> points, double scale, GridVector2 origin)
        {
            return points.Scale(new GridVector2(scale, scale), origin);
        }

        /// <summary>
        /// Scale distance of points from a centerpoint by a scalar value
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scale"></param>
        /// <param name="centerOfScale"></param>
        /// <returns></returns>
        public static GridVector2[] Scale(this ICollection<GridVector2> points, GridVector2 scale, GridVector2 origin)
        {
            Matrix<double> pointMatrix = points.ToMatrix();

            Matrix<double> scaleMatrix = CreateScaleMatrix(scale.X, scale.Y, 1);

            Matrix<double> translationMatrix = (-origin).CreateTranslationMatrix();
            Matrix<double> inverseTranslationMatrix = (origin).CreateTranslationMatrix();

            Matrix<double> translatedPoints = translationMatrix * pointMatrix;
            Matrix<double> rotatedPoints = scaleMatrix * translatedPoints;
            Matrix<double> finalPoints = inverseTranslationMatrix * rotatedPoints;

            return finalPoints.ToGridVector2();
        }

        public static GridVector2[] Translate(this ICollection<GridVector2> points, GridVector2 offset)
        {
            Matrix<double> pointMatrix = points.ToMatrix();
            Matrix<double> translationMatrix = offset.CreateTranslationMatrix();

            Matrix<double> translatedPoints = translationMatrix * pointMatrix;

            return translatedPoints.ToGridVector2();
        }

        public static GridVector2[] Transform(this ICollection<GridVector2> points, Matrix<double> matrix)
        {
            Matrix<double> pointMatrix = points.ToMatrix();
            Matrix<double> transformedPoints = matrix * pointMatrix;
            return transformedPoints.ToGridVector2();
        }
    }

    public static class IPoint2DExtensions
    {
        public static GridVector2 Round(this IPoint2D p, int precision)
        {
            return new GridVector2(Math.Round(p.X, precision), Math.Round(p.Y, precision));
        }

        public static GridVector2 ToGridVector2(this IPoint2D p)
        {
            return new GridVector2(p.X, p.Y);
        }
    }

    public static class GridVector2Extensions
    {
        /// <summary>
        /// If the first and last elements are not the same we add an element at the end equal to the first elements value
        /// This is because Polygons and several algorithms expect arrays to be closed loops of points.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static ICollection<int> EnsureClosedRing(this ICollection<int> points)
        {
            if (points.First() != points.Last())
            {
                List<int> newPoints = new List<int>(points);
                newPoints.Add(points.First());
                return newPoints;
            }

            return points;
        }

        /// <summary>
        /// If the first and last elements are not the same we add an element at the end equal to the first elements value
        /// This is because Polygons and several algorithms expect arrays to be closed loops of points.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static ICollection<GridVector2> EnsureClosedRing(this ICollection<GridVector2> points)
        {
            if (points.First() != points.Last())
            {
                List<GridVector2> newPoints = new List<Geometry.GridVector2>(points);
                newPoints.Add(points.First());
                return newPoints;
            }

            return points;
        }

        /// <summary>
        /// If the first and last elements are not the same we add an element at the end equal to the first elements value
        /// This is because Polygons and several algorithms expect arrays to be closed loops of points.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] EnsureClosedRing(this GridVector2[] points)
        {
            if (points.First() != points.Last())
            {
                GridVector2[] newPoints = new GridVector2[points.Length + 1];
                Array.Copy(points, newPoints, points.Length);
                newPoints[points.Length] = points[0];
                return newPoints;
            }

            return points;
        }

        /// <summary>
        /// If the first and last elements are not the same we add an element at the end equal to the first elements value
        /// This is because Polygons and several algorithms expect arrays to be closed loops of points and other expect open
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static ICollection<GridVector2> EnsureOpenRing(this ICollection<GridVector2> points)
        {
            if (points.Count < 2)
                return points.ToList();

            if (points.First() == points.Last())
            {
                List<GridVector2> newPoints = new List<Geometry.GridVector2>(points);
                newPoints.RemoveAt(newPoints.Count - 1);
                return newPoints;
            }

            return points;
        }

        /// <summary>
        /// If the first and last elements are not the same we remove the last element
        /// This is because Polygons and several algorithms expect arrays to be closed loops of points and other expect open
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] EnsureOpenRing(this GridVector2[] points)
        {
            if (points.Length < 2)
                return points.ToArray();

            if (points.First() == points.Last())
            {
                GridVector2[] newPoints = new GridVector2[points.Length - 1];
                Array.Copy(points, newPoints, points.Length - 1);
                return newPoints;
            }

            return points;
        }

        public static GridVector2 Average(this ICollection<GridVector2> points)
        {
            double mX = 0;
            double mY = 0;

            foreach (GridVector2 p in points)
            {
                mX += p.X;
                mY += p.Y;
            }

            //In case we are passed a closed loop of points we should remove the duplicate
            if (points.First() == points.Last())
            {
                mX -= points.First().X;
                mY -= points.First().Y;
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

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].X == minX && points[i].Y == minY)
                {
                    return i;
                }
            }

            throw new ArgumentException("Could not find point on convex hull!");
        }



        /// <summary>
        /// Return true if the first and last point in the set are the same
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool IsValidClosedRing(this ICollection<GridVector2> points)
        {
            //Need at least three points to be a ring
            if (points.Count < 3)
            {
                //throw new ArgumentException("Must have three points to be a ring");
                return false;
            }

            //Check for consecutive identical points
            for (int iPoint = 0; iPoint < points.Count - 1; iPoint++)
            {
                if (points.ElementAt(iPoint) == points.ElementAt(iPoint + 1))
                {
                    //throw new ArgumentException("Adjacent points should not be identical");
                    return false;
                }
            }

            return points.First() == points.Last();
        }

        /// <summary>
        /// Return true if the points are placed in clockwise order.  Assumes points do not cross over themselves. 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool AreClockwise(this GridVector2[] points)
        {
            return points.Winding() == RotationDirection.CLOCKWISE;
        }

        /// <summary>
        /// Return true if the points are placed in clockwise order.  Assumes points do not cross over themselves. 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static RotationDirection Winding(this GridVector2[] points)
        {
            if (points.Length <= 2)
                return RotationDirection.COLINEAR;

            double area = points.PolygonArea();
            RotationDirection result = area == 0 ? RotationDirection.COLINEAR :
                   area < 0 ? RotationDirection.CLOCKWISE : RotationDirection.COUNTERCLOCKWISE;
            /*
#if DEBUG
            if(points.Length == 3)
            {
                RotationDirection TriResult = GridTriangle.GetWinding(points);
                System.Diagnostics.Debug.Assert(result == TriResult);
            }
#endif
*/
            return result;
        }

        /// <summary>
        /// Create line segments between adjacent points in the collection
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridLineSegment[] ToLineSegments(this ICollection<GridVector2> points)
        {
            if (points == null)
                return null;

            if (points.Count <= 1)
                throw new ArgumentException("Must have two points to create line segments");

            GridLineSegment[] segments = new GridLineSegment[points.Count - 1];
            for (int iPoint = 0; iPoint < points.Count - 1; iPoint++)
            {
                segments[iPoint] = new GridLineSegment(points.ElementAt(iPoint), points.ElementAt(iPoint + 1));
            }

            return segments;
        }

        /// <summary>
        /// Create a polyline from points in the collection
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridPolyline ToPolyline(this ICollection<GridVector2> points, bool AllowSelfIntersection = false)
        {
            if (points == null)
                return null;

            if (points.Count <= 1)
                throw new ArgumentException("Must have two points to create line segments");

            GridPolyline polyline = new GridPolyline(points, AllowSelfIntersection);
            return polyline;
        }

        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] RemoveAdjacentDuplicates(this IReadOnlyList<GridVector2> points)
        {
            List<GridVector2> nonDuplicatePoints = new List<GridVector2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i] != points[i + 1])
                {
                    nonDuplicatePoints.Add(points[i]);
                }
            }

            nonDuplicatePoints.Add(points.Last());

            //                System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints.ToArray();
        }

        /// <summary>
        /// Remove all of the duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] RemoveDuplicates(this IReadOnlyList<GridVector2> points)
        {
            List<GridVector2> nonDuplicatePoints = new List<GridVector2>();
            for (int i = 0; i < points.Count; i++)
            {
                if (false == nonDuplicatePoints.Contains(points[i]))
                    nonDuplicatePoints.Add(points[i]);
            }


            //                System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints.ToArray();
        }

        /*
        /// <summary>
        /// Return true if the points are placed in clockwise order.  Assumes points do not cross over themselves.
        /// This original implementation only works for convex polygons
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

            Matrix<double> m = Matrix<double>.Build.DenseOfArray(new double[,] { { 1, A.X, A.Y },
                                                                                { 1, B.X, B.Y},
                                                                                { 1, C.X, C.Y} });


            double det = m.Determinant();

            return det < 0;
        }
        */

        /// <summary>
        /// The area of a polygon perimeter described by an array of points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double PolygonArea(this GridVector2[] points)
        {
            //System.Diagnostics.Debug.Assert(points.First() == points.Last(), "First and last point must be identical to determine area of polygon");
            points = points.EnsureClosedRing();

            //Ensure the points do not have large values.
            GridVector2 avg = points.Average();
            points = points.Translate(-avg);

            double accumulator = 0;

            for (int i = 0; i < points.Length - 1; i++)
            {
                GridVector2 p0 = points[i];
                GridVector2 p1 = points[i + 1];
                accumulator += ((p0.X * p1.Y) - (p1.X * p0.Y));
            }

            return accumulator / 2.0;
        }

        public static GridRectangle BoundingBox(this GridVector2[] points)
        {
            if (points == null)
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

        public static GridRectangle BoundingBox(this IEnumerable<GridVector2> points)
        {
            if (points == null)
                throw new ArgumentNullException("points");

            if (points.Any() == false)
                throw new ArgumentException("GridRectangle Border is empty", "points");

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (GridVector2 p in points)
            {
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Given a set of points, return the closest distance between any two points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double MinDistanceBetweenAnyPoints(this IReadOnlyList<GridVector2> points)
        {
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (points[i] != points[j])
                        minVal = Math.Min(minVal, GridVector2.Distance(points[i], points[j]));
                }
            }

            return minVal;
        }

        /// <summary>
        /// Given a set of points, return the closest distance between any two points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double MinDistanceBetweenSequentialPoints(this IReadOnlyList<GridVector2> points, out int FirstIndex)
        {
            FirstIndex = points.Count;
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i] != points[i + 1])
                {
                    minVal = Math.Min(minVal, GridVector2.Distance(points[i], points[i + 1]));
                    FirstIndex = i;
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
            return segments.IntersectionPoint(testSeg, false);
        }


        /// <summary>
        /// Returns the index and distance to the nearest point in an array, brute force
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="p"></param>
        /// <param name="MinDistance"></param>
        /// <returns></returns>
        static public int NearestPoint(this ICollection<GridVector2> points, GridVector2 testPoint, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            double[] distancesToRemovalPoint = points.Select(p => GridVector2.Distance(p, testPoint)).ToArray();
            double minDistance = distancesToRemovalPoint.Min();
            int iNearestPoint = distancesToRemovalPoint.TakeWhile(d => d != distancesToRemovalPoint.Min()).Count();
            MinDistance = minDistance;
            return iNearestPoint;
        }

        static public double PerimeterLength(this GridVector2[] points)
        {
            points = points.EnsureClosedRing();
            double length = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                length += GridVector2.Distance(points[i], points[i + 1]);
            }

            return length;
        }
    }

    public static class GridVector3Extensions
    {
        public static GridVector3 Centroid(this ICollection<GridVector3> points)
        {
            double mX = 0;
            double mY = 0;
            double mZ = 0;

            foreach (GridVector3 p in points)
            {
                mX += p.X;
                mY += p.Y;
                mZ += p.Z;
            }

            return new GridVector3(mX / (double)points.Count, mY / (double)points.Count, mZ / (double)points.Count);
        }

        public static GridBox BoundingBox(this IReadOnlyList<GridVector3> points)
        {
            if (points == null)
                throw new ArgumentNullException("points");

            if (points.Count == 0)
                throw new ArgumentException("GridRectangle Border is empty", "points");

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                minX = Math.Min(minX, points[i].X);
                maxX = Math.Max(maxX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxY = Math.Max(maxY, points[i].Y);
                minZ = Math.Min(minZ, points[i].Z);
                maxZ = Math.Max(maxZ, points[i].Z);
            }

            return new GridBox(new double[] { minX, minY, minZ },
                                new double[] { maxX, maxY, maxZ });
        }

        public static GridVector3 Average(this ICollection<GridVector3> points)
        {
            double mX = 0;
            double mY = 0;
            double mZ = 0;

            foreach (GridVector3 p in points)
            {
                mX += p.X;
                mY += p.Y;
                mZ += p.Z;
            }

            //In case we are passed a closed loop of points we should remove the duplicate
            if (points.First() == points.Last())
            {
                mX -= points.First().X;
                mY -= points.First().Y;
                mZ -= points.First().Z;
            }

            return new GridVector3(mX / (double)points.Count, mY / (double)points.Count, mZ / (double)points.Count);
        }

        public static GridVector2 XY(this GridVector3 point)
        {
            return new GridVector2(point.X, point.Y);
        }
    }

    public static class GridLineSegmentExtensions
    {

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="IgnoreEndpoints">Ignore line segments where the endpoints are identical</param>
        /// <returns></returns>
        static public GridVector2? IntersectionPoint(this ICollection<GridLineSegment> segments, GridLineSegment testSeg, bool IgnoreEndpoints)
        {
            GridLineSegment? intersectedSegment;
            return IntersectionPoint(segments, testSeg, IgnoreEndpoints, out intersectedSegment);
        }

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="IgnoreEndpoints">Ignore line segments where the endpoints are identical</param>
        /// <returns></returns>
        static public GridVector2? IntersectionPoint(this ICollection<GridLineSegment> segments, GridLineSegment testSeg, bool IgnoreEndpoints, out GridLineSegment? intersectedSegment)
        {
            GridVector2 intersection;
            intersectedSegment = new GridLineSegment?();

            if (IgnoreEndpoints)
            {
                segments = segments.Where(s => !s.SharedEndPoint(testSeg)).ToList();
            }

            foreach (GridLineSegment existingLine in segments)
            {
                if (existingLine.Intersects(testSeg, out intersection))
                {
                    intersectedSegment = existingLine;
                    return new GridVector2?(intersection);
                }
            }

            return new GridVector2?();
        }

        /// <summary>
        /// Returns the unique endpoints of all line segments in order
        /// </summary>
        /// <param name="segments"></param>
        /// <returns></returns>
        static public GridVector2[] Verticies(this ICollection<GridLineSegment> segments)
        {
            GridVector2[] verticies = new GridVector2[segments.Count + 1];
            for (int i = 0; i < segments.Count; i++)
            {
                verticies[i] = segments.ElementAt(i).A;
            }

            verticies[segments.Count] = segments.Last().B;

            return verticies;
        }

        /// <summary>
        /// Returns the index and distance to the nearest line segment in an array, brute force.
        /// In the case where the segments are a poly-line and p is an endpoint, the segment with segment.A == p is returned.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="p"></param>
        /// <param name="MinDistance"></param>
        /// <returns></returns>
        static public int NearestSegment(this ICollection<GridLineSegment> segments, GridVector2 p, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            int iNearestSegment = segments.TakeWhile(s => s.A != p).Count();
            if (iNearestSegment < segments.Count || segments.Count == 0)
            {
                MinDistance = 0;
                return iNearestSegment;
            }

            double[] distancesToNewPoint = segments.Select(l => l.DistanceToPoint(p)).ToArray();
            double minDistance = distancesToNewPoint.Min();

            iNearestSegment = distancesToNewPoint.TakeWhile(d => d != minDistance).Count();
            MinDistance = minDistance;
            return iNearestSegment;
        }

        static bool IsRing(this ICollection<GridLineSegment> segments)
        {
            return segments.First().A == segments.Last().B;
        }

        /// <summary>
        /// Include the new point in the grid line segment array.  Creates two new segments from (index-1, index) and (index, index + 1) and removes the segment between (index-1 and index) by creating a new segment between the new point and closest vertex in the existing segments.  Preserves order.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="newPoint"></param>
        /// <returns></returns>
        static public GridLineSegment[] Insert(this ICollection<GridLineSegment> lineSegs, GridVector2 newPointPosition, int segmentIndex)
        {
            GridVector2[] newControlPoints = new GridVector2[lineSegs.Count + 2];

            List<GridVector2> verts = lineSegs.Verticies().ToList();
            verts.Insert(segmentIndex + 1, newPointPosition);
            return verts.ToLineSegments();
        }

        /// <summary>
        /// Remove the grid line segment vertex at the index.  Create new a new line segment between the adjacent points remaining.
        /// </summary>
        /// <param name="lineSegs"></param>
        /// <param name="iNearestPoint"></param>
        /// <returns></returns>
        static public GridLineSegment[] Remove(this ICollection<GridLineSegment> lineSegs, int iNearestPoint)
        {
            GridVector2[] OriginalControlPoints = lineSegs.Verticies();
            GridVector2[] newControlPoints = new GridVector2[OriginalControlPoints.Length - 1];

            Array.Copy(OriginalControlPoints, newControlPoints, iNearestPoint);
            Array.Copy(OriginalControlPoints, iNearestPoint + 1, newControlPoints, iNearestPoint, newControlPoints.Length - iNearestPoint);
            /*for (int iOldPoint = 0; iOldPoint < iNearestPoint; iOldPoint++)
            {
                newControlPoints[iOldPoint] = OriginalControlPoints[iOldPoint];
            }
            
            for (int iOldPoint = iNearestPoint + 1; iOldPoint < OriginalControlPoints.Length; iOldPoint++)
            {
                newControlPoints[iOldPoint - 1] = OriginalControlPoints[iOldPoint];
            }
            */
            //The first point in a closed shape is equal to the last point.  If we remove the first point we must update the last point to match the new first point.
            if (lineSegs.IsRing() && iNearestPoint == 0)
            {
                newControlPoints[newControlPoints.Length - 1] = newControlPoints[0];
            }

            return newControlPoints.ToLineSegments();
        }

        /// <summary>
        /// Shorten the last segment in a collection to be 99% of the original length.  This is used to prevent false positives in self-intersection tests, often for closed rings
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridLineSegment[] ShortenLastVertex(this IReadOnlyList<GridLineSegment> src)
        {
            GridLineSegment[] dest = new GridLineSegment[src.Count];

            for (int i = 0; i < src.Count; i++)
            {
                dest[i] = src[i];
            }

            GridLineSegment lastSegment = src.Last();
            GridVector2 newEndpoint = lastSegment.PointAlongLine(0.99);
            dest[src.Count - 1] = new GridLineSegment(lastSegment.A, newEndpoint);

            return dest;
        }
    }

    public static class GridPolygonExtensions
    {
        public static void AddPointsAtAllIntersections(this GridPolygon[] polygons)
        {
            foreach (Combo<GridPolygon> combo in polygons.CombinationPairs())
            {
                var result = combo.A.AddPointsAtIntersections(combo.B);
                if(result.Any())
                    combo.B.AddPointsAtIntersections(combo.A);
            }
        }

        public static void AddPointsAtAllIntersections(this GridPolygon[] polygons, double[] polyZ)
        {
            if (polygons.Length != polyZ.Length)
            {
                throw new ArgumentException("polyZ must have same length as polygons");
            }

            foreach (Combo<GridPolygon> combo in polygons.CombinationPairs())
            {
                if (polyZ[combo.iA] == polyZ[combo.iB])
                    continue;

                var result = combo.A.AddPointsAtIntersections(combo.B);
                if(result.Any())
                    combo.B.AddPointsAtIntersections(combo.A);
            }
        }

        /// <summary>
        /// Returns the Polygon vertex which intersects the point, if any.  May return interior polygons
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param>
        /// <param name="ControlPointRadius"></param>
        /// <param name="intersectingPoly"></param>
        /// <returns></returns>
        public static bool PointIntersectsAnyPolygonVertex(this GridPolygon polygon, GridVector2 WorldPosition, double ControlPointRadius, out GridPolygon intersectingPoly)
        {
            //Quick check to see if it is possible for a vertex to intersect
            if (!PaddedPolygonContains(polygon, ControlPointRadius, WorldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                if (PointIntersectsAnyPolygonVertex(innerPoly, WorldPosition, ControlPointRadius, out intersectingPoly))
                {
                    return true;
                }
            }

            GridCircle testCircle = new GridCircle(WorldPosition, ControlPointRadius);
            if (polygon.ExteriorRing.Any(v => testCircle.Contains(v)))
            {
                intersectingPoly = polygon;
                return true;
            }

            intersectingPoly = null;
            return false;
        }

        /// <summary>
        /// Returns the Polygon segment which intersects the point, if any.  May return interior polygons
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param>
        /// <param name="LineWidth"></param>
        /// <param name="intersectingPoly"></param>
        /// <returns></returns>
        public static bool PointIntersectsAnyPolygonSegment(this GridPolygon polygon, GridVector2 WorldPosition, double LineWidth, out GridPolygon intersectingPoly)
        {
            //Quick check to see if it is possible for a segment to intersect
            if (!PaddedPolygonContains(polygon, LineWidth / 2.0f, WorldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                if (innerPoly.PointIntersectsAnyPolygonSegment(WorldPosition, LineWidth, out intersectingPoly))
                {
                    return true;
                }
            }

            double MinDistance;
            polygon.ExteriorSegments.NearestSegment(WorldPosition, out MinDistance);
            if (MinDistance < LineWidth / 2.0f)
            {
                intersectingPoly = polygon;
                return true;
            }

            intersectingPoly = null;
            return false;
        }




        /*
     /// <summary>
     /// Returns the Polygon segment which intersects the point, if any.  May return interior polygons
     /// </summary>
     /// <param name="polygon"></param>
     /// <param name="WorldPosition"></param>
     /// <param name="intersectingPoly"></param>
     /// <returns></returns>
     public static double NearestPolygonSegment(this GridPolygon polygon, GridVector2 WorldPosition, out GridPolygon nearestPoly)
     {
         nearestPoly = null;
         double nearestPolyDistance = double.MaxValue;

         foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
         {
             GridPolygon foundPolygon;
             double distance = innerPoly.NearestPolygonSegment(WorldPosition, out foundPolygon);
             if (distance < nearestPolyDistance)
             {
                 nearestPoly = innerPoly;
                 nearestPolyDistance = distance;
             }
         }

         double MinDistance;
         polygon.ExteriorSegments.NearestSegment(WorldPosition, out MinDistance);
         if (MinDistance < nearestPolyDistance)
         {
             nearestPoly = polygon;
             nearestPolyDistance = MinDistance;
         }

         return nearestPolyDistance;
     }
     */
        private static void AddIntersection(SortedDictionary<double, PolygonIndex> dict, double key, PolygonIndex index)
        {
            dict.Add(key, index);
            /*
            if(dict.ContainsKey(key))
            {
                throw new ArgumentException("Intersection dictionary already contains key: " + key.ToString());
            }

            dict[key] = index;*/

            /*if (!dict.ContainsKey(key))
            {
                dict.Add(key, new List<PointIndex>());
            }
            
            if (!dict[key].Contains(index))
            {
                dict[key].Add(index);
            }
            */
            return;
        }

        /// <summary>
        /// Returns point indicies of the segments of the polygon that intersect the line.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns>A dictionary of Polygon vertex indicies and a distance from that vertex.  </returns>
        public static SortedDictionary<double, PolygonIndex> IntersectingSegments(this GridPolygon polygon, GridLineSegment line)
        {
            SortedDictionary<double, PolygonIndex> output = new SortedDictionary<double, PolygonIndex>();

            PolygonIndex[] candidates = polygon.SegmentRTree.Intersects(line.BoundingBox).ToArray();

            //Due to epsilon factors a single line may intersect the same vertex twice when a line passes near the vertex.
            //We control this by keeping a list of verticies we've already added and not adding them again

            List<PolygonIndex> AddedVerticies = new List<PolygonIndex>();

            foreach (PolygonIndex index in candidates)
            {
                if (AddedVerticies.Contains(index)) //There is an error if we add a vertex twice, so don't.
                    continue;

                GridLineSegment segment = index.Segment(polygon);
                if (segment.Intersects(line, false, out IShape2D intersection))
                {
                    double distance;
                    IPoint2D p = intersection as IPoint2D;
                    if (p == null) //It is not a point, it is a line.  Therefore distance is zero
                    {
                        distance = 0;
                        if (output.ContainsKey(distance)) //There is an error if we add an endpoint twice, so don't
                            continue;

                        ILineSegment2D seg = intersection as ILineSegment2D;
                        AddIntersection(output, 0, index);
                        AddedVerticies.Add(index);
                    }
                    else //Intersection is a point
                    {
                        GridVector2 p2 = new GridVector2(p.X, p.Y);
                        distance = GridVector2.Distance(line.A, p2);

                        if (segment.IsEndpoint(p2))
                        {
                            if (output.ContainsKey(distance)) //There is an error if we add an endpoint twice, so don't
                                continue;

                            PolygonIndex intersection_index = index;
                            //If the endpoint is equal to segment.B it will be added on the next loop iteration
                            if (p2 == segment.B)
                            {
                                //If it is the next segment we can increment to the next segment and skip that iteration
                                intersection_index = index.Next;
                                if (AddedVerticies.Contains(intersection_index))
                                    continue; //Skip if we've already added this index.  (Should we check for a different distance?)
                            }

                            AddIntersection(output, distance, intersection_index);
                            AddedVerticies.Add(intersection_index);
                        }
                        else
                        {
                            AddIntersection(output, distance, index);
                            AddedVerticies.Add(index);
                        }
                    }
                }
            }

            /*
            for (int iRing = 0; iRing < polygon.InteriorRings.Count; iRing++)
            {
                GridPolygon innerPoly = polygon.InteriorPolygons[iRing];// new GridPolygon(polygon.InteriorRings.ToArray()[iRing]);
                SortedDictionary<double, PointIndex> ring_intersections = innerPoly.IntersectingSegments(line);
                foreach (var item in ring_intersections)
                {
                    //foreach (var instance in item.Value)
                    //{
                    AddIntersection(output, item.Key, new PointIndex(0, iRing, item.Value.iVertex, innerPoly.ExteriorRing.Length - 1));
                    //}
                }
            }
            
            for(int iSegment = 0; iSegment < polygon.ExteriorSegments.Length; iSegment++)
            {
                GridLineSegment segment = polygon.ExteriorSegments[iSegment];
                if (segment.Intersects(line, false, out IShape2D intersection))
                {
                    IPoint2D p = intersection as IPoint2D;
                    GridVector2 p2 = new GridVector2(p.X, p.Y);
                    double distance = GridVector2.Distance(line.A, p2);
                    if (segment.IsEndpoint(p2))
                    {
                        //If the endpoint is equal to segment.B it will be added on the next loop iteration
                        if (p2 == segment.B)
                        {
                            //If it is the next segment we can increment to the next segment and skip that iteration
                            iSegment = iSegment + 1;
                        }

                        AddIntersection(output, distance, new PointIndex(0, iSegment, polygon.ExteriorSegments.Length));
                    }
                    else
                    {
                        
                        AddIntersection(output, distance, new PointIndex(0, iSegment, polygon.ExteriorSegments.Length));
                    }
                }
            } 
            */
            return output;
        }

        /// <summary>
        /// Returns point indicies of the segments of the polygon that intersect the line.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static SortedDictionary<double, PolygonIndex> IntersectingSegments(this GridPolygon polygon, GridLineSegment[] path)
        {
            SortedDictionary<double, PolygonIndex> output = new SortedDictionary<double, PolygonIndex>();

            for (int iRing = 0; iRing < polygon.InteriorRings.Count; iRing++)
            {
                GridPolygon innerPoly = polygon.InteriorPolygons[iRing];// new GridPolygon(polygon.InteriorRings.ToArray()[iRing]);
                SortedDictionary<double, PolygonIndex> ring_intersections = innerPoly.IntersectingSegments(path);
                foreach (var item in ring_intersections)
                {
                    //foreach (var instance in item.Value)
                    //{
                    AddIntersection(output, item.Key, new PolygonIndex(0, iRing, item.Value.iVertex, innerPoly.ExteriorRing.Length - 1));
                    //}
                }
            }

            double total_length = 0;
            for (int iPath = 0; iPath < path.Length; iPath++)
            {
                GridLineSegment line = path[iPath];

                for (int iSegment = 0; iSegment < polygon.ExteriorSegments.Length; iSegment++)
                {
                    GridLineSegment segment = polygon.ExteriorSegments[iSegment];
                    if (segment.Intersects(line, false, out IShape2D intersection))
                    {
                        IPoint2D p = intersection as IPoint2D;
                        GridVector2 p2 = new GridVector2(p.X, p.Y);
                        double distance = GridVector2.Distance(line.A, p2) + total_length;
                        if (segment.IsEndpoint(p2))
                        {
                            //If the endpoint is equal to segment.B it will be added on the next loop iteration
                            if (p2 == segment.B)
                            {
                                //If it is the next segment we can increment to the next segment and skip that iteration
                                iSegment = iSegment + 1;
                            }

                            AddIntersection(output, distance, new PolygonIndex(0, iSegment, polygon.ExteriorSegments.Length));
                        }
                        else
                        {

                            AddIntersection(output, distance, new PolygonIndex(0, iSegment, polygon.ExteriorSegments.Length));
                        }
                    }
                }

                total_length += line.Length;
            }

            return output;
        }


        /// <summary>
        /// A bounding box of a polygon padded to account for line width or point radius
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="padding"></param>
        /// <param name="Position"></param>
        /// <returns></returns>
        public static bool PaddedPolygonContains(GridPolygon polygon, double padding, GridVector2 Position)
        {
            GridRectangle padded_bbox = polygon.BoundingBox.Pad(padding);
            return padded_bbox.Contains(Position);
        }

        public static GridRectangle BoundingBox(this IReadOnlyList<GridPolygon> polygons)
        {
            if (!polygons.Any())
            {
                throw new ArgumentException("No polygons in array to calculate bounding box");
            }

            GridRectangle bbox = polygons[0].BoundingBox;
            for (int i = 1; i < polygons.Count; i++)
            {
                bbox.Union(polygons[i].BoundingBox);

            }

            return bbox;
        }

        public static GridRectangle BoundingBox(this IEnumerable<GridPolygon> polygons)
        {
            if (!polygons.Any())
            {
                throw new ArgumentException("No polygons in array to calculate bounding box");
            }

            bool first = true;
            GridRectangle bbox = new GridRectangle();
            foreach (var poly in polygons)
            {
                if (first)
                {
                    bbox = poly.BoundingBox;
                    first = false;
                }
                else
                {
                    bbox.Union(poly.BoundingBox);
                }
            }
            return bbox;
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
