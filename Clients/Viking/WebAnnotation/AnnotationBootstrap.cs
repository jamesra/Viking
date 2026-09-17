using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Viking.Tokens;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;

namespace WebAnnotation
{
    /// <summary>
    /// Toolkit-agnostic gRPC store init. Viking's module loader and Jotunn both call this.
    /// </summary>
    public static class AnnotationBootstrap
    {
        public static Volume Volume { get; private set; }

        public static VolumeTransformProvider Transforms { get; private set; }

        public static bool TryInitialize(Volume volume, NetworkCredential credentials, string segmentationServiceUrl)
        {
            return Task.Run(() => TryInitializeAsync(volume, credentials, segmentationServiceUrl))
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Compose gRPC annotation stores and warm structure types. Called by Jotunn from
        /// <c>await</c> during splash so the UI thread is not blocked on gRPC.
        /// </summary>
        public static async Task<bool> TryInitializeAsync(
            Volume volume,
            NetworkCredential credentials,
            string segmentationServiceUrl,
            CancellationToken cancellationToken = default)
        {
            Volume = volume;
            Transforms = volume == null ? null : new VolumeTransformProvider(volume);
            if (volume?.VolumeElement is null)
                return false;

            WebAnnotationModel.State.UserCredentials = credentials;
            if (!string.IsNullOrWhiteSpace(segmentationServiceUrl))
                Global.AnnotationSettings.SegmentationServiceUrl = segmentationServiceUrl;

            if (!TryPopulateEndpointFromVolumeXml(volume.VolumeElement))
                return false;

            Uri endpoint = WebAnnotationModel.State.Endpoint;
            if (endpoint is null)
                return false;

            ServiceCollection services = new();
            services.AddSingleton<IAnnotationAccessTokenProvider, TokenStoreAccessTokenProvider>();
            services.ConfigureAnnotationModel(
                opts => opts.Endpoint = endpoint,
                channelOpts =>
                {
                    channelOpts.HttpHandler = CreateGrpcHttpHandler();
                });

            ServiceProvider provider = services.BuildServiceProvider();
            if (provider.GetService<IOptions<GrpcRepositorySettings>>() is IOptions<GrpcRepositorySettings> grpcSettings)
                grpcSettings.Value.Endpoint = endpoint;

            if (provider.GetService<IAnnotationStores>() is IAnnotationStores stores)
            {
                await Store.InitializeAsync(stores, cancellationToken).ConfigureAwait(false);
                return Store.IsInitialized;
            }

            return false;
        }

        public static bool TryPopulateEndpointFromVolumeXml(XElement volumeElement)
        {
            if (volumeElement is null)
                return false;

            XElement mapping = volumeElement.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "VolumeToEndpoint");
            if (mapping is null)
                return WebAnnotationModel.State.Endpoint != null;

            XAttribute name = mapping.Attribute("Name");
            if (name != null)
                Global.EndpointName = name.Value;

            XAttribute endpoint = mapping.Attribute("Endpoint");
            if (endpoint != null)
                WebAnnotationModel.State.Endpoint = new Uri(endpoint.Value);

            return WebAnnotationModel.State.Endpoint != null;
        }

        /// <summary>
        /// Viking (net48) needs WinHttpHandler for gRPC-over-HTTP/2. Jotunn (net10) uses
        /// HttpClientHandler. Both accept the Docker localhost cert whose root is not in
        /// the Windows trust store. TLS fails before any Bearer token is sent.
        /// </summary>
        static HttpMessageHandler CreateGrpcHttpHandler()
        {
#if NETFRAMEWORK
            return new WinHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ServerCertificateValidationCallback = AcceptLocalDevCertificate
            };
#else
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = AcceptLocalDevCertificate
            };
#endif
        }

        static bool AcceptLocalDevCertificate(
            HttpRequestMessage request,
            System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
            System.Security.Cryptography.X509Certificates.X509Chain chain,
            SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            string host = request?.RequestUri?.Host;
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1"
                || host == "::1";
        }

        sealed class TokenStoreAccessTokenProvider : IAnnotationAccessTokenProvider
        {
            public string GetAccessToken() => TokenStore.BearerToken?.AccessToken;
        }
    }
}
