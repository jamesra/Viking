using System.Collections.Generic;
using System.Windows.Forms;

namespace WebAnnotation
{
    /// <summary>
    /// The keys <see cref="AnnotationOverlay"/> handles in code before it searches the user settings
    /// shortcuts. A chord listed here never reaches a <c>Hotkey</c> in <c>UserSettings.xml</c>, so the
    /// settings file must not bind it; <c>OverlayHotkeysTests</c> checks the repo copy against this list.
    /// </summary>
    internal static class OverlayHotkeys
    {
        /// <summary>Shift+F8: send every circle in view to auto-polygonize again. Plain F8 is the Done tag shortcut.</summary>
        internal const Keys ResubmitAutoPolygonize = Keys.Shift | Keys.F8;

        /// <summary>Ctrl+Z: undo the last recorded annotation edit.</summary>
        internal const Keys Undo = Keys.Control | Keys.Z;

        /// <summary>Shift+8 on the main keyboard: toggle Pen Mode (the numpad * is in <see cref="AnyModifierKeys"/>).</summary>
        internal const Keys PenModeMainKeyboard = Keys.Shift | Keys.D8;

        /// <summary>Keys handled with any combination of Shift, Ctrl, and Alt. Read-only by convention.</summary>
        internal static readonly HashSet<Keys> AnyModifierKeys = new()
        {
            Keys.Tab,      // start a new structure segmentation
            Keys.F3,       // continue the last trace
            Keys.F5,       // reload annotations
            Keys.F6,       // favorite structure types
            Keys.F7,       // structure type management
            Keys.Enter,    // continue the last trace
            Keys.Back,     // go to the last edited location
            Keys.Multiply, // toggle Pen Mode (numpad *)
        };

        /// <summary>Chords handled only with exactly these modifiers. Read-only by convention.</summary>
        internal static readonly HashSet<Keys> ExactChords = new()
        {
            ResubmitAutoPolygonize,
            Undo,
            PenModeMainKeyboard,
        };

        /// <summary>
        /// True when the overlay handles <paramref name="keyData"/> (key code plus modifier flags, as in
        /// <see cref="KeyEventArgs.KeyData"/>) itself and does not search the user settings shortcuts.
        /// </summary>
        internal static bool IsReserved(Keys keyData) =>
            AnyModifierKeys.Contains(keyData & Keys.KeyCode) || ExactChords.Contains(keyData);
    }
}
