using System;
using System.Collections.Generic;

namespace Geometry
{
    public enum RotationDirection
    {
        CLOCKWISE,
        COUNTERCLOCKWISE,
        COLINEAR
    }

    /// <summary>
    /// I continually run into issues where shapes are sometimes expected to be solid vs. empty and the results of intersection and contain tests need to be unambiguous.  
    /// Over time I'd like my contains tests to return more specific results, but that is a large refactor I want to make over time.
    /// </summary>
    [Flags]
    public enum OverlapType
    {
        /// <summary>
        /// The tested geometry is entirely outside the shape
        /// </summary>
        NONE = 0,
        /// <summary>
        /// The tested geometry is entirely inside the shape
        /// </summary>
        CONTAINED = 0x01,
        /// <summary>
        /// The tested geometry only touches the borders of the shape, for example, a point that is exactly on a line
        /// </summary>
        TOUCHING = 0x02,
        /// <summary>
        /// The tested geometry is both inside and outside the shape
        /// </summary>
        INTERSECTING = 0x04
    }

    public enum ShapeType2D
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        /// <summary>
        /// Polygon, no smoothing of exterior verticies with curve fitting
        /// </summary>
        POLYGON = 4,     
        /// <summary>
        /// Line segments with a line width, additional control points created using curve fitting function
        /// </summary>
        OPENCURVE = 5,
        /// <summary>
        /// Polygon whose outer and inner verticies are supplimented with a curve fitting function
        /// </summary>
        CURVEPOLYGON = 6, 
        /// <summary>
        /// Closed ring of line segments with a line width
        /// </summary>
        CLOSEDCURVE = 7, 
        RECTANGLE = 8,
        TRIANGLE = 9,
        LINE = 10,
        /// <summary>
        /// A collection of many geometry objects
        /// </summary>
        COLLECTION = 11,
        /// <summary>
        /// Points describing a series of connected line segments.
        /// </summary>
        POLYLINE = 12
    };

    public static class ShapeTypeExtension
    {
        public static bool IsOpen(this ShapeType2D type)
        {
            switch (type)
            {
                case ShapeType2D.LINE:
                case ShapeType2D.POLYLINE:
                case ShapeType2D.OPENCURVE:
                    return true;
            }

            return false;
        }

        public static bool IsClosed(this ShapeType2D type)
        {
            switch (type)
            {
                case ShapeType2D.POINT:
                case ShapeType2D.LINE:
                case ShapeType2D.POLYLINE:
                case ShapeType2D.OPENCURVE:
                    return false;
            }

            return true;
        }

        public static bool IsPoint(this ShapeType2D type)
        {
            switch (type)
            {
                case ShapeType2D.POINT:
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Represents an N-dimensional point
    /// </summary>
    public interface IPointN
    {
        double[] coords { get; } 
    }

    public interface ICentroid
    {
        IPoint2D Centroid { get; }
    }


    public interface IPoint2D : IPointN, IEquatable<IPoint2D>, ICentroid, IShape2D
    {
        double X { get; set; }
        double Y { get; set; }
    }

    public interface IPoint : IPointN, IEquatable<IPoint>
    {
        double X { get; set; }
        double Y { get; set; }
        double Z { get; set; }
    }

    public interface IShape2D : IEquatable<IShape2D>
    {
        GridRectangle BoundingBox { get; }
        GridVector2 Centroid { get; }
        double Area { get; }
        bool Contains(in IPoint2D p);

        bool Intersects(in IShape2D shape);

        ShapeType2D ShapeType { get; }

        /// <summary>
        /// Return a new object with the provided offset
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        IShape2D Translate(in IPoint2D offset);
    }

    public interface IPolygon2D : IShape2D, IEquatable<IPolygon2D>, ICentroid
    {
        IReadOnlyList<IPoint2D> ExteriorRing { get; }

        IReadOnlyList<IPoint2D[]> InteriorRings { get; }

        IReadOnlyList<IPolygon2D> InteriorPolygons { get; }

        int TotalVerticies { get; }

        int TotalUniqueVerticies { get; }
    }

    public interface ICircle2D : IShape2D, IEquatable<ICircle2D>, ICentroid
    {
        IPoint2D Center { get; }

        double Radius { get; }
    }

    public interface IShapeCollection2D : IShape2D, IEquatable<IShapeCollection2D>
    {
        IList<IShape2D> Geometries { get; }
    }

    public interface IPolyLine2D : IShape2D, IEquatable<IPolyLine2D>
    {
        IReadOnlyList<IPoint2D> Points { get; }
        IReadOnlyList<ILineSegment2D> LineSegments { get; }
        double Length { get; }
    }

    public interface ITriangle2D : IShape2D, IEquatable<ITriangle2D>, ICentroid
    {
        IPoint2D[] Points { get; } 
    }

    public interface ILineSegment2D : IShape2D, IEquatable<ILineSegment2D>, ICentroid
    {
        IPoint2D A { get; }
        IPoint2D B { get; }

        double Length { get; }
    }

    public interface IRectangle : IShape2D, IEquatable<IRectangle>, ICentroid
    {
        double Left { get; }
        double Right { get; }
        double Top { get; }
        double Bottom { get; }
        IPoint2D Center { get; }
    }
}
