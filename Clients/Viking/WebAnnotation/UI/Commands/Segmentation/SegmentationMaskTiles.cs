#nullable enable

using Geometry;
using System;
using System.Collections.Generic;
using System.Configuration;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Mask prompts as grid tiles for <c>SegmentTilesStream</c>: the seed or correction as the tiles it touches, and the
    /// neighbouring annotations as the tiles the server asks for.
    /// </summary>
    /// <remarks>
    /// A tile is on the same grid as the image tiles (<see cref="SegmentationTileGrid"/>), named by the same
    /// <c>TileCoord</c>, and is <c>full</c> (every pixel in the mask, no PNG), a 1-bit PNG, or empty (no PNG, not full), which
    /// the server tells apart from a tile that was never sent. Nothing is merged across tiles, so there is no combined
    /// bounds to cut down, no PNG side to fit and no scale to raise: a region of any size is only more tiles. The PNG is full
    /// resolution (1024 square) unless <see cref="ConfiguredScale"/> asks for 4 (256 square blocks of 4 x 4 pixels).
    /// Servers that do not report <c>mask_tiles</c> get the single <c>Mask</c> of <see cref="SegmentationMaskUnion.ToMask"/> instead.
    /// </remarks>
    internal static class SegmentationMaskTiles
    {
        /// <summary>
        /// The most FOREGROUND tiles one request sends. The server's default limit; a larger seed is refused here with a
        /// message instead of ending the call with RESOURCE_EXHAUSTED.
        /// </summary>
        public const int MaxForegroundTiles = 256;

        /// <summary>Tiles per <c>MaskTiles</c> message, so one message stays well under gRPC's message size limit.</summary>
        public const int TilesPerMessage = 8;

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

        /// <summary>True when the server's status says it accepts mask tiles.</summary>
        public static bool IsSupported(SegmentationServiceTypes.ServerStatusResponse? status) => status?.MaskTiles == true;

        /// <summary>
        /// One tile of <paramref name="union"/>, or null when the tile is empty and <paramref name="sendEmpty"/> is false.
        /// A tile is <c>Full</c> when every pixel is set; otherwise a PNG at <paramref name="scale"/>; an empty tile is sent
        /// with no PNG and not full.
        /// </summary>
        /// <param name="coord">The tile; its downsample is the mosaic pixel size the union is rasterized at.</param>
        /// <param name="holePointsVolume">Background clicks that clear a disc of <paramref name="holeRadiusPixels"/> from a foreground tile.</param>
        /// <param name="scale">1 for a 1024 square PNG, <see cref="CoarseScale"/> for 256.</param>
        public static SegmentationServiceTypes.MaskTile? BuildTile(
            SegmentationMaskUnion union,
            SegmentationServiceTypes.TileCoord coord,
            SegmentationServiceTypes.MaskKind kind,
            IReadOnlyList<Vector2>? holePointsVolume,
            double holeRadiusPixels,
            int scale,
            bool sendEmpty)
        {
            byte[]? pixels = union.RasterizeTile(coord.Downsample, new TileCell(coord.Row, coord.Col), holePointsVolume, holeRadiusPixels);
            SegmentationServiceTypes.MaskTile tile = new() { Coord = coord, Kind = kind };
            if (pixels is null)
                return sendEmpty ? tile : null;

            if (Array.TrueForAll(pixels, value => value != 0))
            {
                tile.Full = true;
                return tile;
            }

            if (scale == CoarseScale)
            {
                tile.Png = Google.Protobuf.ByteString.CopyFrom(
                    SegmentationMaskUnion.EncodePng(Reduce(pixels), TileSize / CoarseScale, TileSize / CoarseScale));
                tile.Scale = CoarseScale;
            }
            else
            {
                tile.Png = Google.Protobuf.ByteString.CopyFrom(SegmentationMaskUnion.EncodePng(pixels, TileSize, TileSize));
            }

            return tile;
        }

        /// <summary>
        /// A tile reduced to 4 x 4 blocks: a block is set when any of its pixels is, so a coarse tile never loses mask.
        /// </summary>
        internal static byte[] Reduce(byte[] pixels)
        {
            const int Side = TileSize / CoarseScale;
            byte[] reduced = new byte[Side * Side];
            for (int row = 0; row < TileSize; row++)
            {
                int target = row / CoarseScale * Side;
                int source = row * TileSize;
                for (int column = 0; column < TileSize; column++)
                {
                    if (pixels[source + column] != 0)
                        reduced[target + column / CoarseScale] = 255;
                }
            }

            return reduced;
        }

        /// <summary>
        /// The FOREGROUND tiles of the seed or correction: every tile with something set, in row then column order, empty
        /// tiles left out (the count the request announces is the number returned).
        /// </summary>
        /// <param name="template">Names the volume, section, channel and transform of every tile; its row and column are replaced.</param>
        /// <exception cref="InvalidOperationException">The seed reaches more than <see cref="MaxForegroundTiles"/> tiles.</exception>
        public static List<SegmentationServiceTypes.MaskTile> BuildForeground(
            SegmentationMaskUnion seeds,
            SegmentationServiceTypes.TileCoord template,
            IReadOnlyList<Vector2>? backgroundPoints,
            double holeRadiusPixels,
            int scale)
        {
            List<SegmentationServiceTypes.MaskTile> tiles = [];
            foreach (TileCell cell in seeds.TilesTouched(template.Downsample))
            {
                SegmentationServiceTypes.MaskTile? tile = BuildTile(
                    seeds,
                    CoordOf(template, cell),
                    SegmentationServiceTypes.MaskKind.Foreground,
                    backgroundPoints,
                    holeRadiusPixels,
                    scale,
                    sendEmpty: false);
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
        /// The BACKGROUND tiles for <paramref name="coords"/>, every one of them, empty ones included: the server waits for an
        /// answer for each tile it asked about. <paramref name="neighbours"/> null means there is no background at all.
        /// </summary>
        public static List<SegmentationServiceTypes.MaskTile> BuildBackground(
            SegmentationMaskUnion? neighbours,
            IEnumerable<SegmentationServiceTypes.TileCoord> coords,
            int scale)
        {
            List<SegmentationServiceTypes.MaskTile> tiles = [];
            foreach (SegmentationServiceTypes.TileCoord coord in coords)
            {
                SegmentationServiceTypes.MaskTile tile = neighbours is null
                    ? new() { Coord = coord, Kind = SegmentationServiceTypes.MaskKind.Background }
                    : BuildTile(neighbours, coord, SegmentationServiceTypes.MaskKind.Background, null, 0, scale, sendEmpty: true)!;
                tiles.Add(tile);
            }

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

        private static SegmentationServiceTypes.TileCoord CoordOf(SegmentationServiceTypes.TileCoord template, TileCell cell)
        {
            SegmentationServiceTypes.TileCoord coord = template.Clone();
            coord.Row = cell.Row;
            coord.Col = cell.Col;
            return coord;
        }
    }
}
