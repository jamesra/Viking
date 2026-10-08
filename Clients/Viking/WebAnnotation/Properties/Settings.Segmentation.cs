using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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
        [global::System.Configuration.DefaultSettingValueAttribute("8")]
        public int SegmentationEdgeCleanupRadius
        {
            get => (int)this[nameof(SegmentationEdgeCleanupRadius)];
            set => this[nameof(SegmentationEdgeCleanupRadius)] = value;
        }

        /// <summary>
        /// SAM2 logit a pixel must exceed to be object; sent as SegmentTilesRequest.MaskThreshold.
        /// SAM2's own default is 0; Viking starts at 0.5 to trim the slight over-segmentation.
        /// Keep in step with <see cref="WebAnnotation.Global.AnnotationSettings.DEFAULT_SEGMENTATION_MASK_THRESHOLD"/>.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.5")]
        public double SegmentationMaskThreshold
        {
            get => (double)this[nameof(SegmentationMaskThreshold)];
            set => this[nameof(SegmentationMaskThreshold)] = value;
        }

        /// <summary>
        /// Sent as SegmentTilesRequest.UseMaskInput: the server runs a second SAM2 pass seeded with the first pass's logits.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SegmentationUseMaskInput
        {
            get => (bool)this[nameof(SegmentationUseMaskInput)];
            set => this[nameof(SegmentationUseMaskInput)] = value;
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

        /// <summary>Minimum circle radius, in nanometers, for auto-polygonize. Default 75.</summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("75")]
        public double AutoPolygonizeMinRadiusNanometers
        {
            get => (double)this[nameof(AutoPolygonizeMinRadiusNanometers)];
            set => this[nameof(AutoPolygonizeMinRadiusNanometers)] = value;
        }

        /// <summary>
        /// Coarsest camera downsample at which auto-segment still runs. A coarser view is skipped.
        /// Default 2, so downsample 2 runs and DS 3 does not. The preference allows 1 to 4;
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/> clamps a saved value above 4.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public double AutoPolygonizeMaxDownsample
        {
            get => (double)this[nameof(AutoPolygonizeMaxDownsample)];
            set => this[nameof(AutoPolygonizeMaxDownsample)] = value;
        }

        /// <summary>
        /// Stored mask-overlay choice. Debug builds ignore this until
        /// <see cref="AutoPolygonizeOverlayMasksUserSet"/> is true.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeOverlayMasks
        {
            get => (bool)this[nameof(AutoPolygonizeOverlayMasks)];
            set => this[nameof(AutoPolygonizeOverlayMasks)] = value;
        }

        /// <summary>
        /// True after the user toggles Overlay returned masks. Until then debug builds
        /// show masks and release builds do not, and this flag stays false across restarts.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeOverlayMasksUserSet
        {
            get => (bool)this[nameof(AutoPolygonizeOverlayMasksUserSet)];
            set => this[nameof(AutoPolygonizeOverlayMasksUserSet)] = value;
        }

        /// <summary>
        /// Stored prompt-dot choice. Debug builds ignore this until
        /// <see cref="AutoPolygonizeOverlayPromptsUserSet"/> is true.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeOverlayPrompts
        {
            get => (bool)this[nameof(AutoPolygonizeOverlayPrompts)];
            set => this[nameof(AutoPolygonizeOverlayPrompts)] = value;
        }

        /// <summary>
        /// True after the user toggles foreground/background prompt dots. Until then debug builds
        /// show the dots and release builds do not, and this flag stays false across restarts.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeOverlayPromptsUserSet
        {
            get => (bool)this[nameof(AutoPolygonizeOverlayPromptsUserSet)];
            set => this[nameof(AutoPolygonizeOverlayPromptsUserSet)] = value;
        }

        /// <summary>
        /// When true, auto-polygonize outlines are omitted while the mask overlay is visible.
        /// Ignored while masks are off, which always draws the rings.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoPolygonizeHideSegmentationRings
        {
            get => (bool)this[nameof(AutoPolygonizeHideSegmentationRings)];
            set => this[nameof(AutoPolygonizeHideSegmentationRings)] = value;
        }

        /// <summary>
        /// Copies this settings section from the previous Viking.exe version once.
        /// Viking's startup upgrade only migrates Viking.Properties.Settings, so a version bump
        /// otherwise drops annotation preferences, including the mask overlay.
        /// A same-version user.config that already has this section is left alone.
        /// Called from <see cref="WebAnnotation.Global.AnnotationSettings"/> before any setting is read.
        /// </summary>
        internal static void UpgradeFromPreviousVersionIfNeeded()
        {
            try
            {
                if (CurrentVersionHasSavedSettings())
                    return;

                Default.Upgrade();
                Default.Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WebAnnotation] Settings upgrade failed: {ex.Message}");
            }
        }

        /// <summary>
        /// True when this Viking version's user.config already contains the WebAnnotation section.
        /// The section is not declared on Viking.exe, so this reads the file instead of ConfigurationManager.
        /// A new version folder has no file, which is the only time <see cref="ApplicationSettingsBase.Upgrade"/> should run.
        /// </summary>
        private static bool CurrentVersionHasSavedSettings()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            if (string.IsNullOrEmpty(config.FilePath) || !File.Exists(config.FilePath))
                return false;

            XDocument document = XDocument.Load(config.FilePath);
            string sectionName = typeof(Settings).FullName;
            return document.Descendants().Any(element => element.Name.LocalName == sectionName);
        }
    }
}
