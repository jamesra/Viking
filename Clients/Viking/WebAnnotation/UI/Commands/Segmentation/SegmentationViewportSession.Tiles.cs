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
        /// Reads the live view. <paramref name="fixedDownsample"/> pins the tile pyramid level so a
        /// multi-round request keeps one level even if the camera zoom changes between rounds.
        /// </summary>
        private async Task<(int downsample, TileSignature signature, List<TileCell> visible, bool grayscale)> ReadViewTilesAsync(
            int? fixedDownsample = null)
        {
            return await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
            {
                int downsample = fixedDownsample ?? CurrentPyramidDownsample();
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
        /// SegmentTiles for <paramref name="cells"/> only. Other keys in the session stay
        /// out of the request so an earlier viewport upload cannot overflow the server cache.
        /// </summary>
        private async Task<SegmentationResponse> SegmentUploadedTilesAsync(
            TileSignature signature,
            IReadOnlyList<TileCell> cells,
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            CancellationToken token)
        {
            if (grpcClient is null)
                throw new InvalidOperationException("Segmentation client is not connected.");

            SegmentTilesRequest request = new()
            {
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

            if (backgroundPoints is not null)
            {
                foreach (Geometry.Vector2 point in backgroundPoints)
                {
                    (int x, int y) = SegmentationTileGrid.WorldToMosaicPixel(point.X, point.Y, signature.Downsample);
                    request.Background.Add(new SegmentationServiceTypes.Point { X = x, Y = y });
                }
            }


            CallOptions callOptions = new(deadline: DateTime.UtcNow.AddSeconds(60), cancellationToken: token);
            SegmentationResponse response =
                await grpcClient.SegmentTilesAsync(request, callOptions).ResponseAsync.ConfigureAwait(false);
            string submittedTiles = string.Join(
                ";",
                request.Tiles.Select(tile => $"{tile.Row},{tile.Col}"));
            string requestedTiles = string.Join(
                ";",
                response.RequestedTiles.Select(tile => $"{tile.Row},{tile.Col}"));
            string segmentSizes = string.Join(
                ",",
                response.Segments.Select(segment => $"{segment.X},{segment.Y}:{GetPngDimensions(segment.Mask.ToByteArray())}"));
            SegmentationDiag.Log(
                $"SegmentTiles response mosaic={response.Width}x{response.Height} " +
                $"origin=({response.OriginX},{response.OriginY}) submittedTiles=[{submittedTiles}] " +
                $"requestedTiles=[{requestedTiles}] " +
                $"segments=[{segmentSizes}]");
            return response;
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
                    byte[] pngData = SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, grayscale);
                    SegmentationCaptureEncoder.SaveCaptureForReview(pngData, width, height);
                    var (isValid, errorMessage) = ValidateCapturedImage(pngData, width, height);
                    SegmentationDiag.Log(
                        $"CaptureTile encoded row={cell.Row} col={cell.Col} " +
                        $"png={GetPngDimensions(pngData)} bytes={pngData.Length} valid={isValid}");
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
        /// Reads PNG IHDR dimensions for diagnostics without decoding pixel data.
        /// </summary>
        private static string GetPngDimensions(byte[] pngData)
        {
            if (pngData is null || pngData.Length < 24 ||
                pngData[0] != 0x89 || pngData[1] != 0x50 ||
                pngData[2] != 0x4E || pngData[3] != 0x47)
            {
                return "invalid";
            }

            int width =
                (pngData[16] << 24) |
                (pngData[17] << 16) |
                (pngData[18] << 8) |
                pngData[19];
            int height =
                (pngData[20] << 24) |
                (pngData[21] << 16) |
                (pngData[22] << 8) |
                pngData[23];
            return $"{width}x{height}";
        }

        /// <summary>
        /// World rectangle covering the fused mosaic for mask-to-polygon mapping.
        /// </summary>
        internal Geometry.Rectangle MosaicWorldBounds(SegmentationResponse response)
        {
            int downsample = Math.Max(1, mosaicDownsample);
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
