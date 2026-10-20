using System.Globalization;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Reads the ID text box of <see cref="GoToActionForm"/> (Go To Location and Go To Structure). The form parses the box on
    /// every keystroke, so text too large for a <see cref="long"/> must report "not an ID" rather than throw out of the
    /// TextChanged handler. Kept apart from the window so tests can call it without referencing WPF.
    /// </summary>
    internal static class GoToActionIDText
    {
        /// <summary>
        /// Parses an ID. Surrounding white space and a leading sign are accepted, as <c>Convert.ToInt64</c> did;
        /// blank, malformed, or out-of-range text (beyond <see cref="long.MinValue"/> to <see cref="long.MaxValue"/>,
        /// such as 20 digits) returns false and sets <paramref name="id"/> to 0. The text box blocks typed non-digits,
        /// but not pasted text or a long run of typed digits.
        /// </summary>
        internal static bool TryParse(string text, out long id) =>
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }
}
