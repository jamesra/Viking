using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Geometry.Meshing;

namespace Geometry
{
    public static class Delaunay2D
    {
        public static int[] Triangulate(GridVector2[] points)
        { 
            GridVector2[] BoundingPoints = GetBounds(points);
            return Delaunay2D.Triangulate(points, BoundingPoints); 
        }

        public static int[] Triangulate(GridVector2[] points, GridRectangle bounds)
        {
            double WidthMargin = bounds.Width;
            double HeightMargin = bounds.Height;
            GridVector2[] BoundingPoints = new GridVector2[] { new GridVector2(bounds.Left - WidthMargin, bounds.Bottom - HeightMargin), 
                                                               new GridVector2(bounds.Right + WidthMargin, bounds.Bottom - HeightMargin), 
                                                               new GridVector2(bounds.Left - WidthMargin, bounds.Top +  HeightMargin), 
                                                               new GridVector2(bounds.Right + WidthMargin, bounds.Top + HeightMargin)}; 
            return Delaunay2D.Triangulate(points, BoundingPoints);
        }

        public static int[] TriangulateLeavingBorders(GridVector2[] points, GridRectangle bounds)
        {
            double WidthMargin = bounds.Width;
            double HeightMargin = bounds.Height;
            GridVector2[] BoundingPoints = new GridVector2[] { new GridVector2(bounds.Left - WidthMargin, bounds.Bottom - HeightMargin), 
                                                               new GridVector2(bounds.Right + WidthMargin, bounds.Bottom - HeightMargin), 
                                                               new GridVector2(bounds.Left - WidthMargin, bounds.Top +  HeightMargin), 
                                                               new GridVector2(bounds.Right + WidthMargin, bounds.Top + HeightMargin)};
            return Delaunay2D.Triangulate(points, BoundingPoints);
        }

