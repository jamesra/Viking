using System;
using System.Collections.Generic;

namespace Annotation.Interfaces
{
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4,     //Polygon, no smoothing of exterior verticies with curve fitting
        OPENCURVE = 5,   //Line segments with a line width, additional control points created using curve fitting function
        CURVEPOLYGON = 6, //Polygon whose outer and inner verticies are supplimented with a curve fitting function
        CLOSEDCURVE = 7 //Ring of line segments with a line width
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
        Microsoft.SqlServer.Types.SqlGeometry Geometry {get;}
        
    }
}
