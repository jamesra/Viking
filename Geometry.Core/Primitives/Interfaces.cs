using System;
using System.Collections.Generic;

namespace Geometry
{
    public enum RotationDirection
    {
        Clockwise,
        Counterclockwise,
        Colinear
    }

    /// <summary>
    /// Distinguishes outside, inside, boundary, and crossing. Contains/Intersects are wrappers over this.
    /// </summary>
    [Flags]
    public enum ShapeRelation
    {
        None = 0,
        Contained = 0x01,
        Touching = 0x02,
        Intersecting = 0x04
    }

    public enum ShapeType2D
    {
        Point = 0,
        Circle = 1,
        Polygon = 4,
        Rectangle = 8,
        Triangle = 9,
        Line = 10,
        Collection = 11,
        Polyline = 12,
        InfiniteLine = 13,
        Quad = 14
    };

    public static class ShapeTypeExtension
    {
        public static bool IsOpen(this ShapeType2D type)
        {
            return type switch
            {
                ShapeType2D.Line or ShapeType2D.Polyline or ShapeType2D.InfiniteLine => true,
                _ => false,
            };
        }

        public static bool IsClosed(this ShapeType2D type)
        {
            return type switch
            {
                ShapeType2D.Point or ShapeType2D.Line or ShapeType2D.Polyline or ShapeType2D.InfiniteLine => false,
                _ => true,
            };
        }

        public static bool IsPoint(this ShapeType2D type)
        {
            return type switch
            {
                ShapeType2D.Point => true,
                _ => false,
            };
        }
    }

    /// <summary>
    /// An N-dimensional point. <see cref="Coords"/> is a copy or snapshot; mutating the array does not mutate the point.
    /// </summary>
    public interface IPointN
    {
        double[] Coords { get; }
    }

    public interface ICentroid
    {
        IPoint2D Centroid { get; }
    }

    /// <summary>
    /// A 2D point. Does not include a dummy Z; use <see cref="IPoint3D"/> for 3D.
    /// </summary>
    public interface IPoint2D : IPointN, IEquatable<IPoint2D>, ICentroid
    {
        double X { get; }
        double Y { get; }
    }

    /// <summary>
    /// A 3D point. Independent of <see cref="IPoint2D"/> so 2D types are not forced to invent a Z.
    /// </summary>
    public interface IPoint3D : IPointN, IEquatable<IPoint3D>
    {
        double X { get; }
        double Y { get; }
        double Z { get; }
    }

    public interface IShape2D : IEquatable<IShape2D>
    {
        Rectangle BoundingBox { get; }
        double Area { get; }
        bool Contains(in IPoint2D p);

        ShapeRelation GetRelation(in IPoint2D p);

        ShapeRelation GetRelation(in ILineSegment2D line);

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

        int TotalVertices { get; }

        int TotalUniqueVertices { get; }
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

    public interface ILine2D : IShape2D, IEquatable<ILine2D>
    {
        IPoint2D Origin { get; }
        IPoint2D Direction { get; }
    }

    public interface IBox3D : IEquatable<IBox3D>
    {
        IPoint3D Min { get; }
        IPoint3D Max { get; }
        double Volume { get; }
        bool Contains(in IPoint3D p);
        IBox3D Translate(in IPoint3D offset);
    }

    public interface IRectangle2D : IShape2D, IEquatable<IRectangle2D>, ICentroid
    {
        double Left { get; }
        double Right { get; }
        double Top { get; }
        double Bottom { get; }
        IPoint2D Center { get; }
    }
}
