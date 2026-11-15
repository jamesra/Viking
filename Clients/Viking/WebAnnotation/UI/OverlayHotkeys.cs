using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.UI;

namespace WebAnnotation
{
    /// <summary>
    /// The chords <see cref="AnnotationOverlay"/> handles in code before it searches the user settings shortcuts, derived from
    /// the overlay's key table (<see cref="AnnotationOverlay.OverlayKeyTable"/>) so the two cannot drift. A chord reserved here
    /// never reaches a <c>Hotkey</c> in <c>UserSettings.xml</c>, so the settings file must not bind it;
    /// <c>OverlayHotkeysTests</c> checks the repo copy against this.
    /// </summary>
    internal static class OverlayHotkeys
    {
        /// <summary>The overlay's key table with handlers that do nothing, for matching chords only.</summary>
        internal static IReadOnlyList<KeyBinding> Table { get; } = AnnotationOverlay.OverlayKeyTable.Create(null);

        /// <summary>
        /// True when the overlay handles <paramref name="keyData"/> (key code plus modifier flags, as in
        /// <see cref="KeyEventArgs.KeyData"/>) itself and does not search the user settings shortcuts.
        /// </summary>
        internal static bool IsReserved(Keys keyData) => ClaimedBy(Table, keyData) is not null;

        /// <summary>
        /// The first binding in <paramref name="table"/> whose key is <paramref name="keyData"/>, whether or not its
        /// <see cref="KeyBinding.When"/> holds right now: a chord a table owns is its own even while the binding is idle.
        /// </summary>
        internal static KeyBinding? ClaimedBy(IEnumerable<KeyBinding> table, Keys keyData) =>
            table.FirstOrDefault(b => (b with { When = null }).Matches(keyData));
    }
}
