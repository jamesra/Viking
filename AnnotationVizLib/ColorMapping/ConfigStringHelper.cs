using System;

namespace AnnotationVizLib
{
    class ConfigStringHelper
    {
        /// <summary>
        /// Strip whitespace and ensure the line starts with a number
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool StartsWithNumber(string str)
        {
            if (str.Length == 0)
                return false;

            return char.IsDigit(str.Trim()[0]);
        }

        /// <summary>
        /// We use the % to indicate comments
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool StartsWithComment(string str)
        {
            if (str.Length == 0)
                return false;

            string trimmed = str.TrimStart();
            if (trimmed.Length == 0)
                return false;

            return trimmed[0] == '%';
        }

        /// <summary>
        /// Convert a string with a floating point number from 0 to 1 into a 0-255 value for building Colors
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int NormalizedStringToByte(string str) => System.Convert.ToInt32(Math.Floor(NormalizedStringToScalar(str) * 255.0));

        /// <summary>
        /// Parse a floating point number from 0 to 1 ("1", "0.5"), written with a '.' whatever the machine's culture,
        /// as a multiplier for a colour channel.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static double NormalizedStringToScalar(string str)
        {
            double val = double.Parse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            if (val < 0.0 || val > 1.0)
            {
                throw new ArgumentException("String value must fall between 0 and 1.");
            }

            return val;
        }

    }
}