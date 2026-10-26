using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI;
using Viking.UI.WPF;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotation.WPF.Forms;

namespace WebAnnotation
{
    [MenuAttribute("Annotation")]
    internal class AnnotationMenu : Viking.Common.IMenuFactory
    {
        private static MergeStructuresForm? _MergeStructuresForm = null;
        private static WebAnnotation.WPF.Forms.AnnotationPreferencesDialog? _preferencesDialog = null;
        private static ToolStripMenuItem menuPenMode;
        private static ToolStripMenuItem menuShowPenActionButtons;
        private static ToolStripMenuItem menuAutoPolygonizeCircles;
        /// <summary>Cancels the running opacity preview pass. Read and replaced only under <see cref="_circleOpacityUpdateLock"/>.</summary>
        private static CancellationTokenSource? _opacityUpdateCancellationTokenSource;
        private static System.Threading.Timer? _circleOpacityUpdateTimer;
        /// <summary>Guards the circle debounce timer and pending values, and <see cref="_opacityUpdateCancellationTokenSource"/>.</summary>
        private static readonly object _circleOpacityUpdateLock = new();
        private static double _pendingCircleOpacityParentless;
        private static double _pendingCircleOpacityWithParent;

        System.Windows.Forms.ToolStripItem Viking.Common.IMenuFactory.CreateMenuItem()
        {
            //Create a menu containing each of our bookmarks
            ToolStripMenuItem menuRoot = new("Annotation");

            ToolStripMenuItem menuFavoriteTypes = new("Choose Favorited Structure Types");
            menuFavoriteTypes.Click += OnChooseFavoriteStructureTypes;
            menuRoot.DropDownItems.Add(menuFavoriteTypes);

            if (Global.Export != null)
            {
                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExport = new("Export");

                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExportMotifs = new("Motifs");

                ToolStripMenuItem menuExportMotifTLP = new("To Tulip Format");
                menuExportMotifTLP.Click += OnExportMotifsTLP;


                menuExportMotifs.DropDownItems.Add(menuExportMotifTLP);
                menuExport.DropDownItems.Add(menuExportMotifs);

                menuRoot.DropDownItems.Add(menuExport);
            }

            menuPenMode = new ToolStripMenuItem("Pen Mode")
            {
                Checked = WebAnnotation.Global.PenMode,
                ShortcutKeyDisplayString = "*"
            };
            menuPenMode.Click += OnPenMode;
            menuRoot.DropDownItems.Add(menuPenMode);

            menuShowPenActionButtons = new ToolStripMenuItem("Show Pen Action Buttons")
            {
                Checked = Global.ShowPenActionButtons
            };
            menuShowPenActionButtons.Click += OnShowPenActionButtons;
            menuRoot.DropDownItems.Add(menuShowPenActionButtons);

            menuAutoPolygonizeCircles = new ToolStripMenuItem("Auto Polygonize Circles")
            {
                Checked = Global.AnnotationSettings.AutoPolygonizeCircles
            };
            menuAutoPolygonizeCircles.Click += OnAutoPolygonizeCircles;
            menuRoot.DropDownItems.Add(menuAutoPolygonizeCircles);

            ToolStripMenuItem menuRandomizeColors = new("Randomize All Colors")
            {
                ToolTipText = "Gives every cell a new random display color. Only changes what you see; nothing is saved and reloading restores the original colors."
            };
            menuRandomizeColors.Click += OnRandomizeAllColors;
            menuRoot.DropDownItems.Add(menuRandomizeColors);

            ToolStripMenuItem menuSelectSegmentation = new("Select Segmentation Service…");
            menuSelectSegmentation.Click += OnSelectSegmentationService;
            menuRoot.DropDownItems.Add(menuSelectSegmentation);

            ToolStripMenuItem menuUpdateVolumePositions = new("Update Volume Positions…");
            menuUpdateVolumePositions.Click += OnUpdateVolumePositions;
            ToolStripSeparator menuUpdateVolumePositionsSeparator = new();
            menuUpdateVolumePositions.Visible = VolumeAccessRoles.HasReviewAccess();
            menuUpdateVolumePositionsSeparator.Visible = menuUpdateVolumePositions.Visible;
            menuRoot.DropDownOpening += (_, _) =>
            {
                bool canReview = VolumeAccessRoles.HasReviewAccess();
                menuUpdateVolumePositions.Visible = canReview;
                menuUpdateVolumePositionsSeparator.Visible = canReview;
            };
            menuRoot.DropDownItems.Add(menuUpdateVolumePositionsSeparator);
            menuRoot.DropDownItems.Add(menuUpdateVolumePositions);


            return menuRoot;
        }

        public static void OnExportMotifsTLP(object sender, EventArgs e)
        {
            Debug.Print("OnExportMotifsTLP");

            Global.Export.OpenMotif();
        }

        public static void OnChooseFavoriteStructureTypes(object sender, EventArgs e)
        {
            Debug.Print("OnChooseFavoriteStructureTypes");
            UI.Forms.SelectStructureTypeForm StructureIDChoiceForm = new();
            Annotation.ViewModels.FavoriteStructureIDsViewModel favorite_view_model = new(Global.UserFavoriteStructureTypes);
            StructureIDChoiceForm.DataContext = favorite_view_model;
            StructureIDChoiceForm.Show();
        }

