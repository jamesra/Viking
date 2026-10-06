using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Geometry.Transforms;

namespace Geometry
{
    /// <summary>
    /// A transform that may or may not be discrete
    /// </summary>
    public interface ITransform
    {
        Vector2 Transform(in Vector2 Point);
        Vector2[] Transform(in Vector2[] Points);

        Vector2 InverseTransform(in Vector2 Point);
        Vector2[] InverseTransform(in Vector2[] Points);

        bool CanTransform(in Vector2 Point);
        bool TryTransform(in Geometry.Vector2 Point, out Vector2 v);
        bool[] TryTransform(in Vector2[] Points, out Vector2[] v);

        bool CanInverseTransform(in Vector2 Point);
        bool TryInverseTransform(in Vector2 Point, out Vector2 v);
        bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v);
    }

    /// <summary>
    /// Adds helper methods to ITransform interface useful for discrete transforms
    /// </summary>
    public interface IDiscreteTransform : ITransform
    {
        Rectangle ControlBounds { get; }

        Rectangle MappedBounds { get; }

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
        double ConvexHullIntersection(in LineSegment L, Vector2 OutsidePoint, out LineSegment foundCtrlLine, out LineSegment foundMapLine, out Vector2 intersection);

    }

    public interface IContinuousTransform : ITransform
    {
        /// <summary>
        /// Translates the control space mapping.  So if source maps to control space with no offset, and
        /// we translate target by (x = 1, y = 0).  The source space at (0,0) now maps to (1,0) in target space.
        /// </summary>
        /// <param name="vector"></param>
        void Translate(in Vector2 vector);
    }

    /// <summary>
    /// Interface for transforms that expose control points
    /// </summary>
    public interface ITransformControlPoints : ITransform
    {
        MappingVector2[] MapPoints { get; }

        List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect);

        List<MappingVector2> IntersectingMappedRectangle(in Rectangle gridRect);

        Rectangle ControlBounds { get; }
        Rectangle MappedBounds { get; }
    }


    public interface IControlPointTriangulation : ITransformControlPoints
    {
        int[] TriangleIndicies { get; }

        List<int>[] Edges { get; }
    }

    public interface IITKSerialization
    {
        /// <summary>
        /// Return the transform in ITK format
        /// </summary>
        /// <returns></returns>
        string GetITKTransform();
    }

    public interface IJSonSerialization
    {
        /// <summary>
        /// Return the transform in ITK format
        /// </summary>
        /// <returns></returns>
        string GetJson();
    }

    public interface IMemoryMinimization
    {
        void MinimizeMemory();
    }

    /// <summary>
    /// A transform that builds its spatial indexes on first use and can start building them early, so the first query
    /// does not pay for the build. Only worth calling for a transform that is about to be queried.
    /// </summary>
    public interface ISpatialIndexPrewarm
    {
        /// <summary>
        /// Starts the build on the thread pool without waiting. The task completes when the indexes exist; callers usually
        /// discard it. Queries made before then wait for the build in progress.
        /// </summary>
        Task PrewarmSpatialIndexAsync();
    }

    /// <summary>
    /// A transform that can estimate the managed memory it holds, counting only the caches it has actually built. Used by
    /// memory-budgeted caches; an estimate from element counts, not a measurement.
    /// </summary>
    public interface IMemoryEstimate
    {
        /// <summary>
        /// Estimated bytes. Cheap, lock-free and safe to read while other threads use the transform; never builds anything.
        /// A race with a build or <see cref="IMemoryMinimization.MinimizeMemory"/> only makes one reading stale.
        /// </summary>
        long EstimatedMemoryBytes { get; }
    }

    public interface ITransformInfo
    {
        TransformBasicInfo Info { get; set; }
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
