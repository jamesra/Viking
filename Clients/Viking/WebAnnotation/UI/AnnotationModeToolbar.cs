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
            toolbarViewer = viewer;
            SyncFromSettings();
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
