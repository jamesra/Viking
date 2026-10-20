using System;

namespace Viking.Common
{
    /// <summary>
    /// VikingXML IdentityApi/Authentication often copies the public Identity host.
    /// Permissions live on the WebApi, not IdentityServerStandalone. Two layouts are supported:
    /// <list type="bullet">
    /// <item>Port-per-app (legacy, e.g. identity.codepharm.net): issuer :5001, WebApi :6001 HTTPS / :6000 HTTP.</item>
    /// <item>Path-per-app behind viking-proxy on 443 (e.g. identity.connectomes.pitt.edu): issuer at /,
    /// WebApi at /webapi/, management at /manage/.</item>
    /// </list>
    /// An issuer URL on the scheme's default port (443/80) selects the path layout.
    /// </summary>
    public static class IdentityEndpoints
    {
        public const int PermissionsApiHttpPort = 6000;
        public const int PermissionsApiHttpsPort = 6001;

        /// <summary>WebApi path prefix when Identity is served on the default port through viking-proxy.</summary>
        public const string PermissionsApiPathBase = "/webapi/";

        /// <summary>
        /// Returns the WebApi base URL for /Permissions.
        /// Called by login and VikingAU after reading VolumeToEndpoint, and by token helpers
        /// that already know the IdentityServer authority.
        /// Same host as IdentityServer is rewritten to the WebApi location (port 6001/6000, or
        /// /webapi/ when the issuer is on the default port) unless it already points there.
        /// A different host is left unchanged (dedicated API proxy).
        /// </summary>
        public static Uri ResolvePermissionsApiUrl(Uri identityApiFromXml, Uri identityServerUrl)
        {
            if (identityServerUrl == null)
                return identityApiFromXml;

            if (identityApiFromXml == null || ShouldRewriteToPermissionsApi(identityApiFromXml, identityServerUrl))
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

            if (identityServerUrl.IsDefaultPort)
            {
                return new UriBuilder(identityServerUrl)
                {
                    Port = -1,
                    Path = PermissionsApiPathBase,
                    Query = string.Empty,
                    Fragment = string.Empty
                }.Uri;
            }

            var https = string.Equals(identityServerUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            return new UriBuilder(identityServerUrl)
            {
                Port = https ? PermissionsApiHttpsPort : PermissionsApiHttpPort,
                Path = "/",
                Query = string.Empty,
                Fragment = string.Empty
            }.Uri;
        }

        static bool ShouldRewriteToPermissionsApi(Uri identityApi, Uri identityServer)
        {
            if (!string.Equals(identityApi.Host, identityServer.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            if (IsPermissionsApiPort(identityApi.Port))
                return false;

            return !IsPermissionsApiPath(identityApi);
        }

        static bool IsPermissionsApiPort(int port)
            => port == PermissionsApiHttpPort || port == PermissionsApiHttpsPort;

        static bool IsPermissionsApiPath(Uri uri)
            => uri.AbsolutePath.StartsWith(PermissionsApiPathBase.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// OAuth scope resource key for Read/Annotate/Review: Identity <c>AnnotationServerName</c> when set,
        /// otherwise the VikingXML or Identity volume display name.
        /// </summary>
        public static string ResolveScopeResourceName(string volumeName, string? annotationServerName)
        {
            if (!string.IsNullOrWhiteSpace(annotationServerName))
                return annotationServerName!.Trim();

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

            return Uri.TryCreate(annotationEndpointFromIdentity!.Trim(), UriKind.Absolute, out endpoint!);
        }

        /// <summary>
        /// Annotation service base URL: Identity <c>AnnotationEndpoint</c> when present, else VikingXML <c>VolumeToEndpoint</c>.
        /// </summary>
        public static Uri? ResolveAnnotationServiceEndpoint(Uri? volumeXmlEndpoint, string? annotationEndpointFromIdentity)
        {
            if (TryResolveAnnotationEndpointUri(annotationEndpointFromIdentity, out Uri identityEndpoint))
                return identityEndpoint;

            return volumeXmlEndpoint;
        }
    }
}
