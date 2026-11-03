#nullable enable

using System;
using Geometry;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>What a paintbrush stroke does to the <see cref="PaintCanvas"/>.</summary>
    internal enum PaintMode
    {
        /// <summary>Marks pixels as the object.</summary>
        Foreground,

        /// <summary>Marks pixels as not the object.</summary>
        Background,

        /// <summary>Clears pixels marked as the object, leaving background paint alone.</summary>
        EraseForeground,

        /// <summary>Clears pixels marked as not the object, leaving foreground paint alone.</summary>
        EraseBackground,

        /// <summary>Clears both foreground and background paint under the brush.</summary>
        EraseAll,
    }

    /// <summary>
    /// The rules of the paintbrush segmentation command that do not depend on the screen: which mode a stroke starts in, how the brush
    /// grows and shrinks, and the canvas value a mode paints or erases. Kept apart from the command so they can be tested.
    /// </summary>
    internal static class PaintBrush
    {
        /// <summary>The smallest brush radius in screen pixels.</summary>
        public const double MinRadiusPixels = 2;

        /// <summary>The largest brush radius in screen pixels.</summary>
        public const double MaxRadiusPixels = 400;

        /// <summary>The factor one wheel notch changes the radius by.</summary>
        public const double NotchFactor = 1.15;

        /// <summary>
        /// The mode a stroke starts in. Without the alternate input (Shift, or the pen's barrel button) a stroke paints foreground. With it,
        /// a stroke started where nothing is painted under the brush paints background; started over paint it erases both foreground and
        /// background paint (<see cref="PaintMode.EraseAll"/>).
        /// </summary>
        /// <remarks>
        /// Choosing which type of paint to erase (the nearer type to the centre, or on a tie the type with more pixels under the brush) is
        /// commented out for now, so an erase clears both types. Restore that block to erase one type again.
        /// </remarks>
        /// <param name="canvas">The paint so far.</param>
        /// <param name="center">Brush centre, volume space.</param>
        /// <param name="radius">Brush radius, volume units.</param>
        /// <param name="alternate">Shift or the barrel button is down.</param>
        public static PaintMode ChooseMode(PaintCanvas canvas, Vector2 center, double radius, bool alternate)
        {
            if (!alternate)
                return PaintMode.Foreground;

            var (foreground, background, nearestForeground, nearestBackground) = canvas.Survey(center, radius);
            if (foreground == 0 && background == 0)
                return PaintMode.Background;

            // Temporarily disabled: erase both types of paint instead of the type nearest the centre.
            //if (nearestForeground < nearestBackground)
            //    return PaintMode.EraseForeground;
            //
            //if (nearestBackground < nearestForeground)
            //    return PaintMode.EraseBackground;
            //
            //return foreground >= background ? PaintMode.EraseForeground : PaintMode.EraseBackground;
            _ = (nearestForeground, nearestBackground);
            return PaintMode.EraseAll;
        }

        /// <summary>True for the modes that clear paint.</summary>
        public static bool IsErase(PaintMode mode) => mode is PaintMode.EraseForeground or PaintMode.EraseBackground or PaintMode.EraseAll;

        /// <summary>
        /// The canvas value <paramref name="mode"/> paints, or for a single-type erase mode the value it clears.
        /// <see cref="PaintMode.EraseAll"/> paints <see cref="PaintCanvas.None"/>.
        /// </summary>
        public static byte ValueOf(PaintMode mode) => mode switch
        {
            PaintMode.Foreground or PaintMode.EraseForeground => PaintCanvas.Foreground,
            PaintMode.EraseAll => PaintCanvas.None,
            _ => PaintCanvas.Background,
        };

        /// <summary>Applies one stroke segment of <paramref name="mode"/> from <paramref name="from"/> to <paramref name="to"/>; returns the pixels changed.</summary>
        public static int Apply(PaintCanvas canvas, PaintMode mode, Vector2 from, Vector2 to, double radius) =>
            mode == PaintMode.EraseAll
                // Painting None over every pixel in the disc clears both foreground and background.
                ? canvas.PaintStroke(from, to, radius, PaintCanvas.None, erase: false)
                : canvas.PaintStroke(from, to, radius, ValueOf(mode), IsErase(mode));

        /// <summary>
        /// The brush radius after <paramref name="notches"/> wheel notches (positive grows), kept within
        /// <see cref="MinRadiusPixels"/> and <see cref="MaxRadiusPixels"/>.
        /// </summary>
        public static double ResizeRadius(double radiusPixels, double notches) =>
            Math.Max(MinRadiusPixels, Math.Min(MaxRadiusPixels, radiusPixels * Math.Pow(NotchFactor, notches)));
    }
}
