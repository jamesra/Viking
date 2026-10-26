using Geometry;
using System.Collections.Generic;
using System.Text;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// What a camera move may resubmit. Every served model is a single-resolution tile grid, so a result depends on the prompts
    /// and the fixed tile level, never on the viewport rectangle or zoom: a pan or zoom never cancels or drops a result, and
    /// only a change of prompts (for example the background derived from the structures now in view) asks again.
    /// Pure so it can be tested without a viewer.
    /// </summary>
    internal static class SegmentationCameraPolicy
    {
        /// <summary>
        /// Whether the interactive command should send a new request once the view settles: only when the prompts differ from
        /// the set last sent, for example after the visible structures changed the background points. A null
        /// <paramref name="lastSent"/> means nothing was delivered, so it tries again.
        /// </summary>
        public static bool SettleNeedsResubmit(string? lastSent, string current)
            => lastSent is null || lastSent != current;

        /// <summary>
        /// Stable text for a prompt set. Order matters because the server keys its session on the points.
        /// </summary>
        public static string PromptSignature(IReadOnlyList<Vector2> foreground, IReadOnlyList<Vector2> background)
        {
            StringBuilder text = new();
            Append(text, foreground);
            text.Append('|');
            Append(text, background);
            return text.ToString();
        }

        private static void Append(StringBuilder text, IReadOnlyList<Vector2> points)
        {
            if (points is null)
                return;

            foreach (Vector2 point in points)
            {
                text.Append(point.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                text.Append(',');
                text.Append(point.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                text.Append(';');
            }
        }
    }
}
