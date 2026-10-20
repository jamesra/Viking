#nullable enable

using System;
using Grpc.Core;
using Viking.UI;
using WebAnnotation.UI;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation
{
    /// <summary>
    /// Whether the segmentation server has been answering, for the "Seg" status-bar chip. Fed by the
    /// segmentation session after each server call; follows the standard <see cref="ServiceErrorTracker"/> behavior.
    /// </summary>
    /// <remarks>
    /// Call from any thread. Only an <see cref="RpcException"/> that means the server did not do its job counts as a
    /// failure; a cancelled call and the server's ordinary refusals (see <see cref="IsServiceFailure"/>) are not errors.
    /// Choosing another endpoint clears the error, because it described the previous server.
    /// </remarks>
    internal static class SegmentationServiceHealth
    {
        private static readonly ServiceErrorTracker Tracker = CreateTracker();

        /// <summary>The last server failure as readable text, or null when the server last answered or was never called.</summary>
        public static string? LastError => Tracker.LastError;

        private static ServiceErrorTracker CreateTracker()
        {
            ServiceErrorTracker tracker = new();
            tracker.Changed += AnnotationStatusChips.Refresh;
            return tracker;
        }

        /// <summary>The server answered a call; clears any error.</summary>
        public static void ReportSuccess() => Tracker.ReportSuccess();

        /// <summary>
        /// Records a failed call when <paramref name="error"/> is a server failure. Other statuses mean the server
        /// answered, so they clear any error instead.
        /// </summary>
        public static void ReportRpcError(RpcException error)
        {
            if (error is null)
                throw new ArgumentNullException(nameof(error));

            if (IsServiceFailure(error))
                Tracker.ReportFailure(Describe(error));
            else
                Tracker.ReportSuccess();
        }

        /// <summary>Forgets the error because the endpoint changed.</summary>
        public static void Reset() => Tracker.Reset();

        /// <summary>
        /// False for a cancelled call (the client gave up) and for FailedPrecondition and NotFound, which are the
        /// server's normal answers to a prompt with no matching mask and to a tile it no longer holds.
        /// </summary>
        internal static bool IsServiceFailure(RpcException error) =>
            error.StatusCode is not (StatusCode.Cancelled or StatusCode.FailedPrecondition or StatusCode.NotFound or StatusCode.OK);

        private static string Describe(RpcException error)
        {
            string? friendly = SegmentationRpcErrors.Describe(error);
            if (friendly is not null)
                return friendly;

            string detail = (error.Status.Detail ?? string.Empty).Trim();
            return detail.Length == 0 ? error.StatusCode.ToString() : $"{error.StatusCode}: {detail}";
        }
    }
}
