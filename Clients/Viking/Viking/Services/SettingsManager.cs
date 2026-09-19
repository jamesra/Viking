using System;
using System.Diagnostics;

namespace Viking.Services
{
    /// <summary>
    /// Service for managing application settings, including upgrades from previous versions.
    /// </summary>
    public static class SettingsManager
    {
        /// <summary>
        /// Copies user settings from the previous application version once per install.
        /// VolumeURLs has a Designer default, so a non-empty collection is not proof that Upgrade already ran.
        /// </summary>
        public static void UpgradeSettingsIfNeeded()
        {
            try
            {
                var settings = Properties.Settings.Default;
                if (!settings.UpgradeRequired)
                {
                    Trace.WriteLine("[Viking] Settings already upgraded for this version.");
                    return;
                }

                Trace.WriteLine("[Viking] UpgradeRequired is set, migrating settings from the previous version...");
                settings.Upgrade();
                settings.UpgradeRequired = false;
                settings.Save();
                Trace.WriteLine("[Viking] Settings upgrade completed.");
            }
            catch (Exception ex)
            {
                // Don't fail application startup if settings upgrade fails
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade settings: {ex.Message}");
            }
        }
    }
}
