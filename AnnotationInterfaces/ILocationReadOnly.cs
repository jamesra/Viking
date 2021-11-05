using System;
using System.Collections.Generic;
using Geometry;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface ISectionIndex
    {
        long Section { get; }
    }

    public interface ILocationReadOnly : IEquatable<ILocationReadOnly>
    {
        ulong ID { get; }
        ulong ParentID { get; }

        bool Terminal { get; }
        bool OffEdge { get; } 

        bool IsVericosityCap { get; }

        bool IsUntraceable { get; }

        IReadOnlyDictionary<string, string> Attributes { get; }

        /// <summary>
        /// Z as stored in the database, which is a section number
        /// </summary>
        long UnscaledZ { get; }
          
        LocationType TypeCode { get; }

        /// <summary>
        /// Z level of annotation.  This is expected to be scaled into the same coordinates as this interface's geometry attribute
        /// </summary>
        double Z { get; }

        double? Width { get; }


        string MosaicGeometryWKT { get; }
        /// <summary>
        /// Volume space shape
        /// </summary>
        string VolumeGeometryWKT {get;} 
    }

    /// <summary>
    /// The interface to our model object
    /// </summary>
    public interface ILocation : IDataObjectWithKey<Int64>, IEquatable<ILocation>, IChangeAction
    {   
        long? ParentID { get; set; }

        bool Terminal { get; set; }
        bool OffEdge { get; set; }

        string Attributes { get; set; }
        
        /// <summary>
        /// The section number the annotation was placed on (Unscaled Z)
        /// </summary>
        long SectionNumber { get; set; }

        string TagsXml { get; set; }

        bool Closed { get; }

        string Username { get; }

        /// <summary>
        /// What type (geometric shape) of annotation is it?
        /// </summary>
        LocationType TypeCode { get; set; }
          
        GridVector3 VolumePosition { get; }

        GridVector3 MosaicPosition { get; }
         
        DateTime LastModified { get; }

        DateTime Created { get; }

        /// <summary>
        /// Computed column, the radius of a circle with equal area to the annotations geometry.
        /// </summary>
        double Radius { get; }

        /// <summary>
        /// If this is a 1-D geometry, how wide is the line/curve?
        /// </summary>
        double? Width { get; set; }

        /// <summary>
        /// List of location IDs linked to this location
        /// </summary>
        IList<long> Links { get; }

        string MosaicGeometryWKT { get; set; }

        /// <summary>
        /// Volume space shape
        /// </summary>
        string VolumeGeometryWKT { get; set; }
    }
}
