using Geometry;
using System.Collections.Generic;
using System.Text;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// What a camera move is allowed to cancel, drop, or resubmit, decided from the model's
    /// <see cref="SegmentationModelProfile"/>. Pure so it can be tested without a viewer.
    /// A fixed-tile result depends on the submitted tile level and the prompts, never on the viewport
    /// rectangle. A full-viewport result is the screen, so it depends on the rectangle and zoom too.
    /// </summary>
    internal static class SegmentationCameraPolicy
    {
        /// <summary>
        /// What auto-polygonize does when the camera moves. Finished results and the circle
        /// completion cache are only touched when <see cref="DropFinishedResults"/> is set.
        /// </summary>
        internal readonly struct CameraMoveResponse
        {
            public CameraMoveResponse(bool cancelInFlightSegmentation, bool dropFinishedResults, bool reorderQueue)
            {
                CancelInFlightSegmentation = cancelInFlightSegmentation;
                DropFinishedResults = dropFinishedResults;
                ReorderQueue = reorderQueue;
            }

            /// <summary>Abort the SegmentTiles call that is running and its response work.</summary>
            public bool CancelInFlightSegmentation { get; }

            /// <summary>Remove on-screen proposals and forget completions made at another tile level.</summary>
            public bool DropFinishedResults { get; }

            /// <summary>
            /// Stop starting circles from the old ordering once the current one returns,
            /// so the next idle batch orders the remaining circles from the new view center.
            /// </summary>
            public bool ReorderQueue { get; }
        }

        /// <summary>
        /// True when the submitted level differs from the last one. Never true unless the profile lets the
        /// level change: otherwise a differing pair is a stale reading, not a zoom.
        /// </summary>
        public static bool TileLevelChanged(SegmentationModelProfile profile, int previousLevel, int liveLevel)
            => profile.TileLevelCanChange &&
               previousLevel != 0 &&
               liveLevel != previousLevel;

        /// <summary>
        /// Auto-polygonize response to a pan or zoom. The in-flight call and finished results are kept
        /// unless the camera move made them stale for this profile: any moved view for a full viewport,
        /// a changed tile level for multi-resolution tiles, never for single-resolution tiles.
        /// The queue always reorders so the next batch starts from the new view.
        /// </summary>
        /// <param name="profile">How the model wants its input.</param>
        /// <param name="viewportMoved">The view rectangle moved by more than the similarity tolerance.</param>
        /// <param name="tileLevelChanged">See <see cref="TileLevelChanged"/>.</param>
        public static CameraMoveResponse OnCameraMoved(
            SegmentationModelProfile profile, bool viewportMoved, bool tileLevelChanged)
        {
            bool stale = profile.ResultDependsOnViewport
                ? viewportMoved
                : profile.TileLevelCanChange && tileLevelChanged;
            return new CameraMoveResponse(
                cancelInFlightSegmentation: stale,
                dropFinishedResults: stale,
                reorderQueue: true);
        }

        /// <summary>
        /// True when a response computed at <paramref name="submittedLevel"/> may still be published.
        /// A full viewport needs the view unchanged; multi-resolution tiles need the same level;
        /// single-resolution tiles have one level and always qualify.
        /// </summary>
        /// <param name="profile">How the model wants its input.</param>
        /// <param name="submittedLevel">Tile level the response was computed at.</param>
        /// <param name="liveLevel">Tile level the camera resolves to now. Only read for multi-resolution tiles.</param>
        /// <param name="viewportUnchanged">The view still matches the submitted one. Only read for a full viewport.</param>
        public static bool IsResultStillValid(
            SegmentationModelProfile profile, int submittedLevel, int liveLevel, bool viewportUnchanged)
        {
            if (profile.ResultDependsOnViewport)
                return viewportUnchanged;

            return !profile.TileLevelCanChange || submittedLevel == liveLevel;
        }

        /// <summary>
        /// Whether the interactive command should cancel its request and drop its image hold when the
        /// view moves. Only when the move can make its result wrong.
        /// </summary>
        public static bool ViewMoveInvalidatesRequest(SegmentationModelProfile profile)
            => profile.CameraMoveCanStaleResults;

        /// <summary>
        /// Whether the interactive command should send a new request once the view settles.
        /// Models whose results a move can stale always do. Single-resolution tiles do only when the
        /// prompts differ from the set last sent, for example after the visible structures changed the
        /// background points. A null <paramref name="lastSent"/> means nothing was delivered, so it tries again.
        /// </summary>
        public static bool SettleNeedsResubmit(SegmentationModelProfile profile, string? lastSent, string current)
            => profile.CameraMoveCanStaleResults || lastSent is null || lastSent != current;

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
