using System;
using System.Globalization;

namespace Viking.UI.WPF.Controls
{
    /// <summary>
    /// Text rules behind <see cref="SliderWithValueBox"/>: how a value is shown in the box and how text the user typed
    /// becomes a value. Kept free of WPF types so <c>VikingCoreTests</c> can pin it without a dispatcher.
    /// </summary>
    /// <remarks>
    /// Values are shown with <see cref="CultureInfo.InvariantCulture"/>, matching what the preference dialogs showed
    /// through WPF <c>StringFormat</c> (WPF formats with en-US unless a window sets <c>Language</c>). Typed text is read
    /// with the invariant culture first and then the current culture, so "0.5" works everywhere and "0,5" works where the
    /// comma is the decimal separator. Thousands separators are not accepted, which keeps the two readings from
    /// disagreeing ("1.5" can never be read as fifteen).
    /// </remarks>
    public static class SliderValueText
    {
        private const NumberStyles TypedNumberStyles = NumberStyles.Float;

        /// <summary>
        /// Shows <paramref name="value"/> with <paramref name="format"/> (a .NET numeric format such as "F2", "P0" or
        /// "0.##"); a null or empty format uses "G".
        /// </summary>
        public static string Format(double value, string format)
        {
            return value.ToString(string.IsNullOrEmpty(format) ? "G" : format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// True when <paramref name="format"/> shows the value as a percentage ("P", "P0", or a custom format with
        /// "%"), so the box shows 0.05 as "5 %" and typed text is read in percent.
        /// </summary>
        public static bool IsPercentFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            char first = format[0];
            if ((first == 'P' || first == 'p') && (format.Length == 1 || IsAllDigits(format, 1)))
                return true;

            return format.IndexOf('%') >= 0;
        }

        /// <summary>
        /// Reads typed <paramref name="text"/> as a value in the units <paramref name="format"/> shows: for a percent
        /// format "5", "5%" and "5 %" all read as 0.05. Returns false for empty or unparsable text and for NaN or
        /// infinity, which the caller treats as "revert to the current value".
        /// </summary>
        public static bool TryParse(string text, string format, out double value)
        {
            value = 0;
            if (text is null)
                return false;

            bool percent = IsPercentFormat(format);
            string trimmed = text.Trim();
            if (percent)
                trimmed = trimmed.Replace("%", string.Empty).Trim();

            if (trimmed.Length == 0)
                return false;

            if (!double.TryParse(trimmed, TypedNumberStyles, CultureInfo.InvariantCulture, out double parsed) &&
                !double.TryParse(trimmed, TypedNumberStyles, CultureInfo.CurrentCulture, out parsed))
                return false;

            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
                return false;

            value = percent ? parsed / 100.0 : parsed;
            return true;
        }

        /// <summary>
        /// Limits <paramref name="value"/> to [<paramref name="minimum"/>, <paramref name="maximum"/>]. When the range is
        /// inverted (a bound range not yet loaded) the minimum wins, as WPF's <c>RangeBase</c> coerces.
        /// </summary>
        public static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                maximum = minimum;

            if (value < minimum)
                return minimum;

            return value > maximum ? maximum : value;
        }

        /// <summary>
        /// The value a commit (Enter or leaving the box) stores for typed <paramref name="text"/>: the parsed value
        /// clamped to the range, or <paramref name="currentValue"/> unchanged when the text is not a number. Typed values
        /// are not snapped to the slider's ticks or rounded to the shown decimals, so any in-range number can be set.
        /// </summary>
        public static double ResolveTypedValue(string text, double currentValue, double minimum, double maximum, string format)
        {
            if (!TryParse(text, format, out double typed))
                return currentValue;

            return Clamp(typed, minimum, maximum);
        }

        private static bool IsAllDigits(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                    return false;
            }

            return true;
        }
    }
}
