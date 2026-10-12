#nullable disable

using System;
using System.Configuration;
using System.Diagnostics;

namespace Viking.UI.WPF.Properties
{
    /// <summary>
    /// The login window's user settings (identity server URL, remembered credentials).
    /// </summary>
    /// <remarks>
    /// The Viking exe has a separate <c>Viking.Properties.Settings</c> class, and
    /// <see cref="ApplicationSettingsBase.Upgrade"/> only migrates the properties of the class it is called on.
    /// The exe's upgrade therefore never carries these values to a new version, so this class upgrades itself.
    /// </remarks>
    internal sealed partial class Settings
    {
        private const string LoginUpgradeRequiredKey = "LoginUpgradeRequired";

        /// <summary>
        /// True until <see cref="UpgradeIfNeeded"/> has copied these settings from the previous application version.
        /// Registered at runtime, like the exe's UpgradeRequired, so the designer file stays untouched. It has its own key
        /// because both classes share one user.config section.
        /// </summary>
        private bool LoginUpgradeRequired
        {
            get
            {
                EnsureLoginUpgradeRequiredProperty();
                object value = this[LoginUpgradeRequiredKey];
                return value is null || (bool)value;
            }
            set
            {
                EnsureLoginUpgradeRequiredProperty();
                this[LoginUpgradeRequiredKey] = value;
            }
        }

        /// <summary>
        /// Copies the login library's user settings from the previous application version once per install, then saves.
        /// Call once at startup before any of these settings are read, on the UI thread.
        /// Failures are logged and swallowed so a bad config file cannot stop the app from starting.
        /// </summary>
        public void UpgradeIfNeeded()
        {
            try
            {
                if (!LoginUpgradeRequired)
                    return;

                Trace.WriteLine("[Viking] Migrating login settings from the previous version...");
                Upgrade();
                LoginUpgradeRequired = false;
                Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade login settings: {ex.Message}");
            }
        }

        private void EnsureLoginUpgradeRequiredProperty()
        {
            if (Properties[LoginUpgradeRequiredKey] is not null)
                return;

            SettingsProperty property = new(LoginUpgradeRequiredKey)
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
    /// Public entry point for the Viking exe to upgrade the login library's internal settings.
    /// </summary>
    public static class LoginSettingsUpgrade
    {
        /// <summary>Upgrades the login library's user settings from the previous version if that has not happened yet.</summary>
        public static void UpgradeIfNeeded() => Settings.Default.UpgradeIfNeeded();
    }
}
