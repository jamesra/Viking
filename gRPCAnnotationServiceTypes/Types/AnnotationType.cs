using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace Viking.AnnotationServiceTypes.gRPC
{
    [DataContract]
    [ProtoContract]
    public enum AnnotationType : Int32
    {
        [EnumMember]
        [ProtoEnum]
        POINT = 0,
        [EnumMember]
        [ProtoEnum]
        CIRCLE = 1,
        [EnumMember]
        [ProtoEnum]
        ELLIPSE = 2,
        [EnumMember]
        [ProtoEnum]
        POLYLINE = 3,
        /// <summary>
        /// Polygon, no smoothing of exterior verticies with curve fitting
        /// </summary>
        [EnumMember]
        [ProtoEnum]
        POLYGON = 4,     
        /// <summary>
        /// Line segments with a line width, additional control points created using curve fitting function
        /// </summary>
        [EnumMember]
        [ProtoEnum]
        OPENCURVE = 5,
        /// <summary>
        /// Polygon whose outer and inner verticies are supplimented with a curve fitting function
        /// </summary>
        [EnumMember]
        [ProtoEnum]
        CURVEPOLYGON = 6,
        /// <summary>
        /// Ring of line segments with a line width
        /// </summary>
        [EnumMember]
        [ProtoEnum]
        CLOSEDCURVE = 7
    };
}
