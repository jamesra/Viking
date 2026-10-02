using System;
using System.Drawing;
using System.Windows.Forms;
using Viking.UI.Controls;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Checkable Pen Mode and Auto Polygonize icon buttons on the viewer tool strip.
    /// Installed from <see cref="AnnotationStatusChips.Attach"/>; kept in sync with the
    /// Annotation menu and status chips via <see cref="SyncFromSettings"/>.
    /// </summary>
    internal static class AnnotationModeToolbar
    {
        private static SectionViewerControl? toolbarViewer;
        private static ToolStripButton? buttonPenMode;
        private static ToolStripButton? buttonAutoPolygonize;
        private static ToolStripLabel? labelMaskThreshold;
        private static ToolStripControlHost? hostMaskThreshold;
        private static TrackBar? trackMaskThreshold;
        private static bool syncingMaskThreshold;

        /// <summary>Slider steps per logit unit, so one tick is 0.1.</summary>
        private const int MaskThresholdTicksPerUnit = 10;

        /// <summary>
        /// Adds the two mode toggles once on the viewer's tool strip.
        /// </summary>
        public static void Ensure(SectionViewerControl viewer)
        {
            if (viewer is null)
                throw new ArgumentNullException(nameof(viewer));

            if (ReferenceEquals(toolbarViewer, viewer) &&
                buttonPenMode is { IsDisposed: false })
            {
                SyncFromSettings();
                return;
            }

            ToolStrip strip = viewer.EnsureViewerToolStrip();
            // 24 logical px scaled by the form DPI; the 16 px default reads as a speck on 150%+ displays.
            int iconPixels = Math.Max(24, (int)Math.Round(24 * strip.DeviceDpi / 96.0));
            strip.ImageScalingSize = new Size(iconPixels, iconPixels);

            buttonPenMode = CreateToggleButton(
                "Pen Mode",
                AnnotationModeToolbarIcons.PenMode(iconPixels),
                (_, _) => AnnotationMenu.OnPenMode(buttonPenMode!, EventArgs.Empty));

            buttonAutoPolygonize = CreateToggleButton(
                "Auto Polygonize",
                AnnotationModeToolbarIcons.AutoPolygonize(iconPixels),
                (_, _) => AnnotationMenu.OnAutoPolygonizeCircles(buttonAutoPolygonize!, EventArgs.Empty));

            viewer.AddViewerToolStripItem(buttonPenMode);
            viewer.AddViewerToolStripItem(buttonAutoPolygonize);
            AddMaskThresholdSlider(viewer);
            toolbarViewer = viewer;
            SyncFromSettings();
        }

        /// <summary>
        /// Adds the SAM2 mask-threshold slider after a separator. Dragging it writes the Global setting,
        /// which persists it and re-segments once the drag settles.
        /// </summary>
        private static void AddMaskThresholdSlider(SectionViewerControl viewer)
        {
            const double min = WebAnnotation.Global.AnnotationSettings.MIN_SEGMENTATION_MASK_THRESHOLD;
            const double max = WebAnnotation.Global.AnnotationSettings.MAX_SEGMENTATION_MASK_THRESHOLD;

            labelMaskThreshold = new ToolStripLabel("SAM2 threshold")
            {
                Name = "SAM2 Mask Threshold Label",
                ToolTipText = "SAM2 mask threshold. Higher values shrink every segmentation mask, lower values grow it."
            };

            trackMaskThreshold = new TrackBar
            {
                Minimum = (int)Math.Round(min * MaskThresholdTicksPerUnit),
                Maximum = (int)Math.Round(max * MaskThresholdTicksPerUnit),
                TickFrequency = MaskThresholdTicksPerUnit,
                TickStyle = TickStyle.None,
                SmallChange = 1,
                LargeChange = 5,
                AutoSize = false,
                Width = 150,
                Height = 28
            };
            trackMaskThreshold.ValueChanged += OnMaskThresholdSliderChanged;

            hostMaskThreshold = new ToolStripControlHost(trackMaskThreshold)
            {
                Name = "SAM2 Mask Threshold",
                AutoSize = false,
                Width = 150,
                ToolTipText = "SAM2 mask threshold. Higher values shrink every segmentation mask, lower values grow it."
            };

            viewer.AddViewerToolStripItem(new ToolStripSeparator());
            viewer.AddViewerToolStripItem(labelMaskThreshold);
            viewer.AddViewerToolStripItem(hostMaskThreshold);
        }

        private static void OnMaskThresholdSliderChanged(object? sender, EventArgs e)
        {
            if (syncingMaskThreshold || trackMaskThreshold is null)
                return;

            WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold =
                trackMaskThreshold.Value / (double)MaskThresholdTicksPerUnit;
            UpdateMaskThresholdLabel();
        }

        private static void UpdateMaskThresholdLabel()
        {
            if (labelMaskThreshold is { IsDisposed: false })
                labelMaskThreshold.Text =
                    $"SAM2 threshold {WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold:0.0}";
        }

        /// <summary>
        /// Moves the slider to the stored threshold, for example after the Preferences dialog changed it.
        /// Safe from any thread.
        /// </summary>
        public static void SyncMaskThreshold()
        {
            TrackBar? track = trackMaskThreshold;
            if (track is null || track.IsDisposed)
                return;

            if (track.InvokeRequired)
            {
                track.BeginInvoke(new System.Action(SyncMaskThreshold));
                return;
            }

            int ticks = (int)Math.Round(
                WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold * MaskThresholdTicksPerUnit);
            ticks = Math.Max(track.Minimum, Math.Min(track.Maximum, ticks));
            if (track.Value != ticks)
            {
                syncingMaskThreshold = true;
                try
                {
                    track.Value = ticks;
                }
                finally
                {
                    syncingMaskThreshold = false;
                }
            }

            UpdateMaskThresholdLabel();
        }

        /// <summary>
        /// Updates toolbar Checked state and tooltips from Global settings. Called from
        /// status-chip Refresh and after menu/chip toggles so all surfaces stay aligned.
        /// </summary>
        public static void SyncFromSettings()
        {
            bool penOn = Global.PenMode;
            bool autoOn = Global.AnnotationSettings.AutoPolygonizeCircles;

            if (buttonPenMode is { IsDisposed: false })
            {
                buttonPenMode.Checked = penOn;
                buttonPenMode.ToolTipText = penOn
                    ? "Pen Mode (on) — click to turn off"
                    : "Pen Mode (off) — click to treat mouse strokes like a pen";
            }

            if (buttonAutoPolygonize is { IsDisposed: false })
            {
                buttonAutoPolygonize.Checked = autoOn;
                buttonAutoPolygonize.ToolTipText = autoOn
                    ? "Auto Polygonize (on) — click to turn off idle SAM2 proposals"
                    : "Auto Polygonize (off) — click to enable idle SAM2 proposals for circles";
            }

            SyncMaskThreshold();
            AnnotationMenu.SyncModeMenuChecked(penOn, autoOn);
        }

        private static ToolStripButton CreateToggleButton(string name, Image image, EventHandler onClick)
        {
            ToolStripButton button = new()
            {
                Name = name,
                Text = name,
                Image = image,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                CheckOnClick = false,
                AutoToolTip = true
            };
            button.Click += onClick;
            return button;
        }
    }
}
