using System;
using System.Diagnostics;
using System.Net.Http;
using Grpc.Core;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Annotation loads that failed on the server. The stores report a failed section or region load here instead of only
    /// tracing it, so the viewer can say why a section shows no annotations (a server error, an unreachable server, a
    /// timeout) rather than draw an empty view. The viewer subscribes to <see cref="Failed"/>.
    /// Port of the WCF client's ServerLoadErrors (Viking-Net4.8 0be72bcc), reading the gRPC status and detail.
    /// </summary>
    public static class ServerLoadErrors
    {
        /// <summary>The same message is raised at most once in this period, so a failing server does not flood the viewer.</summary>
        public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(15);

        private const int MaxDetailLength = 240;

        private static readonly object gate = new object();
        private static string lastMessage;
        private static DateTime lastRaisedUtc = DateTime.MinValue;

        /// <summary>For tests: the clock the repeat interval is measured with.</summary>
        internal static Func<DateTime> UtcNow = () => DateTime.UtcNow;

        /// <summary>
        /// Raised with a one-line message when a load fails, on the thread that saw the failure. Handlers marshal to the UI
        /// themselves.
        /// </summary>
        public static event Action<string> Failed;

        /// <summary>
        /// Reports that loading <paramref name="what"/> (e.g. "annotations") for <paramref name="sectionNumber"/> failed.
        /// A cancelled request is not a failure and is ignored.
        /// </summary>
        public static void Report(string what, long sectionNumber, Exception e)
        {
            if (e is null || IsCancellation(e))
                return;

            Trace.WriteLine($"Loading {what} for section {sectionNumber} failed: {e}", "WebAnnotation");

            string message = $"Section {sectionNumber}: {what} did not load. {Describe(e)}";
            lock (gate)
            {
                DateTime now = UtcNow();
                if (string.Equals(message, lastMessage, StringComparison.Ordinal) && now - lastRaisedUtc < RepeatInterval)
                    return;
                lastMessage = message;
                lastRaisedUtc = now;
            }

            Failed?.Invoke(message);
        }

        /// <summary>
        /// One line saying why a request failed, with the server's own detail when it sent one (the gRPC status detail).
        /// </summary>
        public static string Describe(Exception e)
        {
            e = Unwrap(e);
            string text;
            if (e is RpcException r)
            {
                switch (r.StatusCode)
                {
                    case StatusCode.DeadlineExceeded:
                        text = "The server did not answer in time.";
                        break;
                    case StatusCode.Unavailable:
                        text = "The annotation server could not be reached" + Detail(r.Status.Detail, ": ");
                        break;
                    case StatusCode.PermissionDenied:
                    case StatusCode.Unauthenticated:
                        text = "The server denied access" + Detail(r.Status.Detail, ": ");
                        break;
                    default:
                        text = "Server error" + Detail(string.IsNullOrWhiteSpace(r.Status.Detail) ? r.StatusCode.ToString() : r.Status.Detail, ": ");
                        break;
                }
            }
            else if (e is TimeoutException)
                text = "The server did not answer in time.";
            else if (e is HttpRequestException h)
                text = "The annotation server could not be reached" + Detail(h.Message, ": ");
            else
            {
                string m = Detail(e.Message, "");
                text = m.Length > 0 ? m : e.GetType().Name;
            }

            return Truncate(OneLine(text));
        }

        private static Exception Unwrap(Exception e) =>
            e is AggregateException a && a.InnerExceptions.Count == 1 ? Unwrap(a.InnerExceptions[0]) : e;

        private static bool IsCancellation(Exception e)
        {
            e = Unwrap(e);
            return e is OperationCanceledException || e is ObjectDisposedException
                || (e is RpcException r && r.StatusCode == StatusCode.Cancelled);
        }

        private static string Detail(string text, string prefix)
        {
            if (string.IsNullOrWhiteSpace(text))
                return prefix.Length > 0 ? "." : "";
            string trimmed = text.Trim();
            return prefix + trimmed + (trimmed.EndsWith(".", StringComparison.Ordinal) ? "" : ".");
        }

        private static string OneLine(string text) =>
            string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Replace("  ", " ");

        private static string Truncate(string text) =>
            text.Length <= MaxDetailLength ? text : text.Substring(0, MaxDetailLength - 1).TrimEnd() + "…";

        /// <summary>For tests: forget the last message so the next report is raised.</summary>
        internal static void ResetForTests()
        {
            lock (gate)
            {
                lastMessage = null;
                lastRaisedUtc = DateTime.MinValue;
            }
        }
    }
}
