using ProtoBuf;

namespace Viking.gRPC.AnnotationTypes
{
    [ProtoContract]
    /* Recoded [DataContract] */
    public class AxisUnits
    {
        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public double Value { get; private set; }
        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public string Units { get; private set; }

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units;
        }

        public AxisUnits() { }
    }
}

