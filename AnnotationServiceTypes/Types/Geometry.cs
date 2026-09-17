
using System;
using System.Runtime.Serialization;
using ProtoBuf;
#if NET48
using System.Data.Entity;
#endif
using System.Data.SqlTypes;


namespace AnnotationService.Types
{

    [DataContract]
    [ProtoContract()]
    [Serializable]
    public struct AnnotationPoint(double x, double y, double z)
    {
        [DataMember]
        [ProtoMember(1)]
        public double X { get; set; } = x;

        [DataMember]
        [ProtoMember(2)]
        public double Y { get; set; } = y;

        [DataMember]
        [ProtoMember(3)]
        public double Z { get; set; } = z;
    }

    [DataContract]
    [ProtoContract]
    [Serializable]
    public struct BoundingRectangle(double xmin, double ymin, double xmax, double ymax)
    {
        [DataMember]
        [ProtoMember(1)]
        public double XMax { get; set; } = xmax;

        [DataMember]
        [ProtoMember(2)]
        public double XMin { get; set; } = xmin;

        [DataMember]
        [ProtoMember(3)]
        public double YMax { get; set; } = ymax;

        [DataMember]
        [ProtoMember(4)]
        public double YMin { get; set; } = ymin;

        public readonly double Width => XMax - XMin;

        public readonly double Height => YMax - YMin;

        public readonly double Area => Width * Height;


#if NET48
        public readonly System.Data.Entity.Spatial.DbGeometry ToGeometry() => System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
#endif
    }

    [ProtoContract]
    [DataContract]
    [Serializable]
    public struct BoundingBox(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
    {
        [ProtoMember(1)]
        [DataMember]
        public double XMin { get; set; } = xmin;

        [ProtoMember(2)]
        [DataMember]
        public double YMin { get; set; } = ymin;

        [ProtoMember(3)]
        [DataMember]
        public double ZMin { get; set; } = zmin;

        [ProtoMember(4)]
        [DataMember]
        public double XMax { get; set; } = xmax;

        [ProtoMember(5)]
        [DataMember]
        public double YMax { get; set; } = ymax;

        [ProtoMember(6)]
        [DataMember]
        public double ZMax { get; set; } = zmax;

        public readonly double Width => XMax - XMin;

        public readonly double Height => YMax - YMin;

        public readonly double Depth => ZMax - ZMin;
#if NET48
        public readonly System.Data.Entity.Spatial.DbGeometry ToGeometry() => System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
#endif
    }

}
