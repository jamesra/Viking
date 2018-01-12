using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RTree;

namespace Geometry
{
    public static class ShapeExtensions
    {
        public static GridVector2 Convert(this IPoint2D p)
        {
            if (p is GridVector2)
            {
                return (GridVector2)p;
            }

            return new GridVector2(p.X, p.Y);
        }

        public static GridLineSegment Convert(this ILineSegment2D line)
        {
            if (line is GridLineSegment)
            {
                return (GridLineSegment)line;
            }

            return new GridLineSegment(line.A, line.B);
        }

        public static GridCircle Convert(this ICircle2D c)
        {
            if (c is GridCircle)
            {
                return (GridCircle)c;
            }

            return new GridCircle(c.Center, c.Radius);
        }

        public static GridTriangle Convert(this ITriangle2D t)
        {
            if (t is GridTriangle)
            {
                return (GridTriangle)t;
            }

            GridVector2[] points = t.Points.Select(p => p.Convert()).ToArray();
            return new GridTriangle(points[0], points[1], points[2]);
        }

        public static GridRectangle Convert(this IRectangle r)
        {
            if (r is GridRectangle)
            {
                return (GridRectangle)r;
            }

            return new GridRectangle(r.Left, r.Right, r.Bottom, r.Top);
        }


        public static GridPolygon Convert(this IPolygon2D poly)
        {
            if (poly is GridPolygon)
            {
                return poly as GridPolygon;
            }

            return new GridPolygon(poly.ExteriorRing, poly.InteriorRings);
        }

        public static bool Intersects(this IShape2D shape, IShape2D other)
        {
            if (false == shape.BoundingBox.Intersects(other.BoundingBox))
                return false;

            switch (shape.ShapeType)
            {
                case ShapeType2D.POINT:
                    return PointIntersects(shape as IPoint2D, other);
                case ShapeType2D.CIRCLE:
                    return CircleIntersects(shape as ICircle2D, other);
                case ShapeType2D.RECTANGLE:
                    return RectangleIntersects(shape as IRectangle, other);
                case ShapeType2D.TRIANGLE:
                    return TriangleIntersects(shape as ITriangle2D, other);
                case ShapeType2D.LINE:
                    return LineIntersects(shape as ILineSegment2D, other);
                case ShapeType2D.POLYGON:
                    return PolygonIntersects(shape as IPolygon2D, other);
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            } 
        }

        private static bool PointIntersects(IPoint2D point, IShape2D other)
        {
            return other.Contains(point);
        }

        internal static bool CircleIntersects(ICircle2D c, IShape2D other)
        {
            GridCircle circle = new GridCircle(c.Center, c.Radius);
            return CircleIntersects(circle, other);
        }

