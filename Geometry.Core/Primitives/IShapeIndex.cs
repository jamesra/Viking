using System;
using System.Collections.Generic;

namespace Geometry
{
    public interface IShapeIndex : IComparable<IShapeIndex>, IEquatable<IShapeIndex>, ICloneable
    {
        /// <summary>
        /// The type the index refers to
        /// </summary>
        ShapeType2D ShapeType { get; }

        /// <summary>
        /// Shape the index refers to if there is an array of shapes
        /// </summary>
        int ShapeIndex { get; }

        /// <summary>
        /// If the shape contains inner shapes, the index of the inner shape
        /// </summary>
        int? InnerShapeIndex { get; }

        /// <summary>
        /// Index of the vertex in the shape
        /// </summary>
        int VertexIndex { get; }

        /// <summary>
        /// The number of verticies in the shape the index refers to. Unique is in the name because for closed shapes the first and last index are identical and the duplicate is not counted.
        /// </summary>
        int NumUnique { get; }

        /// <summary>
        /// True if the vertex refers to an inner shape
        /// </summary>
        bool IsInner { get; }

        /// <summary>
        /// The first vertex in the shape being indexed
        /// </summary>
        IShapeIndex FirstVertexInShape { get; }

        /// <summary>
        /// The last vertex in the shape being indexed
        /// </summary>
        IShapeIndex LastVertexInShape { get; }

        /// <summary>
        /// The next vertex in the shape
        /// </summary>
        IShapeIndex Next { get; }

        /// <summary>
        /// The previous vertex in the shape
        /// </summary>
        IShapeIndex Previous { get; }

        /// <summary>
        /// Return a copy of this IShapeIndex with ShapeIndex value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        IShapeIndex Reindex(int shapeIndex);

        /// <summary>
        /// Return the specified point, ignoring the ShapeIndex attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        Vector2 Point(in IShape2D Shape);

        /// <summary>
        /// Return the point corresponding to this index
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        Vector2 Point(in IReadOnlyList<IShape2D> Shapes);

        /// <summary>
        /// Return the point corresponding to this index
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        Vector2 Point(in IReadOnlyDictionary<int, IShape2D> Shapes);

        /// <summary>
        /// Return the normal of the index, with no weighting according to adjacent line segment length.
        /// This is used to determine if two points have normals within 90 degrees of each other
        /// </summary>
        /// <param name="Shapes"></param>
        /// <returns></returns>
        Vector2 GetOrientation(in IReadOnlyList<IShape2D> Shapes);
    }
}