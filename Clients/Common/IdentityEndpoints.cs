using System;

namespace Viking.Common
{
    /// <summary>
    /// VikingXML IdentityApi/Authentication often copies the public Identity host (port 443 or :5001).
    /// Permissions live on WebApi (:6001 HTTPS / :6000 HTTP), not IdentityServerStandalone.
    /// Called by login after reading VolumeToEndpoint, and by volume/segmentation pickers.
    /// </summary>
    public static class IdentityEndpoints
    {
        public const int PermissionsApiHttpPort = 6000;
        public const int PermissionsApiHttpsPort = 6001;

        /// <summary>
        /// Returns the WebApi base URL for /Permissions.
        /// Same host as IdentityServer on any other port is rewritten to 6001/6000.
        /// A different host is left unchanged (dedicated API proxy).
        /// </summary>
        public static Uri ResolvePermissionsApiUrl(Uri identityApiFromXml, Uri identityServerUrl)
        {
            if (identityServerUrl == null)
                return identityApiFromXml;

            if (identityApiFromXml == null || ShouldRewriteToPermissionsPort(identityApiFromXml, identityServerUrl))
                return FromIdentityServer(identityServerUrl);

            return identityApiFromXml;
        }

        /// <summary>
        /// WebApi URL on the IdentityServer host. Used by volume/segmentation pickers that never read VikingXML.
        /// </summary>
        public static Uri FromIdentityServer(Uri identityServerUrl)
        {
            if (identityServerUrl == null)
                throw new ArgumentNullException(nameof(identityServerUrl));

            var https = string.Equals(identityServerUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            return new UriBuilder(identityServerUrl)
            {
                Port = https ? PermissionsApiHttpsPort : PermissionsApiHttpPort,
                Path = "/",
                Query = string.Empty,
                Fragment = string.Empty
            }.Uri;
        }

        static bool ShouldRewriteToPermissionsPort(Uri identityApi, Uri identityServer)
        {
            if (!string.Equals(identityApi.Host, identityServer.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            return !IsPermissionsApiPort(identityApi.Port);
        }

        static bool IsPermissionsApiPort(int port)
            => port == PermissionsApiHttpPort || port == PermissionsApiHttpsPort;

        /// <summary>
        /// OAuth scope resource key for Read/Annotate/Review: Identity <c>AnnotationServerName</c> when set,
        /// otherwise the VikingXML or Identity volume display name.
        /// </summary>
        public static string ResolveScopeResourceName(string volumeName, string? annotationServerName)
        {
            if (!string.IsNullOrWhiteSpace(annotationServerName))
                return annotationServerName.Trim();

            return volumeName ?? string.Empty;
        }

        /// <summary>
        /// Parses Identity <c>AnnotationEndpoint</c> when it is an absolute URI.
        /// </summary>
        public static bool TryResolveAnnotationEndpointUri(string? annotationEndpointFromIdentity, out Uri endpoint)
        {
            endpoint = null!;
            if (string.IsNullOrWhiteSpace(annotationEndpointFromIdentity))
                return false;

            return Uri.TryCreate(annotationEndpointFromIdentity.Trim(), UriKind.Absolute, out endpoint!);
        }

        /// <summary>
        /// Annotation WCF base URL: Identity <c>AnnotationEndpoint</c> when present, else VikingXML <c>VolumeToEndpoint</c>.
        /// </summary>
        public static Uri? ResolveAnnotationServiceEndpoint(Uri? volumeXmlEndpoint, string? annotationEndpointFromIdentity)
        {
            if (TryResolveAnnotationEndpointUri(annotationEndpointFromIdentity, out Uri identityEndpoint))
                return identityEndpoint;

            return volumeXmlEndpoint;
        }
    }
}
