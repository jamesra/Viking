using System;
using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Shared RPC helpers. Writes stamp Username from CallerName, not from the proto payload.
    /// </summary>
    internal static class AnnotationRpc
    {
        /// <summary>
        /// Username stamped on annotation writes. JWT NameClaimType is "name"; tokens that
        /// only carry preferred_username or sub would otherwise be recorded as "unknown".
        /// </summary>
        public static string CallerName(ServerCallContext context)
        {
            var user = context.GetHttpContext()?.User;

            var name = user?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var preferred = user?.FindFirst("preferred_username")?.Value;
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;

            var sub = user?.FindFirst("sub")?.Value
                      ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(sub))
                return sub;

            return "unknown";
        }

        /// <summary>
        /// Converts an exception caught by an RPC's catch-all into the <see cref="RpcException"/> to throw.
        /// An <see cref="OperationCanceledException"/> (the caller cancelled, its deadline passed, or the
        /// request was aborted) becomes <see cref="StatusCode.Cancelled"/> logged at Debug, since it is not a
        /// server fault; anything else becomes <see cref="StatusCode.Unknown"/> logged at Error.
        /// Called from the catch blocks of every annotation service; <paramref name="operation"/> is the
        /// RPC name and doubles as the status detail clients see.
        /// </summary>
        public static RpcException Failure(ILogger logger, string operation, Exception e)
        {
            if (e is OperationCanceledException)
            {
                logger.LogDebug("{Operation} cancelled", operation);
                return new RpcException(new Status(StatusCode.Cancelled, operation, e));
            }

            logger.LogError(e, "{Operation} failed", operation);
            return new RpcException(new Status(StatusCode.Unknown, operation, e));
        }
    }
}
