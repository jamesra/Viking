using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{

    [ProtoContract]
    [DataContract]
    [Serializable]
    public struct AnnotationPoint
    {
        private double _X;
        private double _Y;
        private double _Z;

        [ProtoMember(1)]
        [DataMember]
        public double X
        {
            get { return _X; }
            set { _X = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public double Y
        {
            get { return _Y; }
            set { _Y = value; }
        }

        [ProtoMember(3)]
        [DataMember]
        public double Z
        {
            get { return _Z; }
            set { _Z = value; }
        }

        public AnnotationPoint(double x, double y, double z)
        {
            _X = x;
            _Y = y;
            _Z = z;
        }
    }

    [ProtoContract]
    [DataContract]
    [Serializable]
    public struct BoundingRectangle
    {
        private double _XMin;
        private double _YMin;
        private double _XMax;
        private double _YMax;

        [ProtoMember(1)]
        [DataMember]
        public double XMin
        {
            get { return _XMin; }
            set { _XMin = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public double YMin
        {
            get { return _YMin; }
            set { _YMin = value; }
        }

        [ProtoMember(3)]
        [DataMember]
        public double XMax
        {
            get { return _XMax; }
            set { _XMax = value; }
        }

        [ProtoMember(4)]
        [DataMember]
        public double YMax
        {
            get { return _YMax; }
            set { _YMax = value; }
        }

        public double Width
        {
            get { return _XMax - _XMin; }
        }

        public double Height
        {
            get { return _YMax - _YMin; }
        }

        public double Area
        {
            get { return Width * Height; }
        }

        public BoundingRectangle(double xmin, double ymin, double xmax, double ymax)
        {
            _XMin = xmin;
            _YMin = ymin;
            _XMax = xmax;
            _YMax = ymax;
        }
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
    }

    [ProtoContract]
    [DataContract]
    [Serializable]
    public struct BoundingBox
    {
        private double _XMin;
        private double _YMin;
        private double _ZMin;
        private double _XMax;
        private double _YMax;
        private double _ZMax;

        [ProtoMember(1)]
        [DataMember]
        public double XMin
        {
            get { return _XMin; }
            set { _XMin = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public double YMin
        {
            get { return _YMin; }
            set { _YMin = value; }
        }

        [ProtoMember(3)]
        [DataMember]
        public double ZMin
        {
            get { return _ZMin; }
            set { _ZMin = value; }
        }

        [ProtoMember(4)]
        [DataMember]
        public double XMax
        {
            get { return _XMax; }
            set { _XMax = value; }
        }

        [ProtoMember(5)]
        [DataMember]
        public double YMax
        {
            get { return _YMax; }
            set { _YMax = value; }
        }

        [ProtoMember(6)]
        [DataMember]
        public double ZMax
        {
            get { return _ZMax; }
            set { _ZMax = value; }
        }

        public double Width => _XMax - _XMin;

        public double Height => _YMax - _YMin;

        public double Depth
        {
            get { return _ZMax - _ZMin; }
        }

        public BoundingBox(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
        {
            _XMin = xmin;
            _YMin = ymin;
            _ZMin = zmin;
            _XMax = xmax;
            _YMax = ymax;
            _ZMax = zmax;
        }
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
    }

}
