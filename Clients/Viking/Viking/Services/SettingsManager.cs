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
        /// Upgrades settings from previous versions to preserve user preferences across updates.
        /// This migrates settings from version-specific directories to the current version.
        /// Uses a simple approach: if current settings are empty/default, try to upgrade from previous version.
        /// </summary>
        public static void UpgradeSettingsIfNeeded()
        {
            try
            {
                var settings = Properties.Settings.Default;

                // Check if we have any meaningful settings already (if VolumeURLs is empty, likely first run or after update)
                var hasExistingSettings = settings.VolumeURLs != null && settings.VolumeURLs.Count > 0;

                if (!hasExistingSettings)
                {
                    Trace.WriteLine("[Viking] No existing settings found, attempting to upgrade from previous version...");

                    // Call the built-in Upgrade() method which migrates settings from previous versions
                    // This will look for settings in older version directories and copy them to current version
                    settings.Upgrade();
                    settings.Reload();

                    // Check if we now have settings after upgrade
                    var hasSettingsAfterUpgrade = settings.VolumeURLs != null && settings.VolumeURLs.Count > 0;
                    if (hasSettingsAfterUpgrade)
                    {
                        var count = settings.VolumeURLs?.Count ?? 0;
                        Trace.WriteLine($"[Viking] Settings upgraded successfully. Found {count} volume URL(s).");
                        settings.Save(); // Save the upgraded settings
                    }
                    else
                    {
                        Trace.WriteLine("[Viking] No settings found in previous version - this appears to be a fresh install.");
                    }
                }
                else
                {
                    Trace.WriteLine("[Viking] Existing settings found, skipping upgrade.");
                }
            }
            catch (Exception ex)
            {
                // Don't fail application startup if settings upgrade fails
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade settings: {ex.Message}");
            }
        }
    }
}
