#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Common
{
    /// <summary>
    /// Modifier keys held with a help entry's input. The numeric value is the sort order among entries for the same input:
    /// none, Shift, Ctrl, Ctrl+Shift, Alt, and so on.
    /// </summary>
    [Flags]
    public enum HelpModifiers
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Alt = 4,
    }

    /// <summary>
    /// A key, mouse action or pen action shown in the help. The declaration order is the order the help is sorted in:
    /// letters, digits, symbols, function keys, named keys, then mouse and pen. Mouse members are drawn as mouse icons.
    /// </summary>
    public enum HelpInput
    {
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        Plus, Minus, NumStar,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        Arrows, Backspace, Delete, End, Enter, Escape, Home, Insert, PageDown, PageUp, Space, Tab,

        /// <summary>A key with no member here (a user's hotkey on, say, OemPeriod); <see cref="HelpEntry.Label"/> names it.</summary>
        Other,

        LeftClick, LeftDrag, LeftDoubleClick, MiddleClick, RightClick, RightDrag, RightDoubleClick,
        Wheel, TiltWheel, BackForwardButtons,
        Pen, PenContact,
    }

    /// <summary>
    /// Where the help window (F1) lists an entry. <see cref="Command"/> is 0, so an entry its source does not tag is the
    /// command's; the window shows the sections in its own order (<c>HelpWindow.SectionOrder</c>).
    /// </summary>
    public enum HelpSection
    {
        /// <summary>What the active command (or the idle viewer) does. The default.</summary>
        Command = 0,

        /// <summary>What can be done with the annotation under the mouse while no other command runs.</summary>
        MouseOver,

        /// <summary>The annotation hotkeys from the user settings file; they work while no other command runs.</summary>
        AnnotationShortcuts,

        /// <summary>Keys and mouse actions that work whatever command runs: the viewer's, the annotation overlay's and every command's defaults.</summary>
        AlwaysActive,

        /// <summary>What the items on the viewer's status bar show and what clicking them does. Listed only in the help window.</summary>
        StatusBar,
    }

    /// <summary>What kind of picture an input is drawn as.</summary>
    public enum HelpInputKind
    {
        Key,
        Mouse,
        Pen,
    }

    /// <summary>
    /// One line of command help: the inputs that do something and what they do. An entry with no inputs is plain text
    /// (an instruction, a step, a caption), shown before the keyed entries in the order it was given.
    /// </summary>
    /// <param name="Description">What the input does, or the whole line for a plain-text entry.</param>
    /// <param name="Modifiers">Modifier keys held with the input.</param>
    /// <param name="Inputs">Alternatives, any of which does the same thing (drawn with "/" between them), e.g. Plus and Minus,
    /// or F3 and Enter. Null or empty for a plain-text entry.</param>
    /// <param name="Where">Plain text drawn after the inputs, such as "on a control point".</param>
    /// <param name="Label">The keycap text of <see cref="HelpInput.Other"/>.</param>
    public readonly record struct HelpEntry(
        string Description,
        HelpModifiers Modifiers = HelpModifiers.None,
        HelpInput[]? Inputs = null,
        string? Where = null,
        string? Label = null)
    {
        /// <summary>
        /// True when the entry has inputs or modifiers; false for plain text and the divider. An entry with modifiers and no
        /// inputs is a modifier held during another action ("hold Shift to measure horizontally").
        /// </summary>
        public bool IsKeyed => Inputs is { Length: > 0 } || Modifiers != HelpModifiers.None;

        /// <summary>True for the line <see cref="Arrange"/> puts between the plain-text and the keyed entries.</summary>
        public bool IsDivider { get; private init; }

        /// <summary>The divider between plain-text and keyed entries.</summary>
        public static HelpEntry Divider { get; } = new("") { IsDivider = true };

        /// <summary>The help window section the entry is listed in; <see cref="HelpSection.Command"/> unless its source says otherwise.</summary>
        public HelpSection Section { get; init; }

        /// <summary>This entry, listed in <paramref name="section"/> of the help window.</summary>
        public HelpEntry In(HelpSection section) => this with { Section = section };

        /// <summary>A plain-text entry.</summary>
        public static HelpEntry Text(string line) => new(line);

        /// <summary>An entry for one input.</summary>
        public static HelpEntry For(HelpInput input, string description, HelpModifiers modifiers = HelpModifiers.None, string? where = null) =>
            new(description, modifiers, [input], where);

        /// <summary>
        /// The entries as the help shows them: plain-text entries in the order given, then <see cref="Divider"/> when there are
        /// both kinds, then the keyed entries sorted by their first input (modifier-only entries first), then modifiers, then
        /// <see cref="Where"/>, keeping the given order among equals. Repeated entries are shown once.
        /// </summary>
        public static IReadOnlyList<HelpEntry> Arrange(IEnumerable<HelpEntry> entries)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            List<HelpEntry> text = [];
            List<HelpEntry> keyed = [];
            foreach (HelpEntry entry in entries)
            {
                if (entry.IsDivider || !seen.Add(entry.ToString()))
                    continue;

                (entry.IsKeyed ? keyed : text).Add(entry);
            }

            // OrderBy is stable, so equal entries keep the order they were given in.
            List<HelpEntry> result = [.. text];
            if (text.Count > 0 && keyed.Count > 0)
                result.Add(Divider);

            result.AddRange(keyed
                .OrderBy(e => e.Inputs is { Length: > 0 } inputs ? (int)inputs[0] : -1)
                .ThenBy(e => e.Inputs is { Length: > 0 } inputs && inputs[0] == HelpInput.Other ? e.Label ?? "" : "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => (int)e.Modifiers)
                .ThenBy(e => e.Where ?? "", StringComparer.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>The picture an input is drawn as.</summary>
        public static HelpInputKind KindOf(HelpInput input) => input switch
        {
            >= HelpInput.LeftClick and <= HelpInput.BackForwardButtons => HelpInputKind.Mouse,
            HelpInput.Pen or HelpInput.PenContact => HelpInputKind.Pen,
            _ => HelpInputKind.Key,
        };

        /// <summary>The keycap text of a key, or the words for a mouse or pen action (also drawn after its icon).</summary>
        public static string LabelFor(HelpInput input, string? label = null) => input switch
        {
            >= HelpInput.D0 and <= HelpInput.D9 => ((int)(input - HelpInput.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            HelpInput.Plus => "+",
            HelpInput.Minus => "−",
            HelpInput.NumStar => "Num *",
            HelpInput.Arrows => "← ↑ → ↓",
            HelpInput.Delete => "Del",
            HelpInput.Escape => "Esc",
            HelpInput.Insert => "Ins",
            HelpInput.PageDown => "PgDn",
            HelpInput.PageUp => "PgUp",
            HelpInput.Other => label ?? "?",
            HelpInput.LeftClick => "click",
            HelpInput.LeftDrag => "drag",
            HelpInput.LeftDoubleClick => "double-click",
            HelpInput.MiddleClick => "middle click",
            HelpInput.RightClick => "right click",
            HelpInput.RightDrag => "right drag",
            HelpInput.RightDoubleClick => "right double-click",
            HelpInput.Wheel => "wheel",
            HelpInput.TiltWheel => "tilt wheel",
            HelpInput.BackForwardButtons => "back/forward buttons",
            HelpInput.Pen => "Pen",
            HelpInput.PenContact => "Pen contact",
            _ => input.ToString(),
        };

        /// <summary>The keycap text of a modifier.</summary>
        public static string LabelFor(HelpModifiers modifier) => modifier switch
        {
            HelpModifiers.Shift => "⇧ Shift",
            HelpModifiers.Ctrl => "Ctrl",
            HelpModifiers.Alt => "Alt",
            _ => modifier.ToString(),
        };

        /// <summary>The modifiers held, each on its own keycap, in the order Ctrl, Shift, Alt.</summary>
        public IEnumerable<HelpModifiers> ModifierKeys()
        {
            if (Modifiers.HasFlag(HelpModifiers.Ctrl))
                yield return HelpModifiers.Ctrl;
            if (Modifiers.HasFlag(HelpModifiers.Shift))
                yield return HelpModifiers.Shift;
            if (Modifiers.HasFlag(HelpModifiers.Alt))
                yield return HelpModifiers.Alt;
        }

        /// <summary>The entry as one line of text, e.g. "Ctrl+⇧ Shift+Z: Undo", for traces, tests and screen readers.</summary>
        public override string ToString()
        {
            if (IsDivider)
                return "—";

            if (!IsKeyed)
                return Description;

            string label = Label ?? "";
            IEnumerable<string> keys = ModifierKeys().Select(m => LabelFor(m));
            if (Inputs is { Length: > 0 } inputs)
                keys = keys.Concat([string.Join("/", inputs.Select(i => LabelFor(i, label)))]);

            string chord = string.Join("+", keys);
            return string.IsNullOrEmpty(Where) ? $"{chord}: {Description}" : $"{chord} {Where}: {Description}";
        }
    }

    /// <summary>Command help for the help bar.</summary>
    public interface IHelp
    {
        HelpEntry[] Help { get; }
    }

    /// <summary>Command help that changes while the command runs (the help bar follows the collection).</summary>
    public interface IObservableHelp
    {
        System.Collections.ObjectModel.ObservableCollection<HelpEntry> ObservableHelp { get; }
    }
}
