namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Service interface for managing debug logging configuration and runtime overrides
    /// </summary>
    public interface IDebugLoggingService
    {
        /// <summary>
        /// Check if debug logging is enabled for a specific category
        /// </summary>
        bool IsEnabled(DebugLogCategory category);

        /// <summary>
        /// Set runtime override for a specific category
        /// </summary>
        void SetEnabled(DebugLogCategory category, bool enabled);

        /// <summary>
        /// Set global runtime override
        /// </summary>
        void SetGlobalEnabled(bool enabled);

        /// <summary>
        /// Get current debug logging options (including runtime overrides)
        /// </summary>
        DebugLoggingOptions GetOptions();

        /// <summary>
        /// Reset all runtime overrides to configuration values
        /// </summary>
        void ResetToConfiguration();
    }

    /// <summary>
    /// Debug logging categories
    /// </summary>
    public enum DebugLogCategory
    {
        Authentication,
        Permissions,
        Database,
        Email,
        Api,
        Configuration
    }
}


