using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Geometry
{
    /// <summary>
    /// Incremental Bowyer–Watson Delaunay returning triangle indices.
    /// For a mesh (polygon CDT, medial axis) use <see cref="GenericDelaunayMeshGenerator2D{VERTEX}"/>
    /// (polygon rings: MeshExtensions.Triangulate).
    /// </summary>
    public static class Delaunay2D
    {
        public static int[] Triangulate(Vector2[] points)
        {
            Vector2[] BoundingPoints = GetCorners(points);
            return Delaunay2D.Triangulate(points, BoundingPoints);
        }

        public static int[] Triangulate(Vector2[] points, in Rectangle bounds)
        {
            double WidthMargin = bounds.Width;
            double HeightMargin = bounds.Height;
            Vector2[] BoundingPoints = [ new(bounds.Left - WidthMargin, bounds.Bottom - HeightMargin),
                                                               new(bounds.Right + WidthMargin, bounds.Bottom - HeightMargin),
                                                               new(bounds.Left - WidthMargin, bounds.Top +  HeightMargin),
                                                               new(bounds.Right + WidthMargin, bounds.Top + HeightMargin)];
            return Delaunay2D.Triangulate(points, BoundingPoints);
        }

        public static int[] TriangulateLeavingBorders(Vector2[] points, in Rectangle bounds)
        {
            double WidthMargin = bounds.Width;
            double HeightMargin = bounds.Height;
            Vector2[] BoundingPoints = [ new(bounds.Left - WidthMargin, bounds.Bottom - HeightMargin),
                                                               new(bounds.Right + WidthMargin, bounds.Bottom - HeightMargin),
                                                               new(bounds.Left - WidthMargin, bounds.Top +  HeightMargin),
                                                               new(bounds.Right + WidthMargin, bounds.Top + HeightMargin)];
            return Delaunay2D.Triangulate(points, BoundingPoints);
        }

        /// <summary>
        /// Incremental Bowyer–Watson Delaunay triangulation. Vertex indices in the result refer to
        /// the input <paramref name="points"/> array (plus four bounding-box corners used internally).
        /// </summary>
        /// <remarks>
        /// Bowyer, "Computing Dirichlet tessellations," Comput. J. 24(2):162–166 (1981);
        /// Watson, "Computing the n-dimensional Delaunay tessellation with application to Voronoi
        /// polytopes," Comput. J. 24(2):167–172 (1981). Implementation follows Paul Bourke,
        /// "Triangulate: Efficient Triangulation Algorithm Suitable for Terrain Modelling,"
        /// https://paulbourke.net/papers/triangulate/
        /// Points are sorted on X internally; duplicates closer than <see cref="Global.Epsilon"/> throw.
        /// </remarks>
        public static int[] Triangulate(Vector2[] points, Vector2[] BoundingPoints) => TriangulateCore(points, BoundingPoints, PairwiseEdgeLimit);

        /// <summary>
        /// Cavities with at most this many boundary edges are de-duplicated by comparing every pair, which beats a hash for
        /// the handful of edges a typical insertion produces.
        /// </summary>
        private const int PairwiseEdgeLimit = 48;

        /// <summary>
        /// <see cref="Triangulate(Vector2[], Vector2[])"/> with the pairwise de-duplication limit exposed so tests can force
        /// either de-duplication path. Both paths return the same triangles in the same order.
        /// </summary>
        /// <param name="pairwiseEdgeLimit">Cavities with more edges than this use the hashed de-duplication</param>
        internal static int[] TriangulateCore(Vector2[] points, Vector2[] BoundingPoints, int pairwiseEdgeLimit)
        {
            if (BoundingPoints is null)
            {
                throw new ArgumentNullException(nameof(BoundingPoints));
            }

            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Length < 3)
                return [];

            Vector2[] sortedPoints = (Vector2[])points.Clone();
            Array.Sort(sortedPoints, new Vector2Comparer(xyOrder: true));

            for (int i = 1; i < sortedPoints.Length; i++)
            {
                if (Vector2.Distance(in sortedPoints[i - 1], in sortedPoints[i]) < Global.Epsilon)
                    throw new ArgumentException($"Duplicate points, this breaks delaunay: #{i - 1} and #{i}");
            }

#if DEBUG

            //Check to ensure the input is really sorted on the X-Axis
            for (int iDebug = 1; iDebug < sortedPoints.Length; iDebug++)
            {
                if(sortedPoints[iDebug - 1].X > sortedPoints[iDebug].X)
                    throw new ArgumentException($"Points not sorted on X axis: #{iDebug - 1} and #{iDebug}");

                if(Vector2.Distance(in sortedPoints[iDebug - 1], in sortedPoints[iDebug]) < Global.Epsilon)
                    throw new ArgumentException($"Duplicate points, this breaks delaunay: #{iDebug - 1} and #{iDebug}");
            }
#endif

            points = sortedPoints;

            List<GridIndexTriangle> triangles = new(points.Length);

            //Safe triangles have a circle with a center.X+radius which is less than the current point.
            //This means they can never intersect with a new point and we never need to test them again.
            List<GridIndexTriangle> safeTriangles = [];

            int iNumPoints = points.Length;
            Vector2[] allpoints = new Vector2[iNumPoints + 4];

            points.CopyTo(allpoints, 0);
            BoundingPoints.CopyTo(allpoints, iNumPoints);

            //Initialize bounding triangles
            triangles.AddRange([new(iNumPoints, iNumPoints + 1, iNumPoints + 2, ref allpoints),
                                new(iNumPoints + 1, iNumPoints + 2, iNumPoints + 3, ref allpoints)]);

            IndexEdge[] Edges = new IndexEdge[(triangles.Count * 3) * 2];
            Dictionary<long, int> pendingEdges = [];
            for (int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                Vector2 P = points[iPoint];

                //Use preallocated buffer if we can, otherwise expand it
                int maxEdges = triangles.Count * 3;
                if (Edges.Length < maxEdges)
                    Edges = new IndexEdge[maxEdges * 2];

                //Compact the list in place. The survivors keep their relative order, which is the order RemoveAt produced,
                //and the order of the final triangle array depends on it.
                int iEdge = 0;
                int triangleCount = triangles.Count;
                int iKept = 0;
                for (int iTri = 0; iTri < triangleCount; iTri++)
                {
                    GridIndexTriangle tri = triangles[iTri];
                    Circle circle = tri.Circle;
                    if (circle.Covers(in P))
                    {
                        Edges[iEdge++] = new IndexEdge(tri.i1, tri.i2);
                        Edges[iEdge++] = new IndexEdge(tri.i2, tri.i3);
                        Edges[iEdge++] = new IndexEdge(tri.i3, tri.i1);
                    }
                    //Check if the triangle is safe from ever intersecting with a new point again
                    else if (circle.Center.X + circle.Radius + Global.Epsilon < P.X)
                    {
                        safeTriangles.Add(tri);
                    }
                    else
                    {
                        if (iKept != iTri)
                            triangles[iKept] = tri;

                        iKept++;
                    }
                }

                if (iKept != triangleCount)
                    triangles.RemoveRange(iKept, triangleCount - iKept);

                //Record how many edges there are
                int numEdges = iEdge;

                //Edges on the cavity boundary appear once; edges inside it appear twice and cancel. An edge seen more than
                //twice cancels in consecutive pairs (first with second, third with fourth), and an odd one out stays.
                if (numEdges <= pairwiseEdgeLimit)
                {
                    for (int iA = 0; iA < numEdges; iA++)
                    {
                        if (Edges[iA].IsValid == false)
                            continue;

                        for (int iB = iA + 1; iB < numEdges; iB++)
                        {
                            if (Edges[iB].IsValid == false)
                                continue;

                            if (Edges[iA] == Edges[iB])
                            {
                                Edges[iB].IsValid = false;
                                Edges[iA].IsValid = false;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    pendingEdges.Clear();
                    for (int iA = 0; iA < numEdges; iA++)
                    {
                        long key = ((long)Edges[iA].iA << 32) | (uint)Edges[iA].iB;
                        if (pendingEdges.TryGetValue(key, out int iFirst))
                        {
                            Edges[iFirst].IsValid = false;
                            Edges[iA].IsValid = false;
                            pendingEdges.Remove(key);
                        }
                        else
                        {
                            pendingEdges.Add(key, iA);
                        }
                    }
                }

                //Add triangles with the remaining edges
                for (iEdge = 0; iEdge < numEdges; iEdge++)
                {
                    IndexEdge E = Edges[iEdge];

                    if (!E.IsValid)
                        continue;

                    GridIndexTriangle newTri = new(E.iA, E.iB, iPoint, ref allpoints);
                    triangles.Add(newTri);


#if DEBUG
                    //Check to make sure the new triangle intersects the point.  This is a slow test.
                    Debug.Assert(((Triangle)newTri).Covers(P));
#endif
                }
            }

            //Return all the safe triangles to the triangles list 
            triangles.AddRange(safeTriangles);

            //Skip the triangles that are part of the bounding triangles
            int keptTriangles = 0;
            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                if (tri.i1 < iNumPoints &&
                   tri.i2 < iNumPoints &&
                   tri.i3 < iNumPoints)
                {
                    keptTriangles++;
                }
            }

            //Build a list of triangle indicies to return
            int[] TriangleIndicies = new int[keptTriangles * 3];
            int iOut = 0;
            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                if (tri.i1 >= iNumPoints ||
                   tri.i2 >= iNumPoints ||
                   tri.i3 >= iNumPoints)
                {
                    continue;
                }

                TriangleIndicies[iOut] = tri.i1;
                TriangleIndicies[iOut + 1] = tri.i2;
                TriangleIndicies[iOut + 2] = tri.i3;
                iOut += 3;
            }

            return TriangleIndicies;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <returns>[BotLeft, BotRight, TopLeft, TopRight]</returns>
        static Vector2[] GetCorners(Vector2[] points)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < points.Length; i++)
            {
                minX = points[i].X < minX ? points[i].X : minX;
                maxX = points[i].X > maxX ? points[i].X : maxX;
                minY = points[i].Y < minY ? points[i].Y : minY;
                maxY = points[i].Y > maxY ? points[i].Y : maxY;
            }

            double width = maxX - minX;
            double height = maxY - minY;

            //We don't want to add duplicate points by mistake, so move boundaries out a bit
            minX -= width;
            maxX += width;
            minY -= height;
            maxY += height;

            Vector2 BotLeft = new(minX, minY);
            Vector2 BotRight = new(maxX, minY);
            Vector2 TopLeft = new(minX, maxY);
            Vector2 TopRight = new(maxX, maxY);

            return [BotLeft, BotRight, TopLeft, TopRight];
        }
    }
}
