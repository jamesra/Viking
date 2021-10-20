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
        public static GridPolygon ConvexHull(this GridPolygon[] Polygons)
        {
            GridVector2[] AllPoints = Polygons.Where(poly => poly != null).SelectMany(poly => poly.ExteriorRing.EnsureOpenRing()).ToArray();

            if (AllPoints.Length < 3)
                return null;

            int[] original_indicies;
            GridVector2[] EntireSetConvexHull = AllPoints.ConvexHull(out original_indicies);
            return new GridPolygon(EntireSetConvexHull);
        }

        public static GridVector2[] ConvexHull(this IReadOnlyList<GridVector2> points)
        {
            int[] original_indicies;
            return ConvexHull(points, out original_indicies);
        }

        /// <summary>
        /// Return the convex hull of a set of points
        /// </summary>
        /// <param name="points"></param>
        /// <param name="original_indicies"></param>
        /// <returns></returns>
        public static GridVector2[] ConvexHull(this IReadOnlyList<GridVector2> points, out int[] original_indicies)
        {
            int[] ordered_idx = points.Select((p, i) => i).ToArray();

            if (points.Count == 0)
            {
                original_indicies = Array.Empty<int>();
                return Array.Empty<GridVector2>();
            }

            if (points.Count == 1)
            {
                original_indicies = ordered_idx;
                return points.ToArray();
            }

            //If the points are a cycle, then make each point unique
            if (points[0] == points[points.Count-1])
            {
                if (points.Count <= 4)
                {
                    original_indicies = ordered_idx;
                    return points.ToArray(); //All points are on convex hull
                }

                GridVector2[] newArray = new GridVector2[points.Count - 1];
                Array.Copy(points.ToArray(), newArray, newArray.Length);
                points = newArray;
                ordered_idx = points.Select((p, i) => i).ToArray();
            }
            else if (points.Count <= 3)
            {
                GridVector2[] ring_points = points.ToArray().EnsureClosedRing();
                List<int> list_original_indicies = new List<int>(ordered_idx)
                {
                    0
                };
                original_indicies = list_original_indicies.ToArray();
                return ring_points;
            }



            //I've seen bugs with this code for very large numbers.  So I center the points on the centroid


            //Sort and return the index of original points
            Array.Sort<int>(ordered_idx, (a, b) => points[a].CompareTo(points[b]));

            GridVector2[] ordered_verts = ordered_idx.Select(i => points[i]).ToArray();

            List<GridVector2> upper_convex_hull = new List<Geometry.GridVector2>(points.Count);
            List<int> upper_convex_hull_idx = new List<int>(points.Count);

            List<GridVector2> lower_convex_hull = new List<Geometry.GridVector2>(points.Count);
            List<int> lower_convex_hull_idx = new List<int>(points.Count);

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
                    original_indicies = ordered_idx;
                    return upper_convex_hull.ToArray();
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

            original_indicies = upper_convex_hull_idx.ToArray();
            return upper_convex_hull.ToArray();
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
        private static bool TryAddVertexToHull(int iTestVert, bool TestUpperHull, GridVector2[] ordered_verts, int[] ordered_idx, ref List<GridVector2> convex_hull, ref List<int> convex_hull_idx)
        {
            if (convex_hull.Count >= 2)
            {
                GridVector2 v0 = ordered_verts[iTestVert];
                GridVector2 v1 = convex_hull.Last();
                GridVector2 v2 = convex_hull[convex_hull.Count - 2];

                GridTriangle tri = new Geometry.GridTriangle(v0, v1, v2);

                //bool ConvexTriangleForUpperHull = tri.VectorProducts > 0;
                bool ConvexTriangleForUpperHull = GridVector2Extensions.AreClockwise(new GridVector2[] { v0, v1, v2 });
                bool ConvexTriangle = TestUpperHull ? ConvexTriangleForUpperHull : !ConvexTriangleForUpperHull;

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
