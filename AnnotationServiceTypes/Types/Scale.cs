using ProtoBuf;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [ProtoContract]
    [DataContract]
    public class AxisUnits
    {
        [ProtoMember(1)]
        [DataMember]
        public double Value { get; private set; }
        [ProtoMember(2)]
        [DataMember]
        public string Units { get; private set; }

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units;
        }

        public AxisUnits() { }
    }

    [ProtoContract]
    [DataContract]
    public class Scale
    {
        [ProtoMember(1)]
        [DataMember]
        public AxisUnits X { get; private set; }
        [ProtoMember(2)]
        [DataMember]
        public AxisUnits Y { get; private set; }
        [ProtoMember(3)]
        [DataMember]
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
