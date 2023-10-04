namespace Geometry
{
    /// <summary>
    /// A simple helper class to manage mapping values into ranges
    /// </summary>
    public class Range
    {
        private readonly double _min;
        private double _max => _min + _range;
        private readonly double _range;

        public Range(double min, double max)
        {
            _min = min; 
            _range = max - min;
        }

        /// <summary>
        /// Return the fractional distance between min and max values in the range
        /// </summary>
        /// <param name="clip">If true, values outside the range are clipped to 0 or 1, whichever is closer.</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public double Normalize(double value, bool clip = false)
        {
            double fraction = (value - _min) / _range;
            return clip ? 
                fraction <= 0 
                    ? 0
                    : fraction >= 1.0 
                        ? 1.0 
                        : fraction
                : fraction;
        }

        /// <summary>
        /// Return the value at the given fraction of the range
        /// </summary>
        /// <param name="fraction"></param>
        /// <returns></returns>
        public double Interpolate(double fraction) => (fraction * _range) + _min;

        public override string ToString() => $"Range({_min}, {_max})";

        /// <summary>
        /// If the value falls outside the range, return min or max value.  Otherwise return the passed value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public double Clip(double value) => value <= _min ? _min : value >= _max ? _max : value;
    }
}