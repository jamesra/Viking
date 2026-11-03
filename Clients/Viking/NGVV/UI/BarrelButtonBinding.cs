using System;

namespace Viking.UI
{
    /// <summary>Which pen report stands for "the barrel button" in Viking.</summary>
    public enum BarrelButtonKind
    {
        /// <summary>Windows' own barrel flag (<see cref="PenFlags.Barrel"/>): the default and what most pens send.</summary>
        Barrel = 0,

        /// <summary>Pens whose side button is reported as the eraser (<see cref="PenFlags.Eraser"/>).</summary>
        Eraser = 1,
    }

    /// <summary>
    /// The pen report Viking treats as the barrel button. Windows already reports a barrel button, so the default
    /// is that flag; the Viking preferences let a user whose pen reports its button differently pick the one theirs
    /// sends. <see cref="PenEventArgs.Barrel"/> and the barrel button events follow the binding, so every command
    /// that uses the barrel button honors the preference without knowing about it.
    /// </summary>
    /// <remarks>
    /// <see cref="Current"/> is process-wide and set at start-up and when preferences change; it is read on the UI thread.
    /// </remarks>
    public static class BarrelButtonBinding
    {
        /// <summary>The pen report currently treated as the barrel button.</summary>
        public static BarrelButtonKind Current { get; set; } = BarrelButtonKind.Barrel;

        /// <summary>True when <paramref name="flags"/> say the bound button is down.</summary>
        public static bool IsDown(PenFlags flags) => IsDown(Current, flags);

        /// <summary>True when <paramref name="flags"/> say the button of <paramref name="kind"/> is down.</summary>
        public static bool IsDown(BarrelButtonKind kind, PenFlags flags) =>
            (flags & FlagFor(kind)) != 0;

        /// <summary>
        /// True when <paramref name="flags"/> say the eraser is active and the eraser is not also the barrel button.
        /// An eraser bound as the barrel button must not pan the view as well.
        /// </summary>
        public static bool IsEraserDown(PenFlags flags) =>
            Current != BarrelButtonKind.Eraser && (flags & PenFlags.Eraser) != 0;

        /// <summary>The Windows pen flag that reports <paramref name="kind"/>.</summary>
        public static PenFlags FlagFor(BarrelButtonKind kind) =>
            kind == BarrelButtonKind.Eraser ? PenFlags.Eraser : PenFlags.Barrel;

        /// <summary>
        /// Reads a stored binding. Anything unknown, including null or empty, is <see cref="BarrelButtonKind.Barrel"/>,
        /// so a damaged setting never leaves the user without a barrel button.
        /// </summary>
        public static BarrelButtonKind Parse(string text) =>
            Enum.TryParse(text, ignoreCase: true, out BarrelButtonKind kind) && Enum.IsDefined(typeof(BarrelButtonKind), kind)
                ? kind
                : BarrelButtonKind.Barrel;

        /// <summary>The stored form of <paramref name="kind"/>; <see cref="Parse"/> reads it back.</summary>
        public static string Format(BarrelButtonKind kind) => kind.ToString();
    }
}
