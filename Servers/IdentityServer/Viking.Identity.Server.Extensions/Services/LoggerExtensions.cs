using System;
using Microsoft.Extensions.Logging;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Extension methods for conditional debug logging based on debug logging service configuration
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Log debug message if the specified category is enabled
        /// </summary>
        public static void LogDebugIfEnabled(this ILogger logger, IDebugLoggingService debugLoggingService, DebugLogCategory category, string message, params object[] args)
        {
            if (debugLoggingService.IsEnabled(category))
            {
                logger.LogDebug(message, args);
            }
        }

        /// <summary>
        /// Log debug message with exception if the specified category is enabled
        /// </summary>
        public static void LogDebugIfEnabled(this ILogger logger, IDebugLoggingService debugLoggingService, DebugLogCategory category, Exception exception, string message, params object[] args)
        {
            if (debugLoggingService.IsEnabled(category))
            {
                logger.LogDebug(exception, message, args);
            }
        }
    }
}


