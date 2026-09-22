using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.UI;
using Viking.UI.Controls;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation;
using WebAnnotationModel;
using WebAnnotation.ViewModel;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.Services.Grpc;
using WebAnnotation.UI.AutoPolygonize;

using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Interactive segmentation command that uses AI (SAM2) to segment images based on user-placed points.
    /// Users place foreground (green) and background (red) points, and the system generates a segmentation mask
    /// via gRPC, which can then be converted to a polygon annotation.
    /// 
    /// </summary>
    internal class SegmentationCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        #region Constants
        private const int DEFAULT_DEBOUNCE_MS = 500;
        #endregion

        #region Fields
        // Point collections
        private readonly List<Geometry.Vector2> foregroundPoints = [];
        private readonly List<Geometry.Vector2> backgroundPoints = [];

        // Monographics views for rendering
        private PointSetView foregroundPointsView;
        private PointSetView backgroundPointsView;
        private TextureOverlayView maskOverlayView;
        private readonly List<SolidPolygonView> segmentPolygonViews = [];
        private readonly List<CurveView> segmentPolygonRingViews = [];
        private SolidPolygonView hoveredPolygonView;

        private readonly SegmentationViewportSession viewportSession;

        // Segmentation state
        private byte[] currentMaskData;
        private Texture2D maskTexture;
        private int maskWidth;
        private int maskHeight;
        private Polygon selectedPolygon; // Track the polygon clicked for finalization
        private SegmentationServiceTypes.SegmentationResponse lastSegmentationResponse;
        private readonly SegmentationRequestCoalescer requestCoalescer = new();
        private CancellationTokenSource processResponseCts;

        // Pan/zoom tracking
        private Geometry.Rectangle lastViewBounds;
        private System.Timers.Timer panZoomDebounceTimer;

        // Rendering
        private readonly Color maskColor = new(255, 128, 0, 128); // Orange with transparency

        // Configuration 
        private readonly int debounceMs;

        // Structure type for created annotations
        //private readonly StructureTypeObj structureType;

        /// <summary>
        /// When set, OnActivate and pan/zoom fill avoid marks from other structures in view
        /// (any type except this location and this structure). The numeric type id is only a
        /// flag that auto-background is wanted; it is not used as a type filter.
        /// </summary>
        private readonly long? structureTypeIdForBackgroundPoints;

        /// <summary>
        /// Location being converted must not receive an avoid (background) mark; those marks are for other annotations in view.
        /// </summary>
        private readonly long? locationIdToExcludeFromBackgroundPoints;

        /// <summary>
        /// Adjacent-section members of the same structure sit on the selected circle's center.
        /// They must not get an avoid mark or SAM2 sees a red+green stack at the same pixel.
        /// </summary>
        private readonly long? structureIdToExcludeFromBackgroundPoints;

        /// <summary>
        /// Volume polygon to save when Escape is pressed before a mask arrives.
        /// Null for resegment and circle-to-polygon, which still cancel without saving.
        /// </summary>
        private readonly Polygon? placementFallback;

        /// <summary>True after a mask click or Escape placement so a late mask cannot save again.</summary>
        private bool placementFinished;

        /// <summary>Server image this command holds. Released on deactivate; deleted only when it is the last hold.</summary>
        private ulong? heldImageId;

        /// <summary>Cancels the in-flight SegmentImage so Escape can place the fallback before a late mask arrives.</summary>
        private CancellationTokenSource segmentRequestCts = new();

        /// <summary>
        /// Set to the segmented polygon if the command completes successfully
        /// </summary>
        public Polygon Output
        {
            get;
            private set;
        }
        #endregion

        #region Help Strings
        public new static string[] DefaultMouseHelpStrings =
        [
            "Left-click: Add foreground point (green)",
            "Left-click inside polygon: Finalize and create annotation",
            "Middle-click: Remove nearest point",
            "Right-click: Add background point (red)",
            "Ctrl + Left-click: Delete foreground point",
            "Ctrl + Right-click: Delete background point",
            "Ctrl + drag Left: Delete foreground points under cursor",
            "Ctrl + drag Right: Delete background points under cursor"
        ];

        public string[] HelpStrings
        {
            get
            {
                List<string> s = [.. DefaultMouseHelpStrings];
                foreach (string line in Viking.UI.Commands.Command.DefaultKeyHelpStrings)
                {
                    if (placementFallback is not null && line.StartsWith("Escape", StringComparison.Ordinal))
                        s.Add("Escape: Place the dragged polygon without waiting");
                    else
                        s.Add(line);
                }

                s.Sort();
                return [.. s];
            }
        }

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);


        /// <summary>
        /// Return the approved polygon
        /// </summary>
        /// <param name="output"></param>
        public delegate void OnCommandSuccess(Polygon output);
        private readonly OnCommandSuccess success_callback;

        #endregion

        #region Constructor
        public SegmentationCommand(SectionViewerControl parent,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long? structureTypeId = null,
            long? excludeLocationId = null,
            long? excludeStructureId = null,
            Polygon? placementFallback = null) : base(parent)
        {
            this.success_callback = success_callback;
            this.placementFallback = placementFallback;

            // Load configuration from AppSettings
            debounceMs = int.TryParse(ConfigurationManager.AppSettings["SegmentationDebounceMs"], out var ms) ? ms : DEFAULT_DEBOUNCE_MS;

            Parent.Cursor = Cursors.Cross;

            viewportSession = new SegmentationViewportSession(parent);

            structureTypeIdForBackgroundPoints = structureTypeId;
            locationIdToExcludeFromBackgroundPoints = excludeLocationId;
            structureIdToExcludeFromBackgroundPoints = excludeStructureId;
        }

        /// <summary>
        /// Constructor that accepts initial foreground and background points for automated segmentation.
        /// <paramref name="placementFallback"/> is the dragged volume polygon Escape saves when the mask has not arrived.
        /// </summary>
        public SegmentationCommand(SectionViewerControl parent,
            IEnumerable<Geometry.Vector2> initialForegroundPoints,
            IEnumerable<Geometry.Vector2> initialBackgroundPoints,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long? structureTypeId = null,
            long? excludeLocationId = null,
            long? excludeStructureId = null,
            Polygon? placementFallback = null) : this(parent, success_callback, grpcChannelManager, structureTypeId, excludeLocationId, excludeStructureId, placementFallback)
        {
            // Populate initial points
            if (initialForegroundPoints != null)
            {
                foregroundPoints.AddRange(initialForegroundPoints);
            }
            if (initialBackgroundPoints != null)
            {
                backgroundPoints.AddRange(initialBackgroundPoints);
            }
        }

        /// <summary>
        /// Avoid marks for every visible annotation that is not this location or this structure.
        /// Called from OnActivate and after the view settles. Same filter as auto-polygonize.
        /// </summary>
        private void AddBackgroundPointsFromOtherStructures(VikingXNA.Scene scene)
        {
            var sectionAnnotations = AnnotationOverlay.GetOrCreateAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations is null)
                return;

            IEnumerable<LocationObj> visible = sectionAnnotations.GetLocations(scene.VisibleWorldBounds)
                .Where(loc => loc is not null && loc.IsVisible(scene))
                .Select(loc => Store.Locations.GetObjectByID(loc.ID, false))
                .OfType<LocationObj>();

            double minDistance = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample;
            IReadOnlyCollection<long> excludeIds = locationIdToExcludeFromBackgroundPoints is long id
                ? [id]
                : [];
            backgroundPoints.AddRange(
                CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                    visible,
                    Parent.Section.ActiveSectionToVolumeTransform,
                    foregroundPoints,
                    minDistance,
                    excludeIds,
                    structureIdToExcludeFromBackgroundPoints));
        }
        #endregion

        #region Lifecycle Methods
        public override void OnActivate()
        {
            base.OnActivate();

            if (structureTypeIdForBackgroundPoints.HasValue && Parent.Scene != null)
            {
                AddBackgroundPointsFromOtherStructures(Parent.Scene);
            }

            if (!viewportSession.TryInitializeClient())
                Debug.WriteLine("Failed to initialize segmentation gRPC client");

            // Check if we have initial points from constructor
            bool hasInitialPoints = foregroundPoints.Count > 0 || backgroundPoints.Count > 0;

            // Clear any existing state (only if no initial points were provided)
            if (!hasInitialPoints)
            {
                foregroundPoints.Clear();
                backgroundPoints.Clear();
            }

            // Initialize point views to empty if they don't exist
            if (foregroundPointsView == null)
            {
                foregroundPointsView = new PointSetView(Color.Green, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample)
                {
                    Points = []
                };
            }
            if (backgroundPointsView == null)
            {
                backgroundPointsView = new PointSetView(Color.Red, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample)
                {
                    Points = []
                };
            }

            lastViewBounds = viewportSession.GetCurrentViewportBounds();
            viewportSession.ViewportBounds = lastViewBounds;
            UpdatePointViews();

            // Initialize pan/zoom debounce timer
            panZoomDebounceTimer = new System.Timers.Timer(debounceMs);
            panZoomDebounceTimer.Elapsed += OnPanZoomDebounceElapsed;
            panZoomDebounceTimer.AutoReset = false;

            // If we have initial points, automatically upload image and request segmentation
            if (hasInitialPoints)
            {
                Debug.WriteLine($"SegmentationCommand activated with {foregroundPoints.Count} foreground and {backgroundPoints.Count} background points");
                UploadCurrentImage().ContinueWith(ContinueAfterUpload, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        protected override void OnDeactivate()
        {
            viewportSession.CancelPendingWork();
            CancelSegmentRequest();
            ReleaseHeldImage();

            CleanupCommand();

            panZoomDebounceTimer?.Dispose();
            panZoomDebounceTimer = null;

            Parent.Cursor = Cursors.Default;
            base.OnDeactivate();
        }
        #endregion

        #region Mouse Input Handling
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            Geometry.Vector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
            bool ctrlHeld = Control.ModifierKeys.HasFlag(Keys.Control);

            if (e.Button.Left())
            {
                if (ctrlHeld)
                {
                    HandlePointDeletion(foregroundPoints, worldPos);
                }
                else
                {
                    HandleForegroundPointAddition(worldPos);
                }
            }
            else if (e.Button.Right())
            {
                if (ctrlHeld)
                    HandlePointDeletion(backgroundPoints, worldPos);
                else
                    HandleBackgroundPointAddition(worldPos);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                RemoveNearestPoint(worldPos);
                UpdatePointViews();
                RequestSegmentationOrClear();
            }

            base.OnMouseDown(sender, e);
        }

        private void HandlePointDeletion(List<Geometry.Vector2> pointList, Geometry.Vector2 worldPos)
        {
            Geometry.Vector2? pointToRemove = FindPointWithinRadius(pointList, worldPos, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius);
            if (pointToRemove.HasValue)
            {
                pointList.Remove(pointToRemove.Value);
                UpdatePointViews();
                RequestSegmentationOrClear();
            }
        }

        /// <summary>
        /// Removes all points in the list that are within the given radius of worldPos (same screen-space logic as FindPointWithinRadius).
        /// Returns true if any points were removed.
        /// </summary>
        private bool RemovePointsWithinRadius(List<Geometry.Vector2> pointList, Geometry.Vector2 worldPos, double radiusInScreenUnits)
        {
            Geometry.Vector2 screenPos = WorldToScreen(worldPos);
            double radiusSquared = radiusInScreenUnits * radiusInScreenUnits;
            bool anyRemoved = false;
            for (int i = pointList.Count - 1; i >= 0; i--)
            {
                Geometry.Vector2 ptScreen = WorldToScreen(pointList[i]);
                if (Geometry.Vector2.DistanceSquared(ptScreen, screenPos) <= radiusSquared)
                {
                    pointList.RemoveAt(i);
                    anyRemoved = true;
                }
            }
            return anyRemoved;
        }

        private void HandleForegroundPointAddition(Geometry.Vector2 worldPos)
        {
            //Check if we are clicking inside a foreground point
            if (ForegroundPointsContain(worldPos))
            {
                // Check if clicking inside existing polygon to execute (finalize)
                Polygon clickedPolygon = FindPolygonContainingPoint(worldPos);
                if (clickedPolygon != null)
                {
                    //Check if the user has selected a foreground point

                    selectedPolygon = clickedPolygon;
                    Execute();
                    return;
                }
            }

            HandlePointAddition(foregroundPoints, worldPos);
        }

        private void HandleBackgroundPointAddition(Geometry.Vector2 worldPos) => HandlePointAddition(backgroundPoints, worldPos);

        private void HandlePointAddition(List<Geometry.Vector2> pointList, Geometry.Vector2 worldPos)
        {
            // Check for overlapping point
            Geometry.Vector2? existingPoint = FindPointWithinRadius(pointList, worldPos, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius);
            if (!existingPoint.HasValue)
            {
                // Add point only if no overlap
                pointList.Add(worldPos);
                UpdatePointViews();
                UploadImageAndRequestSegmentation();
            }
        }

        /// <summary>
        /// Uploads image if needed (first point) and requests segmentation
        /// </summary>
        private void UploadImageAndRequestSegmentation()
        {
            bool isFirstPoint = (foregroundPoints.Count + backgroundPoints.Count == 1);

            // Check if already uploading using Interlocked
            bool currentlyUploading = viewportSession.IsUploading;
            if (isFirstPoint && !viewportSession.CurrentImageId.HasValue && !currentlyUploading)
            {
                Debug.WriteLine("First point placed, uploading image to server cache");
                UploadCurrentImage().ContinueWith(ContinueAfterUpload, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                RequestSegmentation();
            }
        }

        /// <summary>
        /// Clears segmentation if no foreground points remain, otherwise requests new segmentation
        /// </summary>
        private void RequestSegmentationOrClear()
        {
            if (foregroundPoints.Count == 0)
            {
                requestCoalescer.Invalidate();
                CancelProcessSegmentationResponse();
                ClearSegmentationResults();
            }
            else
            {
                RequestSegmentation();
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            bool ctrlHeld = Control.ModifierKeys.HasFlag(Keys.Control);
            // When Ctrl+RMB is held we delete background points; do not let base command pan the scene
            if (!(ctrlHeld && e.Button.Right()))
                base.OnMouseMove(sender, e);

            // Update cursor based on mouse position over points
            Geometry.Vector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
            
            // Check if mouse is over a foreground or background point
            // Convert world-space radius to screen-space radius for detection
            double pointRadiusInWorld = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
            double pointRadiusInScreen = pointRadiusInWorld;
            Geometry.Vector2? foregroundPoint = FindPointWithinRadius(foregroundPoints, worldPos, pointRadiusInScreen);
            Geometry.Vector2? backgroundPoint = FindPointWithinRadius(backgroundPoints, worldPos, pointRadiusInScreen);
            hoveredPolygonView = foregroundPoint.HasValue
                ? FindPolygonViewContainingPoint(worldPos)
                : null;
            
            // Ctrl + button held: delete points under cursor (left = foreground, right = background)
            if (ctrlHeld)
            {
                double radius = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
                bool anyRemoved = false;
                if (e.Button.Left())
                    anyRemoved = RemovePointsWithinRadius(foregroundPoints, worldPos, radius);
                else if (e.Button.Right())
                    anyRemoved = RemovePointsWithinRadius(backgroundPoints, worldPos, radius);
                if (anyRemoved)
                {
                    UpdatePointViews();
                    RequestSegmentationOrClear();
                }
            }

            // Update cursor based on detected state
            if (ctrlHeld && (foregroundPoint.HasValue || backgroundPoint.HasValue))
            {
                // Ctrl held over a point indicates deletion intent
                Parent.Cursor = Cursors.No;
            }
            else if (hoveredPolygonView != null)
            {
                Parent.Cursor = Cursors.Hand;
            }
            else if (foregroundPoint.HasValue || backgroundPoint.HasValue)
            {
                // Hovering over a point indicates adjustment intent
                Parent.Cursor = Cursors.Default;
            }
            else
            {
                // Default cursor for placing new points
                Parent.Cursor = Cursors.Cross;
            }

            // Check if viewport has changed (pan/zoom)
            CheckForViewportChange();

            Parent.Invalidate();
        }

        /// <summary>
        /// Escape places <see cref="placementFallback"/> and leaves the command queue intact.
        /// Callers that passed no fallback keep the base cancel, which clears the queue.
        /// </summary>
        protected override void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Escape && placementFallback is not null)
            {
                AcceptPlacementFallback();
                return;
            }

            base.OnKeyPress(sender, e);
        }

#if DEBUG
        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                RequestSegmentation();
            }
            base.OnKeyDown(sender, e);
        }
