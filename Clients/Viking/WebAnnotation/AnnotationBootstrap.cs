using System;
using System.Linq;
using System.Net;
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
#if NETFRAMEWORK
                    channelOpts.HttpHandler = CreateWinHttpHandler();
#else
                    _ = channelOpts;
#endif
                });

            ServiceProvider provider = services.BuildServiceProvider();
            if (provider.GetService<IOptions<GrpcRepositorySettings>>() is IOptions<GrpcRepositorySettings> grpcSettings)
                grpcSettings.Value.Endpoint = endpoint;

            if (provider.GetService<IAnnotationStores>() is IAnnotationStores stores)
                Store.Initialize(stores);

            return true;
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

#if NETFRAMEWORK
        static System.Net.Http.WinHttpHandler CreateWinHttpHandler()
        {
            return new System.Net.Http.WinHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ServerCertificateValidationCallback = (request, certificate, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;

                    string host = request?.RequestUri?.Host;
                    return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || host == "127.0.0.1"
                        || host == "::1";
                }
            };
        }
#endif

        sealed class TokenStoreAccessTokenProvider : IAnnotationAccessTokenProvider
        {
            public string GetAccessToken() => TokenStore.BearerToken?.AccessToken;
        }
    }
}
