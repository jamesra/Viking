#nullable enable

using Geometry;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

        /// <summary>
        /// How many grid tiles the volume-space rectangle reaches at <paramref name="downsample"/>: an upper bound on the
        /// FOREGROUND tiles a shape inside it sends, for refusing a mask the server would not accept before it is built.
        /// </summary>
        public static long TilesSpanned(Rectangle volumeBounds, int downsample)
        {
            double tileWorld = TileSize * (double)downsample;
            long columns = (long)Math.Floor(volumeBounds.Right / tileWorld) - (long)Math.Floor(volumeBounds.Left / tileWorld) + 1;
            long rows = (long)Math.Floor(volumeBounds.Top / tileWorld) - (long)Math.Floor(volumeBounds.Bottom / tileWorld) + 1;
            return columns * rows;
        }

        /// <summary>
        /// The tile scale for the seeds: <see cref="CoarseScale"/> (a 256 x 256 PNG per tile) when <see cref="ConfiguredScale"/> asks for it
        /// or the seeds hold an edited mask larger than one SAM2 window, otherwise 1. An edit is made on a raster that was already
        /// downsampled to fit the screen's texture, so a large mask loses nothing it still had by going out at one pixel per 4 x 4 block;
        /// a mask within one window keeps full resolution.
        /// </summary>
        public static int ScaleFor(SegmentationMaskUnion? seeds, int downsample) =>
            ConfiguredScale == CoarseScale || (seeds is not null && seeds.LargestBitmapSide(downsample) > MaskScalePolicy.SingleWindowSide)
                ? CoarseScale
                : 1;

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
            int tileScale = scale == CoarseScale ? CoarseScale : 1;
            byte[]? pixels = union.RasterizeTile(
                coord.Downsample, new TileCell(coord.Row, coord.Col), holePointsVolume, holeRadiusPixels, tileScale);
            SegmentationServiceTypes.MaskTile tile = new() { Coord = coord, Kind = kind };
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

        /// <summary>The mask tiles a tiled request writes right after <c>start</c>.</summary>
        public readonly struct StartTiles
        {
            public StartTiles(List<SegmentationServiceTypes.MaskTile> foreground, List<SegmentationServiceTypes.MaskTile> background)
            {
                Foreground = foreground;
                Background = background;
            }

            /// <summary>The FOREGROUND tiles, the count the start announces. Empty for a click-only request.</summary>
            public List<SegmentationServiceTypes.MaskTile> Foreground { get; }

            /// <summary>The BACKGROUND tiles of the start windows, one for every image tile of the start, empty ones included.</summary>
            public List<SegmentationServiceTypes.MaskTile> Background { get; }

            /// <summary>The stream messages to write after <c>start</c>, foreground first: the server waits for every announced foreground tile.</summary>
            public IEnumerable<SegmentationServiceTypes.SegmentTilesStreamRequest> Messages() =>
                Chunk(Foreground).Concat(Chunk(Background)).Select(tiles => new SegmentationServiceTypes.SegmentTilesStreamRequest { MaskTiles = tiles });
        }

        /// <summary>
        /// What a request on the tiled path sends with <c>start</c>: sets <paramref name="request"/>'s
        /// <c>foreground_mask_tile_count</c> to the number of FOREGROUND tiles, <b>including 0</b> for a click-only request with
        /// no seed mask (the count's presence is what tells the server this client sends mask tiles, so it asks for the background), and
        /// builds the BACKGROUND tiles of the start windows (<paramref name="request"/>.Tiles) so the server need not ask for them.
        /// </summary>
        /// <param name="request">The start request, with its image tiles already listed; its count is set here.</param>
        /// <param name="seeds">The seed or correction shapes, or null for a click-only request.</param>
        /// <param name="template">Names the volume, section, channel, transform and downsample of every tile.</param>
        /// <param name="neighbours">The neighbouring annotations, or null when there are none; every start tile still gets a (possibly empty) tile.</param>
        /// <param name="scale">The scale of the BACKGROUND tiles (and of the FOREGROUND ones unless <paramref name="foregroundScale"/> is given).</param>
        /// <param name="foregroundScale">The scale of the FOREGROUND tiles, when it differs: see <see cref="ScaleFor"/>.</param>
        public static StartTiles PrepareStart(
            SegmentationServiceTypes.SegmentTilesRequest request,
            SegmentationMaskUnion? seeds,
            SegmentationServiceTypes.TileCoord template,
            IReadOnlyList<Vector2>? backgroundPoints,
            double holeRadiusPixels,
            SegmentationMaskUnion? neighbours,
            int scale,
            int? foregroundScale = null)
        {
            List<SegmentationServiceTypes.MaskTile> foreground = seeds is null
                ? []
                : BuildForeground(seeds, template, backgroundPoints, holeRadiusPixels, foregroundScale ?? scale);
            request.ForegroundMaskTileCount = (uint)foreground.Count;
            return new StartTiles(foreground, BuildBackground(neighbours, request.Tiles, scale));
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
