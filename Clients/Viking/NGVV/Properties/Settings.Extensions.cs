#nullable disable

using System;
using System.Configuration;
using System.Diagnostics;

namespace Viking.Properties
{
    /// <summary>
    /// VikingCore's own user settings (section-number overlay, texture loading, command help).
    /// </summary>
    /// <remarks>
    /// The Viking exe has a separate <c>Viking.Properties.Settings</c> class, and
    /// <see cref="ApplicationSettingsBase.Upgrade"/> only migrates the properties of the class it is called on.
    /// The exe's upgrade therefore never carries these values to a new version, so this class upgrades itself.
    /// </remarks>
    internal sealed partial class Settings
    {
        private const string CoreUpgradeRequiredKey = "CoreUpgradeRequired";

        /// <summary>
        /// True until <see cref="UpgradeIfNeeded"/> has copied these settings from the previous application version.
        /// Registered at runtime, like the exe's UpgradeRequired, so the designer file stays untouched. It has its own key
        /// because both classes share one user.config section.
        /// </summary>
        private bool CoreUpgradeRequired
        {
            get
            {
                EnsureCoreUpgradeRequiredProperty();
                object value = this[CoreUpgradeRequiredKey];
                return value is null || (bool)value;
            }
            set
            {
                EnsureCoreUpgradeRequiredProperty();
                this[CoreUpgradeRequiredKey] = value;
            }
        }

        /// <summary>
        /// Copies VikingCore's user settings from the previous application version once per install, then saves.
        /// Call once at startup before any of these settings are read, on the UI thread.
        /// Failures are logged and swallowed so a bad config file cannot stop the app from starting.
        /// </summary>
        public void UpgradeIfNeeded()
        {
            try
            {
                if (!CoreUpgradeRequired)
                    return;

                Trace.WriteLine("[Viking] Migrating VikingCore settings from the previous version...");
                Upgrade();
                CoreUpgradeRequired = false;
                Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade VikingCore settings: {ex.Message}");
            }
        }

        private void EnsureCoreUpgradeRequiredProperty()
        {
            if (Properties[CoreUpgradeRequiredKey] is not null)
                return;

            SettingsProperty property = new(CoreUpgradeRequiredKey)
            {
                PropertyType = typeof(bool),
                IsReadOnly = false,
                Provider = Providers["LocalFileSettingsProvider"],
                SerializeAs = SettingsSerializeAs.String,
                DefaultValue = true
            };
            if (!property.Attributes.Contains(typeof(UserScopedSettingAttribute)))
                property.Attributes.Add(typeof(UserScopedSettingAttribute), new UserScopedSettingAttribute());
            Properties.Add(property);
        }
    }

    /// <summary>
    /// Public entry point for the Viking exe to upgrade VikingCore's internal settings.
    /// </summary>
    public static class CoreSettingsUpgrade
    {
        /// <summary>Upgrades VikingCore's user settings from the previous version if that has not happened yet.</summary>
        public static void UpgradeIfNeeded() => Settings.Default.UpgradeIfNeeded();
    }
}