        /// <summary>
        /// Generates the delaunay triangulation for a list of points. 
        /// Requires the points to be sorted on the X-axis coordinate!
        /// Every the integers in the returned array are the indicies in the passes array of triangles. 
        /// Implemented based upon: http://local.wasp.uwa.edu.au/~pbourke/papers/triangulate/
        /// "Triangulate: Efficient Triangulation Algorithm Suitable for Terrain Modelling"
        /// by Paul Bourke
        /// </summary>
        public static int[] Triangulate(GridVector2[] points, GridVector2[] BoundingPoints)
        {
            if (BoundingPoints == null)
            {
                throw new ArgumentNullException("BoundingPoints");
            }

            if (points == null)
            {
                throw new ArgumentNullException("points");
            }

            if (points.Length < 3)
                return new int[0]; 

#if DEBUG
            
            //Check to ensure the input is really sorted on the X-Axis
            for (int iDebug = 1; iDebug < points.Length; iDebug++)
            {
                Debug.Assert(points[iDebug - 1].X <= points[iDebug].X);
                Debug.Assert(GridVector2.Distance(points[iDebug - 1], points[iDebug]) >= Global.Epsilon);
            } 
#endif             

            List<GridIndexTriangle> triangles = new List<GridIndexTriangle>(points.Length);

            //Safe triangles have a circle with a center.X+radius which is less than the current point.
            //This means they can never intersect with a new point and we never need to test them again.
            List<GridIndexTriangle> safeTriangles = new List<GridIndexTriangle>();

            int iNumPoints = points.Length;
            GridVector2[] allpoints = new GridVector2[iNumPoints + 4];

            points.CopyTo(allpoints, 0);
            BoundingPoints.CopyTo(allpoints, iNumPoints); 

            //Initialize bounding triangles
            triangles.AddRange(new GridIndexTriangle[] { new GridIndexTriangle(iNumPoints, iNumPoints + 1, iNumPoints + 2, ref allpoints),
                                                         new GridIndexTriangle(iNumPoints + 1, iNumPoints + 2, iNumPoints + 3, ref allpoints) });

            IndexEdge[] Edges = new IndexEdge[(triangles.Count * 3) * 2];
            for(int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                GridVector2 P = points[iPoint];
                
                //Use preallocated buffer if we can, otherwise expand it
                int maxEdges = triangles.Count * 3;
                if (Edges.Length < maxEdges)
                    Edges = new IndexEdge[maxEdges * 2]; 

                int iTri = 0;
                int iEdge = 0; 
                while(iTri < triangles.Count)
                {
                    GridIndexTriangle tri = triangles[iTri];
                    GridCircle circle = tri.Circle;
                    if (circle.Contains(P))
                    {
                        Edges[iEdge++] = new IndexEdge(tri.i1, tri.i2);
                        Edges[iEdge++] = new IndexEdge(tri.i2, tri.i3);
                        Edges[iEdge++] = new IndexEdge(tri.i3, tri.i1); 

/*                        Edges.AddRange(new IndexEdge[] {new IndexEdge(tri.i1, tri.i2), 
                                                               new IndexEdge(tri.i2, tri.i3),
                                                               new IndexEdge(tri.i3, tri.i1)});
                        */
                        triangles.RemoveAt(iTri);
                    }
                    //Check if the triangle is safe from ever intersecting with a new point again
                    else if (circle.Center.X + circle.Radius + Global.Epsilon < P.X)
                    {
                        safeTriangles.Add(tri);
                        triangles.RemoveAt(iTri);
                    } 
                    else
                    {
                        iTri++;
                    }
                }

                //Record how many edges there are
                int numEdges = iEdge; 

                //Remove duplicates from edge buffer
                //This is easier with a list, but arrays were faster
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

                //Add triangles with the remaining edges
                for(iEdge = 0; iEdge < numEdges; iEdge++)
                {
                    IndexEdge E = Edges[iEdge]; 

                    if (!E.IsValid)
                        continue;

                    GridIndexTriangle newTri = new GridIndexTriangle(E.iA, E.iB, iPoint, ref allpoints);
                    triangles.Add(newTri); 

         
#if DEBUG
                    //Check to make sure the new triangle intersects the point.  This is a slow test.
                    Debug.Assert(((GridTriangle)newTri).Contains(P));
#endif
                }
            }

            //Return all the safe triangles to the triangles list 
            triangles.AddRange(safeTriangles);

            //Yank all the triangles that are part of the bounding triangles
            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                if (tri.i1 >= iNumPoints ||
                   tri.i2 >= iNumPoints ||
                   tri.i3 >= iNumPoints)
                {
                    triangles.RemoveAt(iTri);
                    iTri--;
                }
            }            

            //Build a list of triangle indicies to return
            int[] TriangleIndicies = new int[triangles.Count * 3];
            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                int iPoint = iTri * 3;
                TriangleIndicies[iPoint] = tri.i1;
                TriangleIndicies[iPoint + 1] = tri.i2;
                TriangleIndicies[iPoint + 2] = tri.i3; 
            }

            return TriangleIndicies;
        }

        static GridVector2[] GetBounds(GridVector2[] points)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < points.Length; i++)
            {
                minX = Math.Min(minX, points[i].X);
                maxX = Math.Max(maxX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxY = Math.Max(maxY, points[i].Y);
            }

            double width = maxX - minX;
            double height = maxY - minY; 

            //We don't want to add duplicate points by mistake, so move boundaries out a bit
            minX -= width;
            maxX += width;
            minY -= height;
            maxY += height;

            GridVector2 BotLeft = new GridVector2(minX, minY); 
            GridVector2 BotRight = new GridVector2(maxX, minY); 
            GridVector2 TopLeft = new GridVector2(minX, maxY); 
            GridVector2 TopRight = new GridVector2(maxX, maxY);

            return new GridVector2[] { BotLeft, BotRight, TopLeft, TopRight}; 
        }
    }
}
