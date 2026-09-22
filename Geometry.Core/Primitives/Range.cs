using System;

namespace Geometry
{
    public interface IRange
    {
        double Min { get; }
        double Max { get; }
        double Span { get; }
    }

    /// <summary>
    /// Closed numeric interval with normalize/interpolate/clip helpers.
    /// </summary>
    public readonly struct Range : IRange, ICloneable
    {
        public double Min { get; }
        public double Max { get; }
        public double Span => Max - Min;

        double IRange.Min => Min;
        double IRange.Max => Max;
        double IRange.Span => Span;

        public Range(double min, double max)
        {
            if (min > max)
                throw new ArgumentException($"{min} > {max}");

            Min = min;
            Max = max;
        }

        /// <summary>Closed interval: true when <paramref name="value"/> is in [<see cref="Min"/>, <see cref="Max"/>]. Not OGC Contains.</summary>
        public bool Contains(double value) => value >= Min && value <= Max;

        public double Normalize(double value, bool clip = false)
        {
            if (Span == 0)
                return 0;

            double fraction = (value - Min) / Span;
            if (!clip)
                return fraction;

            if (fraction <= 0)
                return 0;
            if (fraction >= 1.0)
                return 1.0;
            return fraction;
        }

        public double Interpolate(double fraction) => (fraction * Span) + Min;

        public double Clip(double value) => value <= Min ? Min : value >= Max ? Max : value;

        public override string ToString() => Min == Max ? $"{Min:F4}" : $"{Min:F4} - {Max:F4}";

        public object Clone() => new Range(Min, Max);
    }
}
