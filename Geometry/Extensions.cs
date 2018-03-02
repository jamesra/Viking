using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RTree;
using MathNet.Numerics.LinearAlgebra;

namespace Geometry
{

    public static class SortingExtensions
    {
        /// sort array 'rg', returning the original index positions
        public static int[] SortAndIndex<T>(this T[] rg)
        {
            int i, c = rg.Length;
            var keys = new int[c];
            if (c > 1)
            {
                for (i = 0; i < c; i++)
                    keys[i] = i;

                System.Array.Sort(rg, keys /*, ... */);
            }
            return keys;
        }
    }

    public static class ArrayToStringExtensions
    {
        public static string ToCSV(this double[] array, string delimiter = ", ", string format = "F2")
        {
            return string.Join(delimiter, array.Select(v => v.ToString(format)) );
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

        public static RTree.Rectangle ToRTreeRect(this IPoint2D p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridBox bbox)
        {
            return new RTree.Rectangle(bbox.minVals,
                                       bbox.maxVals);
        }

        public static RTree.RTree<GridLineSegment> ToRTree(this IEnumerable<GridLineSegment> lines)
        {
            RTree.RTree<GridLineSegment> rTree = new RTree<Geometry.GridLineSegment>();
            foreach(GridLineSegment l in lines)
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

    public static class GridVector2Extensions
    {
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
            if(points.First() == points.Last())
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
            return points.PolygonArea() < 0;
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
            for(int iPoint = 0; iPoint < points.Count-1; iPoint++)
            {
                segments[iPoint] = new GridLineSegment(points.ElementAt(iPoint), points.ElementAt(iPoint + 1));
            }

            return segments;
        }

        /// <summary>
        /// Remove all of the adjacent duplicate points and return as a new array
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] RemoveDuplicates(this IReadOnlyList<GridVector2> points)
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

        public static double MinDistanceBetweenPoints(this GridVector2[] points)
        {
            double minVal = double.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    if (points[i] != points[j])
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
            for(int i = 0; i < points.Length-1; i++)
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

            return new GridBox( new double[] { minX, minY, minZ }, 
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
            GridVector2 intersection;

            if (IgnoreEndpoints)
            {
                segments = segments.Where(s => !s.SharedEndPoint(testSeg)).ToList();
            }

            foreach (GridLineSegment existingLine in segments)
            {
                if (existingLine.Intersects(testSeg, out intersection))
                {
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
            for(int i = 0; i < segments.Count; i++)
            {
                verticies[i] = segments.ElementAt(i).A;
            }

            verticies[segments.Count] = segments.Last().B;

            return verticies;
        }

        /// <summary>
        /// Returns the index and distance to the nearest line segment in an array, brute force
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="p"></param>
        /// <param name="MinDistance"></param>
        /// <returns></returns>
        static public int NearestSegment(this ICollection<GridLineSegment> segments, GridVector2 p, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            double[] distancesToNewPoint = segments.Select(l => l.DistanceToPoint(p)).ToArray();
            double minDistance = distancesToNewPoint.Min();
            int iNearestSegment = distancesToNewPoint.TakeWhile(d => d != minDistance).Count();
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
            verts.Insert(segmentIndex+1, newPointPosition); 
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

            for (int iOldPoint = 0; iOldPoint < iNearestPoint; iOldPoint++)
            {
                newControlPoints[iOldPoint] = OriginalControlPoints[iOldPoint];
            }

            for (int iOldPoint = iNearestPoint + 1; iOldPoint < OriginalControlPoints.Length; iOldPoint++)
            {
                newControlPoints[iOldPoint - 1] = OriginalControlPoints[iOldPoint];
            }

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

            for(int i = 0; i < src.Count; i++)
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
                combo.A.AddPointsAtIntersections(combo.B);
                combo.B.AddPointsAtIntersections(combo.A);
            }
        }

        public static void AddPointsAtAllIntersections(this GridPolygon[] polygons, double[] polyZ)
        { 
            if(polygons.Length != polyZ.Length)
            {
                throw new ArgumentException("polyZ must have same length as polygons");
            }

            foreach (Combo<GridPolygon> combo in polygons.CombinationPairs())
            {
                if (polyZ[combo.iA] == polyZ[combo.iB])
                    continue;

                combo.A.AddPointsAtIntersections(combo.B);
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

        /// <summary>
        /// Returns the Polygon segment which intersects the point, if any.  May return interior polygons
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param> 
        /// <param name="nearestPoly">Nearest polygon</param>
        /// <param name="intersectingPoly">Index of vertex in the ring</param>
        /// <returns></returns>
        public static double NearestPolygonVertex(this GridPolygon polygon, GridVector2 WorldPosition, out GridPolygon nearestPoly, out int ringIndex)
        {
            nearestPoly = null;
            ringIndex = -1;
            double nearestPolyDistance = double.MaxValue;

            foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                GridPolygon foundPolygon;
                int foundIndex;
                double distance = innerPoly.NearestPolygonVertex(WorldPosition, out foundPolygon, out foundIndex);
                if (distance < nearestPolyDistance)
                {
                    nearestPoly = innerPoly;
                    nearestPolyDistance = distance;
                    ringIndex = foundIndex;
                }
            }

            double[] distances = polygon.ExteriorRing.Select(p => GridVector2.Distance(p, WorldPosition)).ToArray();
            double MinDistance = distances.Min();

            if (MinDistance < nearestPolyDistance)
            {
                ringIndex = Array.IndexOf(distances, distances.Min());
                nearestPoly = polygon;
                nearestPolyDistance = MinDistance;
            }

            return nearestPolyDistance;
        }

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
            for(int i = 1; i < polygons.Count; i++)
            { 
                bbox.Union(polygons[i].BoundingBox);
                
            }

            return bbox;
        }

        public static bool AreIndiciesAdjacent(this IReadOnlyList<GridPolygon> polygons, PointIndex A, PointIndex B)
        {
            return A.AreAdjacent(B, polygons);
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
