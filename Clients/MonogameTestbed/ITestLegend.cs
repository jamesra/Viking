using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// A single row in a test's legend: a color swatch and the text explaining what that color (or line) means.
    /// </summary>
    public readonly struct LegendEntry
    {
        public readonly string Text;
        public readonly Color Color;

        /// <summary>
        /// Optional line style this entry refers to. Used only to append a short note to the text; the swatch
        /// itself is always a solid color rectangle.
        /// </summary>
        public readonly LineStyle? Style;

        public LegendEntry(string text, Color color, LineStyle? style = null)
        {
            Text = text;
            Color = color;
            Style = style;
        }
    }

    /// <summary>
    /// Optional contract a test can implement to display an on-screen legend and description HUD.
    /// Tests opt in by implementing this interface; tests that do not implement it render unchanged.
    /// </summary>
    interface ITestLegend
    {
        /// <summary>
        /// Static description of what the current test mode demonstrates.
        /// </summary>
        string ModeDescription { get; }

        /// <summary>
        /// Live description of the currently enabled sub-views. May change every frame.
        /// </summary>
        string ActiveViewDescription { get; }

        /// <summary>
        /// Color/text rows explaining the meaning of the lines and vertices drawn by the test.
        /// </summary>
        IReadOnlyList<LegendEntry> LegendEntries { get; }
    }
}
