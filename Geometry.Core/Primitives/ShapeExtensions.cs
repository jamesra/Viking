using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// This is a helper struct used to describe exactly where intersections occurred when 
    /// testing intersections on two arrays of shapes. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct ArrayIntersection<T>(T a, T b, int I, int J, IShape2D intersection)
        where T : IShape2D
    {
        ///
        /// This struct is the Combo struct with an extra field
        /// 

        public readonly int iA = I;
        public readonly int iB = J;
        public readonly T A = a;
        public readonly T B = b;
        public readonly IShape2D Intersection = intersection;

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            ArrayIntersection<T> other = (ArrayIntersection<T>)obj;
            return other.iA == this.iA && other.iB == this.iB;
        }

        public override int GetHashCode() => (iA * 23) + iB;

        public static bool operator ==(ArrayIntersection<T> left, ArrayIntersection<T> right) => left.Equals(right);

        public static bool operator !=(ArrayIntersection<T> left, ArrayIntersection<T> right) => !(left == right);

        public override string ToString() => $"Intersection: {iA} {iB} {Intersection}";
    }

    public static class ShapeDecomposition
    {
        /// <summary>
        /// Returns the unique edges of the set of triangles
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public static LineSegment[] Edges(this ICollection<Triangle> triangles) => [.. triangles.SelectMany(t => t.Segments).Distinct(LineSegmentUndirectedComparer.Default)];
    }

    public static class ShapeExtensions
    {
        public static Vector2 Convert(this IPoint2D p)
        {
            if (p is Vector2 v)
                return v;

            return new Vector2(p.X, p.Y);
        }

        public static LineSegment Convert(this ILineSegment2D line)
        {
            if (line is LineSegment l)
                return l;

            return new LineSegment(line.A, line.B);
        }

        public static Circle Convert(this ICircle2D c)
        {
            if (c is Circle circle)
                return circle;

            return new Circle(c.Center, c.Radius);
        }

        public static Triangle Convert(this ITriangle2D t)
        {
            if (t is Triangle tri)
                return tri;

            Vector2[] points = [.. t.Points.Select(p => p.Convert())];
            return new Triangle(points[0], points[1], points[2]);
        }

        public static Rectangle Convert(this IRectangle2D r)
        {
            if (r is Rectangle rect)
                return rect;

            return new Rectangle(r.Left, r.Right, r.Bottom, r.Top);
        }


        public static Polygon Convert(this IPolygon2D poly)
        {
            if (poly is Polygon p)
                return p;

            return new Polygon(poly.ExteriorRing, poly.InteriorRings);
        }

        public static bool Intersects(this IShape2D shape, IShape2D other)
        {
            if (false == shape.BoundingBox.Intersects(other.BoundingBox))
                return false;

            return shape.ShapeType switch
            {
                ShapeType2D.Point => PointIntersects(shape as IPoint2D, other),
                ShapeType2D.Circle => CircleIntersects(shape as ICircle2D, other),
                ShapeType2D.Rectangle => RectangleIntersects(shape as IRectangle2D, other),
                ShapeType2D.Triangle => TriangleIntersects(shape as ITriangle2D, other),
                ShapeType2D.Line => LineIntersects(shape as ILineSegment2D, other),
                ShapeType2D.Polygon => PolygonIntersects(shape as IPolygon2D, other),
                ShapeType2D.Polyline => PolylineIntersects(shape as IPolyLine2D, other),
                ShapeType2D.InfiniteLine => shape is Line infinite && infinite.Intersects(other),
                ShapeType2D.Quad => shape is Quad quad && quad.Intersects(other),
                ShapeType2D.Collection => shape is IShapeCollection2D collection && collection.Geometries.Any(g => g.Intersects(other)),
                _ => false,
            };
        }

        private static bool PointIntersects(IPoint2D point, IShape2D other) => other.Covers(point);

        internal static bool CircleIntersects(ICircle2D c, IShape2D other)
        {
            Circle circle = new(c.Center, c.Radius);
            return CircleIntersects(circle, other);
        }

        internal static bool CircleIntersects(in Circle circle, in IShape2D other)
        {
            switch (other.ShapeType)
            {
                case ShapeType2D.Point:
                    return circle.Covers(other as IPoint2D);
                case ShapeType2D.Line:
                    return circle.Intersects(other as ILineSegment2D);
                case ShapeType2D.Circle:
                    ICircle2D other_circle = other as ICircle2D;
                    return circle.Intersects(new Circle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.Triangle:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return circle.Intersects(other_tri.Convert());
                case ShapeType2D.Polygon:
                    return circle.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.Rectangle:
                    return circle.Intersects(((Rectangle)other).Convert());
                default:
                    return ExtendedIntersects(circle, other);
            }
        }

        internal static bool RectangleIntersects(in IRectangle2D r, in IShape2D other)
        {
            Rectangle rect = r.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.Point:
                    return rect.Covers(other as IPoint2D);
                case ShapeType2D.Line:
                    return rect.Intersects(other as ILineSegment2D);
                case ShapeType2D.Circle:
                    ICircle2D other_circle = other as ICircle2D;
                    return rect.Intersects(new Circle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.Triangle:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return rect.Intersects(other_tri.Convert());
                case ShapeType2D.Polygon:
                    return rect.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.Rectangle:
                    return rect.Intersects(((Rectangle)other).Convert());
                default:
                    return ExtendedIntersects(rect, other);
            }
        }

        internal static bool TriangleIntersects(in ITriangle2D t, in IShape2D other)
        {
            Triangle tri = t.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.Point:
                    return tri.Covers(other as IPoint2D);
                case ShapeType2D.Line:
                    return tri.Intersects(other as ILineSegment2D);
                case ShapeType2D.Circle:
                    ICircle2D other_circle = other as ICircle2D;
                    return tri.Intersects(new Circle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.Triangle:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return tri.Intersects(other_tri.Convert());
                case ShapeType2D.Polygon:
                    return tri.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.Rectangle:
                    if (other is Rectangle rect)
                        return RectangleIntersectionExtensions.Intersects(rect, tri);
                    else if (other is IRectangle2D r)
                        return RectangleIntersectionExtensions.Intersects(r.Convert(), tri);

                    throw new ArgumentException("Unexpected rectangle object");

                default:
                    return ExtendedIntersects(tri, other);
            }
        }

        internal static bool LineIntersects(in ILineSegment2D l, in IShape2D other)
        {
            LineSegment line = l.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.Point:
                    return line.Covers(other as IPoint2D);
                case ShapeType2D.Line:
                    return line.Intersects(((ILineSegment2D)other).Convert());
                case ShapeType2D.Circle:
                    ICircle2D other_circle = other as ICircle2D;
                    return line.Intersects(new Circle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.Triangle:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return line.Intersects(other_tri.Convert());
                case ShapeType2D.Polygon:
                    return line.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.Rectangle:
                    return ((IRectangle2D)other).Convert().Intersects(line);
                default:
                    return ExtendedIntersects(line, other);
            }
        }

        internal static bool PolylineIntersects(IPolyLine2D line, IShape2D other)
        {
            if (line is null)
                throw new ArgumentNullException(nameof(line));

            return line.LineSegments.Any(segment => LineIntersects(segment, other));
        }

        static bool ExtendedIntersects(in IShape2D shape, in IShape2D other)
        {
            IShape2D left = shape;
            return other.ShapeType switch
            {
                ShapeType2D.Polyline => PolylineIntersects((IPolyLine2D)other, left),
                ShapeType2D.InfiniteLine => other is Line infinite && infinite.Intersects(left),
                ShapeType2D.Quad => other is Quad quad && quad.Intersects(left),
                ShapeType2D.Collection => other is IShapeCollection2D collection && collection.Geometries.Any(g => left.Intersects(g)),
                _ => false,
            };
        }
        internal static bool PolygonIntersects(in IPolygon2D p, in IShape2D other)
        {
            Polygon poly = p.Convert();

            switch (other.ShapeType)
            {
                case ShapeType2D.Point:
                    return poly.Covers(other as IPoint2D);
                case ShapeType2D.Line:
                    return poly.Intersects(other as ILineSegment2D);
                case ShapeType2D.Circle:
                    ICircle2D other_circle = other as ICircle2D;
                    return poly.Intersects(new Circle(other_circle.Center, other_circle.Radius));
                case ShapeType2D.Triangle:
                    ITriangle2D other_tri = other as ITriangle2D;
                    return poly.Intersects(other_tri.Convert());
                case ShapeType2D.Polygon:
                    return poly.Intersects(((IPolygon2D)other).Convert());
                case ShapeType2D.Rectangle:
                    return poly.Intersects(((Rectangle)other).Convert());
                default:
                    return ExtendedIntersects(poly, other);
            }
        }

        /// <summary>
        /// Return true if any Polygons in the set intersect
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static bool AnyIntersect(this IReadOnlyList<Polygon> Polygons)
        {
            for (int i = 0; i < Polygons.Count; i++)
            {
                Polygon iPoly = Polygons[i];
                if (iPoly is null)
                    continue;

                for (int j = i + 1; j < Polygons.Count; j++)
                {
                    Polygon jPoly = Polygons[j];
                    if (jPoly is null)
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
        public static bool Intersects(in Circle circle, in LineSegment line)
        {
            if (false == line.BoundingBox.Intersects(circle.BoundingBox))
                return false;

            if (circle.Covers(line.A) || circle.Covers(line.B))
                return true;

            //TODO: I'm not sure we need the IsNearestPointWithinLineSegment check because the bounding boxes intersect.
            if (line.IsNearestPointWithinLineSegment(circle.Center))
            {
                double distanceToLine = line.DistanceToPoint(circle.Center);
                return distanceToLine <= circle.Radius;
            }

            return false;
        }

        public static bool Intersects(in Circle circle, in Rectangle rect)
        {
            if (false == circle.BoundingBox.Intersects(rect))
            {
                return false;
            }

            if (rect.Covers(circle.Center))
                return true;


            if (circle.Covers(rect.LowerLeft) || circle.Covers(rect.LowerRight) ||
                circle.Covers(rect.UpperLeft) || circle.Covers(rect.UpperRight))
                return true;

            foreach (LineSegment border in rect.Edges)
            {
                //TODO: I'm not sure we need the IsNearestPointWithinLineSegment check because the bounding boxes intersect.
                if (border.IsNearestPointWithinLineSegment(circle.Center))
                {
                    double distanceToLine = border.DistanceToPoint(circle.Center);
                    if (distanceToLine <= circle.Radius)
                        return true;
                }
            }

            return false;
        }

        public static bool Intersects(in Circle circle, in Triangle tri)
        {
            if (false == circle.BoundingBox.Intersects(tri.BoundingBox))
                return false;

            //Do any triangle verts fall inside our circle?
            if (circle.Covers(tri.P1) || circle.Covers(tri.P2) || circle.Covers(tri.P3))
                return true;

            //Is the center of our circle inside the triangle?
            if (tri.Covers(circle.Center))
                return true;

            //Do any triangle line segments intersect our circle?
            foreach (LineSegment line in tri.Segments)
            {
                if (circle.Intersects(line))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the circle intersects the polygon without containing all of it
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static bool Intersects(in Circle circle, in Polygon poly)
        {

            if (false == circle.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            //Do any triangle verts fall inside our circle? 
            foreach (Vector2 p in poly.ExteriorRing)
            {
                if (circle.Covers(p))
                    return true;
            }

            //Is the center of our circle inside the triangle?
            //Todo: This contains test is inconsistent with the idea of an intersection.  I need to clarify when circles and all shapes are considered solid or traces. This old code is prevalent and I don't want to break things until I can fix all of these instances
            if (poly.Covers(circle.Center))
                return true;

            //Do any line segments intersect our circle?
            //List<LineSegment> Candidates = poly.SegmentRTree.Intersects(circle.BoundingBox).Select(p => p.Segment(poly)).Where(segment => circle.Intersects(segment)).ToList();
            foreach (PolygonIndex p in poly.SegmentRTree.IntersectionGenerator(circle.BoundingBox))
            {
                LineSegment line = p.Segment(poly);

                if (circle.Intersects(line))
                    return true;
            }

            return false;
        }

        public static ShapeRelation GetRelation(in Circle circle, Polygon poly) => circle.GetRelation(poly);
    }

    public static class RectangleIntersectionExtensions
    {
        public static bool Intersects(in Rectangle rect, in Circle circle) => CircleIntersectionExtensions.Intersects(circle, rect);

        public static bool Intersects(in Rectangle rect, in LineSegment line)
        {
            if (false == line.BoundingBox.Intersects(rect))
                return false;

            if (rect.Covers(line.A) || rect.Covers(line.B))
                return true;

            foreach (var rect_line in rect.Segments)
            {
                if (rect_line.Intersects(in line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(in Rectangle rect, in Triangle tri)
        {
            if (false == tri.BoundingBox.Intersects(rect))
                return false;

            if (rect.Covers(tri.P1) || rect.Covers(tri.P2) || rect.Covers(tri.P3))
                return true;

            //If even one point of the rectangle is inside the triangle then the rectangle is entirely contained in the triangle
            //or a line segment of the rectangle must intersect the triangle
            if (tri.Covers(rect.Center))
                return true;

            foreach (var tri_line in tri.Segments)
            {
                if (rect.Intersects(tri_line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(in Rectangle rect, Polygon poly)
        {
            if (false == poly.BoundingBox.Intersects(rect))
                return false;

            foreach (Vector2 p in poly.ExteriorRing)
            {
                if (rect.Covers(p))
                    return true;
            }

            foreach (Vector2 p in rect.Corners)
            {
                if (poly.Covers(p))
                    return true;
            }

            var lambdaCopy = rect;
            return poly.SegmentRTree.Intersects(rect).Any(p => lambdaCopy.Intersects(p.Segment(poly)));
            /*
        List<LineSegment> Candidates = poly.ExteriorSegmentRTree.Intersects(rect);
        foreach (LineSegment line in Candidates)
        {
            if (rect.Intersects(line))
                return true;
        }

        return false;
        */
        }
    }

    public static class TriangleIntersectionExtensions
    {
        public static bool Intersects(in Triangle tri, in Circle circle) => CircleIntersectionExtensions.Intersects(circle, tri);

        public static bool Intersects(in Triangle tri, in Rectangle rect) => RectangleIntersectionExtensions.Intersects(rect, tri);

        public static bool Intersects(in Triangle tri, in LineSegment line)
        {
            if (false == tri.BoundingBox.Intersects(line.BoundingBox))
                return false;

            if (tri.Covers(line.A) || tri.Covers(line.B))
                return true;

            foreach (LineSegment tri_line in tri.Segments)
            {
                if (line.Intersects(tri_line))
                    return true;
            }

            return false;
        }

        public static bool Intersects(in Triangle tri, Polygon poly)
        {
            if (false == tri.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            foreach (Vector2 p in poly.ExteriorRing)
            {
                if (tri.Covers(p))
                    return true;
            }

            foreach (Vector2 p in tri.Points)
            {
                if (poly.Covers(p))
                    return true;
            }

            ///Check in case a triangle vertex falls inside an interior polygon
            foreach (LineSegment line in tri.Segments)
            {
                if (poly.Intersects(line))
                    return true;
            }

            return false;
        }
    }

    public enum LineSetOrdering
    {
        None,
        Polyline,
        Closed,
    }

    public static class LineIntersectionExtensions
    {
        public static bool Intersects(in LineSegment line, in Circle circle) => CircleIntersectionExtensions.Intersects(circle, line);

        public static bool Intersects(in LineSegment line, in Rectangle rect) => RectangleIntersectionExtensions.Intersects(rect, line);

        public static bool Intersects(in LineSegment line, in Triangle tri) => TriangleIntersectionExtensions.Intersects(tri, line);

        public static bool Intersects(this in LineSegment line, in Polygon poly, out Vector2 intersection)
        {
            intersection = Vector2.Zero;
            bool intersected = Intersects(line, poly, false, out List<Vector2> intersections);
            if (intersected)
            {
                intersection = intersections.First();
            }

            return intersected;
        }

        public static bool Intersects(this in LineSegment line, in Polygon poly, bool EndpointsOnRingDoNotIntersect = false) => Intersects(line, poly, EndpointsOnRingDoNotIntersect, out List<Vector2> intersections);

        /// <summary>
        /// Return true if any of the segments of the polygon intersect the line
        /// </summary>
        /// <param name="line"></param>
        /// <param name="poly"></param>
        /// <param name="EndpointsOnRingDoNotIntersect"></param>
        /// <param name="intersections"></param>
        /// <returns></returns>
        public static bool Intersects(this in LineSegment line, Polygon poly, bool EndpointsOnRingDoNotIntersect, out List<Vector2> intersections)
        {
            intersections = [];

            if (false == line.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            List<LineSegment> listCandidates = [.. poly.SegmentRTree.Intersects(line.BoundingBox).Select(p => p.Segment(poly))];

            foreach (LineSegment poly_line in listCandidates)
            {
                if (line.Intersects(poly_line, out Vector2 intersection))
                {
                    intersections.Add(intersection);
                }
            }
            /*
            List<LineSegment> listCandidates = poly.ExteriorSegmentRTree.Intersects(line.BoundingBox);

            foreach(LineSegment poly_line in listCandidates)
            {
                Vector2 intersection; 
                if (line.Intersects(poly_line, out intersection))
                {
                    intersections.Add(intersection);
                }
            }

            foreach(Polygon inner in poly.InteriorPolygons)
            {
                List<Vector2> listInnerIntersections;
                if(Intersects(line, inner, EndpointsOnRingDoNotIntersect, out listInnerIntersections))
                {
                    intersections.AddRange(listInnerIntersections);
                }
            }
            */
            var lambda_copy = line;
            intersections = [.. intersections.Distinct()]; //Remove duplicates if our line happens to pass directly through a vertex
            if (EndpointsOnRingDoNotIntersect)
            {
                intersections = [.. intersections.Where(i => !lambda_copy.IsEndpoint(i))];
            }

            return intersections.Count > 0;

        }

        /// <summary>
        /// Returns true if any portion of the line is inside the polygon
        /// </summary>
        /// <param name="line"></param>
        /// <param name="poly"></param>
        /// <param name="Intersections"></param>
        /// <returns></returns>
        public static bool Crosses(this in LineSegment line, in Polygon poly) => line.Crosses(poly, out List<Vector2> Intersections);

        /// <summary>
        /// Returns true if any portion of the line is inside the polygon
        /// </summary>
        /// <param name="line"></param>
        /// <param name="poly"></param>
        /// <param name="Intersections"></param>
        /// <returns></returns>
        public static bool Crosses(this in LineSegment line, in Polygon poly, out List<Vector2> Intersections)
        {
            Intersections = [];

            if (false == line.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            bool Intersects = line.Intersects(poly, true, out Intersections);
            if (Intersects)
            {
                return true;
            }

            //Now check if the line is entirely inside the polygon without crossing a ring
            return poly.Covers(line);
        }

        /// <summary>
        /// Add a new point where line intersects any other line
        /// </summary>
        /// <param name="line">Line we add points to</param>
        /// <param name="lines">Lines we are testing for intersection</param>
        /// <param name="IntersectionPoints">The intersection points on the line, in increasing order of distance from line.A to line.B</param>
        /// <returns>The lines that intersect the line parameter</returns>
        public static bool Intersects(this in LineSegment line, in IEnumerable<LineSegment> lines, bool EndpointsOnRingDoNotIntersect = false)
        {
            foreach (LineSegment testLine in lines)
            {
                if (line.Intersects(testLine, EndpointsOnRingDoNotIntersect))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<LineSegment> Intersections(this in LineSegment line, in IReadOnlyList<LineSegment> lines, out Vector2[] IntersectionPoints) => Intersections(line, lines, true, out IntersectionPoints);


        /// <summary>
        /// Return a list of lines the passed line intersects and the intersection points
        /// </summary>
        /// <param name="line">Line we are checking</param>
        /// <param name="lines">Lines we are testing for intersection</param>
        /// <param name="EndpointsOnLineDoNotIntersect"></param>
        /// <param name="IntersectionPoints">The intersection points on the line, in increasing order of distance from line.A to line.B</param>
        /// <returns>The lines that intersect the line parameter</returns>
        public static List<LineSegment> Intersections(this LineSegment line, in IReadOnlyList<LineSegment> lines, bool EndpointsOnLineDoNotIntersect, out Vector2[] IntersectionPoints)
        {
            //Cannot use an out parameter in the anonymous method I use below, so I have a bit of redundancy in tracking added points
            List<Vector2> NewPoints = new(lines.Count);
            List<LineSegment> IntersectingLines = new(lines.Count);

            foreach (LineSegment testLine in lines)
            {
                if (line.Intersects(testLine, out Vector2 intersection))
                {
                    //Check that NewPoints does not contain the point.  This can occur when the test line intersects exactly over the endpoint of two lines.
                    if (EndpointsOnLineDoNotIntersect && line.IsEndpoint(intersection))
                    {
                        continue;
                    }

                    if (!NewPoints.Contains(intersection))
                    {
                        NewPoints.Add(intersection);
                        IntersectingLines.Add(testLine);
                    }
                }
            }

            double[] dotValues = [.. NewPoints.Select(p => line.Dot(p))];
            int[] sortedIndices = dotValues.SortAndIndex();

            IntersectionPoints = [.. sortedIndices.Select(i => NewPoints[i])];

            return [.. sortedIndices.Select(i => IntersectingLines[i])];
        }

        /// <summary>
        /// Return a list of lines the passed line intersects and the intersection points
        /// </summary>
        /// <param name="line">Line we are checking</param>
        /// <param name="lines">Lines we are testing for intersection</param>
        /// <param name="EndpointsOnLineDoNotIntersect"></param>
        /// <param name="IntersectionPoints">The intersection points on the line, in increasing order of distance from line.A to line.B</param>
        /// <returns>The lines that intersect the line parameter</returns>
        public static List<Tuple<LineSegment, LineSegment>> Intersections(this IEnumerable<LineSegment> ALines, IReadOnlyList<LineSegment> BLines, bool EndpointsOnLineDoNotIntersect, out Vector2[] IntersectionPoints)
        {
            List<Tuple<LineSegment, LineSegment>> listLinePairIntersections = [];
            List<Vector2> listIntersections = [];
            foreach (LineSegment line in ALines)
            {
                List<LineSegment> intersectedLines = line.Intersections(BLines, EndpointsOnLineDoNotIntersect, out Vector2[] intersections);
                listLinePairIntersections.AddRange(intersectedLines.Select(other => new Tuple<LineSegment, LineSegment>(line, other)));
                listIntersections.AddRange(intersections);
            }

            IntersectionPoints = [.. listIntersections];
            return listLinePairIntersections;
        }

        /// <summary>
        /// Return the list line pairs that intersect between the two sets of lines
        /// </summary>
        /// <param name="line">Line we are checking</param>
        /// <param name="lines">Lines we are testing for intersection</param>
        /// <param name="EndpointsOnLineDoNotIntersect"></param>
        /// <param name="IntersectionPoints">The intersection points on the line, in increasing order of distance from line.A to line.B</param>
        /// <returns>The lines that intersect the line parameter</returns>
        public static List<ArrayIntersection<LineSegment>> Intersections(this IReadOnlyList<LineSegment> ALines, IReadOnlyList<LineSegment> BLines, bool EndpointsOnLineDoNotIntersect)
        {
            List<ArrayIntersection<LineSegment>> listLinePairIntersections = [];
            List<Vector2> listIntersections = [];

            for (int iA = 0; iA < ALines.Count; iA++)
            {
                LineSegment A = ALines[iA];
                for (int iB = 0; iB < BLines.Count; iB++)
                {
                    LineSegment B = BLines[iB];

                    if (A.Intersects(B, EndpointsOnLineDoNotIntersect, out IShape2D Intersection))
                    {
                        ArrayIntersection<LineSegment> Result = new(A, B, iA, iB, Intersection);
                        listLinePairIntersections.Add(Result);
                    }
                }
            }

            return listLinePairIntersections;
        }

        public static bool IsEndpointIntersectionExpected(this LineSetOrdering order, int iLine, int jLine, int list_length)
        {
            return order switch
            {
                LineSetOrdering.None => false,
                LineSetOrdering.Polyline => iLine + 1 == jLine,
                LineSetOrdering.Closed => iLine + 1 == jLine || (iLine == 0 && jLine == (list_length - 1)),
                _ => throw new ArgumentException("Unexpected LineSetOrdering provided to IsEndpointInteresectionExpected"),
            };
        }

        /// <summary>
        /// Return true if the passed test line intersects any of the set of other lines, which may be part of a closed or polyline.
        /// In the case of a polyline or closed line, the test line is considered to be the last element of the set, and the set 
        /// is assumed to not have any self-intersections already.
        /// </summary>
        /// <param name="test">The line being checked</param>
        /// <param name="lines">A set of lines</param>
        /// <param name="order">Information as to how the lines are connected. </param>
        /// <returns></returns>
        public static bool SelfIntersects(this in LineSegment addition, in IReadOnlyList<LineSegment> lines, LineSetOrdering order, out LineSegment? intersected)
        {
            intersected = null;

            for (int iLine = 0; iLine < lines.Count; iLine++)
            {
                LineSegment line = lines[iLine];
                //For polyline and closed loops for adjacent lines we only need to check that the endpoints aren't equal to know that the lines do not overlap
                if (iLine + 1 == lines.Count && (order == LineSetOrdering.Polyline || order == LineSetOrdering.Closed))
                {
                    if ((line.A != addition.B && line.B == addition.A) ||
                        (line.B != addition.A && line.A == addition.B) ||
                        (line.A != addition.A && line.B == addition.B) ||
                        (line.B != addition.B && line.A == addition.A))
                        continue;
                }

                bool EndpointsOnRingDoNotIntersect = order.IsEndpointIntersectionExpected(iLine, lines.Count, lines.Count + 1);

                if (line.Intersects(addition, EndpointsOnRingDoNotIntersect: EndpointsOnRingDoNotIntersect))
                {
                    intersected = line;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Return true if the passed test line intersects any of the set of other lines, which may be part of a closed or polyline.
        /// In the case of a polyline or closed line, the test line is considered to be the last element of the set, and the set 
        /// is assumed to not have any self-intersections already.
        /// </summary>
        /// <param name="test">The line being checked</param>
        /// <param name="lines">A set of lines</param>
        /// <param name="order">Information as to how the lines are connected. </param>
        /// <returns></returns>
        public static bool SelfIntersects(this in LineSegment addition, in IReadOnlyList<LineSegment> lines, LineSetOrdering order) => SelfIntersects(in addition, in lines, order, out LineSegment? intersected);

        /// <summary>
        /// Return true if the passed Polyline intersects itself. 
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="IsClosedRing">True if the polyline forms a closed ring, in which case the first and last points are allowed to overlap</param>
        /// <returns></returns>
        public static bool SelfIntersects(this IReadOnlyList<LineSegment> lines, LineSetOrdering order)
        {
            for (int iLine = 0; iLine < lines.Count; iLine++)
            {
                for (int jLine = iLine + 1; jLine < lines.Count; jLine++)
                {
                    //For polyline and closed loops for adjacent lines we only need to check that the endpoints aren't equal to know that the lines do not overlap
                    if (iLine + 1 == jLine && (order == LineSetOrdering.Polyline || order == LineSetOrdering.Closed))
                    {
                        if (lines[iLine].A != lines[jLine].B)
                            continue;
                    }

                    bool EndpointsOnRingDoNotIntersect = order.IsEndpointIntersectionExpected(iLine, jLine, lines.Count);

                    if (lines[iLine].Intersects(lines[jLine], EndpointsOnRingDoNotIntersect: EndpointsOnRingDoNotIntersect))
                        return true;
                }
            }

            return false;
        }

        public static List<LineSegment> SubdivideAtIntersections(this in LineSegment line, in IReadOnlyList<LineSegment> lines, out Vector2[] IntersectionPoints)
        {
            List<LineSegment> Unused = line.Intersections(lines, out IntersectionPoints);

            List<LineSegment> DividedLines = new(IntersectionPoints.Length + 2);
            if (IntersectionPoints.Length == 0)
            {
                DividedLines.Add(line);
                return DividedLines;
            }

            DividedLines.Add(new LineSegment(line.A, IntersectionPoints[0]));
            for (int i = 0; i < IntersectionPoints.Length - 1; i++)
            {
                DividedLines.Add(new LineSegment(IntersectionPoints[i], IntersectionPoints[i + 1]));
            }
            DividedLines.Add(new LineSegment(IntersectionPoints.Last(), line.B));

            return DividedLines;
        }

        /// <summary>
        /// Given a set of lines, return a new set of lines where line-line intersections only occur at line endpoints by splitting lines at intersections.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static SortedSet<LineSegment> SplitLinesAtIntersections(this IEnumerable<LineSegment> lines, out SortedSet<Vector2> AddedPoints)
        {
            BoundingBoxIndex<LineSegment> rTree = lines.ToBoundingBoxIndex();

            IList<LineSegment> sortedLines = lines is IList<LineSegment> existing ? existing : [.. lines];
            SortedSet<LineSegment> output = [];

            Stack<LineSegment> linesToTest = new(lines);

            AddedPoints = [];

            while (linesToTest.Count > 0)
            {
                LineSegment A = linesToTest.Pop();

                ///Find lines that intersect A, but not on an endpoint of A
                IEnumerable<LineSegment> intersections = rTree.Intersects(A.BoundingBox).Where(B =>
                    {
                        if (B == A)
                            return false;

                        if (B.SharedEndPoint(in A))
                            return false;

                        if (B.Intersects(A, out Vector2 intersection))
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
                    LineSegment B = intersections.First();

                    if (B.Intersects(A, out Vector2 intersection))
                    {
                        AddedPoints.Add(intersection);
                        linesToTest.Push(new LineSegment(A.A, intersection));
                        linesToTest.Push(new LineSegment(A.B, intersection));
                    }
                }
            }

            return output;
        }
    }

    public static class PolygonIntersectionExtensions
    {
        public static bool Intersects(Polygon poly, in Circle circle) => CircleIntersectionExtensions.Intersects(in circle, poly);

        public static bool Intersects(Polygon poly, in Rectangle rect) => RectangleIntersectionExtensions.Intersects(rect, poly);

        public static bool Intersects(Polygon poly, in Triangle tri) => TriangleIntersectionExtensions.Intersects(in tri, poly);

        public static bool Intersects(Polygon poly, in LineSegment line) => LineIntersectionExtensions.Intersects(in line, poly);

        public static bool Intersections(in Polygon A, Polygon B, out LineSegment[] AIntersections, out LineSegment[] BIntersections)
        {
            if (false == A.BoundingBox.Intersects(B.BoundingBox))
            {
                AIntersections = [];
                BIntersections = [];
                return false;
            }

            List<LineSegment> AMatches = [];
            List<LineSegment> BMatches = [];

            foreach (LineSegment ALine in A.ExteriorSegments)
            {
                bool AAdded = false;
                IEnumerable<LineSegment> BCandidates = B.SegmentRTree.Intersects(ALine.BoundingBox).Select(p => p.Segment(B));
                foreach (LineSegment BLine in BCandidates)
                {
                    if (ALine.Intersects(BLine))
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

            AIntersections = [.. AMatches];
            BIntersections = [.. BMatches];

            return AIntersections.Length > 0 || BIntersections.Length > 0;
        }

        /// <summary>
        /// Create an RTree containing all segments from the borders of the polygon
        /// </summary>
        /// <param name="rTree"></param>
        /// <param name="poly"></param>
        private static void AddPolygonSegmentsToRTree(in BoundingBoxIndex<LineSegment> rTree, in Polygon poly)
        {
            foreach (LineSegment l in poly.ExteriorSegments)
            {
                rTree.Add(l.BoundingBox, l);
            }

            foreach (Polygon innerPoly in poly.InteriorPolygons)
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
        private static List<LineSegment> FindIntersectingSegments(in BoundingBoxIndex<LineSegment> rTree, Polygon poly)
        {
            List<LineSegment> Intersecting = FindIntersectingSegments(rTree, poly.ExteriorSegments);

            foreach (Polygon innerPoly in poly.InteriorPolygons)
            {
                Intersecting.AddRange(FindIntersectingSegments(rTree, innerPoly));
            }

            return Intersecting;
        }

        private static List<LineSegment> FindIntersectingSegments(BoundingBoxIndex<LineSegment> rTree, ICollection<LineSegment> segments)
        {
            List<LineSegment> Intersecting = [];

            foreach (LineSegment l in segments)
            {
                List<LineSegment> Candidates = rTree.Intersects(l.BoundingBox);

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
        private static List<LineSegment> FindNonIntersectingSegments(BoundingBoxIndex<LineSegment> rTree, Polygon poly)
        {
            List<LineSegment> NonIntersecting = FindNonIntersectingSegments(rTree, poly.ExteriorSegments);

            foreach (Polygon innerPoly in poly.InteriorPolygons)
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
        private static List<LineSegment> FindNonIntersectingSegments(BoundingBoxIndex<LineSegment> rTree, ICollection<LineSegment> segments)
        {
            List<LineSegment> NonIntersecting = [];

            foreach (LineSegment l in segments)
            {
                List<LineSegment> Candidates = rTree.Intersects(l.BoundingBox);

                //Find out if there is a segment that we aren't sharing an endpoint with (part of same polygon border) and is not ourselves
                if (Candidates.Any(c => c != l && !c.SharedEndPoint(l) && c.Intersects(l)))
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
        public static List<LineSegment> Segments(this IEnumerable<Polygon> Polygons)
        {
            //BoundingBoxIndex<LineSegment> SegmentRTree = new BoundingBoxIndex<LineSegment>();

            List<LineSegment> segments = [];

            foreach (Polygon poly in Polygons)
            {
                segments.AddRange(poly.AllSegments);
                //AddPolygonSegmentsToRTree(SegmentRTree, poly);
            }

            return segments;
        }

        /// <summary>
        /// Return all segments of the polygons that do not intersect any border of the other polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <param name="B"></param>
        /// <param name="AIntersections"></param>
        /// <param name="BIntersections"></param>
        /// <returns></returns>
        public static List<LineSegment> NonIntersectingSegments(this Polygon[] Polygons)
        {
            BoundingBoxIndex<LineSegment> SegmentRTree = new();

            foreach (Polygon poly in Polygons)
            {
                AddPolygonSegmentsToRTree(SegmentRTree, poly);
            }

            List<LineSegment> NonIntersecting = [];

            //Identify which line segments do not intersect with segments in the RTree
            foreach (Polygon poly in Polygons)
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
        public static SortedSet<LineSegment> NonIntersectingSegments(this Polygon[] Polygons, bool AddPointsAtIntersections, out SortedSet<Vector2> AddedPoints)
        {
            BoundingBoxIndex<LineSegment> SegmentRTree = new();

            foreach (Polygon poly in Polygons)
            {
                AddPolygonSegmentsToRTree(SegmentRTree, poly);
            }

            SortedSet<LineSegment> NonIntersecting = [];
            AddedPoints = [];

            //Identify which line segments do not intersect with segments in the RTree
            foreach (Polygon poly in Polygons)
            {
                NonIntersecting.UnionWith(FindNonIntersectingSegments(SegmentRTree, poly));
            }

            if (!AddPointsAtIntersections)
            {
                return NonIntersecting;
            }

            SortedSet<LineSegment> IntersectingLines = [.. SegmentRTree.Items];
            IntersectingLines.ExceptWith(NonIntersecting);

            SortedSet<LineSegment> SplitIntersectionLines = IntersectingLines.SplitLinesAtIntersections(out AddedPoints);

            NonIntersecting.UnionWith(SplitIntersectionLines);

            return NonIntersecting;
        }


        /// <summary>
        /// Add verticies at intersection points for all intersection points
        /// </summary>
        /// <param name="Polys"></param>
        public static List<Vector2> AddCorrespondingVertices(this IReadOnlyList<Polygon> Polys)
        {
            List<Vector2> added_intersections = [];
            foreach (var combo in Polys.CombinationPairs())
            {
                Polygon A = combo.A;
                Polygon B = combo.B;
                List<Vector2> newIntersections = A.AddPointsAtIntersections(B);
                added_intersections.AddRange(newIntersections);

#if DEBUG
                foreach (Vector2 p in newIntersections)
                {
                    Debug.Assert(A.IsVertex(p));
                    Debug.Assert(B.IsVertex(p));
                }
#endif
            }

            return [.. added_intersections.Distinct()];
        }

        /// <summary>
        /// Add verticies at intersection points for all intersection points
        /// </summary>
        /// <param name="Polys"></param>
        public static List<Vector2> AddCorrespondingVertices(this IReadOnlyList<Polyline> lines)
        {
            List<Vector2> added_intersections = [];
            foreach (var combo in lines.CombinationPairs())
            {
                Polyline A = combo.A;
                Polyline B = combo.B;
                List<Vector2> newIntersections = A.AddPointsAtIntersections(B);

                if (newIntersections.Any())
                    B.AddPointsAtIntersections(A);

                added_intersections.AddRange(newIntersections);

#if DEBUG
                foreach (Vector2 p in newIntersections)
                {
                    Debug.Assert(A.Points.Contains(p));
                    Debug.Assert(B.Points.Contains(p));
                }
#endif
            }

            return added_intersections;
        }

        static List<Vector2> AddCorrespondingVertices(Polygon poly, Polyline line)
        {
            List<Vector2> added = [];
            foreach (ILineSegment2D seg in line.LineSegments)
                poly.AddPointsAtIntersections(seg.Convert());

            foreach (LineSegment pseg in poly.AllSegments)
                added.AddRange(line.AddPointsAtIntersections(pseg));

            return added;
        }

        /// <summary>
        /// Add verticies at intersection points for all intersection points
        /// </summary>
        /// <param name="shapes"></param>
        public static List<Vector2> AddCorrespondingVertices(this IReadOnlyList<IShape2D> shapes)
        {
            List<Vector2> added_intersections = [];
            foreach (var combo in shapes.CombinationPairs())
            {
                IShape2D A = combo.A;
                IShape2D B = combo.B;

                if (A is Polygon polyA && B is Polygon polyB)
                {
                    var newAIntersections = polyA.AddPointsAtIntersections(polyB);

                    added_intersections.AddRange(newAIntersections);
                }
                else if (A is Polyline lineA && B is Polyline lineB)
                {
                    var newAIntersections = lineA.AddPointsAtIntersections(lineB);
                    added_intersections.AddRange(newAIntersections);
                }
                else if (A is Polygon polyAB && B is Polyline lineAB)
                {
                    added_intersections.AddRange(AddCorrespondingVertices(polyAB, lineAB));
                }
                else if (A is Polyline lineBA && B is Polygon polyBA)
                {
                    added_intersections.AddRange(AddCorrespondingVertices(polyBA, lineBA));
                }
            }

            //added_intersections.RemoveAdjacentDuplicates();
            if (added_intersections.Count() > 1)
                return added_intersections.RemoveAdjacentDuplicates();
            else
                return added_intersections;
        }

        /// <summary>
        /// Given an array of polygons and an array of points, returns the PointIndex of points that are exact verticies on the polygons
        /// </summary>
        /// <param name="Polygons"></param>
        /// <param name="correspondingPoints"></param>
        public static PolygonIndex[][] IndicesForPoints(this IReadOnlyList<Polygon> Polygons, ICollection<Vector2> correspondingPoints)
        {
            //List<PointIndex[]> output = new List<PointIndex[]>(correspondingPoints.Count);
            PolygonIndex[][] output = new PolygonIndex[correspondingPoints.Count][];
            int iOutput = 0;
            foreach (Vector2 correspondingPoint in correspondingPoints)
            {
                //Determine polygon indicies of corresponding verticies
                output[iOutput] = [.. Polygons.Select((poly, iPoly) =>
                {
                    if (poly.TryGetIndex(correspondingPoint, out PolygonIndex index))
                        return index.Reindex(iPoly);
                    else
                        return new PolygonIndex?();
                }).Where(index => index.HasValue).Select(index => index.Value)];
                iOutput += 1;
            }

            return output;
        }
    }
}