namespace Viking.Identity.Server
{
    /// <summary>
    /// Cookie and Data Protection names shared by Standalone (:5001) and WebManagement (:4001)
    /// so an Identity login on the management site is readable by the OIDC issuer.
    /// </summary>
    public static class SharedIdentityAuthentication
    {
        public const string DataProtectionApplicationName = "VikingIdentityServer";

        public const string CookieName = ".AspNet.SharedVikingIdentity";

        public const string CookiePath = "/";
    }
}
