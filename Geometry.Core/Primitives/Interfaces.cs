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
    /// Distinguishes outside, inside, boundary, and crossing.
    /// <see cref="IShape2D.Contains"/>, <see cref="IShape2D.Covers"/>, and <see cref="IShape2D.Intersects"/>
    /// are wrappers over this.
    /// </summary>
    /// <remarks>
    /// Flags so a collection can OR child results.
    /// Spatial predicates follow OGC Simple Features (DE-9IM): Open Geospatial Consortium,
    /// OpenGIS Implementation Standard for Geographic information — Simple feature access —
    /// Part 1: Common architecture, OGC 06-103r4 (2011). See also Egenhofer and Franzosa,
    /// "Point-Set Topological Spatial Relations," Int. J. Geographical Information Systems
    /// 5(2):161–174 (1991).
    /// </remarks>
    [Flags]
    public enum ShapeRelation
    {
        None = 0,
        Contained = 0x01,
        Touching = 0x02,
        Intersecting = 0x04
    }

    /// <summary>
    /// Maps <see cref="ShapeRelation"/> onto OGC Contains / Covers (OGC 06-103r4).
    /// Crossing (<see cref="ShapeRelation.Intersecting"/>) is neither.
    /// </summary>
    public static class ShapeRelationExtensions
    {
        /// <summary>
        /// OGC Contains: the other geometry's interior lies in this shape's interior.
        /// Boundary-only contact is false.
        /// </summary>
        public static bool IsContains(this ShapeRelation relation) =>
            (relation & ShapeRelation.Contained) != 0 &&
            (relation & ShapeRelation.Intersecting) == 0;

        /// <summary>
        /// OGC Covers: no point of the other geometry is outside this shape (boundary counts).
        /// </summary>
        public static bool IsCovers(this ShapeRelation relation) =>
            relation != ShapeRelation.None &&
            (relation & ShapeRelation.Intersecting) == 0;
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

        /// <summary>
        /// Bounded area types, including a degenerate point. Line, polyline, and infinite line are open.
        /// </summary>
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

    /// <summary>Center of mass of a 2D shape.</summary>
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

    /// <summary>
    /// 2D geometry with OGC-style predicates. <see cref="GetRelation(in IPoint2D)"/> is the
    /// source of truth; <see cref="Contains"/> and <see cref="Covers"/> are wrappers.
    /// </summary>
    public interface IShape2D : IEquatable<IShape2D>
    {
        Rectangle BoundingBox { get; }
        double Area { get; }

        /// <summary>
        /// OGC Contains: true when <paramref name="p"/> lies in this shape's interior.
        /// Boundary points are false. For hit-testing, AABB culling, or Delaunay in-circle use
        /// <see cref="Covers"/>; use <see cref="GetRelation(in IPoint2D)"/> to distinguish interior, boundary, and exterior.
        /// </summary>
        bool Contains(in IPoint2D p);

        /// <summary>
        /// OGC Covers / closed-set test: true when <paramref name="p"/> is in the interior or on the boundary.
        /// Use this for hit-testing, AABB culling, and Delaunay in-circle.
        /// For a point this is <c>GetRelation(p) != ShapeRelation.None</c>.
        /// </summary>
        bool Covers(in IPoint2D p);

        /// <summary>
        /// Classifies <paramref name="p"/> as outside (<see cref="ShapeRelation.None"/>),
        /// interior (<see cref="ShapeRelation.Contained"/>), or boundary (<see cref="ShapeRelation.Touching"/>).
        /// Source of truth for <see cref="Contains"/> and <see cref="Covers"/>.
        /// Point tests return exactly one of those three flags, never <see cref="ShapeRelation.Intersecting"/>.
        /// </summary>
        ShapeRelation GetRelation(in IPoint2D p);

        /// <summary>
        /// Classifies how <paramref name="line"/> sits relative to this shape (disjoint, interior, boundary, or crossing).
        /// </summary>
        ShapeRelation GetRelation(in ILineSegment2D line);

        /// <summary>
        /// True when this shape and <paramref name="shape"/> are not disjoint (interiors or boundaries meet).
        /// Containment counts as intersecting.
        /// </summary>
        bool Intersects(in IShape2D shape);

        ShapeType2D ShapeType { get; }

        /// <summary>Copy translated by <paramref name="offset"/>; this instance is unchanged.</summary>
        IShape2D Translate(in IPoint2D offset);
    }

    /// <summary>
    /// Vertex list plus <see cref="IShape2D.ShapeType"/> so callers can interpret control points
    /// without casting to ILineSegment2D / IPolyLine2D / IPolygon2D.
    /// </summary>
    public interface IHasControlPoints : IShape2D
    {
        /// <summary>
        /// Primary/exterior vertices only. Polygon holes stay on <see cref="IPolygon2D.InteriorRings"/>.
        /// </summary>
        IReadOnlyList<IPoint2D> ControlPoints { get; }
    }

    /// <summary>
    /// Closed CCW rings (first vertex equals last). Holes are <see cref="InteriorPolygons"/>, not extra ControlPoints.
    /// </summary>
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

        /// <summary>
        /// Consecutive segments. Implementations must rebuild from <see cref="Points"/> if a cache is null or stale;
        /// do not return a null backing field.
        /// </summary>
        IReadOnlyList<ILineSegment2D> LineSegments { get; }

        double Length { get; }
    }

    public interface ITriangle2D : IShape2D, IEquatable<ITriangle2D>, ICentroid
    {
        IPoint2D[] Points { get; }
    }

    /// <summary>
    /// Finite segment. Endpoints are boundary (<see cref="ShapeRelation.Touching"/>); the open segment is interior.
    /// </summary>
    public interface ILineSegment2D : IShape2D, IEquatable<ILineSegment2D>, ICentroid
    {
        IPoint2D A { get; }
        IPoint2D B { get; }

        double Length { get; }
    }

    /// <summary>
    /// Infinite line. Empty boundary: every on-line point is interior (<see cref="ShapeRelation.Contained"/>).
    /// </summary>
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
