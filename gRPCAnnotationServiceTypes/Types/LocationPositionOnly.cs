//using Annotation;
using ProtoBuf;

namespace Viking.AnnotationServiceTypes.gRPC
{
    [ProtoContract]
    /* Recoded [DataContract] */
    public class LocationPositionOnly : DataObjectWithKeyOfLong
    {
        AnnotationPoint _Position;
        private double _Radius;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        //[Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }


    }
}

