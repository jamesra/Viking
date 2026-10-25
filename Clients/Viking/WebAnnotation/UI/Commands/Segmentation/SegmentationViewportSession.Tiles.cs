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
        /// Requests, in any session, uploading tiles they are waiting for: their prompt tiles or the tiles the server asked for.
        /// While it is above zero the prefetch starts no new tile, so the server's GPU encodes the tiles a request needs first.
        /// </summary>
        private static int requestTileUploads;

        /// <summary>How often a paused prefetch looks again whether requests still wait on tiles.</summary>
        private const int PrefetchYieldPollMilliseconds = 100;

        /// <summary>
        /// Returns once no request is uploading tiles it waits for; the time spent waiting, in milliseconds.
        /// </summary>
        private static async Task<long> WaitWhileRequestsNeedTilesAsync(CancellationToken token)
        {
            if (Volatile.Read(ref requestTileUploads) == 0)
                return 0;

            Stopwatch paused = Stopwatch.StartNew();
            while (Volatile.Read(ref requestTileUploads) > 0)
                await Task.Delay(PrefetchYieldPollMilliseconds, token).ConfigureAwait(false);

            return paused.ElapsedMilliseconds;
        }

        /// <summary>
        /// Waits for <paramref name="upload"/> until <paramref name="token"/> is cancelled, without cancelling the upload: it
        /// runs under the session's upload lifetime, so a caller that gives up leaves it to finish for the next one.
        /// </summary>
        internal static async Task<bool> WaitForUploadAsync(Task<bool> upload, CancellationToken token)
        {
            if (!token.CanBeCanceled || upload.IsCompleted)
                return await upload.ConfigureAwait(false);

            TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(upload, cancelled.Task).ConfigureAwait(false) != upload)
                {
                    // Nobody may await it again; observe its fault so it is not reported as unobserved.
                    _ = upload.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    throw new OperationCanceledException(token);
                }
            }

            return await upload.ConfigureAwait(false);
        }

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
            // Every caller is a request waiting for these tiles; the prefetch holds back while it does.
            Interlocked.Increment(ref requestTileUploads);
            try
            {
                foreach (TileCell cell in cells)
                {
                    token.ThrowIfCancellationRequested();
                    if (!seen.Add((cell.Row, cell.Col)))
                        continue;

                    if (await UploadTileAsync(signature, cell, grayscale, token).ConfigureAwait(false))
                        anyReady = true;
                }
            }
            finally
            {
                Interlocked.Decrement(ref requestTileUploads);
            }

            return anyReady;
        }

        /// <summary>
        /// True once the server holds <paramref name="cell"/>: already accepted in this session, or uploaded now.
        /// Joins an upload of the same cell that is already running; if that one fails or is aborted while this
        /// caller still wants the cell, this caller tries once more itself. <paramref name="token"/> only stops this caller
        /// waiting: the upload runs under <see cref="TileUploadLifetimeToken"/> and is left to finish. An
        /// <see cref="RpcException"/> from UploadTile is not caught.
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
                        upload = TrackTileUploadAsync(key, CaptureAndUploadTileAsync(signature, cell, key, grayscale, TileUploadLifetimeToken));
                        tileUploadsInFlight[key] = upload;
                        started = true;
                    }
                }

                try
                {
                    if (await WaitForUploadAsync(upload, token).ConfigureAwait(false))
                        return true;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // The session aborted the upload (CancelPendingWork) while this caller still wants the cell.
                }

                token.ThrowIfCancellationRequested();
                if (started)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// How long one UploadTile may take. A busy server (sharing its GPU, or growing a large mask) has held an upload in its
        /// queue for about 30 s before handling it, and a timed-out upload fails the whole request that needed the tile.
        /// </summary>
        private const int UploadTileDeadlineSeconds = 120;

        /// <summary>Removes <paramref name="key"/> from <see cref="tileUploadsInFlight"/> when <paramref name="upload"/> ends.</summary>
        private async Task<bool> TrackTileUploadAsync(string key, Task<bool> upload)
        {
            try
            {
                return await upload.ConfigureAwait(false);
            }
            finally
            {
                // The entry was added under this lock right after this task was made, so taking it here cannot run first.
                lock (tileUploadsGate)
                    tileUploadsInFlight.Remove(key);
            }
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
            CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(UploadTileDeadlineSeconds), cancellationToken: token);
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
            long pausedMs = 0;
            try
            {
                (int downsample, TileSignature signature, bool grayscale) = await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
                {
                    int level = ResolveTileDownsample(parent.CameraDownsample);
                    return (level, CurrentTileSignature(level), parent.IsGrayscale);
                }).Task.ConfigureAwait(false);

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
                (uploaded, skipped, pausedMs) = await UploadPrefetchCellsAsync(signature, cells, grayscale, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"Prefetch stopped: UploadTile RPC {rpcEx.StatusCode}");
            }

            SegmentationDiag.Log($"Prefetch done uploaded={uploaded} alreadySent={skipped} pausedForRequests={pausedMs}ms ms={timer.ElapsedMilliseconds}");
        }

        /// <summary>
        /// Uploads <paramref name="cells"/> one at a time, skipping those already sent and holding back while a request uploads
        /// tiles it waits for. <paramref name="token"/> stops the loop; an upload already started is left to finish.
        /// Returns (uploaded, skipped, milliseconds paused for requests).
        /// </summary>
        private async Task<(int Uploaded, int Skipped, long PausedMs)> UploadPrefetchCellsAsync(
            TileSignature signature, IReadOnlyList<TileCell> cells, bool grayscale, CancellationToken token)
        {
            int uploaded = 0;
            int skipped = 0;
            long pausedMs = 0;
            foreach (TileCell cell in cells)
            {
                token.ThrowIfCancellationRequested();
                string key = TileKey(signature, cell.Row, cell.Col);
                if (prefetchedToServer.Contains(key) || uploadedTileKeys.Contains(key))
                {
                    skipped++;
                    continue;
                }

                pausedMs += await WaitWhileRequestsNeedTilesAsync(token).ConfigureAwait(false);
                if (await UploadTileAsync(signature, cell, grayscale, token).ConfigureAwait(false))
                    uploaded++;
            }

            return (uploaded, skipped, pausedMs);
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
                {
                    SegmentationDiag.Log(
                        $"CaptureTile no pixels row={cell.Row} col={cell.Col} " +
                        $"cancelled={cancellationToken.IsCancellationRequested}");
                    return (null, 0, 0);
                }

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
