using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
                int downsample = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
                TileSignature signature = CurrentTileSignature(downsample);
                Geometry.Rectangle bounds = GetCurrentViewportBounds();
                ViewportBounds = bounds;
                List<TileCell> visible = SegmentationTileGrid.CellsCovering(
                    bounds.LowerLeft.X,
                    bounds.LowerLeft.Y,
                    bounds.UpperRight.X,
                    bounds.UpperRight.Y,
                    downsample);
                bool grayscale = parent.CurrentChannelset.Length == 1;
                return (downsample, signature, visible, grayscale);
            }).Task.ConfigureAwait(false);
        }

        private TileSignature CurrentTileSignature(int downsample)
        {
            string volume = parent.Section?.VolumeViewModel?.Name ?? string.Empty;
            int section = parent.Section?.Number ?? 0;
            string channel = parent.CurrentChannel ?? string.Empty;
            string volumeTransform = parent.Section?.VolumeViewModel?.ActiveVolumeTransform ?? string.Empty;
            string sectionTransform = parent.CurrentTransform ?? string.Empty;
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
        /// Uploads that are running, by <see cref="TileKey"/>. A request that needs a tile already being rendered and
        /// uploaded, by the prefetch or by another request in flight, waits for that upload instead of rendering it again.
        /// </summary>
        private readonly Dictionary<string, Task<bool>> tileUploadsInFlight = [];
        private readonly object tileUploadsGate = new();

        /// <summary>
        /// Keys of tiles this client prefetched to the current segmentation server, kept across sessions so a
        /// revisited view is not rendered and sent again. Only the prefetch reads it: a tile the server has since
        /// dropped is still asked for during a request and uploaded then. Cleared when the endpoint changes.
        /// </summary>
        private static readonly TileUploadCache prefetchedToServer = new();

        /// <summary>Forgets <see cref="prefetchedToServer"/>, for a new segmentation server.</summary>
        internal static void ForgetPrefetchedTiles() => prefetchedToServer.Clear();

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

                if (await UploadTileAsync(signature, cell, grayscale, token).ConfigureAwait(false))
                    anyReady = true;
            }

            return anyReady;
        }

        /// <summary>
        /// True once the server holds <paramref name="cell"/>: already accepted in this session, or uploaded now.
        /// Joins an upload of the same cell that is already running; if that one fails or is cancelled while this
        /// caller still wants the cell, this caller tries once more itself. An <see cref="RpcException"/> from
        /// UploadTile is not caught.
        /// </summary>
        private async Task<bool> UploadTileAsync(TileSignature signature, TileCell cell, bool grayscale, CancellationToken token)
        {
            string key = TileKey(signature, cell.Row, cell.Col);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (uploadedTileKeys.Contains(key))
                    return true;

                Task<bool> upload;
                bool started = false;
                lock (tileUploadsGate)
                {
                    if (!tileUploadsInFlight.TryGetValue(key, out upload))
                    {
                        upload = CaptureAndUploadTileAsync(signature, cell, key, grayscale, token);
                        tileUploadsInFlight[key] = upload;
                        started = true;
                    }
                }

                try
                {
                    if (await upload.ConfigureAwait(false))
                        return true;
                }
                catch (OperationCanceledException) when (!started && !token.IsCancellationRequested)
                {
                    // The caller that started the upload gave up on it; this one still wants the cell.
                }
                finally
                {
                    if (started)
                    {
                        lock (tileUploadsGate)
                            tileUploadsInFlight.Remove(key);
                    }
                }

                token.ThrowIfCancellationRequested();
                if (started)
                    return false;
            }

            return false;
        }

        /// <summary>One render, encode and UploadTile of <paramref name="cell"/>; false when the capture failed.</summary>
        private async Task<bool> CaptureAndUploadTileAsync(
            TileSignature signature,
            TileCell cell,
            string key,
            bool grayscale,
            CancellationToken token)
        {
            // Started under tileUploadsGate; the render and encode must run after the lock is released.
            await Task.Yield();
            if (grpcClient is null)
                return false;

            var (png, width, height) = await CaptureTileImage(
                cell,
                signature.Downsample,
                grayscale,
                token,
                gray => tileImages.Store(key, gray)).ConfigureAwait(false);
            if (png is null || png.Length == 0)
            {
                token.ThrowIfCancellationRequested();
                SegmentationDiag.Log($"Capture failed row={cell.Row} col={cell.Col} ds={signature.Downsample}");
                Debug.WriteLine($"Failed to capture tile row={cell.Row} col={cell.Col}");
                return false;
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
                prefetchedToServer.Add(key);
                SegmentationDiag.Log($"UploadTile ok row={cell.Row} col={cell.Col} alreadyCached={response.AlreadyCached}");
                Debug.WriteLine(
                    $"UploadTile accepted key=ds={signature.Downsample}|row={cell.Row}|col={cell.Col} " +
                    $"alreadyCached={response.AlreadyCached}");
                return true;
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"UploadTile RPC {rpcEx.StatusCode}: {rpcEx.Status.Detail}");
                throw;
            }
        }

        /// <summary>
        /// Uploads, ahead of the requests that will need them, the cells covering each of <paramref name="regions"/>
        /// (world space), in the order given and nearest each region's centre first. The server encodes a tile when
        /// it arrives, so a request that finds its tiles already there skips the round trips of asking for them.
        /// One tile at a time, so requests in flight still get their own tiles promptly; a request that needs a tile
        /// the prefetch is sending waits for that upload. Stops on cancellation or an RPC error, which a request
        /// would meet anyway.
        /// </summary>
        internal async Task PrefetchTilesAsync(IReadOnlyList<Geometry.Rectangle> regions, CancellationToken token)
        {
            if (grpcClient is null || regions.Count == 0 || !IsCameraWithinTileSubmission(recordSkip: false))
                return;

            Stopwatch timer = Stopwatch.StartNew();
            int uploaded = 0;
            int skipped = 0;
            try
            {
                (int downsample, TileSignature signature, bool grayscale) = await ReadPrefetchSignatureAsync().ConfigureAwait(false);

                List<TileCell> cells = [];
                HashSet<TileCell> seen = [];
                foreach (Geometry.Rectangle region in regions)
                {
                    Geometry.Vector2 center = region.Center;
                    double cellWorld = SegmentationTileGrid.TileSize * (double)downsample;
                    IEnumerable<TileCell> covering = SegmentationTileGrid.CellsCovering(
                            region.LowerLeft.X, region.LowerLeft.Y, region.UpperRight.X, region.UpperRight.Y, downsample)
                        .OrderBy(cell => Geometry.Vector2.DistanceSquared(
                            new Geometry.Vector2((cell.Col + 0.5) * cellWorld, (cell.Row + 0.5) * cellWorld), center));
                    foreach (TileCell cell in covering)
                    {
                        if (seen.Add(cell))
                            cells.Add(cell);
                    }
                }

                SegmentationDiag.Log($"Prefetch start regions={regions.Count} tiles={cells.Count} ds={downsample}");
                (uploaded, skipped) = await UploadPrefetchCellsAsync(signature, cells, grayscale, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"Prefetch stopped: UploadTile RPC {rpcEx.StatusCode}");
            }

            SegmentationDiag.Log($"Prefetch done uploaded={uploaded} alreadySent={skipped} ms={timer.ElapsedMilliseconds}");
        }

        /// <summary>
        /// Uploads the image tiles under <paramref name="mask"/>, nearest <paramref name="nearest"/> first, so the server has them
        /// encoded before a request for the mask needs them. The paint command calls it after every change; a new call should
        /// cancel the last one, whose tiles already sent are skipped. Stops on cancellation or an RPC error.
        /// </summary>
        internal async Task PrefetchMaskTilesAsync(SegmentationMaskUnion mask, Geometry.Vector2 nearest, CancellationToken token)
        {
            if (grpcClient is null || mask.IsEmpty || !IsCameraWithinTileSubmission(recordSkip: false))
                return;

            Stopwatch timer = Stopwatch.StartNew();
            int uploaded = 0;
            int skipped = 0;
            try
            {
                (int downsample, TileSignature signature, bool grayscale) = await ReadPrefetchSignatureAsync().ConfigureAwait(false);
                double cellWorld = SegmentationTileGrid.TileSize * (double)downsample;
                List<TileCell> cells = [.. mask.TilesTouched(downsample).OrderBy(cell => Geometry.Vector2.DistanceSquared(
                    new Geometry.Vector2((cell.Col + 0.5) * cellWorld, (cell.Row + 0.5) * cellWorld), nearest))];
                SegmentationDiag.Log($"Prefetch mask tiles={cells.Count} ds={downsample}");
                (uploaded, skipped) = await UploadPrefetchCellsAsync(signature, cells, grayscale, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"Prefetch mask stopped: UploadTile RPC {rpcEx.StatusCode}");
            }

            SegmentationDiag.Log($"Prefetch mask done uploaded={uploaded} alreadySent={skipped} ms={timer.ElapsedMilliseconds}");
        }

        /// <summary>The tile level, signature and colour mode a request sent now would use, read on the UI thread.</summary>
        private Task<(int Downsample, TileSignature Signature, bool Grayscale)> ReadPrefetchSignatureAsync() =>
            Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
            {
                int level = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
                return (level, CurrentTileSignature(level), parent.CurrentChannelset.Length == 1);
            }).Task;

        /// <summary>Uploads <paramref name="cells"/> one at a time, skipping those already sent; returns (uploaded, skipped).</summary>
        private async Task<(int Uploaded, int Skipped)> UploadPrefetchCellsAsync(
            TileSignature signature, IReadOnlyList<TileCell> cells, bool grayscale, CancellationToken token)
        {
            int uploaded = 0;
            int skipped = 0;
            foreach (TileCell cell in cells)
            {
                token.ThrowIfCancellationRequested();
                string key = TileKey(signature, cell.Row, cell.Col);
                if (prefetchedToServer.Contains(key) || uploadedTileKeys.Contains(key))
                {
                    skipped++;
                    continue;
                }

                if (await UploadTileAsync(signature, cell, grayscale, token).ConfigureAwait(false))
                    uploaded++;
            }

            return (uploaded, skipped);
        }

        /// <summary>
        /// Longest a SegmentTilesStream conversation may go with no sign of progress. Every message from the server (a request for
        /// tiles, the result) and every tile this client finishes uploading restarts it, so a large object that keeps the server
        /// busy for longer is not cut off while tiles are still moving. On a cell of about 100 tiles the server went over two
        /// minutes between requests for tiles.
        /// </summary>
        private const int SegmentStreamIdleSeconds = 300;

        /// <summary>Upper bound for one whole conversation, however busy, so a stream that never ends cannot hang the request.</summary>
        private const int SegmentStreamMaxMinutes = 30;

        /// <summary>
        /// One SegmentTilesStream call for <paramref name="cells"/> only. Other keys in the session stay
        /// out of the request so an earlier viewport upload cannot overflow the server cache.
        /// The server owns the growth: it sends <c>needed</c> when the mask reaches cells it lacks, this
        /// method uploads them and answers, and the server continues the same walk. The finished mask
        /// arrives as <c>result</c>. A NOT_FOUND status for a prompt cell surfaces as an
        /// <see cref="RpcException"/> for <see cref="SegmentWithMissingTileRetriesAsync"/> to handle.
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
            {
                if (!uploadedTileKeys.Contains(TileKey(signature, cell.Row, cell.Col)))
                    continue;

                request.Tiles.Add(ToCoord(signature, cell));
            }

            if (request.Tiles.Count == 0)
                throw new InvalidOperationException("No uploaded tiles cover the segmentation prompts.");

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

            // The idle timer is restarted by every sign of progress (StreamAlive); when it runs out the stream is cancelled and the
            // request fails as a timeout.
            using CancellationTokenSource idle = new();
            using CancellationTokenSource streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, idle.Token);
            void StreamAlive()
            {
                try
                {
                    idle.CancelAfter(TimeSpan.FromSeconds(SegmentStreamIdleSeconds));
                }
                catch (ObjectDisposedException)
                {
                }
            }

            StreamAlive();
            CancellationToken streamToken = streamCancellation.Token;
            CallOptions callOptions = new(
                deadline: DateTime.UtcNow.AddMinutes(SegmentStreamMaxMinutes),
                cancellationToken: streamToken);
            SegmentationResponse? response = null;
            List<string> requestedTileList = [];
            try
            {
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

                    while (response is null && await call.ResponseStream.MoveNext(streamToken).ConfigureAwait(false))
                    {
                        StreamAlive();
                        SegmentTilesStreamResponse update = call.ResponseStream.Current;
                        if (update.UpdateCase == SegmentTilesStreamResponse.UpdateOneofCase.Result)
                        {
                            response = update.Result;
                        }
                        else if (update.UpdateCase == SegmentTilesStreamResponse.UpdateOneofCase.Needed)
                        {
                            requestedTileList.AddRange(update.Needed.Tiles.Select(tile => $"{tile.Row},{tile.Col}"));
                            TilesAnswer answer = await UploadNeededTilesAsync(signature, update.Needed, grayscale, streamToken, StreamAlive).ConfigureAwait(false);
                            if (update.Needed.Masks.Count > 0)
                                await AnswerNeededMasksAsync(call.RequestStream, update.Needed, answer, signature, backgroundMask, backgroundSent, maskTileScale, streamToken).ConfigureAwait(false);

                            await call.RequestStream.WriteAsync(new SegmentTilesStreamRequest { Answer = answer }).ConfigureAwait(false);
                            StreamAlive();
                        }
                    }
                }
            }
            catch (Exception ex) when (idle.IsCancellationRequested && !token.IsCancellationRequested &&
                (ex is OperationCanceledException || ex is RpcException { StatusCode: StatusCode.Cancelled }))
            {
                // The idle timer, not the caller, ended the stream: report it as the timeout it is.
                throw new RpcException(new Status(
                    StatusCode.DeadlineExceeded,
                    $"no tile request, tile or result for {SegmentStreamIdleSeconds / 60} minutes"));
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
        /// <param name="onTileReady">Called after each tile is uploaded, so the stream's idle timer restarts.</param>
        private async Task<TilesAnswer> UploadNeededTilesAsync(
            TileSignature signature,
            TilesNeeded needed,
            bool grayscale,
            CancellationToken token,
            Action? onTileReady = null)
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
                string key = TileKey(signature, tile.Row, tile.Col);
                uploadedTileKeys.Remove(key);
                prefetchedToServer.Remove(key);
                bool uploaded = await UploadMissingTilesAsync(
                    signature,
                    [new TileCell(tile.Row, tile.Col)],
                    grayscale,
                    token).ConfigureAwait(false);
                (uploaded ? answer.Ready : answer.Unavailable).Add(tile);
                onTileReady?.Invoke();
            }

            SegmentationDiag.Log(
                $"SegmentTilesStream answer ready=[{string.Join(";", answer.Ready.Select(t => $"{t.Row},{t.Col}"))}] " +
                $"unavailable=[{string.Join(";", answer.Unavailable.Select(t => $"{t.Row},{t.Col}"))}]");
            return answer;
        }

        /// <summary>
        /// Reads the tile from a server NotFound detail of the form <c>row=R col=C</c>. Digits are read with the
        /// invariant culture, so a locale with another minus sign cannot break the parse, and a number too large
        /// for an int returns false instead of throwing.
        /// </summary>
        internal static bool TryParseMissingTile(string? detail, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (string.IsNullOrEmpty(detail))
                return false;

            Match match = Regex.Match(detail, @"row=(-?\d+)\s+col=(-?\d+)");
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsedRow)
                || !int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsedCol))
            {
                return false;
            }

            row = parsedRow;
            col = parsedCol;
            return true;
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
                using CancellationTokenSource renderCancellation = CreateRenderCancellation(cancellationToken);
                CancellationToken renderToken = renderCancellation.Token;
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
                    parent.Section.Number,
                    showOverlays: false,
                    asyncTextureLoad: false,
                    renderToken).ConfigureAwait(false);
                if (renderTarget is null)
                {
                    SegmentationDiag.Log(
                        $"CaptureTile no pixels row={cell.Row} col={cell.Col} " +
                        $"cancelled={cancellationToken.IsCancellationRequested}");
                    return (null, 0, 0);
                }

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
                        return (null, 0, 0);
                    }

                    Color[] pixels = await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
                    {
                        Color[] buffer = new Color[width * height];
                        renderTarget.GetData(buffer);
                        return buffer;
                    }).Task.ConfigureAwait(false);
                    // Awaiting the dispatcher task can resume this method on the UI thread; encoding a 1024x1024
                    // tile there stalls the viewer, so it is pushed to the pool explicitly.
                    Task<byte[]> grayTask = onGray is null
                        ? Task.FromResult<byte[]>([])
                        : Task.Run(
                            () => SegmentationTileImages.PrepareGray(
                                SegmentationCaptureEncoder.StretchedLuma(pixels, grayscale),
                                width,
                                height),
                            cancellationToken);
                    byte[] pngData = await Task.Run(
                        () => SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, grayscale),
                        cancellationToken).ConfigureAwait(false);
                    byte[] gray = await grayTask.ConfigureAwait(false);
                    SegmentationCaptureEncoder.SaveCaptureForReview(
                        pngData,
                        width,
                        height,
                        $"s{parent.Section?.Number}_ds{downsample}_r{cell.Row}_c{cell.Col}");
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
                finally
                {
                    Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => renderTarget.Dispose()));
                }
            }
            catch (OperationCanceledException)
            {
                SegmentationDiag.Log($"CaptureTile cancelled row={cell.Row} col={cell.Col}");
                return (null, 0, 0);
            }
            catch (Exception ex)
            {
                SegmentationDiag.Log($"CaptureTile error row={cell.Row} col={cell.Col}: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"Error capturing tile row={cell.Row} col={cell.Col}: {ex.Message}");
                return (null, 0, 0);
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
