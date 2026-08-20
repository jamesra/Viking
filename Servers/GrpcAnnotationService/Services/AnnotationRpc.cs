using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

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
    }
}
