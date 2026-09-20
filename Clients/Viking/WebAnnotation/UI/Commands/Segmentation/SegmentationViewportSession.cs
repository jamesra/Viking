using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DependencyInjection;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.UI;
using Viking.UI.Controls;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Captures the current viewport, uploads it to the segmentation service, and converts
    /// SegmentImage responses into world-space polygons. Shared by interactive SegmentationCommand
    /// and AutoCirclePolygonizeController.
    /// </summary>
    internal sealed class SegmentationViewportSession
    {
        private readonly SectionViewerControl parent;
        private SegmentationServiceTypes.SegmentationService.SegmentationServiceClient grpcClient;

        private ulong? currentImageId;
        private GridRectangle? uploadedImageBounds;
        private int uploadedImageWidth;
        private int uploadedImageHeight;
        private int isUploadingImage;
        private CancellationTokenSource renderCancellationTokenSource;
        private CancellationTokenSource linkedRenderCancellationTokenSource;
        private CancellationTokenSource uploadCancellationTokenSource;

        /// <summary>
        /// Binds the session to a viewer. ViewportBounds starts as the live camera rectangle.
        /// </summary>
        public SegmentationViewportSession(SectionViewerControl parent)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            ViewportBounds = GetCurrentViewportBounds();
        }

        public GridRectangle ViewportBounds { get; set; }

        public ulong? CurrentImageId => currentImageId;

        public GridRectangle? UploadedImageBounds => uploadedImageBounds;

        public int UploadedImageWidth => uploadedImageWidth;

        public int UploadedImageHeight => uploadedImageHeight;

        public bool IsUploading => Interlocked.CompareExchange(ref isUploadingImage, 0, 0) != 0;

        public bool HasClient => grpcClient is not null;

        /// <summary>
        /// Creates the gRPC stub from <see cref="ServiceLocator.GrpcChannelManager"/>. Safe to call repeatedly.
        /// </summary>
        public bool TryInitializeClient()
        {
            try
            {
                var channel = ServiceLocator.GrpcChannelManager?.GetOrCreateChannel();
                if (channel is null)
                    return false;

                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(channel);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize segmentation gRPC client: {ex.Message}");
                grpcClient = null;
                return false;
            }
        }

        public GridRectangle GetCurrentViewportBounds()
        {
            GridVector2 topLeft = parent.ScreenToWorld(0, 0);
            GridVector2 bottomRight = parent.ScreenToWorld(parent.Width, parent.Height);
            return new GridRectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// True when corners differ by less than 1% of the larger side. Used to treat hitch jitter as the same view.
        /// </summary>
        public static bool AreViewportBoundsSimilar(GridRectangle a, GridRectangle b)
        {
            double tolerance = Math.Max(a.Width, a.Height) * 0.01;
            return Math.Abs(a.LowerLeft.X - b.LowerLeft.X) < tolerance &&
                   Math.Abs(a.LowerLeft.Y - b.LowerLeft.Y) < tolerance &&
                   Math.Abs(a.UpperRight.X - b.UpperRight.X) < tolerance &&
                   Math.Abs(a.UpperRight.Y - b.UpperRight.Y) < tolerance;
        }

        /// <summary>
        /// After PNG encode finishes, upload only when the live viewport still matches the captured bounds.
        /// Used by UploadCurrentImageAsync and covered by auto-polygonize tests.
        /// </summary>
        public static bool ShouldUploadEncodedCapture(GridRectangle capturedBounds, GridRectangle currentBounds)
            => AreViewportBoundsSimilar(capturedBounds, currentBounds);

        /// <summary>
        /// Maps world to capture-pixel space without a Y flip. SAM2 mask Y is flipped in <see cref="GetSegmentWorldBounds"/>.
        /// </summary>
        public GridVector2 WorldToViewport(GridVector2 worldPos, int viewportWidth, int viewportHeight)
        {
            GridVector2 boundsMin = ViewportBounds.LowerLeft;
            GridVector2 boundsMax = ViewportBounds.UpperRight;

            double normalizedX = (worldPos.X - boundsMin.X) / (boundsMax.X - boundsMin.X);
            double normalizedY = (worldPos.Y - boundsMin.Y) / (boundsMax.Y - boundsMin.Y);

            return new GridVector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight);
        }

        /// <summary>
        /// Inverse of <see cref="WorldToViewport"/>; pixel Y is not flipped here.
        /// </summary>
        public GridVector2 ViewportToWorld(int pixelX, int pixelY, int viewportWidth, int viewportHeight)
        {
            double normalizedX = (double)pixelX / viewportWidth;
            double normalizedY = (double)pixelY / viewportHeight;
            GridVector2 boundsMin = ViewportBounds.LowerLeft;
            GridVector2 boundsMax = ViewportBounds.UpperRight;
            return new GridVector2(
                boundsMin.X + normalizedX * (boundsMax.X - boundsMin.X),
                boundsMin.Y + normalizedY * (boundsMax.Y - boundsMin.Y));
        }

        /// <summary>
        /// Cancels in-flight render, linked render, and upload tokens. Does not delete a server image.
        /// </summary>
        public void CancelPendingWork()
        {
            linkedRenderCancellationTokenSource?.Cancel();
            renderCancellationTokenSource?.Cancel();
            uploadCancellationTokenSource?.Cancel();
        }

        public void ClearImageId()
        {
            currentImageId = null;
            uploadedImageBounds = null;
            uploadedImageWidth = 0;
            uploadedImageHeight = 0;
            Interlocked.Exchange(ref isUploadingImage, 0);
        }

        /// <summary>
        /// Installs a previously uploaded SAM2 image so SegmentImage can reuse it.
        /// Prompt mapping uses <paramref name="worldBounds"/>, not the live camera.
        /// NotFound still re-uploads via <see cref="SegmentAsync"/>.
        /// </summary>
        public void AdoptUploadedImage(ulong imageId, GridRectangle worldBounds, int width, int height)
        {
            currentImageId = imageId;
            uploadedImageBounds = worldBounds;
            uploadedImageWidth = width;
            uploadedImageHeight = height;
            ViewportBounds = worldBounds;
        }

        /// <summary>
        /// Best-effort DeleteImage for a leased id that is no longer referenced by the auto-polygonize cache.
        /// Does not change <see cref="CurrentImageId"/> on this session.
        /// </summary>
        public async Task DeleteImageByIdAsync(ulong imageId)
        {
            if (imageId == 0 || grpcClient is null)
                return;

            try
            {
                DeleteImageRequest deleteRequest = new()
                {
                    ImageId = imageId
                };

                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(5));
                await grpcClient.DeleteImageAsync(deleteRequest, callOptions).ResponseAsync.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting leased image from cache (ID={imageId}): {ex.Message}");
            }
        }

        /// <summary>
        /// Waits for visible tiles, GPU-captures, encodes off-UI, then uploads if the live view still matches.
        /// UploadImage is not passed the cancel token so a completed RPC always yields an ID; cancel after assign deletes.
        /// </summary>
        /// <returns>True only when the server image is recorded and the client was not cancelled after assign.</returns>
        public async Task<bool> UploadCurrentImageAsync(CancellationToken cancellationToken)
        {
            if (grpcClient is null)
                return false;

            if (Interlocked.CompareExchange(ref isUploadingImage, 1, 0) != 0)
                return false;

            Stopwatch totalTimer = Stopwatch.StartNew();
            ulong? assignedId = null;
            try
            {
                uploadCancellationTokenSource?.Cancel();
                uploadCancellationTokenSource?.Dispose();
                uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Stopwatch captureTimer = Stopwatch.StartNew();
                var (imageData, width, height, capturedBounds) = await CaptureViewportImage(uploadCancellationTokenSource.Token).ConfigureAwait(false);
                long captureMs = captureTimer.ElapsedMilliseconds;
                if (imageData is null || imageData.Length == 0)
                {
                    Debug.WriteLine("Failed to capture viewport image for upload");
                    return false;
                }

                GridRectangle liveBounds = await GetLiveViewportBoundsAsync().ConfigureAwait(false);
                if (!ShouldUploadEncodedCapture(capturedBounds, liveBounds))
                {
                    Debug.WriteLine("[SegmentationProfile] Skipping upload: view moved during encode");
                    return false;
                }

                UploadImageRequest uploadRequest = new()
                {
                    ImageData = Google.Protobuf.ByteString.CopyFrom(imageData),
                    Width = width,
                    Height = height
                };

                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(30));

                Stopwatch rpcTimer = Stopwatch.StartNew();
                var uploadResponse = await grpcClient.UploadImageAsync(uploadRequest, callOptions).ResponseAsync.ConfigureAwait(false);
                long rpcMs = rpcTimer.ElapsedMilliseconds;

                assignedId = uploadResponse.ImageId;
                currentImageId = assignedId;
                uploadedImageBounds = ViewportBounds;
                uploadedImageWidth = width;
                uploadedImageHeight = height;

                Debug.WriteLine(
                    $"[SegmentationProfile] Upload image={currentImageId} dimensions={width}x{height} bytes={imageData.Length} " +
                    $"capture={captureMs}ms uploadRpc={rpcMs}ms total={totalTimer.ElapsedMilliseconds}ms");

                if (uploadCancellationTokenSource.Token.IsCancellationRequested)
                {
                    Debug.WriteLine("Image upload cancelled after server assigned id; deleting");
                    await DeleteCurrentImageAsync().ConfigureAwait(false);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Image upload cancelled due to view change");
                await DeleteAssignedImageOrClearAsync(assignedId).ConfigureAwait(false);
            }
            catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Cancelled)
            {
                Debug.WriteLine("Image upload cancelled due to view change");
                await DeleteAssignedImageOrClearAsync(assignedId).ConfigureAwait(false);
            }
            catch (RpcException rpcEx)
            {
                Debug.WriteLine($"gRPC error during upload: {rpcEx.Status.Detail}");
                await DeleteAssignedImageOrClearAsync(assignedId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading image: {ex.Message}");
                await DeleteAssignedImageOrClearAsync(assignedId).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref isUploadingImage, 0);
            }

            return false;
        }

        /// <summary>
        /// Deletes the server image when UploadImage assigned an ID before the client cancelled or failed.
        /// Otherwise only clears local state so we do not leave an orphaned SAM2 cache entry.
        /// </summary>
        private async Task DeleteAssignedImageOrClearAsync(ulong? assignedId)
        {
            if (assignedId.HasValue)
            {
                currentImageId = assignedId;
                await DeleteCurrentImageAsync().ConfigureAwait(false);
                return;
            }

            ClearImageId();
        }

        /// <summary>
        /// Best-effort DeleteImage RPC, then clears <see cref="CurrentImageId"/>. Failures are logged only.
        /// </summary>
        public async Task DeleteCurrentImageAsync()
        {
            if (!currentImageId.HasValue || grpcClient is null)
                return;

            ulong imageIdToDelete = currentImageId.Value;
            ClearImageId();

            try
            {
                DeleteImageRequest deleteRequest = new()
                {
                    ImageId = imageIdToDelete
                };

                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(5));
                await grpcClient.DeleteImageAsync(deleteRequest, callOptions).ResponseAsync.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting image from cache (ID={imageIdToDelete}): {ex.Message}");
            }
        }

        /// <summary>
        /// Uploads if needed, then SegmentImage with the given world-space prompts.
        /// NotFound re-uploads once. Returns null when cancelled or the client is missing.
        /// </summary>
        public async Task<SegmentationResponse?> SegmentAsync(
            IReadOnlyList<GridVector2> foregroundPoints,
            IReadOnlyList<GridVector2> backgroundPoints,
            CancellationToken cancellationToken)
        {
            if (grpcClient is null)
                return null;

            if ((foregroundPoints is null || foregroundPoints.Count == 0) &&
                (backgroundPoints is null || backgroundPoints.Count == 0))
                return null;

            if (!currentImageId.HasValue && !IsUploading)
            {
                if (!await UploadCurrentImageAsync(cancellationToken).ConfigureAwait(false))
                    return null;
            }

            if (IsUploading || !currentImageId.HasValue)
                return null;

            try
            {
                Stopwatch totalTimer = Stopwatch.StartNew();
                Stopwatch requestTimer = Stopwatch.StartNew();
                SegmentationRequest? request = BuildSegmentationRequest(foregroundPoints, backgroundPoints);
                if (request is null)
                    return null;
                long requestMs = requestTimer.ElapsedMilliseconds;

                CallOptions callOptions = new(
                    deadline: DateTime.UtcNow.AddSeconds(30),
                    cancellationToken: cancellationToken);

                Stopwatch rpcTimer = Stopwatch.StartNew();
                SegmentationResponse response =
                    await grpcClient.SegmentImageAsync(request, callOptions).ResponseAsync.ConfigureAwait(false);
                long rpcMs = rpcTimer.ElapsedMilliseconds;
                int responseBytes = response.Segments.Sum(segment => segment.Mask.Length);
                Debug.WriteLine(
                    $"[SegmentationProfile] Segment image={currentImageId} foreground={foregroundPoints?.Count ?? 0} " +
                    $"background={backgroundPoints?.Count ?? 0} request={requestMs}ms rpc={rpcMs}ms " +
                    $"segments={response.Segments.Count} responseBytes={responseBytes} total={totalTimer.ElapsedMilliseconds}ms");
                return response;
            }
            catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound)
            {
                Debug.WriteLine($"Image not found in cache, re-uploading: {rpcEx.Status.Detail}");
                currentImageId = null;
                uploadedImageBounds = null;

                if (!await UploadCurrentImageAsync(cancellationToken).ConfigureAwait(false))
                    return null;

                SegmentationRequest? retry = BuildSegmentationRequest(foregroundPoints, backgroundPoints);
                if (retry is null)
                    return null;

                CallOptions retryOptions = new(
                    deadline: DateTime.UtcNow.AddSeconds(30),
                    cancellationToken: cancellationToken);

                SegmentationResponse retryResponse =
                    await grpcClient.SegmentImageAsync(retry, retryOptions).ResponseAsync.ConfigureAwait(false);
                return retryResponse;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Segmentation error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decodes each segment mask and polygonizes in score order. Near-full-frame masks are skipped.
        /// Cleanup and marching squares run on the downsampled mask.
        /// <paramref name="cancellationToken"/> is checked between segments so a newer click can abort.
        /// </summary>
        public IReadOnlyList<GridPolygon> CreatePolygonsFromResponse(
            SegmentationResponse response,
            double? holeDropFraction = null,
            IReadOnlyList<GridVector2> preserveHolesContainingWorldPoints = null,
            int? edgeCleanupRadius = null,
            CancellationToken cancellationToken = default)
        {
            if (response is null || response.Segments.Count == 0)
                return [];

            Stopwatch totalTimer = Stopwatch.StartNew();
            long decodeMs = 0;
            long cleanupMs = 0;
            long polygonizeMs = 0;
            int maskBytes = 0;
            int foregroundBefore = 0;
            int foregroundAfter = 0;
            double dropFraction = holeDropFraction ?? WebAnnotation.Global.AnnotationSettings.SegmentationHoleDropFraction;
            int cleanupRadius = edgeCleanupRadius ?? WebAnnotation.Global.AnnotationSettings.SegmentationEdgeCleanupRadius;
            List<GridPolygon> polygons = [];
            foreach (var segment in response.Segments.OrderByDescending(s => s.Score))
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] pngBytes = segment.Mask.ToByteArray();
                maskBytes += pngBytes.Length;
                Stopwatch decodeTimer = Stopwatch.StartNew();
                var (decodedMaskData, decodedWidth, decodedHeight) = DecodePngMask(pngBytes);
                decodeMs += decodeTimer.ElapsedMilliseconds;
                if (decodedMaskData is null)
                    continue;

                int captureWidth = uploadedImageWidth > 0 ? uploadedImageWidth : response.Width;
                int captureHeight = uploadedImageHeight > 0 ? uploadedImageHeight : response.Height;

                Stopwatch polygonizeTimer = Stopwatch.StartNew();
                polygons.AddRange(SegmentationMaskPolygonizer.CreatePolygons(
                    decodedMaskData,
                    decodedWidth,
                    decodedHeight,
                    segment.X,
                    segment.Y,
                    captureWidth,
                    captureHeight,
                    ViewportBounds,
                    dropFraction,
                    preserveHolesContainingWorldPoints,
                    cleanupRadius,
                    out SegmentationMaskPolygonizer.CleanupStats cleanupStats));
                cleanupMs += cleanupStats.ElapsedMilliseconds;
                polygonizeMs += Math.Max(0, polygonizeTimer.ElapsedMilliseconds - cleanupStats.ElapsedMilliseconds);
                foregroundBefore += cleanupStats.ForegroundPixelsBefore;
                foregroundAfter += cleanupStats.ForegroundPixelsAfter;
            }

            int vertexCount = polygons.Sum(polygon =>
                polygon.ExteriorRing.Length + polygon.InteriorRings.Sum(ring => ring.Length));
            Debug.WriteLine(
                $"[SegmentationProfile] Client polygons image={currentImageId} segments={response.Segments.Count} " +
                $"maskBytes={maskBytes} decode={decodeMs}ms cleanup={cleanupMs}ms " +
                $"pixels={foregroundBefore}->{foregroundAfter} polygonize={polygonizeMs}ms " +
                $"polygons={polygons.Count} vertices={vertexCount} total={totalTimer.ElapsedMilliseconds}ms");
            return polygons;
        }

        /// <summary>
        /// Decodes a SAM2 1-bit PNG into a packed 0/255 byte mask. Returns null data on invalid PNG.
        /// </summary>
        public (byte[]? maskData, int width, int height) DecodePngMask(byte[] pngBytes)
        {
            try
            {
                if (pngBytes is null || pngBytes.Length == 0)
                    return (null, 0, 0);

                using SixLabors.ImageSharp.Image<Rgba32> image =
                    SixLabors.ImageSharp.Image.Load<Rgba32>(pngBytes);
                int width = image.Width;
                int height = image.Height;
                Rgba32[] pixels = new Rgba32[width * height];
                image.CopyPixelDataTo(pixels);
                byte[] maskData = new byte[width * height];
                for (int i = 0; i < pixels.Length; i++)
                    maskData[i] = pixels[i].R;

                return (maskData, width, height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding PNG mask: {ex.Message}");
                return (null, 0, 0);
            }
        }

        private SegmentationRequest? BuildSegmentationRequest(
            IReadOnlyList<GridVector2> foregroundPoints,
            IReadOnlyList<GridVector2> backgroundPoints)
        {
            if (!currentImageId.HasValue)
                return null;

            SegmentationRequest request = new()
            {
                ImageId = currentImageId.Value,
                MultimaskOutput = false,
                OmitLabeledImage = true
            };

            int width = uploadedImageWidth;
            int height = uploadedImageHeight;

            if (foregroundPoints is not null)
            {
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
            }

            if (backgroundPoints is not null)
            {
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
            }

            return request;
        }

        /// <summary>
        /// Reads the camera rectangle on the UI dispatcher so off-UI encode/publish can compare against the live view.
        /// </summary>
        public Task<GridRectangle> GetLiveViewportBoundsAsync()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                return Task.FromResult(GetCurrentViewportBounds());

            return dispatcher.InvokeAsync(GetCurrentViewportBounds).Task;
        }

        /// <summary>
        /// World rectangle of a SAM2 segment. Mask Y is top-origin, so Y is flipped against Viking world-up.
        /// </summary>
        public GridRectangle GetSegmentWorldBounds(
            int segmentX,
            int segmentY,
            int maskWidth,
            int maskHeight,
            int responseWidth,
            int responseHeight)
        {
            int imageWidth = uploadedImageWidth > 0 ? uploadedImageWidth : responseWidth;
            int imageHeight = uploadedImageHeight > 0 ? uploadedImageHeight : responseHeight;
            GridVector2 topLeft = ViewportToWorld(
                segmentX,
                responseHeight - segmentY,
                imageWidth,
                imageHeight);
            GridVector2 bottomRight = ViewportToWorld(
                segmentX + maskWidth,
                (responseHeight - segmentY) - maskHeight,
                imageWidth,
                imageHeight);
            return new GridRectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// Waits for visible tiles, GPU-captures on the UI thread, then encodes on a worker.
        /// Returns null data when tiles never become ready or encode is cancelled.
        /// </summary>
        private async Task<(byte[]? data, int width, int height, GridRectangle capturedBounds)> CaptureViewportImage(CancellationToken cancellationToken)
        {
            try
            {
                if (parent.Scene is null || parent.Section is null)
                    return (null, 0, 0, default);

                if (!await parent.WaitForVisibleTexturesAsync(parent.Scene, parent.Section.Number, cancellationToken).ConfigureAwait(false))
                {
                    Debug.WriteLine("[SegmentationProfile] Skipping capture: visible tiles not ready");
                    return (null, 0, 0, default);
                }

                Stopwatch totalTimer = Stopwatch.StartNew();
                var (pixels, width, height, isGrayscale, capturedBounds, renderMs, readbackMs) =
                    await ReadViewportPixelsAsync(cancellationToken).ConfigureAwait(false);
                if (pixels is null || width <= 0 || height <= 0)
                    return (null, 0, 0, default);

                var encodeResult = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Stopwatch encodeTimer = Stopwatch.StartNew();
                    byte[] encoded = SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, isGrayscale);
                    long encodeMs = encodeTimer.ElapsedMilliseconds;

                    Stopwatch validationTimer = Stopwatch.StartNew();
                    var (isValid, errorMessage) = ValidateCapturedImage(encoded, width, height);
                    long validationMs = validationTimer.ElapsedMilliseconds;
                    if (!isValid)
                    {
                        Debug.WriteLine($"Captured image failed validation: {errorMessage}");
                        return (data: (byte[]?)null, encodeMs, validationMs);
                    }

                    return (data: encoded, encodeMs, validationMs);
                }, cancellationToken).ConfigureAwait(false);
                if (encodeResult.data is null)
                    return (null, 0, 0, default);

                SegmentationCaptureEncoder.SaveCaptureForReview(encodeResult.data, width, height);
                Debug.WriteLine(
                    $"[SegmentationProfile] Capture dimensions={width}x{height} render={renderMs}ms " +
                    $"readback={readbackMs}ms encode={encodeResult.encodeMs}ms validate={encodeResult.validationMs}ms " +
                    $"total={totalTimer.ElapsedMilliseconds}ms bytes={encodeResult.data.Length}");
                return (encodeResult.data, width, height, capturedBounds);
            }
            catch (OperationCanceledException)
            {
                return (null, 0, 0, default);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing viewport: {ex.Message}");
                return (null, 0, 0, default);
            }
        }

        /// <summary>
        /// GPU render and GetData stay on the UI dispatcher. Encode is not done here.
        /// </summary>
        private async Task<(Color[]? pixels, int width, int height, bool isGrayscale, GridRectangle capturedBounds, long renderMs, long readbackMs)> ReadViewportPixelsAsync(CancellationToken cancellationToken)
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return (null, 0, 0, false, default, 0, 0);

            if (!dispatcher.CheckAccess())
            {
                var operation = dispatcher.InvokeAsync(() => ReadViewportPixelsCoreAsync(cancellationToken));
                return await operation.Task.Unwrap().ConfigureAwait(false);
            }

            return await ReadViewportPixelsCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<(Color[]? pixels, int width, int height, bool isGrayscale, GridRectangle capturedBounds, long renderMs, long readbackMs)> ReadViewportPixelsCoreAsync(CancellationToken cancellationToken)
        {
            CancellationToken renderToken = PrepareCancellationToken(cancellationToken);
            var (graphicsDevice, scene, width, height) = ValidateRenderingContext();
            if (graphicsDevice is null || scene is null)
                return (null, 0, 0, false, default, 0, 0);

            Stopwatch renderTimer = Stopwatch.StartNew();
            var (renderTarget, isGrayscale) = await RenderViewportToTexture(scene, width, height, renderToken).ConfigureAwait(true);
            long renderMs = renderTimer.ElapsedMilliseconds;
            if (renderTarget is null)
                return (null, 0, 0, false, default, renderMs, 0);

            try
            {
                Stopwatch readbackTimer = Stopwatch.StartNew();
                Color[] pixels = new Color[width * height];
                renderTarget.GetData(pixels);
                long readbackMs = readbackTimer.ElapsedMilliseconds;
                GridRectangle capturedBounds = GetCurrentViewportBounds();
                ViewportBounds = capturedBounds;
                return (pixels, width, height, isGrayscale, capturedBounds, renderMs, readbackMs);
            }
            finally
            {
                renderTarget.Dispose();
            }
        }

        private static (bool isValid, string errorMessage) ValidateCapturedImage(byte[] pngData, int expectedWidth, int expectedHeight)
        {
            if (pngData is null || pngData.Length == 0)
                return (false, "Image validation failed: null or empty data");

            if (pngData.Length < 8 ||
                pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47 ||
                pngData[4] != 0x0D || pngData[5] != 0x0A || pngData[6] != 0x1A || pngData[7] != 0x0A)
            {
                return (false, "Image validation failed: invalid PNG signature");
            }

            if (expectedWidth <= 0 || expectedHeight <= 0)
                return (false, $"Image validation failed: invalid dimensions {expectedWidth}x{expectedHeight}");

            try
            {
                using MemoryStream stream = new(pngData);
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                if (image.Width != expectedWidth || image.Height != expectedHeight)
                {
                    return (false, $"Image validation failed: dimension mismatch. Expected {expectedWidth}x{expectedHeight}, got {image.Width}x{image.Height}");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Image validation failed: PNG decode error - {ex.Message}");
            }
        }

        private CancellationToken PrepareCancellationToken(CancellationToken externalToken)
        {
            linkedRenderCancellationTokenSource?.Cancel();
            linkedRenderCancellationTokenSource?.Dispose();
            linkedRenderCancellationTokenSource = null;

            renderCancellationTokenSource?.Cancel();
            renderCancellationTokenSource?.Dispose();
            renderCancellationTokenSource = new CancellationTokenSource();

            linkedRenderCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                externalToken,
                renderCancellationTokenSource.Token);
            return linkedRenderCancellationTokenSource.Token;
        }

        private (GraphicsDevice device, VikingXNA.Scene scene, int width, int height) ValidateRenderingContext()
        {
            var graphicsDevice = parent.Device;
            var scene = parent.Scene;
            if (graphicsDevice is null || scene is null)
                return (null, null, 0, 0);

            int width = scene.Viewport.Width;
            int height = scene.Viewport.Height;
            if (width <= 0 || height <= 0)
                return (null, null, 0, 0);

            return (graphicsDevice, scene, width, height);
        }

        private async Task<(RenderTarget2D? renderTarget, bool isGrayscale)> RenderViewportToTexture(
            VikingXNA.Scene scene, int width, int height, CancellationToken cancellationToken)
        {
            float centerX = scene.Camera.LookAt.X;
            float centerY = scene.Camera.LookAt.Y;
            int sectionZ = parent.Section.Number;

            try
            {
                bool isGrayscale = parent.CurrentChannelset.Length == 1;
                RenderTarget2D renderTarget = await parent.RenderSceneToTexture(
                    scene,
                    centerX,
                    centerY,
                    sectionZ,
                    showOverlays: false,
                    asyncTextureLoad: false,
                    cancellationToken).ConfigureAwait(false);

                return (renderTarget, isGrayscale);
            }
            catch (OperationCanceledException)
            {
                return (null, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenderSceneToTexture failed: {ex.Message}");
                return (null, false);
            }
        }

    }
}
