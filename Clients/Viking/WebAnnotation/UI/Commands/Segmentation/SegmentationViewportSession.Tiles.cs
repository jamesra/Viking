using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.gRPC.SegmentationServiceTypes.V1;
using VikingXNA;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// 1024x1024 mosaic tile upload and SegmentTiles helpers.
    /// Ported from VikingLegacy SegmentationCommand so the client trees stay aligned.
    /// </summary>
    internal sealed partial class SegmentationViewportSession
    {
        internal readonly struct TileSignature
        {
            public TileSignature(string volume, int section, string channel, string transform, int downsample)
            {
                Volume = volume;
                Section = section;
                Channel = channel;
                Transform = transform;
                Downsample = downsample;
            }

            public string Volume { get; }
            public int Section { get; }
            public string Channel { get; }
            public string Transform { get; }
            public int Downsample { get; }
        }

        /// <summary>
        /// Reads the live view: the tile pyramid level, tile signature and the cells covering the viewport.
        /// </summary>
        private async Task<(int downsample, TileSignature signature, List<TileCell> visible, bool grayscale)> ReadViewTilesAsync()
        {
            return await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
            {
                int downsample = ResolveTileDownsample(parent.CameraDownsample);
                TileSignature signature = CurrentTileSignature(downsample);
                Geometry.Rectangle bounds = GetCurrentViewportBounds();
                ViewportBounds = bounds;
                List<TileCell> visible = SegmentationTileGrid.CellsCovering(
                    bounds.LowerLeft.X,
                    bounds.LowerLeft.Y,
                    bounds.UpperRight.X,
                    bounds.UpperRight.Y,
                    downsample);
                bool grayscale = parent.IsGrayscale;
                return (downsample, signature, visible, grayscale);
            }).Task.ConfigureAwait(false);
        }

        private TileSignature CurrentTileSignature(int downsample)
        {
            string volume = parent.VolumeName ?? string.Empty;
            int section = parent.SectionNumber;
            string channel = parent.Channel ?? string.Empty;
            string volumeTransform = parent.VolumeTransform ?? string.Empty;
            string sectionTransform = parent.SectionTransform ?? string.Empty;
            return new TileSignature(volume, section, channel, volumeTransform + "|" + sectionTransform, downsample);
        }

        private static string TileKey(TileSignature signature, int row, int col) =>
            $"{signature.Volume}\n{signature.Section}\n{signature.Channel}\n{signature.Transform}\n{signature.Downsample}\n{row}\n{col}";

        private TileCoord ToCoord(TileSignature signature, TileCell cell) => new()
        {
            Volume = signature.Volume,
            Section = signature.Section,
            Channel = signature.Channel,
            Transform = signature.Transform,
            Downsample = signature.Downsample,
            Row = cell.Row,
            Col = cell.Col
        };

        /// <summary>
        /// Renders and uploads cells that this session has not yet had accepted.
        /// Returns false when every needed cell failed to capture.
        /// </summary>
        private async Task<bool> UploadMissingTilesAsync(
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            bool grayscale,
            CancellationToken token)
        {
            if (grpcClient is null)
                return false;

            bool anyReady = false;
            HashSet<(int Row, int Col)> seen = [];
            foreach (TileCell cell in cells)
            {
                token.ThrowIfCancellationRequested();
                if (!seen.Add((cell.Row, cell.Col)))
                    continue;

                string key = TileKey(signature, cell.Row, cell.Col);
                if (uploadedTileKeys.Contains(key))
                {
                    anyReady = true;
                    continue;
                }

                var (png, width, height) = await CaptureTileImage(
                    cell,
                    signature.Downsample,
                    grayscale,
                    token,
                    gray => tileImages.Store(key, gray)).ConfigureAwait(false);
                if (png is null || png.Length == 0)
                {
                    SegmentationDiag.Log($"Capture failed row={cell.Row} col={cell.Col} ds={signature.Downsample}");
                    Debug.WriteLine($"Failed to capture tile row={cell.Row} col={cell.Col}");
                    continue;
                }

                UploadTileRequest upload = new()
                {
                    Coord = ToCoord(signature, cell),
                    ImageData = Google.Protobuf.ByteString.CopyFrom(png),
                    Width = width,
                    Height = height
                };
                CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: token);
                SegmentationDiag.Log(
                    $"UploadTile send row={cell.Row} col={cell.Col} ds={signature.Downsample} " +
                    $"declared={upload.Width}x{upload.Height} encodedBytes={png.Length}");
                Debug.WriteLine(
                    $"UploadTile key=vol={signature.Volume}|sec={signature.Section}|ch={signature.Channel}|" +
                    $"xf={signature.Transform}|ds={signature.Downsample}|row={cell.Row}|col={cell.Col} " +
                    $"dimensions={upload.Width}x{upload.Height} bytes={png.Length}");
                try
                {
                    UploadTileResponse response = await grpcClient.UploadTileAsync(upload, callOptions).ResponseAsync.ConfigureAwait(false);
                    uploadedTileKeys.Add(key);
                    anyReady = true;
                    SegmentationDiag.Log($"UploadTile ok alreadyCached={response.AlreadyCached}");
                    Debug.WriteLine(
                        $"UploadTile accepted key=ds={signature.Downsample}|row={cell.Row}|col={cell.Col} " +
                        $"alreadyCached={response.AlreadyCached}");
                }
                catch (RpcException rpcEx)
                {
                    SegmentationDiag.Log($"UploadTile RPC {rpcEx.StatusCode}: {rpcEx.Status.Detail}");
                    throw;
                }
            }

            return anyReady;
        }

        /// <summary>
        /// Upper bound for one whole SegmentTilesStream conversation, including the tile uploads the server asks for.
        /// </summary>
        private const int SegmentStreamDeadlineSeconds = 300;

        /// <summary>
        /// One SegmentTilesStream call naming <paramref name="cells"/>, which need not be uploaded: the
        /// server takes the ones it holds from its own caches. The server owns the growth: it sends
        /// <c>needed</c> for the cells it lacks (a prompt cell included), this method renders and uploads
        /// them and answers, and the server continues the same walk. The finished mask arrives as <c>result</c>.
        /// </summary>
        private async Task<SegmentationResponse> SegmentTilesStreamAsync(
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            bool grayscale,
            CancellationToken token,
            IReadOnlyList<SegmentationSeed>? foregroundBoxes = null,
            SegmentationMaskUnion? backgroundMask = null,
            ulong requestId = 0)
        {
            if (grpcClient is null)
                throw new InvalidOperationException("Segmentation client is not connected.");

            SegmentTilesRequest request = new()
            {
                RequestId = requestId,
                MultimaskOutput = false,
                OmitLabeledImage = true,
                MaskThreshold = (float)WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold,
                UseMaskInput = WebAnnotation.Global.AnnotationSettings.SegmentationUseMaskInput,
                ResponseContent = (uint)RequestedContent
            };
            foreach (TileCell cell in cells)
                request.Tiles.Add(ToCoord(signature, cell));

            if (request.Tiles.Count == 0)
                throw new InvalidOperationException("No tile cells cover the segmentation prompts.");

            // The model id comes from the status query a connection starts. The first request after the app starts, or the first after
            // a model change reset what was known, may get here before it has answered, so (re)start it if nothing is known and wait.
            // Unset when the server reported none: it accepts that unless the model's manifest requires an id.
            BeginModelCapabilitiesRefresh(grpcClient);
            await WhenCapabilitiesKnownAsync().ConfigureAwait(false);
            if (ServerModelId is { } modelId)
                request.ModelId = modelId;

            // The clicks are the points of the two Features; whatever is object beyond them is a FOREGROUND mask tile, and the
            // neighbouring annotations are BACKGROUND mask tiles the server asks for as its walk grows.
            (SegmentationServiceTypes.Feature foregroundFeature, SegmentationServiceTypes.Feature? backgroundFeature) =
                ClickFeatures(foregroundPoints, backgroundPoints, signature.Downsample);

            // The seeds (a circle's disc, a polygon, an edited mask) are the foreground mask: "everything inside is object, grow
            // beyond it". They go out as the grid tiles they touch, however large they are.
            int maskTileScale = SegmentationMaskTiles.ConfiguredScale;
            double pointRadius = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
            SegmentationMaskUnion? seedUnion = null;
            if (foregroundBoxes is { Count: > 0 })
            {
                seedUnion = new SegmentationMaskUnion();
                foreach (SegmentationSeed seed in foregroundBoxes)
                    seedUnion.Add(seed);
            }

            // PrepareStart sets the count of FOREGROUND tiles, 0 for a click-only request.
            SegmentationMaskTiles.StartTiles startTiles = await Task.Run(
                () => SegmentationMaskTiles.PrepareStart(
                    request, seedUnion, ToCoord(signature, new TileCell(0, 0)), backgroundPoints, pointRadius, backgroundMask, maskTileScale,
                    SegmentationMaskTiles.ScaleFor(seedUnion, signature.Downsample)),
                token).ConfigureAwait(false);
            request.ForegroundFeature = foregroundFeature;
            if (backgroundFeature is not null)
                request.BackgroundFeature = backgroundFeature;

            CallOptions callOptions = new(
                deadline: DateTime.UtcNow.AddSeconds(SegmentStreamDeadlineSeconds),
                cancellationToken: token);
            SegmentationResponse? response = null;
            List<string> requestedTileList = [];
            using (AsyncDuplexStreamingCall<SegmentTilesStreamRequest, SegmentTilesStreamResponse> call =
                grpcClient.SegmentTilesStream(callOptions))
            {
                await call.RequestStream.WriteAsync(new SegmentTilesStreamRequest { Start = request }).ConfigureAwait(false);
                // The seed's tiles first (the server starts growing only when all the announced ones have arrived), then the
                // background of the start windows' tiles, which the server needs before it predicts them: asking would only cost
                // a round trip.
                HashSet<TileCell> backgroundSent = [];
                foreach (SegmentationServiceTypes.MaskTile tile in startTiles.Background)
                    backgroundSent.Add(new TileCell(tile.Coord.Row, tile.Coord.Col));

                foreach (SegmentTilesStreamRequest message in startTiles.Messages())
                    await call.RequestStream.WriteAsync(message).ConfigureAwait(false);

                SegmentationDiag.Log($"SegmentTilesStream mask tiles foreground={startTiles.Foreground.Count} startBackground={startTiles.Background.Count} scale={maskTileScale}");

                while (response is null && await call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                {
                    SegmentTilesStreamResponse update = call.ResponseStream.Current;
                    if (update.UpdateCase == SegmentTilesStreamResponse.UpdateOneofCase.Result)
                    {
                        response = update.Result;
                    }
                    else if (update.UpdateCase == SegmentTilesStreamResponse.UpdateOneofCase.Needed)
                    {
                        requestedTileList.AddRange(update.Needed.Tiles.Select(tile => $"{tile.Row},{tile.Col}"));
                        TilesAnswer answer = await UploadNeededTilesAsync(signature, update.Needed, grayscale, token).ConfigureAwait(false);
                        if (update.Needed.Masks.Count > 0)
                            await AnswerNeededMasksAsync(call.RequestStream, update.Needed, answer, signature, backgroundMask, backgroundSent, maskTileScale, token).ConfigureAwait(false);

                        await call.RequestStream.WriteAsync(new SegmentTilesStreamRequest { Answer = answer }).ConfigureAwait(false);
                    }
                }
            }

            if (response is null)
                throw new InvalidOperationException("The segmentation stream ended without a result.");

            string submittedTiles = string.Join(
                ";",
                request.Tiles.Select(tile => $"{tile.Row},{tile.Col}"));
            string requestedTiles = string.Join(";", requestedTileList);
            string segmentSizes = string.Join(
                ",",
                response.Segments.Select(segment => SegmentationLogitField.HasLogits(segment)
                    ? $"{segment.LogitsX},{segment.LogitsY}:logits {segment.LogitsWidth}x{segment.LogitsHeight}@{segment.LogitsScale}"
                    : $"{segment.X},{segment.Y}:{CapturedPng.DescribeDimensions(segment.Mask.ToByteArray())}"));
            SegmentationDiag.Log(
                $"SegmentTiles response req={requestId}{(response.RequestId == requestId ? string.Empty : $" MISMATCH echoed={response.RequestId}")} mosaic={response.Width}x{response.Height} " +
                $"origin=({response.OriginX},{response.OriginY}) submittedTiles=[{submittedTiles}] " +
                $"requestedTiles=[{requestedTiles}] " +
                $"segments=[{segmentSizes}]");
            return response;
        }

        /// <summary>
        /// The clicks as the points of the request's two Features, in mosaic pixels at <paramref name="downsample"/> (X right, Y up). The
        /// foreground Feature always exists and carries the role of the FOREGROUND mask tiles; the background one is null without clicks.
        /// </summary>
        internal static (SegmentationServiceTypes.Feature Foreground, SegmentationServiceTypes.Feature? Background) ClickFeatures(
            IReadOnlyList<Geometry.Vector2>? foregroundPoints, IReadOnlyList<Geometry.Vector2>? backgroundPoints, int downsample)
        {
            SegmentationServiceTypes.Feature foreground = new() { MaskRole = SegmentationServiceTypes.MaskRole.Seed };
            foreach (Geometry.Vector2 point in foregroundPoints ?? [])
            {
                (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(point.X, point.Y, downsample);
                foreground.Points.Add(new SegmentationServiceTypes.Point { X = x, Y = y });
            }

            SegmentationServiceTypes.Feature? background = null;
            foreach (Geometry.Vector2 point in backgroundPoints ?? [])
            {
                (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(point.X, point.Y, downsample);
                background ??= new SegmentationServiceTypes.Feature();
                background.Points.Add(new SegmentationServiceTypes.Point { X = x, Y = y });
            }

            return (foreground, background);
        }

        /// <summary>Sends <paramref name="tiles"/> as <c>MaskTiles</c> messages. Nothing is sent for an empty list.</summary>
        private static async Task WriteMaskTilesAsync(
            IClientStreamWriter<SegmentTilesStreamRequest> stream,
            IReadOnlyList<SegmentationServiceTypes.MaskTile> tiles)
        {
            foreach (SegmentationServiceTypes.MaskTiles message in SegmentationMaskTiles.Chunk(tiles))
                await stream.WriteAsync(new SegmentTilesStreamRequest { MaskTiles = message }).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the BACKGROUND tiles the server asked for (<paramref name="needed"/>.Masks) before its answer, empty tiles
        /// included, rasterizing only the neighbour shapes that reach each tile. A tile at another downsample is listed in
        /// <c>masks_unavailable</c>; one already sent (the start windows') is not sent twice, which the server refuses.
        /// </summary>
        internal static async Task AnswerNeededMasksAsync(
            IClientStreamWriter<SegmentTilesStreamRequest> stream,
            TilesNeeded needed,
            TilesAnswer answer,
            TileSignature signature,
            SegmentationMaskUnion? backgroundMask,
            HashSet<TileCell> backgroundSent,
            int scale,
            CancellationToken token)
        {
            List<TileCoord> wanted = [];
            foreach (TileCoord coord in needed.Masks)
            {
                if (coord.Downsample != signature.Downsample)
                    answer.MasksUnavailable.Add(coord);
                else if (backgroundSent.Add(new TileCell(coord.Row, coord.Col)))
                    wanted.Add(coord);
            }

            List<SegmentationServiceTypes.MaskTile> tiles = await Task.Run(
                () => SegmentationMaskTiles.BuildBackground(backgroundMask, wanted, scale),
                token).ConfigureAwait(false);
            await WriteMaskTilesAsync(stream, tiles).ConfigureAwait(false);
            SegmentationDiag.Log(
                $"SegmentTilesStream background tiles sent={tiles.Count} unavailable={answer.MasksUnavailable.Count} " +
                $"nonEmpty={tiles.Count(tile => tile.Full || !tile.Png.IsEmpty)}");
        }

        /// <summary>
        /// Uploads the cells the server asked for and reports each as ready or unavailable.
        /// A cell that cannot be captured is unavailable, which tells the server not to grow into it.
        /// An <see cref="RpcException"/> from UploadTile is not caught: it ends the whole request.
        /// </summary>
        private async Task<TilesAnswer> UploadNeededTilesAsync(
            TileSignature signature,
            TilesNeeded needed,
            bool grayscale,
            CancellationToken token)
        {
            TilesAnswer answer = new();
            foreach (TileCoord tile in needed.Tiles)
            {
                if (tile.Downsample != signature.Downsample)
                {
                    answer.Unavailable.Add(tile);
                    continue;
                }

                // The server asks only for cells it does not hold. A key left from an earlier
                // upload would skip the re-upload and the server would wait on a tile it never gets.
                uploadedTileKeys.Remove(TileKey(signature, tile.Row, tile.Col));
                bool uploaded = await UploadMissingTilesAsync(
                    signature,
                    [new TileCell(tile.Row, tile.Col)],
                    grayscale,
                    token).ConfigureAwait(false);
                (uploaded ? answer.Ready : answer.Unavailable).Add(tile);
            }

            SegmentationDiag.Log(
                $"SegmentTilesStream answer ready=[{string.Join(";", answer.Ready.Select(t => $"{t.Row},{t.Col}"))}] " +
                $"unavailable=[{string.Join(";", answer.Unavailable.Select(t => $"{t.Row},{t.Col}"))}]");
            return answer;
        }

        /// <summary>
        /// Renders one cell and encodes it for SAM2. <paramref name="onGray"/>, when given, receives the blurred grayscale
        /// copy kept for the edge snap (<see cref="SegmentationTileImages.PrepareGray"/>) once the capture is valid.
        /// </summary>
        private async Task<(byte[]? data, int width, int height)> CaptureTileImage(
            TileCell cell,
            int downsample,
            bool grayscale,
            CancellationToken cancellationToken,
            Action<byte[]>? onGray = null)
        {
            try
            {
                Color[]? pixels = await RenderTilePixelsAsync(cell, downsample, cancellationToken).ConfigureAwait(false);
                if (pixels is null)
                    return (null, 0, 0);

                int width = SegmentationTileGrid.TileSize;
                int height = SegmentationTileGrid.TileSize;
                // Awaiting the dispatcher task can resume this method on the UI thread; encoding a 1024x1024
                // tile there stalls the viewer, so it is pushed to the pool explicitly.
                Task<byte[]> grayTask = onGray is null
                    ? Task.FromResult<byte[]>([])
                    : Task.Run(() => GrayFor(pixels, grayscale), cancellationToken);
                byte[] pngData = await Task.Run(
                    () => SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, grayscale),
                    cancellationToken).ConfigureAwait(false);
                byte[] gray = await grayTask.ConfigureAwait(false);
                SegmentationCaptureEncoder.SaveCaptureForReview(
                    pngData,
                    width,
                    height,
                    $"s{parent.SectionNumber}_ds{downsample}_r{cell.Row}_c{cell.Col}");
                var (isValid, errorMessage) = CapturedPng.Validate(pngData, width, height);
                SegmentationDiag.Log(
                    $"CaptureTile encoded row={cell.Row} col={cell.Col} " +
                    $"png={CapturedPng.DescribeDimensions(pngData)} bytes={pngData.Length} valid={isValid}");
                if (!isValid)
                {
                    Debug.WriteLine($"Tile capture failed validation: {errorMessage}");
                    return (null, 0, 0);
                }

                if (onGray is not null && gray.Length == width * height)
                    onGray(gray);

                return (pngData, width, height);
            }
            catch (OperationCanceledException)
            {
                return (null, 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing tile row={cell.Row} col={cell.Col}: {ex.Message}");
                return (null, 0, 0);
            }
        }

        /// <summary>
        /// Captures, for the edge snap, the cells under <paramref name="response"/> this session holds no image of. The
        /// server takes cells from its own caches without asking, so a mask can cover cells this client never rendered.
        /// Only the grayscale copy is made: nothing is encoded or uploaded. Does nothing while the edge snap is off, and
        /// stops at <see cref="SegmentationTileImages.Capacity"/> cells, as many as the store keeps.
        /// </summary>
        private async Task CaptureMissingTileImagesAsync(
            SegmentationResponse response,
            TileSignature signature,
            int downsample,
            bool grayscale,
            CancellationToken token)
        {
            if (!WebAnnotation.Global.AnnotationSettings.SegmentationEdgeSnapEnabled || response.Segments.Count == 0)
                return;

            Geometry.Rectangle bounds = MosaicWorldBounds(response, downsample);
            List<TileCell> cells = SegmentationTileGrid.CellsCovering(
                bounds.LowerLeft.X,
                bounds.LowerLeft.Y,
                bounds.UpperRight.X,
                bounds.UpperRight.Y,
                downsample);
            int captured = 0;
            foreach (TileCell cell in cells)
            {
                string key = TileKey(signature, cell.Row, cell.Col);
                if (tileImages.Get(key) is not null)
                    continue;

                if (captured >= SegmentationTileImages.Capacity)
                    break;

                token.ThrowIfCancellationRequested();
                captured++;
                try
                {
                    Color[]? pixels = await RenderTilePixelsAsync(cell, downsample, token).ConfigureAwait(false);
                    if (pixels is not null)
                        tileImages.Store(key, await Task.Run(() => GrayFor(pixels, grayscale), token).ConfigureAwait(false));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The mask is already here; a cell the edge snap cannot see is only left unsnapped.
                    Debug.WriteLine($"Error capturing tile image row={cell.Row} col={cell.Col}: {ex.Message}");
                }
            }

            if (captured > 0)
                SegmentationDiag.Log($"EdgeSnap captured {captured} tile image(s) the server had taken from its caches");
        }

        /// <summary>The blurred grayscale copy of a rendered cell that the edge snap samples.</summary>
        private static byte[] GrayFor(Color[] pixels, bool grayscale) =>
            SegmentationTileImages.PrepareGray(
                SegmentationCaptureEncoder.StretchedLuma(pixels, grayscale),
                SegmentationTileGrid.TileSize,
                SegmentationTileGrid.TileSize);

        /// <summary>
        /// Renders one cell of the current view and reads back its pixels; null when the render was cancelled or did
        /// not come back at the tile size.
        /// </summary>
        private async Task<Color[]?> RenderTilePixelsAsync(TileCell cell, int downsample, CancellationToken cancellationToken)
        {
            CancellationToken renderToken = PrepareCancellationToken(cancellationToken);
            double cellWorld = SegmentationTileGrid.TileSize * (double)downsample;
            float centerX = (float)((cell.Col + 0.5) * cellWorld);
            float centerY = (float)((cell.Row + 0.5) * cellWorld);
            Camera camera = new() { Downsample = downsample, LookAt = new Microsoft.Xna.Framework.Vector2(centerX, centerY) };
            VikingXNA.Scene tileScene = new(
                new Viewport(0, 0, SegmentationTileGrid.TileSize, SegmentationTileGrid.TileSize),
                camera);
            Geometry.Rectangle worldBounds = tileScene.VisibleWorldBounds;
            SegmentationDiag.Log(
                $"CaptureTile start row={cell.Row} col={cell.Col} center=({centerX},{centerY}) ds={downsample} " +
                $"viewport={tileScene.Viewport.Width}x{tileScene.Viewport.Height} " +
                $"world=({worldBounds.Left},{worldBounds.Bottom})-({worldBounds.Right},{worldBounds.Top}) " +
                $"worldSize={worldBounds.Width}x{worldBounds.Height}");
            RenderTarget2D renderTarget = await parent.RenderSceneToTexture(
                tileScene,
                centerX,
                centerY,
                parent.SectionNumber,
                showOverlays: false,
                asyncTextureLoad: false,
                renderToken).ConfigureAwait(false);
            if (renderTarget is null)
                return null;

            try
            {
                int width = SegmentationTileGrid.TileSize;
                int height = SegmentationTileGrid.TileSize;
                SegmentationDiag.Log(
                    $"CaptureTile target row={cell.Row} col={cell.Col} " +
                    $"requested={width}x{height} actual={renderTarget.Width}x{renderTarget.Height}");
                if (renderTarget.Width != width || renderTarget.Height != height)
                {
                    SegmentationDiag.Log(
                        $"CaptureTile rejected row={cell.Row} col={cell.Col}: " +
                        $"render target mismatch {renderTarget.Width}x{renderTarget.Height}");
                    return null;
                }

                return await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
                {
                    Color[] buffer = new Color[width * height];
                    renderTarget.GetData(buffer);
                    return buffer;
                }).Task.ConfigureAwait(false);
            }
            finally
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => renderTarget.Dispose()));
            }
        }

        /// <summary>
        /// What a tile request asks the server to send back: the fused SAM2 logits at their native resolution.
        /// </summary>
        /// <remarks>
        /// <c>NoUpscale</c> means the server does not upscale (see <c>RESPONSE_CONTENT_NO_UPSCALE</c> in the proto), so
        /// the logit grid comes back at one sample per four image pixels. This client polygonizes it at that
        /// resolution and the world transform scales the ring up afterwards. The mask overlay the user can turn
        /// on in preferences is drawn from the same logits (<see cref="SegmentationLogitField"/>) and only upscaled by the
        /// GPU when it is drawn, so no mask PNG is requested. A server that predates the flag ignores it and sends
        /// masks, which the client still reads.
        /// </remarks>
        private const SegmentationServiceTypes.ResponseContent RequestedContent =
            SegmentationServiceTypes.ResponseContent.Logits | SegmentationServiceTypes.ResponseContent.NoUpscale;

        /// <summary>
        /// World rectangle of the mosaic for a response sent at <paramref name="requestedDownsample"/>.
        /// <c>Width</c> and <c>Height</c> of a no-upscale response count samples, so they are scaled to image pixels.
        /// </summary>
        internal static Geometry.Rectangle MosaicWorldBounds(SegmentationResponse response, int requestedDownsample)
        {
            int downsample = Math.Max(1, requestedDownsample);
            int pixelScale = response.PixelScale();
            int width = Math.Max(1, response.Width) * pixelScale;
            int height = Math.Max(1, response.Height) * pixelScale;
            int originX = response.OriginX;
            int originY = response.OriginY;
            Geometry.Vector2 lowerLeft = new(
                originX * (double)downsample,
                originY * (double)downsample);
            Geometry.Vector2 upperRight = new(
                (originX + width) * (double)downsample,
                (originY + height) * (double)downsample);
            return new Geometry.Rectangle(lowerLeft, upperRight);
        }
    }
}
