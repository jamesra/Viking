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

    public interface ILocation : IEquatable<ILocation>
    {
        ulong ID { get; }

        ulong ParentID { get; }

        bool Terminal { get; }
        bool OffEdge { get; }

        bool IsVericosityCap { get; }

        bool IsUntraceable { get; }

        IDictionary<string, string> Attributes { get; }

        long UnscaledZ { get; }

        string TagsXml { get; }

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