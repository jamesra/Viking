
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
    public struct AnnotationPoint
    { 
        [DataMember]
        [ProtoMember(1)] 
        public double X { get; set; } 

        [DataMember]
        [ProtoMember(2)] 
        public double Y { get; set; } 

        [DataMember]
        [ProtoMember(3)] 
        public double Z { get; set; } 

        public AnnotationPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
         

    }

    [DataContract]
    [ProtoContract] 
    [Serializable]
    public struct BoundingRectangle
    {
        [DataMember]
        [ProtoMember(1)] 
        public double XMax { get; set; } 

        [DataMember]
        [ProtoMember(2)] 
        public double XMin { get; set; } 

        [DataMember]
        [ProtoMember(3)] 
        public double YMax { get; set; } 

        [DataMember]
        [ProtoMember(4)] 
        public double YMin { get; set; } 

        public double Width => XMax - XMin;

        public double Height => YMax - YMin;

        public double Area => Width * Height;

        public BoundingRectangle(double xmin, double ymin, double xmax, double ymax)
        {
            XMin = xmin;
            YMin = ymin;
            XMax = xmax;
            YMax = ymax;
        }
         

#if NET48
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
#endif
    }

    [ProtoContract]
    [DataContract]
    [Serializable]
    public struct BoundingBox
    { 
        [ProtoMember(1)]
        [DataMember]
        public double XMin { get; set; } 

        [ProtoMember(2)]
        [DataMember]
        public double YMin { get; set; } 

        [ProtoMember(3)]
        [DataMember]
        public double ZMin { get; set; } 

        [ProtoMember(4)]
        [DataMember]
        public double XMax { get; set; } 

        [ProtoMember(5)]
        [DataMember]
        public double YMax { get; set; } 

        [ProtoMember(6)]
        [DataMember]
        public double ZMax { get; set; } 

        public double Width => XMax - XMin;

        public double Height => YMax - YMin;

        public double Depth => ZMax - ZMin;

        public BoundingBox(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
        {
            XMin = xmin;
            YMin = ymin;
            ZMin = zmin;
            XMax = xmax;
            YMax = ymax;
            ZMax = zmax;
        }
#if NET48
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
#endif
    }

}
