using ProtoBuf;

namespace Viking.AnnotationServiceTypes.gRPC
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public class Scale
    {
        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public AxisUnits X { get; private set; }
        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public AxisUnits Y { get; private set; }
        [ProtoMember(3)]
        /* Recoded [DataMember] */
        public AxisUnits Z { get; private set; }

        public Scale(AxisUnits X, AxisUnits Y, AxisUnits Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public Scale()
        { }
    }
}

