using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
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

                if (comparer is null)
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
        public static string ToCSV(this double[] array, string delimiter = ", ", string format = "F2") => string.Join(delimiter, array.Select(v => v.ToString(format)));/*
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < array.Count(); i++)
            {
                sb.Append(array[i].ToString(format));
                if (i < array.Count() - 1)
                    sb.Append(",");
            }

            return sb.ToString();
            */

        public static string ToMatlab(this double[] array, string format = "F2")
        {
            StringBuilder sb = new();
            sb.Append('[');
            sb.Append(array.ToCSV(" "));
            sb.Append(']');

            return sb.ToString();
        }
    }

    public static class IPoint2DExtensions
    {
        public static Vector2 Round(this IPoint2D p, int precision) => new Vector2(Math.Round(p.X, precision), Math.Round(p.Y, precision));

        public static Vector2 ToVector2(this IPoint2D p) => new Vector2(p.X, p.Y);

        public static Vector2[] ToVector2(this IEnumerable<IPoint2D> points)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));

            return points.Select(p => p.ToVector2()).ToArray();
        }

        public static Rectangle ToRectangle(this IRectangle2D rectangle)
        {
            if (rectangle is null)
                throw new ArgumentNullException(nameof(rectangle));

            if (rectangle is Rectangle r)
                return r;

            return new Rectangle(rectangle.Left, rectangle.Right, rectangle.Bottom, rectangle.Top);
        }
    }

    public static class IShape2DExtensions
    {
        public static Rectangle BoundingBox(this IEnumerable<IShape2D> shapes)
        {
            if (shapes is null)
                throw new ArgumentNullException(nameof(shapes));

            if (!shapes.Any())
                throw new ArgumentException("Parameter must have at least one entry", nameof(shapes));


            bool first = true;
            Rectangle bbox = new();
            foreach (var s in shapes)
            {
                var result = s.BoundingBox;
                if (first)
                {
                    bbox = result;
                    first = false;
                }
                else
                {
                    bbox += result;
                }
            }
            return bbox;
        }
    }

    public static class Vector2Extensions
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
                List<int> newPoints = [.. points, points.First()];
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
        public static ICollection<Vector2> EnsureClosedRing(this ICollection<Vector2> points)
        {
            if (points.First() != points.Last())
            {
                List<Vector2> newPoints = [.. points, points.First()];
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
        public static Vector2[] EnsureClosedRing(this Vector2[] points)
        {
            if (points.First() != points.Last())
            {
                Vector2[] newPoints = new Vector2[points.Length + 1];
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
        public static ICollection<Vector2> EnsureOpenRing(this ICollection<Vector2> points)
        {
            if (points.Count < 2)
                return [.. points];

            if (points.First() == points.Last())
            {
                List<Vector2> newPoints = [.. points];
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
        public static Vector2[] EnsureOpenRing(this Vector2[] points)
        {
            if (points.Length < 2)
                return [.. points];

            if (points.First() == points.Last())
            {
                Vector2[] newPoints = new Vector2[points.Length - 1];
                Array.Copy(points, newPoints, points.Length - 1);
                return newPoints;
            }

            return points;
        }

        public static Vector2 Average(this ICollection<Vector2> points)
        {
            double mX = 0;
            double mY = 0;

            foreach (Vector2 p in points)
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

            return new Vector2(mX / (double)points.Count, mY / (double)points.Count);
        }

        /// <summary>
        /// Return the index of a point in the array we know is on the convex hull
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static int FindPointOnConvexHull(this Vector2[] points)
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
        public static bool IsValidClosedRing(this ICollection<Vector2> points)
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
        public static bool AreClockwise(this Vector2[] points) => points.Winding() == RotationDirection.Clockwise;

        /// <summary>
        /// Return RotationDirection of the points.  Code Assumes points do not cross over themselves. 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static RotationDirection Winding(this Vector2[] points)
        {
            if (points.Length <= 2)
                return RotationDirection.Colinear;
            else if (points.Length == 3)
                return Winding(points[0], points[1], points[2]);
            else
            {
                double area = points.PolygonArea();
                RotationDirection result = area == 0 ? RotationDirection.Colinear :
                    area < 0 ? RotationDirection.Clockwise : RotationDirection.Counterclockwise;
                return result;
            }
        }

        /// <summary>
        /// Return RotationDirection of the points.  Code Assumes points do not cross over themselves. 
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static RotationDirection Winding(this Vector2 p1, Vector2 p2, Vector2 p3)
        {
            // See 10th slides from following link
            // for derivation of the formula
            double val = (p2.Y - p1.Y) * (p3.X - p2.X) -
                      (p2.X - p1.X) * (p3.Y - p2.Y);

            if (val > -Tolerance.Epsilon && val < Tolerance.Epsilon) return RotationDirection.Colinear;

            // clock or counterclock wise
            return (val > 0) ? RotationDirection.Clockwise : RotationDirection.Counterclockwise;
        }

        /// <summary>
        /// Create line segments between adjacent points in the collection.  Identical adjacent points are ignored.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static LineSegment[] ToLineSegments(this ICollection<Vector2> points)
        {
            if (points is null)
                return null;

            if (points.Count <= 1)
                throw new ArgumentException("Must have two points to create line segments");

            LineSegment[] segments = new LineSegment[points.Count - 1];
            int iLine = 0;
            for (int iPoint = 0; iPoint < points.Count - 1; iPoint++)
            {
                try
                {
                    segments[iLine] = new LineSegment(points.ElementAt(iPoint), points.ElementAt(iPoint + 1));
                    iLine++;
                }
                catch (ArgumentException)
                {
                    //If points are identical, do not add them to the result set
                    if (points.ElementAt(iPoint) == points.ElementAt(iPoint + 1))
                        continue;
                    else
                        throw;
                }
            }

            //Resize the array if we omitted any identical point pairs
            if (iLine < segments.Length)
            {
                LineSegment[] shortened_array = new LineSegment[iLine];
                Array.Copy(segments, shortened_array, iLine);
                return shortened_array;
            }


            return segments;
        }

        /// <summary>
        /// Create a polyline from points in the collection
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Polyline ToPolyline(this ICollection<Vector2> points, bool AllowSelfIntersection = false)
        {
            if (points is null)
                return null;

            if (points.Count <= 1)
                throw new ArgumentException("Must have two points to create line segments");

            Polyline polyline = new(points, AllowSelfIntersection);
            return polyline;
        }

        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static List<Vector2> RemoveAdjacentDuplicates(this IEnumerable<Vector2> points)
        {
            List<Vector2> nonDuplicatePoints = [];
            var enumerator = points.GetEnumerator();
            if (!enumerator.MoveNext())
                throw new ArgumentException("Must have at least one point to remove adjacent duplicates");

            Vector2 p = enumerator.Current;
            Vector2 next = Vector2.Zero;
            int count = 0;
            while (enumerator.MoveNext())
            {
                next = enumerator.Current;
                if (p != next)
                {
                    nonDuplicatePoints.Add(p);
                    p = next;
                }

                //Don't advance p every loop if values are equal.  This allows small epsilon changes to add up and eventually add a point

                count++;
            }

            if (count == 0) //There was only one point and the loop didn't execute
                nonDuplicatePoints.Add(p);

            //Always preserve the last point
            if (count > 0) //Did the loop execute at least once?  If so we need to account for the last point
                nonDuplicatePoints.Add(next);

            //System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints;
        }

        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <param name="preserved_path">These control points need to be preserved.  They appear in the same order as they appear in the input points parameter.</param>
        /// <returns></returns>
        public static List<Vector2> RemoveAdjacentDuplicates(this IEnumerable<Vector2> points, IEnumerable<Vector2> preserved_path)
        {
            List<Vector2> nonDuplicatePoints = [];
            var enumerator = points.GetEnumerator();
            if (!enumerator.MoveNext())
                throw new ArgumentException("Must have at least one point to remove adjacent duplicates");

            if (preserved_path is null)
                throw new ArgumentNullException(nameof(preserved_path));

            var preserved_enumerator = preserved_path.GetEnumerator();
            Vector2 preserved_point = preserved_enumerator.MoveNext() ? preserved_enumerator.Current : Vector2.NaN;

            Vector2 p = enumerator.Current;
            Vector2 next = Vector2.NaN;
            int count = 0;
            while (enumerator.MoveNext())
            {
                next = enumerator.Current;
                if (p.X == preserved_point.X && p.Y == preserved_point.Y)
                {
                    //Skip further duplicates of the preserved point, use the epsilon equality operator to ensure we get far enough away to not add a psuedo-duplicate point
                    while (next == preserved_point)
                        next = enumerator.MoveNext() ? enumerator.Current : Vector2.NaN;

                    //Add the preserved point
                    nonDuplicatePoints.Add(p);

                    preserved_point = preserved_enumerator.MoveNext() ? preserved_enumerator.Current : Vector2.NaN;
                }
                else if (p == preserved_point) //It is close, but not the exact point, so skip it because we know the exact preserved_point will be added soon.
                {
                    if (next != preserved_point) //We are moving away from the preserved point, so add it to ensure it isn't lost
                    {
                        nonDuplicatePoints.Add(preserved_point);
                        preserved_point = preserved_enumerator.MoveNext() ? preserved_enumerator.Current : Vector2.NaN;
                    }
                }
                else if (p != next)
                {
                    nonDuplicatePoints.Add(p);
                }

                //Advance p every loop to ensure we add the preserved the control points
                p = next;

                count++;
            }

            if (count == 0) //There was only one point and the loop didn't execute
                nonDuplicatePoints.Add(p);

            //Always preserve the last point
            if (count > 0) //Did the loop execute at least once?  If so we need to account for the last point
                nonDuplicatePoints.Add(next);

            //System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints;
        }

        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static List<Vector2> RemoveAdjacentDuplicates(this ICollection<Vector2> points)
        {
            List<Vector2> nonDuplicatePoints = new(points.Count);
            Vector2 p = points.First();
            int i = 0;
            foreach (Vector2 next in points)
            {
                if (p != next)
                    nonDuplicatePoints.Add(p);

                if (i == points.Count - 1)
                {
                    nonDuplicatePoints.Add(next);
                    //This is the last pass through the loop, always preserve the last point
                }

                p = next;
                i++;
            }

            //                System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints;
        }

        /*
        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Vector2[] RemoveAdjacentDuplicates(this IReadOnlyList<Vector2> points)
        {
            List<Vector2> nonDuplicatePoints = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i] != points[i + 1])
                {
                    nonDuplicatePoints.Add(points[i]);
                }
            }

            nonDuplicatePoints.Add(points[points.Count - 1]);

            //                System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return nonDuplicatePoints.ToArray();
        }
        */

        /// <summary>
        /// Remove all of the duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Vector2[] RemoveDuplicates(this IReadOnlyList<Vector2> points)
        {
            List<Vector2> nonDuplicatePoints = [];
            for (int i = 0; i < points.Count; i++)
            {
                if (false == nonDuplicatePoints.Contains(points[i]))
                    nonDuplicatePoints.Add(points[i]);
            }


            //                System.Diagnostics.Trace.WriteLine("Originally " + (ControlPoints.Count * NumInterpolations).ToString() + " now " + nonDuplicatePoints.Count.ToString());
            return [.. nonDuplicatePoints];
        }

        /*
        /// <summary>
        /// Return true if the points are placed in clockwise order.  Assumes points do not cross over themselves.
        /// This original implementation only works for convex polygons
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool AreClockwise(this Vector2[] points)
        {
            if (points.Length < 3)
                throw new ArgumentException("Insufficient points to determine AreClockwise()");

            //We need to make sure our center vertex is on the convex hull

            int iConvexHullPoint = FindPointOnConvexHull(points);
            int iBefore = iConvexHullPoint - 1 > 0 ? iConvexHullPoint - 1 : points.Length - 1;
            int iAfter = iConvexHullPoint + 1 < points.Length ? iConvexHullPoint + 1 : 0;

            Vector2 A = points[iBefore];
            Vector2 B = points[iConvexHullPoint];
            Vector2 C = points[iAfter]; 

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
        public static double PolygonArea(this Vector2[] points)
        {
            //System.Diagnostics.Debug.Assert(points.First() == points.Last(), "First and last point must be identical to determine area of polygon");
            points = points.EnsureClosedRing();

            //Ensure the points do not have large values.
            Vector2 avg = points.Average();
            points = points.Translate(-avg);

            double accumulator = 0;

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                accumulator += ((p0.X * p1.Y) - (p1.X * p0.Y));
            }

            return accumulator / 2.0;
        }

        public static Vector2 Min(this IEnumerable<Vector2> points)
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            foreach (var p in points)
            {
                minX = p.X < minX ? p.X : minX;
                minY = p.Y < minY ? p.Y : minY;
            }

            return new Vector2(minX, minY);
        }

        public static Vector2 Max(this IEnumerable<Vector2> points)
        {
            var maxX = double.MinValue;
            var maxY = double.MinValue;
            foreach (var p in points)
            {
                maxX = p.X > maxX ? p.X : maxX;
                maxY = p.Y > maxY ? p.Y : maxY;
            }

            return new Vector2(maxX, maxY);
        }

        /// <returns>[MinX/Left, MaxX/Right, MinY/Bottom, MaxY/Top]</returns>
        public static double[] GetBounds(this Vector2[] points)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));

            if (points.Length == 0)
                throw new ArgumentException("Empty collection", nameof(points));

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                minX = points[i].X < minX ? points[i].X : minX;
                maxX = points[i].X > maxX ? points[i].X : maxX;
                minY = points[i].Y < minY ? points[i].Y : minY;
                maxY = points[i].Y > maxY ? points[i].Y : maxY;
            }

            return [minX, maxX, minY, maxY];
        }

        /// <returns>[MinX/Left, MaxX/Right, MinY/Bottom, MaxY/Top]</returns>
        public static double[] GetBounds(this IEnumerable<Vector2> points)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));


            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Vector2 p in points)
            {
                minX = p.X < minX ? p.X : minX;
                maxX = p.X > maxX ? p.X : maxX;
                minY = p.Y < minY ? p.Y : minY;
                maxY = p.Y > maxY ? p.Y : maxY;
            }

            if (minX == double.MinValue && points.Any() == false)
                throw new ArgumentException("Empty collection", nameof(points));

            return [minX, maxX, minY, maxY];
        }

        public static Rectangle BoundingBox(this Vector2[] points) => new(GetBounds(points));

        public static Rectangle BoundingBox(this IEnumerable<Vector2> points) => new(GetBounds(points));

        /// <summary>
        /// Given a set of points, return the closest distance between any two points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double MinDistanceBetweenAnyPoints(this IReadOnlyList<Vector2> points)
        {
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (points[i] != points[j])
                        minVal = Math.Min(minVal, Vector2.Distance(points[i], points[j]));
                }
            }

            return minVal;
        }

        /// <summary>
        /// Given a set of points, return the closest distance between any two points
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static double MinDistanceBetweenSequentialPoints(this IReadOnlyList<Vector2> points, out int FirstIndex)
        {
            FirstIndex = points.Count;
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i] != points[i + 1])
                {
                    minVal = Math.Min(minVal, Vector2.Distance(points[i], points[i + 1]));
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
        public static Vector2? IntersectionPoint(this ICollection<Vector2> Vertices, LineSegment testSeg)
        {
            LineSegment[] segments = LineSegment.SegmentsFromPoints([.. Vertices]);
            return segments.IntersectionPoint(testSeg, false);
        }


        /// <summary>
        /// Returns the index and distance to the nearest point in an array, brute force
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="p"></param>
        /// <param name="MinDistance"></param>
        /// <returns></returns>
        public static int NearestPoint(this ICollection<Vector2> points, Vector2 testPoint, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            double[] distancesToRemovalPoint = [.. points.Select(p => Vector2.Distance(p, testPoint))];
            double minDistance = distancesToRemovalPoint.Min();
            int iNearestPoint = distancesToRemovalPoint.TakeWhile(d => d != distancesToRemovalPoint.Min()).Count();
            MinDistance = minDistance;
            return iNearestPoint;
        }

        public static double PerimeterLength(this Vector2[] points)
        {
            points = points.EnsureClosedRing();
            double length = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                length += Vector2.Distance(in points[i], in points[i + 1]);
            }

            return length;
        }

        public static bool SetEquals(this IReadOnlyList<Vector2> A, IReadOnlyList<Vector2> B)
        {
            if (A is null) throw new ArgumentNullException(nameof(A));
            if (B is null) throw new ArgumentNullException(nameof(B));
            if (A.Count != B.Count)
                return false;
            HashSet<Vector2> set = new(A);
            foreach (Vector2 p in B)
            {
                if (!set.Contains(p))
                    return false;
            }
            return true;
        }

        public static Vector2[] Translate(this ICollection<Vector2> points, Vector2 offset)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            Vector2[] result = new Vector2[points.Count];
            int i = 0;
            foreach (Vector2 p in points)
            {
                result[i++] = p + offset;
            }
            return result;
        }

        public static Vector2[] Rotate(this ICollection<Vector2> points, double angle, Vector2 centerOfRotation)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            Vector2[] result = new Vector2[points.Count];
            int i = 0;
            foreach (Vector2 p in points)
            {
                result[i++] = ((p - centerOfRotation).Rotate(angle)) + centerOfRotation;
            }
            return result;
        }

        public static Vector2[] Scale(this ICollection<Vector2> points, double scale, Vector2 origin) =>
            points.Scale(new Vector2(scale, scale), origin);

        public static Vector2[] Scale(this ICollection<Vector2> points, Vector2 scale, Vector2 origin)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));
            Vector2[] result = new Vector2[points.Count];
            int i = 0;
            foreach (Vector2 p in points)
            {
                Vector2 d = p - origin;
                result[i++] = new Vector2(origin.X + (d.X * scale.X), origin.Y + (d.Y * scale.Y));
            }
            return result;
        }
    }

    public static class Vector3Extensions
    {
        public static Vector3 Centroid(this ICollection<Vector3> points)
        {
            double mX = 0;
            double mY = 0;
            double mZ = 0;

            foreach (Vector3 p in points)
            {
                mX += p.X;
                mY += p.Y;
                mZ += p.Z;
            }

            return new Vector3(mX / (double)points.Count, mY / (double)points.Count, mZ / (double)points.Count);
        }

        public static Box BoundingBox(this IReadOnlyList<Vector3> points)
        {
            if (points is null)
                throw new ArgumentNullException(nameof(points));

            if (points.Count == 0)
                throw new ArgumentException("Rectangle Border is empty", nameof(points));

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

            return new Box([minX, minY, minZ],
                                [maxX, maxY, maxZ]);
        }

        public static Vector3 Average(this ICollection<Vector3> points)
        {
            double mX = 0;
            double mY = 0;
            double mZ = 0;

            foreach (Vector3 p in points)
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

            return new Vector3(mX / (double)points.Count, mY / (double)points.Count, mZ / (double)points.Count);
        }

        public static Vector2 XY(this Vector3 point) => new Vector2(point.X, point.Y);
    }

    public static class LineSegmentExtensions
    {

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="IgnoreEndpoints">Ignore line segments where the endpoints are identical</param>
        /// <returns></returns>
        public static Vector2? IntersectionPoint(this ICollection<LineSegment> segments, LineSegment testSeg, bool IgnoreEndpoints) => IntersectionPoint(segments, testSeg, IgnoreEndpoints, out LineSegment? intersectedSegment);

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="IgnoreEndpoints">Ignore line segments where the endpoints are identical</param>
        /// <returns></returns>
        public static Vector2? IntersectionPoint(this ICollection<LineSegment> segments, LineSegment testSeg, bool IgnoreEndpoints, out LineSegment? intersectedSegment)
        {
            intersectedSegment = new LineSegment?();

            if (IgnoreEndpoints)
            {
                segments = [.. segments.Where(s => !s.SharedEndPoint(in testSeg))];
            }

            foreach (LineSegment existingLine in segments)
            {
                if (existingLine.Intersects(in testSeg, out Vector2 intersection))
                {
                    intersectedSegment = existingLine;
                    return new Vector2?(intersection);
                }
            }

            return new Vector2?();
        }

        /// <summary>
        /// Returns the unique endpoints of all line segments in order
        /// </summary>
        /// <param name="segments"></param>
        /// <returns></returns>
        public static Vector2[] Vertices(this ICollection<LineSegment> segments)
        {
            Vector2[] verticies = new Vector2[segments.Count + 1];
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
        public static int NearestSegment(this ICollection<LineSegment> segments, Vector2 p, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            int iNearestSegment = segments.TakeWhile(s => s.A != p).Count();
            if (iNearestSegment < segments.Count || segments.Count == 0)
            {
                MinDistance = 0;
                return iNearestSegment;
            }

            double[] distancesToNewPoint = [.. segments.Select(l => l.DistanceToPoint(in p))];
            double minDistance = distancesToNewPoint.Min();

            iNearestSegment = distancesToNewPoint.TakeWhile(d => d != minDistance).Count();
            MinDistance = minDistance;
            return iNearestSegment;
        }

        static bool IsRing(this ICollection<LineSegment> segments) => segments.First().A == segments.Last().B;

        /// <summary>
        /// Include the new point in the grid line segment array.  Creates two new segments from (index-1, index) and (index, index + 1) and removes the segment between (index-1 and index) by creating a new segment between the new point and closest vertex in the existing segments.  Preserves order.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="newPoint"></param>
        /// <returns></returns>
        public static LineSegment[] Insert(this ICollection<LineSegment> lineSegs, Vector2 newPointPosition, int segmentIndex)
        {
            Vector2[] newControlPoints = new Vector2[lineSegs.Count + 2];

            List<Vector2> verts = [.. lineSegs.Vertices()];
            verts.Insert(segmentIndex + 1, newPointPosition);
            return verts.ToLineSegments();
        }

        /// <summary>
        /// Remove the grid line segment vertex at the index.  Create new a new line segment between the adjacent points remaining.
        /// </summary>
        /// <param name="lineSegs"></param>
        /// <param name="iNearestPoint"></param>
        /// <returns></returns>
        public static LineSegment[] Remove(this ICollection<LineSegment> lineSegs, int iNearestPoint)
        {
            Vector2[] OriginalControlPoints = lineSegs.Vertices();
            Vector2[] newControlPoints = new Vector2[OriginalControlPoints.Length - 1];

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
        public static LineSegment[] ShortenLastVertex(this IReadOnlyList<LineSegment> src)
        {
            LineSegment[] dest = new LineSegment[src.Count];

            for (int i = 0; i < src.Count; i++)
            {
                dest[i] = src[i];
            }

            LineSegment lastSegment = src[src.Count - 1];
            Vector2 newEndpoint = lastSegment.PointAlongLine(0.99);
            dest[src.Count - 1] = new LineSegment(lastSegment.A, newEndpoint);

            return dest;
        }
    }

    public static class PolygonExtensions
    {
        public static void AddPointsAtAllIntersections(this Polygon[] polygons)
        {
            foreach (Combo<Polygon> combo in polygons.CombinationPairs())
            {
                var result = combo.A.AddPointsAtIntersections(combo.B);
                if (result.Any())
                    combo.B.AddPointsAtIntersections(combo.A);
            }
        }

        public static void AddPointsAtAllIntersections(this Polygon[] polygons, double[] polyZ)
        {
            if (polygons.Length != polyZ.Length)
            {
                throw new ArgumentException("polyZ must have same length as polygons");
            }

            foreach (Combo<Polygon> combo in polygons.CombinationPairs())
            {
                if (polyZ[combo.iA] == polyZ[combo.iB])
                    continue;

                var result = combo.A.AddPointsAtIntersections(combo.B);
                if (result.Any())
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
        public static bool PointIntersectsAnyPolygonVertex(this Polygon polygon, Vector2 WorldPosition, double ControlPointRadius, out Polygon intersectingPoly)
        {
            //Quick check to see if it is possible for a vertex to intersect
            if (!PaddedPolygonContains(polygon, ControlPointRadius, WorldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (Polygon innerPoly in polygon.InteriorPolygons)
            {
                if (PointIntersectsAnyPolygonVertex(innerPoly, WorldPosition, ControlPointRadius, out intersectingPoly))
                {
                    return true;
                }
            }

            Circle testCircle = new(WorldPosition, ControlPointRadius);
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
        public static bool PointIntersectsAnyPolygonSegment(this Polygon polygon, Vector2 WorldPosition, double LineWidth, out Polygon intersectingPoly)
        {
            //Quick check to see if it is possible for a segment to intersect
            if (!PaddedPolygonContains(polygon, LineWidth / 2.0f, WorldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (Polygon innerPoly in polygon.InteriorPolygons)
            {
                if (innerPoly.PointIntersectsAnyPolygonSegment(WorldPosition, LineWidth, out intersectingPoly))
                {
                    return true;
                }
            }

            polygon.ExteriorSegments.NearestSegment(WorldPosition, out double MinDistance);
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
     public static double NearestPolygonSegment(this Polygon polygon, Vector2 WorldPosition, out Polygon nearestPoly)
     {
         nearestPoly = null;
         double nearestPolyDistance = double.MaxValue;

         foreach (Polygon innerPoly in polygon.InteriorPolygons)
         {
             Polygon foundPolygon;
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
        public static SortedDictionary<double, PolygonIndex> IntersectingSegments(this Polygon polygon, in LineSegment line)
        {
            SortedDictionary<double, PolygonIndex> output = [];

            PolygonIndex[] candidates = [.. polygon.SegmentRTree.Intersects(line.BoundingBox)];

            //Due to epsilon factors a single line may intersect the same vertex twice when a line passes near the vertex.
            //We control this by keeping a list of verticies we've already added and not adding them again

            List<PolygonIndex> AddedVerticies = [];

            foreach (PolygonIndex index in candidates)
            {
                if (AddedVerticies.Contains(index)) //There is an error if we add a vertex twice, so don't.
                    continue;

                LineSegment segment = index.Segment(polygon);
                if (segment.Intersects(in line, false, out IShape2D intersection))
                {
                    double distance;
                    if (intersection is not IPoint2D p) //It is not a point, it is a line.  Therefore distance is zero
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
                        Vector2 p2 = new(p.X, p.Y);
                        distance = Vector2.Distance(line.A, p2);

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
                Polygon innerPoly = polygon.InteriorPolygons[iRing];// new Polygon(polygon.InteriorRings.ToArray()[iRing]);
                SortedDictionary<double, PointIndex> ring_intersections = innerPoly.IntersectingSegments(line);
                foreach (var item in ring_intersections)
                {
                    //foreach (var instance in item.Value)
                    //{
                    AddIntersection(output, item.Key, new PointIndex(0, iRing, item.Value.VertexIndex, innerPoly.ExteriorRing.Length - 1));
                    //}
                }
            }
            
            for(int iSegment = 0; iSegment < polygon.ExteriorSegments.Length; iSegment++)
            {
                LineSegment segment = polygon.ExteriorSegments[iSegment];
                if (segment.Intersects(line, false, out IShape2D intersection))
                {
                    IPoint2D p = intersection as IPoint2D;
                    Vector2 p2 = new Vector2(p.X, p.Y);
                    double distance = Vector2.Distance(line.A, p2);
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
        public static SortedDictionary<double, PolygonIndex> IntersectingSegments(this Polygon polygon, LineSegment[] path)
        {
            SortedDictionary<double, PolygonIndex> output = [];

            for (int iRing = 0; iRing < polygon.InteriorRings.Count; iRing++)
            {
                Polygon innerPoly = polygon.InteriorPolygons[iRing];// new Polygon(polygon.InteriorRings.ToArray()[iRing]);
                SortedDictionary<double, PolygonIndex> ring_intersections = innerPoly.IntersectingSegments(path);
                foreach (var item in ring_intersections)
                {
                    //foreach (var instance in item.Value)
                    //{
                    AddIntersection(output, item.Key, new PolygonIndex(0, iRing, item.Value.VertexIndex, innerPoly.ExteriorRing.Length - 1));
                    //}
                }
            }

            double total_length = 0;
            for (int iPath = 0; iPath < path.Length; iPath++)
            {
                LineSegment line = path[iPath];

                for (int iSegment = 0; iSegment < polygon.ExteriorSegments.Length; iSegment++)
                {
                    LineSegment segment = polygon.ExteriorSegments[iSegment];
                    if (segment.Intersects(line, false, out IShape2D intersection))
                    {
                        IPoint2D p = intersection as IPoint2D;
                        Vector2 p2 = new(p.X, p.Y);
                        double distance = Vector2.Distance(line.A, p2) + total_length;
                        if (segment.IsEndpoint(p2))
                        {
                            //If the endpoint is equal to segment.B it will be added on the next loop iteration
                            if (p2 == segment.B)
                            {
                                //If it is the next segment we can increment to the next segment and skip that iteration
                                iSegment++;
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
        public static bool PaddedPolygonContains(Polygon polygon, double padding, Vector2 position)
        {
            Rectangle padded_bbox = polygon.BoundingBox + padding;
            return padded_bbox.Contains(position);
        }

        public static Rectangle BoundingBox(this IReadOnlyList<Polygon> polygons)
        {
            if (polygons is null)
            {
                throw new ArgumentNullException(nameof(polygons));
            }

            if (!polygons.Any())
            {
                throw new ArgumentException("No polygons in array to calculate bounding box");
            }

            Rectangle bbox = polygons[0].BoundingBox;
            for (int i = 1; i < polygons.Count; i++)
            {
                bbox += polygons[i].BoundingBox;
            }

            return bbox;
        }


    }

}
