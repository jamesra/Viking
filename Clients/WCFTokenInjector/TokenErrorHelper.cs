using System;
using System.Net.Http;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;

namespace Viking.Tokens
{
    /// <summary>
    /// Builds user-facing messages from token endpoint errors and exceptions so login/token failures are easier to diagnose.
    /// </summary>
    public static class TokenErrorHelper
    {
        private const string TimeoutNetworkMessage = "The identity server did not respond in time. Please check your network and try again.";

        /// <summary>
        /// Converts a token endpoint protocol response into a single user-facing message.
        /// Uses the server's error_description when present, otherwise maps known error codes or returns Error as-is.
        /// </summary>
        /// <returns>User-facing message; never null.</returns>
        public static string ToUserMessage(ProtocolResponse response)
        {
            if (response is null)
                return "No response from server.";

            var description = response.TryGet("error_description");
            if (!string.IsNullOrWhiteSpace(description))
                return description.Trim();

            var error = response.Error;
            if (string.IsNullOrWhiteSpace(error))
                return "Unknown error.";

            return error.Trim().ToLowerInvariant() switch
            {
                "invalid_grant" => "Incorrect username or password, or the account may be locked or disabled.",
                "invalid_client" => "Authentication configuration error. Please contact support.",
                "invalid_request" => "Invalid login request. Please try again.",
                "server_error" => "The identity server is temporarily unavailable. Please try again later.",
                _ => error
            };
        }

        /// <summary>
        /// Converts an exception from token/login calls into a user-facing message.
        /// Timeouts and connection failures share one friendly message. HTTP status failures
        /// (for example 404) keep the server status so they are not reported as a timeout.
        /// </summary>
        /// <returns>User-facing message; never null.</returns>
        public static string ToExceptionMessage(Exception ex)
        {
            if (ex is null)
                return "An unexpected error occurred.";

            if (ex is TaskCanceledException)
                return TimeoutNetworkMessage;

            if (ex is HttpRequestException)
                return IsConnectivityFailure(ex.Message) ? TimeoutNetworkMessage : (ex.Message ?? TimeoutNetworkMessage);

            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg is null)
                return ex.Message ?? "An unexpected error occurred.";

            if (IsConnectivityFailure(msg))
                return TimeoutNetworkMessage;

            return ex.Message;
        }

        private static bool IsConnectivityFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return true;

            var lower = message.ToLowerInvariant();
            return lower.Contains("timeout") || lower.Contains("canceled") || lower.Contains("cancelled") ||
                   lower.Contains("connection") || lower.Contains("refused") || lower.Contains("unable to connect");
        }
    }
}
