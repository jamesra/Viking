using System.Globalization;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Reads and writes the structure and location ID text boxes of the merge and split dialogs. The dialogs re-validate on every keystroke,
    /// so a pasted number too large for the type must report "not a number" rather than throw out of the TextChanged handler.
    /// It also reads the persisted favorite structure type IDs, where a hand-edited or damaged user settings file must not throw.
    /// </summary>
    public static class StructureIdText
    {
        /// <summary>
        /// Parses a database ID, which the store keys as <see cref="long"/>. Surrounding white space and a leading sign are accepted,
        /// the same as <see cref="long.Parse(string)"/>; blank, malformed, or out-of-range text returns false.
        /// </summary>
        public static bool TryParse(string? text, out long id) =>
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

        /// <summary>
        /// Text for an ID text box: the ID for a database ID (greater than 0), or empty for 0 or a negative "no ID" value such as
        /// the -1 the split dialog's getters return for an empty box, so the dialog's Flip button can swap an empty box.
        /// </summary>
        public static string ToText(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : "";

        /// <summary>
        /// Parses a persisted structure type ID, which the favorites list keeps as <see cref="ulong"/>. Surrounding white space is
        /// accepted; blank, malformed, negative, or out-of-range text returns false. Values above <see cref="long.MaxValue"/> are
        /// rejected too: the store keys IDs as <see cref="long"/>, and the annotation menu converts each favorite with
        /// <c>Convert.ToInt64</c>, which throws on a larger value. Called by <c>Global.UserFavoriteStructureTypes</c>
        /// for every stored entry so one bad entry is skipped instead of throwing out of the getter or the menu.
        /// </summary>
        public static bool TryParseUnsigned(string? text, out ulong id)
        {
            if (ulong.TryParse(text, NumberStyles.None | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out id)
                && id <= long.MaxValue)
                return true;

            id = 0;
            return false;
        }
    }
}
