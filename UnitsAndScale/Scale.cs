using System;

namespace UnitsAndScale
{
    /// <summary>
    /// Describes scale along an axis
    /// </summary>
    [Serializable]
    public class AxisUnits(double value, string units) : IAxisUnits
    {
        double IAxisUnits.Value => this.Value;
        string IAxisUnits.Units => Units;

        public double Value { get; private set; } = value;
        public string Units { get; private set; } = units;
    }

    /// <summary>
    /// Describes the scale for each axis in a 3D scene
    /// </summary>
    [Serializable]
    public class Scale(IAxisUnits X, IAxisUnits Y, IAxisUnits Z) : IScale
    {
        public IAxisUnits X { get; private set; } = X;
        public IAxisUnits Y { get; private set; } = Y;
        public IAxisUnits Z { get; private set; } = Z;
    }
}
