namespace Viking.Identity.Server
{
    /// <summary>OAuth client ids issued by Identity Server.</summary>
    public static class VikingOAuthClients
    {
        /// <summary>Confidential backend for sbfsem-tools.com. The only client allowed to mint desktop launch codes from a bearer token.</summary>
        public const string SbfsemTools = "sbfsem-tools";
    }
}
