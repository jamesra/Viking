using System;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Canonical service-root helpers for <see cref="AnnotationServer.BaseUrl"/>.
    /// Derives Annotation / OData / Export URLs with the IIS layout used by Connectome volumes
    /// (<c>{BaseUrl}/Annotation/Service.svc</c>, <c>{BaseUrl}/OData/</c>, <c>{BaseUrl}/Export/</c>).
    /// Called by catalog sync, management UI, and accessible-volume metadata.
    /// </summary>
    public static class AnnotationServiceUrls
    {
        public const string AnnotationServicePath = "Annotation/Service.svc";
        public const string ODataPath = "OData/";
        public const string ExportPath = "Export/";

        /// <summary>
        /// Normalizes an absolute URL to a BaseUrl: lower-case scheme/host, no default port,
        /// no query/fragment/trailing slash, and strips known trailing service segments
        /// (<c>/Annotation/…</c>, <c>/OData</c>, <c>/Export</c>).
        /// </summary>
        public static string ToBaseUrl(Uri endpoint)
        {
            if (endpoint == null || !endpoint.IsAbsoluteUri)
                return null;

            var path = endpoint.AbsolutePath.TrimEnd('/');
            path = StripServiceSuffix(path);
            var builder = new UriBuilder(endpoint.Scheme, endpoint.Host, endpoint.IsDefaultPort ? -1 : endpoint.Port, path)
            {
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        /// <summary>
        /// Same as <see cref="ToBaseUrl(Uri)"/> for a stored string.
        /// </summary>
        public static string ToBaseUrl(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return null;

            return Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ? ToBaseUrl(uri) : null;
        }

        /// <summary>True when the normalized base URL fits the indexed column.</summary>
        public static bool FitsBaseUrlColumn(string baseUrl) =>
            baseUrl != null && baseUrl.Length <= AnnotationServer.MaxEndpointLength;

        /// <summary>Resolved WCF annotation URL for the server (or null when BaseUrl is missing).</summary>
        public static string AnnotationUrl(AnnotationServer server)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl))
                return null;

            return Combine(server.BaseUrl, AnnotationServicePath);
        }

        /// <summary>Resolved OData service root (trailing slash).</summary>
        public static string ODataUrl(AnnotationServer server)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl))
                return null;

            return Combine(server.BaseUrl, ODataPath);
        }

        /// <summary>
        /// Export service URL: stored override when set, otherwise <c>{BaseUrl}/Export/</c>.
        /// </summary>
        public static string ExportUrl(AnnotationServer server)
        {
            if (server == null)
                return null;

            if (server.ExportUrl != null)
                return server.ExportUrl.ToString();

            if (string.IsNullOrWhiteSpace(server.BaseUrl))
                return null;

            return Combine(server.BaseUrl, ExportPath);
        }

        /// <summary>
        /// True when <paramref name="exportUrl"/> is the same as the derived Export path for
        /// <paramref name="baseUrl"/> (so catalog sync need not store an override).
        /// </summary>
        public static bool IsDerivedExportUrl(string baseUrl, Uri exportUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || exportUrl == null)
                return false;

            var derived = Combine(baseUrl, ExportPath);
            var normalizedExport = VikingXmlCatalog.NormalizeEndpoint(exportUrl);
            var normalizedDerived = VikingXmlCatalog.NormalizeEndpoint(new Uri(derived));
            return string.Equals(normalizedExport, normalizedDerived, StringComparison.OrdinalIgnoreCase);
        }

        private static string Combine(string baseUrl, string relative)
        {
            var root = baseUrl.TrimEnd('/');
            var path = relative.TrimStart('/');
            return $"{root}/{path}";
        }

        private static string StripServiceSuffix(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath;

            var segments = absolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return absolutePath;

            int cut = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("Annotation", StringComparison.OrdinalIgnoreCase)
                    || segments[i].Equals("OData", StringComparison.OrdinalIgnoreCase)
                    || segments[i].Equals("Export", StringComparison.OrdinalIgnoreCase))
                {
                    cut = i;
                    break;
                }
            }

            if (cut == segments.Length)
                return "/" + string.Join("/", segments);

            if (cut == 0)
                return string.Empty;

            return "/" + string.Join("/", segments, 0, cut);
        }
    }
}