#endif
        #endregion

        #region Pan/Zoom Handling
        /// <summary>
        /// Drops this command's hold when the view moves more than 1%. The shared lease releases its own
        /// hold so DeleteImage runs only when auto-polygonize is not still using the id.
        /// </summary>
        private void CheckForViewportChange()
        {
            Geometry.Rectangle currentBounds = viewportSession.GetCurrentViewportBounds();

            if (!SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, currentBounds))
            {
                lastViewBounds = currentBounds;
                viewportSession.ViewportBounds = currentBounds;
                requestCoalescer.Invalidate();
                CancelProcessSegmentationResponse();
                CancelSegmentRequest();
                viewportSession.CancelPendingWork();
                AnnotationOverlay.CurrentOverlay?.SharedViewportImages?.ForgetIfViewMoved(currentBounds, Parent.Downsample);
                ReleaseHeldImage();

                // Restart debounce timer
                panZoomDebounceTimer?.Stop();
                panZoomDebounceTimer?.Start();
            }
        }

        /// <summary>
        /// After the view settles, refresh structure-type background prompts. Does not re-upload until the next point.
        /// </summary>
        private void OnPanZoomDebounceElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // User has stopped panning/zooming
            // Recompute structure-type background points when visible set changes (replaces any previously derived points)
            if (structureTypeIdForBackgroundPoints.HasValue && Parent.Scene != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    backgroundPoints.Clear();
                    AddBackgroundPointsFromOtherStructures(Parent.Scene);
                    UpdatePointViews();
                }));
            }

            // Only re-request segmentation if we have points and an uploaded image
            if (foregroundPoints.Count > 0 || backgroundPoints.Count > 0)
            {
                double pointRadius = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample;
                backgroundPointsView.PointRadius = pointRadius;
                foregroundPointsView.PointRadius = pointRadius;
                Debug.WriteLine("Viewport settled with existing points, re-requesting segmentation");

                // Must invoke on UI thread
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                    // RequestSegmentation will handle uploading if needed
                    RequestSegmentation()));
            }
            else
            {
                Debug.WriteLine("Viewport settled, no points present - no upload needed");
            }
        }
        #endregion

        #region Point Management
        private void UpdatePointViews()
        {
            UpdatePointViews(WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample);
        }

        private void UpdatePointViews(double pointRadius)
        {
            foregroundPointsView.PointRadius = pointRadius;
            foregroundPointsView.Points = [.. foregroundPoints];
            foregroundPointsView.UpdateViews();

            backgroundPointsView.PointRadius = pointRadius;
            backgroundPointsView.Points = [.. backgroundPoints];
            backgroundPointsView.UpdateViews();

            Parent.Invalidate();
        }

        private Geometry.Vector2? FindPointWithinRadius(List<Geometry.Vector2> points, Geometry.Vector2 worldPos, double radiusInScreenUnits)
        {
            // Convert world position to screen coordinates
            Geometry.Vector2 screenPos = WorldToScreen(worldPos);
            double radiusSquared = radiusInScreenUnits * radiusInScreenUnits;

            // Search for a point within the radius
            foreach (var pt in points)
            {
                Geometry.Vector2 ptScreen = WorldToScreen(pt);
                double distSq = Geometry.Vector2.DistanceSquared(ptScreen, screenPos);
                if (distSq <= radiusSquared)
                {
                    return pt;
                }
            }

            return null;
        }

        private void RemoveNearestPoint(Geometry.Vector2 worldPos)
        {
            const double searchRadiusSquared = 100.0; // 10 pixel radius squared

            // Find nearest foreground point
            Geometry.Vector2? nearestFg = null;
            double nearestFgDistSq = double.MaxValue;
            foreach (var pt in foregroundPoints)
            {
                double distSq = Geometry.Vector2.DistanceSquared(pt, worldPos);
                if (distSq < nearestFgDistSq && distSq < searchRadiusSquared)
                {
                    nearestFgDistSq = distSq;
                    nearestFg = pt;
                }
            }

            // Find nearest background point
            Geometry.Vector2? nearestBg = null;
            double nearestBgDistSq = double.MaxValue;
            foreach (var pt in backgroundPoints)
            {
                double distSq = Geometry.Vector2.DistanceSquared(pt, worldPos);
                if (distSq < nearestBgDistSq && distSq < searchRadiusSquared)
                {
                    nearestBgDistSq = distSq;
                    nearestBg = pt;
                }
            }

            // Remove the closest point
            if (nearestFg.HasValue && nearestFgDistSq < nearestBgDistSq)
            {
                foregroundPoints.Remove(nearestFg.Value);
            }
            else if (nearestBg.HasValue)
            {
                backgroundPoints.Remove(nearestBg.Value);
            }
        }

        private SolidPolygonView FindPolygonViewContainingPoint(Geometry.Vector2 worldPos) =>
            segmentPolygonViews.FirstOrDefault(polygonView =>
                polygonView?.InputPolygon != null && polygonView.InputPolygon.Covers(worldPos));

        private Polygon FindPolygonContainingPoint(Geometry.Vector2 worldPos) =>
            FindPolygonViewContainingPoint(worldPos)?.InputPolygon;

        /// <summary>
        /// Returns the point that contains the worldPos parameter.  Otherwise null
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        private bool ForegroundPointsContain(Geometry.Vector2 worldPos) => foregroundPointsView.Points.Any(p => new Circle(p, foregroundPointsView.PointRadius).Covers(worldPos));
        #endregion

        #region Color Generation
        /// <summary>
        /// Generates a distinct color for a segment based on its index
        /// </summary>
        /// <param name="index">Index of the segment</param>
        /// <param name="total">Total number of segments</param>
        /// <returns>A color with distinct hue</returns>
        private Color GenerateDistinctColor(int index, int total)
        {
            // Distribute hues evenly across the color spectrum
            float hue = (float)index / Math.Max(total, 1);
            return ColorFromHSL(hue, 0.8f, 0.5f, 0.25f);
        }

        /// <summary>
        /// Converts HSL color values to RGB Color
        /// </summary>
        /// <param name="hue">Hue value from 0.0 to 1.0</param>
        /// <param name="saturation">Saturation value from 0.0 to 1.0</param>
        /// <param name="lightness">Lightness value from 0.0 to 1.0</param>
        /// <param name="alpha">Alpha value from 0.0 to 1.0</param>
        /// <returns>RGB Color</returns>
        private Color ColorFromHSL(float hue, float saturation, float lightness, float alpha)
        {
            // Ensure hue wraps around
            hue = hue - (float)Math.Floor(hue);

            float r, g, b;

            if (saturation == 0)
            {
                // Achromatic (gray)
                r = g = b = lightness;
            }
            else
            {
                float q = lightness < 0.5f
                    ? lightness * (1 + saturation)
                    : lightness + saturation - lightness * saturation;
                float p = 2 * lightness - q;

                r = HueToRGB(p, q, hue + 1f / 3f);
                g = HueToRGB(p, q, hue);
                b = HueToRGB(p, q, hue - 1f / 3f);
            }

            return new Color(r, g, b, alpha);
        }

        /// <summary>
        /// Helper function for HSL to RGB conversion
        /// </summary>
        private float HueToRGB(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
        #endregion

        #region Server Image Upload/Delete
        /// <summary>
        /// Adopts a viewport-similar upload or captures once onto the shared lease.
        /// Holds the id on the UI continuation so a pan or Escape cannot race the hold.
        /// </summary>
        private async Task<bool> UploadCurrentImage()
        {
            Geometry.Rectangle bounds = viewportSession.GetCurrentViewportBounds();
            double downsample = Parent.Downsample;
            SharedViewportImageLease? lease = AnnotationOverlay.CurrentOverlay?.SharedViewportImages;
            if (lease is null)
            {
                viewportSession.ViewportBounds = bounds;
                return await viewportSession.UploadCurrentImageAsync(segmentRequestCts.Token).ConfigureAwait(false);
            }

            AutoPolygonizeUploadContext? context = await lease.GetOrUploadAsync(bounds, downsample, () => CaptureForLeaseAsync(bounds, downsample)).ConfigureAwait(false);
            if (context is null && !placementFinished && !segmentRequestCts.IsCancellationRequested)
                context = await lease.GetOrUploadAsync(bounds, downsample, () => CaptureForLeaseAsync(bounds, downsample)).ConfigureAwait(false);

            if (context is not { IsUsable: true } ready)
                return false;

            if (!SharedViewportImageLease.CanReuse(ready, viewportSession.GetCurrentViewportBounds(), Parent.Downsample))
            {
                lease.ForgetIfViewMoved(viewportSession.GetCurrentViewportBounds(), Parent.Downsample);
                return false;
            }

            if (viewportSession.CurrentImageId != ready.ImageId)
                viewportSession.AdoptUploadedImage(ready.ImageId, ready.WorldBounds, ready.Width, ready.Height);

            return true;
        }

        /// <summary>
        /// Capture used only when this command is the lease starter. A joined waiter never runs it.
        /// </summary>
        private async Task<AutoPolygonizeUploadContext?> CaptureForLeaseAsync(Geometry.Rectangle bounds, double downsample)
        {
            viewportSession.ViewportBounds = bounds;
            if (!await viewportSession.UploadCurrentImageAsync(segmentRequestCts.Token).ConfigureAwait(false))
                return null;

            return SharedViewportImageLease.TryCreateContext(viewportSession, downsample);
        }

        /// <summary>
        /// UI-thread follow-up for <see cref="UploadCurrentImage"/>. Takes the cache hold, then segments.
        /// </summary>
        private void ContinueAfterUpload(Task<bool> task)
        {
            if (placementFinished || Deactivated)
                return;

            if (!SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, viewportSession.GetCurrentViewportBounds()))
                return;

            if (task.Status == TaskStatus.RanToCompletion && task.Result && viewportSession.CurrentImageId is ulong imageId)
            {
                HoldSharedImage(imageId);
                RequestSegmentation();
            }
        }

        private void HoldSharedImage(ulong imageId)
        {
            if (placementFinished || heldImageId == imageId)
                return;

            AutoPolygonizeCache? cache = AnnotationOverlay.CurrentOverlay?.PolygonizeImageCache;
            if (cache is null)
                return;

            ulong? previous = heldImageId;
            heldImageId = imageId;
            cache.AcquireBatchHold(imageId);
            if (previous is ulong oldId)
                cache.ReleaseBatchHold(oldId);
        }

        /// <summary>
        /// Drops this command's hold. Deletes only when the cache has no remaining hold and this session still owns an id nobody leased.
        /// </summary>
        private void ReleaseHeldImage()
        {
            AutoPolygonizeCache? cache = AnnotationOverlay.CurrentOverlay?.PolygonizeImageCache;
            if (heldImageId is ulong id)
            {
                cache?.ReleaseBatchHold(id);
                heldImageId = null;
                viewportSession.ClearImageId();
                return;
            }

            if (viewportSession.CurrentImageId is not ulong currentId)
                return;

            if (cache is not null && cache.IsImageHeld(currentId))
            {
                viewportSession.ClearImageId();
                return;
            }

            _ = viewportSession.DeleteCurrentImageAsync();
        }

        private void CancelSegmentRequest()
        {
            CancellationTokenSource previous = segmentRequestCts;
            segmentRequestCts = new CancellationTokenSource();
            try
            {
                previous.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            previous.Dispose();
        }

        /// <summary>
        /// Cancels upload and SegmentImage, then saves the dragged polygon through the same callback as a finished mask.
        /// Does not call <see cref="Viking.UI.Commands.Command.CancelCommand"/> because that clears the queue.
        /// </summary>
        private void AcceptPlacementFallback()
        {
            if (placementFinished || placementFallback is null)
                return;

            placementFinished = true;
            requestCoalescer.Invalidate();
            CancelSegmentRequest();
            viewportSession.CancelPendingWork();
            CancelProcessSegmentationResponse();
            try
            {
                success_callback?.Invoke(placementFallback);
            }
            finally
            {
                Deactivated = true;
            }
        }
        #endregion

        #region gRPC Segmentation
        /// <summary>
        /// Sends one SegmentImage with the current prompt lists. Extra clicks while busy or uploading
        /// set a follow-up so the next attempt uses every point collected so far.
        /// </summary>
        private async Task RequestSegmentation()
        {
            if (placementFinished || Deactivated)
                return;

            if (!viewportSession.HasClient)
                return;

            if (foregroundPoints.Count == 0 && backgroundPoints.Count == 0)
                return;

            if (viewportSession.IsUploading)
            {
                requestCoalescer.MarkDirty();
                Debug.WriteLine("Upload in progress, segmentation will be requested after upload completes");
                return;
            }

            if (!requestCoalescer.TryStart(out int generation))
                return;

            try
            {
                Debug.WriteLine($"Sending segmentation request with image ID {viewportSession.CurrentImageId}: {viewportSession.UploadedImageWidth}x{viewportSession.UploadedImageHeight}, {foregroundPoints.Count} fg, {backgroundPoints.Count} bg points");
                CancellationToken segmentToken = segmentRequestCts.Token;
                var response = await viewportSession.SegmentAsync(foregroundPoints, backgroundPoints, segmentToken).ConfigureAwait(false);
                if (!placementFinished && response is not null && requestCoalescer.ShouldApply(generation))
                    StartProcessSegmentationResponse(response, generation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Segmentation error: {ex.Message}");
            }
            finally
            {
                if (requestCoalescer.OnFinishedShouldRetry())
                    ScheduleFollowUpSegmentation();
            }
        }

        /// <summary>
        /// Starts the coalesced follow-up on the UI dispatcher so prompt lists are read on that thread.
        /// </summary>
        private void ScheduleFollowUpSegmentation()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return;

            dispatcher.BeginInvoke(new Action(() => _ = RequestSegmentation()));
        }

        /// <summary>
        /// Cancels an in-flight polygonize and starts one for this response on a worker.
        /// A newer result replaces the previous process token so stale marching squares stop applying.
        /// </summary>
        private void StartProcessSegmentationResponse(
            SegmentationServiceTypes.SegmentationResponse response,
            int generation)
        {
            CancellationTokenSource previous = processResponseCts;
            CancellationTokenSource next = new();
            processResponseCts = next;
            try
            {
                previous?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            previous?.Dispose();
            _ = ProcessSegmentationResponseAsync(response, generation, next.Token);
        }

        /// <summary>
        /// Polygonizes off the UI thread. Applies views only if this generation still owns the overlay.
        /// </summary>
        private async Task ProcessSegmentationResponseAsync(
            SegmentationServiceTypes.SegmentationResponse response,
            int generation,
            CancellationToken cancellationToken)
        {
            if (response.Segments.Count == 0)
            {
                Debug.WriteLine("No segments returned");
                return;
            }

            IReadOnlyList<Polygon> polygons;
            try
            {
                polygons = await Task.Run(
                    () => viewportSession.CreatePolygonsFromResponse(
                        response,
                        preserveHolesContainingWorldPoints: backgroundPoints,
                        cancellationToken: cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Segmentation process generation={generation} cancelled");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Segmentation process generation={generation} failed: {ex}");
                return;
            }

            if (polygons is null || polygons.Count == 0)
            {
                Debug.WriteLine($"Segmentation process generation={generation} produced no polygons");
                return;
            }

            if (cancellationToken.IsCancellationRequested || !requestCoalescer.ShouldApply(generation))
                return;

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return;

            await dispatcher.InvokeAsync(() =>
            {
                if (!requestCoalescer.TryApply(generation))
                    return;

                lastSegmentationResponse = response;
                ApplyPolygonViews(polygons, response.Segments.Count);
#if DEBUG
                CreateDebugMaskOverlay(response);
#endif
                Parent.Invalidate();
            }).Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Converts protobuf segments to Polygons and creates colored polygon views
        /// </summary>
        public void RefreshPolygonsFromLastMask(double? holeDropFraction = null, int? edgeCleanupRadius = null)
        {
            if (lastSegmentationResponse is null)
                return;

            ConvertSegmentsToPolygonViews(lastSegmentationResponse, holeDropFraction, edgeCleanupRadius);
            Parent.Invalidate();
        }

        public void RefreshPromptPointViews(double? pointRadiusPixels = null)
        {
            if (foregroundPointsView is null || backgroundPointsView is null)
                return;

            double radius = (pointRadiusPixels ?? WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius) * Parent.Downsample;
            UpdatePointViews(radius);
        }

        private void ConvertSegmentsToPolygonViews(
            SegmentationServiceTypes.SegmentationResponse response,
            double? holeDropFraction = null,
            int? edgeCleanupRadius = null)
        {
            IReadOnlyList<Polygon> polygons = viewportSession.CreatePolygonsFromResponse(
                response,
                holeDropFraction,
                backgroundPoints,
                edgeCleanupRadius);
            ApplyPolygonViews(polygons, response.Segments.Count);
        }

        /// <summary>
        /// Replaces the live overlay with already-built polygons. Must run on the UI thread.
        /// </summary>
        private void ApplyPolygonViews(IReadOnlyList<Polygon> polygons, int totalPolygons)
        {
            segmentPolygonViews.Clear();
            segmentPolygonRingViews.Clear();
            hoveredPolygonView = null;

            int polygonIndex = 0;
            foreach (Polygon gridPolygon in polygons)
            {
                Color polygonColor = GenerateDistinctColor(polygonIndex, totalPolygons);
                segmentPolygonViews.Add(new SolidPolygonView(gridPolygon, polygonColor));
                segmentPolygonRingViews.Add(CreateRingView(gridPolygon.ExteriorRing, polygonColor));
                segmentPolygonRingViews.AddRange(gridPolygon.InteriorRings.Select(ring => CreateRingView(ring, polygonColor)));
                polygonIndex++;
            }

            Debug.WriteLine($"Created {segmentPolygonViews.Count} polygon views");
        }

        private CurveView CreateRingView(IEnumerable<Geometry.Vector2> ring, Color color) =>
            new(
                [.. ring],
                color.SetAlpha(0.65f),
                TryToClose: true,
                numInterpolations: 0,
                lineWidth: Math.Max(1.0, Parent.Downsample * 2.0),
                lineStyle: LineStyle.Tubular,
                ShowControlPoints: false);

        /// <summary>
        /// Converts a protobuf polygon to Polygon with Y-axis inversion
        /// </summary>
        private Polygon ConvertProtoPolygonToPolygon(
            SegmentationServiceTypes.Polygon protoPolygon,
            SegmentationServiceTypes.SegmentationResponse response)
        {
            try
            {
                // Invert Y coordinates: Viking uses bottom-left origin, server uses top-left
                SegmentationServiceTypes.Polygon invertedProtoPolygon = new()
                {
                    Points = { protoPolygon.Points.Select(p => new SegmentationServiceTypes.Point
                    {
                        X = p.X,
                        Y = response.Height - p.Y
                    }) }
                };
                return invertedProtoPolygon.ToPolygon(viewportSession.ViewportBounds, response.Width, response.Height);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

#if DEBUG
        /// <summary>
        /// Creates a debug mask overlay texture for visualization (DEBUG only)
        /// </summary>
        private void CreateDebugMaskOverlay(SegmentationServiceTypes.SegmentationResponse response)
        {
            var bestSegment = response.Segments.OrderByDescending(s => s.Score).First();
            
            // Decode PNG mask to get dimensions and pixel data
            byte[] pngBytes = bestSegment.Mask.ToByteArray();
            var (decodedMaskData, decodedWidth, decodedHeight) = viewportSession.DecodePngMask(pngBytes);
            
            // Store mask data
            currentMaskData = decodedMaskData;
            maskWidth = decodedWidth;
            maskHeight = decodedHeight;

            // Create texture for rendering
            maskTexture?.Dispose();
            maskTexture = CreateMaskTexture(currentMaskData, maskWidth, maskHeight);

            // Create TextureOverlayView for rendering
            if (maskTexture != null)
            {
                // Transform segment bounds from viewport coordinates to world coordinates
                Geometry.Vector2 topLeft = viewportSession.ViewportToWorld(bestSegment.X, response.Height - bestSegment.Y, viewportSession.UploadedImageWidth, viewportSession.UploadedImageHeight);
                Geometry.Vector2 bottomRight = viewportSession.ViewportToWorld(
                    bestSegment.X + decodedWidth,
                    (response.Height - bestSegment.Y) - decodedHeight,
                    viewportSession.UploadedImageWidth,
                    viewportSession.UploadedImageHeight
                );
                Geometry.Rectangle segmentBounds = new(topLeft, bottomRight);
                maskOverlayView = new TextureOverlayView(maskTexture, segmentBounds, maskColor);
            }
        }
#endif
        #endregion

        #region Mask Processing
        private Texture2D CreateMaskTexture(byte[] maskData, int width, int height)
        {
            try
            {
                var graphicsDevice = Parent.Device;
                if (graphicsDevice is null || maskData is null || maskData.Length != width * height)
                    return null;

                Texture2D texture = new(graphicsDevice, width, height);
                Color[] pixels = new Color[width * height];

                for (int i = 0; i < maskData.Length; i++)
                {
                    // Non-zero mask values become the mask color
                    pixels[i] = maskData[i] > 0 ? maskColor : Color.Transparent;
                }

                texture.SetData(pixels);
                return texture;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating mask texture: {ex.Message}");
                return null;
            }
        }

        private bool IsPointInsideMask(Geometry.Vector2 worldPos)
        {
            if (currentMaskData is null || maskWidth == 0 || maskHeight == 0)
                return false;

            try
            {
                // Convert world position to viewport pixel coordinates
                var screenPt = viewportSession.WorldToViewport(worldPos, maskWidth, maskHeight);
                int x = (int)screenPt.X;
                int y = (int)screenPt.Y;

                // Check bounds
                if (x < 0 || x >= maskWidth || y < 0 || y >= maskHeight)
                    return false;

                // Check mask value
                int idx = y * maskWidth + x;
                return idx < currentMaskData.Length && currentMaskData[idx] > 0;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Coordinate Transforms
        /// <summary>
        /// Coordinate System Notes:
        /// - World: Viking's annotation space coordinates (origin at volume corner)
        /// - Screen: Control's display coordinates (origin at top-left corner)
        /// - Viewport: Captured image pixel coordinates (origin at bottom-left, matches world space orientation)
        /// 
        /// Y-axis conventions:
        /// - Viking world space: Y increases upward (bottom-left origin)
        /// - Segmentation server: Y increases downward (top-left origin)
        /// - Conversions handle Y-axis inversion when communicating with server
        /// </summary>

        /// <summary>
        /// Converts world coordinates to screen pixel coordinates
        /// </summary>
        private Geometry.Vector2 WorldToScreen(Geometry.Vector2 worldPos) => Parent.WorldToScreen(worldPos.X, worldPos.Y);
        #endregion

        #region Rendering
        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if(foregroundPoints is null || backgroundPoints is null)
                return;

            // Save current depth buffer state
            var previousDepthStencilState = graphicsDevice.DepthStencilState;

#if DEBUG
            // Draw mask overlay if available (using TextureOverlayView) - DEBUG only
            maskOverlayView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
#endif

            hoveredPolygonView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            foreach (CurveView ringView in segmentPolygonRingViews)
                ringView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            // Disable depth testing to ensure points always draw on top of polygons
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            // Animate point opacity with pulsing effect for visibility
            const float FLASH_RATE_SECONDS = 3.0f; // Time for one complete pulse cycle
            DateTime now = DateTime.UtcNow;
            float elapsedSeconds = (now.Second * 1000 + now.Millisecond) / 1000f;

            // Calculate pulsing alpha values using sine/cosine for smooth animation
            double phaseAngle = ((elapsedSeconds % FLASH_RATE_SECONDS) / FLASH_RATE_SECONDS) * 2 * Math.PI;
            double foregroundPulse = Math.Sin(phaseAngle);
            double backgroundPulse = Math.Cos(phaseAngle);

            // Draw background points (red circles) with pulsing alpha (opposite phase)
            backgroundPointsView?.Alpha = (float)(0.64 + backgroundPulse * 0.33); // Range: 0.0 to 1.0
            backgroundPointsView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            // Draw foreground points (green circles) with pulsing alpha
            foregroundPointsView?.Alpha = (float)(0.64 + foregroundPulse * 0.33); // Range: 0.0 to 1.0
            foregroundPointsView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            

            // Restore previous depth buffer state
            graphicsDevice.DepthStencilState = previousDepthStencilState;
        }
        #endregion

        #region Command Execution
        protected override void Execute()
        {
            if (placementFinished)
                return;

            if (selectedPolygon is null)
            {
                MessageBox.Show("No polygon selected. Please click inside a segmented polygon to finalize.",
                    "No Polygon Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Validate polygon
                if (selectedPolygon.ExteriorRing.Length < 3)
                {
                    MessageBox.Show("Invalid polygon (less than 3 points).",
                        "Invalid Polygon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                this.Output = selectedPolygon;
                placementFinished = true;
                this?.success_callback(selectedPolygon);
                // Create structure and location using the selected polygon
                //CreateAnnotationFromPolygon(selectedPolygon);

                // Clean up and deactivate
                CleanupCommand();
                Deactivated = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing segmentation command: {ex.Message}");
                MessageBox.Show($"Error creating annotation: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            base.Execute();
        }

        public static void CreateAnnotationFromPolygon(Viking.UI.Controls.SectionViewerControl Parent, StructureType? type, Polygon polygon)
        {
            StructureTypeObj typeObj = GetDefaultStructureType(type);
            // Create structure
            StructureObj newStruct = new(typeObj);

            // Create location with polygon type
            LocationObj newLocation = new(
                newStruct,
                Parent.Section.Number,
                Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON);

            try
            {
                // Set the polygon geometry
                // SetShapeFromGeometryInSection will transform the mosaic shape to volume coordinates
                SqlGeometry mosaicGeometry = polygon.ToSqlGeometry();
                newLocation.SetShapeFromGeometryInVolume(Parent.Section.ActiveSectionToVolumeTransform, mosaicGeometry);

                // Enqueue command to save the structure
                Parent.CommandQueue.EnqueueCommand(
                    typeof(CreateNewStructureCommand),
                    [Parent, newStruct, newLocation]);
            }
            catch (ArgumentException e)
            {
                MessageBox.Show($"Could not create polygon: {e.Message}",
                    "Error Creating Annotation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Gets the default structure type to use for new annotations if the provided type is null.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static StructureTypeObj GetDefaultStructureType(StructureType? type = null)
        {
            if (type is not null)
                return type.modelObj;

            // Try to get from state
            if (Viking.UI.State.SelectedObject is not StructureType result)
            {
                return Store.StructureTypes[1];
            }
            else
            {
                return result.modelObj;
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Clears segmentation results (mask, polygons) while preserving points.
        /// Used when the last foreground point is removed.
        /// </summary>
        private void ClearSegmentationResults()
        {
            // Clear rendered mesh and polygons
            segmentPolygonViews.Clear();
            segmentPolygonRingViews.Clear();
            hoveredPolygonView = null;
            maskOverlayView = null;
            currentMaskData = null;
            maskTexture?.Dispose();
            maskTexture = null;
            selectedPolygon = null;
            lastSegmentationResponse = null;

            // Trigger redraw to update display
            Parent.Invalidate();

            Debug.WriteLine("Segmentation results cleared (no foreground points remaining)");
        }

        /// <summary>
        /// Aborts decode/polygonize for the last SegmentImage result. Does not cancel the RPC itself.
        /// </summary>
        private void CancelProcessSegmentationResponse()
        {
            try
            {
                processResponseCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            processResponseCts?.Dispose();
            processResponseCts = null;
        }

        private void CleanupCommand()
        {
            requestCoalescer.Invalidate();
            CancelProcessSegmentationResponse();
            foregroundPoints.Clear();
            backgroundPoints.Clear();
            // Clear point views but keep them initialized (never null)
            foregroundPointsView.Points = [];
            backgroundPointsView.Points = [];
            currentMaskData = null;
            maskTexture?.Dispose();
            maskTexture = null;
            maskOverlayView = null;
            segmentPolygonViews.Clear();
            segmentPolygonRingViews.Clear();
            hoveredPolygonView = null;
            selectedPolygon = null;
            lastSegmentationResponse = null;

            viewportSession.CancelPendingWork();
            viewportSession.ClearImageId();
        }
        #endregion
    }
}
