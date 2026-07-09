namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Configuration options for debug logging control
    /// </summary>
    public class DebugLoggingOptions
    {
        /// <summary>
        /// Global enable/disable switch for all debug logging
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Per-category debug logging settings
        /// </summary>
        public DebugCategoryOptions Categories { get; set; } = new();
    }

    /// <summary>
    /// Per-category debug logging configuration
    /// </summary>
    public class DebugCategoryOptions
    {
        public bool Authentication { get; set; } = false;
        public bool Permissions { get; set; } = false;
        public bool Database { get; set; } = false;
        public bool Email { get; set; } = false;
        public bool Api { get; set; } = false;
        public bool Configuration { get; set; } = false;
    }
}


