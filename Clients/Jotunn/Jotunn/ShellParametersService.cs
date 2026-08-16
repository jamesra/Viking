using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Jotunn
{
    /// <summary>
    /// Exposes the volume URL / CLI arguments and the VikingXML document to the shell.
    /// </summary>
    internal class ShellParameterService : IShellParameters
    {
        public const string DefaultVolumeUrl = "http://connectomes.utah.edu/Rabbit/Volume.VikingXML";

        internal readonly NameValueCollection ArgTable;
        internal XDocument InitializationXML;

        public string VolumeUrl { get; }
        public string HostPath { get; }
        public XDocument Xml => InitializationXML;

        public ShellParameterService(NameValueCollection argTable, XDocument initXml, string volumeUrl, string hostPath)
        {
            ArgTable = argTable;
            InitializationXML = initXml;
            VolumeUrl = volumeUrl;
            HostPath = hostPath;
        }

        public static string FirstVolumeUrlFromArgs(string[] args)
        {
            if (args == null || args.Length == 0)
                args = Environment.GetCommandLineArgs().Skip(1).ToArray();

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith("-", StringComparison.Ordinal))
                        continue;

                    return arg;
                }
            }

            return null;
        }

        public static ShellParameterService FromCommandLine(string[] args)
        {
            return FromVolumeUrl(FirstVolumeUrlFromArgs(args) ?? DefaultVolumeUrl);
        }

        public static ShellParameterService FromVolumeUrl(string volumeUrl)
        {
            if (string.IsNullOrWhiteSpace(volumeUrl))
                volumeUrl = DefaultVolumeUrl;

            string website = AppendDefaultVolumeFilenameIfMissing(volumeUrl);

            Uri websiteUri = new Uri(website, UriKind.RelativeOrAbsolute);
            if (!websiteUri.IsAbsoluteUri)
                websiteUri = new Uri(Path.GetFullPath(website));

            string hostPath = DirectoryUrl(websiteUri);
            NameValueCollection argTable = new NameValueCollection
            {
                { "Host", websiteUri.ToString() },
                { "HostPath", hostPath }
            };

            XDocument xDoc = Utils.IO.Load(websiteUri);
            return new ShellParameterService(argTable, xDoc, websiteUri.ToString(), hostPath);
        }

        private static string AppendDefaultVolumeFilenameIfMissing(string website)
        {
            Uri websiteUri;
            if (!Uri.TryCreate(website, UriKind.Absolute, out websiteUri))
            {
                if (website.IndexOf('.') < 0)
                {
                    if (!website.EndsWith("/", StringComparison.Ordinal) && !website.EndsWith("\\", StringComparison.Ordinal))
                        website += "/";
                    website += "volume.VikingXML";
                }
                return website;
            }

            string path = websiteUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            if (path.Contains("."))
                return website;

            if (!website.EndsWith("/", StringComparison.Ordinal))
                website += "/";

            return website + "volume.VikingXML";
        }

        private static string DirectoryUrl(Uri volumeUri)
        {
            string value = volumeUri.ToString();
            int lastSlash = value.LastIndexOf('/');
            if (lastSlash <= 0)
                return value;
            return value.Substring(0, lastSlash);
        }

        XDocument IShellParameters.GetXML => InitializationXML;

        NameValueCollection IShellParameters.GetArgTable => ArgTable;
    }
}
