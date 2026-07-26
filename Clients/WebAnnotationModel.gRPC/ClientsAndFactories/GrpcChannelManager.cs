using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using Grpc.Core;
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

        public GrpcChannelManager(IOptions<GrpcChannelOptions> options)
        {
            _options = options.Value;
        }

        public GrpcChannel GetOrCreate(Uri endpoint)
        {
            //_options.Credentials = ChannelCredentials.SecureSsl;


            return _channels.GetOrAdd(endpoint, (x) => GrpcChannel.ForAddress(endpoint, _options));
        }

    }
}