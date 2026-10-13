#nullable enable

using System;
using System.Drawing;

namespace Viking.UI
{
    /// <summary>
    /// The standard error state of a remote service the viewer's status bar reports on (corrections, segmentation).
    /// A service records each request outcome here; the status-bar item for the service shows
    /// <see cref="ErrorText"/> in <see cref="ErrorColor"/> with <see cref="ToolTip"/> while <see cref="LastError"/> is set.
    /// </summary>
    /// <remarks>
    /// Only failures that mean the service could not do its job belong here: it could not be reached, timed out, or
    /// answered with a server fault. A refusal that is an ordinary result of the request (for example "no mask fits
    /// the prompt") and a cancelled request are successes of the connection, not errors. An error stays until the
    /// next request succeeds, so the status bar never claims a service is healthy on the strength of an old success.
    /// Thread-safe. <see cref="Changed"/> runs on the thread that reported, outside the lock, so a UI host must marshal.
    /// </remarks>
    public sealed class ServiceErrorTracker
    {
        /// <summary>The word the status-bar item shows while the service is in error, whatever the service's own label.</summary>
        public const string ErrorText = "Error";

        /// <summary>Foreground color of a status-bar item whose service is in error.</summary>
        public static readonly Color ErrorColor = Color.FromArgb(200, 0, 0);

        private readonly object _lock = new();
        private string? _lastError;

        /// <summary>
        /// The message of the most recent failed request, or null when the last request succeeded or none has been made.
        /// </summary>
        public string? LastError
        {
            get
            {
                lock (_lock)
                    return _lastError;
            }
        }

        /// <summary>Raised when <see cref="LastError"/> changes (set, cleared, or a different message).</summary>
        public event Action? Changed;

        /// <summary>Records a failed request. Blank messages are replaced with a generic one so the error cannot be hidden.</summary>
        /// <param name="message">What went wrong, as a user can read it.</param>
        public void ReportFailure(string? message) =>
            Set(string.IsNullOrWhiteSpace(message) ? "the request failed" : message!.Trim());

        /// <summary>Records a request the service answered, which clears any error.</summary>
        public void ReportSuccess() => Set(null);

        /// <summary>Forgets any error without implying a request succeeded, for example when another endpoint is chosen.</summary>
        public void Reset() => Set(null);

        /// <summary>
        /// The standard tooltip for a status-bar item in error.
        /// </summary>
        /// <param name="serviceName">Plain name of the service, such as "The correction service".</param>
        /// <param name="error">The tracker's <see cref="LastError"/>.</param>
        public static string ToolTip(string serviceName, string error) =>
            $"{serviceName} is not working: {error}\nViking tries again with the next request.";

        private void Set(string? message)
        {
            lock (_lock)
            {
                if (_lastError == message)
                    return;
                _lastError = message;
            }

            Changed?.Invoke();
        }
    }
}
