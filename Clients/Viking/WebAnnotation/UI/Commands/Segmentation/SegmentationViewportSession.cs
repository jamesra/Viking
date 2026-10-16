using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        private SegmentationServiceTypes.SegmentationService.SegmentationServiceClient grpcClient;

        private ulong? currentImageId;
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
        private CancellationTokenSource renderCancellationTokenSource;
        private CancellationTokenSource linkedRenderCancellationTokenSource;
        private CancellationTokenSource uploadCancellationTokenSource;
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
        private int mosaicOriginX;
        private int mosaicOriginY;

        /// <summary>
        /// Ceiling for submitted tile downsample from appSettings
        /// <c>SegmentationTileDownsample</c> (default 1). Camera zoom may submit a finer
        /// level down to 1; never coarser than this. A coarser camera still uploads at this
        /// level until <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>,
        /// which cannot exceed 4 (default 2). A camera coarser than that preference does not upload.
        /// </summary>
        public static int MaxTileDownsample { get; } =
            int.TryParse(ConfigurationManager.AppSettings["SegmentationTileDownsample"], out int ds) && ds >= 1
                ? ds
                : 1;

        private static SegmentationModelProfile modelProfile = SegmentationModelProfile.Default;

        private static string? serverModelId;

        /// <summary>
        /// The <c>ModelInfo.model_id</c> the server reported in GetServerStatus, or null until it has answered (or when it
        /// reports none). Sent back as <c>model_id</c> on requests so the server can refuse prompts built for another model
        /// instead of segmenting them wrongly. Cleared with the profile when the endpoint changes.
        /// </summary>
        public static string? ServerModelId => Volatile.Read(ref serverModelId);

        /// <summary>
        /// How the model in use wants its input: fixed grid tiles or the whole screen, and whether the
        /// tile level follows zoom. Starts as <see cref="SegmentationModelProfile.Default"/> (fixed tiles,
        /// single resolution) and is replaced by <see cref="AdoptModelCapabilities"/>.
        /// Drives <see cref="SegmentationCameraPolicy"/>.
        /// </summary>
        public static SegmentationModelProfile ModelProfile => Volatile.Read(ref modelProfile);

        /// <summary>
        /// Adopts the capabilities the server advertised in GetServerStatus. The local ceiling can only
        /// narrow the result. Throws if the server left a flag unspecified; the current profile is kept.
        /// </summary>
        public static void AdoptModelCapabilities(Viking.gRPC.SegmentationServiceTypes.V1.ModelCapabilities advertised)
            => Volatile.Write(ref modelProfile, SegmentationModelProfile.FromAdvertised(advertised, MaxTileDownsample));

        /// <summary>
        /// Profile worth adopting from <paramref name="advertised"/>, or null to keep the current one. Null when the
        /// server left a mode unspecified (a server bug that must not change how input is submitted) or asks for
        /// full-viewport submission: this client only implements the fixed tile path, so adopting that profile
        /// would stop every request from being submittable.
        /// </summary>
        internal static SegmentationModelProfile? ProfileToAdopt(
            Viking.gRPC.SegmentationServiceTypes.V1.ModelCapabilities? advertised,
            int maxTileDownsample)
        {
            if (advertised is null)
                return null;

            SegmentationModelProfile profile;
            try
            {
                profile = SegmentationModelProfile.FromAdvertised(advertised, maxTileDownsample);
            }
            catch (ArgumentException ex)
            {
                SegmentationDiag.Log($"GetServerStatus: ignoring capabilities: {ex.Message}");
                return null;
            }

            if (!profile.UsesFixedTiles)
            {
                SegmentationDiag.Log("GetServerStatus: server wants full-viewport submission, which this client cannot send; keeping fixed tiles");
                return null;
            }

            return profile;
        }

        /// <summary>
        /// The model id worth sending back on requests, or null when the server reported none. A server whose model has no
        /// manifest reports "unknown", which names no model and is not sent.
        /// </summary>
        internal static string? ModelIdToAdopt(Viking.gRPC.SegmentationServiceTypes.V1.ModelInfo? model)
        {
            string? id = model?.ModelId?.Trim();
            return string.IsNullOrEmpty(id) || string.Equals(id, "unknown", StringComparison.OrdinalIgnoreCase) ? null : id;
        }

        private static readonly object capabilitiesGate = new();
        private static string? capabilitiesEndpoint;

        /// <summary>
        /// Forgets the capabilities of the previous server: the profile returns to
        /// <see cref="SegmentationModelProfile.Default"/> and the next client connection asks the new server again.
        /// Called when the segmentation endpoint changes.
        /// </summary>
        public static void ResetModelProfile()
        {
            lock (capabilitiesGate)
                capabilitiesEndpoint = null;

            Volatile.Write(ref modelProfile, SegmentationModelProfile.Default);
            Volatile.Write(ref serverModelId, null);
        }

        /// <summary>
        /// Starts one GetServerStatus per endpoint to learn the model's capabilities. Later connections to the same
        /// endpoint do nothing; a failed query is retried by the next connection. Never blocks the caller.
        /// </summary>
        private void BeginModelCapabilitiesRefresh(SegmentationServiceTypes.SegmentationService.SegmentationServiceClient client)
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

            lock (capabilitiesGate)
            {
                if (string.Equals(capabilitiesEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                    return;

                capabilitiesEndpoint = endpoint;
            }

            _ = RefreshModelCapabilitiesAsync(client, endpoint);
        }

        /// <summary>
        /// Fetches GetServerStatus and installs the advertised profile when it is still for the current endpoint.
        /// </summary>
        private static async Task RefreshModelCapabilitiesAsync(
            SegmentationServiceTypes.SegmentationService.SegmentationServiceClient client,
            string endpoint)
        {
            try
            {
                CallOptions options = new(deadline: DateTime.UtcNow.AddSeconds(5));
                ServerStatusResponse status = await client
                    .GetServerStatusAsync(new ServerStatusRequest(), options)
                    .ResponseAsync.ConfigureAwait(false);

                SegmentationServiceHealth.ReportSuccess();
                SegmentationModelProfile? profile = ProfileToAdopt(status?.Capabilities, MaxTileDownsample);
                string? modelId = ModelIdToAdopt(status?.Model);

                lock (capabilitiesGate)
                {
                    if (!string.Equals(capabilitiesEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                        return;

                    // The model identity is independent of the capabilities: a server whose capabilities we ignore still
                    // refuses prompts built for a different model.
                    Volatile.Write(ref serverModelId, modelId);
                    if (profile is not null)
                        Volatile.Write(ref modelProfile, profile);
                }

                if (status?.Model is { } model)
                    SegmentationDiag.Log($"GetServerStatus: model={model.ModelId} version={model.ModelVersion} encoding={model.PromptEncoding} data={model.TrainingDataSet} contract={model.ContractJson}");

                if (profile is null)
                    return;

                SegmentationDiag.Log($"GetServerStatus: adopted submission={profile.Submission} resolution={profile.Resolution}");
                WebAnnotation.UI.AnnotationStatusChips.Refresh();
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"GetServerStatus failed: {ex.GetType().Name}: {ex.Message}");
                if (ex is RpcException statusError)
                    SegmentationServiceHealth.ReportRpcError(statusError);
                lock (capabilitiesGate)
                {
                    if (string.Equals(capabilitiesEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                        capabilitiesEndpoint = null;
                }
            }
        }

        /// <summary>
        /// Highest level the current model may be sent: <see cref="MaxTileDownsample"/> when its profile
        /// lets the level change, otherwise 1.
        /// </summary>
        private static int EffectiveTileCeiling => ModelProfile.TileLevelCanChange ? MaxTileDownsample : 1;

        /// <summary>
        /// False when this session enforces the auto-polygonize cutoff (<see cref="EnforceAutoDownsampleLimit"/>)
        /// and the live camera is coarser than
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>.
        /// Equality still submits. The preference is capped at DS 4 (default 2), so a camera at DS 5
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
        /// <see cref="MaxTileDownsample"/>. Example: 1.21 and 4 both submit 1 when max is 1.
        /// </summary>
        /// <param name="cameraDownsample">Live camera downsample (may be fractional).</param>
        public static int ResolveTileDownsample(double cameraDownsample)
        {
            if (double.IsNaN(cameraDownsample) || double.IsInfinity(cameraDownsample) || cameraDownsample <= 0)
                return 1;

            int roundedUp = (int)Math.Ceiling(cameraDownsample);
            if (roundedUp < 1)
                roundedUp = 1;

            return Math.Min(EffectiveTileCeiling, roundedUp);
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

        public ulong? CurrentImageId => currentImageId;

        public Geometry.Rectangle? UploadedImageBounds => uploadedImageBounds;

        public int UploadedImageWidth => uploadedImageWidth;

        public int UploadedImageHeight => uploadedImageHeight;

        /// <summary>Pyramid level used for the last tile upload/segment (resolved from camera, capped).</summary>
        public int MosaicDownsample => mosaicDownsample > 0 ? mosaicDownsample : EffectiveTileCeiling;

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

                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(channel);
                BeginModelCapabilitiesRefresh(grpcClient);
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
            uploadedTileKeys.Clear();
            tileImages.Clear();
            mosaicDownsample = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
            mosaicOriginX = 0;
            mosaicOriginY = 0;
            Interlocked.Exchange(ref isUploadingImage, 0);
        }

        /// <summary>
        /// Installs a previously uploaded SAM2 image so SegmentImage can reuse it.
        /// Prompt mapping uses <paramref name="worldBounds"/>, not the live camera.
        /// NotFound still re-uploads via <see cref="SegmentAsync"/>.
        /// </summary>
        public void AdoptUploadedImage(ulong imageId, Geometry.Rectangle worldBounds, int width, int height)
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
        /// Records the live viewport as the tile-mode capture. Does not upload every visible cell.
        /// SegmentAsync uploads the cells that contain foreground points, then any cells growth asks for.
        /// A full DS1 view is often larger than the server embedding cache (32). Uploading it first
        /// evicts the prompt cells, and the following SegmentTiles call returns TILE_NOT_FOUND.
        /// Tile mode does not use UploadImage; CurrentImageId stays unset.
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
                currentImageId = null;
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
        /// Calls SegmentTiles. On TILE_NOT_FOUND, drops that cell and uploads it again.
        /// One retry is not enough: the cache evicts several prompt cells, and the second
        /// missing cell used to abort the whole request.
        /// Returns null when a cell still cannot be uploaded.
        /// </summary>
        private async Task<SegmentationResponse?> SegmentWithMissingTileRetriesAsync(
            TileSignature signature,
            List<TileCell> needed,
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            bool grayscale,
            CancellationToken cancellationToken,
            IReadOnlyList<SegmentationSeed>? foregroundBoxes = null,
            SegmentationMaskUnion? backgroundMask = null,
            ulong requestId = 0)
        {
            const int maxNotFoundRetries = 16;
            string? lastMissingKey = null;
            int sameTileMisses = 0;
            for (int attempt = 0; attempt <= maxNotFoundRetries; attempt++)
            {
                try
                {
                    return await SegmentTilesStreamAsync(
                        signature,
                        needed,
                        foregroundPoints,
                        backgroundPoints,
                        grayscale,
                        cancellationToken,
                        foregroundBoxes,
                        backgroundMask,
                        requestId).ConfigureAwait(false);
                }
                catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.NotFound &&
                    TryParseMissingTile(rpcEx.Status.Detail, out int missingRow, out int missingCol))
                {
                    string missingKey = TileKey(signature, missingRow, missingCol);
                    if (missingKey == lastMissingKey)
                    {
                        sameTileMisses++;
                        if (sameTileMisses >= 2)
                            throw;
                    }
                    else
                    {
                        lastMissingKey = missingKey;
                        sameTileMisses = 0;
                    }

                    SegmentationDiag.Log(
                        $"SegmentTiles TILE_NOT_FOUND row={missingRow} col={missingCol} retry={attempt}");
                    uploadedTileKeys.Remove(missingKey);
                    TileCell missing = new(missingRow, missingCol);
                    if (!needed.Any(cell => cell.Row == missingRow && cell.Col == missingCol))
                        needed.Add(missing);

                    if (!await UploadMissingTilesAsync(
                        signature,
                        [missing],
                        grayscale,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Best-effort DeleteImage RPC, then clears CurrentImageId. Failures are logged only.
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
        /// Uploads the cells that contain foreground points, then runs one SegmentTilesStream call.
        /// The server grows the mask and asks for further cells as the mask reaches them; the client
        /// uploads those and the call returns the finished mask. Does not name every viewport cell.
        /// Returns null when no mask could be produced.
        /// <paramref name="requestId"/> is sent with the request and echoed by the server; it only
        /// correlates log lines. Deciding whether a result is still wanted stays with the caller.
        /// </summary>
        public async Task<SegmentationResponse?> SegmentAsync(
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            CancellationToken cancellationToken,
            IReadOnlyList<SegmentationSeed>? foregroundBoxes = null,
            ulong requestId = 0,
            SegmentationMaskUnion? backgroundMask = null)
        {
            if (grpcClient is null)
            {
                RecordSkip(SegmentationSkipKind.NoClient);
                return null;
            }

            if ((foregroundPoints is null || foregroundPoints.Count == 0) &&
                (backgroundPoints is null || backgroundPoints.Count == 0))
                return null;

            if (!IsCameraWithinTileSubmission())
                return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int downsample, TileSignature signature, List<TileCell> visible, bool grayscale) =
                    await ReadViewTilesAsync().ConfigureAwait(false);
                mosaicDownsample = downsample;
                // The cells under the clicks, and under each box's center: the box says where the object is,
                // so its center cell is the best first tile to have ready.
                List<Geometry.Vector2> tilePoints = [.. foregroundPoints ?? []];
                foreach (SegmentationSeed seed in foregroundBoxes ?? [])
                    tilePoints.Add(seed.Center);
                List<TileCell> needed = SegmentationTileGrid.CellsContainingPoints(tilePoints, downsample);
                SegmentationDiag.Log(
                    $"SegmentAsync ds={downsample} visible={visible.Count} promptTiles={needed.Count} " +
                    $"vol={signature.Volume} sec={signature.Section}");
                if (needed.Count == 0)
                {
                    SegmentationDiag.Log("SegmentAsync: no tile cells for prompts");
                    RecordSkip(SegmentationSkipKind.TilesUnavailable);
                    return null;
                }

                if (!await UploadMissingTilesAsync(signature, needed, grayscale, cancellationToken).ConfigureAwait(false))
                {
                    SegmentationDiag.Log("SegmentAsync: UploadMissingTilesAsync returned false");
                    Debug.WriteLine("No segmentation tiles could be uploaded");
                    RecordSkip(SegmentationSkipKind.UploadFailed);
                    return null;
                }

                SegmentationResponse? response = await SegmentWithMissingTileRetriesAsync(
                    signature,
                    needed,
                    foregroundPoints,
                    backgroundPoints,
                    grayscale,
                    cancellationToken,
                    foregroundBoxes,
                    backgroundMask,
                    requestId).ConfigureAwait(false);
                if (response is null)
                {
                    RecordSkip(SegmentationSkipKind.TilesUnavailable);
                    return null;
                }

                resultContexts.Record(response, downsample);
                AttachTileImages(response, signature, downsample);
                mosaicOriginX = response.OriginX;
                mosaicOriginY = response.OriginY;
                uploadedImageBounds = MosaicWorldBounds(response, DownsampleFor(response));
                int responseScale = response.PixelScale();
                uploadedImageWidth = Math.Max(1, response.Width) * responseScale;
                uploadedImageHeight = Math.Max(1, response.Height) * responseScale;
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
                    ResetModelProfile();
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
        /// Decodes each segment and polygonizes in score order. Near-full-frame masks are skipped.
        /// Cleanup and marching squares run at the field's own resolution: a segment that carries SAM2's logits is
        /// traced on that native grid (one sample per four pixels) and the world transform scales the ring up; a
        /// segment that only has a mask PNG is traced at pixel resolution. Callers apply pen-threshold simplification.
        /// <paramref name="cancellationToken"/> is checked between segments so a newer click can abort.
        /// <paramref name="keepComponentsContainingWorldPoints"/>, when given, hit-tests the mask at those
        /// world points and polygonizes only the connected pieces they land on.
        /// </summary>
        public IReadOnlyList<Polygon> CreatePolygonsFromResponse(
            SegmentationResponse response,
            double? holeDropFraction = null,
            IReadOnlyList<Geometry.Vector2> preserveHolesContainingWorldPoints = null,
            int? edgeCleanupRadius = null,
            CancellationToken cancellationToken = default,
            IReadOnlyList<Geometry.Vector2> keepComponentsContainingWorldPoints = null)
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
            List<Polygon> polygons = [];
            foreach (var segment in response.GetSegmentsByDescendingScore())
            {
                cancellationToken.ThrowIfCancellationRequested();
                maskBytes += segment.Mask.Length + segment.Logits.Length;
                Stopwatch decodeTimer = Stopwatch.StartNew();
                SegmentField field = DecodeSegmentField(segment);
                decodeMs += decodeTimer.ElapsedMilliseconds;
                if (field.Data is null)
                    continue;

                // The frame is counted in samples, like the field: response.Width is already reduced when the
                // server did not upscale, and the field's offset is a whole number of samples.
                int captureWidth = Math.Max(1, response.Width > 0 ? response.Width : uploadedImageWidth / field.Scale);
                int captureHeight = Math.Max(1, response.Height > 0 ? response.Height : uploadedImageHeight / field.Scale);
                Geometry.Rectangle mosaicBounds = SegmentationMaskPolygonizer.CenterSampleBounds(
                    MosaicWorldBounds(response, DownsampleFor(response)),
                    captureWidth,
                    captureHeight,
                    field.Scale);

                Stopwatch polygonizeTimer = Stopwatch.StartNew();
                polygons.AddRange(SegmentationMaskPolygonizer.CreatePolygons(
                    field.Data,
                    field.Width,
                    field.Height,
                    field.X / field.Scale,
                    field.Y / field.Scale,
                    captureWidth,
                    captureHeight,
                    mosaicBounds,
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
                $"[SegmentationProfile] Client polygons image={currentImageId} segments={response.Segments.Count} " +
                $"maskBytes={maskBytes} decode={decodeMs}ms cleanup={cleanupMs}ms " +
                $"pixels={foregroundBefore}->{foregroundAfter} polygonize={polygonizeMs}ms " +
                $"polygons={polygons.Count} vertices={vertexCount} total={totalTimer.ElapsedMilliseconds}ms");
            return polygons;
        }

        /// <summary>
        /// Remembers the captured tile images that cover <paramref name="response"/>, for <see cref="CreateEdgeRefiner"/>.
        /// </summary>
        private void AttachTileImages(SegmentationResponse response, TileSignature signature, int downsample)
        {
            TileImageSet? images = tileImages.Snapshot(
                downsample,
                (row, column) => TileKey(signature, row, column),
                MosaicWorldBounds(response, downsample));
            responseTileImages.Remove(response);
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
        /// The soft field of <paramref name="segment"/>, decoded once. Polygonizing and the mask overlay both need the
        /// same samples, and the decode is the costly part of a large mask. The returned array is shared: callers
        /// must not write to it (the polygonizer and overlay only read).
        /// </summary>
        /// <remarks>
        /// A segment with logits becomes a field at the logits' native resolution, with the boundary at the mask
        /// threshold the request used. Otherwise its mask PNG is decoded at pixel resolution (a server that does
        /// not know the response flags, or one that had no logits to send). Field X and Y are image pixels in the
        /// frame <see cref="SegmentationServiceTypes.SegmentResult.X"/> uses.
        /// </remarks>
        public SegmentField DecodeSegmentField(SegmentationServiceTypes.SegmentResult segment) =>
            decodedFields.GetValue(segment, s =>
            {
                if (SegmentationLogitField.HasLogits(s))
                    return SegmentationLogitField.Decode(
                        s,
                        WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold,
                        WebAnnotation.Global.AnnotationSettings.SegmentationSmoothingSigma);

                (byte[]? data, int width, int height) = DecodePngMask(s.Mask.ToByteArray());
                return new SegmentField(data, width, height, 1, s.X, s.Y);
            });

        /// <summary>
        /// <see cref="DecodeSegmentField"/> without the placement, for callers that only need the samples.
        /// </summary>
        public (byte[]? maskData, int width, int height) DecodeSegmentMask(SegmentationServiceTypes.SegmentResult segment)
        {
            SegmentField field = DecodeSegmentField(segment);
            return (field.Data, field.Width, field.Height);
        }

        /// <summary>
        /// Decodes a mask PNG into 0–255 bytes. A thresholded mask arrives as 0 and 255.
        /// Returns null data on an invalid PNG.
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
        /// Tile level the live camera would submit now, read on the UI dispatcher. Compare it with
        /// <see cref="MosaicDownsample"/> to tell whether a finished response was computed at a level
        /// that the camera has since left. Only needed when <see cref="SegmentationModelProfile.TileLevelCanChange"/>.
        /// </summary>
        public Task<int> GetLiveTileDownsampleAsync()
        {
            int Resolve() => ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                return Task.FromResult(Resolve());

            return dispatcher.InvokeAsync(Resolve).Task;
        }

        /// <summary>
        /// World rectangle of a SAM2 segment on the fused mosaic. <paramref name="segmentX"/>, <paramref name="segmentY"/>,
        /// <paramref name="maskWidth"/> and <paramref name="maskHeight"/> are image pixels, so a reduced field passes its
        /// size times its scale (<see cref="SegmentField.PixelWidth"/>).
        /// Mask Y is top-origin; MosaicPixelToWorld flips into Viking world-up.
        /// The mosaic origin comes from <paramref name="response"/>, not session state: the session is shared by
        /// concurrent requests and growth rounds, so its origin may already belong to a different response.
        /// </summary>
        public Geometry.Rectangle GetSegmentWorldBounds(
            SegmentationResponse response,
            int segmentX,
            int segmentY,
            int maskWidth,
            int maskHeight)
        {
            int imageHeight = Math.Max(1, response.Height > 0 ? response.Height * response.PixelScale() : uploadedImageHeight);
            int downsample = Math.Max(1, DownsampleFor(response));
            Geometry.Vector2 topLeft = SegmentationTileGrid.MosaicPixelToWorld(
                response.OriginX,
                response.OriginY,
                segmentX,
                segmentY,
                imageHeight,
                downsample);
            Geometry.Vector2 bottomRight = SegmentationTileGrid.MosaicPixelToWorld(
                response.OriginX,
                response.OriginY,
                segmentX + maskWidth,
                segmentY + maskHeight,
                imageHeight,
                downsample);
            return new Geometry.Rectangle(topLeft, bottomRight);
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

    }
}
