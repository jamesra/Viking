using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// One line per unary call: method, volume when the request has volume_name, and the caller.
    /// Caller is the authenticated name when present, otherwise the peer address.
    /// </summary>
    public sealed class CorrectionRequestLoggingInterceptor : Interceptor
    {
        readonly ILogger<CorrectionRequestLoggingInterceptor> _logger;

        public CorrectionRequestLoggingInterceptor(ILogger<CorrectionRequestLoggingInterceptor> logger)
        {
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            string volume = VolumeName(request);
            string caller = Caller(context);
            if (string.IsNullOrWhiteSpace(volume))
                _logger.LogInformation("{Method} from {Caller}", context.Method, caller);
            else
                _logger.LogInformation("{Method} volume {Volume} from {Caller}", context.Method, volume, caller);

            return await continuation(request, context).ConfigureAwait(false);
        }

        static string VolumeName<TRequest>(TRequest request)
        {
            if (request is not Google.Protobuf.IMessage message)
                return "";

            Google.Protobuf.Reflection.FieldDescriptor field = message.Descriptor.FindFieldByName("volume_name");
            return field?.Accessor.GetValue(message) as string ?? "";
        }

        static string Caller(ServerCallContext context)
        {
            HttpContext http = context.GetHttpContext();
            string name = http?.User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            return string.IsNullOrWhiteSpace(context.Peer) ? "" : context.Peer;
        }
    }
}
