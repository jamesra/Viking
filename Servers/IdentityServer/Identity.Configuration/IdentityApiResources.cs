namespace Viking.Identity.Server
{
    /// <summary>
    /// Shared OAuth resource names used by the issuer and the Permissions Web API.
    /// </summary>
    public static class IdentityApiResources
    {
        /// <summary>
        /// ApiResource name for Permissions API introspection (HTTP basic client_id at /connect/introspect).
        /// Must match the issuer ApiResource that lists scope Viking.Annotation; must not be OAuth client id "api".
        /// </summary>
        public const string PermissionsApiResourceName = "Viking.Annotation";
    }
}
