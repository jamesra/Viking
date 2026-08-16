using Geometry;
using Rectangle = Geometry.Rectangle;
using Grpc.Core;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
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
using WebAnnotationModel.Objects;
using WebAnnotation.ViewModel;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Polygon = Geometry.Polygon;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
        private const double SIMPLIFICATION_TOLERANCE = 2.0; // pixels
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

        // gRPC client - channel is now shared via Global.SegmentationChannelManager
        private SegmentationServiceTypes.SegmentationService.SegmentationServiceClient grpcClient;

        // Segmentation state
        private byte[] currentMaskData;
        private Texture2D maskTexture;
        private Rectangle viewportBounds;
        private bool isSegmenting = false;
        private int maskWidth;
        private int maskHeight;
        private Polygon selectedPolygon; // Track the polygon clicked for finalization

        // Pan/zoom tracking
        private Rectangle lastViewBounds;
        private System.Timers.Timer panZoomDebounceTimer;

        // Uploaded image tracking (for coordinate mapping)
        private int uploadedImageWidth;
        private int uploadedImageHeight;
        private CancellationTokenSource renderCancellationTokenSource;
        private CancellationTokenSource linkedRenderCancellationTokenSource;

        // Server-side image caching
        private ulong? currentImageId;
        private CancellationTokenSource uploadCancellationTokenSource;
        private Rectangle? uploadedImageBounds;
        private int isUploadingImage = 0; // 0 = false, 1 = true (for Interlocked operations)

        // Rendering
        private readonly Color maskColor = new(255, 128, 0, 128); // Orange with transparency

        // Configuration 
        private readonly int debounceMs;

        // Structure type for created annotations
        //private readonly StructureTypeObj structureType;

        /// <summary>
        /// When set, background points are computed from visible same-type annotations in OnActivate and on pan/zoom.
        /// </summary>
        private readonly long[]? structureTypeIdsForBackgroundPoints;

        private readonly HashSet<long> includedStructureIds;

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
        public delegate void OnCommandSuccess(Polygon output);
        private readonly OnCommandSuccess success_callback;

        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="success_callback"></param>
        /// <param name="grpcChannelManager"></param>
        /// <param name="excludestructureTypeIds">Structures of this type are excluded from the segmentation by adding background points</param>
        /// <param name="excludeStructureIds">Structures of this type are included in the segmentation by including foreground points.
        /// If they are of a type also included in excludeStructureTypeIds then that structure instance is not included in the set of background points contributed by that type.</param>
        public SegmentationCommand(SectionViewerControl parent,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long[]? excludestructureTypeIds = null,
            long[]? includeStructureIds = null) : base(parent)
        {
            this.success_callback = success_callback;

            // Load configuration from AppSettings
            debounceMs = int.TryParse(ConfigurationManager.AppSettings["SegmentationDebounceMs"], out var ms) ? ms : DEFAULT_DEBOUNCE_MS;

            Parent.Cursor = Cursors.Cross;

            // Initialize viewport bounds
            viewportBounds = GetCurrentViewportBounds();

            structureTypeIdsForBackgroundPoints = excludestructureTypeIds;
            includedStructureIds = new HashSet<long>();
            if (includeStructureIds != null) {
                includedStructureIds.UnionWith(includeStructureIds);
            }
        }

        /// <summary>
        /// Constructor that accepts initial foreground and background points for automated segmentation
        /// </summary>
        public SegmentationCommand(SectionViewerControl parent,
            IEnumerable<Geometry.Vector2> initialForegroundPoints,
            IEnumerable<Geometry.Vector2> initialBackgroundPoints,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            long[]? excludestructureTypeIds = null, 
            long[]? includeStructureIds = null) : this(parent, success_callback, grpcChannelManager, excludestructureTypeIds, includeStructureIds)
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

        private void AddBackgroundPointsFromStructureType(long structureTypeId, HashSet<long> exemptStructureIds, VikingXNA.Scene scene)
        {
            var sectionAnnotations = AnnotationOverlay.GetOrCreateAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations is null)
                return;

            var locationsInView = sectionAnnotations.GetLocations(scene.VisibleWorldBounds);
            var visibleSameType = locationsInView
                .Where(loc => loc != null && loc.Parent != null && loc.Parent.Type != null
                    && loc.Parent.Type.modelObj.ID == structureTypeId
                    && !exemptStructureIds.Contains(loc.Parent.ID) 
                    && loc.IsVisible(scene));

            var locationObjs = visibleSameType
                .Select(loc => Store.Locations.TryGetObjectByID(loc.ID, out var o) ? o : null)
                .OfType<LocationObj>();

            var mosaicPoints = AnnotationPointExtensions.GetAnnotationRepresentativePoints(locationObjs);
            if (mosaicPoints.Count == 0)
                return;

            var success = Parent.Section.ActiveSectionToVolumeTransform.TrySectionToVolume([.. mosaicPoints], out var volumePoints);
            var validVolumePoints = volumePoints.Where((p, i) => i < success.Length && success[i]).ToList();
            backgroundPoints.AddRange(validVolumePoints);
        }

        private void AddBackgroundPointsFromStructureTypes(long[]? structureTypeIds, HashSet<long> exemptStructureIds, VikingXNA.Scene scene)
        {
            if(structureTypeIds is null)
                return;

            foreach(long id in structureTypeIds)
            {
                AddBackgroundPointsFromStructureType(id, exemptStructureIds, scene);
            }
        }
        #endregion

        #region Lifecycle Methods
        public override void OnActivate()
        {
            base.OnActivate();

            if (structureTypeIdsForBackgroundPoints is not null && Parent.Scene is not null)
            {
                AddBackgroundPointsFromStructureTypes(structureTypeIdsForBackgroundPoints, includedStructureIds, Parent.Scene);
            }

            try
            {
                // Use shared gRPC channel from service locator
                var channel = ServiceLocator.GrpcChannelManager?.GetOrCreateChannel();
                if (channel is null)
                {
                    throw new InvalidOperationException("Segmentation service URL is not configured");
                }

                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(channel);

                Debug.WriteLine($"SegmentationCommand activated. Using shared channel to {channel.ResolvedTarget}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize gRPC client: {ex.Message}");
            }

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

            lastViewBounds = GetCurrentViewportBounds();
            UpdatePointViews();

            // Initialize pan/zoom debounce timer
            panZoomDebounceTimer = new System.Timers.Timer(debounceMs);
            panZoomDebounceTimer.Elapsed += OnPanZoomDebounceElapsed;
            panZoomDebounceTimer.AutoReset = false;

            // If we have initial points, automatically upload image and request segmentation
            if (hasInitialPoints)
            {
                Debug.WriteLine($"SegmentationCommand activated with {foregroundPoints.Count} foreground and {backgroundPoints.Count} background points");
                UploadThenRequestSegmentationAsync();
            }
        }

        protected override void OnDeactivate()
        {
            // Cancel any ongoing upload
            uploadCancellationTokenSource?.Cancel();

            // Delete the current image from server cache
            if (currentImageId.HasValue)
            {
                DeleteCurrentImage();
            }

            // Clean up resources
            CleanupCommand();

            // Note: We no longer shut down the channel here - it's shared!
            // Just null out the client reference
            grpcClient = null;

            panZoomDebounceTimer?.Dispose();
            panZoomDebounceTimer = null;

            uploadCancellationTokenSource?.Dispose();
            uploadCancellationTokenSource = null;

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
            bool currentlyUploading = Interlocked.CompareExchange(ref isUploadingImage, 0, 0) != 0;
            if (isFirstPoint && !currentImageId.HasValue && !currentlyUploading)
            {
                Debug.WriteLine("First point placed, uploading image to server cache");
                UploadThenRequestSegmentationAsync();
            }
            else
            {
                RequestSegmentation();
            }
        }

        private async void UploadThenRequestSegmentationAsync()
        {
            try
            {
                if (await UploadCurrentImage().ConfigureAwait(true))
                    RequestSegmentation();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"SegmentationCommand UploadThenRequestSegmentation failed: {ex}", "SegmentationCommand");
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
            Geometry.Vector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
            
            // Check if mouse is over a foreground or background point
            // Convert world-space radius to screen-space radius for detection
            double pointRadiusInWorld = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
            double pointRadiusInScreen = pointRadiusInWorld;
            Geometry.Vector2? foregroundPoint = FindPointWithinRadius(foregroundPoints, worldPos, pointRadiusInScreen);
            Geometry.Vector2? backgroundPoint = FindPointWithinRadius(backgroundPoints, worldPos, pointRadiusInScreen);
            
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
        private void CheckForViewportChange()
        {
            Rectangle currentBounds = GetCurrentViewportBounds();

            // Check if viewport has changed significantly
            if (!AreViewportBoundsSimilar(lastViewBounds, currentBounds))
            {
                lastViewBounds = currentBounds;
                viewportBounds = currentBounds;

                // Cancel any ongoing render operation
                linkedRenderCancellationTokenSource?.Cancel();
                linkedRenderCancellationTokenSource?.Dispose();
                linkedRenderCancellationTokenSource = null;

                renderCancellationTokenSource?.Cancel();

                // Cancel any ongoing image upload
                uploadCancellationTokenSource?.Cancel();

                // Delete the current image from server cache asynchronously
                if (currentImageId.HasValue)
                {
                    DeleteCurrentImage();
                }

                // Restart debounce timer
                panZoomDebounceTimer?.Stop();
                panZoomDebounceTimer?.Start();
            }
        }

        private bool AreViewportBoundsSimilar(Rectangle a, Rectangle b)
        {
            // Check if bounds are within 1% of each other
            double tolerance = Math.Max(a.Width, a.Height) * 0.01;
            return Math.Abs(a.LowerLeft.X - b.LowerLeft.X) < tolerance &&
                   Math.Abs(a.LowerLeft.Y - b.LowerLeft.Y) < tolerance &&
                   Math.Abs(a.UpperRight.X - b.UpperRight.X) < tolerance &&
                   Math.Abs(a.UpperRight.Y - b.UpperRight.Y) < tolerance;
        }

        private void OnPanZoomDebounceElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // User has stopped panning/zooming
            // Recompute structure-type background points when visible set changes (replaces any previously derived points)
            if (structureTypeIdsForBackgroundPoints is not null && Parent.Scene is not null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    backgroundPoints.Clear();
                    AddBackgroundPointsFromStructureTypes(structureTypeIdsForBackgroundPoints, includedStructureIds, Parent.Scene);
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
            // Update foreground points view (always exists, never null)
            double pointRadius = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius * Parent.Downsample;
            foregroundPointsView.PointRadius = pointRadius;
            foregroundPointsView.Points = [.. foregroundPoints];
            foregroundPointsView.UpdateViews();

            // Update background points view (always exists, never null)
            backgroundPointsView.PointRadius = pointRadius;
            backgroundPointsView.Points = [.. backgroundPoints];
            backgroundPointsView.UpdateViews();

            Parent.Invalidate(); // Trigger redraw
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

        private Polygon FindPolygonContainingPoint(Geometry.Vector2 worldPos) =>
            // Check each segment polygon to see if the point is inside
            segmentPolygonViews.FirstOrDefault(polygonView => polygonView?.InputPolygon != null && polygonView.InputPolygon.Covers(worldPos))?.InputPolygon;

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
        /// Upload an image to the server.  Returns true if successful
        /// </summary>
        /// <returns></returns>
        private async Task<bool> UploadCurrentImage()
        {
            if (grpcClient is null)
                return false;

            // Atomically check and set isUploadingImage from 0 to 1
            // Returns 0 if it was 0 (success), or 1 if it was already 1 (another upload in progress)
            if (Interlocked.CompareExchange(ref isUploadingImage, 1, 0) != 0)
                return false;

            try
            {
                // Cancel any existing upload
                uploadCancellationTokenSource?.Cancel();
                uploadCancellationTokenSource?.Dispose();
                uploadCancellationTokenSource = new CancellationTokenSource();

                // Capture current viewport image
                var (imageData, width, height) = await CaptureViewportImage(uploadCancellationTokenSource.Token).ConfigureAwait(false);
                if (imageData is null || imageData.Length == 0)
                {
                    Debug.WriteLine("Failed to capture viewport image for upload");
                    return false;
                }

                // Build gRPC upload request
                UploadImageRequest uploadRequest = new()
                {
                    ImageData = Google.Protobuf.ByteString.CopyFrom(imageData),
                    Width = width,
                    Height = height
                };

                Debug.WriteLine($"Uploading image to server cache: {width}x{height}, {imageData.Length} bytes");

                // Call gRPC service with cancellation token and timeout
                CallOptions callOptions = new(
                    deadline: DateTime.UtcNow.AddSeconds(30),
                    cancellationToken: uploadCancellationTokenSource.Token);

                var uploadResponse = await grpcClient.UploadImageAsync(uploadRequest, callOptions).ResponseAsync.ConfigureAwait(false);

                // Store the image ID, bounds, and dimensions
                currentImageId = uploadResponse.ImageId;
                uploadedImageBounds = viewportBounds;
                uploadedImageWidth = width;
                uploadedImageHeight = height;

                Debug.WriteLine($"Image uploaded successfully: ID={currentImageId}, dimensions={width}x{height}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Image upload cancelled due to view change");
                currentImageId = null;
                uploadedImageBounds = null;
            }
            catch (RpcException rpcEx)
            {
                Debug.WriteLine($"gRPC error during upload: {rpcEx.Status.Detail}");
#if DEBUG
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"Failed to upload image to segmentation service: {rpcEx.Status.Detail}",
                        "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
#endif
                currentImageId = null;
                uploadedImageBounds = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading image: {ex.Message}");
#if DEBUG
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"Error uploading image: {ex.Message}", "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
#endif
                currentImageId = null;
                uploadedImageBounds = null;
            }
            finally
            {
                Interlocked.Exchange(ref isUploadingImage, 0);
            }

            return false;
        }

        private async Task DeleteCurrentImage()
        {
            if (!currentImageId.HasValue || grpcClient is null)
                return;

            ulong imageIdToDelete = currentImageId.Value;
            currentImageId = null;
            uploadedImageBounds = null;

            try
            {
                DeleteImageRequest deleteRequest = new()
                {
                    ImageId = imageIdToDelete
                };

                Debug.WriteLine($"Deleting image from server cache: ID={imageIdToDelete}");

                // Call gRPC service with timeout (fire and forget, don't block UI)
                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(5));
                var deleteResponse = await grpcClient.DeleteImageAsync(deleteRequest, callOptions).ResponseAsync.ConfigureAwait(false);

                Debug.WriteLine($"Image deleted from cache: ID={imageIdToDelete}, success={deleteResponse.Success}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting image from cache (ID={imageIdToDelete}): {ex.Message}");
                // Don't show error to user - this is a background cleanup operation
            }
        }
        #endregion

        #region gRPC Segmentation
        private async Task RequestSegmentation()
        {
            if (isSegmenting || grpcClient is null)
                return;

            if (foregroundPoints.Count == 0 && backgroundPoints.Count == 0)
                return;

            // If we don't have an uploaded image, upload one first
            bool currentlyUploading = Interlocked.CompareExchange(ref isUploadingImage, 0, 0) != 0;
            if (!currentImageId.HasValue && !currentlyUploading)
            {
                Debug.WriteLine("No cached image ID, uploading image first");
                var uploadResult = await UploadCurrentImage().ConfigureAwait(false);
                if (uploadResult)
                {
                    await RequestSegmentation().ConfigureAwait(false);
                }

                return;
            }

            // Wait for upload to complete if it's in progress
            if (currentlyUploading)
            {
                Debug.WriteLine("Upload in progress, segmentation will be requested after upload completes");
                return;
            }

            isSegmenting = true;

            try
            {
                var request = BuildSegmentationRequest();
                Debug.WriteLine($"Sending segmentation request with image ID {currentImageId}: {uploadedImageWidth}x{uploadedImageHeight}, {foregroundPoints.Count} fg, {backgroundPoints.Count} bg points");

                // Call gRPC service with timeout
                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(30));
                var response = await grpcClient.SegmentImageAsync(request, callOptions).ResponseAsync.ConfigureAwait(false);

                // Process response on UI thread
                await Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => ProcessSegmentationResponse(response)));
            }
            catch (RpcException rpcEx)
            {
                await HandleImageNotFoundError(rpcEx);
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

        /// <summary>
        /// Builds a gRPC segmentation request from current points
        /// </summary>
        private SegmentationServiceTypes.SegmentationRequest? BuildSegmentationRequest()
        {
            if (!currentImageId.HasValue) return null;
            SegmentationRequest request = new()
            {
                ImageId = currentImageId.Value,
                MultimaskOutput = false
            };

            int width = uploadedImageWidth;
            int height = uploadedImageHeight;

            // Add foreground points (label = 1)
            // Note: Y-axis is inverted - Viking uses bottom-left origin, server uses top-left
            foreach (var pt in foregroundPoints)
            {
                var screenPt = WorldToViewport(pt, width, height);
                request.Coordinates.Add(new SegmentationServiceTypes.Point
                {
                    X = (int)screenPt.X,
                    Y = height - (int)screenPt.Y
                });
                request.Labels.Add(1);
            }

            // Add background points (label = 0)
            foreach (var pt in backgroundPoints)
            {
                var screenPt = WorldToViewport(pt, width, height);
                request.Coordinates.Add(new SegmentationServiceTypes.Point
                {
                    X = (int)screenPt.X,
                    Y = height - (int)screenPt.Y
                });
                request.Labels.Add(0);
            }

            return request;
        }

        /// <summary>
        /// Handles the case where cached image was evicted from server
        /// </summary>
        private async Task HandleImageNotFoundError(RpcException rpcEx)
        {
            if (rpcEx.StatusCode == StatusCode.NotFound)
            {
                Debug.WriteLine($"Image not found in cache (evicted/expired), re-uploading and retrying: {rpcEx.Status.Detail}");

                // Clear the image ID
                currentImageId = null;
                uploadedImageBounds = null;

                // Re-upload the image and retry segmentation
                if (await UploadCurrentImage().ConfigureAwait(false))
                {
                    var request = BuildSegmentationRequest();
                    CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(30));
                    var response = await grpcClient.SegmentImageAsync(request, callOptions).ResponseAsync.ConfigureAwait(false);

                    // Process response on UI thread
                    await Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => ProcessSegmentationResponse(response)));
                }
            }
            else
            {
                Debug.WriteLine($"gRPC error: {rpcEx.Status.Detail}");
#if DEBUG
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show($"Segmentation service error: {rpcEx.Status.Detail}",
                        "Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
#endif
            }
        }

        private void ProcessSegmentationResponse(SegmentationServiceTypes.SegmentationResponse response)
        {
            if (response.Segments.Count == 0)
            {
                Debug.WriteLine("No segments returned");
                return;
            }

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
        private void ConvertSegmentsToPolygonViews(SegmentationServiceTypes.SegmentationResponse response)
        {
            // Clear existing polygon views
            segmentPolygonViews.Clear();

            // Count total polygons for color distribution
            int totalPolygons = response.Segments.Sum(s => s.Polygons?.Count ?? 0);
            int polygonIndex = 0;

            // Process all segments and their polygons
            foreach (var segment in response.Segments.OrderByDescending(s => s.Score))
            {
                if (segment.Polygons is null)
                    continue;

                Debug.WriteLine($"Processing segment with score: {segment.Score:F3}, {segment.Polygons.Count} polygons");

                // Convert each protobuf polygon to Polygon and create a view
                foreach (var protoPolygon in segment.Polygons)
                {
                    Polygon gridPolygon = ConvertProtoPolygonToGridPolygon(protoPolygon, response);

                    if (gridPolygon != null && gridPolygon.ExteriorRing.Length >= 3)
                    {
                        // Generate a distinct color for this polygon
                        Color polygonColor = GenerateDistinctColor(polygonIndex, totalPolygons);

                        // Create a SolidPolygonView
                        SolidPolygonView polygonView = new(gridPolygon, polygonColor);
                        segmentPolygonViews.Add(polygonView);

                        polygonIndex++;
                    }
                    else
                    {
                        Debug.WriteLine("Skipped invalid polygon (less than 3 points)");
                    }
                }
            }

            Debug.WriteLine($"Created {segmentPolygonViews.Count} polygon views");
        }

        /// <summary>
        /// Converts a protobuf polygon to Polygon with Y-axis inversion
        /// </summary>
        private Polygon ConvertProtoPolygonToGridPolygon(
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
                return invertedProtoPolygon.ToGridPolygon(viewportBounds, response.Width, response.Height);
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
            var (decodedMaskData, decodedWidth, decodedHeight) = DecodePngMask(pngBytes);
            
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
                Geometry.Vector2 topLeft = ViewportToWorld(bestSegment.X, response.Height - bestSegment.Y, uploadedImageWidth, uploadedImageHeight);
                Geometry.Vector2 bottomRight = ViewportToWorld(
                    bestSegment.X + decodedWidth,
                    (response.Height - bestSegment.Y) - decodedHeight,
                    uploadedImageWidth,
                    uploadedImageHeight
                );
                Rectangle segmentBounds = new(topLeft, bottomRight);
                maskOverlayView = new TextureOverlayView(maskTexture, segmentBounds, maskColor);
            }
        }
