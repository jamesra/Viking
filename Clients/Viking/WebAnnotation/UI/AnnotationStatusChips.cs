using System;
using System.Drawing;
using Viking.UI.Controls;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Pushes Pen Mode, Auto Polygonize, and Segmentation ambient state onto the viewer
    /// status-bar chips. Attached once from <see cref="AnnotationOverlay.SetParent"/>;
    /// refresh after mode toggles, endpoint changes, and busy transitions.
    /// </summary>
    internal static class AnnotationStatusChips
    {
        private static SectionViewerControl? attachedViewer;
        private static bool clickHooked;

        private static readonly Color ColorOn = Color.FromArgb(0, 110, 60);
        private static readonly Color ColorOff = Color.Gray;
        private static readonly Color ColorBusy = Color.FromArgb(180, 100, 0);
        private static readonly Color ColorUnavailable = Color.FromArgb(140, 40, 40);

        /// <summary>
        /// Subscribes click toggles on the viewer and paints the initial chip state.
        /// Safe to call again when the same viewer is re-parented.
        /// </summary>
        public static void Attach(SectionViewerControl viewer)
        {
            if (viewer is null)
                throw new ArgumentNullException(nameof(viewer));

            if (!ReferenceEquals(attachedViewer, viewer))
            {
                if (attachedViewer is not null && clickHooked)
                    attachedViewer.ViewerStatusChipClicked -= OnChipClicked;

                attachedViewer = viewer;
                attachedViewer.ViewerStatusChipClicked += OnChipClicked;
                clickHooked = true;
            }

            AnnotationModeToolbar.Ensure(viewer);
            Refresh();
        }

        /// <summary>
        /// Recomputes chip text/colors from Global settings and current busy state.
        /// No-op before <see cref="Attach"/> or after the viewer is disposed.
        /// Safe from any thread: callers include upload/segment continuations and the idle
        /// batch, but the chips, tool strip buttons, and menu checkmarks are UI-thread objects,
        /// so off-thread calls are posted to the viewer.
        /// </summary>
        public static void Refresh()
        {
            SectionViewerControl? viewer = attachedViewer;
            if (viewer is null || viewer.IsDisposed)
            {
                // Menu checkmarks must still follow the setting when no viewer chrome exists yet.
                AnnotationModeToolbar.SyncFromSettings();
                return;
            }

            if (viewer.InvokeRequired)
            {
                try
                {
                    viewer.BeginInvoke(new Action(Refresh));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                    // Handle not created yet or already destroyed; the next UI-thread refresh repaints.
                }

                return;
            }

            var pen = DescribePen(Global.PenMode);
            viewer.SetViewerStatusChip(
                ViewerStatusChipSlot.PenMode,
                pen.Text,
                ChipColor(pen.EmphasizeOn, pen.EmphasizeBusy, pen.EmphasizeUnavailable),
                pen.EmphasizeOn
                    ? "Pen Mode is on. Click to turn off (toolbar or Annotation menu)."
                    : "Pen Mode is off. Click to turn on (toolbar or Annotation menu).");

            bool autoOn = Global.AnnotationSettings.AutoPolygonizeCircles;
            bool autoBusy = AnnotationOverlay.CurrentOverlay?.IsAutoPolygonizeBusy == true;
            var auto = DescribeAutoPolygonize(autoOn, autoBusy);
            string autoTip = !autoOn
                ? "Idle auto-polygonize is off. Click to enable."
                : autoBusy
                    ? "Auto-polygonize is segmenting circles in view."
                    : Global.IsSegmentationServiceAvailable
                        ? "Idle auto-polygonize is on. Click to disable."
                        : "Auto-polygonize is on, but no segmentation service is selected.";
            viewer.SetViewerStatusChip(
                ViewerStatusChipSlot.AutoPolygonize,
                auto.Text,
                ChipColor(auto.EmphasizeOn, auto.EmphasizeBusy, unavailable: false),
                autoTip);

            bool segAvailable = Global.IsSegmentationServiceAvailable;
            bool segBusy = IsSegmentationBusy(viewer);
            bool segProcessing = viewer.CurrentCommand is SegmentationCommand processingCommand &&
                processingCommand.IsProcessingResults;
            var seg = DescribeSegmentation(segAvailable, segBusy, segProcessing);
            string segTip = !segAvailable
                ? "No segmentation service selected. Click to choose one."
                : segBusy
                    ? "Segmentation upload or request in progress."
                    : segProcessing
                        ? "Segmentation answered; converting the mask into an outline."
                        : "Segmentation service is available. Click to change or clear.";
            viewer.SetViewerStatusChip(
                ViewerStatusChipSlot.Segmentation,
                seg.Text,
                ChipColor(seg.EmphasizeOn, seg.EmphasizeBusy, seg.EmphasizeUnavailable),
                segTip);

            AnnotationModeToolbar.SyncFromSettings();
        }

        private static Color ChipColor(bool on, bool busy, bool unavailable)
        {
            if (unavailable)
                return ColorUnavailable;
            if (busy)
                return ColorBusy;
            if (on)
                return ColorOn;
            return ColorOff;
        }

        private static bool IsSegmentationBusy(SectionViewerControl viewer)
        {
            if (AnnotationOverlay.CurrentOverlay?.IsAutoPolygonizeBusy == true)
                return true;

            return viewer.CurrentCommand is SegmentationCommand command && command.IsBusy;
        }

        /// <summary>
        /// Pure Pen chip label for tests and <see cref="Refresh"/>.
        /// </summary>
        internal static (string Text, bool EmphasizeOn, bool EmphasizeBusy, bool EmphasizeUnavailable) DescribePen(bool penOn)
            => penOn ? ("Pen On", true, false, false) : ("Pen Off", false, false, false);

        /// <summary>
        /// Pure AutoPoly chip label. Busy wins over On.
        /// </summary>
        internal static (string Text, bool EmphasizeOn, bool EmphasizeBusy) DescribeAutoPolygonize(bool enabled, bool busy)
        {
            if (!enabled)
                return ("AutoPoly Off", false, false);
            if (busy)
                return ("AutoPoly Busy", false, true);
            return ("AutoPoly On", true, false);
        }

        /// <summary>
        /// Pure Seg chip label. Unavailable wins; then busy; then processing (server answered, outlines
        /// still being built); else ready.
        /// </summary>
        internal static (string Text, bool EmphasizeOn, bool EmphasizeBusy, bool EmphasizeUnavailable) DescribeSegmentation(
            bool available,
            bool busy,
            bool processing = false)
        {
            if (!available)
                return ("Seg Off", false, false, true);
            if (busy)
                return ("Seg Busy", false, true, false);
            if (processing)
                return ("Seg Processing", false, true, false);
            return ("Seg Ready", true, false, false);
        }

        private static void OnChipClicked(object? sender, ViewerStatusChipSlot slot)
        {
            switch (slot)
            {
                case ViewerStatusChipSlot.PenMode:
                    AnnotationMenu.OnPenMode(sender!, EventArgs.Empty);
                    break;
                case ViewerStatusChipSlot.AutoPolygonize:
                    AnnotationMenu.OnAutoPolygonizeCircles(sender!, EventArgs.Empty);
                    break;
                case ViewerStatusChipSlot.Segmentation:
                    AnnotationMenu.OnSelectSegmentationService(sender!, EventArgs.Empty);
                    break;
            }

            Refresh();
        }
    }
}
