#nullable disable

using System;
using System.Configuration;
using System.Diagnostics;

namespace Measurement.Properties
{
    /// <summary>
    /// The measurement extension's user settings (scale bar visibility).
    /// </summary>
    /// <remarks>
    /// The Viking exe has a separate <c>Viking.Properties.Settings</c> class, and
    /// <see cref="ApplicationSettingsBase.Upgrade"/> only migrates the properties of the class it is called on.
    /// The exe's upgrade therefore never carries these values to a new version, so this class upgrades itself.
    /// </remarks>
    internal sealed partial class Settings
    {
        private const string MeasurementUpgradeRequiredKey = "MeasurementUpgradeRequired";

        /// <summary>
        /// True until <see cref="UpgradeIfNeeded"/> has copied these settings from the previous application version.
        /// Registered at runtime, like the exe's UpgradeRequired, so the designer file stays untouched. It has its own key
        /// because both classes share one user.config section.
        /// </summary>
        private bool MeasurementUpgradeRequired
        {
            get
            {
                EnsureMeasurementUpgradeRequiredProperty();
                object value = this[MeasurementUpgradeRequiredKey];
                return value is null || (bool)value;
            }
            set
            {
                EnsureMeasurementUpgradeRequiredProperty();
                this[MeasurementUpgradeRequiredKey] = value;
            }
        }

        /// <summary>
        /// Copies the measurement extension's user settings from the previous application version once per install, then saves.
        /// Called from the static constructors of the types that read these settings, because the exe loads this extension as a plugin and cannot call it directly. Safe to call repeatedly.
        /// Failures are logged and swallowed so a bad config file cannot stop the app from starting.
        /// </summary>
        public void UpgradeIfNeeded()
        {
            try
            {
                if (!MeasurementUpgradeRequired)
                    return;

                Trace.WriteLine("[Viking] Migrating measurement settings from the previous version...");
                Upgrade();
                MeasurementUpgradeRequired = false;
                Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade measurement settings: {ex.Message}");
            }
        }

        private void EnsureMeasurementUpgradeRequiredProperty()
        {
            if (Properties[MeasurementUpgradeRequiredKey] is not null)
                return;

            SettingsProperty property = new(MeasurementUpgradeRequiredKey)
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
}
