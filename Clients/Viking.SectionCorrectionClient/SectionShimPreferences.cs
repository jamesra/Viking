using System;
using System.Globalization;
using System.IO;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// Persists <see cref="SectionShimSettings"/> as two <c>key=value</c> lines for hosts without a settings system
    /// (Jotunn). Failures to read or write are ignored: the defaults are always usable.
    /// </summary>
    public static class SectionShimPreferences
    {
        const string StartKey = "FadeStartDownsample";
        const string EndKey = "FadeEndDownsample";

        /// <summary>Reads <paramref name="path"/> into <paramref name="settings"/>; missing or malformed values keep the current ones.</summary>
        public static void Load(string path, SectionShimSettings settings)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || !double.TryParse(line.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    if (key == StartKey)
                        settings.FadeStartDownsample = value;
                    else if (key == EndKey)
                        settings.FadeEndDownsample = value;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Writes <paramref name="settings"/> to <paramref name="path"/>, creating the folder.</summary>
        public static void Save(string path, SectionShimSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, new[]
                {
                    StartKey + "=" + settings.FadeStartDownsample.ToString(CultureInfo.InvariantCulture),
                    EndKey + "=" + settings.FadeEndDownsample.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}
