using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Viking.Common
{
    public static class Extensions
    {


    }

    public class Util
    {
        /// <summary>
        /// Builds the viking.application deep-link URL for a position in the open volume.
        /// Called by <c>BookmarkUIObj.URI</c> on the UI thread; reads <c>State.volume</c>.
        /// </summary>
        /// <remarks>Numbers use <see cref="CultureInfo.InvariantCulture"/> because the URL is machine-read; a comma decimal separator would corrupt the query string.</remarks>
        public static string CoordinatesToURI(double X, double Y, int Z, double Downsample)
        {
            string URI = "http://connectomes.utah.edu/software/viking.application?" +
                             "Volume=" + Viking.UI.State.volume.Host + "&" +
                             "X=" + X.ToString("F1", CultureInfo.InvariantCulture) + "&" +
                             "Y=" + Y.ToString("F1", CultureInfo.InvariantCulture) + "&" +
                             "Z=" + Z.ToString(CultureInfo.InvariantCulture) + "&" +
                             "DS=" + Downsample.ToString("F2", CultureInfo.InvariantCulture);
            return URI;
        }

        public static string CoordinatesToCopyPaste(double X, double Y, int Z, double Downsample)
        {
            string clip = "X: " + X.ToString("F1") + "\t" +
                          "Y: " + Y.ToString("F1") + "\t" +
                          "Z: " + Z.ToString() + "\t" +
                          "DS: " + Downsample.ToString("F2");
            return clip;
        }

        /// <summary>
        /// Returns a single attribute of type from an object
        /// </summary>
        public static Attribute? GetAttribute(System.Type ObjType, System.Type AttribType)
        {
            MemberInfo info = ObjType;
            Attribute[] aAttributes = (Attribute[])info.GetCustomAttributes(AttribType, true);
            Debug.Assert(aAttributes.Length < 2);
            if (aAttributes.Length == 1)
                return aAttributes[0];

            return null;
        }

        public static string AppendDefaultVolumeFilenameIfMissing(string url)
        {
            if (url is null)
                return null;

            Uri WebsiteURI = new(url);
            string path = WebsiteURI.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            if (!path.Contains('.'))
            {
                if (url.EndsWith("/") == false)
                    url += "/";

                url += "volume.vikingxml";
            }

            return url;
        }

        private string TryAddVikingXMLExtension(string URL)
        {
            string NewURL = URL;

            if (!NewURL.ToLower().EndsWith(".vikingxml"))
            {
                if (NewURL.EndsWith("/") == false)
                    NewURL += '/';

                NewURL += "volume.vikingxml";
            }

            return NewURL;
        }
    }
}
