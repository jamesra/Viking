using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
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
            return _channels.GetOrAdd(endpoint, CreateChannel);
        }

        private GrpcChannel CreateChannel(Uri endpoint)
        {
            var channelOptions = CloneOptions(_options);
            ApplyAuthCredentials(channelOptions);
            return GrpcChannel.ForAddress(endpoint, channelOptions);
        }

        private void ApplyAuthCredentials(GrpcChannelOptions channelOptions)
        {
            if (_tokenProvider == null)
                return;

            var callCredentials = CallCredentials.FromInterceptor((context, metadata) =>
            {
                var token = _tokenProvider.GetAccessToken();
                if (!string.IsNullOrEmpty(token))
                {
                    metadata.Add("Authorization", $"Bearer {token}");
                }

                return Task.CompletedTask;
            });

            var channelCredentials = channelOptions.Credentials ?? ChannelCredentials.SecureSsl;
            channelOptions.Credentials = ChannelCredentials.Create(channelCredentials, callCredentials);
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
    }
}
