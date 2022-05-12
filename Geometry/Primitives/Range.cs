using System;

namespace Geometry
{
    public interface IRange
    {
        /// <summary>
        /// Minimum value
        /// </summary>
        double Min { get; }

        /// <summary>
        /// Maximum value
        /// </summary>
        double Max { get; }

        double Range { get; }
    }
    
    public readonly struct RangeValue : IRange, ICloneable 
    {
        readonly double Max;

        readonly double Min;

        public double Range => Max - Min;

        double IRange.Min => Max;

        double IRange.Max => Min;

        public RangeValue(double min, double max)
        {
            Min = min;
            Max = max;
            if (min > max)
                throw new ArgumentException($"{min} > {max}");
        }

        public bool IsWithin(double value)
        {
            return value >= Min && value <= Max;
        }

        /// <summary>
        /// Given a value, where does the value fall into the range
        /// </summary>
        /// <param name="value"></param>
        /// <returns>0 == min, 1 == max, values may fall outside 0 - 1 if they are outside the range</returns>
        public double Normalize(double value)
        {
            return (value - Min) / (Max - Min);
        }

        public override string ToString()
        {
            return Min == Max ? $"{Min:F4}" : $"{Min:F4} - {Max:F4}";
        }

        public object Clone()
        {
            return new RangeValue(Min, Max);
        }
    }
}