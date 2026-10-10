using System;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Pure layout helpers for Review change cards. Used by <see cref="ReviewChangeFeedView"/>
    /// when the ACTION pane width changes (tab/image divider).
    /// </summary>
    public static class ReviewChangeCardLayout
    {
        /// <summary>
        /// Maps list width to a square thumb size: subtract item margin, card padding, and vertical
        /// scrollbar reserve so the image fits without horizontal clipping.
        /// </summary>
        public static double ComputeCardThumbSize(double listWidth, double verticalScrollBarWidth)
        {
            // Item Margin L+R (8) + Border Padding L+R (8).
            const double horizontalChrome = 16;
            const double minThumb = 48;
            if (listWidth <= 0 || double.IsNaN(listWidth))
                return minThumb;

            double scroll = verticalScrollBarWidth > 0 ? verticalScrollBarWidth : 0;
            return Math.Max(minThumb, listWidth - scroll - horizontalChrome);
        }
    }
}
