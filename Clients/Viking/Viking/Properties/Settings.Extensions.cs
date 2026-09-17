#nullable disable

using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Viking.Properties
{
    internal sealed partial class Settings
    {
        private const string SegmentationServiceUrlsKey = "SegmentationServiceUrls";
        private const string LastSegmentationServiceUrlKey = "SegmentationServiceUrl";
        private const string SbfsemToolsOpenUrlKey = "SbfsemToolsOpenUrl";
        private const string SbfsemToolsIdentityBounceUrlKey = "SbfsemToolsIdentityBounceUrl";
        private const string DefaultSbfsemToolsOpenUrl = "https://sbfsem-tools.com/open";
        private const string DefaultSbfsemToolsIdentityBounceUrl = "https://identity.codepharm.net:4001/SbfsemOpen/Redirect";

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

