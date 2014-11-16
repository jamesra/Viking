using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Web.Configuration;
using System.Net;

namespace DataExport
{
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

        public static string WebServiceURLTemplate
        {
            get
            {
                return GetApplicationSetting("EndpointURLTemplate");
            }
        }

        public static NetworkCredential EndpointCredentials
        {
            get
            {
                NetworkCredential userCredentials = new NetworkCredential(GetApplicationSetting("EndpointUsername"), GetApplicationSetting("EndpointPassword"));
                return userCredentials;
            }
        }
        
    }
}