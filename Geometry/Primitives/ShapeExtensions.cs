using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            foreach (GridLineSegment line in poly.ExteriorSegments)
            {
                if (circle.Intersects(line))
                    return true;
            }

            //Do any interior line segments intersect our circle?
            //Doesn't work with nested inner polygons...
            GridPolygon inner_poly = null;
            if (poly.InteriorPolygonContains(circle.Center, out inner_poly))
            {
                foreach (GridLineSegment line in inner_poly.ExteriorSegments)
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

            foreach (GridLineSegment line in poly.ExteriorSegments)
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
            if (false == line.BoundingBox.Intersects(poly.BoundingBox))
                return false;

            if (poly.Contains(line.A) || poly.Contains(line.B))
                return true; 

            foreach(GridLineSegment poly_line in poly.ExteriorSegments)
            {
                if (line.Intersects(poly_line))
                    return true; 
            }

            return false; 
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

        public static bool Intersects(GridPolygon poly, GridPolygon other)
        {
            if (false == poly.BoundingBox.Intersects(other.BoundingBox))
                return false;

            if (poly.ExteriorRing.Where(p => other.Contains(p)).Any())
                return true;

            if (other.ExteriorRing.Where(p => poly.Contains(p)).Any())
                return true;

            foreach(GridLineSegment line in poly.ExteriorSegments)
            {
                if (other.Intersects(line))
                    return true;
            }

            return false;
        }
    }
}