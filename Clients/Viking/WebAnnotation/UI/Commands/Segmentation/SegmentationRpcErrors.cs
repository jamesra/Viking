using Grpc.Core;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Turns the segmentation server's refusal statuses into text a user can act on. Without this the
    /// status bar shows <c>Status(StatusCode="DeadlineExceeded", Detail="...")</c>.
    /// </summary>
    internal static class SegmentationRpcErrors
    {
        /// <summary>
        /// A readable reason for <see cref="StatusCode.DeadlineExceeded"/> and <see cref="StatusCode.ResourceExhausted"/>,
        /// or null for any other status so the caller keeps its generic handling.
        /// </summary>
        /// <remarks>
        /// DeadlineExceeded means the server gave up waiting for tiles it asked for, or the call outlived the
        /// client's own limit. ResourceExhausted means the request went over a server limit, the server is
        /// working on its maximum number of calls, or its image cache is full of tiles other calls are still
        /// using. In every case the request itself was fine and trying again shortly can succeed. The server's
        /// own detail is appended because it names the limit that was hit.
        /// </remarks>
        public static string? Describe(RpcException error)
        {
            string detail = (error.Status.Detail ?? string.Empty).Trim();
            string suffix = detail.Length == 0 ? string.Empty : $" ({detail})";
            return error.StatusCode switch
            {
                StatusCode.DeadlineExceeded =>
                    "the segmentation server timed out waiting for image tiles; try again" + suffix,
                StatusCode.ResourceExhausted =>
                    "the segmentation server is busy or the request was over a limit; try again in a moment" + suffix,
                _ => null
            };
        }
    }
}