        /// <summary>
        /// Annotation menu: re-rolls the random display color of every loaded cell. Display only, see <see cref="StructureColors"/>.
        /// </summary>
        private static void OnRandomizeAllColors(object sender, EventArgs e)
        {
            try
            {
                int recolored = StructureColors.RandomizeAll();
                Debug.WriteLine($"Randomized the display color of {recolored} cells");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error randomizing colors: {ex.Message}");
            }
        }

        public static void OnPreferences(object sender, EventArgs e)
        {
            Debug.Print("OnPreferences");

            // If dialog already exists and is open, just focus it
            if (_preferencesDialog != null && !_preferencesDialog.IsClosed)
            {
                _preferencesDialog.Focus();
                return;
            }

            // Create ViewModel and load current settings
            AnnotationPreferencesDialogViewModel viewModel = new();
            viewModel.LoadCurrentSettings(
                Global.AnnotationSettings.NumSectionsInMemory,
                Global.AnnotationSettings.NumSectionsLoading,
                Global.AnnotationSettings.LocationTextScaleFactor,
                Global.AnnotationSettings.ReferenceLocationTextScaleFactor,
                Global.AnnotationSettings.DefaultClosedLineWidth,
                Global.AnnotationSettings.DefaultLocationJumpDownsample,
                Global.AnnotationSettings.AdjacentLocationRadiusScalar,
                Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay,
                Global.AnnotationSettings.PenSimplifyThreshold,
                Global.AnnotationSettings.MinRadius,
                Global.AnnotationSettings.PolygonOpacityParentless,
                Global.AnnotationSettings.PolygonOpacityWithParent,
                Global.AnnotationSettings.CircleOpacityParentless,
                Global.AnnotationSettings.CircleOpacityWithParent,
                Global.AnnotationSettings.SegmentationPointRadius,
                Global.AnnotationSettings.SegmentationHoleDropFraction,
                Global.AnnotationSettings.SegmentationEdgeCleanupRadius,
                Global.AnnotationSettings.PolygonPointRadius,
                Global.AnnotationSettings.SmallestRenderedSize,
                Global.AnnotationSettings.AutoPolygonizeCircles,
                Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers,
                Global.AnnotationSettings.AutoPolygonizeMaxDownsample,
                Global.AnnotationSettings.AutoPolygonizeOverlayMasks,
                Global.AnnotationSettings.AutoPolygonizeOverlayPrompts,
                Global.AnnotationSettings.AutoPolygonizeHideSegmentationRings,
                Global.AnnotationSettings.SegmentationMaskThreshold,
                Global.AnnotationSettings.SegmentationUseMaskInput,
                Global.AnnotationSettings.CorrectionShimEnabled,
                Global.AnnotationSettings.CorrectionShimFadeStartDownsample,
                Global.AnnotationSettings.CorrectionShimFadeEndDownsample,
                Global.AnnotationSettings.CorrectionShimShowOffset,
                Global.AnnotationSettings.PolygonPointOpacity,
                Global.AnnotationSettings.HidePolygonPointsInPenMode,
                Global.AnnotationSettings.SegmentationEdgeSnapEnabled,
                Global.AnnotationSettings.SegmentationSmoothingSigma,
                Global.AnnotationSettings.SegmentationMinFeatureNanometers,
                Global.AnnotationSettings.AutoPolygonizeShimmerEnabled,
                Global.AnnotationSettings.AutoPolygonizeShimmerBrightness,
                Global.AnnotationSettings.AutoPolygonizeShimmerDurationMs,
                Global.AnnotationSettings.SegmentLinkedAnnotations,
                Global.AnnotationSettings.LocationLinkWaistPixels,
                Global.AnnotationSettings.LocationLinkDrawUnderMarkers,
                Global.AnnotationSettings.AutoPolygonizeShimmerStyle,
                Global.AnnotationSettings.HideAnnotationSeconds,
                Global.AnnotationSettings.LabelOutlineEnabled,
                Global.AnnotationSettings.LabelOutlineWhiteText,
                Viking.UI.BarrelButtonBinding.Format(Global.AnnotationSettings.BarrelButton),
                Global.AnnotationSettings.LocationLinkVisibilityPercent,
                Global.AnnotationSettings.LocationLinkLabels
            );

            VikingXNA.Scene scene = AnnotationOverlay.CurrentOverlay?.Parent?.Scene;
            if (scene != null)
                viewModel.NanometersPerPixel = scene.ScreenPixelSizeInVolume * Global.Scale.X;

            // Initialize static accessor properties
            CircleView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;
            LocationCanvasView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;

            viewModel.PropertyChanged += (_, e) =>
            {
                bool refreshAll = string.IsNullOrEmpty(e.PropertyName);

                ApplyLiveSettings(viewModel, e.PropertyName);

                if (AnnotationOverlay.CurrentOverlay?.Parent?.CurrentCommand is not SegmentationCommand command)
                    return;

                if (refreshAll ||
                    e.PropertyName == nameof(viewModel.SegmentationHoleDropFraction) ||
                    e.PropertyName == nameof(viewModel.SegmentationEdgeCleanupRadius))
                {
                    command.RefreshPolygonsFromLastMask(
                        viewModel.SegmentationHoleDropFraction,
                        viewModel.SegmentationEdgeCleanupRadius);
                }
                if (refreshAll || e.PropertyName == nameof(viewModel.SegmentationPointRadius))
                    command.RefreshPromptPointViews(viewModel.SegmentationPointRadius);
            };

            // Set once a slider has rewritten in-memory opacity, so Cancel knows a preview needs undoing.
            bool opacityPreviewed = false;

            // Wire up real-time preview for polygon opacity changes
            viewModel.PolygonOpacityChanged += (parentlessOpacity, withParentOpacity) =>
            {
                opacityPreviewed = true;

                // Cancel the previous pass; this pass keeps its own token with its own values.
                CancellationToken token = StartNewOpacityPreviewPass();

                _ = Task.Run(() =>
                {
                    try
                    {
                        UpdateVisibleSectionPolygonOpacity(
                            parentlessOpacity,
                            withParentOpacity,
                            token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is expected when user adjusts opacity again
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating polygon opacity: {ex.Message}");
                    }
                });
            };

            // Wire up real-time preview for circle opacity changes
            viewModel.CircleOpacityChanged += (parentlessOpacity, withParentOpacity) =>
            {
                opacityPreviewed = true;

                // Use the same throttling mechanism as polygon opacity
                lock (_circleOpacityUpdateLock)
                {
                    _pendingCircleOpacityParentless = parentlessOpacity;
                    _pendingCircleOpacityWithParent = withParentOpacity;

                    _circleOpacityUpdateTimer?.Dispose();
                    System.Threading.Timer? timer = null;
                    timer = new System.Threading.Timer(_ =>
                    {
                        lock (_circleOpacityUpdateLock)
                        {
                            // Disposing a timer does not stop a callback already queued; skip one that was
                            // replaced by a newer slider change or stopped by Cancel.
                            if (!ReferenceEquals(timer, _circleOpacityUpdateTimer))
                                return;

                            CancellationToken token = StartNewOpacityPreviewPass();

                            double parentless = _pendingCircleOpacityParentless;
                            double withParent = _pendingCircleOpacityWithParent;

                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    UpdateVisibleSectionCircleOpacity(
                                        parentless,
                                        withParent,
                                        token);
                                }
                                catch (OperationCanceledException) { }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error updating circle opacity: {ex.Message}");
                                }
                            });
                        }
                    }, null, 100, Timeout.Infinite);
                    _circleOpacityUpdateTimer = timer;
                }
            };

            _preferencesDialog = new WebAnnotation.WPF.Forms.AnnotationPreferencesDialog(viewModel);

            // Wire up event handlers to save settings
            _preferencesDialog.ApplyClicked += (s, args) => SaveSettingsFromViewModel(viewModel);
            _preferencesDialog.OkClicked += (s, args) => SaveSettingsFromViewModel(viewModel);
            // Live settings wrote Global before Cancel; the reverted view model holds the values to restore.
            _preferencesDialog.CancelClicked += (s, args) =>
            {
                ApplyLiveSettings(viewModel, null);

                // Opacity sliders preview by rewriting in-memory views; Global keeps the last applied values.
                if (opacityPreviewed)
                    RestoreOpacityPreview();
            };

            _preferencesDialog.Show(); // Modeless dialog
        }

        /// <summary>
        /// Writes the settings that preview live into <see cref="Global.AnnotationSettings"/>, so the view shows them
        /// while the dialog is open. Called with the changed property's name as the user edits, and with null (every
        /// live setting) when the view model is reverted or reset and on Cancel, which restores the values from
        /// opening or the last Apply. Settings not listed here take effect on Apply/OK.
        /// </summary>
        internal static void ApplyLiveSettings(AnnotationPreferencesDialogViewModel viewModel, string? propertyName)
        {
            bool all = string.IsNullOrEmpty(propertyName);
            bool Changed(string name) => all || propertyName == name;

            // Apply max downsample as the slider moves so a view that is now allowed can
            // start auto-segment without waiting for Apply/OK (Global setter restarts idle).
            if (Changed(nameof(viewModel.AutoPolygonizeMaxDownsample)))
                Global.AnnotationSettings.AutoPolygonizeMaxDownsample = viewModel.AutoPolygonizeMaxDownsample;

            // Live-apply the SAM2 request settings. The Global setters debounce and re-segment the
            // active command and the auto-polygonize view, so a slider drag sends one request.
            if (Changed(nameof(viewModel.SegmentationMaskThreshold)))
                Global.AnnotationSettings.SegmentationMaskThreshold = viewModel.SegmentationMaskThreshold;
            if (Changed(nameof(viewModel.SegmentationUseMaskInput)))
                Global.AnnotationSettings.SegmentationUseMaskInput = viewModel.SegmentationUseMaskInput;
            // Client-side: the setter retraces what is on screen from the stored masks, so no request is sent.
            if (Changed(nameof(viewModel.SegmentationEdgeSnapEnabled)))
                Global.AnnotationSettings.SegmentationEdgeSnapEnabled = viewModel.SegmentationEdgeSnapEnabled;
            if (Changed(nameof(viewModel.SegmentationMinFeatureNanometers)))
                Global.AnnotationSettings.SegmentationMinFeatureNanometers = viewModel.SegmentationMinFeatureNanometers;

            // The overlay setters refresh the auto-polygonize view themselves.
            if (Changed(nameof(viewModel.AutoPolygonizeOverlayMasks)))
                Global.AnnotationSettings.AutoPolygonizeOverlayMasks = viewModel.AutoPolygonizeOverlayMasks;
            if (Changed(nameof(viewModel.AutoPolygonizeOverlayPrompts)))
                Global.AnnotationSettings.AutoPolygonizeOverlayPrompts = viewModel.AutoPolygonizeOverlayPrompts;
            if (Changed(nameof(viewModel.AutoPolygonizeHideSegmentationRings)))
                Global.AnnotationSettings.AutoPolygonizeHideSegmentationRings = viewModel.AutoPolygonizeHideSegmentationRings;

            // Read when a shimmer starts, so the next one uses them.
            if (Changed(nameof(viewModel.AutoPolygonizeShimmerEnabled)))
                Global.AnnotationSettings.AutoPolygonizeShimmerEnabled = viewModel.AutoPolygonizeShimmerEnabled;
            if (Changed(nameof(viewModel.AutoPolygonizeShimmerBrightness)))
                Global.AnnotationSettings.AutoPolygonizeShimmerBrightness = viewModel.AutoPolygonizeShimmerBrightness;
            if (Changed(nameof(viewModel.AutoPolygonizeShimmerDurationMs)))
                Global.AnnotationSettings.AutoPolygonizeShimmerDurationMs = viewModel.AutoPolygonizeShimmerDurationMs;
            if (Changed(nameof(viewModel.AutoPolygonizeShimmerStyle)))
                Global.AnnotationSettings.AutoPolygonizeShimmerStyle = viewModel.AutoPolygonizeShimmerStyle;

            // The setters copy these into the shim settings the viewer reads; refresh it as Apply does.
            bool shimChanged = false;
            if (Changed(nameof(viewModel.CorrectionShimEnabled)))
            {
                Global.AnnotationSettings.CorrectionShimEnabled = viewModel.CorrectionShimEnabled;
                shimChanged = true;
            }
            if (Changed(nameof(viewModel.CorrectionShimFadeStartDownsample)))
            {
                Global.AnnotationSettings.CorrectionShimFadeStartDownsample = viewModel.CorrectionShimFadeStartDownsample;
                shimChanged = true;
            }
            if (Changed(nameof(viewModel.CorrectionShimFadeEndDownsample)))
            {
                Global.AnnotationSettings.CorrectionShimFadeEndDownsample = viewModel.CorrectionShimFadeEndDownsample;
                shimChanged = true;
            }
            if (Changed(nameof(viewModel.CorrectionShimShowOffset)))
            {
                Global.AnnotationSettings.CorrectionShimShowOffset = viewModel.CorrectionShimShowOffset;
                shimChanged = true;
            }
            if (shimChanged)
                Viking.UI.State.ViewerControl?.RefreshCorrectionShim();

            // Read each time they are drawn, so writing Global and redrawing is the whole preview.
            bool redraw = false;
            void Drawn(string name, Action apply)
            {
                if (!Changed(name))
                    return;
                apply();
                redraw = true;
            }

            Drawn(nameof(viewModel.LocationLinkVisibilityPercent),
                () => Global.AnnotationSettings.LocationLinkVisibilityPercent = viewModel.LocationLinkVisibilityPercent);
            Drawn(nameof(viewModel.LocationLinkLabels),
                () => Global.AnnotationSettings.LocationLinkLabels = viewModel.LocationLinkLabels);
            Drawn(nameof(viewModel.LocationLinkWaistPixels),
                () => Global.AnnotationSettings.LocationLinkWaistPixels = viewModel.LocationLinkWaistPixels);
            Drawn(nameof(viewModel.LocationLinkDrawUnderMarkers),
                () => Global.AnnotationSettings.LocationLinkDrawUnderMarkers = viewModel.LocationLinkDrawUnderMarkers);
            Drawn(nameof(viewModel.LabelOutlineEnabled),
                () => Global.AnnotationSettings.LabelOutlineEnabled = viewModel.LabelOutlineEnabled);
            Drawn(nameof(viewModel.LabelOutlineWhiteText),
                () => Global.AnnotationSettings.LabelOutlineWhiteText = viewModel.LabelOutlineWhiteText);
            Drawn(nameof(viewModel.PolygonPointRadius),
                () => Global.AnnotationSettings.PolygonPointRadius = viewModel.PolygonPointRadius);
            Drawn(nameof(viewModel.PolygonPointOpacity),
                () => Global.AnnotationSettings.PolygonPointOpacity = viewModel.PolygonPointOpacity);
            Drawn(nameof(viewModel.HidePolygonPointsInPenMode),
                () => Global.AnnotationSettings.HidePolygonPointsInPenMode = viewModel.HidePolygonPointsInPenMode);
            Drawn(nameof(viewModel.SmallestRenderedSize),
                () => Global.AnnotationSettings.SmallestRenderedSize = viewModel.SmallestRenderedSize);

            if (redraw)
                AnnotationOverlay.CurrentOverlay?.InvalidateParent();
        }

        /// <summary>
        /// Undoes the Annotation Preferences opacity preview: stops a pending circle debounce and any running
        /// preview pass, then puts every polygon and circle in memory back to the opacities in
        /// <see cref="Global.AnnotationSettings"/>. Called on the UI thread when the dialog is cancelled.
        /// </summary>
        private static void RestoreOpacityPreview()
        {
            lock (_circleOpacityUpdateLock)
            {
                _circleOpacityUpdateTimer?.Dispose();
                _circleOpacityUpdateTimer = null;
                // Cancel without replacing: the running pass holds this token. Under the lock, so it cannot
                // race StartNewOpacityPreviewPass disposing the same source.
                _opacityUpdateCancellationTokenSource?.Cancel();
            }

            UpdateAllPolygonOpacityInMemory(
                Global.AnnotationSettings.PolygonOpacityParentless,
                Global.AnnotationSettings.PolygonOpacityWithParent);
            UpdateAllCircleOpacityInMemory(
                Global.AnnotationSettings.CircleOpacityParentless,
                Global.AnnotationSettings.CircleOpacityWithParent);
        }

        /// <summary>
        /// Cancels the running opacity preview pass and returns the token for a new one. Swaps the shared
        /// source under <see cref="_circleOpacityUpdateLock"/> so the polygon handler (UI thread) and the circle
        /// debounce timer (threadpool) never cancel a source the other just disposed. Callers capture the
        /// returned token before starting their task, so a task queued earlier cannot pick up a newer token
        /// and then write its older opacity values after the newer pass.
        /// </summary>
        /// <returns>An uncancelled token owned by the caller's preview pass.</returns>
        private static CancellationToken StartNewOpacityPreviewPass()
        {
            lock (_circleOpacityUpdateLock)
            {
                _opacityUpdateCancellationTokenSource?.Cancel();
                _opacityUpdateCancellationTokenSource?.Dispose();
                _opacityUpdateCancellationTokenSource = new CancellationTokenSource();
                return _opacityUpdateCancellationTokenSource.Token;
            }
        }

        private static void SaveSettingsFromViewModel(WebAnnotation.WPF.Forms.AnnotationPreferencesDialogViewModel viewModel)
        {
            Global.AnnotationSettings.NumSectionsInMemory = viewModel.NumSectionsInMemory;
            Global.AnnotationSettings.NumSectionsLoading = viewModel.NumSectionsLoading;
            Global.AnnotationSettings.LocationTextScaleFactor = viewModel.LocationTextScaleFactor;
            Global.AnnotationSettings.ReferenceLocationTextScaleFactor = viewModel.ReferenceLocationTextScaleFactor;
            Global.AnnotationSettings.DefaultClosedLineWidth = viewModel.DefaultClosedLineWidth;
            Global.AnnotationSettings.DefaultLocationJumpDownsample = viewModel.DefaultLocationJumpDownsample;
            Global.AnnotationSettings.AdjacentLocationRadiusScalar = viewModel.AdjacentLocationRadiusScalar;
            Global.AnnotationSettings.LocationLinkWaistPixels = viewModel.LocationLinkWaistPixels;
            Global.AnnotationSettings.LocationLinkDrawUnderMarkers = viewModel.LocationLinkDrawUnderMarkers;
            Global.AnnotationSettings.LocationLinkVisibilityPercent = viewModel.LocationLinkVisibilityPercent;
            Global.AnnotationSettings.LocationLinkLabels = viewModel.LocationLinkLabels;
            Global.AnnotationSettings.HideAnnotationSeconds = viewModel.HideAnnotationSeconds;
            Global.AnnotationSettings.LabelOutlineEnabled = viewModel.LabelOutlineEnabled;
            Global.AnnotationSettings.LabelOutlineWhiteText = viewModel.LabelOutlineWhiteText;
            Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay = viewModel.NumClosedCurveInterpolationPointsForDisplay;
            Global.AnnotationSettings.PenSimplifyThreshold = viewModel.PenSimplifyThreshold;
            Global.AnnotationSettings.MinRadius = viewModel.MinRadius;
            Global.AnnotationSettings.SegmentationPointRadius = viewModel.SegmentationPointRadius;
            Global.AnnotationSettings.SegmentationHoleDropFraction = viewModel.SegmentationHoleDropFraction;
            Global.AnnotationSettings.SegmentationEdgeCleanupRadius = viewModel.SegmentationEdgeCleanupRadius;
            Global.AnnotationSettings.SegmentationMaskThreshold = viewModel.SegmentationMaskThreshold;
            Global.AnnotationSettings.SegmentationUseMaskInput = viewModel.SegmentationUseMaskInput;
            Global.AnnotationSettings.SegmentationEdgeSnapEnabled = viewModel.SegmentationEdgeSnapEnabled;
            Global.AnnotationSettings.SegmentationSmoothingSigma = viewModel.SegmentationSmoothingSigma;
            Global.AnnotationSettings.SegmentationMinFeatureNanometers = viewModel.SegmentationMinFeatureNanometers;
            Global.AnnotationSettings.AutoPolygonizeCircles = viewModel.AutoPolygonizeCircles;
            Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers = viewModel.AutoPolygonizeMinRadiusNanometers;
            Global.AnnotationSettings.AutoPolygonizeMaxDownsample = viewModel.AutoPolygonizeMaxDownsample;
            Global.AnnotationSettings.AutoPolygonizeOverlayMasks = viewModel.AutoPolygonizeOverlayMasks;
            Global.AnnotationSettings.AutoPolygonizeOverlayPrompts = viewModel.AutoPolygonizeOverlayPrompts;
            Global.AnnotationSettings.AutoPolygonizeHideSegmentationRings = viewModel.AutoPolygonizeHideSegmentationRings;
            Global.AnnotationSettings.AutoPolygonizeShimmerEnabled = viewModel.AutoPolygonizeShimmerEnabled;
            Global.AnnotationSettings.SegmentLinkedAnnotations = viewModel.SegmentLinkedAnnotations;
            Global.AnnotationSettings.AutoPolygonizeShimmerBrightness = viewModel.AutoPolygonizeShimmerBrightness;
            Global.AnnotationSettings.AutoPolygonizeShimmerDurationMs = viewModel.AutoPolygonizeShimmerDurationMs;
            Global.AnnotationSettings.AutoPolygonizeShimmerStyle = viewModel.AutoPolygonizeShimmerStyle;
            Global.AnnotationSettings.CorrectionShimEnabled = viewModel.CorrectionShimEnabled;
            Global.AnnotationSettings.CorrectionShimFadeStartDownsample = viewModel.CorrectionShimFadeStartDownsample;
            Global.AnnotationSettings.CorrectionShimFadeEndDownsample = viewModel.CorrectionShimFadeEndDownsample;
            Global.AnnotationSettings.CorrectionShimShowOffset = viewModel.CorrectionShimShowOffset;
            Viking.UI.State.ViewerControl?.RefreshCorrectionShim();
            // Setter no-ops when the value is unchanged (e.g. already live-applied from the slider).
            // Always re-arm idle so a view that is now in range starts without a camera nudge.
            if (Global.AnnotationSettings.AutoPolygonizeCircles)
                AnnotationOverlay.CurrentOverlay?.RequestAutoPolygonizeIdlePass();
            if (menuAutoPolygonizeCircles != null)
                menuAutoPolygonizeCircles.Checked = viewModel.AutoPolygonizeCircles;
            Global.AnnotationSettings.PolygonPointRadius = viewModel.PolygonPointRadius;
            Global.AnnotationSettings.PolygonPointOpacity = viewModel.PolygonPointOpacity;
            Global.AnnotationSettings.HidePolygonPointsInPenMode = viewModel.HidePolygonPointsInPenMode;
            Global.AnnotationSettings.BarrelButton = Viking.UI.BarrelButtonBinding.Parse(viewModel.BarrelButton);
            Global.AnnotationSettings.SmallestRenderedSize = viewModel.SmallestRenderedSize;

            // Update static accessor properties
            VikingXNAGraphics.CircleView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;
            WebAnnotation.View.LocationCanvasView.SmallestRenderedSizeAccessor = () => Global.AnnotationSettings.SmallestRenderedSize;

            float oldParentlessOpacity = Global.AnnotationSettings.PolygonOpacityParentless;
            float oldWithParentOpacity = Global.AnnotationSettings.PolygonOpacityWithParent;

            Global.AnnotationSettings.PolygonOpacityParentless = (float)viewModel.PolygonOpacityParentless;
            Global.AnnotationSettings.PolygonOpacityWithParent = (float)viewModel.PolygonOpacityWithParent;

            // Update all polygons in memory if opacity has changed
            if (oldParentlessOpacity != Global.AnnotationSettings.PolygonOpacityParentless ||
                oldWithParentOpacity != Global.AnnotationSettings.PolygonOpacityWithParent)
            {
                UpdateAllPolygonOpacityInMemory(
                    Global.AnnotationSettings.PolygonOpacityParentless,
                    Global.AnnotationSettings.PolygonOpacityWithParent);
            }

            float oldCircleOpacityParentless = Global.AnnotationSettings.CircleOpacityParentless;
            float oldCircleOpacityWithParent = Global.AnnotationSettings.CircleOpacityWithParent;

            Global.AnnotationSettings.CircleOpacityParentless = (float)viewModel.CircleOpacityParentless;
            Global.AnnotationSettings.CircleOpacityWithParent = (float)viewModel.CircleOpacityWithParent;

            // Update all circles in memory if opacity has changed
            if (oldCircleOpacityParentless != Global.AnnotationSettings.CircleOpacityParentless ||
                oldCircleOpacityWithParent != Global.AnnotationSettings.CircleOpacityWithParent)
            {
                UpdateAllCircleOpacityInMemory(
                    Global.AnnotationSettings.CircleOpacityParentless,
                    Global.AnnotationSettings.CircleOpacityWithParent);
            }
        }

        /// <summary>
        /// Applies opacity to a view based on whether it has a parent structure
        /// </summary>
        private static void ApplyOpacityToView(LocationCanvasView view, float parentlessOpacity, float withParentOpacity)
        {
            if (view is IColorView colorView)
            {
                bool hasParent = view.Parent?.ParentID.HasValue ?? false;
                float targetOpacity = hasParent ? withParentOpacity : parentlessOpacity;

                var currentColor = colorView.Color;
                colorView.Color = currentColor.SetAlpha(targetOpacity);
            }
        }

        /// <summary>
        /// Updates opacity for views of a specific type in the specified sections using parallel processing
        /// </summary>
        private static void UpdateOpacityForSections<T>(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default) where T : LocationCanvasView, IColorView
        {
            if (Viking.UI.State.volume?.SectionViewModels is null)
                return;

            // Collect sections that exist and have annotations loaded
            List<SectionAnnotationsView> sectionsToProcess = [];

            foreach (var sectionNumber in sectionNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SectionAnnotationsView sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(sectionNumber);
                if (sectionAnnotations != null)
                {
                    sectionsToProcess.Add(sectionAnnotations);
                }
            }

            if (sectionsToProcess.Count == 0)
                return;

            // Process all sections in parallel
            ParallelOptions parallelOptions = new()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };

            Parallel.ForEach(sectionsToProcess, parallelOptions, sectionAnnotations =>
            {
                try
                {
                    // Collect all views of the specified type from this section
                    List<T> views = [.. sectionAnnotations.GetLocations().OfType<T>()];

                    if (views.Count == 0)
                        return;

                    // Update all views in this section in parallel
                    Parallel.ForEach(views, parallelOptions, view =>
                    {
                        try
                        {
                            ApplyOpacityToView(view, parentlessOpacity, withParentOpacity);
                        }
                        catch
                        {
                            // Skip views that aren't ready for updates yet
                        }
                    });
                }
                catch
                {
                    // Skip sections that have issues
                }
            });
        }

        /// <summary>
        /// Updates opacity for polygons in the specified sections using parallel processing
        /// </summary>
        private static void UpdatePolygonOpacityForSections(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default)
        {
            UpdateOpacityForSections<LocationPolygonView>(
                sectionNumbers,
                parentlessOpacity,
                withParentOpacity,
                cancellationToken);
        }

        /// <summary>
        /// Updates opacity for all polygon views in memory using parallel processing
        /// </summary>
        private static void UpdateAllPolygonOpacityInMemory(
            float parentlessOpacity,
            float withParentOpacity)
        {
            if (Viking.UI.State.volume?.SectionViewModels is null)
                return;

            try
            {
                // Get all section numbers that might have annotations loaded
                var allSectionNumbers = Viking.UI.State.volume.SectionViewModels.Keys;

                // Update polygons in all sections
                UpdatePolygonOpacityForSections(
                    allSectionNumbers,
                    parentlessOpacity,
                    withParentOpacity);

                // Trigger redraw
                AnnotationOverlay.CurrentOverlay?.InvalidateParent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating all polygon opacity in memory: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to collect visible section numbers (current and adjacent sections)
        /// </summary>
        private static List<int> GetVisibleSectionNumbers()
        {
            if (AnnotationOverlay.CurrentOverlay is null)
                return [];

            var overlay = AnnotationOverlay.CurrentOverlay;
            var currentSection = overlay.Parent.Section;

            // Collect visible section numbers: current first, then adjacent
            List<int> visibleSectionNumbers = [currentSection.Number];

            if (currentSection.ReferenceSectionAbove != null)
            {
                visibleSectionNumbers.Add(currentSection.ReferenceSectionAbove.Number);
            }

            if (currentSection.ReferenceSectionBelow != null)
            {
                visibleSectionNumbers.Add(currentSection.ReferenceSectionBelow.Number);
            }

            return visibleSectionNumbers;
        }

        /// <summary>
        /// Updates opacity for circles in the specified sections using parallel processing
        /// </summary>
        private static void UpdateCircleOpacityForSections(
            IEnumerable<int> sectionNumbers,
            float parentlessOpacity,
            float withParentOpacity,
            CancellationToken cancellationToken = default)
        {
            UpdateOpacityForSections<LocationCircleView>(
                sectionNumbers,
                parentlessOpacity,
                withParentOpacity,
                cancellationToken);
        }

        /// <summary>
        /// Updates opacity for all circle views in memory using parallel processing
        /// </summary>
        private static void UpdateAllCircleOpacityInMemory(
            float parentlessOpacity,
            float withParentOpacity)
        {
            if (Viking.UI.State.volume?.SectionViewModels is null)
                return;

            try
            {
                // Get all section numbers that might have annotations loaded
                var allSectionNumbers = Viking.UI.State.volume.SectionViewModels.Keys;

                // Update circles in all sections
                UpdateCircleOpacityForSections(
                    allSectionNumbers,
                    parentlessOpacity,
                    withParentOpacity);

                // Trigger redraw
                AnnotationOverlay.CurrentOverlay?.InvalidateParent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating all circle opacity in memory: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates opacity for visible section circle views in real-time for preview using parallel processing
        /// </summary>
        private static void UpdateVisibleSectionCircleOpacity(
            double parentlessOpacity,
            double withParentOpacity,
            CancellationToken cancellationToken)
        {
            if (AnnotationOverlay.CurrentOverlay is null)
                return;

            var overlay = AnnotationOverlay.CurrentOverlay;
            var visibleSectionNumbers = GetVisibleSectionNumbers();

            if (visibleSectionNumbers.Count == 0)
                return;

            // Update circles in visible sections
            UpdateCircleOpacityForSections(
                visibleSectionNumbers,
                (float)parentlessOpacity,
                (float)withParentOpacity,
                cancellationToken);

            // Trigger redraw on UI thread
            overlay.InvalidateParent();
        }

        /// <summary>
        /// Updates opacity for visible section polygon views in real-time for preview using parallel processing
        /// </summary>
        private static void UpdateVisibleSectionPolygonOpacity(
            double parentlessOpacity,
            double withParentOpacity,
            CancellationToken cancellationToken)
        {
            if (AnnotationOverlay.CurrentOverlay is null)
                return;

            var overlay = AnnotationOverlay.CurrentOverlay;
            var visibleSectionNumbers = GetVisibleSectionNumbers();

            if (visibleSectionNumbers.Count == 0)
                return;

            // Update polygons in visible sections
            UpdatePolygonOpacityForSections(
                visibleSectionNumbers,
                (float)parentlessOpacity,
                (float)withParentOpacity,
                cancellationToken);

            // Trigger redraw on UI thread
            overlay.InvalidateParent();
        }

        [MenuItem("Show Pen Input Window")]
        public static void OnShowPenInputWindow(object sender, EventArgs e)
        {
            Debug.Print("OnShowPenInputWindow");

            if (Global.PenAnnotationForm is null || Global.PenAnnotationForm.IsDisposed)
            {
                Global.PenAnnotationForm = new UI.Forms.PenAnnotationViewForm(Viking.UI.State.ViewerForm.Section);
                Global.PenAnnotationForm.Show();
            }
            else
            {
                Global.PenAnnotationForm.Visible = !Global.PenAnnotationForm.Visible;
            }
        }

        [MenuItem("Open Last Modified Location")]
        public static void GoToLastModifiedLocation(object sender, EventArgs e) => AnnotationOverlay.GotoLastModifiedLocation();

        /// <summary>
        /// Toggles Pen Mode (menu, toolbar, status chip, and the <c>*</c> hotkey in
        /// <see cref="AnnotationOverlay.OnKeyDown"/>). Refreshes chips/toolbar/menu checkmarks.
        /// </summary>
        public static void OnPenMode(object sender, EventArgs e)
        {
            Global.PenMode = !Global.PenMode;
            AnnotationStatusChips.Refresh();
        }

        /// <summary>
        /// Toggles the circular choice buttons shown after a pen stroke.
        /// </summary>
        public static void OnShowPenActionButtons(object sender, EventArgs e)
        {
            Global.ShowPenActionButtons = !Global.ShowPenActionButtons;
            menuShowPenActionButtons.Checked = Global.ShowPenActionButtons;
        }

        public static void OnAutoPolygonizeCircles(object sender, EventArgs e)
        {
            Global.AnnotationSettings.AutoPolygonizeCircles = !Global.AnnotationSettings.AutoPolygonizeCircles;
            AnnotationStatusChips.Refresh();
        }

        /// <summary>
        /// Keeps Annotation menu checkmarks aligned with toolbar buttons and status chips.
        /// Called after toggles and from <see cref="UI.AnnotationModeToolbar.SyncFromSettings"/>.
        /// </summary>
        internal static void SyncModeMenuChecked(bool penMode, bool autoPolygonize)
        {
            if (menuPenMode != null)
                menuPenMode.Checked = penMode;
            if (menuAutoPolygonizeCircles != null)
                menuAutoPolygonizeCircles.Checked = autoPolygonize;
        }

        /// <summary>
        /// Opens the login-stage segmentation picker mid-session and applies Select or None.
        /// </summary>
        public static void OnSelectSegmentationService(object sender, EventArgs e)
        {
            if (State.UserBearerToken == null || string.IsNullOrEmpty(State.UserBearerToken.AccessToken))
            {
                MessageBox.Show(
                    "A signed-in bearer token is required to list segmentation services.\n\nSign in again, then retry.",
                    "Select Segmentation Service",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string identityUrl = State.IdentityServerUrl
                ?? Viking.Tokens.TokenInjector.BearerTokenAuthority;
            if (string.IsNullOrWhiteSpace(identityUrl))
            {
                MessageBox.Show(
                    "The Identity Server URL for this session is unknown.\n\nSign in again, then retry.",
                    "Select Segmentation Service",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            IntPtr ownerHandle = IntPtr.Zero;
            if (State.Appwindow != null && State.Appwindow.IsHandleCreated)
                ownerHandle = State.Appwindow.Handle;

            bool? confirmed = SegmentationServiceSelectionWindow.ShowDialog(
                ownerHandle,
                State.UserBearerToken,
                identityUrl,
                SegmentationServiceSession.CurrentEndpoint(),
                State.RecentSegmentationServiceUrls,
                out string? selectedEndpoint,
                SegmentationServiceHealth.LastError);

            if (confirmed == true)
                SegmentationServiceSession.ApplyEndpoint(selectedEndpoint);
        }

        private static void OnUpdateVolumePositions(object sender, EventArgs e)
        {
            if (!VolumeAccessRoles.HasReviewAccess())
                return;

            _ = VolumePositionUpdateLauncher.RunAsync();
        }

        [MenuItem("Open Structure")]
        public static void ShowStructure(object sender, EventArgs e)
        {
            Debug.Print("Show Structure");
            WebAnnotation.AnnotationOverlay.CurrentOverlay.OpenFindStructureForm();
        }

        [MenuItem("Goto Structure")]
        public static void GotoStructure(object sender, EventArgs e)
        {
            Debug.Print("Goto Structure");

            WebAnnotation.AnnotationOverlay.CurrentOverlay.OpenGotoStructureForm();
        }

        [MenuItem("Goto Location")]
        public static void GotoLocation(object sender, EventArgs e)
        {
            Debug.Print("Goto Location");

            WebAnnotation.AnnotationOverlay.CurrentOverlay.OpenGotoLocationForm();
        }

        [MenuItem("Merge Structures")]
        public static void MergeStructures(object sender, EventArgs e)
        {
            Debug.Print("Merge Structures");

            if (_MergeStructuresForm is null)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }
            else if (_MergeStructuresForm.IsDisposed)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }

            _MergeStructuresForm.ShowDialog();
            _MergeStructuresForm.Focus();
        }

        [MenuItem("Export")]
        public static void Export(object sender, EventArgs e) => Debug.Print("Export");


    }

    [MenuAttribute("menuEdit")]
    internal class AnnotationEditMenu
    {
        [MenuItem("Annotation Preferences")]
        public static void OnAnnotationPreferences(object sender, EventArgs e) => AnnotationMenu.OnPreferences(sender, e);
    }
}
