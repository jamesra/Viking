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
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using TileGrid = Viking.gRPC.Geometry.V1.TileGrid;

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
            public TileSignature(string volume, int section, string channel, string transform, int downsample, CoordinateSpace space)
            {
                Volume = volume;
                Section = section;
                Channel = channel;
                Transform = transform;
                Downsample = downsample;
                Space = space;
            }

            public string Volume { get; }
            public int Section { get; }
            public string Channel { get; }
            public string Transform { get; }
            public int Downsample { get; }

            /// <summary>The space of the tiles and of every prompt and result of a request naming them.</summary>
            public CoordinateSpace Space { get; }
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
            string channel = parent.CaptureChannelKey;
            // A section the active volume transform has no entry for is drawn with an identity transform, so it is in
            // mosaic space even while a volume transform is chosen, and its volume transform part is empty.
            bool inVolumeSpace = parent.Section?.VolumeViewModel?.HasVolumeTransformFor(section) ?? false;
            string volumeTransform = inVolumeSpace ? parent.Section!.VolumeViewModel.ActiveVolumeTransform ?? string.Empty : string.Empty;
            string sectionTransform = parent.CurrentTransform ?? string.Empty;
            return new TileSignature(
                volume, section, channel, volumeTransform + "|" + sectionTransform, downsample,
                inVolumeSpace ? CoordinateSpace.Volume : CoordinateSpace.Mosaic);
        }

        internal static string TileKey(TileSignature signature, int row, int col) =>
            $"{signature.Volume}\n{signature.Section}\n{signature.Channel}\n{signature.Transform}\n{signature.Space}\n{signature.Downsample}\n{row}\n{col}";

        /// <summary>The grid every tile of <paramref name="signature"/> is a cell of.</summary>
        internal static TileGrid ToGrid(TileSignature signature) => new()
        {
            Volume = signature.Volume,
            Section = signature.Section,
            Channel = signature.Channel,
            Transform = signature.Transform,
            Downsample = signature.Downsample,
            Space = signature.Space
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
                Grid = ToGrid(signature),
                Cell = new GridCell { Row = cell.Row, Col = cell.Col },
                ImageData = Google.Protobuf.ByteString.CopyFrom(png)
            };
            CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(UploadTileDeadlineSeconds), cancellationToken: token);
            SegmentationDiag.Log(
                $"UploadTile send row={cell.Row} col={cell.Col} ds={signature.Downsample} " +
                $"size={width}x{height} encodedBytes={png.Length}");
            Debug.WriteLine(
                $"UploadTile key=vol={signature.Volume}|sec={signature.Section}|ch={signature.Channel}|" +
                $"xf={signature.Transform}|ds={signature.Downsample}|row={cell.Row}|col={cell.Col} " +
                $"dimensions={width}x{height} bytes={png.Length}");
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
            int held = 0;
            long pausedMs = 0;
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
                (uploaded, skipped, held, pausedMs) = await UploadPrefetchCellsAsync(signature, cells, grayscale, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"Prefetch stopped: UploadTile RPC {rpcEx.StatusCode}");
            }

            SegmentationDiag.Log($"Prefetch done uploaded={uploaded} alreadySent={skipped} serverHeld={held} pausedForRequests={pausedMs}ms ms={timer.ElapsedMilliseconds}");
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
            int held = 0;
            long pausedMs = 0;
            try
            {
                (int downsample, TileSignature signature, bool grayscale) = await ReadPrefetchSignatureAsync().ConfigureAwait(false);
                double cellWorld = SegmentationTileGrid.TileSize * (double)downsample;
                List<TileCell> cells = [.. mask.TilesTouched(downsample).OrderBy(cell => Geometry.Vector2.DistanceSquared(
                    new Geometry.Vector2((cell.Col + 0.5) * cellWorld, (cell.Row + 0.5) * cellWorld), nearest))];
                SegmentationDiag.Log($"Prefetch mask tiles={cells.Count} ds={downsample}");
                (uploaded, skipped, held, pausedMs) = await UploadPrefetchCellsAsync(signature, cells, grayscale, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (RpcException rpcEx)
            {
                SegmentationDiag.Log($"Prefetch mask stopped: UploadTile RPC {rpcEx.StatusCode}");
            }

            SegmentationDiag.Log($"Prefetch mask done uploaded={uploaded} alreadySent={skipped} serverHeld={held} pausedForRequests={pausedMs}ms ms={timer.ElapsedMilliseconds}");
        }

        /// <summary>The tile level, signature and colour mode a request sent now would use, read on the UI thread.</summary>
        private Task<(int Downsample, TileSignature Signature, bool Grayscale)> ReadPrefetchSignatureAsync() =>
            Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
            {
                int level = ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);
                return (level, CurrentTileSignature(level), parent.CurrentChannelset.Length == 1);
            }).Task;

        /// <summary>
        /// Uploads <paramref name="cells"/> one at a time, skipping those already sent and those the server says it holds, and
        /// holding back while a request uploads tiles it waits for. <paramref name="token"/> stops the loop; an upload already
        /// started is left to finish. Returns (uploaded, skipped as already sent, skipped as held, milliseconds paused for requests).
        /// </summary>
        private async Task<(int Uploaded, int Skipped, int Held, long PausedMs)> UploadPrefetchCellsAsync(
            TileSignature signature, IReadOnlyList<TileCell> cells, bool grayscale, CancellationToken token)
        {
            int skipped = 0;
            List<TileCell> unsent = [];
            foreach (TileCell cell in cells)
            {
                string key = TileKey(signature, cell.Row, cell.Col);
                if (prefetchedToServer.Contains(key) || uploadedTileKeys.Contains(key))
                    skipped++;
                else
                    unsent.Add(cell);
            }

            HashSet<TileCell> held = grpcClient is null
                ? []
                : await HeldCellsAsync(
                    request => grpcClient.GetHeldTilesAsync(
                        request, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(HeldTilesDeadlineSeconds), cancellationToken: token)).ResponseAsync,
                    signature,
                    unsent,
                    Viking.Common.AccessibleVolumeSession.AnnotationContextId,
                    token).ConfigureAwait(false);

            int uploaded = 0;
            long pausedMs = 0;
            foreach (TileCell cell in unsent)
            {
                token.ThrowIfCancellationRequested();
                if (held.Contains(cell))
                {
                    // The stream will not ask for it, so neither does the next prefetch.
                    prefetchedToServer.Add(TileKey(signature, cell.Row, cell.Col));
                    continue;
                }

                pausedMs += await WaitWhileRequestsNeedTilesAsync(token).ConfigureAwait(false);
                if (await UploadTileAsync(signature, cell, grayscale, token).ConfigureAwait(false))
                    uploaded++;
            }

            return (uploaded, skipped, held.Count, pausedMs);
        }

        /// <summary>Most cells one GetHeldTiles call names; the server refuses more.</summary>
        internal const int MaxHeldTilesCells = 256;

        /// <summary>How long a GetHeldTiles call may take. It is a few dictionary lookups; a slow answer is not worth waiting for.</summary>
        private const int HeldTilesDeadlineSeconds = 5;

        /// <summary>
        /// The cells of <paramref name="cells"/> the server says it holds or reads itself, asked through <paramref name="ask"/>
        /// (GetHeldTiles) in batches of <see cref="MaxHeldTilesCells"/>. A batch whose call fails (a server without the RPC, a
        /// timeout) counts as holding nothing, so those cells are uploaded as before.
        /// </summary>
        internal static async Task<HashSet<TileCell>> HeldCellsAsync(
            Func<HeldTilesRequest, Task<HeldTilesResponse>> ask,
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            long annotationContextId,
            CancellationToken token)
        {
            HashSet<TileCell> held = [];
            for (int start = 0; start < cells.Count; start += MaxHeldTilesCells)
            {
                token.ThrowIfCancellationRequested();
                List<TileCell> batch = [.. cells.Skip(start).Take(MaxHeldTilesCells)];
                HeldTilesRequest request = new()
                {
                    Grid = ToGrid(signature),
                    AnnotationContextId = annotationContextId
                };
                foreach (TileCell cell in batch)
                    request.Cells.Add(new GridCell { Row = cell.Row, Col = cell.Col });

                HeldTilesResponse response;
                try
                {
                    response = await ask(request).ConfigureAwait(false);
                }
                catch (RpcException rpcEx) when (!token.IsCancellationRequested)
                {
                    SegmentationDiag.Log($"GetHeldTiles {rpcEx.StatusCode}: {rpcEx.Status.Detail}; uploading these {batch.Count} tiles");
                    continue;
                }

                if (response.Held.Count != batch.Count)
                {
                    SegmentationDiag.Log($"GetHeldTiles answered {response.Held.Count} of {batch.Count} cells; uploading them");
                    continue;
                }

                for (int k = 0; k < batch.Count; k++)
                {
                    if (response.Held[k])
                        held.Add(batch[k]);
                }
            }

            return held;
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
        /// <c>needed</c> for the cells it lacks (a prompt cell included) and for the cells whose neighbouring annotations it
        /// has not been sent; this method uploads the tiles, answers with the tiles it could not supply and the background
        /// shapes reaching those cells, and the server continues the same walk. The finished segmentation arrives as
        /// <c>result</c>.
        /// </summary>
        private async Task<SegmentationResponse> SegmentTilesStreamAsync(
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            bool grayscale,
            CancellationToken token,
            IReadOnlyList<SegmentationSeed>? seeds = null,
            SegmentationBackgroundShapes? backgroundShapes = null,
            ulong requestId = 0,
            SegmentationMaskUnion? backgroundBitmap = null)
        {
            if (grpcClient is null)
                throw new InvalidOperationException("Segmentation client is not connected.");

            if (cells.Count == 0)
                throw new InvalidOperationException("No tile cells cover the segmentation prompts.");

            // The model id comes from the status query a connection starts. The first request after the app starts, or the first after
            // a model change reset what was known, may get here before it has answered, so (re)start it if nothing is known and wait.
            // Unset when the server reported none: it accepts that unless the model's manifest requires an id.
            BeginServerStatusRefresh(grpcClient);
            await WhenServerStatusKnownAsync().ConfigureAwait(false);

            // The prompt: clicks and seed shapes in the two Features, painted bitmaps as mask tiles, and the
            // neighbouring annotations that reach the prompt cells as background shapes. The rest of the background goes out as
            // the server asks for the cells it reaches.
            HashSet<int> backgroundSent = [];
            int maskTileScale = SegmentationMaskTiles.ConfiguredScale;
            double pointRadius = WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius;
            StartMessage start = await Task.Run(
                () => BuildStart(
                    signature,
                    cells,
                    foregroundPoints,
                    backgroundPoints,
                    seeds,
                    backgroundShapes,
                    backgroundSent,
                    pointRadius,
                    maskTileScale,
                    requestId,
                    ServerModelId,
                    Viking.Common.AccessibleVolumeSession.AnnotationContextId,
                    backgroundBitmap),
                token).ConfigureAwait(false);
            SegmentTilesRequest request = start.Request;

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
                    // The announced mask tiles right after start: the server starts growing only when all of them have arrived.
                    foreach (SegmentTilesStreamRequest message in start.Messages())
                        await call.RequestStream.WriteAsync(message).ConfigureAwait(false);

                    SegmentationDiag.Log(
                        $"SegmentTilesStream start fgShapes={request.ForegroundFeature.Shapes.Count} bgShapes={request.BackgroundFeature?.Shapes.Count ?? 0} " +
                        $"maskTiles={start.MaskTiles.Count} scale={maskTileScale}");

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
                            AddBackgroundAnswer(answer, update.Needed, backgroundShapes, signature.Downsample, backgroundSent);
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
            string islands = string.Join(
                ",",
                response.Segments.Select(segment => SegmentationLogitField.HasLogits(segment)
                    ? $"{segment.Island}:{segment.LogitsOrigin.X},{segment.LogitsOrigin.Y}:logits {segment.LogitsSize.Width}x{segment.LogitsSize.Height}@{segment.LogitsScale} score={segment.Score:0.00}"
                    : $"{segment.Island}:no logits"));
            SegmentationDiag.Log(
                $"SegmentTiles response req={requestId}{(response.RequestId == requestId ? string.Empty : $" MISMATCH echoed={response.RequestId}")} " +
                $"submittedTiles=[{submittedTiles}] requestedTiles=[{requestedTiles}] backgroundShapesSent={backgroundSent.Count} " +
                $"islands=[{islands}]");
            return response;
        }

        /// <summary>The first message of a stream and the mask tiles it announces.</summary>
        internal sealed class StartMessage(SegmentTilesRequest request, List<SegmentationServiceTypes.MaskTile> maskTiles)
        {
            public SegmentTilesRequest Request { get; } = request;

            /// <summary>The tiles of the painted bitmaps, FOREGROUND then BACKGROUND, as many as <c>mask_tile_count</c> announces. Empty without one.</summary>
            public List<SegmentationServiceTypes.MaskTile> MaskTiles { get; } = maskTiles;

            /// <summary>The stream messages to write: <c>start</c>, then the announced mask tiles.</summary>
            public IEnumerable<SegmentTilesStreamRequest> Messages() =>
                new[] { new SegmentTilesStreamRequest { Start = Request } }
                    .Concat(SegmentationMaskTiles.Chunk(MaskTiles).Select(tiles => new SegmentTilesStreamRequest { MaskTiles = tiles }));
        }

        /// <summary>
        /// The <c>start</c> of a request on <paramref name="signature"/>'s grid, every coordinate in the grid frame (the tiles'
        /// space divided by the downsample, Y up):
        /// <list type="bullet">
        /// <item>the foreground Feature: the clicks, and each geometry seed as a shape (a circle as <c>Shape.circle</c> with its
        /// true radius, a polygon with its holes), which the server softens;</item>
        /// <item>a painted bitmap seed as FOREGROUND mask tiles, used as drawn, counted in <c>mask_tile_count</c>; background
        /// clicks clear a disc of <paramref name="pointRadiusPixels"/> from it;</item>
        /// <item>the background Feature: the background clicks, and the neighbouring shapes that reach <paramref name="cells"/>,
        /// recorded in <paramref name="backgroundSent"/> so they are not sent again;</item>
        /// <item>a painted <paramref name="backgroundBitmap"/> as BACKGROUND mask tiles, counted with the foreground ones.</item>
        /// </list>
        /// </summary>
        internal static StartMessage BuildStart(
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            IReadOnlyList<Geometry.Vector2>? foregroundPoints,
            IReadOnlyList<Geometry.Vector2>? backgroundPoints,
            IReadOnlyList<SegmentationSeed>? seeds,
            SegmentationBackgroundShapes? backgroundShapes,
            ISet<int> backgroundSent,
            double pointRadiusPixels,
            int maskTileScale,
            ulong requestId = 0,
            string? modelId = null,
            long annotationContextId = 0,
            SegmentationMaskUnion? backgroundBitmap = null)
        {
            int downsample = signature.Downsample;
            SegmentTilesRequest request = new()
            {
                Grid = ToGrid(signature),
                RequestId = requestId,
                MaskThreshold = (float)WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold,
                UseMaskInput = WebAnnotation.Global.AnnotationSettings.SegmentationUseMaskInput,
                // A server the context is local to reads plain greyscale mosaic cells from its tileset instead of asking.
                AnnotationContextId = annotationContextId
            };
            if (modelId is not null)
                request.ModelId = modelId;

            foreach (TileCell cell in cells)
                request.Tiles.Add(new GridCell { Row = cell.Row, Col = cell.Col });

            (SegmentationServiceTypes.Feature foreground, SegmentationServiceTypes.Feature background) =
                ClickFeatures(foregroundPoints, backgroundPoints, downsample);

            SegmentationMaskUnion? bitmap = null;
            foreach (SegmentationSeed seed in seeds ?? [])
            {
                if (seed.Mask is not null)
                {
                    bitmap ??= new SegmentationMaskUnion();
                    bitmap.Add(seed);
                }
                else if (SegmentationPromptShapes.FromSeed(seed, downsample) is { } shape)
                {
                    foreground.Shapes.Add(shape);
                }
            }

            if (backgroundShapes is not null)
            {
                background.Shapes.Add(backgroundShapes.ToShapes(
                    backgroundShapes.TakeReaching(cells, downsample, backgroundSent), downsample));
            }

            request.ForegroundFeature = foreground;
            if (background.Points.Count > 0 || background.Shapes.Count > 0)
                request.BackgroundFeature = background;

            List<SegmentationServiceTypes.MaskTile> maskTiles = SegmentationMaskTiles.PrepareStart(
                request, bitmap, backgroundBitmap, downsample, backgroundPoints, pointRadiusPixels, maskTileScale);
            return new StartMessage(request, maskTiles);
        }

        /// <summary>
        /// The clicks as the points of the request's two Features, in the grid frame at <paramref name="downsample"/> (X right,
        /// Y up). Both Features are returned, empty when there are no clicks of that side.
        /// </summary>
        internal static (SegmentationServiceTypes.Feature Foreground, SegmentationServiceTypes.Feature Background) ClickFeatures(
            IReadOnlyList<Geometry.Vector2>? foregroundPoints, IReadOnlyList<Geometry.Vector2>? backgroundPoints, int downsample)
        {
            SegmentationServiceTypes.Feature foreground = new();
            foreach (Geometry.Vector2 point in foregroundPoints ?? [])
                foreground.Points.Add(SegmentationPromptShapes.ToGrid(point, downsample));

            SegmentationServiceTypes.Feature background = new();
            foreach (Geometry.Vector2 point in backgroundPoints ?? [])
                background.Points.Add(SegmentationPromptShapes.ToGrid(point, downsample));

            return (foreground, background);
        }

        /// <summary>
        /// Adds to <paramref name="answer"/> the background shapes reaching <paramref name="needed"/>.background_cells that were
        /// not sent before (with the start or an earlier answer). Nothing is added without background cells or shapes.
        /// </summary>
        internal static void AddBackgroundAnswer(
            TilesAnswer answer,
            TilesNeeded needed,
            SegmentationBackgroundShapes? backgroundShapes,
            int downsample,
            ISet<int> backgroundSent)
        {
            if (backgroundShapes is null || needed.BackgroundCells.Count == 0)
                return;

            List<int> reaching = backgroundShapes.TakeReaching(
                needed.BackgroundCells.Select(cell => new TileCell(cell.Row, cell.Col)), downsample, backgroundSent);
            answer.BackgroundShapes.Add(backgroundShapes.ToShapes(reaching, downsample));
            SegmentationDiag.Log(
                $"SegmentTilesStream background cells={needed.BackgroundCells.Count} shapes={answer.BackgroundShapes.Count}");
        }

        /// <summary>
        /// Uploads the cells the server asked for and lists, in the answer, the ones it could not supply: a cell that cannot be
        /// captured is unavailable, which tells the server not to grow into it. An uploaded cell is not listed: the server sees
        /// its upload arrive. An <see cref="RpcException"/> from UploadTile is not caught: it ends the whole request.
        /// </summary>
        /// <param name="onTileReady">Called after each tile is uploaded, so the stream's idle timer restarts.</param>
        private async Task<TilesAnswer> UploadNeededTilesAsync(
            TileSignature signature,
            TilesNeeded needed,
            bool grayscale,
            CancellationToken token,
            Action? onTileReady = null)
        {
            TilesAnswer answer = await AnswerNeededTilesAsync(
                needed,
                tile =>
                {
                    // The server asks only for cells it does not hold. A key left from an earlier
                    // upload would skip the re-upload and the server would wait on a tile it never gets.
                    string key = TileKey(signature, tile.Row, tile.Col);
                    uploadedTileKeys.Remove(key);
                    prefetchedToServer.Remove(key);
                    return UploadMissingTilesAsync(signature, [new TileCell(tile.Row, tile.Col)], grayscale, token);
                },
                onTileReady).ConfigureAwait(false);

            SegmentationDiag.Log(
                $"SegmentTilesStream needed=[{string.Join(";", needed.Tiles.Select(t => $"{t.Row},{t.Col}"))}] " +
                $"unavailable=[{string.Join(";", answer.Unavailable.Select(t => $"{t.Row},{t.Col}"))}]");
            return answer;
        }

        /// <summary>
        /// Uploads each tile of <paramref name="needed"/> in order with <paramref name="upload"/> and returns the answer: only the
        /// tiles whose upload failed, as <c>unavailable</c>. A tile that was uploaded is settled by its upload arriving.
        /// </summary>
        internal static async Task<TilesAnswer> AnswerNeededTilesAsync(
            TilesNeeded needed,
            Func<GridCell, Task<bool>> upload,
            Action? onTileReady = null)
        {
            TilesAnswer answer = new();
            foreach (GridCell tile in needed.Tiles)
            {
                if (!await upload(tile).ConfigureAwait(false))
                    answer.Unavailable.Add(tile);
                onTileReady?.Invoke();
            }

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
            if (!WebAnnotation.Global.AnnotationSettings.SegmentationEdgeSnapEnabled ||
                ResponseWorldBounds(response, downsample) is not Geometry.Rectangle bounds)
                return;

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
                parent.Section.Number,
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
                _ = Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => renderTarget.Dispose()));
            }
        }
    }
}
