using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// What the annotation server catalog needs from one VikingXML document.
    /// </summary>
    public class VikingXmlCatalogEntry
    {
        public string VolumeName { get; set; }

        /// <summary>VolumeToEndpoint/@Name: the annotation database name.</summary>
        public string AnnotationDatabaseName { get; set; }

        /// <summary>VolumeToEndpoint/@Endpoint. Null when the VikingXML describes images only.</summary>
        public Uri AnnotationEndpoint { get; set; }

        public Uri ExportUrl { get; set; }

        public Uri AuthenticationUrl { get; set; }

        /// <summary>See <see cref="VikingXmlCatalog.ComputeContentHash"/>.</summary>
        public string ContentHash { get; set; }

        /// <summary>Set when VolumeToEndpoint exists but its Endpoint is not an absolute http(s) URL.</summary>
        public string EndpointError { get; set; }
    }

    public static class VikingXmlCatalog
    {
        public static VikingXmlCatalogEntry Parse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new ArgumentException("VikingXML content is empty.", nameof(xml));

            var doc = XDocument.Parse(xml);
            var volumeElement = FindChild(doc.Root, "Volume") ?? doc.Root;
            if (volumeElement == null)
                throw new InvalidOperationException("VikingXML does not contain a Volume element.");

            var entry = new VikingXmlCatalogEntry
            {
                VolumeName = Attribute(volumeElement, "Name")?.Trim(),
                ContentHash = ComputeContentHash(volumeElement)
            };

            var endpointElement = FindChild(volumeElement, "VolumeToEndpoint");
            if (endpointElement == null)
                return entry;

            entry.AnnotationDatabaseName = Attribute(endpointElement, "Name")?.Trim();
            entry.ExportUrl = TryHttpUri(Attribute(endpointElement, "ExportURL"));
            entry.AuthenticationUrl = TryHttpUri(Attribute(endpointElement, "Authentication"));

            var endpointText = Attribute(endpointElement, "Endpoint");
            entry.AnnotationEndpoint = TryHttpUri(endpointText);
            if (entry.AnnotationEndpoint == null && !string.IsNullOrWhiteSpace(endpointText))
                entry.EndpointError = $"VolumeToEndpoint Endpoint '{endpointText.Trim()}' is not an absolute http or https URL.";

            return entry;
        }

        /// <summary>
        /// Canonical form used to match volumes to one annotation server: lower-case scheme and host,
        /// default port dropped, no query, fragment, or trailing slash. Path case is kept.
        /// </summary>
        public static string NormalizeEndpoint(Uri endpoint)
        {
            if (endpoint == null || !endpoint.IsAbsoluteUri)
                return null;

            return endpoint.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        /// <summary>
        /// True when the normalized endpoint fits the indexed column.
        /// </summary>
        public static bool FitsEndpointColumn(string normalizedEndpoint) =>
            normalizedEndpoint != null && normalizedEndpoint.Length <= AnnotationServer.MaxEndpointLength;

        /// <summary>
        /// SHA-256 (lower-case hex) of the Volume element with its host-specific <c>path</c> attribute removed,
        /// so two hosts serving the same files produce the same hash.
        /// </summary>
        public static string ComputeContentHash(XElement volumeElement)
        {
            ArgumentNullException.ThrowIfNull(volumeElement);

            var copy = new XElement(volumeElement);
            copy.Attributes()
                .Where(a => string.Equals(a.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                .Remove();

            var bytes = Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting));
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static Uri TryHttpUri(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
                return null;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri : null;
        }

        private static XElement FindChild(XElement parent, string localName)
        {
            if (parent == null)
                return null;

            if (string.Equals(parent.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                return parent;

            return parent.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        }

        private static string Attribute(XElement element, string name) =>
            element.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
                ?.Value;
    }
}
