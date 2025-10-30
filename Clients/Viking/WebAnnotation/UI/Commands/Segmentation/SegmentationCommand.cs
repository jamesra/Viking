using Geometry;
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
using WebAnnotationModel;
using WebAnnotation.ViewModel;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Interactive segmentation command that uses AI (SAM2) to segment images based on user-placed points.
    /// Users place foreground (green) and background (red) points, and the system generates a segmentation mask
    /// via gRPC, which can then be converted to a polygon annotation.
    /// </summary>
    internal class SegmentationCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        #region Constants
        private const double POINT_RADIUS = 5.0;
        private const double SIMPLIFICATION_TOLERANCE = 2.0; // pixels
        private const int DEFAULT_DEBOUNCE_MS = 500;
        #endregion

        #region Fields
        // Point collections
        private readonly List<GridVector2> foregroundPoints = new List<GridVector2>();
        private readonly List<GridVector2> backgroundPoints = new List<GridVector2>();

        // Monographics views for rendering
        private PointSetView foregroundPointsView;
        private PointSetView backgroundPointsView;
        private TextureOverlayView maskOverlayView;
        private List<SolidPolygonView> segmentPolygonViews = new List<SolidPolygonView>();

        // gRPC client
        private SegmentationServiceTypes.SegmentationService.SegmentationServiceClient grpcClient;
        private Channel grpcChannel;

        // Segmentation state
        private byte[] currentMaskData;
        private Texture2D maskTexture;
        private GridRectangle viewportBounds;
        private bool isSegmenting = false;
        private int maskWidth;
        private int maskHeight;
        private GridPolygon selectedPolygon; // Track the polygon clicked for finalization

        // Pan/zoom tracking
        private GridRectangle lastViewBounds;
        private System.Timers.Timer panZoomDebounceTimer;

        // Uploaded image tracking (for coordinate mapping)
        private int uploadedImageWidth;
        private int uploadedImageHeight;
        private CancellationTokenSource renderCancellationTokenSource;

        // Server-side image caching
        private ulong? currentImageId;
        private CancellationTokenSource uploadCancellationTokenSource;
        private GridRectangle? uploadedImageBounds;
        private bool isUploadingImage = false;

        // Rendering
        private readonly Color maskColor = new Color(255, 128, 0, 128); // Orange with transparency
        
        // Configuration
        private readonly string serviceUrl;
        private readonly int debounceMs;

        // Structure type for created annotations
        private readonly StructureTypeObj structureType;
        #endregion

        #region Help Strings
        public new static string[] DefaultMouseHelpStrings = new string[]
        {
            "Left-click: Add foreground point (green)",
            "Left-click inside polygon: Finalize and create annotation",
            "Middle-click: Remove nearest point",
            "Right-click: Add background point (red)",
            "Shift + Left-click: Delete foreground point",
            "Shift + Right-click: Delete background point"
        };

        public string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>();
                s.AddRange(DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        public ObservableCollection<string> ObservableHelpStrings => new ObservableCollection<string>(HelpStrings);
        #endregion

        #region Constructor
        public SegmentationCommand(SectionViewerControl parent, StructureType type = null) : base(parent)
        {
            structureType = type?.modelObj ?? (Viking.UI.State.SelectedObject as StructureType)?.modelObj;
            
            // Load configuration
            serviceUrl = ConfigurationManager.AppSettings["SegmentationServiceUrl"] ?? "localhost:50051";
            debounceMs = int.TryParse(ConfigurationManager.AppSettings["SegmentationDebounceMs"], out var ms) ? ms : DEFAULT_DEBOUNCE_MS;

            Parent.Cursor = Cursors.Cross;
            
            // Initialize viewport bounds
            viewportBounds = GetCurrentViewportBounds();
        }
        #endregion

        #region Lifecycle Methods
        public override void OnActivate()
        {
            base.OnActivate();

            try
            {
                // Initialize gRPC channel and client
                grpcChannel = new Channel(serviceUrl, ChannelCredentials.Insecure);
                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(grpcChannel);

                Debug.WriteLine($"SegmentationCommand activated. Connected to {serviceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize gRPC client: {ex.Message}");
                MessageBox.Show($"Failed to connect to segmentation service at {serviceUrl}. Please ensure the service is running.", 
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Clear any existing state
            foregroundPoints.Clear();
            backgroundPoints.Clear();
            lastViewBounds = GetCurrentViewportBounds();
            UpdatePointViews();

            // Initialize pan/zoom debounce timer
            panZoomDebounceTimer = new System.Timers.Timer(debounceMs);
            panZoomDebounceTimer.Elapsed += OnPanZoomDebounceElapsed;
            panZoomDebounceTimer.AutoReset = false;
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

            // Shutdown gRPC channel
            try
            {
                grpcChannel?.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error shutting down gRPC channel: {ex.Message}");
            }

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
            GridVector2 worldPos = Parent.ScreenToWorld(e.X, e.Y);
            bool shiftHeld = Control.ModifierKeys.HasFlag(Keys.Shift);

            if (e.Button.Left())
            {
                if (shiftHeld)
                {
                    // Shift + Left-click: Delete foreground point within POINT_RADIUS
                    GridVector2? pointToRemove = FindPointWithinRadius(foregroundPoints, worldPos, POINT_RADIUS);
                    if (pointToRemove.HasValue)
                    {
                        foregroundPoints.Remove(pointToRemove.Value);
                        UpdatePointViews();
                        
                        // If last foreground point was removed, clear rendered mesh and polygons
                        if (foregroundPoints.Count == 0)
                        {
                            ClearSegmentationResults();
                        }
                        else
                        {
                            RequestSegmentation();
                        }
                    }
                }
                else
                {
                    // Check if clicking inside existing polygon to execute (finalize)
                    GridPolygon clickedPolygon = FindPolygonContainingPoint(worldPos);
                    if (clickedPolygon != null)
                    {
                        selectedPolygon = clickedPolygon;
                        Execute();
                        return;
                    }

                    // Check for overlapping foreground point
                    GridVector2? existingPoint = FindPointWithinRadius(foregroundPoints, worldPos, POINT_RADIUS);
                    if (!existingPoint.HasValue)
                    {
                        // Check if this is the first point being added
                        bool isFirstPoint = (foregroundPoints.Count == 0 && backgroundPoints.Count == 0);
                        
                        // Add foreground point only if no overlap
                        foregroundPoints.Add(worldPos);
                        UpdatePointViews();
                        
                        // If this is the first point, upload the image first
                        if (isFirstPoint && !currentImageId.HasValue && !isUploadingImage)
                        {
                            Debug.WriteLine("First point placed, uploading image to server cache");
                            UploadCurrentImage().ContinueWith(task =>
                            {
                                if (task.Status == TaskStatus.RanToCompletion)
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
                }
            }
            else if (e.Button.Right())
            {
                if (shiftHeld)
                {
                    // Shift + Right-click: Delete background point within POINT_RADIUS
                    GridVector2? pointToRemove = FindPointWithinRadius(backgroundPoints, worldPos, POINT_RADIUS);
                    if (pointToRemove.HasValue)
                    {
                        backgroundPoints.Remove(pointToRemove.Value);
                        UpdatePointViews();
                        RequestSegmentation();
                    }
                }
                else
                {
                    // Check for overlapping background point
                    GridVector2? existingPoint = FindPointWithinRadius(backgroundPoints, worldPos, POINT_RADIUS);
                    if (!existingPoint.HasValue)
                    {
                        // Check if this is the first point being added
                        bool isFirstPoint = (foregroundPoints.Count == 0 && backgroundPoints.Count == 0);
                        
                        // Add background point only if no overlap
                        backgroundPoints.Add(worldPos);
                        UpdatePointViews();
                        
                        // If this is the first point, upload the image first
                        if (isFirstPoint && !currentImageId.HasValue && !isUploadingImage)
                        {
                            Debug.WriteLine("First point placed, uploading image to server cache");
                            UploadCurrentImage().ContinueWith(task =>
                            {
                                if (task.Status == TaskStatus.RanToCompletion)
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
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
                // Remove nearest point
                RemoveNearestPoint(worldPos);
                UpdatePointViews();
                
                // If last foreground point was removed, clear rendered mesh and polygons
                if (foregroundPoints.Count == 0)
                {
                    ClearSegmentationResults();
                }
                else
                {
                    RequestSegmentation();
                }
            }

            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(sender, e);
            
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
            GridRectangle currentBounds = GetCurrentViewportBounds();
            
            // Check if viewport has changed significantly
            if (!AreViewportBoundsSimilar(lastViewBounds, currentBounds))
            {
                lastViewBounds = currentBounds;
                viewportBounds = currentBounds;
                
                // Cancel any ongoing render operation
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

        private bool AreViewportBoundsSimilar(GridRectangle a, GridRectangle b)
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
            // Only re-request segmentation if we have points and an uploaded image
            if (foregroundPoints.Count > 0 || backgroundPoints.Count > 0)
            {
                Debug.WriteLine("Viewport settled with existing points, re-requesting segmentation");
                
                // Must invoke on UI thread
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    // RequestSegmentation will handle uploading if needed
                    RequestSegmentation();
                }));
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
            // Update foreground points view (green circles)
            if (foregroundPoints.Count > 0)
            {
                foregroundPointsView = new PointSetView(Color.Green, POINT_RADIUS)
                {
                    Points = foregroundPoints.ToArray()
                };
                foregroundPointsView.UpdateViews();
            }
            else
            {
                foregroundPointsView = null;
            }

            // Update background points view (red circles)
            if (backgroundPoints.Count > 0)
            {
                backgroundPointsView = new PointSetView(Color.Red, POINT_RADIUS)
                {
                    Points = backgroundPoints.ToArray()
                };
                backgroundPointsView.UpdateViews();
            }
            else
            {
                backgroundPointsView = null;
            }

            Parent.Invalidate(); // Trigger redraw
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

        private GridPolygon FindPolygonContainingPoint(GridVector2 worldPos)
        {
            // Check each segment polygon to see if the point is inside
            return segmentPolygonViews.FirstOrDefault(polygonView => polygonView?.InputPolygon != null && polygonView.InputPolygon.Contains(worldPos))?.InputPolygon;
        }
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
            return ColorFromHSL(hue, 0.8f, 0.5f, 0.5f);
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
        private async Task UploadCurrentImage()
        {
            if (isUploadingImage || grpcClient == null)
                return;

            isUploadingImage = true;

            try
            {
                // Cancel any existing upload
                uploadCancellationTokenSource?.Cancel();
                uploadCancellationTokenSource?.Dispose();
                uploadCancellationTokenSource = new CancellationTokenSource();

                // Capture current viewport image
                var (imageData, width, height) = await CaptureViewportImage();
                if (imageData == null || imageData.Length == 0)
                {
                    Debug.WriteLine("Failed to capture viewport image for upload");
                    return;
                }

                // Build gRPC upload request
                var uploadRequest = new SegmentationServiceTypes.UploadImageRequest
                {
                    ImageData = Google.Protobuf.ByteString.CopyFrom(imageData),
                    Width = width,
                    Height = height
                };

                Debug.WriteLine($"Uploading image to server cache: {width}x{height}, {imageData.Length} bytes");

                // Call gRPC service with cancellation token and timeout
                var callOptions = new CallOptions(
                    deadline: DateTime.UtcNow.AddSeconds(30),
                    cancellationToken: uploadCancellationTokenSource.Token);
                
                var uploadResponse = await grpcClient.UploadImageAsync(uploadRequest, callOptions);

                // Store the image ID, bounds, and dimensions
                currentImageId = uploadResponse.ImageId;
                uploadedImageBounds = viewportBounds;
                uploadedImageWidth = width;
                uploadedImageHeight = height;

                Debug.WriteLine($"Image uploaded successfully: ID={currentImageId}, dimensions={width}x{height}");

                // After upload completes, request segmentation if we have points
                if (foregroundPoints.Count > 0 || backgroundPoints.Count > 0)
                {
                    Debug.WriteLine("Requesting segmentation with uploaded image");
                    RequestSegmentation();
                }
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
                MessageBox.Show($"Failed to upload image to segmentation service: {rpcEx.Status.Detail}",
                    "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentImageId = null;
                uploadedImageBounds = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading image: {ex.Message}");
                currentImageId = null;
                uploadedImageBounds = null;
            }
            finally
            {
                isUploadingImage = false;
            }
        }

        private async Task DeleteCurrentImage()
        {
            if (!currentImageId.HasValue || grpcClient == null)
                return;

            ulong imageIdToDelete = currentImageId.Value;
            currentImageId = null;
            uploadedImageBounds = null;

            try
            {
                var deleteRequest = new SegmentationServiceTypes.DeleteImageRequest
                {
                    ImageId = imageIdToDelete
                };

                Debug.WriteLine($"Deleting image from server cache: ID={imageIdToDelete}");

                // Call gRPC service with timeout (fire and forget, don't block UI)
                var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(5));
                var deleteResponse = await grpcClient.DeleteImageAsync(deleteRequest, callOptions);

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
            if (isSegmenting || grpcClient == null)
                return;

            if (foregroundPoints.Count == 0 && backgroundPoints.Count == 0)
                return;

            // If we don't have an uploaded image, upload one first
            if (!currentImageId.HasValue && !isUploadingImage) 
            {
                Debug.WriteLine("No cached image ID, uploading image first");
                await UploadCurrentImage();
                await RequestSegmentation();
                return;
            }

            // Wait for upload to complete if it's in progress
            if (isUploadingImage)
            {
                Debug.WriteLine("Upload in progress, segmentation will be requested after upload completes");
                return;
            }

            isSegmenting = true;

            // Build gRPC request using cached image ID
            var request = new SegmentationServiceTypes.SegmentationRequest
            {
                ImageId = currentImageId.Value,
                MultimaskOutput = false
            };

            try
            { 
                // Use uploaded image dimensions for coordinate mapping
                int width = uploadedImageWidth;
                int height = uploadedImageHeight;

                // Add foreground points (label = 1)
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

                Debug.WriteLine($"Sending segmentation request with image ID {currentImageId}: {width}x{height}, {foregroundPoints.Count} fg, {backgroundPoints.Count} bg points");

                // Call gRPC service with timeout
                var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(30));
                var response = await grpcClient.SegmentImageAsync(request, callOptions);

                // Process response on UI thread
                await Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                {
                    ProcessSegmentationResponse(response);
                }));
            }
            catch (RpcException rpcEx)
            {
                // Handle case where image was evicted/expired from cache
                if (rpcEx.StatusCode == StatusCode.NotFound)
                {
                    Debug.WriteLine($"Image not found in cache (evicted/expired), re-uploading and retrying: {rpcEx.Status.Detail}");
                    
                    // Clear the image ID
                    currentImageId = null;
                    uploadedImageBounds = null;

                    // Re-upload the image and retry segmentation
                    await UploadCurrentImage();
                    var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(30));
                    var response = await grpcClient.SegmentImageAsync(request, callOptions);

                    // Process response on UI thread
                    await Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() =>
                    {
                        ProcessSegmentationResponse(response);
                    }));
                }
                else
                {
                    Debug.WriteLine($"gRPC error: {rpcEx.Status.Detail}");
                    MessageBox.Show($"Segmentation service error: {rpcEx.Status.Detail}", 
                        "Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

            // Clear existing polygon views
            segmentPolygonViews.Clear();

            // Count total polygons for color distribution
            int totalPolygons = response.Segments.Sum(s => s.Polygons.Count);
            int polygonIndex = 0;

            // Process all segments and their polygons
            foreach (var segment in response.Segments.OrderByDescending(s => s.Score))
            {
                if(segment.Polygons is null)
                    continue;

                Debug.WriteLine($"Processing segment with score: {segment.Score:F3}, {segment.Polygons.Count} polygons");

                // Convert each protobuf polygon to GridPolygon and create a view
                foreach (var protoPolygon in segment.Polygons)
                {
                    GridPolygon gridPolygon;
                    try
                    { 
                        // Invert Y coordinates: Viking indexes from bottom-left, segmentation server from top-left
                        var invertedProtoPolygon = new SegmentationServiceTypes.Polygon
                        {
                            Points = { protoPolygon.Points.Select(p => new SegmentationServiceTypes.Point
                            {
                                X = p.X,
                                Y = response.Height - p.Y
                            }) }
                        };
                        gridPolygon = invertedProtoPolygon.ToGridPolygon(viewportBounds, response.Width, response.Height);
                    }
                    catch(ArgumentException)
                    {
                        continue; 
                    }
                    
                    if (gridPolygon != null && gridPolygon.ExteriorRing.Length >= 3)
                    {
                        // Generate a distinct color for this polygon
                        Color polygonColor = GenerateDistinctColor(polygonIndex, totalPolygons);
                        
                        // Create a SolidPolygonView
                        SolidPolygonView polygonView = new SolidPolygonView(gridPolygon, polygonColor);
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

#if DEBUG
            // In DEBUG builds, also show the mask overlay
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
                GridVector2 topLeft = ViewportToWorld(bestSegment.X, bestSegment.Y, decodedWidth, decodedHeight);
                GridVector2 bottomRight = ViewportToWorld(
                    bestSegment.X + decodedWidth,
                    bestSegment.Y + decodedHeight,
                    decodedWidth, 
                    decodedHeight
                );
                GridRectangle segmentBounds = new GridRectangle(topLeft, bottomRight);
                maskOverlayView = new TextureOverlayView(maskTexture, segmentBounds, maskColor);
            }
#endif

            // Invalidate to trigger redraw
            Parent.Invalidate();
        }
        #endregion

        #region Image Capture
        private async Task<(byte[] data, int width, int height)> CaptureViewportImage()
        {
            try
            {
                PrepareCancellationToken();

                var (graphicsDevice, scene, width, height) = ValidateRenderingContext();
                if (graphicsDevice == null || scene == null)
                {
                    return (null, 0, 0);
                }

                RenderTarget2D renderTarget = await RenderViewportToTexture(scene, width, height);
                if (renderTarget == null)
                {
                    return (null, 0, 0);
                }

                try
                {
                    Color[] pixels = new Color[width * height];
                    renderTarget.GetData(pixels);

                    byte[] pngData = EncodeToPng(graphicsDevice, renderTarget, pixels, width, height);

                    Debug.WriteLine($"Viewport image captured as PNG ({pngData.Length} bytes)");
                    return (pngData, width, height);
                }
                finally
                {
                    renderTarget?.Dispose();
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

        private void PrepareCancellationToken()
        {
            renderCancellationTokenSource?.Cancel();
            renderCancellationTokenSource?.Dispose();
            renderCancellationTokenSource = new CancellationTokenSource();
        }

        private (GraphicsDevice device, VikingXNA.Scene scene, int width, int height) ValidateRenderingContext()
        {
            var graphicsDevice = Parent.Device;
            if (graphicsDevice == null)
            {
                Debug.WriteLine("GraphicsDevice is null");
                return (null, null, 0, 0);
            }

            var scene = Parent.Scene;
            if (scene == null)
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

        private async Task<RenderTarget2D> RenderViewportToTexture(VikingXNA.Scene scene, int width, int height)
        {
            float centerX = scene.Camera.LookAt.X;
            float centerY = scene.Camera.LookAt.Y;
            int sectionZ = Parent.Section.Number;

            Debug.WriteLine($"Rendering scene to texture: {width}x{height}, center: ({centerX}, {centerY}), section: {sectionZ}");

            RenderTarget2D renderTarget = await Parent.RenderSceneToTexture(
                scene,
                centerX,
                centerY,
                sectionZ,
                showOverlays: false,
                asyncTextureLoad: false,
                renderCancellationTokenSource.Token);

            if (renderTarget == null)
            {
                Debug.WriteLine("RenderSceneToTexture returned null");
            }

            return renderTarget;
        }

        private byte[] EncodeToPng(GraphicsDevice graphicsDevice, RenderTarget2D renderTarget, Color[] pixels, int width, int height)
        {
            bool isGrayscale = IsImageGrayscale(pixels);

            using (MemoryStream pngStream = new MemoryStream())
            {
                if (isGrayscale)
                {
                    EncodeGrayscalePng(graphicsDevice, pngStream, pixels, width, height);
                }
                else
                {
                    EncodeColorPng(renderTarget, pngStream, width, height);
                }

                return pngStream.ToArray();
            }
        }

        private void EncodeGrayscalePng(GraphicsDevice graphicsDevice, MemoryStream pngStream, Color[] pixels, int width, int height)
        {
            Texture2D grayscaleTexture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            try
            {
                Color[] grayscalePixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte gray = pixels[i].R; // Already grayscale, so R = G = B
                    grayscalePixels[i] = new Color(gray, gray, gray, (byte)255);
                }
                grayscaleTexture.SetData(grayscalePixels);
                grayscaleTexture.SaveAsPng(pngStream, width, height);
                Debug.WriteLine("Image detected as grayscale, encoded as single-channel PNG");
            }
            finally
            {
                grayscaleTexture?.Dispose();
            }
        }

        private void EncodeColorPng(RenderTarget2D renderTarget, MemoryStream pngStream, int width, int height)
        {
            renderTarget.SaveAsPng(pngStream, width, height);
            Debug.WriteLine("Image detected as color, encoded as full-color PNG");
        }

        private bool IsImageGrayscale(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].R != pixels[i].G || pixels[i].R != pixels[i].B)
                {
                    return false;
                }
            }
            return true;
        }

// DEBUG: Image saving commented out - not needed for production
//#if DEBUG
//        private void SaveDebugImage(byte[] pngData)
//        {
//            try
//            {
//                const string debugPath = @"C:\Temp\SegmentationServiceImage.png";
//                
//                // Ensure directory exists
//                string directory = System.IO.Path.GetDirectoryName(debugPath);
//                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
//                {
//                    Directory.CreateDirectory(directory);
//                }
//
//                File.WriteAllBytes(debugPath, pngData);
//                Debug.WriteLine($"Debug image saved to: {debugPath}");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"Failed to save debug image: {ex.Message}");
//                // Don't throw - this is just for debugging
//            }
//        }
//#endif
        #endregion

        #region Mask Processing
        private (byte[] maskData, int width, int height) DecodePngMask(byte[] pngBytes)
        {
            try
            {
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    Debug.WriteLine("Empty PNG mask data");
                    return (null, 0, 0);
                }

                // Load PNG using Texture2D.FromStream
                using (var stream = new MemoryStream(pngBytes))
                {
                    var graphicsDevice = Parent.Device;
                    if (graphicsDevice == null)
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
                if (graphicsDevice == null || maskData == null || maskData.Length != width * height)
                    return null;

                Texture2D texture = new Texture2D(graphicsDevice, width, height);
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
            if (currentMaskData == null || maskWidth == 0 || maskHeight == 0)
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
        private GridVector2 WorldToScreen(GridVector2 worldPos)
        {
            // Convert world coordinates to screen pixel coordinates
            return Parent.WorldToScreen(worldPos.X, worldPos.Y);
        }

        private GridRectangle GetCurrentViewportBounds()
        {
            // Get the world-space bounds of the current viewport
            GridVector2 topLeft = Parent.ScreenToWorld(0, 0);
            GridVector2 bottomRight = Parent.ScreenToWorld(Parent.Width, Parent.Height);
            return new GridRectangle(topLeft, bottomRight);
        }

        private GridVector2 WorldToViewport(GridVector2 worldPos, int viewportWidth, int viewportHeight)
        {
            // Transform world coordinates to viewport pixel coordinates
            GridVector2 topLeft = viewportBounds.LowerLeft;
            GridVector2 bottomRight = viewportBounds.UpperRight;

            double normalizedX = (worldPos.X - topLeft.X) / (bottomRight.X - topLeft.X);
            double normalizedY = (worldPos.Y - topLeft.Y) / (bottomRight.Y - topLeft.Y);

            return new GridVector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight
            );
        }

        private GridVector2 ViewportToWorld(int pixelX, int pixelY, int viewportWidth, int viewportHeight)
        {
            // Transform viewport pixel coordinates to world coordinates
            double normalizedX = (double)pixelX / viewportWidth;
            double normalizedY = (double)pixelY / viewportHeight;

            GridVector2 topLeft = viewportBounds.LowerLeft;
            GridVector2 bottomRight = viewportBounds.UpperRight;

            return new GridVector2(
                topLeft.X + normalizedX * (bottomRight.X - topLeft.X),
                topLeft.Y + normalizedY * (bottomRight.Y - topLeft.Y)
            );
        }
        #endregion

        #region Rendering
        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            // Save current depth buffer state
            var previousDepthStencilState = graphicsDevice.DepthStencilState;
            
#if DEBUG
            // Draw mask overlay if available (using TextureOverlayView) - DEBUG only
            if (maskOverlayView != null)
            {
                maskOverlayView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
#endif

            // Draw segment polygons first (underneath points)
            foreach (var polygonView in segmentPolygonViews)
            {
                polygonView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            // Disable depth testing to ensure points always draw on top of polygons
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            // Draw foreground points (green circles using PointSetView) - always on top
            if (foregroundPointsView != null)
            {
                foregroundPointsView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            // Draw background points (red circles using PointSetView) - always on top
            if (backgroundPointsView != null)
            {
                backgroundPointsView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            // Restore previous depth buffer state
            graphicsDevice.DepthStencilState = previousDepthStencilState;
        }
        #endregion

        #region Command Execution
        protected override void Execute()
        {
            if (selectedPolygon == null)
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

                // Create structure and location using the selected polygon
                CreateAnnotationFromPolygon(selectedPolygon);

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

        private GridPolygon MaskToPolygon(byte[] maskData, int width, int height)
        {
            // Extract contour points from binary mask
            List<GridVector2> contourPoints = ExtractContourFromMask(maskData, width, height);
            
            if (contourPoints.Count < 3)
            {
                Debug.WriteLine("Not enough contour points extracted");
                return null;
            }

            // Simplify using Douglas-Peucker algorithm from Geometry package
            var simplifiedPoints = contourPoints.DouglasPeuckerReduction(SIMPLIFICATION_TOLERANCE);

            Debug.WriteLine($"Contour: {contourPoints.Count} points simplified to {simplifiedPoints.Count} points");

            // Create polygon
            return new GridPolygon(simplifiedPoints.EnsureClosedRing().RemoveAdjacentDuplicates());
        }

        private List<GridVector2> ExtractContourFromMask(byte[] maskData, int width, int height)
        {
            // Simple boundary extraction: find pixels on the edge of the mask
            List<GridVector2> boundaryPixels = new List<GridVector2>();

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = y * width + x;
                    if (maskData[idx] > 0 && IsBoundaryPixel(maskData, x, y, width, height))
                    {
                        // Transform from pixel to world coordinates
                        GridVector2 worldPt = ViewportToWorld(x, y, width, height);
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

        private List<GridVector2> OrderContourPoints(List<GridVector2> points)
        {
            if (points.Count < 3)
                return points;

            // Simple ordering: start from leftmost point and find nearest unvisited neighbors
            // For production, implement proper contour following
            List<GridVector2> ordered = new List<GridVector2>();
            HashSet<GridVector2> remaining = new HashSet<GridVector2>(points);

            // Start with leftmost point
            GridVector2 current = points.OrderBy(p => p.X).First();
            ordered.Add(current);
            remaining.Remove(current);

            while (remaining.Count > 0)
            {
                // Find nearest remaining point
                GridVector2 nearest = remaining.OrderBy(p => GridVector2.DistanceSquared(current, p)).First();
                ordered.Add(nearest);
                remaining.Remove(nearest);
                current = nearest;
            }

            return ordered;
        }

        private void CreateAnnotationFromPolygon(GridPolygon polygon)
        {
            // Determine structure type
            StructureTypeObj type = structureType ?? GetDefaultStructureType();
            if (type == null)
            {
                MessageBox.Show("No structure type selected. Please select a structure type first.", 
                    "No Structure Type", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Create structure
            StructureObj newStruct = new StructureObj(type);

            // Create location with polygon type
            LocationObj newLocation = new LocationObj(
                newStruct,
                Parent.Section.Number,
                Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON);

            try
            {
                // Set the polygon geometry
                // SetShapeFromGeometryInSection will transform the mosaic shape to volume coordinates
                SqlGeometry mosaicGeometry = polygon.ToSqlGeometry();
                newLocation.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, mosaicGeometry);

                // Enqueue command to save the structure
                Parent.CommandQueue.EnqueueCommand(
                    typeof(CreateNewStructureCommand),
                    new object[] { Parent, newStruct, newLocation });
            }
            catch (ArgumentException e)
            {
                MessageBox.Show($"Could not create polygon: {e.Message}", 
                    "Error Creating Annotation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private StructureTypeObj GetDefaultStructureType()
        {
            // Try to get from state
            StructureType result = Viking.UI.State.SelectedObject as StructureType;
            if(result is null)
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
            foregroundPointsView = null;
            backgroundPointsView = null;
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
            isUploadingImage = false;
            
            // Cancel and dispose of cancellation token sources
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
