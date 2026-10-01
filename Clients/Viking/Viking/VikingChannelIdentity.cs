namespace Viking
{
    /// <summary>
    /// Compile-time channel identity for production vs unsigned Viking Test builds.
    /// Set by MSBuild <c>VikingTestChannel=true</c> → <c>VIKING_TEST_CHANNEL</c>.
    /// </summary>
    public static class VikingChannelIdentity
    {
#if VIKING_TEST_CHANNEL
        public const bool IsTestChannel = true;
        public const string PackId = "VikingTest";
        public const string ProductDisplayName = "Viking Test";
        public const string ProtocolScheme = "viking-test";
        public const string UpdateUrl = "https://websvc.codepharm.net/Software/VikingTest";
#else
        public const bool IsTestChannel = false;
        public const string PackId = "Viking";
        public const string ProductDisplayName = "Viking";
        public const string ProtocolScheme = "viking";
        public const string UpdateUrl = "https://websvc.codepharm.net/Software/Viking";
#endif

        // Same pipe names as production so a pre-opened Viking Test receives viking://
        // forwards from SBFSEM tools (OS still launches the signed app for cold starts;
        // that process hands off via these pipes when Test already owns the volume).
        public const string PipePrefix = "VikingLegacy.Activation.";
        public const string PipeNamePrefix = "VikingLegacy.Activation.Name.";

        public static string ProtocolPrefix => ProtocolScheme + ":";
        public static string OpenUrlBase => ProtocolScheme + "://open";
    }
}
