namespace Viking.Common
{
    /// <summary>
    /// Utility methods for mathematical operations.
    /// Provides Clamp methods since Math.Clamp is not available in .NET Framework 4.8.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Clamps an integer value between min and max (inclusive).
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum value</param>
        /// <param name="max">The maximum value</param>
        /// <returns>The clamped value</returns>
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamps a double value between min and max (inclusive).
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum value</param>
        /// <param name="max">The maximum value</param>
        /// <returns>The clamped value</returns>
        public static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamps a float value between min and max (inclusive).
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum value</param>
        /// <param name="max">The maximum value</param>
        /// <returns>The clamped value</returns>
        public static float Clamp(float value, double min, double max) => (float)(value < min ? min : (value > max ? max : value));
    }
}
