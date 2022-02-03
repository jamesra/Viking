using System.Collections.Generic;
using Geometry.Transforms;

namespace Geometry
{
    /// <summary>
    /// A transform that may or may not be discrete
    /// </summary>
    public interface ITransform
    {
        GridVector2 Transform(GridVector2 Point);
        GridVector2[] Transform(GridVector2[] Points);

        GridVector2 InverseTransform(GridVector2 Point);
        GridVector2[] InverseTransform(GridVector2[] Points);

        bool CanTransform(GridVector2 Point);
        bool TryTransform(GridVector2 Point, out GridVector2 v);
        bool[] TryTransform(GridVector2[] Points, out GridVector2[] v);

        bool CanInverseTransform(GridVector2 Point);
        bool TryInverseTransform(GridVector2 Point, out GridVector2 v);
        bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] v);

        void Translate(GridVector2 vector);
    }

    /// <summary>
    /// Adds helper methods to ITransform interface useful for discrete transforms
    /// </summary>
    public interface IDiscreteTransform : ITransform
    {
        GridRectangle ControlBounds { get; }

        GridRectangle MappedBounds { get; }

        /// <summary>
        /// Find the edge which intersects the passed edge L.
        /// Return the distance to the intersection point.  If they exist the out parameters are intersection point and the Control and Mapped Line.
        /// </summary>
        /// <param name="L">Line to test for intersection with the transform</param>
        /// <param name="OutsidePoint">Point on line which is outside the convex hull from which distance is calculated</param>
        /// <param name="foundCtrlLine"></param>
        /// <param name="foundMapLine"></param>
        /// <param name="intersection">Intersection point</param>
        /// <returns>Distance to intersection or double.MaxValue if no intersection is found</returns>
        double ConvexHullIntersection(GridLineSegment L, GridVector2 OutsidePoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection);

    }

    public interface IContinuousTransform : ITransform
    {
    }

    /// <summary>
    /// Interface for transforms that expose control points
    /// </summary>
    public interface ITransformControlPoints : ITransform
    {
        MappingGridVector2[] MapPoints { get; }

        List<MappingGridVector2> IntersectingControlRectangle(in GridRectangle gridRect);

        List<MappingGridVector2> IntersectingMappedRectangle(in GridRectangle gridRect);

        GridRectangle ControlBounds { get; }
        GridRectangle MappedBounds { get; }
    }

    public interface IControlPointTriangulation : ITransformControlPoints
    {
        int[] TriangleIndicies { get; }

        List<int>[] Edges { get; }
    }

    public interface IITKSerialization
    {
        void WriteITKTransform(System.IO.StreamWriter stream);
    }

    public interface IMemoryMinimization
    {
        void MinimizeMemory();
    }

    public interface ITransformInfo
    {
        TransformBasicInfo Info { get; set; }
    }

    public interface IStosTransformInfo
    {
        Geometry.Transforms.StosTransformInfo Info { get; }
    }

    public interface IGridTransformInfo
    {
        int GridSizeX { get; }

        /// <summary>
        /// Size of y dimension of grid 
        /// </summary>
        int GridSizeY { get; }
    }

    public interface ITransformCacheInfo
    {
        string Extension { get; }
        string CacheDirectory { get; }
        string CacheFilename { get; }
        string CacheFullPath { get; }
    }

}
