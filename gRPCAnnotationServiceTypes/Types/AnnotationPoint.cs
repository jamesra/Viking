using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace Viking.AnnotationServiceTypes.gRPC
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
        /* Recoded [DataMember] */
        public double X
        {
            get { return _X; }
            set { _X = value; }
        }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public double Y
        {
            get { return _Y; }
            set { _Y = value; }
        }

        [ProtoMember(3)]
        /* Recoded [DataMember] */
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

}