        internal static bool CircleIntersects(GridCircle circle, IShape2D other)
        {
            switch (other.ShapeType)
            {
                case ShapeType2D.POINT:
                    return circle.Contains(other as IPoint2D);
                case ShapeType2D.LINE:
                    return circle.Intersects(other as ILineSegment2D);
                case ShapeType2D.CIRCLE:
                    ICircle2D other_circle = other as ICircle2D;
                    return circle.Intersects(new GridCircle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.TRIANGLE:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return circle.Intersects(other_tri.Convert());
                case ShapeType2D.POLYGON:
                    return circle.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.RECTANGLE:
                    return circle.Intersects(((GridRectangle)other).Convert());
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            }
        }

        internal static bool RectangleIntersects(IRectangle r, IShape2D other)
        {
            GridRectangle rect = r.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.POINT:
                    return rect.Contains(other as IPoint2D);
                case ShapeType2D.LINE:
                    return rect.Intersects(other as ILineSegment2D);
                case ShapeType2D.CIRCLE:
                    ICircle2D other_circle = other as ICircle2D;
                    return rect.Intersects(new GridCircle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.TRIANGLE:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return rect.Intersects(other_tri.Convert());
                case ShapeType2D.POLYGON:
                    return rect.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.RECTANGLE:
                    return rect.Intersects(((GridRectangle)other).Convert());
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            }
        }

        internal static bool TriangleIntersects(ITriangle2D t, IShape2D other)
        {
            GridTriangle tri = t.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.POINT:
                    return tri.Contains(other as IPoint2D);
                case ShapeType2D.LINE:
                    return tri.Intersects(other as ILineSegment2D);
                case ShapeType2D.CIRCLE:
                    ICircle2D other_circle = other as ICircle2D;
                    return tri.Intersects(new GridCircle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.TRIANGLE:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return tri.Intersects(other_tri.Convert());
                case ShapeType2D.POLYGON:
                    return tri.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.RECTANGLE:
                    return tri.Intersects(((GridRectangle)other).Convert());
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            }
        }

        internal static bool LineIntersects(ILineSegment2D l, IShape2D other)
        {
            GridLineSegment line = l.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.POINT:
                    return line.Contains(other as IPoint2D);
                case ShapeType2D.LINE:
                    return line.Intersects(other as ILineSegment2D);
                case ShapeType2D.CIRCLE:
                    ICircle2D other_circle = other as ICircle2D;
                    return line.Intersects(new GridCircle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.TRIANGLE:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return line.Intersects(other_tri.Convert());
                case ShapeType2D.POLYGON:
                    return line.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.RECTANGLE:
                    return line.Intersects(((GridRectangle)other).Convert());
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            }
        }

        internal static bool PolygonIntersects(IPolygon2D p, IShape2D other)
        {
            GridPolygon poly = p.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.POINT:
                    return poly.Contains(other as IPoint2D);
                case ShapeType2D.LINE:
                    return poly.Intersects(other as ILineSegment2D);
                case ShapeType2D.CIRCLE:
                    ICircle2D other_circle = other as ICircle2D;
                    return poly.Intersects(new GridCircle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.TRIANGLE:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return poly.Intersects(other_tri.Convert());
                case ShapeType2D.POLYGON:
                    return poly.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.RECTANGLE:
                    return poly.Intersects(((GridRectangle)other).Convert());
                case ShapeType2D.ELLIPSE:
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Return true if any Polygons in the set intersect
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static bool AnyIntersect(this IReadOnlyList<GridPolygon> Polygons)
        {
            for (int i = 0; i < Polygons.Count; i++)
            {
                GridPolygon iPoly = Polygons[i];
                if (iPoly == null)
                    continue;

                for (int j = i + 1; j < Polygons.Count; j++)
                {
                    GridPolygon jPoly = Polygons[j];
                    if (jPoly == null)
                        continue;

                    if (iPoly.Intersects(jPoly))
                        return true;
                }
            }

            return false;
        }
    }

    public static class CircleIntersectionExtensions
    {
        public static bool Intersects(GridCircle circle, GridLineSegment line)
        {
            if (false == line.BoundingBox.Intersects(circle.BoundingBox))
                return false;

            if (circle.Contains(line.A) || circle.Contains(line.B))
                return true;

            if (line.IsNearestPointWithinLineSegment(circle.Center))
            {
                double distanceToLine = line.DistanceToPoint(circle.Center);
                return distanceToLine <= circle.Radius;
            }

            return false;
        }

        public static bool Intersects(GridCircle circle, GridRectangle rect)
        {
            if (false == circle.BoundingBox.Intersects(rect))
            {
                return false;
            }

            if (rect.Contains(circle.Center))
                return true;

            if (circle.Contains(rect.LowerLeft) || circle.Contains(rect.LowerRight) ||
                circle.Contains(rect.UpperLeft) || circle.Contains(rect.UpperRight))
                return true;

            return false;
        }

        public static bool Intersects(GridCircle circle, GridTriangle tri)
        {
            if (false == circle.BoundingBox.Intersects(tri.BoundingBox))
                return false;

            //Do any triangle verts fall inside our circle?
            if (circle.Contains(tri.p1) || circle.Contains(tri.p2) || circle.Contains(tri.p3))
                return true;

            //Is the center of our circle inside the triangle?
            if (tri.Contains(circle.Center))
                return true;

            //Do any triangle line segments intersect our circle?
            foreach (GridLineSegment line in tri.Segments)
            {
                if (circle.Intersects(line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(GridCircle circle, GridPolygon poly)
        {
            if (false == circle.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            //Do any triangle verts fall inside our circle? 
            foreach (GridVector2 p in poly.ExteriorRing)
            {
                if (circle.Contains(p))
                    return true;
            }

            //Is the center of our circle inside the triangle?
            if (poly.Contains(circle.Center))
                return true;

            //Do any exterior line segments intersect our circle?
            List<GridLineSegment> Candidates = poly.ExteriorSegmentRTree.Intersects(circle.BoundingBox.ToRTreeRect(0));
            foreach (GridLineSegment line in Candidates)
            {
                if (circle.Intersects(line))
                    return true;
            }

            //Do any interior line segments intersect our circle?
            //Doesn't work with nested inner polygons...
            GridPolygon inner_poly = null;
            if (poly.InteriorPolygonContains(circle.Center, out inner_poly))
            {
                Candidates = inner_poly.ExteriorSegmentRTree.Intersects(circle.BoundingBox.ToRTreeRect(0));
                foreach (GridLineSegment line in Candidates)
                {
                    if (circle.Intersects(line))
                        return true;
                }                
            }

            return false;
        }
    }

    public static class RectangleIntersectionExtensions
    {
        public static bool Intersects(GridRectangle rect, GridCircle circle)
        {
            return CircleIntersectionExtensions.Intersects(circle, rect);
        }

        public static bool Intersects(GridRectangle rect, GridLineSegment line)
        {
            if (false == line.BoundingBox.Intersects(rect))
                return false;

            if (rect.Contains(line.A) || rect.Contains(line.B))
                return true;

            foreach (GridLineSegment rect_line in rect.Segments)
            {
                if (rect_line.Intersects(line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(GridRectangle rect, GridTriangle tri)
        {
            if (false == tri.BoundingBox.Intersects(rect))
                return false;

            if (rect.Contains(tri.p1) || rect.Contains(tri.p2) || rect.Contains(tri.p3))
                return true;

            foreach (GridLineSegment tri_line in tri.Segments)
            {
                if (rect.Intersects(tri_line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(GridRectangle rect, GridPolygon poly)
        {
            if (false == poly.BoundingBox.Intersects(rect))
                return false;

            foreach (GridVector2 p in poly.ExteriorRing)
            {
                if (rect.Contains(p))
                    return true;
            }

            foreach (GridVector2 p in rect.Corners)
            {
                if (poly.Contains(p))
                    return true;
            }

            List<GridLineSegment> Candidates = poly.ExteriorSegmentRTree.Intersects(rect.ToRTreeRect(0));
            foreach (GridLineSegment line in Candidates)
            {
                if (rect.Intersects(line))
                    return true;
            }

            return false;
        }
    }

    public static class TriangleIntersectionExtensions
    {
        public static bool Intersects(GridTriangle tri, GridCircle circle)
        {
            return CircleIntersectionExtensions.Intersects(circle, tri);
        }

        public static bool Intersects(GridTriangle tri, GridRectangle rect)
        {
            return RectangleIntersectionExtensions.Intersects(rect, tri);
        }

        public static bool Intersects(GridTriangle tri, GridLineSegment line)
        {
            if (false == tri.BoundingBox.Intersects(line.BoundingBox))
                return false;

            if (tri.Contains(line.A) || tri.Contains(line.B))
                return true;

            foreach(GridLineSegment tri_line in tri.Segments)
            {
                if (line.Intersects(tri_line))
                    return true;
            }

            return false;
        } 

        public static bool Intersects(GridTriangle tri, GridPolygon poly)
        {
            if (false == tri.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            foreach(GridVector2 p in poly.ExteriorRing)
            {
                if (tri.Contains(p))
                    return true;
            }

            foreach(GridVector2 p in tri.Points)
            {
                if (poly.Contains(p))
                    return true;
            }

            ///Check in case a triangle vertex falls inside an interior polygon
            foreach(GridLineSegment line in tri.Segments)
            {
                if (poly.Intersects(line))
                    return true;
            }

            return false; 
        }
    }

    public static class LineIntersectionExtensions
    {
        public static bool Intersects(GridLineSegment line, GridCircle circle)
        {
            return CircleIntersectionExtensions.Intersects(circle, line);
        }

        public static bool Intersects(GridLineSegment line, GridRectangle rect)
        {
            return RectangleIntersectionExtensions.Intersects(rect, line);
        }

        public static bool Intersects(GridLineSegment line, GridTriangle tri)
        {
            return TriangleIntersectionExtensions.Intersects(tri, line);
        }

        public static bool Intersects(GridLineSegment line, GridPolygon poly)
        {
            GridVector2 intersection; 
            return Intersects(line, poly, out intersection);
        }

        public static bool Intersects(GridLineSegment line, GridPolygon poly, out GridVector2 intersection)
        {
            intersection = GridVector2.Zero;

            if (false == line.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            List<GridLineSegment> listCandidates = poly.ExteriorSegmentRTree.Intersects(line.BoundingBox.ToRTreeRect(0));

            foreach(GridLineSegment poly_line in listCandidates)
            {
                if (line.Intersects(poly_line, out intersection))
                    return true; 
            }

            //If the point is inside the polygon return 
            if (poly.Contains(line.A) || poly.Contains(line.B))
                return true;

            return false;
        }

        /// <summary>
        /// Add a new point where line intersects any other line
        /// </summary>
        /// <param name="line">Line we add points to</param>
        /// <param name="lines">Lines we are testing for intersection</param>
        /// <param name="IntersectionPoints">The intersection points on the line, in increasing order of distance from line.A to line.B</param>
        /// <returns>The lines that intersect the line parameter</returns>
        public static List<GridLineSegment> Intersections(this GridLineSegment line, IReadOnlyList<GridLineSegment> lines, out GridVector2[] IntersectionPoints)
        {
            RTree.RTree<GridLineSegment> rTree = lines.ToRTree();

            //Cannot use an out parameter in the anonymous method I use below, so I have a bit of redundancy in tracking added points
            List<GridVector2> NewPoints = new List<Geometry.GridVector2>(lines.Count);
            List<GridLineSegment> IntersectingLines = new List<GridLineSegment>(lines.Count);

            foreach (GridLineSegment testLine in lines)
            {
                GridVector2 intersection;
                if (line.Intersects(testLine, out intersection))
                {
                    //Check that NewPoints does not contain the point.  This can occur when the test line intersects exactly over the endpoint of two lines.
                    if (!line.IsEndpoint(intersection) && !NewPoints.Contains(intersection))
                    {
                        NewPoints.Add(intersection);
                        IntersectingLines.Add(testLine);
                    }
                }
            }
            
            double[] dotValues = NewPoints.Select(p => line.Dot(p)).ToArray();
            int[] sortedIndicies = dotValues.SortAndIndex();

            IntersectionPoints = sortedIndicies.Select(i => NewPoints[i]).ToArray();

            return sortedIndicies.Select(i => IntersectingLines[i]).ToList();
        }

        public static List<GridLineSegment> SubdivideAtIntersections(this GridLineSegment line, IReadOnlyList<GridLineSegment> lines, out GridVector2[] IntersectionPoints)
        { 
            List<GridLineSegment> Unused = line.Intersections(lines, out IntersectionPoints);

            List<GridLineSegment> DividedLines = new List<Geometry.GridLineSegment>(IntersectionPoints.Length + 2);
            if(IntersectionPoints.Length == 0)
            {
                DividedLines.Add(line);
                return DividedLines;
            }

            DividedLines.Add(new GridLineSegment(line.A, IntersectionPoints[0]));
            for(int i=0; i < IntersectionPoints.Length - 1; i++)
            {
                DividedLines.Add(new GridLineSegment(IntersectionPoints[i], IntersectionPoints[i + 1]));
            }
            DividedLines.Add(new GridLineSegment(IntersectionPoints.Last(), line.B));

            return DividedLines;
        }

        /// <summary>
        /// Given a set of lines, return a new set of lines where line-line intersections only occur at line endpoints by splitting lines at intersections.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static SortedSet<GridLineSegment> SplitLinesAtIntersections(this IEnumerable<GridLineSegment> lines, out SortedSet<GridVector2> AddedPoints)
        { 
            RTree.RTree<GridLineSegment> rTree = lines.ToRTree();

            IList<GridLineSegment> sortedLines;
            if(lines as IList<GridLineSegment> != null)
            {
                sortedLines = (IList<GridLineSegment>)lines;
            }
            else
            {
                sortedLines = new List<Geometry.GridLineSegment>(lines);
            }

            SortedSet<GridLineSegment> output = new SortedSet<Geometry.GridLineSegment>();

            Stack<GridLineSegment> linesToTest = new Stack<Geometry.GridLineSegment>(lines);

            AddedPoints = new SortedSet<Geometry.GridVector2>();

            while (linesToTest.Count > 0)
            {
                GridLineSegment A = linesToTest.Pop();

                ///Find lines that intersect A, but not on an endpoint of A
                IEnumerable<GridLineSegment> intersections = rTree.Intersects(A.BoundingBox.ToRTreeRect(0)).Where(B => 
                    { 
                        if (B == A)
                            return false;

                        if (B.SharedEndPoint(A))
                            return false;
                           
                        GridVector2 intersection;
                        if (B.Intersects(A, out intersection))
                        {
                            

                            return !(A.A == intersection || A.B == intersection);
                        }
                        else
                        {
                            return false; 
                        }
                    });

                if (!intersections.Any())
                {
                    output.Add(A);
                }
                else
                {
                    //Find the first line we do not intersect on an endpoint of our line
                    GridLineSegment B = intersections.First();

                    GridVector2 intersection;
                    if (B.Intersects(A, out intersection))
                    {
                        AddedPoints.Add(intersection);
                        linesToTest.Push(new GridLineSegment(A.A, intersection));
                        linesToTest.Push(new GridLineSegment(A.B, intersection));
                    }
                }
            }

            return output; 
        }
    }

    public static class PolygonIntersectionExtensions
    {
        public static bool Intersects(GridPolygon poly, GridCircle circle)
        {
            return CircleIntersectionExtensions.Intersects(circle, poly);
        }

        public static bool Intersects(GridPolygon poly, GridRectangle rect)
        {
            return RectangleIntersectionExtensions.Intersects(rect, poly);
        }

        public static bool Intersects(GridPolygon poly, GridTriangle tri)
        {
            return TriangleIntersectionExtensions.Intersects(tri, poly);
        }

        public static bool Intersects(GridPolygon poly, GridLineSegment line)
        {
            return LineIntersectionExtensions.Intersects(line, poly);
        } 
         
        public static bool Intersections(GridPolygon A, GridPolygon B, out GridLineSegment[] AIntersections, out GridLineSegment[] BIntersections)
        {
            if (false == A.BoundingBox.Intersects(B.BoundingBox))
            {
                AIntersections = new GridLineSegment[0];
                BIntersections = new GridLineSegment[0];
                return false;
            }

            List<GridLineSegment> AMatches = new List<Geometry.GridLineSegment>();
            List<GridLineSegment> BMatches = new List<Geometry.GridLineSegment>();

            foreach(GridLineSegment ALine in A.ExteriorSegments)
            {
                bool AAdded = false;
                List<GridLineSegment> BCandidates = B.ExteriorSegmentRTree.Intersects(ALine.BoundingBox.ToRTreeRect(0));
                foreach (GridLineSegment BLine in BCandidates)
                {
                    if(ALine.Intersects(BLine))
                    {
                        BMatches.Add(BLine);
                        if (!AAdded)
                        {
                            AMatches.Add(ALine);
                            AAdded = true;
                        }   
                    }
                }
            }

            AIntersections = AMatches.ToArray();
            BIntersections = BMatches.ToArray();

            return AIntersections.Length > 0 || BIntersections.Length > 0;
        }

        /// <summary>
        /// Create an RTree containing all segments from the borders of the polygon
        /// </summary>
        /// <param name="rTree"></param>
        /// <param name="poly"></param>
        private static void AddPolygonSegmentsToRTree(RTree.RTree<GridLineSegment> rTree, GridPolygon poly)
        {
            foreach (GridLineSegment l in poly.ExteriorSegments)
            {
                rTree.Add(l.BoundingBox.ToRTreeRect(0), l);
            }

            foreach(GridPolygon innerPoly in poly.InteriorPolygons)
            {
                AddPolygonSegmentsToRTree(rTree, innerPoly);
            }
        }

        /// <summary>
        /// Find all the line segments on the polygon borders that intersect line segments in the rTree 
        /// </summary>
        /// <param name="rTree"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        private static List<GridLineSegment> FindIntersectingSegments(RTree.RTree<GridLineSegment> rTree, GridPolygon poly)
        {
            List<GridLineSegment> Intersecting;

            Intersecting = FindIntersectingSegments(rTree, poly.ExteriorSegments);

            foreach (GridPolygon innerPoly in poly.InteriorPolygons)
            {
                Intersecting.AddRange(FindIntersectingSegments(rTree, innerPoly));
            }

            return Intersecting;
        }

        private static List<GridLineSegment> FindIntersectingSegments(RTree.RTree<GridLineSegment> rTree, ICollection<GridLineSegment> segments)
        {
            List<GridLineSegment> Intersecting = new List<Geometry.GridLineSegment>();

            foreach (GridLineSegment l in segments)
            {
                List<GridLineSegment> Candidates = rTree.Intersects(l.BoundingBox.ToRTreeRect(0));

                //Find out if there is a segment that we aren't sharing an endpoint with (part of same polygon border) and is not ourselves
                Intersecting.AddRange(Candidates.Where(c => c != l && !c.SharedEndPoint(l) && c.Intersects(l)));
            }

            return Intersecting;
        }

        /// <summary>
        /// Find all line segments that do not intersect the line segments in the RTree
        /// </summary>
        /// <param name="rTree"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        private static List<GridLineSegment> FindNonIntersectingSegments(RTree.RTree<GridLineSegment> rTree, GridPolygon poly)
        {
            List<GridLineSegment> NonIntersecting;

            NonIntersecting = FindNonIntersectingSegments(rTree, poly.ExteriorSegments);

            foreach(GridPolygon innerPoly in poly.InteriorPolygons)
            {
                NonIntersecting.AddRange(FindNonIntersectingSegments(rTree, innerPoly));
            }

            return NonIntersecting;
        }

        /// <summary>
        /// Find all segments that do not intersect the line segments in the rTree
        /// </summary>
        /// <param name="rTree"></param>
        /// <param name="segments"></param>
        /// <returns></returns>
        private static List<GridLineSegment> FindNonIntersectingSegments(RTree.RTree<GridLineSegment> rTree, ICollection<GridLineSegment> segments)
        {
            List<GridLineSegment> NonIntersecting = new List<Geometry.GridLineSegment>();

            foreach(GridLineSegment l in segments)
            {
                List<GridLineSegment> Candidates = rTree.Intersects(l.BoundingBox.ToRTreeRect(0));

                //Find out if there is a segment that we aren't sharing an endpoint with (part of same polygon border) and is not ourselves
                if(Candidates.Where(c => c != l && !c.SharedEndPoint(l) && c.Intersects(l)).Any())
                {
                    continue;
                }

                NonIntersecting.Add(l);
            }

            return NonIntersecting;
        }
         
        /// <summary>
        /// Return all segments of the polygons that do not intersect any border of the other polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <param name="B"></param>
        /// <param name="AIntersections"></param>
        /// <param name="BIntersections"></param>
        /// <returns></returns>
        public static List<GridLineSegment> NonIntersectingSegments(this GridPolygon[] Polygons)
        {
            RTree.RTree<GridLineSegment> SegmentRTree = new RTree<GridLineSegment>();

            foreach(GridPolygon poly in Polygons)
            {
                AddPolygonSegmentsToRTree(SegmentRTree, poly);
            }

            List<GridLineSegment> NonIntersecting = new List<Geometry.GridLineSegment>();
              
            //Identify which line segments do not intersect with segments in the RTree
            foreach(GridPolygon poly in Polygons)
            {
                NonIntersecting.AddRange(FindNonIntersectingSegments(SegmentRTree, poly)); 
            }

            return NonIntersecting;
        }

        /// <summary>
        /// Return all segments of the polygons that do not intersect any border of the other polygons
        /// </summary>
        /// <param name="Polygons">Input array</param>
        /// <param name="AddPointsAtIntersections">True if points should be added where the polygons intersect and the resulting line segments added to the result set</param>
        /// <param name="AddedPoints">List the points added at intersection points</param>
        /// <returns></returns>
        public static SortedSet<GridLineSegment> NonIntersectingSegments(this GridPolygon[] Polygons, bool AddPointsAtIntersections, out SortedSet<GridVector2> AddedPoints)
        {
            RTree.RTree<GridLineSegment> SegmentRTree = new RTree<GridLineSegment>();

            foreach (GridPolygon poly in Polygons)
            {
                AddPolygonSegmentsToRTree(SegmentRTree, poly);
            }

            SortedSet<GridLineSegment> NonIntersecting = new SortedSet<Geometry.GridLineSegment>();
            AddedPoints = new SortedSet<Geometry.GridVector2>();

            //Identify which line segments do not intersect with segments in the RTree
            foreach (GridPolygon poly in Polygons)
            {
                NonIntersecting.Union(FindNonIntersectingSegments(SegmentRTree, poly));
            }

            if(!AddPointsAtIntersections)
            {
                return NonIntersecting;
            }

            SortedSet<GridLineSegment> IntersectingLines = new SortedSet<GridLineSegment>(SegmentRTree.Items);
            IntersectingLines.ExceptWith(NonIntersecting);

            SortedSet<GridLineSegment> SplitIntersectionLines = IntersectingLines.SplitLinesAtIntersections(out AddedPoints);

            NonIntersecting.UnionWith(SplitIntersectionLines);

            return NonIntersecting;
        }
    }
}