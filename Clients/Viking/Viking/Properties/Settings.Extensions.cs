#nullable disable

using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Viking.Properties
{
    internal sealed partial class Settings
    {
        private const string UpgradeRequiredKey = "UpgradeRequired";
        private const string SegmentationServiceUrlsKey = "SegmentationServiceUrls";
        private const string LastSegmentationServiceUrlKey = "SegmentationServiceUrl";
        private const string LaunchExchangeBaseUrlKey = "LaunchExchangeBaseUrl";
        private const string SbfsemToolsOpenUrlKey = "SbfsemToolsOpenUrl";
        private const string SbfsemToolsIdentityBounceUrlKey = "SbfsemToolsIdentityBounceUrl";
        private const string DefaultLaunchExchangeBaseUrl = "https://identity.codepharm.net:6001";
        private const string DefaultSbfsemToolsOpenUrl = "https://sbfsem-tools.com/open";
        private const string DefaultSbfsemToolsIdentityBounceUrl = "https://identity.codepharm.net:4001/SbfsemOpen/Redirect";

        /// <summary>
        /// When true, user settings should be copied from the previous application version.
        /// Defaults to true so a new Velopack user.config still runs Upgrade() even when
        /// VolumeURLs already has a Designer default. Do not route this through GetOrCreateSetting:
        /// that helper Reload()s and would wipe the in-memory flag before Save.
        /// </summary>
        public bool UpgradeRequired
        {
            get
            {
                EnsureUpgradeRequiredProperty();
                var value = this[UpgradeRequiredKey];
                return value is null || (bool)value;
            }
            set
            {
                EnsureUpgradeRequiredProperty();
                this[UpgradeRequiredKey] = value;
            }
        }

        public StringCollection SegmentationServiceUrls
        {
            get => (StringCollection)GetOrCreateSetting(SegmentationServiceUrlsKey, () => new StringCollection(), typeof(StringCollection), SettingsSerializeAs.Xml);
            set => this[SegmentationServiceUrlsKey] = value;
        }

        public string LastSegmentationServiceUrl
        {
            get => (string)GetOrCreateSetting(LastSegmentationServiceUrlKey, () => string.Empty, typeof(string), SettingsSerializeAs.String);
            set => this[LastSegmentationServiceUrlKey] = value;
        }

        /// <summary>Base URL of the Identity WebApi for viking://open code exchange.</summary>
        public string LaunchExchangeBaseUrl
        {
            get => (string)GetOrCreateSetting(LaunchExchangeBaseUrlKey, () => DefaultLaunchExchangeBaseUrl, typeof(string), SettingsSerializeAs.String);
            set => this[LaunchExchangeBaseUrlKey] = value;
        }

        /// <summary>SBFSEM-tools /open endpoint (used by Identity bounce target).</summary>
        public string SbfsemToolsOpenUrl
        {
            get => (string)GetOrCreateSetting(SbfsemToolsOpenUrlKey, () => DefaultSbfsemToolsOpenUrl, typeof(string), SettingsSerializeAs.String);
            set => this[SbfsemToolsOpenUrlKey] = value;
        }

        /// <summary>Identity bounce URL for Open in SBFSEM-tools (establishes browser SSO cookie).</summary>
        public string SbfsemToolsIdentityBounceUrl
        {
            get => (string)GetOrCreateSetting(SbfsemToolsIdentityBounceUrlKey, () => DefaultSbfsemToolsIdentityBounceUrl, typeof(string), SettingsSerializeAs.String);
            set => this[SbfsemToolsIdentityBounceUrlKey] = value;
        }

        /// <summary>
        /// Registers UpgradeRequired as a user-scoped bool without Reload(), which would clobber the flag.
        /// </summary>
        private void EnsureUpgradeRequiredProperty()
        {
            if (Properties[UpgradeRequiredKey] is not null)
                return;

            var provider = Providers["LocalFileSettingsProvider"];
            SettingsProperty property = new(UpgradeRequiredKey)
            {
                PropertyType = typeof(bool),
                IsReadOnly = false,
                Provider = provider,
                SerializeAs = SettingsSerializeAs.String,
                DefaultValue = true
            };
            if (!property.Attributes.Contains(typeof(UserScopedSettingAttribute)))
                property.Attributes.Add(typeof(UserScopedSettingAttribute), new UserScopedSettingAttribute());
            Properties.Add(property);
        }

        private object GetOrCreateSetting(string key, Func<object> defaultFactory, Type valueType, SettingsSerializeAs serializeAs)
        {
            if (Properties[key] is null)
            {
                var provider = Providers["LocalFileSettingsProvider"];
                SettingsProperty property = new(key)
                {
                    PropertyType = valueType,
                    IsReadOnly = false,
                    Provider = provider,
                    SerializeAs = serializeAs,
                    DefaultValue = null
                };

                // Check if UserScopedSettingAttribute already exists before adding
                if (!property.Attributes.Contains(typeof(UserScopedSettingAttribute)))
                {
                    property.Attributes.Add(typeof(UserScopedSettingAttribute), new UserScopedSettingAttribute());
                }

                Properties.Add(property);
                Reload();
            }

            var value = this[key];
            if (value is null)
            {
                value = defaultFactory();
                this[key] = value;
            }

            return value;
        }
    }
}

