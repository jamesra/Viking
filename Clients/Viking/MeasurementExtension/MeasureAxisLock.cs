namespace MeasurementExtension
{
    /// <summary>
    /// SHIFT (horizontal) and CTRL (vertical) axis lock for the end point of <see cref="MeasureCommand"/>.
    /// </summary>
    public static class MeasureAxisLock
    {
        /// <summary>
        /// Returns the measured end point with the requested axis lock applied. The result stays in double
        /// precision: volume coordinates reach the hundreds of thousands of pixels, where a float round trip
        /// would leave a non-zero offset on the locked axis and skew the displayed distance.
        /// </summary>
        public static Geometry.Vector2 Apply(Geometry.Vector2 origin, Geometry.Vector2 cursor, bool horizontal, bool vertical)
        {
            if (horizontal)
                return new Geometry.Vector2(cursor.X, origin.Y);
            if (vertical)
                return new Geometry.Vector2(origin.X, cursor.Y);
            return cursor;
        }
    }
}