#endif
        #endregion

        #region Image Capture
        private async Task<(byte[]? data, int width, int height)> CaptureViewportImage(CancellationToken cancellationToken)
        {
            try
            {
                CancellationToken renderToken = PrepareCancellationToken(cancellationToken);

                var (graphicsDevice, scene, width, height) = ValidateRenderingContext();
                if (graphicsDevice is null || scene is null)
                {
                    return (null, 0, 0);
                }

                var (renderTarget, isGrayscale) = await RenderViewportToTexture(scene, width, height, renderToken).ConfigureAwait(false);
                if (renderTarget is null)
                {
                    return (null, 0, 0);
                }

                try
                {
                    // GetData must run on the UI/Graphics thread (GraphicsDevice affinity)
                    Color[] pixels = await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
                    {
                        Color[] p = new Color[width * height];
                        renderTarget.GetData(p);
                        return p;
                    }).Task.ConfigureAwait(false);

                    byte[] pngData = EncodeToPng(renderTarget, pixels, width, height, isGrayscale);

                    // Validate the captured image
                    var (isValid, errorMessage) = ValidateCapturedImage(pngData, width, height);
                    if (!isValid)
                    {
                        Debug.WriteLine($"Captured image failed validation: {errorMessage}");
                        return (null, 0, 0);
                    }

                    Debug.WriteLine($"Viewport image captured and validated as PNG ({pngData.Length} bytes, {width}x{height})");

#if DEBUG
                    // Save captured image to disk for debugging
                    SaveCapturedImageToDisk(pngData, width, height);
#endif

                    return (pngData, width, height);
                }
                finally
                {
                    Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => renderTarget?.Dispose()));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Viewport image capture was cancelled");
                return (null, 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing viewport: {ex.Message}");
                return (null, 0, 0);
            }
        }

        /// <summary>
        /// Validates that captured PNG image data is valid and can be fully decoded.
        /// Uses ImageSharp so validation can run on any thread without requiring the graphics device.
        /// </summary>
        /// <param name="pngData">The PNG image data to validate</param>
        /// <param name="expectedWidth">Expected width in pixels</param>
        /// <param name="expectedHeight">Expected height in pixels</param>
        /// <returns>Tuple with validation result and error message if invalid</returns>
        private (bool isValid, string errorMessage) ValidateCapturedImage(byte[] pngData, int expectedWidth, int expectedHeight)
        {
            if (pngData is null || pngData.Length == 0)
            {
                return (false, "Image validation failed: null or empty data");
            }

            // Check PNG magic bytes (89 50 4E 47 0D 0A 1A 0A)
            if (pngData.Length < 8 ||
                pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47 ||
                pngData[4] != 0x0D || pngData[5] != 0x0A || pngData[6] != 0x1A || pngData[7] != 0x0A)
            {
                return (false, "Image validation failed: invalid PNG signature");
            }

            // Verify minimum size constraints
            if (expectedWidth <= 0 || expectedHeight <= 0)
            {
                return (false, $"Image validation failed: invalid dimensions {expectedWidth}x{expectedHeight}");
            }

            try
            {
                using MemoryStream stream = new(pngData);
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);

                int decodedWidth = image.Width;
                int decodedHeight = image.Height;

                // Verify dimensions match expectations
                if (decodedWidth != expectedWidth || decodedHeight != expectedHeight)
                {
                    return (false, $"Image validation failed: dimension mismatch. Expected {expectedWidth}x{expectedHeight}, got {decodedWidth}x{decodedHeight}");
                }

                // Check pixel content for suspicious patterns (warnings only)
                bool isAllBlack = true;
                bool isAllSame = true;
                Rgba32 firstPixel = image[0, 0];
                int nonTransparentPixels = 0;
                int pixelCount = decodedWidth * decodedHeight;

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgba32 pixel = row[x];

                            if (pixel.R > 0 || pixel.G > 0 || pixel.B > 0)
                                isAllBlack = false;
                            if (pixel.A > 0)
                                nonTransparentPixels++;
                            if (pixel.R != firstPixel.R || pixel.G != firstPixel.G ||
                                pixel.B != firstPixel.B || pixel.A != firstPixel.A)
                                isAllSame = false;
                        }
                    }
                });

                if (isAllBlack && nonTransparentPixels > 0)
                    Debug.WriteLine($"Warning: Captured image appears to be completely black ({nonTransparentPixels} non-transparent pixels)");
                else if (isAllSame && pixelCount > 0)
                    Debug.WriteLine($"Warning: Captured image appears to be a solid color (R:{firstPixel.R}, G:{firstPixel.G}, B:{firstPixel.B}, A:{firstPixel.A})");

                Debug.WriteLine($"Image validation passed: {expectedWidth}x{expectedHeight}, {pngData.Length} bytes");
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Image validation failed: PNG decode error - {ex.Message}");
            }
        }

        private CancellationToken PrepareCancellationToken(CancellationToken? externalToken = null)
        {
            // Cancel and dispose any existing linked token source
            linkedRenderCancellationTokenSource?.Cancel();
            linkedRenderCancellationTokenSource?.Dispose();
            linkedRenderCancellationTokenSource = null;

            // Cancel and recreate render cancellation token source
            renderCancellationTokenSource?.Cancel();
            renderCancellationTokenSource?.Dispose();
            renderCancellationTokenSource = new CancellationTokenSource();

            // If external token provided, create linked token source
            if (externalToken.HasValue)
            {
                linkedRenderCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    externalToken.Value,
                    renderCancellationTokenSource.Token);
                return linkedRenderCancellationTokenSource.Token;
            }

            // Otherwise return the render token
            return renderCancellationTokenSource.Token;
        }

