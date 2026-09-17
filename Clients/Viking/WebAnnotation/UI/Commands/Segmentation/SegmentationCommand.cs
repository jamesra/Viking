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
using Viking.gRPC.SegmentationServiceTypes.V1;

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
        private readonly List<GridVector2> foregroundPoints = [];
        private readonly List<GridVector2> backgroundPoints = [];

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
        private bool isSegmenting = false;
        private int maskWidth;
        private int maskHeight;
        private GridPolygon selectedPolygon; // Track the polygon clicked for finalization
        private SegmentationServiceTypes.SegmentationResponse lastSegmentationResponse;

        // Pan/zoom tracking
        private GridRectangle lastViewBounds;
        private System.Timers.Timer panZoomDebounceTimer;

        // Rendering
        private readonly Color maskColor = new(255, 128, 0, 128); // Orange with transparency

        // Configuration 
        private readonly int debounceMs;

        // Structure type for created annotations
        //private readonly StructureTypeObj structureType;

        /// <summary>
        /// When set, background points are computed from visible same-type annotations in OnActivate and on pan/zoom.
        /// </summary>
        private readonly long? structureTypeIdForBackgroundPoints;

        /// <summary>
        /// Location being converted must not receive an avoid (background) mark; those marks are for other annotations in view.
        /// </summary>
        private readonly long? locationIdToExcludeFromBackgroundPoints;

        /// <summary>
        /// Set to the segmented polygon if the command completes successfully
        /// </summary>
        public GridPolygon Output
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
                List<string> s = [.. DefaultMouseHelpStrings, .. Viking.UI.Commands.Command.DefaultKeyHelpStrings];
                s.Sort();
                return [.. s];
            }
        }

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);


        /// <summary>
        /// Return the approved polygon
        /// </summary>
        /// <param name="output"></param>
        public delegate void OnCommandSuccess(GridPolygon output);
        private readonly OnCommandSuccess success_callback;

        #endregion

        #region Constructor
        public SegmentationCommand(SectionViewerControl parent,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long? structureTypeId = null,
            long? excludeLocationId = null) : base(parent)
        {
            this.success_callback = success_callback;

            // Load configuration from AppSettings
            debounceMs = int.TryParse(ConfigurationManager.AppSettings["SegmentationDebounceMs"], out var ms) ? ms : DEFAULT_DEBOUNCE_MS;

            Parent.Cursor = Cursors.Cross;

            viewportSession = new SegmentationViewportSession(parent);

            structureTypeIdForBackgroundPoints = structureTypeId;
            locationIdToExcludeFromBackgroundPoints = excludeLocationId;
        }

        /// <summary>
        /// Constructor that accepts initial foreground and background points for automated segmentation
        /// </summary>
        public SegmentationCommand(SectionViewerControl parent,
            IEnumerable<GridVector2> initialForegroundPoints,
            IEnumerable<GridVector2> initialBackgroundPoints,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long? structureTypeId = null,
            long? excludeLocationId = null) : this(parent, success_callback, grpcChannelManager, structureTypeId, excludeLocationId)
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

        private void AddBackgroundPointsFromStructureType(long structureTypeId, VikingXNA.Scene scene)
        {
            var sectionAnnotations = AnnotationOverlay.GetOrCreateAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations is null)
                return;

            var locationsInView = sectionAnnotations.GetLocations(scene.VisibleWorldBounds);
            var visibleSameType = locationsInView
                .Where(loc => loc != null && loc.Parent != null && loc.Parent.Type != null
                    && loc.Parent.Type.modelObj.ID == structureTypeId
                    && loc.ID != locationIdToExcludeFromBackgroundPoints
                    && loc.IsVisible(scene));

            var locationObjs = visibleSameType
                .Select(loc => Store.Locations.GetObjectByID(loc.ID, false))
                .OfType<LocationObj>();

            var mosaicPoints = AnnotationPointExtensions.GetAnnotationRepresentativePoints(locationObjs);
            if (mosaicPoints.Count == 0)
                return;

            var success = Parent.Section.ActiveSectionToVolumeTransform.TrySectionToVolume([.. mosaicPoints], out var volumePoints);
            var validVolumePoints = volumePoints.Where((p, i) => i < success.Length && success[i]).ToList();
            backgroundPoints.AddRange(validVolumePoints);
        }
        #endregion

        #region Lifecycle Methods
        public override void OnActivate()
        {
            base.OnActivate();

            if (structureTypeIdForBackgroundPoints.HasValue && Parent.Scene != null)
            {
                AddBackgroundPointsFromStructureType(structureTypeIdForBackgroundPoints.Value, Parent.Scene);
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
                UploadCurrentImage().ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion && task.Result)
                    {
                        RequestSegmentation();
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        protected override void OnDeactivate()
        {
            viewportSession.CancelPendingWork();
            if (viewportSession.CurrentImageId.HasValue)
                _ = viewportSession.DeleteCurrentImageAsync();

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
            GridVector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
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

        private void HandlePointDeletion(List<GridVector2> pointList, GridVector2 worldPos)
        {
            GridVector2? pointToRemove = FindPointWithinRadius(pointList, worldPos, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius);
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
        private bool RemovePointsWithinRadius(List<GridVector2> pointList, GridVector2 worldPos, double radiusInScreenUnits)
        {
            GridVector2 screenPos = WorldToScreen(worldPos);
            double radiusSquared = radiusInScreenUnits * radiusInScreenUnits;
            bool anyRemoved = false;
            for (int i = pointList.Count - 1; i >= 0; i--)
            {
                GridVector2 ptScreen = WorldToScreen(pointList[i]);
                if (GridVector2.DistanceSquared(ptScreen, screenPos) <= radiusSquared)
                {
                    pointList.RemoveAt(i);
                    anyRemoved = true;
                }
            }
            return anyRemoved;
        }

        private void HandleForegroundPointAddition(GridVector2 worldPos)
        {
            //Check if we are clicking inside a foreground point
            if (ForegroundPointsContain(worldPos))
            {
                // Check if clicking inside existing polygon to execute (finalize)
                GridPolygon clickedPolygon = FindPolygonContainingPoint(worldPos);
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

        private void HandleBackgroundPointAddition(GridVector2 worldPos) => HandlePointAddition(backgroundPoints, worldPos);

        private void HandlePointAddition(List<GridVector2> pointList, GridVector2 worldPos)
        {
            // Check for overlapping point
            GridVector2? existingPoint = FindPointWithinRadius(pointList, worldPos, WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius);
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
                UploadCurrentImage().ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion && task.Result)
                    {
                        RequestSegmentation();
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
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
            GridVector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
            
            // Check if mouse is over a foreground or background point
            // Convert world-space radius to screen-space radius for detection
            double pointRadiusInWorld = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
            double pointRadiusInScreen = pointRadiusInWorld;
            GridVector2? foregroundPoint = FindPointWithinRadius(foregroundPoints, worldPos, pointRadiusInScreen);
            GridVector2? backgroundPoint = FindPointWithinRadius(backgroundPoints, worldPos, pointRadiusInScreen);
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
        /// Deletes the cached SAM2 image when the view moves more than 1%. Auto-polygonize does not use this path;
        /// it keeps an already-uploaded image and only cancels pre-upload work.
        /// </summary>
        private void CheckForViewportChange()
        {
            GridRectangle currentBounds = viewportSession.GetCurrentViewportBounds();

            if (!SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, currentBounds))
            {
                lastViewBounds = currentBounds;
                viewportSession.ViewportBounds = currentBounds;
                viewportSession.CancelPendingWork();

                if (viewportSession.CurrentImageId.HasValue)
                    _ = viewportSession.DeleteCurrentImageAsync();

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
                    AddBackgroundPointsFromStructureType(structureTypeIdForBackgroundPoints.Value, Parent.Scene);
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

        private GridVector2? FindPointWithinRadius(List<GridVector2> points, GridVector2 worldPos, double radiusInScreenUnits)
        {
            // Convert world position to screen coordinates
            GridVector2 screenPos = WorldToScreen(worldPos);
            double radiusSquared = radiusInScreenUnits * radiusInScreenUnits;

            // Search for a point within the radius
            foreach (var pt in points)
            {
                GridVector2 ptScreen = WorldToScreen(pt);
                double distSq = GridVector2.DistanceSquared(ptScreen, screenPos);
                if (distSq <= radiusSquared)
                {
                    return pt;
                }
            }

            return null;
        }

        private void RemoveNearestPoint(GridVector2 worldPos)
        {
            const double searchRadiusSquared = 100.0; // 10 pixel radius squared

            // Find nearest foreground point
            GridVector2? nearestFg = null;
            double nearestFgDistSq = double.MaxValue;
            foreach (var pt in foregroundPoints)
            {
                double distSq = GridVector2.DistanceSquared(pt, worldPos);
                if (distSq < nearestFgDistSq && distSq < searchRadiusSquared)
                {
                    nearestFgDistSq = distSq;
                    nearestFg = pt;
                }
            }

            // Find nearest background point
            GridVector2? nearestBg = null;
            double nearestBgDistSq = double.MaxValue;
            foreach (var pt in backgroundPoints)
            {
                double distSq = GridVector2.DistanceSquared(pt, worldPos);
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

        private SolidPolygonView FindPolygonViewContainingPoint(GridVector2 worldPos) =>
            segmentPolygonViews.FirstOrDefault(polygonView =>
                polygonView?.InputPolygon != null && polygonView.InputPolygon.Contains(worldPos));

        private GridPolygon FindPolygonContainingPoint(GridVector2 worldPos) =>
            FindPolygonViewContainingPoint(worldPos)?.InputPolygon;

        /// <summary>
        /// Returns the point that contains the worldPos parameter.  Otherwise null
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        private bool ForegroundPointsContain(GridVector2 worldPos) => foregroundPointsView.Points.Any(p => GridCircle.Contains(p, foregroundPointsView.PointRadius, worldPos) == ShapeRelation.CONTAINED);
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
        private Task<bool> UploadCurrentImage()
        {
            viewportSession.ViewportBounds = viewportSession.GetCurrentViewportBounds();
            return viewportSession.UploadCurrentImageAsync(CancellationToken.None);
        }

        private Task DeleteCurrentImage() => viewportSession.DeleteCurrentImageAsync();
        #endregion

        #region gRPC Segmentation
        private async Task RequestSegmentation()
        {
            if (isSegmenting || !viewportSession.HasClient)
                return;

            if (foregroundPoints.Count == 0 && backgroundPoints.Count == 0)
                return;

            if (viewportSession.IsUploading)
            {
                Debug.WriteLine("Upload in progress, segmentation will be requested after upload completes");
                return;
            }

            isSegmenting = true;
            try
            {
                Debug.WriteLine($"Sending segmentation request with image ID {viewportSession.CurrentImageId}: {viewportSession.UploadedImageWidth}x{viewportSession.UploadedImageHeight}, {foregroundPoints.Count} fg, {backgroundPoints.Count} bg points");
                var response = await viewportSession.SegmentAsync(foregroundPoints, backgroundPoints, CancellationToken.None).ConfigureAwait(false);
                if (response is not null)
                    await Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => ProcessSegmentationResponse(response)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Segmentation error: {ex.Message}");
            }
            finally
            {
                isSegmenting = false;
            }
        }

        private void ProcessSegmentationResponse(SegmentationServiceTypes.SegmentationResponse response)
        {
            if (response.Segments.Count == 0)
            {
                Debug.WriteLine("No segments returned");
                return;
            }

            lastSegmentationResponse = response;
            ConvertSegmentsToPolygonViews(response);

#if DEBUG
            CreateDebugMaskOverlay(response);
#endif

            // Invalidate to trigger redraw
            Parent.Invalidate();
        }

        /// <summary>
        /// Converts protobuf segments to GridPolygons and creates colored polygon views
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
            segmentPolygonViews.Clear();
            segmentPolygonRingViews.Clear();
            hoveredPolygonView = null;

            int totalPolygons = response.Segments.Count;
            int polygonIndex = 0;

            IReadOnlyList<GridPolygon> polygons = viewportSession.CreatePolygonsFromResponse(
                response,
                holeDropFraction,
                backgroundPoints,
                edgeCleanupRadius);
            foreach (GridPolygon gridPolygon in polygons)
            {
                Color polygonColor = GenerateDistinctColor(polygonIndex, totalPolygons);
                segmentPolygonViews.Add(new SolidPolygonView(gridPolygon, polygonColor));
                segmentPolygonRingViews.Add(CreateRingView(gridPolygon.ExteriorRing, polygonColor));
                segmentPolygonRingViews.AddRange(gridPolygon.InteriorRings.Select(ring => CreateRingView(ring, polygonColor)));
                polygonIndex++;
            }

            Debug.WriteLine($"Created {segmentPolygonViews.Count} polygon views");
        }

        private CurveView CreateRingView(IEnumerable<GridVector2> ring, Color color) =>
            new(
                [.. ring],
                color.SetAlpha(0.65f),
                TryToClose: true,
                numInterpolations: 0,
                lineWidth: Math.Max(1.0, Parent.Downsample * 2.0),
                lineStyle: LineStyle.Tubular,
                ShowControlPoints: false);

        /// <summary>
        /// Converts a protobuf polygon to GridPolygon with Y-axis inversion
        /// </summary>
        private GridPolygon ConvertProtoPolygonToGridPolygon(
            SegmentationServiceTypes.Polygon protoPolygon,
            SegmentationServiceTypes.SegmentationResponse response)
        {
            try
            {
                // Invert Y coordinates: Viking uses bottom-left origin, server uses top-left
                Polygon invertedProtoPolygon = new()
                {
                    Points = { protoPolygon.Points.Select(p => new SegmentationServiceTypes.Point
                    {
                        X = p.X,
                        Y = response.Height - p.Y
                    }) }
                };
                return invertedProtoPolygon.ToGridPolygon(viewportSession.ViewportBounds, response.Width, response.Height);
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
                GridVector2 topLeft = viewportSession.ViewportToWorld(bestSegment.X, response.Height - bestSegment.Y, viewportSession.UploadedImageWidth, viewportSession.UploadedImageHeight);
                GridVector2 bottomRight = viewportSession.ViewportToWorld(
                    bestSegment.X + decodedWidth,
                    (response.Height - bestSegment.Y) - decodedHeight,
                    viewportSession.UploadedImageWidth,
                    viewportSession.UploadedImageHeight
                );
                GridRectangle segmentBounds = new(topLeft, bottomRight);
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

        private bool IsPointInsideMask(GridVector2 worldPos)
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
        private GridVector2 WorldToScreen(GridVector2 worldPos) => Parent.WorldToScreen(worldPos.X, worldPos.Y);
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

        public static void CreateAnnotationFromPolygon(Viking.UI.Controls.SectionViewerControl Parent, StructureType? type, GridPolygon polygon)
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

        private void CleanupCommand()
        {
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
