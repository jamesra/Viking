using System;

namespace UnitsAndScale
{
    /// <summary>
    /// Describes scale along an axis
    /// </summary>
    [Serializable]
    public class AxisUnits : IAxisUnits
    {
        double IAxisUnits.Value => this.Value;
        string IAxisUnits.Units => Units;
        
        public double Value { get; private set; }
        public string Units { get; private set; }

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units;
        }
    }

    /// <summary>
    /// Describes the scale for each axis in a 3D scene
    /// </summary>
    [Serializable]
    public class Scale : IScale
    {
        public IAxisUnits X { get; private set; }
        public IAxisUnits Y { get; private set; }
        public IAxisUnits Z { get; private set; }

        public Scale(IAxisUnits X, IAxisUnits Y, IAxisUnits Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }
    }
}
