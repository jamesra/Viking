using System;
using System.Collections.Generic;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    /// <summary>
    /// Represents the Type column in the SQL Locations table
    /// </summary>
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
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
        /// Ring of line segments with a line width
        /// </summary>
        CLOSEDCURVE = 7 
    };

    /// <summary>
    /// An interface to an annotation that marks a location in a structure.  Locations on the same section should not overlap.  Locations on adjacent structure physically connected should be linked.
    /// </summary>
    public interface ILocation : IEquatable<ILocation>
    {
        /// <summary>
        /// Unique ID of the location
        /// </summary>
        ulong ID { get; }

        /// <summary>
        /// The structure ID the annotation belongs to
        /// </summary>
        ulong ParentID { get; }

        /// <summary>
        /// True if the location marks where a structure process ends as part of normal biology
        /// </summary>
        bool Terminal { get; }

        /// <summary>
        /// True if the location marks where a structure goes off the edge of a volume
        /// </summary>
        bool OffEdge { get; }

        /// <summary>
        /// True if the location is a vericosity cap, this terminates a process in a structure
        /// </summary>
        bool IsVericosityCap { get; }

        /// <summary>
        /// True if the location indicates a boundary beyond which the structure cannot be traced
        /// </summary>
        bool IsUntraceable { get; }

        IDictionary<string, string> Attributes { get; }

        long UnscaledZ { get; }

        string TagsXml { get; }

        /// <summary>
        /// What type of shape is used to encode this annotation
        /// </summary>
        LocationType TypeCode { get; }

        /// <summary>
        /// Z level of annotation.  This is expected to be scaled into the same coordinates as this interface's geometry attribute
        /// </summary>
        double Z { get; }

        /// <summary>
        /// Volume space shape
        /// </summary>
        Microsoft.SqlServer.Types.SqlGeometry Geometry { get; }

    }
}