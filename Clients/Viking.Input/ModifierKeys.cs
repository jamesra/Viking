using System;

namespace Viking.Input
{
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4
    }

    public static class ModifierKeysExtensions
    {
        public static bool ShiftPressed(this ModifierKeys modifierKeys) =>
            (modifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift;

        public static bool CtrlPressed(this ModifierKeys modifierKeys) =>
            (modifierKeys & ModifierKeys.Control) == ModifierKeys.Control;

        public static bool AltPressed(this ModifierKeys modifierKeys) =>
            (modifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt;

        public static bool ShiftOrCtrlPressed(this ModifierKeys modifierKeys) =>
            modifierKeys.ShiftPressed() || modifierKeys.CtrlPressed();
    }
}
