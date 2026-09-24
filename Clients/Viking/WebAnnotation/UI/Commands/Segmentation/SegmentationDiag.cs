using System;
using System.IO;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Append-only file log for tiled segmentation bring-up. Writes to %TEMP%\viking-seg-diag.log.
    /// </summary>
    internal static class SegmentationDiag
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "viking-seg-diag.log");
        private static readonly object Gate = new();

        /// <summary>Append one timestamped line. Failures are ignored so diagnostics never break the path.</summary>
        public static void Log(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
                lock (Gate)
                    File.AppendAllText(LogPath, line);
            }
            catch
            {
            }
        }
    }
}