#if DEBUG
        /// <summary>
        /// Saves captured image to disk for debugging purposes
        /// </summary>
        /// <param name="pngData">The PNG image data to save</param>
        /// <param name="width">Image width in pixels</param>
        /// <param name="height">Image height in pixels</param>
        private void SaveCapturedImageToDisk(byte[] pngData, int width, int height)
        {
            try
            {
                // Create directory in temp folder
                string debugDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VikingSegmentation");
                if (!Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                }

                // Create filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"segmentation_capture_{timestamp}_{width}x{height}.png";
                string filepath = System.IO.Path.Combine(debugDir, filename);

                // Save the image
                File.WriteAllBytes(filepath, pngData);

                Debug.WriteLine($"Captured image saved to: {filepath}");
            }
            catch (Exception ex)
            {
                // Don't fail capture if save fails - just log the error
                Debug.WriteLine($"Failed to save captured image to disk: {ex.Message}");
            }
        }
#endif

        private (GraphicsDevice device, VikingXNA.Scene scene, int width, int height) ValidateRenderingContext()
        {
            var graphicsDevice = Parent.Device;
            if (graphicsDevice is null)
            {
                Debug.WriteLine("GraphicsDevice is null");
                return (null, null, 0, 0);
            }

            var scene = Parent.Scene;
            if (scene is null)
            {
                Debug.WriteLine("Scene is null");
                return (null, null, 0, 0);
            }

            int width = scene.Viewport.Width;
            int height = scene.Viewport.Height;

            if (width <= 0 || height <= 0)
            {
                Debug.WriteLine($"Invalid viewport dimensions: {width}x{height}");
                return (null, null, 0, 0);
            }

            return (graphicsDevice, scene, width, height);
        }

        private async Task<(RenderTarget2D? renderTarget, bool isGrayscale)> RenderViewportToTexture(VikingXNA.Scene scene, int width, int height, CancellationToken cancellationToken)
        {
            float centerX = scene.Camera.LookAt.X;
            float centerY = scene.Camera.LookAt.Y;
            int sectionZ = Parent.Section.Number;

            Debug.WriteLine($"Rendering scene to texture: {width}x{height}, center: ({centerX}, {centerY}), section: {sectionZ}");

            try
            {
                bool isGrayscale = Parent.CurrentChannelset.Length == 1;
                RenderTarget2D renderTarget = await Parent.RenderSceneToTexture(
                    scene,
                    centerX,
                    centerY,
                    sectionZ,
                    showOverlays: false,
                    asyncTextureLoad: false,
                    cancellationToken).ConfigureAwait(false);

                if (renderTarget is null)
                {
                    Debug.WriteLine("RenderSceneToTexture returned null");
                }

                return (renderTarget, isGrayscale);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("RenderSceneToTexture was cancelled");
                return (null, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenderSceneToTexture failed: {ex.Message}");
                return (null, false);
            }
        }

        private byte[] EncodeToPng(RenderTarget2D renderTarget, Color[] pixels, int width, int height, bool isGrayscale)
        {
            using MemoryStream pngStream = new();
            if (isGrayscale)
            {
                EncodeGrayscalePng(pngStream, pixels, width, height);
            }
            else
            {
                EncodeColorPng(pngStream, pixels, width, height);
            }

            return pngStream.ToArray();
        }

        private void EncodeGrayscalePng(MemoryStream pngStream, Color[] pixels, int width, int height)
        {
            // Build Rgba32 buffer: grayscale R=G=B from pixels[i].R, A from pixels[i].A
            byte[] buffer = new byte[width * height * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = pixels[i].R;
                int off = i * 4;
                buffer[off] = g;
                buffer[off + 1] = g;
                buffer[off + 2] = g;
                buffer[off + 3] = pixels[i].A;
            }
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(buffer, width, height);
            image.Save(pngStream, new PngEncoder());
            Debug.WriteLine("Image detected as grayscale, encoded as single-channel PNG");
        }

        private void EncodeColorPng(MemoryStream pngStream, Color[] pixels, int width, int height)
        {
            byte[] buffer = new byte[width * height * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                int off = i * 4;
                buffer[off] = pixels[i].R;
                buffer[off + 1] = pixels[i].G;
                buffer[off + 2] = pixels[i].B;
                buffer[off + 3] = pixels[i].A;
            }
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(buffer, width, height);
            image.Save(pngStream, new PngEncoder());
            Debug.WriteLine("Image detected as color, encoded as full-color PNG");
        }

        #endregion

        #region Mask Processing
        private (byte[] maskData, int width, int height) DecodePngMask(byte[] pngBytes)
        {
            try
            {
                if (pngBytes is null || pngBytes.Length == 0)
                {
                    Debug.WriteLine("Empty PNG mask data");
                    return (null, 0, 0);
                }

                // Load PNG using Texture2D.FromStream
                using MemoryStream stream = new(pngBytes);
                var graphicsDevice = Parent.Device;
                if (graphicsDevice is null)
                {
                    Debug.WriteLine("Graphics device is null");
                    return (null, 0, 0);
                }

                Texture2D pngTexture = Texture2D.FromStream(graphicsDevice, stream);
                int width = pngTexture.Width;
                int height = pngTexture.Height;

                // Extract pixel data as grayscale
                Color[] pixels = new Color[width * height];
                pngTexture.GetData(pixels);

                // Convert to grayscale byte array (0 or 255)
                byte[] maskData = new byte[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    // Use R channel since PNG is grayscale (R=G=B)
                    maskData[i] = pixels[i].R;
                }

                pngTexture.Dispose();
                return (maskData, width, height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding PNG mask: {ex.Message}");
                return (null, 0, 0);
            }
        }

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
                var screenPt = WorldToViewport(worldPos, maskWidth, maskHeight);
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

        /// <summary>
        /// Gets the current viewport bounds in world coordinates
        /// </summary>
        private Rectangle GetCurrentViewportBounds()
        {
            Geometry.Vector2 topLeft = Parent.ScreenToWorld(0, 0);
            Geometry.Vector2 bottomRight = Parent.ScreenToWorld(Parent.Width, Parent.Height);
            return new Rectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// Transforms world coordinates to viewport pixel coordinates (for sending to server)
        /// </summary>
        /// <param name="worldPos">Position in world coordinates</param>
        /// <param name="viewportWidth">Width of captured viewport image in pixels</param>
        /// <param name="viewportHeight">Height of captured viewport image in pixels</param>
        /// <returns>Position in viewport pixel coordinates</returns>
        private Geometry.Vector2 WorldToViewport(Geometry.Vector2 worldPos, int viewportWidth, int viewportHeight)
        {
            Geometry.Vector2 boundsMin = viewportBounds.LowerLeft;
            Geometry.Vector2 boundsMax = viewportBounds.UpperRight;

            // Normalize to [0,1] range within viewport bounds
            double normalizedX = (worldPos.X - boundsMin.X) / (boundsMax.X - boundsMin.X);
            double normalizedY = (worldPos.Y - boundsMin.Y) / (boundsMax.Y - boundsMin.Y);

            // Scale to viewport pixel dimensions
            return new Geometry.Vector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight
            );
        }

        /// <summary>
        /// Transforms viewport pixel coordinates to world coordinates (for receiving from server)
        /// </summary>
        /// <param name="pixelX">X coordinate in viewport pixels</param>
        /// <param name="pixelY">Y coordinate in viewport pixels</param>
        /// <param name="viewportWidth">Width of captured viewport image in pixels</param>
        /// <param name="viewportHeight">Height of captured viewport image in pixels</param>
        /// <returns>Position in world coordinates</returns>
        private Geometry.Vector2 ViewportToWorld(int pixelX, int pixelY, int viewportWidth, int viewportHeight)
        {
            // Normalize from pixel coordinates to [0,1] range
            double normalizedX = (double)pixelX / viewportWidth;
            double normalizedY = (double)pixelY / viewportHeight;

            Geometry.Vector2 boundsMin = viewportBounds.LowerLeft;
            Geometry.Vector2 boundsMax = viewportBounds.UpperRight;

            // Scale to world coordinates within viewport bounds
            return new Geometry.Vector2(
                boundsMin.X + normalizedX * (boundsMax.X - boundsMin.X),
                boundsMin.Y + normalizedY * (boundsMax.Y - boundsMin.Y)
            );
        }
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

            // Draw segment polygons first (underneath points)
            foreach (var polygonView in segmentPolygonViews)
            {
                polygonView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

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
            if (backgroundPointsView != null)
            {
                backgroundPointsView.Alpha = (float)(0.64 + backgroundPulse * 0.33); // Range: 0.0 to 1.0
                backgroundPointsView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            // Draw foreground points (green circles) with pulsing alpha
            if (foregroundPointsView != null)
            {
                foregroundPointsView.Alpha = (float)(0.64 + foregroundPulse * 0.33); // Range: 0.0 to 1.0
                foregroundPointsView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            

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

        private Polygon MaskToPolygon(byte[] maskData, int width, int height)
        {
            // Extract contour points from binary mask
            List<Geometry.Vector2> contourPoints = ExtractContourFromMask(maskData, width, height);

            if (contourPoints.Count < 3)
            {
                Debug.WriteLine("Not enough contour points extracted");
                return null;
            }

            // Simplify using Douglas-Peucker algorithm from Geometry package
            var simplifiedPoints = contourPoints.DouglasPeuckerReduction(SIMPLIFICATION_TOLERANCE);

            Debug.WriteLine($"Contour: {contourPoints.Count} points simplified to {simplifiedPoints.Count} points");

            // Create polygon
            return new Polygon(simplifiedPoints.EnsureClosedRing().RemoveAdjacentDuplicates());
        }

        private List<Geometry.Vector2> ExtractContourFromMask(byte[] maskData, int width, int height)
        {
            // Simple boundary extraction: find pixels on the edge of the mask
            List<Geometry.Vector2> boundaryPixels = [];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = y * width + x;
                    if (maskData[idx] > 0 && IsBoundaryPixel(maskData, x, y, width, height))
                    {
                        // Transform from pixel to world coordinates
                        Geometry.Vector2 worldPt = ViewportToWorld(x, y, width, height);
                        boundaryPixels.Add(worldPt);
                    }
                }
            }

            // Order points to form a coherent contour (simplified approach)
            // For production, use a proper contour tracing algorithm (marching squares)
            return OrderContourPoints(boundaryPixels);
        }

        private bool IsBoundaryPixel(byte[] mask, int x, int y, int width, int height)
        {
            int idx = y * width + x;
            if (mask[idx] == 0) return false;

            // Check 4-connectivity neighbors
            return mask[(y - 1) * width + x] == 0 ||     // top
                   mask[(y + 1) * width + x] == 0 ||     // bottom
                   mask[y * width + (x - 1)] == 0 ||     // left
                   mask[y * width + (x + 1)] == 0;       // right
        }

        private List<Geometry.Vector2> OrderContourPoints(List<Geometry.Vector2> points)
        {
            if (points.Count < 3)
                return points;

            // Simple ordering: start from leftmost point and find nearest unvisited neighbors
            // For production, implement proper contour following
            List<Geometry.Vector2> ordered = [];
            HashSet<Geometry.Vector2> remaining = [.. points];

            // Start with leftmost point
            Geometry.Vector2 current = points.OrderBy(p => p.X).First();
            ordered.Add(current);
            remaining.Remove(current);

            while (remaining.Count > 0)
            {
                // Find nearest remaining point
                Geometry.Vector2 nearest = remaining.OrderBy(p => Geometry.Vector2.DistanceSquared(current, p)).First();
                ordered.Add(nearest);
                remaining.Remove(nearest);
                current = nearest;
            }

            return ordered;
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
            maskOverlayView = null;
            currentMaskData = null;
            maskTexture?.Dispose();
            maskTexture = null;
            selectedPolygon = null;

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
            selectedPolygon = null;

            // Clear server-side cache references and dimensions
            currentImageId = null;
            uploadedImageBounds = null;
            uploadedImageWidth = 0;
            uploadedImageHeight = 0;
            Interlocked.Exchange(ref isUploadingImage, 0);

            // Cancel and dispose of cancellation token sources
            linkedRenderCancellationTokenSource?.Cancel();
            linkedRenderCancellationTokenSource?.Dispose();
            linkedRenderCancellationTokenSource = null;

            renderCancellationTokenSource?.Cancel();
            renderCancellationTokenSource?.Dispose();
            renderCancellationTokenSource = null;

            uploadCancellationTokenSource?.Cancel();
            uploadCancellationTokenSource?.Dispose();
            uploadCancellationTokenSource = null;
        }
        #endregion
    }
}
