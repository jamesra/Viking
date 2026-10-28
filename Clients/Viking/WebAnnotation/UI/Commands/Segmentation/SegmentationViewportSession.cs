using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.DependencyInjection;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.UI;
using Viking.UI.Controls;
using Viking.VolumeModel;
using VikingXNA;
using WebAnnotation.UI;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using Polygon = Geometry.Polygon;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Uploads 1024×1024 mosaic tiles and converts SegmentTiles responses into world-space polygons.
    /// Shared by interactive SegmentationCommand and AutoCirclePolygonizeController.
    /// </summary>
    internal sealed partial class SegmentationViewportSession
    {
        private readonly SectionViewerControl parent;
        private SegmentationServiceTypes.SegmentationService.SegmentationServiceClient? grpcClient;

        private Geometry.Rectangle? uploadedImageBounds;
        private int uploadedImageWidth;
        private int uploadedImageHeight;
        private int isUploadingImage;
        /// <summary>
        /// Kind and detail published together so a reader never pairs one attempt's kind with
        /// another's detail when upload and segment finish on different threads.
        /// </summary>
        private sealed class SkipInfo(SegmentationSkipKind kind, string? detail)
        {
            public SegmentationSkipKind Kind { get; } = kind;

            public string? Detail { get; } = detail;
        }

        private SkipInfo? lastSkip;

        /// <summary>
        /// Reads and clears the last user-visible skip from upload/segment. Interactive
        /// Segment calls this after a null/false result so the status bar can explain why.
        /// Returns <see cref="SegmentationSkipKind.None"/> when no reason was recorded.
        /// </summary>
        public SegmentationSkipKind ConsumeLastSkip(out string? detail)
        {
            SkipInfo? skip = Interlocked.Exchange(ref lastSkip, null);
            detail = skip?.Detail;
            return skip?.Kind ?? SegmentationSkipKind.None;
        }

        private void RecordSkip(SegmentationSkipKind kind, string? detail = null)
        {
            Volatile.Write(ref lastSkip, new SkipInfo(kind, detail));
        }

        private void ClearSkip()
        {
            Volatile.Write(ref lastSkip, null);
        }
        /// <summary>
        /// Cancelled by <see cref="CancelPendingWork"/>, then replaced. Every tile render links its own token to it, so
        /// renders running at once for overlapping requests never cancel each other.
        /// </summary>
        private CancellationTokenSource renderCancellationTokenSource = new();
        private readonly object renderCancellationGate = new();

        /// <summary>
        /// Cancels the tile uploads this session started, and only <see cref="CancelPendingWork"/> cancels it. A caller that stops
        /// waiting for a tile (a superseded request, a re-planned prefetch) does not stop its upload: the server encodes a tile
        /// it has started receiving whatever the client does, so cancelling only made the next request send it again.
        /// </summary>
        private CancellationTokenSource tileUploadLifetime = new();

        /// <summary>The token tile uploads run under; see <see cref="tileUploadLifetime"/>.</summary>
        private CancellationToken TileUploadLifetimeToken
        {
            get
            {
                lock (renderCancellationGate)
                    return tileUploadLifetime.Token;
            }
        }
        private CancellationTokenSource? uploadCancellationTokenSource;
        private readonly TileUploadCache uploadedTileKeys = new();

        /// <summary>Blurred grayscale copies of the tiles captured for SAM2, for the edge snap.</summary>
        private readonly SegmentationTileImages tileImages = new();

        /// <summary>
        /// The tile images that cover each response, taken when it arrived and with the signature it was requested with,
        /// so a later view change cannot change what the response's contours are snapped to.
        /// </summary>
        private readonly ConditionalWeakTable<SegmentationResponse, TileImageSet> responseTileImages = new();
        private readonly Viking.Services.Grpc.IGrpcChannelManager? channelManager;
        private int mosaicDownsample = 1;

        /// <summary>
        /// Tile level each response was requested at. <see cref="mosaicDownsample"/> is overwritten by the next
        /// request while an earlier response is still being polygonized, so mapping a mask to world space reads this.
        /// </summary>
        private readonly SegmentationResultContexts resultContexts = new();

        /// <summary>
        /// The tile level every request is sent at. Every served model is a single-resolution tile grid, so the level does not
        /// follow zoom: a camera coarser than 1 still uploads at this level until
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>, which cannot exceed 6
        /// (default 2). A camera coarser than that preference does not upload.
        /// </summary>
        private const int TileLevel = 1;

        private static string? serverModelId;

        /// <summary>
        /// The <c>ModelInfo.model_id</c> the server reported in GetServerStatus, or null until it has answered (or when it
        /// reports none). Sent back as <c>model_id</c> on requests so the server can refuse prompts built for another model
        /// instead of segmenting them wrongly. Cleared with the profile when the endpoint changes.
        /// </summary>
        public static string? ServerModelId => Volatile.Read(ref serverModelId);

        /// <summary>The GetServerStatus query in flight or finished for the current endpoint; null until a session has connected.</summary>
        private static Task? serverStatusRefresh;

        /// <summary>
        /// The model id worth sending back on requests, or null when the server reported none. A server whose model has no
        /// manifest reports "unknown", which names no model and is not sent.
        /// </summary>
        internal static string? ModelIdToAdopt(Viking.gRPC.SegmentationServiceTypes.V1.ModelInfo? model)
        {
            string? id = model?.ModelId?.Trim();
            return string.IsNullOrEmpty(id) || string.Equals(id, "unknown", StringComparison.OrdinalIgnoreCase) ? null : id;
        }

        private static readonly object serverStatusGate = new();
        private static string? serverStatusEndpoint;

        /// <summary>
        /// Forgets the model of the previous server: <see cref="ServerModelId"/> is cleared and the next client connection asks
        /// the new server again. Called when the segmentation endpoint changes, or when the server refused a request built for
        /// another model.
        /// </summary>
        public static void ResetServerModel()
        {
            lock (serverStatusGate)
                serverStatusEndpoint = null;

            Volatile.Write(ref serverModelId, null);
            Volatile.Write(ref serverStatusRefresh, null);
        }

        /// <summary>
        /// Waits (at most <paramref name="timeoutSeconds"/>) for the GetServerStatus query a connection started, so
        /// <see cref="ServerModelId"/> is known before a request is built. Returns at once when the query has finished or none
        /// was started, and never throws: a server that does not answer leaves the model id unset.
        /// </summary>
        public static async Task WhenServerStatusKnownAsync(int timeoutSeconds = 6)
        {
            Task? refresh = Volatile.Read(ref serverStatusRefresh);
            if (refresh is null || refresh.IsCompleted)
                return;

            await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts one GetServerStatus per endpoint to learn the served model. Later connections to the same endpoint do
        /// nothing; a failed query is retried by the next connection. Never blocks the caller.
        /// </summary>
        private void BeginServerStatusRefresh(SegmentationServiceTypes.SegmentationService.SegmentationServiceClient client)
        {
            string endpoint;
            try
            {
                endpoint = SegmentationServiceSession.CurrentEndpoint();
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"GetServerStatus skipped: no endpoint ({ex.Message})");
                return;
            }

            lock (serverStatusGate)
            {
                if (string.Equals(serverStatusEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                    return;

                serverStatusEndpoint = endpoint;
            }

            Volatile.Write(ref serverStatusRefresh, RefreshServerStatusAsync(client, endpoint));
        }

        /// <summary>
        /// How long the status query a request waits on (for the model id) may take. A busy server answers slowly, and giving
        /// up after a few seconds made requests start without knowing the model.
        /// </summary>
        private const int StatusDeadlineSeconds = 60;

        /// <summary>Fetches GetServerStatus and installs the served model's id when it is still for the current endpoint.</summary>
        private static async Task RefreshServerStatusAsync(
            SegmentationServiceTypes.SegmentationService.SegmentationServiceClient client,
            string endpoint)
        {
            try
            {
                CallOptions options = new(deadline: DateTime.UtcNow.AddSeconds(StatusDeadlineSeconds));
                ServerStatusResponse status = await client
                    .GetServerStatusAsync(new ServerStatusRequest(), options)
                    .ResponseAsync.ConfigureAwait(false);

                SegmentationServiceHealth.ReportSuccess();
                string? modelId = ModelIdToAdopt(status?.Model);

                lock (serverStatusGate)
                {
                    if (!string.Equals(serverStatusEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                        return;

                    Volatile.Write(ref serverModelId, modelId);
                }

                if (status?.Model is { } model)
                    SegmentationDiag.Log($"GetServerStatus: model={model.ModelId} version={model.ModelVersion} encoding={model.PromptEncoding} data={model.TrainingDataSet} contract={model.ContractJson}");

                WebAnnotation.UI.AnnotationStatusChips.Refresh();
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"GetServerStatus failed: {ex.GetType().Name}: {ex.Message}");
                if (ex is RpcException statusError)
                    SegmentationServiceHealth.ReportRpcError(statusError);
                lock (serverStatusGate)
                {
                    if (string.Equals(serverStatusEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                        serverStatusEndpoint = null;
                }
            }
        }

        /// <summary>
        /// False when this session enforces the auto-polygonize cutoff (<see cref="EnforceAutoDownsampleLimit"/>)
        /// and the live camera is coarser than
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>.
        /// Equality still submits. The preference is capped at DS 6 (default 2), so a camera at DS 7
        /// never uploads. Called by <see cref="UploadCurrentImageAsync"/> and
        /// <see cref="SegmentAsync"/> before any tile is sent. Auto-segment checks the same
        /// cutoff earlier so an idle batch never starts. A manual operation (Resegment, add points,
        /// dilate/erode) never enforces it.
        /// </summary>
        private bool IsCameraWithinTileSubmission(bool recordSkip = true)
        {
            double cameraDownsample = parent.Camera?.Downsample ?? parent.Downsample;
            double maxCameraDownsample = WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample;
            if (CameraAllowsSubmission(cameraDownsample, maxCameraDownsample, EnforceAutoDownsampleLimit))
                return true;

            SegmentationDiag.Log(
                $"Tile submit skip: downsample out of range ds={cameraDownsample} max={maxCameraDownsample}");
            if (recordSkip)
                RecordSkip(SegmentationSkipKind.ZoomTooCoarse);
            return false;
        }

        /// <summary>
        /// True when a camera at <paramref name="cameraDownsample"/> may submit tiles: always when the limit is not
        /// enforced (a manual operation), otherwise only up to and including <paramref name="maxCameraDownsample"/>.
        /// </summary>
        internal static bool CameraAllowsSubmission(double cameraDownsample, double maxCameraDownsample, bool enforceLimit)
            => !enforceLimit || cameraDownsample <= maxCameraDownsample;

        /// <summary>
        /// True when this session refuses to submit while the camera is coarser than
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>. Only the auto-polygonize
        /// batch sets it. Manual operations leave it false, so the user can segment or edit a mask at any zoom.
        /// </summary>
        public bool EnforceAutoDownsampleLimit { get; set; }

        /// <summary>
        /// Maps camera downsample to the integer pyramid level sent to UploadTile/SegmentTiles.
        /// Values at or below 1 submit 1; larger values round up (ceil) and clamp to
        /// <see cref="TileLevel"/>, so 1.21 and 4 both submit 1.
        /// </summary>
        /// <param name="cameraDownsample">Live camera downsample (may be fractional).</param>
        public static int ResolveTileDownsample(double cameraDownsample)
        {
            if (double.IsNaN(cameraDownsample) || double.IsInfinity(cameraDownsample) || cameraDownsample <= 0)
                return 1;

            int roundedUp = (int)Math.Ceiling(cameraDownsample);
            if (roundedUp < 1)
                roundedUp = 1;

            return Math.Min(TileLevel, roundedUp);
        }

        /// <summary>
        /// True when two live camera downsamples map to the same tile pyramid level for upload/segment.
        /// </summary>
        public static bool SameResolvedTileDownsample(double cameraDownsampleA, double cameraDownsampleB)
        {
            return ResolveTileDownsample(cameraDownsampleA) == ResolveTileDownsample(cameraDownsampleB);
        }

        /// <summary>
        /// True when a stored mosaic tile pyramid level matches what
        /// <paramref name="liveCameraDownsample"/> resolves to for upload/segment.
        /// Stored values are not re-resolved (they are already a tile level from
        /// <see cref="MosaicDownsample"/> or <see cref="AutoPolygonizeUploadContext.Downsample"/>).
        /// </summary>
        public static bool StoredTileLevelMatchesLiveCamera(double storedMosaicTileDownsample, double liveCameraDownsample)
        {
            if (storedMosaicTileDownsample <= 0 ||
                double.IsNaN(storedMosaicTileDownsample) ||
                double.IsInfinity(storedMosaicTileDownsample))
            {
                return false;
            }

            return (int)storedMosaicTileDownsample ==
                   ResolveTileDownsample(liveCameraDownsample);
        }

        /// <summary>
        /// True when a stored upload matches the live view: same resolved tile level and world bounds within 1%.
        /// </summary>
        public static bool StoredUploadViewMatches(
            Geometry.Rectangle storedBounds,
            double storedMosaicTileDownsample,
            Geometry.Rectangle liveBounds,
            double liveCameraDownsample)
        {
            return StoredTileLevelMatchesLiveCamera(storedMosaicTileDownsample, liveCameraDownsample) &&
                   AreViewportBoundsSimilar(storedBounds, liveBounds);
        }

        /// <summary>
        /// True when two live camera views share a resolved tile pyramid level and world bounds differ by less than 1%.
        /// For a stored upload tile level, use <see cref="StoredUploadViewMatches"/> instead.
        /// </summary>
        public static bool LiveCameraViewMatches(
            Geometry.Rectangle referenceBounds,
            double referenceCameraDownsample,
            Geometry.Rectangle liveBounds,
            double liveCameraDownsample)
        {
            return SameResolvedTileDownsample(referenceCameraDownsample, liveCameraDownsample) &&
                   AreViewportBoundsSimilar(referenceBounds, liveBounds);
        }

        /// <summary>
        /// Binds the session to a viewer. ViewportBounds starts as the live camera rectangle.
        /// </summary>
        /// <param name="parent">The viewer to capture tiles from.</param>
        /// <param name="channelManager">
        /// Source of the shared gRPC channel. Null uses <see cref="ServiceLocator.GrpcChannelManager"/>; a caller
        /// that already holds the manager passes it so the session does not depend on the global locator.
        /// </param>
        public SegmentationViewportSession(SectionViewerControl parent, Viking.Services.Grpc.IGrpcChannelManager? channelManager = null)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.channelManager = channelManager;
            ViewportBounds = GetCurrentViewportBounds();
            mosaicDownsample = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
        }

        public Geometry.Rectangle ViewportBounds { get; set; }

        public Geometry.Rectangle? UploadedImageBounds => uploadedImageBounds;

        public int UploadedImageWidth => uploadedImageWidth;

        public int UploadedImageHeight => uploadedImageHeight;

        /// <summary>Pyramid level used for the last tile upload/segment (resolved from camera, capped).</summary>
        public int MosaicDownsample => mosaicDownsample > 0 ? mosaicDownsample : TileLevel;

        /// <summary>
        /// Tile level <paramref name="response"/> was requested at, fixed when the response arrived. Use this, not
        /// <see cref="MosaicDownsample"/>, for anything about a specific response: the session level moves on with the next request.
        /// </summary>
        public int DownsampleFor(SegmentationServiceTypes.SegmentationResponse response)
            => resultContexts.DownsampleOrDefault(response, MosaicDownsample);

        /// <summary>True when this session has accepted at least one UploadTile for the current view identity.</summary>
        public bool HasUploadedTiles => uploadedTileKeys.Count > 0;

        public bool IsUploading => Interlocked.CompareExchange(ref isUploadingImage, 0, 0) != 0;

        public bool HasClient => grpcClient is not null;

        /// <summary>
        /// Creates the gRPC stub from <see cref="ServiceLocator.GrpcChannelManager"/>. Safe to call repeatedly.
        /// Every call carries <see cref="Viking.ApplicationShutdown.Token"/>, so closing Viking cancels it.
        /// </summary>
        public bool TryInitializeClient()
        {
            try
            {
                var channel = (channelManager ?? ServiceLocator.GrpcChannelManager)?.GetOrCreateChannel();
                if (channel is null)
                {
                    SegmentationDiag.Log("TryInitializeClient: GrpcChannelManager/channel is null");
                    return false;
                }

                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(
                    Grpc.Core.Interceptors.ChannelExtensions.Intercept(channel, Viking.Services.Grpc.ShutdownCancellationInterceptor.Instance));
                BeginServerStatusRefresh(grpcClient);
                SegmentationDiag.Log("TryInitializeClient: ok");
                return true;
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"TryInitializeClient failed: {ex.Message}");
                Debug.WriteLine($"Failed to initialize segmentation gRPC client: {ex.Message}");
                grpcClient = null;
                return false;
            }
        }

        public Geometry.Rectangle GetCurrentViewportBounds()
        {
            Geometry.Vector2 topLeft = parent.ScreenToWorld(0, 0);
            Geometry.Vector2 bottomRight = parent.ScreenToWorld(parent.Width, parent.Height);
            return new Geometry.Rectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// True when corners differ by less than 1% of the larger side. Used to treat hitch jitter as the same view,
        /// including whether a PNG encode captured the same bounds as the live viewport before upload.
        /// </summary>
        public static bool AreViewportBoundsSimilar(Geometry.Rectangle a, Geometry.Rectangle b)
        {
            double tolerance = Math.Max(a.Width, a.Height) * 0.01;
            return Math.Abs(a.LowerLeft.X - b.LowerLeft.X) < tolerance &&
                   Math.Abs(a.LowerLeft.Y - b.LowerLeft.Y) < tolerance &&
                   Math.Abs(a.UpperRight.X - b.UpperRight.X) < tolerance &&
                   Math.Abs(a.UpperRight.Y - b.UpperRight.Y) < tolerance;
        }

        /// <summary>
        /// Cancels in-flight render, linked render, and upload tokens.
        /// </summary>
        public void CancelPendingWork()
        {
            CancellationTokenSource cancelled;
            CancellationTokenSource uploads;
            lock (renderCancellationGate)
            {
                cancelled = renderCancellationTokenSource;
                renderCancellationTokenSource = new CancellationTokenSource();
                uploads = tileUploadLifetime;
                tileUploadLifetime = new CancellationTokenSource();
            }

            cancelled.Cancel();
            uploads.Cancel();
            uploadCancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Forgets the recorded view and the tiles the server accepted for it, so the next request starts a new capture.
        /// The server's shared tile cache drops tiles itself: nothing is deleted there.
        /// </summary>
        public void ClearUploadedView()
        {
            uploadedImageBounds = null;
            uploadedImageWidth = 0;
            uploadedImageHeight = 0;
            uploadedTileKeys.Clear();
            tileImages.Clear();
            mosaicDownsample = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
            Interlocked.Exchange(ref isUploadingImage, 0);
        }

        /// <summary>
        /// Records the live viewport as the tile-mode capture. Does not upload every visible cell.
        /// SegmentAsync uploads the cells that contain foreground points, then any cells growth asks for.
        /// A full DS1 view is often larger than the server embedding cache (32). Uploading it first
        /// evicts the prompt cells, and the following SegmentTiles call returns TILE_NOT_FOUND.
        /// </summary>
        /// <returns>True when the camera is inside the tile-submission range and the viewport was recorded.</returns>
        public async Task<bool> UploadCurrentImageAsync(CancellationToken cancellationToken)
        {
            if (grpcClient is null)
            {
                RecordSkip(SegmentationSkipKind.NoClient);
                return false;
            }

            if (!IsCameraWithinTileSubmission())
                return false;

            if (Interlocked.CompareExchange(ref isUploadingImage, 1, 0) != 0)
                return false;

            AnnotationStatusChips.Refresh();
            try
            {
                uploadCancellationTokenSource?.Cancel();
                uploadCancellationTokenSource?.Dispose();
                uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                (int downsample, _, _, _) = await ReadViewTilesAsync().ConfigureAwait(false);
                if (uploadCancellationTokenSource.Token.IsCancellationRequested)
                {
                    RecordSkip(SegmentationSkipKind.Cancelled);
                    return false;
                }

                mosaicDownsample = downsample;
                uploadedImageBounds = ViewportBounds;
                uploadedImageWidth = SegmentationTileGrid.TileSize;
                uploadedImageHeight = SegmentationTileGrid.TileSize;
                ClearSkip();
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Tile upload cancelled due to view change");
                RecordSkip(SegmentationSkipKind.Cancelled);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tile upload error: {ex.Message}");
                if (ex is RpcException uploadError)
                    SegmentationServiceHealth.ReportRpcError(uploadError);
                RecordSkip(SegmentationSkipKind.UploadFailed, ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref isUploadingImage, 0);
                AnnotationStatusChips.Refresh();
            }

            return false;
        }

        /// <summary>
        /// Runs one SegmentTilesStream call naming the cells that contain foreground points. The server
        /// takes every cell it can from its own caches, which other users and earlier sessions fill too,
        /// grows the mask, and asks for the cells it lacks; the client renders and uploads only those, and
        /// the call returns the finished segmentation, one <see cref="SegmentResult"/> per island of the prompt.
        /// Does not name every viewport cell. Returns null when no mask could be produced.
        /// <paramref name="requestId"/> is sent with the request and echoed by the server; it only
        /// correlates log lines. Deciding whether a result is still wanted stays with the caller.
        /// </summary>
        /// <param name="foregroundBoxes">
        /// The seeds: circles and polygons go as foreground shapes, an edited bitmap as FOREGROUND mask tiles.
        /// </param>
        /// <param name="backgroundShapes">The neighbouring annotations, sent as background shapes as the object reaches them.</param>
        /// <param name="backgroundBitmap">A painted bitmap of what the object is not, sent as BACKGROUND mask tiles.</param>
        public async Task<SegmentationResponse?> SegmentAsync(
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            CancellationToken cancellationToken,
            IReadOnlyList<SegmentationSeed>? foregroundBoxes = null,
            ulong requestId = 0,
            SegmentationBackgroundShapes? backgroundShapes = null,
            SegmentationMaskUnion? backgroundBitmap = null)
        {
            if (grpcClient is null)
            {
                RecordSkip(SegmentationSkipKind.NoClient);
                return null;
            }

            if ((foregroundPoints is null || foregroundPoints.Count == 0) &&
                (backgroundPoints is null || backgroundPoints.Count == 0) &&
                (foregroundBoxes is null || foregroundBoxes.Count == 0))
                return null;

            if (!IsCameraWithinTileSubmission())
                return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int downsample, TileSignature signature, List<TileCell> visible, bool grayscale) =
                    await ReadViewTilesAsync().ConfigureAwait(false);
                mosaicDownsample = downsample;
                // The cells under the clicks, and under each seed's center: the seed says where the object is,
                // so its center cell is the best first tile to have ready.
                List<Geometry.Vector2> tilePoints = [.. foregroundPoints ?? []];
                foreach (SegmentationSeed seed in foregroundBoxes ?? [])
                    tilePoints.Add(seed.Center);
                List<TileCell> needed = SegmentationTileGrid.CellsContainingPoints(tilePoints, downsample);
                SegmentationDiag.Log(
                    $"SegmentAsync req={requestId} ds={downsample} visible={visible.Count} promptTiles={needed.Count} " +
                    $"vol={signature.Volume} sec={signature.Section}");
                if (needed.Count == 0)
                {
                    SegmentationDiag.Log("SegmentAsync: no tile cells for prompts");
                    RecordSkip(SegmentationSkipKind.TilesUnavailable);
                    return null;
                }

                // The prompt cells are named, not uploaded: the server takes what it already holds from its own
                // caches and asks for the rest in the stream.
                SegmentationResponse response = await SegmentTilesStreamAsync(
                    signature,
                    needed,
                    foregroundPoints,
                    backgroundPoints,
                    grayscale,
                    cancellationToken,
                    foregroundBoxes,
                    backgroundShapes,
                    requestId,
                    backgroundBitmap).ConfigureAwait(false);

                resultContexts.Record(response, downsample);
                await CaptureMissingTileImagesAsync(response, signature, downsample, grayscale, cancellationToken).ConfigureAwait(false);
                AttachTileImages(response, signature, downsample);
                if (ResponseWorldBounds(response, downsample) is Geometry.Rectangle bounds)
                {
                    uploadedImageBounds = bounds;
                    uploadedImageWidth = Math.Max(1, (int)Math.Round(bounds.Width / downsample));
                    uploadedImageHeight = Math.Max(1, (int)Math.Round(bounds.Height / downsample));
                }

                ClearSkip();
                SegmentationServiceHealth.ReportSuccess();
                return response;
            }
            catch (OperationCanceledException)
            {
                SegmentationDiag.Log("SegmentAsync cancelled");
                RecordSkip(SegmentationSkipKind.Cancelled);
                return null;
            }
            catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.FailedPrecondition &&
                rpcEx.Status.Detail.StartsWith("NO_MATCHING_MASK", StringComparison.Ordinal))
            {
                // The server found no candidate mask that fits the prompt (the starting rectangle, or the
                // clicks) and does not guess. There is nothing to retry; log what the server said.
                SegmentationDiag.Log($"SegmentAsync req={requestId} {rpcEx.Status.Detail}");
                Debug.WriteLine($"Segmentation found no matching mask: {rpcEx.Status.Detail}");
                SegmentationServiceHealth.ReportSuccess();
                RecordSkip(SegmentationSkipKind.Error, rpcEx.Status.Detail);
                return null;
            }
            catch (RpcException rpcEx) when (SegmentationRpcErrors.Describe(rpcEx) is not null)
            {
                // A timeout or a server limit says nothing is wrong with the request, so the user is
                // told that trying again can work instead of being shown the raw gRPC status.
                string reason = SegmentationRpcErrors.Describe(rpcEx)!;
                SegmentationDiag.Log($"SegmentAsync req={requestId} {rpcEx.StatusCode}: {rpcEx.Status.Detail}");
                if (rpcEx.StatusCode == StatusCode.FailedPrecondition)
                {
                    // The server now serves a different model than the one the next prompts were built for. Forget it so
                    // the next connection asks GetServerStatus again.
                    ResetServerModel();
                }

                SegmentationServiceHealth.ReportRpcError(rpcEx);
                RecordSkip(SegmentationSkipKind.Error, reason);
                return null;
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"SegmentAsync error: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"Segmentation error: {ex.Message}");
                if (ex is RpcException callError)
                    SegmentationServiceHealth.ReportRpcError(callError);
                RecordSkip(SegmentationSkipKind.Error, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Decodes each island's logits and polygonizes them, island 1 (the largest) first. Cleanup and marching squares run
        /// on the logits' native grid (one sample per four pixels) and the world transform scales the ring up. Callers
        /// apply pen-threshold simplification. <paramref name="cancellationToken"/> is checked between islands so a newer
        /// click can abort. <paramref name="keepComponentsContainingWorldPoints"/>, when given, hit-tests each island's mask at
        /// those world points and polygonizes only the connected pieces they land on (an island no point lands on is kept
        /// whole). <paramref name="primaryIslandOnly"/> traces island 1 alone, for a caller where only one shape fits.
        /// </summary>
        public IReadOnlyList<Polygon> CreatePolygonsFromResponse(
            SegmentationResponse response,
            double? holeDropFraction = null,
            IReadOnlyList<Geometry.Vector2>? preserveHolesContainingWorldPoints = null,
            int? edgeCleanupRadius = null,
            CancellationToken cancellationToken = default,
            IReadOnlyList<Geometry.Vector2>? keepComponentsContainingWorldPoints = null,
            bool primaryIslandOnly = false)
        {
            if (response is null || response.Segments.Count == 0)
                return [];

            Stopwatch totalTimer = Stopwatch.StartNew();
            long decodeMs = 0;
            long cleanupMs = 0;
            long polygonizeMs = 0;
            int logitBytes = 0;
            int foregroundBefore = 0;
            int foregroundAfter = 0;
            double dropFraction = holeDropFraction ?? WebAnnotation.Global.AnnotationSettings.SegmentationHoleDropFraction;
            int cleanupRadius = edgeCleanupRadius ?? WebAnnotation.Global.AnnotationSettings.SegmentationEdgeCleanupRadius;
            int downsample = Math.Max(1, DownsampleFor(response));
            List<Polygon> polygons = [];
            IEnumerable<SegmentationServiceTypes.SegmentResult> islands = primaryIslandOnly
                ? [response.GetPrimaryIsland()!]
                : response.GetSegmentsByIsland();
            foreach (var segment in islands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logitBytes += segment.Logits.Length;
                Stopwatch decodeTimer = Stopwatch.StartNew();
                SegmentField field = DecodeSegmentField(segment);
                decodeMs += decodeTimer.ElapsedMilliseconds;
                if (field.Data is null)
                    continue;

                // The frame is the island's own logit grid: offsets are 0, the field fills it, and the world rectangle
                // is where logits_origin and logits_scale put it, moved half a sample so each sample sits at its centre.
                Geometry.Rectangle traceBounds = SegmentationMaskPolygonizer.CenterSampleBounds(
                    FieldWorldBounds(field, downsample),
                    field.Width,
                    field.Height,
                    field.Scale);

                Stopwatch polygonizeTimer = Stopwatch.StartNew();
                polygons.AddRange(SegmentationMaskPolygonizer.CreatePolygons(
                    field.Data,
                    field.Width,
                    field.Height,
                    0,
                    0,
                    field.Width,
                    field.Height,
                    traceBounds,
                    dropFraction,
                    preserveHolesContainingWorldPoints,
                    SegmentationMaskPolygonizer.ScaleCleanupRadius(cleanupRadius, field.Scale),
                    out SegmentationMaskPolygonizer.CleanupStats cleanupStats,
                    keepComponentsContainingWorldPoints,
                    WebAnnotation.Global.AnnotationSettings.SegmentationEdgeSnapEnabled ? CreateEdgeRefiner(response) : null,
                    WebAnnotation.Global.AnnotationSettings.SegmentationMinFeatureWorld));
                cleanupMs += cleanupStats.ElapsedMilliseconds;
                polygonizeMs += Math.Max(0, polygonizeTimer.ElapsedMilliseconds - cleanupStats.ElapsedMilliseconds);
                foregroundBefore += cleanupStats.ForegroundPixelsBefore;
                foregroundAfter += cleanupStats.ForegroundPixelsAfter;
            }

            int vertexCount = polygons.Sum(polygon =>
                polygon.ExteriorRing.Length + polygon.InteriorRings.Sum(ring => ring.Length));
            Debug.WriteLine(
                $"[SegmentationProfile] Client polygons islands={response.Segments.Count} primaryOnly={primaryIslandOnly} " +
                $"logitBytes={logitBytes} decode={decodeMs}ms cleanup={cleanupMs}ms " +
                $"pixels={foregroundBefore}->{foregroundAfter} polygonize={polygonizeMs}ms " +
                $"polygons={polygons.Count} vertices={vertexCount} total={totalTimer.ElapsedMilliseconds}ms");
            return polygons;
        }

        /// <summary>
        /// Remembers the captured tile images that cover <paramref name="response"/>, for <see cref="CreateEdgeRefiner"/>.
        /// </summary>
        private void AttachTileImages(SegmentationResponse response, TileSignature signature, int downsample)
        {
            responseTileImages.Remove(response);
            if (ResponseWorldBounds(response, downsample) is not Geometry.Rectangle bounds)
                return;

            TileImageSet? images = tileImages.Snapshot(
                downsample,
                (row, column) => TileKey(signature, row, column),
                bounds);
            if (images is not null)
                responseTileImages.Add(response, images);
        }

        /// <summary>
        /// A function that moves a polygon traced from <paramref name="response"/> onto the nearest strong bright-to-dark
        /// image edge (<see cref="SegmentationEdgeSnap"/>), or null when no captured tile covers the response. It holds
        /// the images as they were when the response arrived, so it stays valid however long a proposal keeps it, and it
        /// does not look at the preference: callers decide whether to use it.
        /// </summary>
        public Func<Polygon, Polygon>? CreateEdgeRefiner(SegmentationResponse response)
        {
            if (response is null || !responseTileImages.TryGetValue(response, out TileImageSet? images))
                return null;

            return polygon =>
            {
                Polygon refined = SegmentationEdgeSnap.Refine(
                    polygon,
                    images.Sample,
                    images.Downsample,
                    new EdgeSnapOptions { ReferenceEdgeLevelsPerPixel = images.EdgeScale },
                    out EdgeSnapStats stats);
                SegmentationDiag.Log(
                    $"EdgeSnap rings={stats.Rings} snapped={stats.Snapped} meanOffset={stats.MeanOffsetPixels:+0.0;-0.0}px " +
                    $"meanAbs={stats.MeanAbsoluteOffsetPixels:0.0}px reducedRange={stats.ReducedRange} edgeScale={images.EdgeScale:0.0}");
                return refined;
            };
        }

        private readonly ConditionalWeakTable<SegmentationServiceTypes.SegmentResult, SegmentField> decodedFields = new();

        /// <summary>
        /// The soft field of <paramref name="segment"/>'s logits at their native resolution, with the boundary at the mask
        /// threshold the request used, decoded once. Polygonizing and the mask overlay both need the same samples, and the
        /// decode is the costly part of a large mask. The returned array is shared: callers must not write to it (the
        /// polygonizer and overlay only read).
        /// </summary>
        public SegmentField DecodeSegmentField(SegmentationServiceTypes.SegmentResult segment) =>
            decodedFields.GetValue(segment, s => SegmentationLogitField.Decode(
                s,
                WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold,
                WebAnnotation.Global.AnnotationSettings.SegmentationSmoothingSigma));

        /// <summary>
        /// Reads the camera rectangle on the UI dispatcher so off-UI encode/publish can compare against the live view.
        /// </summary>
        public Task<Geometry.Rectangle> GetLiveViewportBoundsAsync()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                return Task.FromResult(GetCurrentViewportBounds());

            return dispatcher.InvokeAsync(GetCurrentViewportBounds).Task;
        }

        /// <summary>
        /// World rectangle of a decoded field: the footprint of its samples at the grid's <paramref name="downsample"/>. The
        /// logits' origin is a pixel index, the top-left pixel of sample (0, 0) in grid space (Y up), and pixel (x, y) covers
        /// [x, x + 1) x [y, y + 1), so the footprint's top edge is origin.y + 1.
        /// </summary>
        internal static Geometry.Rectangle FieldWorldBounds(SegmentField field, int downsample) =>
            LogitWorldBounds(field.X, field.Y, field.PixelWidth, field.PixelHeight, downsample);

        /// <summary>
        /// World rectangle of <paramref name="segment"/>'s logits for a response requested at <paramref name="downsample"/>:
        /// sample (i, j) covers grid pixel columns origin.x + i * scale .. origin.x + (i + 1) * scale - 1 and rows
        /// origin.y - (j + 1) * scale + 1 .. origin.y - j * scale. Null when the segment carries no usable logits.
        /// </summary>
        internal static Geometry.Rectangle? SegmentWorldBounds(SegmentationServiceTypes.SegmentResult segment, int downsample)
        {
            if (!SegmentationLogitField.HasLogits(segment))
                return null;

            int scale = SegmentationLogitField.PixelScale(segment);
            return LogitWorldBounds(
                segment.LogitsOrigin.X,
                segment.LogitsOrigin.Y,
                segment.LogitsSize.Width * scale,
                segment.LogitsSize.Height * scale,
                downsample);
        }

        /// <summary>
        /// The world rectangle of <paramref name="pixelWidth"/> x <paramref name="pixelHeight"/> grid pixels whose top-left
        /// pixel is (<paramref name="originX"/>, <paramref name="originY"/>), Y up, at <paramref name="downsample"/>.
        /// </summary>
        private static Geometry.Rectangle LogitWorldBounds(int originX, int originY, int pixelWidth, int pixelHeight, int downsample)
        {
            double ds = Math.Max(1, downsample);
            double top = originY + 1.0;
            return new Geometry.Rectangle(
                new Geometry.Vector2(originX * ds, (top - pixelHeight) * ds),
                new Geometry.Vector2((originX + (double)pixelWidth) * ds, top * ds));
        }

        /// <summary>The world rectangle holding every island of <paramref name="response"/>; null when none carries logits.</summary>
        internal static Geometry.Rectangle? ResponseWorldBounds(SegmentationResponse response, int downsample)
        {
            Geometry.Rectangle? union = null;
            foreach (SegmentationServiceTypes.SegmentResult segment in response?.Segments ?? [])
            {
                if (SegmentWorldBounds(segment, downsample) is not Geometry.Rectangle bounds)
                    continue;

                union = union is Geometry.Rectangle sofar ? Geometry.Rectangle.Union(sofar, bounds) : bounds;
            }

            return union;
        }

        /// <summary>
        /// World rectangle of <paramref name="field"/>, decoded from an island of <paramref name="response"/>; the response
        /// says which tile level its logits' origin is at.
        /// </summary>
        public Geometry.Rectangle GetSegmentWorldBounds(SegmentationResponse response, SegmentField field)
            => FieldWorldBounds(field, DownsampleFor(response));

        /// <summary>
        /// A token for one tile render: cancelled by <paramref name="externalToken"/> or <see cref="CancelPendingWork"/>.
        /// The caller disposes it. A new render no longer cancels the one before it: with several requests in
        /// flight, that made each request's capture cancel the other's.
        /// </summary>
        private CancellationTokenSource CreateRenderCancellation(CancellationToken externalToken)
        {
            lock (renderCancellationGate)
                return CancellationTokenSource.CreateLinkedTokenSource(externalToken, renderCancellationTokenSource.Token);
        }

    }
}
