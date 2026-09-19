namespace WebAnnotation.Properties
{
    internal sealed partial class Settings
    {
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.03")]
        public double SegmentationHoleDropFraction
        {
            get => (double)this[nameof(SegmentationHoleDropFraction)];
            set => this[nameof(SegmentationHoleDropFraction)] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public int SegmentationEdgeCleanupRadius
        {
            get => (int)this[nameof(SegmentationEdgeCleanupRadius)];
            set => this[nameof(SegmentationEdgeCleanupRadius)] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeCircles
        {
            get => (bool)this[nameof(AutoPolygonizeCircles)];
            set => this[nameof(AutoPolygonizeCircles)] = value;
        }

        /// <summary>
        /// True after the user toggles Auto Polygonize Circles. Until then the role default applies
        /// (review/admin on, annotate off) and this flag stays false across restarts.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeCirclesUserSet
        {
            get => (bool)this[nameof(AutoPolygonizeCirclesUserSet)];
            set => this[nameof(AutoPolygonizeCirclesUserSet)] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        public double AutoPolygonizeMinScreenAreaPercent
        {
            get => (double)this[nameof(AutoPolygonizeMinScreenAreaPercent)];
            set => this[nameof(AutoPolygonizeMinScreenAreaPercent)] = value;
        }

        /// <summary>Minimum on-screen circle radius, in device pixels, for auto-polygonize.</summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("8")]
        public double AutoPolygonizeMinRadiusPixels
        {
            get => (double)this[nameof(AutoPolygonizeMinRadiusPixels)];
            set => this[nameof(AutoPolygonizeMinRadiusPixels)] = value;
        }

        /// <summary>Minimum circle radius, in nanometers, for auto-polygonize. Default 75.</summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("75")]
        public double AutoPolygonizeMinRadiusNanometers
        {
            get => (double)this[nameof(AutoPolygonizeMinRadiusNanometers)];
            set => this[nameof(AutoPolygonizeMinRadiusNanometers)] = value;
        }

        /// <summary>Debug overlay of raw SAM2 masks on auto-polygonize proposals. Default off.</summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeOverlayMasks
        {
            get => (bool)this[nameof(AutoPolygonizeOverlayMasks)];
            set => this[nameof(AutoPolygonizeOverlayMasks)] = value;
        }
    }
}
