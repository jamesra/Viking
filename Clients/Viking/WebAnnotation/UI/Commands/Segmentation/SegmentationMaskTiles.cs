#nullable enable

using Geometry;
using System;
using System.Collections.Generic;
using System.Configuration;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// A painted bitmap (an edited mask) as grid tiles for <c>SegmentTilesStream</c>: the FOREGROUND tiles it touches, all sent
    /// right after <c>start</c> and counted in its <c>mask_tile_count</c>.
    /// </summary>
    /// <remarks>
    /// A tile is on the same grid as the image tiles (<see cref="SegmentationTileGrid"/>), named by a <c>GridCell</c> of the
    /// request's <c>TileGrid</c>, and is <c>full</c> (every pixel set, no PNG), a 1-bit PNG, or empty (no PNG, not full). Nothing
    /// is merged across tiles, so a bitmap of any size is only more tiles. The PNG is full resolution (1024 square) unless
    /// <see cref="ConfiguredScale"/> asks for 4 (256 square blocks of 4 x 4 pixels). Geometry (circles, polygons, the
    /// neighbouring annotations) is never rasterized here: it goes as shapes (<see cref="SegmentationPromptShapes"/>).
    /// </remarks>
    internal static class SegmentationMaskTiles
    {
        /// <summary>
        /// The most mask tiles one request sends. The server's default limit; a larger bitmap is refused here with a
        /// message instead of ending the call with RESOURCE_EXHAUSTED.
        /// </summary>
        public const int MaxForegroundTiles = 256;

        /// <summary>Tiles per <c>MaskTiles</c> message, so one message stays well under gRPC's message size limit.</summary>
        public const int TilesPerMessage = 8;

        /// <summary>An edited mask whose longest side is more than this many mosaic pixels (one SAM2 window) is sent as coarse tiles.</summary>
        public const int LargeMaskSide = SegmentationMaskUnion.WindowSize;

        /// <summary>Mosaic pixels per PNG pixel when <see cref="ConfiguredScale"/> asks for coarse tiles.</summary>
        public const int CoarseScale = 4;

        private const int TileSize = SegmentationTileGrid.TileSize;

        /// <summary>
        /// Mosaic pixels per PNG pixel: 1 (default) or <see cref="CoarseScale"/>, from the <c>SegmentationMaskTileScale</c>
        /// app setting. Coarse tiles are four times smaller in each side and their edge is blocky, so they are for a slow link.
        /// </summary>
        public static int ConfiguredScale { get; } = ReadScale(ConfigurationManager.AppSettings["SegmentationMaskTileScale"]);

        internal static int ReadScale(string? setting) =>
            int.TryParse(setting, out int scale) && scale == CoarseScale ? CoarseScale : 1;

        /// <summary>
        /// How many grid tiles the volume-space rectangle reaches at <paramref name="downsample"/>: an upper bound on the
        /// FOREGROUND tiles a bitmap inside it sends, for refusing a mask the server would not accept before it is built.
        /// </summary>
        public static long TilesSpanned(Rectangle volumeBounds, int downsample)
        {
            // The right and top edges are exclusive: a rectangle that ends exactly on a tile boundary does not reach the next tile.
            double tileWorld = TileSize * (double)downsample;
            long firstColumn = (long)Math.Floor(volumeBounds.Left / tileWorld);
            long firstRow = (long)Math.Floor(volumeBounds.Bottom / tileWorld);
            long columns = Math.Max(firstColumn, (long)Math.Ceiling(volumeBounds.Right / tileWorld) - 1) - firstColumn + 1;
            long rows = Math.Max(firstRow, (long)Math.Ceiling(volumeBounds.Top / tileWorld) - 1) - firstRow + 1;
            return columns * rows;
        }

        /// <summary>
        /// The tile scale for a painted bitmap: <see cref="CoarseScale"/> (a 256 x 256 PNG per tile) when <see cref="ConfiguredScale"/>
        /// asks for it or the bitmap is larger than one SAM2 window, otherwise 1. An edit is made on a raster that was already
        /// downsampled to fit the screen's texture, so a large mask loses nothing it still had by going out at one pixel per 4 x 4 block;
        /// a mask within one window keeps full resolution.
        /// </summary>
        public static int ScaleFor(SegmentationMaskUnion? bitmap, int downsample) =>
            ConfiguredScale == CoarseScale || (bitmap is not null && bitmap.LargestBitmapSide(downsample) > LargeMaskSide)
                ? CoarseScale
                : 1;

        /// <summary>
        /// One tile of <paramref name="union"/>, or null when the tile is empty and <paramref name="sendEmpty"/> is false.
        /// A tile is <c>Full</c> when every pixel is set; otherwise a PNG at <paramref name="scale"/>; an empty tile is sent
        /// with no PNG and not full.
        /// </summary>
        /// <param name="downsample">The grid's downsample: the mosaic pixel size the union is rasterized at.</param>
        /// <param name="cell">The tile.</param>
        /// <param name="holePointsVolume">Background clicks that clear a disc of <paramref name="holeRadiusPixels"/> from a foreground tile.</param>
        /// <param name="scale">1 for a 1024 square PNG, <see cref="CoarseScale"/> for 256.</param>
        public static SegmentationServiceTypes.MaskTile? BuildTile(
            SegmentationMaskUnion union,
            int downsample,
            TileCell cell,
            SegmentationServiceTypes.MaskKind kind,
            IReadOnlyList<Vector2>? holePointsVolume,
            double holeRadiusPixels,
            int scale,
            bool sendEmpty)
        {
            int tileScale = scale == CoarseScale ? CoarseScale : 1;
            byte[]? pixels = union.RasterizeTile(downsample, cell, holePointsVolume, holeRadiusPixels, tileScale);
            SegmentationServiceTypes.MaskTile tile = new()
            {
                Cell = new GridCell { Row = cell.Row, Col = cell.Col },
                Kind = kind
            };
            if (pixels is null)
                return sendEmpty ? tile : null;

            if (Array.TrueForAll(pixels, value => value != 0))
            {
                tile.Full = true;
                return tile;
            }

            int side = TileSize / tileScale;
            tile.Png = Google.Protobuf.ByteString.CopyFrom(SegmentationMaskUnion.EncodePng(pixels, side, side));
            if (tileScale == CoarseScale)
                tile.Scale = CoarseScale;

            return tile;
        }

        /// <summary>
        /// The tiles of a painted bitmap of <paramref name="kind"/>: every tile with something set, in row then column order,
        /// empty tiles left out (each one sent counts in the request's <c>mask_tile_count</c>).
        /// </summary>
        /// <exception cref="InvalidOperationException">The bitmap reaches more than <see cref="MaxForegroundTiles"/> tiles.</exception>
        public static List<SegmentationServiceTypes.MaskTile> BuildBitmap(
            SegmentationMaskUnion bitmap,
            int downsample,
            SegmentationServiceTypes.MaskKind kind,
            IReadOnlyList<Vector2>? holePoints,
            double holeRadiusPixels,
            int scale)
        {
            List<SegmentationServiceTypes.MaskTile> tiles = [];
            foreach (TileCell cell in bitmap.TilesTouched(downsample))
            {
                SegmentationServiceTypes.MaskTile? tile = BuildTile(
                    bitmap, downsample, cell, kind, holePoints, holeRadiusPixels, scale, sendEmpty: false);
                if (tile is null)
                    continue;

                if (tiles.Count >= MaxForegroundTiles)
                {
                    throw new InvalidOperationException(
                        $"The mask to segment from spans more than {MaxForegroundTiles} tiles, which is as many as the segmentation server accepts. Zoom out or segment a smaller area.");
                }

                tiles.Add(tile);
            }

            return tiles;
        }

        /// <summary>
        /// The tiles of the painted bitmaps, FOREGROUND (<paramref name="foreground"/>) then BACKGROUND (<paramref name="background"/>),
        /// either null or empty for a request without one, and the request's <c>mask_tile_count</c> set to their number: they are
        /// all written right after <c>start</c>. Each side's scale is <paramref name="scale"/> unless its bitmap needs coarse tiles
        /// (<see cref="ScaleFor"/>).
        /// </summary>
        /// <param name="request">The start request; its count is set here.</param>
        /// <param name="backgroundPoints">Background clicks; each clears a disc of <paramref name="holeRadiusPixels"/> from the foreground.</param>
        /// <exception cref="InvalidOperationException">The bitmaps reach more than <see cref="MaxForegroundTiles"/> tiles together.</exception>
        public static List<SegmentationServiceTypes.MaskTile> PrepareStart(
            SegmentationServiceTypes.SegmentTilesRequest request,
            SegmentationMaskUnion? foreground,
            SegmentationMaskUnion? background,
            int downsample,
            IReadOnlyList<Vector2>? backgroundPoints,
            double holeRadiusPixels,
            int scale)
        {
            List<SegmentationServiceTypes.MaskTile> tiles = [];
            if (foreground is { IsEmpty: false })
            {
                tiles.AddRange(BuildBitmap(
                    foreground, downsample, SegmentationServiceTypes.MaskKind.Foreground, backgroundPoints, holeRadiusPixels,
                    Math.Max(scale, ScaleFor(foreground, downsample))));
            }

            if (background is { IsEmpty: false })
            {
                tiles.AddRange(BuildBitmap(
                    background, downsample, SegmentationServiceTypes.MaskKind.Background, null, 0,
                    Math.Max(scale, ScaleFor(background, downsample))));
            }

            if (tiles.Count > MaxForegroundTiles)
            {
                throw new InvalidOperationException(
                    $"The painted masks span more than {MaxForegroundTiles} tiles, which is as many as the segmentation server accepts. Zoom out or paint a smaller area.");
            }

            request.MaskTileCount = (uint)tiles.Count;
            return tiles;
        }

        /// <summary>The tiles split into <c>MaskTiles</c> messages of at most <see cref="TilesPerMessage"/>.</summary>
        public static IEnumerable<SegmentationServiceTypes.MaskTiles> Chunk(IReadOnlyList<SegmentationServiceTypes.MaskTile> tiles)
        {
            for (int start = 0; start < tiles.Count; start += TilesPerMessage)
            {
                SegmentationServiceTypes.MaskTiles message = new();
                for (int i = start; i < Math.Min(tiles.Count, start + TilesPerMessage); i++)
                    message.Tiles.Add(tiles[i]);

                yield return message;
            }
        }
    }
}
