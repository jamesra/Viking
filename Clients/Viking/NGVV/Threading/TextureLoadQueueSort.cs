using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Viking.ViewModels;
using VikingXNA;

namespace Viking
{
    /// <summary>
    /// Stable-sort keys shared by <see cref="TextureRequestQueue.SortByPriority"/> and
    /// <see cref="PendingTextureQueue.SortByVisibility"/>.
    /// </summary>
    internal static class TextureLoadQueueSort
    {
        /// <summary>
        /// Visibility rank (0 in bounds, 1 not), absolute section distance, and downsample for one tile.
        /// A null tile is non-visible with zero section distance and zero downsample.
        /// </summary>
        internal static (int visibilityRank, int sectionDistance, int downsample) GetSortKeys(
            TileView? tileView,
            Rectangle visibleBounds,
            int currentSectionZ)
        {
            bool visible = tileView != null && tileView.Bounds.Intersects(visibleBounds);
            int section = tileView?.Section ?? currentSectionZ;
            int downsample = tileView?.Downsample ?? 0;
            return (visible ? 0 : 1, Math.Abs(section - currentSectionZ), downsample);
        }

        /// <summary>
        /// Stable-sort items by visibility in bounds, then |sectionZ - currentZ|, then descending downsample.
        /// </summary>
        internal static List<T> SortToList<T>(
            IEnumerable<T> source,
            Rectangle visibleBounds,
            int currentSectionZ,
            Func<T, TileView?> selectTileView)
        {
            return source
                .Select(item => (item, keys: GetSortKeys(selectTileView(item), visibleBounds, currentSectionZ)))
                .OrderBy(x => x.keys.visibilityRank)
                .ThenBy(x => x.keys.sectionDistance)
                .ThenByDescending(x => x.keys.downsample)
                .Select(x => x.item)
                .ToList();
        }
    }
}
