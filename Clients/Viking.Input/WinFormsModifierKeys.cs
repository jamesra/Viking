namespace Viking.Input
{
    /// <summary>
    /// Maps toolkit modifier bits onto <see cref="ModifierKeys"/> at the overlay edge.
    /// Integer overloads avoid a WinForms/WPF reference from this library.
    /// </summary>
    public static class ModifierKeysConverter
    {
        /// <summary>
        /// Maps <c>System.Windows.Forms.Keys</c> bits: Shift=0x10000, Control=0x20000, Alt=0x40000.
        /// </summary>
        public static ModifierKeys FromWinFormsKeys(int keysValue)
        {
            ModifierKeys result = ModifierKeys.None;
            if ((keysValue & 0x10000) != 0)
                result |= ModifierKeys.Shift;
            if ((keysValue & 0x20000) != 0)
                result |= ModifierKeys.Control;
            if ((keysValue & 0x40000) != 0)
                result |= ModifierKeys.Alt;
            return result;
        }

        /// <summary>
        /// Maps <c>System.Windows.Input.ModifierKeys</c> bits: Alt=1, Control=2, Shift=4.
        /// </summary>
        public static ModifierKeys FromWpfModifierKeys(int wpfModifierKeys)
        {
            ModifierKeys result = ModifierKeys.None;
            if ((wpfModifierKeys & 4) != 0)
                result |= ModifierKeys.Shift;
            if ((wpfModifierKeys & 2) != 0)
                result |= ModifierKeys.Control;
            if ((wpfModifierKeys & 1) != 0)
                result |= ModifierKeys.Alt;
            return result;
        }
    }
}
