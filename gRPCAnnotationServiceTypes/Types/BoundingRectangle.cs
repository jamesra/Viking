using NetTopologySuite.Geometries;
using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace Viking.gRPC.AnnotationTypes
{

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
        /* Recoded [DataMember] */
        public double XMin
        {
            get { return _XMin; }
            set { _XMin = value; }
        }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public double YMin
        {
            get { return _YMin; }
            set { _YMin = value; }
        }

        [ProtoMember(3)]
        /* Recoded [DataMember] */
        public double XMax
        {
            get { return _XMax; }
            set { _XMax = value; }
        }

        [ProtoMember(4)]
        /* Recoded [DataMember] */
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
        public Geometry ToGeometry()
        {
            Coordinate[] coords = new Coordinate[]
            {
                new Coordinate(XMin, YMin),
                new Coordinate(XMin, YMax),
                new Coordinate(XMax, YMax),
                new Coordinate(XMax, YMin),
                new Coordinate(XMin, YMin),
            };

            return Polygon.DefaultFactory.CreatePolygon(coords);
        }
    }

}

