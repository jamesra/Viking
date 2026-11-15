#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI
{
    /// <summary>
    /// A key a command or the viewer handles, with its help entry. A class keeps its keys in one <c>KeyBinding[]</c>, dispatches
    /// <c>OnKeyDown</c> through <see cref="KeyBindings.TryDispatch"/> and lists the same table as its help, so a key cannot be
    /// handled without being documented or documented without being handled.
    /// </summary>
    /// <param name="Help">The help entry; its inputs and modifiers are the keys matched.</param>
    /// <param name="Handle">Runs when the key is pressed; returns false to let the key go on as if unbound.</param>
    /// <param name="When">When set, the binding (and its help) applies only while this returns true.</param>
    /// <param name="AnyModifiers">True matches the key with any Shift, Ctrl or Alt held, not only <see cref="HelpEntry.Modifiers"/>.</param>
    public sealed record KeyBinding(HelpEntry Help, Func<KeyEventArgs, bool> Handle, Func<bool>? When = null, bool AnyModifiers = false)
    {
        /// <summary>A binding whose handler always handles the key.</summary>
        public static KeyBinding Do(HelpEntry help, Action<KeyEventArgs> handle, Func<bool>? when = null, bool anyModifiers = false) =>
            new(help, e => { handle(e); return true; }, when, anyModifiers);

        /// <summary>True when this binding applies now and <paramref name="keyData"/> (key code and modifier flags) is its key.</summary>
        public bool Matches(Keys keyData)
        {
            if (When is { } when && !when())
                return false;

            if (!AnyModifiers && HelpInputKeys.ModifiersOf(keyData) != Help.Modifiers)
                return false;

            Keys keyCode = keyData & Keys.KeyCode;
            return Help.Inputs is { } inputs && inputs.Any(input => HelpInputKeys.Matches(input, keyCode, Help.Label));
        }
    }

    public static class KeyBindings
    {
        /// <summary>
        /// Runs the first binding in <paramref name="table"/> that matches the key; true when one handled it. A handled key is
        /// marked handled and its key press suppressed, so the character is not also typed or passed on as unhandled.
        /// </summary>
        public static bool TryDispatch(IEnumerable<KeyBinding> table, KeyEventArgs e)
        {
            foreach (KeyBinding binding in table)
            {
                if (!binding.Matches(e.KeyData))
                    continue;

                if (binding.Handle(e))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The help entries of the bindings that apply now.</summary>
        public static IEnumerable<HelpEntry> Help(IEnumerable<KeyBinding> table) =>
            table.Where(b => b.When is null || b.When()).Select(b => b.Help);

        /// <summary>True when some binding in <paramref name="table"/> would take <paramref name="keyData"/> now.</summary>
        public static bool Handles(IEnumerable<KeyBinding> table, Keys keyData) => table.Any(b => b.Matches(keyData));

        /// <summary>The bindings with their help listed in <paramref name="section"/> of the help window; matching is unchanged.</summary>
        public static KeyBinding[] InSection(this IEnumerable<KeyBinding> table, HelpSection section) =>
            [.. table.Select(b => b with { Help = b.Help.In(section) })];
    }

    /// <summary>How help inputs and modifiers relate to WinForms <see cref="Keys"/>.</summary>
    public static class HelpInputKeys
    {
        /// <summary>The modifiers held in <paramref name="keyData"/>.</summary>
        public static HelpModifiers ModifiersOf(Keys keyData) =>
            (keyData.HasFlag(Keys.Shift) ? HelpModifiers.Shift : HelpModifiers.None) |
            (keyData.HasFlag(Keys.Control) ? HelpModifiers.Ctrl : HelpModifiers.None) |
            (keyData.HasFlag(Keys.Alt) ? HelpModifiers.Alt : HelpModifiers.None);

        /// <summary>
        /// True when pressing <paramref name="keyCode"/> is <paramref name="input"/>. Plus, Minus and the digits match the main
        /// keyboard and the numeric keypad; Arrows matches any arrow key. <see cref="HelpInput.Other"/> matches the key named by
        /// <paramref name="label"/>. Mouse and pen inputs match no key.
        /// </summary>
        public static bool Matches(HelpInput input, Keys keyCode, string? label = null) => input switch
        {
            >= HelpInput.A and <= HelpInput.Z => keyCode == Keys.A + (input - HelpInput.A),
            >= HelpInput.D0 and <= HelpInput.D9 => keyCode == Keys.D0 + (input - HelpInput.D0) || keyCode == Keys.NumPad0 + (input - HelpInput.D0),
            >= HelpInput.F1 and <= HelpInput.F12 => keyCode == Keys.F1 + (input - HelpInput.F1),
            HelpInput.Plus => keyCode is Keys.Oemplus or Keys.Add,
            HelpInput.Minus => keyCode is Keys.OemMinus or Keys.Subtract,
            HelpInput.NumStar => keyCode == Keys.Multiply,
            HelpInput.Arrows => keyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down,
            HelpInput.Backspace => keyCode == Keys.Back,
            HelpInput.Delete => keyCode == Keys.Delete,
            HelpInput.End => keyCode == Keys.End,
            HelpInput.Enter => keyCode == Keys.Return,
            HelpInput.Escape => keyCode == Keys.Escape,
            HelpInput.Home => keyCode == Keys.Home,
            HelpInput.Insert => keyCode == Keys.Insert,
            HelpInput.PageDown => keyCode == Keys.PageDown,
            HelpInput.PageUp => keyCode == Keys.PageUp,
            HelpInput.Space => keyCode == Keys.Space,
            HelpInput.Tab => keyCode == Keys.Tab,
            HelpInput.Other => label is not null && Enum.TryParse(label, true, out Keys named) && named == keyCode,
            _ => false,
        };

        /// <summary>
        /// The help input for a key, for help built from data such as the user settings hotkeys. A key with no
        /// <see cref="HelpInput"/> member is <see cref="HelpInput.Other"/> with the key's name as its label.
        /// </summary>
        public static (HelpInput Input, string? Label) FromKey(Keys keyCode)
        {
            keyCode &= Keys.KeyCode;
            foreach (HelpInput input in Enum.GetValues(typeof(HelpInput)))
            {
                if (input != HelpInput.Other && Matches(input, keyCode))
                    return (input, null);
            }

            return (HelpInput.Other, keyCode.ToString());
        }
    }
}
