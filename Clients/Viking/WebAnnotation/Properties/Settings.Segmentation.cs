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

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        public double AutoPolygonizeMinScreenAreaPercent
        {
            get => (double)this[nameof(AutoPolygonizeMinScreenAreaPercent)];
            set => this[nameof(AutoPolygonizeMinScreenAreaPercent)] = value;
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
