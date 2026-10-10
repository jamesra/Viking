using System.Globalization;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Reads the structure and location ID text boxes of the merge and split dialogs. The dialogs re-validate on every keystroke,
    /// so a pasted number too large for the type must report "not a number" rather than throw out of the TextChanged handler.
    /// </summary>
    public static class StructureIdText
    {
        /// <summary>
        /// Parses a database ID, which the store keys as <see cref="long"/>. Surrounding white space and a leading sign are accepted,
        /// the same as <see cref="long.Parse(string)"/>; blank, malformed, or out-of-range text returns false.
        /// </summary>
        public static bool TryParse(string? text, out long id) =>
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }
}
