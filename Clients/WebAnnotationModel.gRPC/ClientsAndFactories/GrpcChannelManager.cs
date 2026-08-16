using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebAnnotationModel.gRPC;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GrpcChannelManagerExtensions
    {
        public static IServiceCollection AddGrpcChannelManager(this IServiceCollection service,
            Action<GrpcChannelOptions> options = null)
        {
            if(options != null)
                service.Configure(options);

            service.AddSingleton<IGrpcChannelManager, GrpcChannelManager>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public interface IGrpcChannelManager
    {
        GrpcChannel GetOrCreate(Uri endpoint); 
    }

    public class GrpcChannelManager : IGrpcChannelManager
    {
        private readonly ConcurrentDictionary<Uri, GrpcChannel> _channels =
            new ConcurrentDictionary<Uri, GrpcChannel>();

        private readonly GrpcChannelOptions _options;
        private readonly IAnnotationAccessTokenProvider _tokenProvider;

        public GrpcChannelManager(IOptions<GrpcChannelOptions> options,
            IAnnotationAccessTokenProvider tokenProvider = null)
        {
            _options = options.Value;
            _tokenProvider = tokenProvider;
        }

        public GrpcChannel GetOrCreate(Uri endpoint)
        {
            endpoint = NormalizeEndpointForHandler(endpoint, _options.HttpHandler);
            return _channels.GetOrAdd(endpoint, CreateChannel);
        }

        private GrpcChannel CreateChannel(Uri endpoint)
        {
            var channelOptions = CloneOptions(_options);
            ApplyAuthCredentials(channelOptions, endpoint);
            return GrpcChannel.ForAddress(endpoint, channelOptions);
        }

        /// <summary>
        /// WinHttpHandler can only speak gRPC over TLS. VikingXML often keeps http://
        /// after changing the Docker port; rewrite to https and map local :5010 → :5011.
        /// </summary>
        internal static Uri NormalizeEndpointForHandler(Uri endpoint, HttpMessageHandler handler)
        {
            if (endpoint == null || !IsOrWrapsWinHttpHandler(handler) || IsHttps(endpoint))
                return endpoint;

            var builder = new UriBuilder(endpoint) { Scheme = Uri.UriSchemeHttps };
            if (IsLoopback(builder.Host) && builder.Port == 5010)
                builder.Port = 5011;

            var rewritten = builder.Uri;
            Trace.WriteLine($"[gRPC] WinHttpHandler requires TLS; using {rewritten} instead of {endpoint}");
            return rewritten;
        }

        /// <summary>
        /// Put Bearer on the HTTP request via a DelegatingHandler. Grpc.Net.Client only
        /// applies CallCredentials when Channel.IsSecure is true, so HTTPS+CallCredentials
        /// never runs for h2c, and WinHttpHandler on net48 often never copies that metadata
        /// onto the HTTP Authorization header the JWT middleware reads.
        /// </summary>
        private void ApplyAuthCredentials(GrpcChannelOptions channelOptions, Uri endpoint)
        {
            if (!IsHttps(endpoint))
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                if (channelOptions.Credentials == null)
                    channelOptions.Credentials = ChannelCredentials.Insecure;
            }

            if (_tokenProvider == null)
                return;

            var existingHandler = channelOptions.HttpHandler;
            channelOptions.HttpHandler = new AnnotationAccessTokenHandler(
                _tokenProvider,
                existingHandler ?? new HttpClientHandler(),
                ownsInnerHandler: existingHandler == null);
        }

        private static bool IsHttps(Uri endpoint)
            => string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        static bool IsLoopback(string host)
            => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || host == "127.0.0.1"
               || host == "::1";

        static bool IsOrWrapsWinHttpHandler(HttpMessageHandler handler)
        {
            while (handler != null)
            {
                if (string.Equals(handler.GetType().Name, "WinHttpHandler", StringComparison.Ordinal))
                    return true;
                handler = (handler as DelegatingHandler)?.InnerHandler;
            }

            return false;
        }

        private static GrpcChannelOptions CloneOptions(GrpcChannelOptions source)
        {
            return new GrpcChannelOptions
            {
                HttpClient = source.HttpClient,
                HttpHandler = source.HttpHandler,
                DisposeHttpClient = source.DisposeHttpClient,
                LoggerFactory = source.LoggerFactory,
                Credentials = source.Credentials,
                MaxReceiveMessageSize = source.MaxReceiveMessageSize,
                MaxSendMessageSize = source.MaxSendMessageSize,
                MaxRetryAttempts = source.MaxRetryAttempts,
                MaxRetryBufferSize = source.MaxRetryBufferSize,
                MaxRetryBufferPerCallSize = source.MaxRetryBufferPerCallSize,
                ThrowOperationCanceledOnCancellation = source.ThrowOperationCanceledOnCancellation,
                ServiceConfig = source.ServiceConfig,
                ServiceProvider = source.ServiceProvider
            };
        }

        /// <summary>
        /// Adds Authorization: Bearer from <see cref="IAnnotationAccessTokenProvider"/> on each request.
        /// Does not dispose a shared inner handler owned by <see cref="GrpcChannelOptions"/>.
        /// </summary>
        private sealed class AnnotationAccessTokenHandler : DelegatingHandler
        {
            private readonly IAnnotationAccessTokenProvider _tokenProvider;
            private readonly bool _ownsInnerHandler;

            public AnnotationAccessTokenHandler(
                IAnnotationAccessTokenProvider tokenProvider,
                HttpMessageHandler innerHandler,
                bool ownsInnerHandler)
                : base(innerHandler)
            {
                _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
                _ownsInnerHandler = ownsInnerHandler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var token = _tokenProvider.GetAccessToken();
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                else
                    Trace.WriteLine("[gRPC] No access token; request will be sent without Authorization.");

                return base.SendAsync(request, cancellationToken);
            }

            protected override void Dispose(bool disposing)
            {
                if (_ownsInnerHandler)
                    base.Dispose(disposing);
            }
        }
    }
}
