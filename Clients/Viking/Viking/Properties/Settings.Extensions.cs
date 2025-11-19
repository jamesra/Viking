using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Viking.Properties
{
    internal sealed partial class Settings
    {
        private const string SegmentationServiceUrlsKey = "SegmentationServiceUrls";
        private const string LastSegmentationServiceUrlKey = "SegmentationServiceUrl";

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

        private object GetOrCreateSetting(string key, Func<object> defaultFactory, Type valueType, SettingsSerializeAs serializeAs)
        {
            if (Properties[key] == null)
            {
                var provider = Providers["LocalFileSettingsProvider"];
                var property = new SettingsProperty(key)
                {
                    PropertyType = valueType,
                    IsReadOnly = false,
                    Provider = provider,
                    SerializeAs = serializeAs,
                    DefaultValue = null
                };
                property.Attributes.Add(typeof(UserScopedSettingAttribute), new UserScopedSettingAttribute());
                Properties.Add(property);
                Reload();
            }

            var value = this[key];
            if (value == null)
            {
                value = defaultFactory();
                this[key] = value;
            }

            return value;
        }
    }
}

