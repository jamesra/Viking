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
using Viking.VolumeModel;
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

        private int CurrentPyramidDownsample()
        {
            double requested = parent.Downsample;
            try
            {
                MappingBase mapping = parent.Section?.VolumeViewModel?.GetTileMapping(
                    parent.Section.Number,
                    parent.CurrentChannel,
                    parent.CurrentTransform);
                if (mapping is not null)
                {
                    int level = mapping.NearestAvailableLevel(requested);
                    if (level > 0 && level != int.MaxValue)
                        return level;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pyramid downsample lookup failed: {ex.Message}");
            }

            int fallback = (int)Math.Round(requested);
            return Math.Max(1, fallback);
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
                Debug.WriteLine(
                    $"UploadTile key=vol={signature.Volume}|sec={signature.Section}|ch={signature.Channel}|" +
                    $"xf={signature.Transform}|ds={signature.Downsample}|row={cell.Row}|col={cell.Col} bytes={png.Length}");
                UploadTileResponse response = await grpcClient.UploadTileAsync(upload, callOptions).ResponseAsync.ConfigureAwait(false);
                uploadedTileKeys.Add(key);
                anyReady = true;
                Debug.WriteLine(
                    $"UploadTile accepted key=ds={signature.Downsample}|row={cell.Row}|col={cell.Col} " +
                    $"alreadyCached={response.AlreadyCached}");
            }

            return anyReady;
        }

        private async Task<SegmentationResponse> SegmentUploadedTilesAsync(
            TileSignature signature,
            IReadOnlyList<Geometry.Vector2> foregroundPoints,
            IReadOnlyList<Geometry.Vector2> backgroundPoints,
            CancellationToken token)
        {
            if (grpcClient is null)
                throw new InvalidOperationException("Segmentation client is not connected.");

            SegmentTilesRequest request = new()
            {
                MultimaskOutput = false,
                OmitLabeledImage = true
            };
            string prefix = $"{signature.Volume}\n{signature.Section}\n{signature.Channel}\n{signature.Transform}\n{signature.Downsample}\n";
            foreach (string key in uploadedTileKeys)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                string[] parts = key.Split('\n');
                if (parts.Length < 7)
                    continue;

                request.Tiles.Add(new TileCoord
                {
                    Volume = signature.Volume,
                    Section = signature.Section,
                    Channel = signature.Channel,
                    Transform = signature.Transform,
                    Downsample = signature.Downsample,
                    Row = int.Parse(parts[5]),
                    Col = int.Parse(parts[6])
                });
            }

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
            return await grpcClient.SegmentTilesAsync(request, callOptions).ResponseAsync.ConfigureAwait(false);
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
                Camera camera = new() { Downsample = downsample };
                VikingXNA.Scene tileScene = new(
                    new Viewport(0, 0, SegmentationTileGrid.TileSize, SegmentationTileGrid.TileSize),
                    camera);
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
                    Color[] pixels = await Viking.UI.State.MainThreadDispatcher.InvokeAsync(() =>
                    {
                        Color[] buffer = new Color[width * height];
                        renderTarget.GetData(buffer);
                        return buffer;
                    }).Task.ConfigureAwait(false);
                    byte[] pngData = SegmentationCaptureEncoder.EncodeToPng(pixels, width, height, grayscale);
                    var (isValid, errorMessage) = ValidateCapturedImage(pngData, width, height);
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
