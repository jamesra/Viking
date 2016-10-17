using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Annotation
{
    [DataContract]
    public class AxisUnits
    {
        [DataMember]
        public double Value { get; private set; }
        [DataMember]
        public string Units { get; private set; }

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units; 
        }
    }

    [DataContract]
    public class Scale
    {
        [DataMember]
        public AxisUnits X { get; private set; }
        [DataMember]
        public AxisUnits Y { get; private set; }
        [DataMember]
        public AxisUnits Z { get; private set; }

        public Scale(AxisUnits X, AxisUnits Y, AxisUnits Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z; 
        }
    }
}
