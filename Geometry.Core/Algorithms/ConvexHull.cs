using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public static class ConvexHullExtension
    {
        /// <summary>
        /// Return the Convex Hull of a set of Polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static Polygon ConvexHull(this Polygon[] Polygons)
        {
            Vector2[] AllPoints = [.. Polygons.Where(poly => poly != null).SelectMany(poly => poly.ExteriorRing.EnsureOpenRing())];

            if (AllPoints.Length < 3)
                return null;

            Vector2[] EntireSetConvexHull = AllPoints.ConvexHull(out int[] originalIndices);
            return new Polygon(EntireSetConvexHull);
        }

        public static Vector2[] ConvexHull(this IReadOnlyList<Vector2> points) => ConvexHull(points, out var _);

        /// <summary>
        /// Convex hull of a point set via Andrew's monotone chain (sort by X, then upper and lower hulls).
        /// <paramref name="originalIndices"/> lists hull vertices in the input list, matching hull order.
        /// </summary>
        /// <remarks>
        /// Andrew, "Another efficient algorithm for convex hulls in two dimensions,"
        /// Inform. Process. Lett. 9(5):216–219 (1979). Large absolute coordinates have historically
        /// produced incorrect hulls (orientation tests); this method does not recenter the input.
        /// </remarks>
        public static Vector2[] ConvexHull(this IReadOnlyList<Vector2> points, out int[] originalIndices)
        {
            int[] ordered_idx = [.. points.Select((p, i) => i)];

            if (points.Count == 0)
            {
                originalIndices = [];
                return [];
            }

            if (points.Count == 1)
            {
                originalIndices = ordered_idx;
                return [.. points];
            }

            //If the points are a cycle, then make each point unique
            if (points[0] == points[points.Count - 1])
            {
                if (points.Count <= 4)
                {
                    originalIndices = ordered_idx;
                    return [.. points]; //All points are on convex hull
                }

                Vector2[] newArray = new Vector2[points.Count - 1];
                Array.Copy(points.ToArray(), newArray, newArray.Length);
                points = newArray;
                ordered_idx = [.. points.Select((p, i) => i)];
            }
            else if (points.Count <= 3)
            {
                Vector2[] ring_points = points.ToArray().EnsureClosedRing();
                List<int> listOriginalIndices = [.. ordered_idx, 0];
                originalIndices = [.. listOriginalIndices];
                return ring_points;
            }



            // Large absolute coordinates have produced incorrect hulls (floating-point orientation).
            // This path does not recenter; MeshExtensions.Triangulate does when triangulating polygons.


            //Sort and return the index of original points
            Array.Sort<int>(ordered_idx, (a, b) => points[a].CompareTo(points[b]));

            Vector2[] ordered_verts = [.. ordered_idx.Select(i => points[i])];

            List<Vector2> upper_convex_hull = new(points.Count);
            List<int> upper_convex_hull_idx = new(points.Count);

            List<Vector2> lower_convex_hull = new(points.Count);
            List<int> lower_convex_hull_idx = new(points.Count);

            int iTestVert = 1;
            upper_convex_hull.Add(ordered_verts[0]);
            upper_convex_hull_idx.Add(ordered_idx[0]);

            lower_convex_hull.Add(ordered_verts[0]);
            lower_convex_hull_idx.Add(ordered_idx[0]);

            //Our Starting vertex for the top hull is the highest point, but they are sorted so Y is the smallest value if there are two X's at the minimum value
            while (ordered_verts[iTestVert - 1].X == ordered_verts[iTestVert].X)
            {
                upper_convex_hull.Add(ordered_verts[iTestVert]);
                upper_convex_hull_idx.Add(ordered_idx[iTestVert]);
                iTestVert++;

                if (iTestVert >= points.Count)
                {
                    originalIndices = ordered_idx;
                    return [.. upper_convex_hull];
                }
            }

            int iStartVert = iTestVert;

            //OK, build triangles and determine orientation
            while (true)
            {
                if (TryAddVertexToHull(iTestVert, true, ordered_verts, ordered_idx, ref upper_convex_hull, ref upper_convex_hull_idx))
                {
                    iTestVert++;
                    if (iTestVert >= points.Count)
                        break;
                }
            }

            iTestVert = 1;

            //OK, build triangles and determine orientation
            while (true)
            {
                if (TryAddVertexToHull(iTestVert, false, ordered_verts, ordered_idx, ref lower_convex_hull, ref lower_convex_hull_idx))
                {
                    iTestVert++;
                    if (iTestVert >= points.Count)
                        break;
                }
            }

            //Remove the last point added to the upper hull.  It will be duplicated on the lower hull
            upper_convex_hull.RemoveAt(upper_convex_hull.Count - 1);
            upper_convex_hull_idx.RemoveAt(upper_convex_hull_idx.Count - 1);

            //Reverse the lower hull so the counter-clockwise order is preserved
            lower_convex_hull.Reverse();
            lower_convex_hull_idx.Reverse();

            upper_convex_hull.AddRange(lower_convex_hull);
            upper_convex_hull_idx.AddRange(lower_convex_hull_idx);

            originalIndices = [.. upper_convex_hull_idx];
            return [.. upper_convex_hull];
        }

        /// <summary>
        /// Return true if the point was added to the convex hull.  Return false if the point before was removed from the convex hull and iTestVert needs to be tested again
        /// </summary>
        /// <param name="iTestVert"></param>
        /// <param name="TestUpperHull">True if we are adding points to the upper hull.  False if adding to lower hull</param>
        /// <param name="ordered_verts"></param>
        /// <param name="ordered_idx"></param>
        /// <param name="convex_hull"></param>
        /// <param name="convex_hull_idx"></param>
        /// <returns></returns>
        private static bool TryAddVertexToHull(int iTestVert, bool TestUpperHull, Vector2[] ordered_verts, int[] ordered_idx, ref List<Vector2> convex_hull, ref List<int> convex_hull_idx)
        {
            if (convex_hull.Count >= 2)
            {
                Vector2 v0 = ordered_verts[iTestVert];
                Vector2 v1 = convex_hull.Last();
                Vector2 v2 = convex_hull[convex_hull.Count - 2];

                //Triangle tri = new Geometry.Triangle(v0, v1, v2);

                //bool ConvexTriangleForUpperHull = tri.VectorProducts > 0;
                var winding = Vector2Extensions.Winding([v0, v1, v2]);
                bool ConvexTriangleForUpperHull =
                    winding == RotationDirection.Clockwise;
                bool ConvexTriangle = (TestUpperHull ? ConvexTriangleForUpperHull : !ConvexTriangleForUpperHull) ||
                                      winding == RotationDirection.Colinear;

                if (ConvexTriangle)
                {
                    convex_hull.Add(ordered_verts[iTestVert]);
                    convex_hull_idx.Add(ordered_idx[iTestVert]);
                    return true;
                }
                else
                {
                    convex_hull.RemoveAt(convex_hull.Count - 1);
                    convex_hull_idx.RemoveAt(convex_hull_idx.Count - 1);
                    return false;
                }
            }
            else
            {
                convex_hull.Add(ordered_verts[iTestVert]);
                convex_hull_idx.Add(ordered_idx[iTestVert]);
                return true;
            }
        }

    }
}
