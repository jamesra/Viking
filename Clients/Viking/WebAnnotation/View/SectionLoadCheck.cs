using System;
using System.Globalization;
using System.IO;

namespace WebAnnotation.View
{
    /// <summary>What the delayed check after a section change found for the current section.</summary>
    internal enum SectionLoadCheckOutcome
    {
        /// <summary>Every region cell covering the view has finished its server query. Any miss is a reply that never reached the screen.</summary>
        QueriesComplete,

        /// <summary>The load is still queued or running, so the check leaves it alone.</summary>
        StillLoading,

        /// <summary>No load is queued or running and the view's cells never finished (cancelled, failed, dropped or never queued).</summary>
        NotLoaded,
    }

    /// <summary>
    /// A still view gets no second annotation load after a section change (only camera, transform and reference
    /// changes queue one), so a first load that was cancelled, dropped or answered without a repaint left the section
    /// blank until the next pan or zoom. The overlay runs one check a short time after a section change and uses this
    /// class to name what it found and to write one line per check to <c>%TEMP%\viking-section-load-check.log</c>.
    /// </summary>
    internal static class SectionLoadCheck
    {
        /// <summary>Delay between the section change and the check.</summary>
        public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

        /// <summary>Destination file. Settable so tests can redirect it away from the user's temp folder.</summary>
        internal static string LogPath { get; set; } = Path.Combine(Path.GetTempPath(), "viking-section-load-check.log");

        private static readonly object Gate = new();

        /// <summary>Names the case the check caught.</summary>
        /// <param name="queriesComplete">True when every region cell for the view has completed a server query.</param>
        /// <param name="loadQueuedOrRunning">True when the section is in the overlay's requested or loading set.</param>
        public static SectionLoadCheckOutcome Classify(bool queriesComplete, bool loadQueuedOrRunning)
        {
            if (queriesComplete)
                return SectionLoadCheckOutcome.QueriesComplete;
            return loadQueuedOrRunning ? SectionLoadCheckOutcome.StillLoading : SectionLoadCheckOutcome.NotLoaded;
        }

        /// <summary>True when the check should queue the section again and repaint. A load still in flight is left to finish.</summary>
        public static bool ShouldRequestAgain(SectionLoadCheckOutcome outcome) => outcome != SectionLoadCheckOutcome.StillLoading;

        /// <summary>
        /// Counts consecutive steps in one direction. A step of zero or a jump of more than one section restarts the run at 1;
        /// a step in the same direction as the last one extends it.
        /// </summary>
        /// <param name="previousSection">Section before the change, 0 when unknown.</param>
        /// <param name="newSection">Section after the change.</param>
        /// <param name="previousDirection">Direction of the last step (-1, 0 or +1).</param>
        /// <param name="previousRun">Run length after the last step.</param>
        /// <param name="direction">Direction of this step.</param>
        /// <param name="run">Run length after this step; above 1 means a repeat in the same direction.</param>
        public static void UpdateRun(int previousSection, int newSection, int previousDirection, int previousRun, out int direction, out int run)
        {
            int delta = newSection - previousSection;
            if (previousSection <= 0 || delta == 0 || Math.Abs(delta) > 1)
            {
                direction = 0;
                run = 1;
                return;
            }

            direction = Math.Sign(delta);
            run = direction == previousDirection ? previousRun + 1 : 1;
        }

        /// <summary>One log line: section, zoom (downsample), step direction, run length, the case, and whether it asked again.</summary>
        public static string FormatLine(int section, double downsample, int direction, int run, SectionLoadCheckOutcome outcome, bool requestedAgain) =>
            string.Format(CultureInfo.InvariantCulture,
                "section={0} downsample={1:0.###} step={2} run={3} outcome={4} requestedAgain={5}",
                section, downsample, direction > 0 ? "up" : direction < 0 ? "down" : "jump", run, outcome, requestedAgain);

        /// <summary>Appends a timestamped line to <see cref="LogPath"/>. A failed write is ignored: this is diagnostics only.</summary>
        public static void Append(string line)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
