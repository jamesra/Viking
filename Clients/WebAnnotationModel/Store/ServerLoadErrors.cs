#nullable enable
using System;
using System.Diagnostics;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace WebAnnotationModel
{
    /// <summary>
    /// Annotation loads that failed on the server. The stores report a failed section or region load here instead of only
    /// tracing it, so the viewer can say why a section shows no annotations (a server fault, an unreachable server, a
    /// timeout) rather than draw an empty view. The viewer subscribes to <see cref="Failed"/>.
    /// </summary>
    public static class ServerLoadErrors
    {
        /// <summary>The same message is raised at most once in this period, so a failing server does not flood the viewer.</summary>
        public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(15);

        private const int MaxDetailLength = 240;

        private static readonly object gate = new();
        private static string? lastMessage;
        private static DateTime lastRaisedUtc = DateTime.MinValue;

        /// <summary>For tests: the clock the repeat interval is measured with.</summary>
        internal static Func<DateTime> UtcNow = () => DateTime.UtcNow;

        /// <summary>
        /// Raised with a one-line message when a load fails, on the thread that saw the failure. Handlers marshal to the UI
        /// themselves.
        /// </summary>
        public static event Action<string>? Failed;

        /// <summary>
        /// Reports that loading <paramref name="what"/> (e.g. "annotations") for <paramref name="sectionNumber"/> failed.
        /// A cancelled or aborted request is not a failure and is ignored.
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
        /// One line saying why a request failed, with the server's own detail when it sent one: the fault message, or the
        /// HTTP status of the response.
        /// </summary>
        public static string Describe(Exception e)
        {
            string text = e switch
            {
                TimeoutException => "The server did not answer in time.",
                EndpointNotFoundException => "The annotation server could not be reached.",
                SecurityAccessDeniedException => "The server denied access" + Detail(e.Message, ": "),
                FaultException<ExceptionDetail> f => "Server error" + Detail(f.Detail?.Message ?? f.Message, ": "),
                FaultException f => "Server error" + Detail(f.Message, ": "),
                _ => HttpStatus(e) is { } status ? status : Detail(e.Message, "") is { Length: > 0 } m ? m : e.GetType().Name,
            };
            return Truncate(OneLine(text));
        }

        private static bool IsCancellation(Exception e) =>
            e is OperationCanceledException or CommunicationObjectAbortedException or ObjectDisposedException;

        /// <summary>The HTTP status of the first <see cref="WebException"/> with a response in the chain, as "HTTP 500 Internal Server Error".</summary>
        private static string? HttpStatus(Exception e)
        {
            for (Exception? inner = e; inner is not null; inner = inner.InnerException)
            {
                if (inner is WebException { Response: HttpWebResponse response })
                    return $"HTTP {(int)response.StatusCode} {response.StatusDescription}".TrimEnd() + ".";
            }

            return null;
        }

        private static string Detail(string? text, string prefix)
        {
            if (string.IsNullOrWhiteSpace(text))
                return prefix.Length > 0 ? "." : "";
            string trimmed = text!.Trim();
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
