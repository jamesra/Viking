using System;
using System.Linq;
using System.Web.Configuration;
using UnitsAndScale;

namespace VikingWebAppSettings
{
    public static class UriExtensions
    {
        public static Uri Append(this Uri uri, params string[] paths)
        {
            return new Uri(paths.Aggregate(uri.AbsoluteUri, (current, path) => string.Format("{0}/{1}", current.TrimEnd('/'), path.TrimStart('/'))));
        }
    }

    public static class AppSettings
    {
        public static string GetApplicationSetting(string name)
        {
            if (!WebConfigurationManager.AppSettings.HasKeys())
            {
                throw new ArgumentException(name + " not configured in AppSettings");
            }

            string setting = WebConfigurationManager.AppSettings[name];
            if (setting == null)
            {
                throw new ArgumentException(name + " not configured in AppSettings");
            }

            return setting;
        }

        public static string GetDatabaseServer()
        {
            return GetApplicationSetting("DatabaseServer");
        }

        public static string GetDatabaseCatalogName()
        {
            return GetApplicationSetting("DatabaseCatalog");
        } 

        public static string GetDefaultDatabaseConnectionStringName()
        { 
            return GetApplicationSetting("DatabaseConnectionName");
        }

        public static string GetIdentityServerURLString()
        {
            return GetApplicationSetting("IdentityServer");
        }

        public static string GetDefaultConnectionString()
        {
            return GetConnectionString(GetDefaultDatabaseConnectionStringName());
        }

        public static string[] GetAllowedOrganizations()
        {
            return GetStringList("AllowedOrganizations");
        }

        public static string[] GetStringList(string name)
        {
            string setting = GetApplicationSetting(name);
            if (setting == null)
                return Array.Empty<string>();

            return setting.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        public static string GetConnectionString(string name)
        { 
            if (WebConfigurationManager.ConnectionStrings == null)
            {
                throw new ArgumentException("WebConfigurationManager.ConnectionStrings is null");
            }
            if (WebConfigurationManager.ConnectionStrings.Count == 0)
            {
                throw new ArgumentException("Connection string " + name + " not configured.");
            }
            if (WebConfigurationManager.ConnectionStrings[name] == null)
            {
                throw new ArgumentException("Connection string " + name + " has a null ConnectionStringSettings value");
            }

            string conn_string = WebConfigurationManager.ConnectionStrings[name].ConnectionString;
            if(conn_string == null)
            {
                throw new ArgumentException("Connection string " + name + " returned null ConnectionString");
            }

            return conn_string;
        }
          
        public static string WebServiceURL
        {
            get
            {
                return GetApplicationSetting("EndpointURL");
            }
        }

        public static string VolumeURL
        {
            get
            {
                return GetApplicationSetting("VolumeURL");
            }
        }

        public static Uri VolumeURI
        {
            get
            {
                Uri uri = null;
                if (Uri.TryCreate(VolumeURL, UriKind.Absolute, out uri))
                    return uri;

                return null;
            }
        }

        public static Uri ODataURL
        {
            get
            {
                return VolumeURI.Append("OData");
            }
        }

        public static System.Net.NetworkCredential EndpointCredentials
        {
            get
            {
                System.Net.NetworkCredential userCredentials = new System.Net.NetworkCredential(GetApplicationSetting("EndpointUsername"), GetApplicationSetting("EndpointPassword"));
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