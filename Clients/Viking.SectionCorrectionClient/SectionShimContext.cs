using System;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// Process-wide description of the correction service for the open annotation context, set once by the login
    /// flow and read by each viewer. Hosts that already have their own state holder (Viking's <c>State</c>) may ignore it.
    /// </summary>
    public static class SectionShimContext
    {
        /// <summary>Correction service URL Identity sent for the context, or null when none is assigned.</summary>
        public static string CorrectionServiceUrl { get; set; }

        /// <summary>Annotation database name Identity sent for the context; the correction set key with the StosGroup.</summary>
        public static string DatabaseName { get; set; }

        /// <summary>Source used to ask the service for offsets. Null disables the shim.</summary>
        public static ISectionShimSource Source { get; set; }

        /// <summary>Fade preferences shared by every viewer in the process.</summary>
        public static SectionShimSettings Settings { get; } = new SectionShimSettings();

        /// <summary>
        /// Raised after a viewer changes <see cref="Settings"/> (the status-bar toggle), so the host can save them.
        /// Handlers run on the caller's thread, normally the UI thread.
        /// </summary>
        public static event Action SettingsChanged;

        /// <summary>Raises <see cref="SettingsChanged"/>. Called by whoever changed <see cref="Settings"/>.</summary>
        public static void NotifySettingsChanged() => SettingsChanged?.Invoke();
    }
}
