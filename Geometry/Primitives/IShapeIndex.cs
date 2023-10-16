using System;

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
        int iShape { get; }

        /// <summary>
        /// If the shape contains inner shapes, the index of the inner shape
        /// </summary>
        int? iInnerShape { get; }

        /// <summary>
        /// Index of the vertex in the shape
        /// </summary>
        int iVertex { get; }

        /// <summary>
        /// The number of verticies in the shape the index refers to. Unique is in the name because for closed shapes the first and last index are identical and the duplicate is not counted.
        /// </summary>
        int NumUnique { get;}

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
    }
}