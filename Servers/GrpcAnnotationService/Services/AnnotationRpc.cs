using System;
using System.Linq;
using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Shared RPC helpers. Writes stamp Username from CallerName, not from the proto payload.
    /// </summary>
    internal static class AnnotationRpc
    {
        /// <summary>
        /// Error number of <c>THROW 50000</c> and of a <c>RAISERROR</c> given message text: dbo.SplitStructure,
        /// dbo.SplitStructureAtLocationLink, dbo.MergeStructures and the StructureLink reciprocal-link trigger.
        /// </summary>
        private const int RefusalErrorNumber = 50000;

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
        /// An <see cref="RpcException"/> is a status the service chose (InvalidArgument for a missing region,
        /// NotFound, ...): it is returned unchanged, status and detail intact, and logged at Debug. An
        /// <see cref="ArgumentException"/> is a request the proto conversion refused (<c>Location.EF.Conversion.ToLocation</c>:
        /// a circle without its volume circle, a location without its volume shape): it becomes
        /// <see cref="StatusCode.InvalidArgument"/> with the reason as the detail, logged at Warning, so the client can show
        /// why its save was refused instead of "Unknown". A <see cref="SqlException"/> carrying error 50000, thrown by a
        /// procedure or (inside a <see cref="DbUpdateException"/>) by a trigger, is the database refusing the request
        /// (dbo.SplitStructure: "connected to the entire keep cell"): it becomes <see cref="StatusCode.FailedPrecondition"/>
        /// with that error's message as the detail, logged at Warning. An <see cref="OperationCanceledException"/> while
        /// <paramref name="context"/>'s token is cancelled (the caller cancelled, its deadline passed, or the request was
        /// aborted) becomes
        /// <see cref="StatusCode.Cancelled"/> logged at Debug, since it is not a server fault. Anything else,
        /// including a cancellation the caller did not request (an internal timeout or a library bug),
        /// becomes <see cref="StatusCode.Unknown"/> logged at Error so the fault is not hidden.
        /// Called from the catch blocks of every annotation service; <paramref name="operation"/> is the
        /// RPC name and doubles as the status detail clients see for the Cancelled and Unknown cases.
        /// </summary>
        public static RpcException Failure(ILogger logger, string operation, Exception e, ServerCallContext context)
        {
            if (e is RpcException refusal)
            {
                logger.LogDebug("{Operation} refused: {Status}", operation, refusal.Status);
                return refusal;
            }

            if (e is ArgumentException refusedRequest)
            {
                logger.LogWarning("{Operation} refused: {Reason}", operation, refusedRequest.Message);
                return new RpcException(new Status(StatusCode.InvalidArgument, refusedRequest.Message, e));
            }

            if (DatabaseRefusal(e) is { } refusedByDatabase)
            {
                logger.LogWarning("{Operation} refused by the database: {Reason}", operation, refusedByDatabase.Message);
                return new RpcException(new Status(StatusCode.FailedPrecondition, refusedByDatabase.Message, e));
            }

            if (e is OperationCanceledException && context.CancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("{Operation} cancelled", operation);
                return new RpcException(new Status(StatusCode.Cancelled, operation, e));
            }

            logger.LogError(e, "{Operation} failed", operation);
            return new RpcException(new Status(StatusCode.Unknown, operation, e));
        }

        /// <summary>
        /// The error a <c>THROW 50000</c> or a message-text <c>RAISERROR</c> raised in <paramref name="e"/>, thrown
        /// directly (a stored procedure call) or inside the <see cref="DbUpdateException"/> of a SaveChanges (a trigger);
        /// null for any other exception. The annotation database raises 50000 only to refuse a request.
        /// </summary>
        private static SqlError DatabaseRefusal(Exception e)
        {
            var sql = e as SqlException ?? (e as DbUpdateException)?.InnerException as SqlException;
            return sql?.Errors.Cast<SqlError>().FirstOrDefault(error => error.Number == RefusalErrorNumber);
        }
    }
}
