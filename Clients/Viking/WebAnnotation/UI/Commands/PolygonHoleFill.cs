using System;
using Geometry;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Resolves which interior ring a fill-bucket click should remove.
    /// Hover tests the smoothed volume hole; the saved shape is the unsmoothed mosaic polygon.
    /// Matching only the mosaic click point silently no-ops when those spaces disagree.
    /// </summary>
    internal static class PolygonHoleFill
    {
        /// <summary>
        /// Remove the interior ring that contains the click, using mosaic first, then
        /// unsmoothed volume, then the same smoothing the hole cursor uses.
        /// Called by <see cref="RemovePolygonHoleCommand"/> after a REMOVEHOLE click.
        /// </summary>
        /// <returns>True when a ring was removed from <paramref name="mosaicPolygon"/>.</returns>
        public static bool TryRemoveHoleAtClick(
            Polygon mosaicPolygon,
            Vector2 mosaicClick,
            Polygon? volumePolygon,
            Vector2 volumeClick,
            uint smoothInterpolations)
        {
            if (mosaicPolygon is null)
                throw new ArgumentNullException(nameof(mosaicPolygon));

            if (mosaicPolygon.TryRemoveInteriorRing(mosaicClick))
                return true;

            int index = IndexOfHoleContaining(volumePolygon, volumeClick);
            if (index < 0 && volumePolygon is not null && smoothInterpolations > 0)
            {
                try
                {
                    index = IndexOfHoleContaining(volumePolygon.Smooth(smoothInterpolations), volumeClick);
                }
                catch (ArgumentException)
                {
                    // Same fallback as LocationPolygonView.Initialize when Smooth fails.
                }
            }

            return index >= 0 && mosaicPolygon.TryRemoveInteriorRing(index);
        }

        /// <summary>
        /// Index of the first interior ring that covers <paramref name="click"/>, or -1.
        /// </summary>
        public static int IndexOfHoleContaining(Polygon? polygon, Vector2 click)
        {
            if (polygon is null)
                return -1;

            for (int i = 0; i < polygon.InteriorPolygons.Count; i++)
            {
                if (polygon.InteriorPolygons[i].Covers(click))
                    return i;
            }

            return -1;
        }
    }
}
