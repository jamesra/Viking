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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.DependencyInjection;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.UI;
using Viking.UI.Controls;
using Viking.VolumeModel;
using VikingXNA;
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
        private CancellationTokenSource renderCancellationTokenSource;
        private CancellationTokenSource linkedRenderCancellationTokenSource;
        private CancellationTokenSource uploadCancellationTokenSource;
        private readonly HashSet<string> uploadedTileKeys = [];
        private int mosaicDownsample = 1;
        private int mosaicOriginX;
        private int mosaicOriginY;
        private readonly int maxTileRounds;

        /// <summary>
        /// Ceiling for submitted tile downsample from appSettings
        /// <c>SegmentationTileDownsample</c> (default 1). Camera zoom may submit a finer
        /// level down to 1; never coarser than this. A coarser camera still uploads at this
        /// level until <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>,
        /// which cannot exceed 2. A camera coarser than DS 2 does not upload.
        /// </summary>
        public static int MaxTileDownsample { get; } =
            int.TryParse(ConfigurationManager.AppSettings["SegmentationTileDownsample"], out int ds) && ds >= 1
                ? ds
                : 1;

        private static SegmentationModelProfile modelProfile = SegmentationModelProfile.Default;

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
        // TODO: call this from the status check; nothing fetches GetServerStatus yet.
        public static void AdoptModelCapabilities(Viking.gRPC.SegmentationServiceTypes.V1.ModelCapabilities advertised)
            => Volatile.Write(ref modelProfile, SegmentationModelProfile.FromAdvertised(advertised, MaxTileDownsample));

        /// <summary>
        /// Highest level the current model may be sent: <see cref="MaxTileDownsample"/> when its profile
        /// lets the level change, otherwise 1.
        /// </summary>
        private static int EffectiveTileCeiling => ModelProfile.TileLevelCanChange ? MaxTileDownsample : 1;

        /// <summary>
        /// Legacy name for <see cref="MaxTileDownsample"/>. Prefer the max name.
        /// </summary>
        public static int PinnedTileDownsample => MaxTileDownsample;

        /// <summary>
        /// False when the live camera is coarser than
        /// <see cref="WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>.
        /// Equality still submits. The preference is capped at DS 2, so a camera at DS 3
        /// does not upload. Called by <see cref="UploadCurrentImageAsync"/> and
        /// <see cref="SegmentAsync"/> before any tile is sent. Auto-segment checks the same
        /// cutoff earlier so an idle batch never starts.
        /// </summary>
        private bool IsCameraWithinTileSubmission()
        {
            double cameraDownsample = parent.Camera?.Downsample ?? parent.Downsample;
            double maxCameraDownsample = WebAnnotation.Global.AnnotationSettings.AutoPolygonizeMaxDownsample;
            if (cameraDownsample <= maxCameraDownsample)
                return true;

            SegmentationDiag.Log(
                $"Tile submit skip: downsample out of range ds={cameraDownsample} max={maxCameraDownsample}");
            return false;
        }

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
        /// Binds the session to a viewer. ViewportBounds starts as the live camera rectangle.
        /// </summary>
        public SegmentationViewportSession(SectionViewerControl parent)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            ViewportBounds = GetCurrentViewportBounds();
            maxTileRounds = int.TryParse(ConfigurationManager.AppSettings["SegmentationMaxTileRounds"], out var rounds) && rounds >= 0
                ? rounds
                : 4;
            mosaicDownsample = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
        }

        public Geometry.Rectangle ViewportBounds { get; set; }

        public ulong? CurrentImageId => currentImageId;

        public Geometry.Rectangle? UploadedImageBounds => uploadedImageBounds;

        public int UploadedImageWidth => uploadedImageWidth;

        public int UploadedImageHeight => uploadedImageHeight;

        /// <summary>Pyramid level used for the last tile upload/segment (resolved from camera, capped).</summary>
        public int MosaicDownsample => mosaicDownsample > 0 ? mosaicDownsample : EffectiveTileCeiling;

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
                var channel = ServiceLocator.GrpcChannelManager?.GetOrCreateChannel();
                if (channel is null)
                {
                    SegmentationDiag.Log("TryInitializeClient: GrpcChannelManager/channel is null");
                    return false;
                }

                grpcClient = new SegmentationServiceTypes.SegmentationService.SegmentationServiceClient(channel);
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
        /// True when corners differ by less than 1% of the larger side. Used to treat hitch jitter as the same view.
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
        /// After PNG encode finishes, upload only when the live viewport still matches the captured bounds.
        /// Used by UploadCurrentImageAsync and covered by auto-polygonize tests.
        /// </summary>
        public static bool ShouldUploadEncodedCapture(Geometry.Rectangle capturedBounds, Geometry.Rectangle currentBounds)
            => AreViewportBoundsSimilar(capturedBounds, currentBounds);

        /// <summary>
        /// Maps world to capture-pixel space without a Y flip. SAM2 mask Y is flipped in <see cref="GetSegmentWorldBounds"/>.
        /// </summary>
        public Geometry.Vector2 WorldToViewport(Geometry.Vector2 worldPos, int viewportWidth, int viewportHeight)
        {
            Geometry.Vector2 boundsMin = ViewportBounds.LowerLeft;
            Geometry.Vector2 boundsMax = ViewportBounds.UpperRight;

            double normalizedX = (worldPos.X - boundsMin.X) / (boundsMax.X - boundsMin.X);
            double normalizedY = (worldPos.Y - boundsMin.Y) / (boundsMax.Y - boundsMin.Y);

            return new Geometry.Vector2(
                normalizedX * viewportWidth,
                normalizedY * viewportHeight);
        }

        /// <summary>
        /// Inverse of <see cref="WorldToViewport"/>; pixel Y is not flipped here.
        /// </summary>
        public Geometry.Vector2 ViewportToWorld(int pixelX, int pixelY, int viewportWidth, int viewportHeight)
        {
            double normalizedX = (double)pixelX / viewportWidth;
            double normalizedY = (double)pixelY / viewportHeight;
            Geometry.Vector2 boundsMin = ViewportBounds.LowerLeft;
            Geometry.Vector2 boundsMax = ViewportBounds.UpperRight;
            return new Geometry.Vector2(
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
            uploadedTileKeys.Clear();
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
                return false;

            if (!IsCameraWithinTileSubmission())
                return false;

            if (Interlocked.CompareExchange(ref isUploadingImage, 1, 0) != 0)
                return false;

            try
            {
                uploadCancellationTokenSource?.Cancel();
                uploadCancellationTokenSource?.Dispose();
                uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                (int downsample, _, _, _) = await ReadViewTilesAsync().ConfigureAwait(false);
                if (uploadCancellationTokenSource.Token.IsCancellationRequested)
                    return false;

                mosaicDownsample = downsample;
                uploadedImageBounds = ViewportBounds;
                uploadedImageWidth = SegmentationTileGrid.TileSize;
                uploadedImageHeight = SegmentationTileGrid.TileSize;
                currentImageId = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Tile upload cancelled due to view change");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tile upload error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref isUploadingImage, 0);
            }

            return false;
        }

        /// <summary>
        /// Foreground cells plus growth neighbors, without duplicates.
        /// Background points are not used to choose cells: they can cover the whole view,
        /// and the server applies a background click only on a tile it actually predicts.
        /// </summary>
        private static List<TileCell> TilesForRound(
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            int downsample,
            IReadOnlyList<TileCell> extras)
        {
            List<TileCell> cells = SegmentationTileGrid.CellsContainingPoints(foregroundPoints, downsample);
            if (extras is null || extras.Count == 0)
                return cells;

            HashSet<(int Row, int Col)> seen = new(cells.Select(cell => (cell.Row, cell.Col)));
            foreach (TileCell extra in extras)
            {
                if (seen.Add((extra.Row, extra.Col)))
                    cells.Add(extra);
            }

            return cells;
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
            CancellationToken cancellationToken)
        {
            const int maxNotFoundRetries = 16;
            string? lastMissingKey = null;
            int sameTileMisses = 0;
            for (int attempt = 0; attempt <= maxNotFoundRetries; attempt++)
            {
                try
                {
                    return await SegmentUploadedTilesAsync(
                        signature,
                        needed,
                        foregroundPoints,
                        backgroundPoints,
                        cancellationToken).ConfigureAwait(false);
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
        /// Uploads cells that contain foreground points (and server-requested neighbors),
        /// then SegmentTiles with growth rounds. Does not name every viewport cell.
        /// Returns the last response, including a partial mask when growth still needs cells.
        /// </summary>
        public async Task<SegmentationResponse?> SegmentAsync(
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            CancellationToken cancellationToken)
        {
            if (grpcClient is null)
                return null;

            if ((foregroundPoints is null || foregroundPoints.Count == 0) &&
                (backgroundPoints is null || backgroundPoints.Count == 0))
                return null;

            if (!IsCameraWithinTileSubmission())
                return null;

            try
            {
                List<TileCell> extras = [];
                SegmentationResponse? lastResponse = null;
                for (int round = 0; round <= maxTileRounds; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (int downsample, TileSignature signature, List<TileCell> visible, bool grayscale) =
                        await ReadViewTilesAsync().ConfigureAwait(false);
                    mosaicDownsample = downsample;
                    List<TileCell> needed = TilesForRound(foregroundPoints, downsample, extras);
                    SegmentationDiag.Log(
                        $"SegmentAsync round={round} ds={downsample} visible={visible.Count} " +
                        $"promptTiles={needed.Count} extras={extras.Count} " +
                        $"vol={signature.Volume} sec={signature.Section}");
                    if (needed.Count == 0)
                    {
                        SegmentationDiag.Log("SegmentAsync: no tile cells for prompts");
                        return lastResponse;
                    }

                    if (!await UploadMissingTilesAsync(signature, needed, grayscale, cancellationToken).ConfigureAwait(false))
                    {
                        SegmentationDiag.Log("SegmentAsync: UploadMissingTilesAsync returned false");
                        Debug.WriteLine("No segmentation tiles could be uploaded");
                        return lastResponse;
                    }

                    SegmentationResponse? response = await SegmentWithMissingTileRetriesAsync(
                        signature,
                        needed,
                        foregroundPoints,
                        backgroundPoints,
                        grayscale,
                        cancellationToken).ConfigureAwait(false);
                    if (response is null)
                        return lastResponse;

                    lastResponse = response;
                    mosaicOriginX = response.OriginX;
                    mosaicOriginY = response.OriginY;
                    uploadedImageBounds = MosaicWorldBounds(response);
                    uploadedImageWidth = Math.Max(1, response.Width);
                    uploadedImageHeight = Math.Max(1, response.Height);

                    if (response.RequestedTiles.Count == 0 || round == maxTileRounds)
                        break;

                    extras.Clear();
                    foreach (TileCoord tile in response.RequestedTiles)
                    {
                        if (tile.Downsample != downsample)
                            continue;

                        // The server asks only for cells it does not have. A key left from an
                        // earlier upload would skip the re-upload and growth would stall.
                        uploadedTileKeys.Remove(TileKey(signature, tile.Row, tile.Col));
                        extras.Add(new TileCell(tile.Row, tile.Col));
                    }

                    if (extras.Count == 0)
                        break;
                }

                return lastResponse;
            }
            catch (OperationCanceledException)
            {
                SegmentationDiag.Log("SegmentAsync cancelled");
                return null;
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"SegmentAsync error: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"Segmentation error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decodes each segment mask and polygonizes in score order. Near-full-frame masks are skipped.
        /// Cleanup and marching squares run at mask resolution. Callers apply pen-threshold simplification.
        /// <paramref name="cancellationToken"/> is checked between segments so a newer click can abort.
        /// </summary>
        public IReadOnlyList<Polygon> CreatePolygonsFromResponse(
            SegmentationResponse response,
            double? holeDropFraction = null,
            IReadOnlyList<Geometry.Vector2> preserveHolesContainingWorldPoints = null,
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
            List<Polygon> polygons = [];
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

                int captureWidth = Math.Max(1, response.Width > 0 ? response.Width : uploadedImageWidth);
                int captureHeight = Math.Max(1, response.Height > 0 ? response.Height : uploadedImageHeight);
                Geometry.Rectangle mosaicBounds = MosaicWorldBounds(response);

                Stopwatch polygonizeTimer = Stopwatch.StartNew();
                polygons.AddRange(SegmentationMaskPolygonizer.CreatePolygons(
                    decodedMaskData,
                    decodedWidth,
                    decodedHeight,
                    segment.X,
                    segment.Y,
                    captureWidth,
                    captureHeight,
                    mosaicBounds,
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
        /// Decodes a SAM2 probability PNG into 0–255 bytes. Older 1-bit masks arrive as 0 and 255.
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

        private SegmentationRequest? BuildSegmentationRequest(
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints)
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
        /// World rectangle of a SAM2 segment on the fused mosaic.
        /// Mask Y is top-origin; MosaicPixelToWorld flips into Viking world-up.
        /// </summary>
        public Geometry.Rectangle GetSegmentWorldBounds(
            int segmentX,
            int segmentY,
            int maskWidth,
            int maskHeight,
            int responseWidth,
            int responseHeight)
        {
            int imageHeight = Math.Max(1, responseHeight > 0 ? responseHeight : uploadedImageHeight);
            int downsample = Math.Max(1, mosaicDownsample);
            Geometry.Vector2 topLeft = SegmentationTileGrid.MosaicPixelToWorld(
                mosaicOriginX,
                mosaicOriginY,
                segmentX,
                segmentY,
                imageHeight,
                downsample);
            Geometry.Vector2 bottomRight = SegmentationTileGrid.MosaicPixelToWorld(
                mosaicOriginX,
                mosaicOriginY,
                segmentX + maskWidth,
                segmentY + maskHeight,
                imageHeight,
                downsample);
            return new Geometry.Rectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// Waits for visible tiles, GPU-captures on the UI thread, then encodes on a worker.
        /// Returns null data when tiles never become ready or encode is cancelled.
        /// </summary>
        private async Task<(byte[]? data, int width, int height, Geometry.Rectangle capturedBounds)> CaptureViewportImage(CancellationToken cancellationToken)
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
        private async Task<(Color[]? pixels, int width, int height, bool isGrayscale, Geometry.Rectangle capturedBounds, long renderMs, long readbackMs)> ReadViewportPixelsAsync(CancellationToken cancellationToken)
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

        private async Task<(Color[]? pixels, int width, int height, bool isGrayscale, Geometry.Rectangle capturedBounds, long renderMs, long readbackMs)> ReadViewportPixelsCoreAsync(CancellationToken cancellationToken)
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
                Geometry.Rectangle capturedBounds = GetCurrentViewportBounds();
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
