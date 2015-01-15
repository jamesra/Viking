using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{ 
    public struct AxisUnits
    {
        public readonly double Value;
        public readonly string Units;

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units; 
        }
    }

    public struct Scale
    {
        public readonly AxisUnits X;
        public readonly AxisUnits Y;
        public readonly AxisUnits Z;

        public Scale(AxisUnits X, AxisUnits Y, AxisUnits Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z; 
        }
    }
}
