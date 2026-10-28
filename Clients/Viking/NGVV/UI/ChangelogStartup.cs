#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms.Integration;
using Viking.UI.Forms;
using Viking.UI.WPF.Forms;

namespace Viking.UI
{
    /// <summary>
    /// The user changelog (<c>VikingChangelog.md</c>), installed beside Viking.exe: shown from Help > Change Log, and once the
    /// first time each version starts.
    /// </summary>
    public static class ChangelogStartup
    {
        /// <summary>The changelog installed beside Viking.exe.</summary>
        public static string ChangelogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VikingChangelog.md");

        /// <summary>Shows the changelog. Called from Help > Change Log.</summary>
        public static void Show() => ChangelogWindow.Show(ChangelogPath, "Viking Change Log", beforeShow: ElementHost.EnableModelessKeyboardInterop);

        /// <summary>
        /// Shows the changelog when this version of Viking starts for the first time. Each version keeps its own marker in its
        /// local application data folder, so going back to an older version does not show it again. Does nothing when the
        /// changelog is not installed. Called once the main window is shown, on the UI thread.
        /// </summary>
        public static void ShowIfFirstRunOfVersion()
        {
            if (!File.Exists(ChangelogPath))
                return;

            string marker;
            try
            {
                marker = Path.Combine(System.Windows.Forms.Application.LocalUserAppDataPath, "ChangelogShown");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"Change log first-run check skipped: {e.Message}");
                return;
            }

            if (!TryMarkFirstRun(marker))
                return;

            string version = AboutBox.AssemblyVersion;
            ChangelogWindow.Show(ChangelogPath, $"What's new in Viking {version}",
                $"Viking {version} is starting for the first time. The change log is always under Help > Change Log.",
                ElementHost.EnableModelessKeyboardInterop);
        }

        /// <summary>
        /// Creates <paramref name="markerPath"/> and returns true the first time; false once it exists, or when it cannot be
        /// written (so a read-only profile does not show the changelog at every start).
        /// </summary>
        public static bool TryMarkFirstRun(string markerPath)
        {
            try
            {
                if (File.Exists(markerPath))
                    return false;

                string? folder = Path.GetDirectoryName(markerPath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(markerPath, DateTime.Now.ToString("o"));
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"Change log first-run marker not written: {e.Message}");
                return false;
            }
        }
    }
}
