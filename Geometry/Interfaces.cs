using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        
    }

    public interface IContinuousTransform : ITransform
    { 
    }

    /// <summary>
    /// Interface for transforms that expose control points
    /// </summary>
    public interface ITransformControlPoints
    { 
        MappingGridVector2[] MapPoints { get; }

        List<MappingGridVector2> IntersectingControlRectangle(GridRectangle gridRect);

        List<MappingGridVector2> IntersectingMappedRectangle(GridRectangle gridRect);

        GridRectangle ControlBounds { get; }
        GridRectangle MappedBounds { get; }
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
        Geometry.Transforms.TransformInfo Info { get; }
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
        string Extension { get; set; }
        string CacheDirectory { get; }
        string CacheFilename { get; }
        string CacheFullPath { get; }
    }

}
