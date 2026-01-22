using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Configuration;
using UnitsAndScale;

namespace VikingWebAppSettings
{
    public static class UriExtensions
    {
        public static Uri Append(this Uri uri, params string[] paths) => new Uri(paths.Aggregate(uri.AbsoluteUri, (current, path) => string.Format("{0}/{1}", current.TrimEnd('/'), path.TrimStart('/'))));
    }

    public static class AppSettings
    {
        public static string GetApplicationSetting(string name)
        {
            // First check environment variable (convert key name to environment variable format)
            string envVarName = name.Replace(".", "_").ToUpperInvariant();
            string envValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }

            // Fall back to web.config
            if (!WebConfigurationManager.AppSettings.HasKeys())
            {
                throw new ArgumentException(name + " not configured in AppSettings or environment variables");
            }

            string setting = WebConfigurationManager.AppSettings[name];
            return setting ?? throw new ArgumentNullException(name + " not configured in AppSettings or environment variables");
        }

        public static string GetDatabaseServer() => GetApplicationSetting("DatabaseServer");

        public static string GetDatabaseCatalogName() => GetApplicationSetting("DatabaseCatalog");

        public static string GetDefaultDatabaseConnectionStringName() => GetApplicationSetting("DatabaseConnectionName");

        public static string GetIdentityServerURLString() => GetApplicationSetting("IdentityServer");

        public static string GetDefaultConnectionString() => GetConnectionString(GetDefaultDatabaseConnectionStringName());

        public static string[] GetAllowedOrganizations() => GetStringList("AllowedOrganizations");

        public static string[] GetStringList(string name)
        {
            string setting = GetApplicationSetting(name);
            if (setting is null)
                return [];

            return [.. setting.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0)];
        }

        public static string GetConnectionString(string name)
        {
            if (WebConfigurationManager.ConnectionStrings is null)
            {
                throw new ArgumentException("WebConfigurationManager.ConnectionStrings is null");
            }
            if (WebConfigurationManager.ConnectionStrings.Count == 0)
            {
                throw new ArgumentException("Connection string " + name + " not configured.");
            }
            if (WebConfigurationManager.ConnectionStrings[name] is null)
            {
                throw new ArgumentException("Connection string " + name + " has a null ConnectionStringSettings value");
            }

            string connectionString = WebConfigurationManager.ConnectionStrings[name].ConnectionString ?? throw new ArgumentException("Connection string " + name + " returned null ConnectionString");

            // Substitute environment variables in connection string
            // Pattern: %VARIABLE_NAME% will be replaced with environment variable value
            connectionString = Regex.Replace(connectionString, @"%([A-Z_][A-Z0-9_]*)%", match =>
            {
                string envVarName = match.Groups[1].Value;
                string envValue = Environment.GetEnvironmentVariable(envVarName);
                return envValue ?? match.Value; // Return original if env var not found
            });

            return connectionString;
        }

        public static string WebServiceURL => GetApplicationSetting("EndpointURL");

        public static string VolumeURL => GetApplicationSetting("VolumeURL");

        public static Uri VolumeURI => Uri.TryCreate(VolumeURL, UriKind.Absolute, out var uri) ? uri : null;

        public static Uri ODataURL => VolumeURI.Append("OData");

        public static System.Net.NetworkCredential EndpointCredentials
        {
            get
            {
                System.Net.NetworkCredential userCredentials = new(GetApplicationSetting("EndpointUsername"), GetApplicationSetting("EndpointPassword"));
                return userCredentials;
            }
        }

        public static Scale GetScale()
        {
            AxisUnits X, Y, Z;

#if DEBUG
            try
            {
#endif
            X = new AxisUnits(System.Convert.ToDouble(GetApplicationSetting("XScaleValue")),
                                        GetApplicationSetting("XScaleUnits"));

            Y = new AxisUnits(System.Convert.ToDouble(GetApplicationSetting("YScaleValue")),
                                        GetApplicationSetting("YScaleUnits"));

            Z = new AxisUnits(System.Convert.ToDouble(GetApplicationSetting("ZScaleValue")),
                                        GetApplicationSetting("ZScaleUnits"));
#if DEBUG
            }
            catch(ArgumentException)
            {
                X = new AxisUnits(2.18, "nm");

                Y = new AxisUnits(2.18, "nm");

                Z = new AxisUnits(90, "nm");
            }
#endif
            return new Scale(X, Y, Z);
        }
    }
}