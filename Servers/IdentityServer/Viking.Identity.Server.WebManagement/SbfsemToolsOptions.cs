namespace Viking.Identity.Server.WebManagement
{
    /// <summary>
    /// Configuration for SBFSEM-tools deep links from the management site / Viking bounce.
    /// </summary>
    public class SbfsemToolsOptions
    {
        /// <summary>Final /open base URL after Identity cookie auth (default https://sbfsem-tools.com/open).</summary>
        public string OpenUrl { get; set; } = Helpers.SbfsemToolsLinks.DefaultOpenBaseUrl;
    }
}
