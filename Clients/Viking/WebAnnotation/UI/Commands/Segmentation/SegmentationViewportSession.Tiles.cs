using Geometry;
using Grpc.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly struct TileSignature
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
                int downsample = CurrentPyramidDownsample();
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

        /// <summary>
        /// Pyramid level for UploadTile/SegmentTiles from the live camera via
        /// <see cref="ResolveTileDownsample"/>.
        /// </summary>
        private int CurrentPyramidDownsample()
            => ResolveTileDownsample(parent.Camera?.Downsample ?? parent.Downsample);

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

                var (png, width, height) = await CaptureTileImage(cell, signature.Downsample, grayscale, token).ConfigureAwait(false);
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
            IReadOnlyList<Geometry.Rectangle>? foregroundBoxes = null,
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
                UseMaskInput = WebAnnotation.Global.AnnotationSettings.SegmentationUseMaskInput
            };
            foreach (TileCell cell in cells)
            {
                if (!uploadedTileKeys.Contains(TileKey(signature, cell.Row, cell.Col)))
                    continue;

                request.Tiles.Add(ToCoord(signature, cell));
            }

            if (request.Tiles.Count == 0)
                throw new InvalidOperationException("No uploaded tiles cover the segmentation prompts.");

            if (foregroundPoints is not null)
            {
                foreach (Geometry.Vector2 point in foregroundPoints)
                {
                    (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(point.X, point.Y, signature.Downsample);
                    request.Foreground.Add(new SegmentationServiceTypes.Point { X = x, Y = y });
                }
            }

            if (foregroundBoxes is not null)
            {
                foreach (Geometry.Rectangle box in foregroundBoxes)
                {
                    (int xA, int yA) = SegmentationTileGrid.WorldToMosaicPixel(box.Left, box.Bottom, signature.Downsample);
                    (int xB, int yB) = SegmentationTileGrid.WorldToMosaicPixel(box.Right, box.Top, signature.Downsample);
                    request.ForegroundBoxes.Add(new SegmentationServiceTypes.BoundingBox
                    {
                        XMin = Math.Min(xA, xB),
                        YMin = Math.Min(yA, yB),
                        XMax = Math.Max(xA, xB),
                        YMax = Math.Max(yA, yB)
                    });
                }
            }

            if (backgroundPoints is not null)
            {
                foreach (Geometry.Vector2 point in backgroundPoints)
                {
                    (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(point.X, point.Y, signature.Downsample);
                    request.Background.Add(new SegmentationServiceTypes.Point { X = x, Y = y });
                }
            }


            CallOptions callOptions = new(
                deadline: DateTime.UtcNow.AddSeconds(SegmentStreamDeadlineSeconds),
                cancellationToken: token);
            SegmentationResponse? response = null;
            List<string> requestedTileList = [];
            using (AsyncDuplexStreamingCall<SegmentTilesStreamRequest, SegmentTilesStreamResponse> call =
                grpcClient.SegmentTilesStream(callOptions))
            {
                await call.RequestStream.WriteAsync(new SegmentTilesStreamRequest { Start = request }).ConfigureAwait(false);
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
                response.Segments.Select(segment => $"{segment.X},{segment.Y}:{CapturedPng.DescribeDimensions(segment.Mask.ToByteArray())}"));
            SegmentationDiag.Log(
                $"SegmentTiles response req={requestId}{(response.RequestId == requestId ? string.Empty : $" MISMATCH echoed={response.RequestId}")} mosaic={response.Width}x{response.Height} " +
                $"origin=({response.OriginX},{response.OriginY}) submittedTiles=[{submittedTiles}] " +
                $"requestedTiles=[{requestedTiles}] " +
                $"segments=[{segmentSizes}]");
            return response;
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

        private static bool TryParseMissingTile(string detail, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (string.IsNullOrEmpty(detail))
                return false;

            Match match = Regex.Match(detail, @"row=(-?\d+)\s+col=(-?\d+)");
            if (!match.Success)
                return false;

            row = int.Parse(match.Groups[1].Value);
            col = int.Parse(match.Groups[2].Value);
            return true;
        }

        private async Task<(byte[]? data, int width, int height)> CaptureTileImage(
            TileCell cell,
            int downsample,
            bool grayscale,
            CancellationToken cancellationToken)
        {
            try
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
                    parent.Section.Number,
                    showOverlays: false,
                    asyncTextureLoad: false,
                    renderToken).ConfigureAwait(false);
                if (renderTarget is null)
                    return (null, 0, 0);

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
                    byte[] pngData = await Task.Run(
                        () => SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, grayscale),
                        cancellationToken).ConfigureAwait(false);
                    SegmentationCaptureEncoder.SaveCaptureForReview(pngData, width, height);
                    var (isValid, errorMessage) = CapturedPng.Validate(pngData, width, height);
                    SegmentationDiag.Log(
                        $"CaptureTile encoded row={cell.Row} col={cell.Col} " +
                        $"png={CapturedPng.DescribeDimensions(pngData)} bytes={pngData.Length} valid={isValid}");
                    if (!isValid)
                    {
                        Debug.WriteLine($"Tile capture failed validation: {errorMessage}");
                        return (null, 0, 0);
                    }

                    return (pngData, width, height);
                }
                finally
                {
                    Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(() => renderTarget.Dispose()));
                }
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
        /// World rectangle of the mosaic for a response sent at <paramref name="requestedDownsample"/>.
        /// </summary>
        internal static Geometry.Rectangle MosaicWorldBounds(SegmentationResponse response, int requestedDownsample)
        {
            int downsample = Math.Max(1, requestedDownsample);
            int width = Math.Max(1, response.Width);
            int height = Math.Max(1, response.Height);
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
