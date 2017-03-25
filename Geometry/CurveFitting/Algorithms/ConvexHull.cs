using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public static class ConvexHull
    {

        public static int[] Hull(GridVector2[] points)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");
            }

            //Find the points we know are on the hull
            List<int> BorderIndicies = FindExtremes(points);

            List<GridVector2> BorderPoints = new List<GridVector2>(4);
            for (int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                BorderPoints.Add(points[iPoint]); 
            }

            //A list of points we know are not on the hull
            bool[] PointsToExclude = new bool[points.Length];

            List<GridTriangle> listBoundingTriangles = new List<GridTriangle>(0);
            if (BorderPoints.Count > 3)
            {
                GridTriangle tri = new GridTriangle(BorderPoints[2],
                                                    BorderPoints[3],
                                                    BorderPoints[4]);
                listBoundingTriangles.Add(tri);
            }

            if (BorderPoints.Count > 2)
            {
                GridTriangle tri = new GridTriangle(BorderPoints[1],
                                                    BorderPoints[2],
                                                    BorderPoints[3]);
                listBoundingTriangles.Add(tri);
            }

            //If we can make a polygon then exclude all points inside the region from consideration
            if (BorderPoints.Count > 2)
            {
                for (int iPoint = 0; iPoint < points.Length; iPoint++)
                {
                    
                    if (BorderIndicies.Contains(iPoint))
                        continue;

                    GridVector2 Point = points[iPoint];

                    for (int iTriangle = 0; iTriangle < listBoundingTriangles.Count; iTriangle++)
                    {
                        if (listBoundingTriangles[iTriangle].Contains(Point))
                        {
                            PointsToExclude[iPoint] = true; 
                        }
                    }
                }
            }

            //

            return new int[0];

        }

        private static List<int> FindExtremes(GridVector2[] points)
        {
            int iMinX = 0;
            int iMinY = 0; 
            int iMaxX = 0;
            int iMaxY = 0;

            for(int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                GridVector2 point = points[iPoint];
                if (point.X < points[iMinX].X)
                    iMinX = iPoint;
                if (point.X > points[iMaxX].X)
                    iMaxX = iPoint;
                if (point.Y < points[iMinY].Y)
                    iMinX = iPoint;
                if (point.Y > points[iMaxY].Y)
                    iMaxY = iPoint; 
            }

            List<int> ListExtremes = new List<int>( new int[]{ iMinX, iMinY, iMaxX, iMaxY} ); 
            ListExtremes.Sort(); 
            for(int iPoint = 1; iPoint < ListExtremes.Count; iPoint++)
            {
                if(ListExtremes[iPoint] == ListExtremes[iPoint-1])
                {
                    ListExtremes.RemoveAt(iPoint); 
                    iPoint--; 
                }
            }

            return ListExtremes;
        }
         
    }
}
