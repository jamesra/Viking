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
        /// <param name="value"></param>
        /// <returns></returns>
        public double Normalize(double value)
        {
            return (value - _min) / _range;
        }

        /// <summary>
        /// Return the value at the given fraction of the range
        /// </summary>
        /// <param name="fraction"></param>
        /// <returns></returns>
        public double Interpolate(double fraction)
        {
            return (fraction * _range) + _min;
        }

        public override string ToString()
        {
            return $"Range({_min}, {_max})";
        }
    }
}