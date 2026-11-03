using System;

#nullable enable

namespace Viking.UI.WPF.Services
{
    /// <summary>
    /// Host:port target for a TLS segmentation-service <c>Grpc.Core.Channel</c>, and whether the saved endpoint named
    /// plaintext (<c>http://</c> or no scheme) and was upgraded. <see cref="TryFormat"/> is the one rule for turning a saved
    /// endpoint into a target: the shared segmentation channel (<c>GrpcChannelManager</c> in VikingCore) and the
    /// Select Segmentation Service picker's status probe (<see cref="SegmentationServerStatusProbe"/>) both use it, so the
    /// picker judges a server at the same host and port that segmentation then dials.
    /// </summary>
    public readonly struct GrpcChannelTarget
    {
        /// <summary>
        /// The port the segmentation server published for its cleartext listener before that listener
        /// was removed. A saved <c>http://</c> or schemeless endpoint that still names it is moved to <see cref="TlsPort"/>.
        /// </summary>
        public const int LegacyCleartextPort = 40080;

        /// <summary>The port the segmentation server publishes for gRPC over TLS.</summary>
        public const int TlsPort = 40443;

        /// <summary>Creates a target; callers outside tests get one from <see cref="TryFormat"/>.</summary>
        public GrpcChannelTarget(string target, bool upgradedFromPlaintext)
        {
            Target = target;
            UpgradedFromPlaintext = upgradedFromPlaintext;
        }

        /// <summary>C-core channel target, <c>host:port[/path][?query]</c>, always with an explicit port.</summary>
        public string Target { get; }

        /// <summary>True when the saved endpoint was <c>http://</c> or had no scheme, so the channel log can say TLS replaced it.</summary>
        public bool UpgradedFromPlaintext { get; }

        /// <summary>Key that identifies the channel this target needs; a different key means the old channel must be replaced.</summary>
        public string ChannelKey => $"tls|{Target}";

        /// <summary>
        /// C-core channel targets are host:port[/path][?query]. The segmentation server only speaks
        /// TLS, so the result always selects TLS. An <c>http://</c> endpoint or one with no scheme was
        /// plaintext before; it keeps its host and port (except <see cref="LegacyCleartextPort"/>,
        /// which becomes <see cref="TlsPort"/>) and is flagged <see cref="UpgradedFromPlaintext"/>.
        /// The port is always written out, so a portless endpoint keeps its scheme's default port
        /// (80 for <c>http://</c> and no scheme, 443 for <c>https://</c>) rather than the TLS default.
        /// An IPv6 literal host stays in brackets, e.g. <c>[::1]:40443</c>.
        /// </summary>
        /// <returns>The target, or null for a blank or unparsable endpoint or a scheme other than http and https.</returns>
        public static GrpcChannelTarget? TryFormat(string? rawEndpoint)
        {
            if (rawEndpoint is null || string.IsNullOrWhiteSpace(rawEndpoint))
                return null;

            string trimmedEndpoint = rawEndpoint.Trim();
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : $"http://{trimmedEndpoint}";

            if (!Uri.TryCreate(endpointToParse, UriKind.Absolute, out Uri? parsedUri) || parsedUri is null)
                return null;

            if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
                return null;

            string absolutePath = parsedUri.AbsolutePath;
            if (string.Equals(absolutePath, "/", StringComparison.Ordinal))
                absolutePath = string.Empty;

            // Uri.Host already brackets an IPv6 literal ("[::1]"), as a host:port target needs; bracketing it again
            // gave "[[::1]]", which C-core cannot resolve.
            string host = parsedUri.Host;
            bool upgradedFromPlaintext = parsedUri.Scheme == Uri.UriSchemeHttp;
            int port = upgradedFromPlaintext && parsedUri.Port == LegacyCleartextPort
                ? TlsPort
                : parsedUri.Port;
            string target = $"{host}:{port}{absolutePath}{parsedUri.Query}";
            return new GrpcChannelTarget(target, upgradedFromPlaintext);
        }
    }
}
