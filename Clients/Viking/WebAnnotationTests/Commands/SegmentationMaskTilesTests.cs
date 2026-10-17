using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;
using MaskKind = Viking.gRPC.SegmentationServiceTypes.V1.MaskKind;
using MaskTile = Viking.gRPC.SegmentationServiceTypes.V1.MaskTile;
using TileCoord = Viking.gRPC.SegmentationServiceTypes.V1.TileCoord;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Mask prompts as grid tiles: what the client sends for a seed of any size and for the neighbours the server asks about.
    /// A tile is full, a 1-bit PNG (Y down like the image tile) or empty, and the tiles of one mask rebuild exactly the mask.
    /// </summary>
    [TestClass]
    public class SegmentationMaskTilesTests
    {
        private const int Tile = 1024;

        private static readonly TileCoord Template = new() { Volume = "vol", Section = 7, Channel = "c", Transform = "t", Downsample = 1 };

        private static Rectangle Box(double x0, double y0, double x1, double y1) => new(new Vector2(x0, y0), new Vector2(x1, y1));

        private static SegmentationMaskUnion UnionOf(params Rectangle[] rectangles)
        {
            SegmentationMaskUnion union = new();
            foreach (Rectangle rectangle in rectangles)
                union.Add(rectangle);

            return union;
        }

        private static TileCoord Coord(int row, int col, int downsample = 1)
        {
            TileCoord coord = Template.Clone();
            coord.Row = row;
            coord.Col = col;
            coord.Downsample = downsample;
            return coord;
        }

        private static bool Inside(Rectangle rectangle, int x, int y)
            => x + 0.5 >= rectangle.Left && x + 0.5 <= rectangle.Right && y + 0.5 >= rectangle.Bottom && y + 0.5 <= rectangle.Top;

        /// <summary>The pixels of one tile as the server reads it: row 0 is the tile's highest mosaic y, and a scale-4 PNG is expanded by 4 x 4 blocks.</summary>
        private static bool[] Decode(MaskTile tile)
        {
            bool[] pixels = new bool[Tile * Tile];
            if (tile.Full)
            {
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = true;
                return pixels;
            }

            if (tile.Png.IsEmpty)
                return pixels;

            using Image<L8> png = Image.Load<L8>(tile.Png.ToByteArray());
            int scale = tile.Scale > 1 ? (int)tile.Scale : 1;
            Assert.AreEqual(Tile / scale, png.Width);
            Assert.AreEqual(Tile / scale, png.Height);
            for (int row = 0; row < Tile; row++)
                for (int column = 0; column < Tile; column++)
                    pixels[row * Tile + column] = png[column / scale, row / scale].PackedValue != 0;

            return pixels;
        }

        /// <summary>Mosaic pixel (x, y) of the tiles as one mask; false for a tile not sent.</summary>
        private static Func<int, int, bool> Rebuild(IEnumerable<MaskTile> tiles)
        {
            Dictionary<(int Row, int Col), bool[]> decoded = tiles.ToDictionary(tile => (tile.Coord.Row, tile.Coord.Col), Decode);
            return (x, y) =>
            {
                int col = (int)Math.Floor(x / (double)Tile), row = (int)Math.Floor(y / (double)Tile);
                return decoded.TryGetValue((row, col), out bool[]? pixels) && pixels[(row * Tile + Tile - 1 - y) * Tile + (x - col * Tile)];
            };
        }

        [TestMethod]
        public void ASeedCoveringThreeByThreeTilesIsNineTilesThatRebuildTheExactShape()
        {
            Rectangle region = Box(500, 500, 2900, 2900);

            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(UnionOf(region), Template, null, 0, 1);
            Func<int, int, bool> on = Rebuild(tiles);

            Assert.AreEqual(9, tiles.Count);
            Assert.IsTrue(tiles.All(tile => tile.Kind == MaskKind.Foreground));
            for (int y = 0; y < 3 * Tile; y += 1)
                for (int x = 0; x < 3 * Tile; x += 1)
                    if (on(x, y) != Inside(region, x, y))
                        Assert.Fail($"pixel ({x}, {y}) is {on(x, y)} in the tiles but {Inside(region, x, y)} in the shape");
        }

        [TestMethod]
        public void ATileTheShapeFillsIsFullAndAnEdgeTileIsAPngAtFullResolution()
        {
            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(UnionOf(Box(500, 500, 2900, 2900)), Template, null, 0, 1);

            MaskTile middle = tiles.Single(tile => tile.Coord.Row == 1 && tile.Coord.Col == 1);
            Assert.IsTrue(middle.Full);
            Assert.IsTrue(middle.Png.IsEmpty, "a full tile has no PNG");

            MaskTile corner = tiles.Single(tile => tile.Coord.Row == 0 && tile.Coord.Col == 0);
            Assert.IsFalse(corner.Full);
            Assert.IsFalse(corner.Png.IsEmpty);
            Assert.AreEqual(0u, corner.Scale, "scale is unset at full resolution");
            using Image<L8> png = Image.Load<L8>(corner.Png.ToByteArray());
            Assert.AreEqual(Tile, png.Width);
            Assert.AreEqual(Tile, png.Height);
            Assert.AreEqual(1, Image.Identify(corner.Png.ToByteArray()).PixelType.BitsPerPixel);
        }

        [TestMethod]
        public void PngRowZeroIsTheTilesHighestY()
        {
            // A strip across the top 10 mosaic rows of tile (0, 0): y 1014..1023.
            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(UnionOf(Box(100, 1014, 400, 1024)), Template, null, 0, 1);
            MaskTile tile = tiles.Single();
            using Image<L8> png = Image.Load<L8>(tile.Png.ToByteArray());

            Assert.AreEqual(255, png[200, 0].PackedValue, "row 0 is y = 1023");
            Assert.AreEqual(255, png[200, 9].PackedValue, "row 9 is y = 1014");
            Assert.AreEqual(0, png[200, 10].PackedValue, "row 10 is below the strip");
        }

        [TestMethod]
        public void AnEmptyForegroundTileIsLeftOutButAnEmptyBackgroundTileIsSentEmpty()
        {
            // The triangle's bounding box reaches tiles (2, 2), (1, 2) and (2, 1); the triangle does not.
            SegmentationMaskUnion union = new();
            union.Add([[new Vector2(0, 0), new Vector2(3000, 0), new Vector2(0, 3000)]]);

            List<MaskTile> foreground = SegmentationMaskTiles.BuildForeground(union, Template, null, 0, 1);
            Assert.AreEqual(6, foreground.Count, "the three tiles past the hypotenuse are not announced or sent");
            Assert.IsFalse(foreground.Any(tile => (tile.Coord.Row, tile.Coord.Col) is (2, 2) or (1, 2) or (2, 1)));

            List<MaskTile> background = SegmentationMaskTiles.BuildBackground(union, [Coord(2, 2)], 1);
            MaskTile empty = background.Single();
            Assert.AreEqual(MaskKind.Background, empty.Kind);
            Assert.IsFalse(empty.Full);
            Assert.IsTrue(empty.Png.IsEmpty, "empty is sent with no PNG, so it differs from a tile never sent");
        }

        [TestMethod]
        public void EveryAskedBackgroundTileIsAnsweredIncludingThoseWithNoNeighbourAtAll()
        {
            TileCoord[] asked = [Coord(0, 0), Coord(5, 5), Coord(-3, 9)];

            List<MaskTile> none = SegmentationMaskTiles.BuildBackground(null, asked, 1);
            List<MaskTile> some = SegmentationMaskTiles.BuildBackground(UnionOf(Box(0, 0, 100, 100)), asked, 1);

            Assert.AreEqual(3, none.Count);
            Assert.AreEqual(3, some.Count);
            Assert.IsTrue(none.All(tile => !tile.Full && tile.Png.IsEmpty && tile.Kind == MaskKind.Background));
            Assert.IsFalse(some[0].Png.IsEmpty, "tile (0, 0) holds the neighbour");
            Assert.IsTrue(some[1].Png.IsEmpty && some[2].Png.IsEmpty);
        }

        [TestMethod]
        public void ANeighbourSpanningTwentyTilesCostsOnlyTheTilesAskedAbout()
        {
            SegmentationMaskUnion union = new();
            union.Add(Box(0, 0, 20 * Tile - 1, 900));
            for (int i = 0; i < 300; i++)
                union.Add(Box(50 * Tile + i * 40, 7 * Tile, 50 * Tile + i * 40 + 30, 7 * Tile + 30));

            Assert.AreEqual(20, Enumerable.Range(0, 60).Count(col => union.ShapesReaching(1, new TileCell(0, col)) > 0 && col < 20));
            Assert.AreEqual(1, union.ShapesReaching(1, new TileCell(0, 13)), "one shape reaches tile (0, 13)");
            Assert.AreEqual(0, union.ShapesReaching(1, new TileCell(3, 13)), "none reaches a tile above it");

            List<MaskTile> tile = SegmentationMaskTiles.BuildBackground(union, [Coord(0, 13)], 1);
            Assert.IsFalse(tile.Single().Png.IsEmpty);
            Assert.IsTrue(SegmentationMaskTiles.BuildBackground(union, [Coord(3, 13)], 1).Single().Png.IsEmpty);
        }

        [TestMethod]
        public void CoarseTilesAreA256SquareAndCoverEveryPixelOfTheFullResolutionTile()
        {
            Rectangle region = Box(503, 501, 2899, 2903);
            SegmentationMaskUnion union = UnionOf(region);

            List<MaskTile> fine = SegmentationMaskTiles.BuildForeground(union, Template, null, 0, 1);
            List<MaskTile> coarse = SegmentationMaskTiles.BuildForeground(union, Template, null, 0, SegmentationMaskTiles.CoarseScale);

            Assert.AreEqual(fine.Count, coarse.Count);
            Assert.IsTrue(coarse.Where(tile => !tile.Full).All(tile => tile.Scale == 4));
            using (Image<L8> png = Image.Load<L8>(coarse.First(tile => !tile.Full).Png.ToByteArray()))
            {
                Assert.AreEqual(256, png.Width);
                Assert.AreEqual(256, png.Height);
            }

            Func<int, int, bool> exact = Rebuild(fine);
            Func<int, int, bool> blocks = Rebuild(coarse);
            int extra = 0;
            for (int y = 0; y < 3 * Tile; y++)
            {
                for (int x = 0; x < 3 * Tile; x++)
                {
                    if (exact(x, y) && !blocks(x, y))
                        Assert.Fail($"({x}, {y}) is in the shape but not in the coarse tiles");
                    if (blocks(x, y) && !exact(x, y))
                    {
                        extra++;
                        bool nearX = x + 4 > region.Left && x - 4 < region.Right;
                        bool nearY = y + 4 > region.Bottom && y - 4 < region.Top;
                        if (!nearX || !nearY)
                            Assert.Fail($"({x}, {y}) is more than one 4-pixel block from the shape");
                    }
                }
            }

            Assert.IsTrue(extra < 4 * 2 * (region.Width + region.Height), "only block edges differ");
        }

        [TestMethod]
        public void ABackgroundClickClearsADiscFromTheForegroundTile()
        {
            SegmentationMaskUnion union = UnionOf(Box(0, 0, 1000, 1000));

            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(union, Template, [new Vector2(300, 300)], 10, 1);
            Func<int, int, bool> on = Rebuild(tiles);

            Assert.IsFalse(on(300, 300), "the background click is not overruled by the seed");
            Assert.IsFalse(on(305, 300));
            Assert.IsTrue(on(330, 300), "beyond the click's radius");
        }

        [TestMethod]
        public void ASeedBeyondTheServersTileLimitIsRefusedWithAMessage()
        {
            // 17 x 16 tiles of full mask: more than the 256 the server accepts.
            SegmentationMaskUnion union = UnionOf(Box(0, 0, 17 * Tile, 16 * Tile));

            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(
                () => SegmentationMaskTiles.BuildForeground(union, Template, null, 0, 1));

            StringAssert.Contains(error.Message, "256");
        }

        [TestMethod]
        public void ASomaSizedCorrectionOfFourThousandPixelsIsAFiveByFiveBlockOfTiles()
        {
            Rectangle soma = Box(300, 300, 4300, 4300);

            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(UnionOf(soma), Template, null, 0, 1);

            Assert.AreEqual(25, tiles.Count, "4000 pixels from 300 reach a 5 x 5 block of tiles");
            Assert.AreEqual(9, tiles.Count(tile => tile.Full), "the 3 x 3 tiles inside the soma are full");
            Func<int, int, bool> on = Rebuild(tiles);
            Assert.IsTrue(on(2000, 2000));
            Assert.IsFalse(on(100, 100));
        }

        [TestMethod]
        public void AnEditedMaskIsSentAsTilesWithoutBeingTraced()
        {
            Vector2[] ring = [new(0, 0), new(3000, 0), new(3000, 1500), new(0, 1500)];
            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128)!;
            SegmentationMaskUnion union = new();
            union.Add(SegmentationSeed.FromMask(mask)!.Value);

            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(union, Template, null, 0, 1);
            Func<int, int, bool> on = Rebuild(tiles);

            Assert.IsTrue(tiles.Count >= 3 * 2);
            for (int y = 0; y < 1500; y += 11)
                for (int x = 0; x < 3000; x += 11)
                    if (!on(x, y))
                        Assert.Fail($"({x}, {y}) of the edited mask is not in the tiles");
            Assert.IsFalse(on(-100, 700), "the dilate margin is not set");
        }

        [TestMethod]
        public void AMaskTooBigForTheGpuIsDownsampledUntilItFitsTheTexture()
        {
            Vector2[] ring = [new(0, 0), new(12000, 0), new(12000, 3000), new(0, 3000)];

            RasterMask roomy = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, wholeRegion: true, maxRasterSide: 4096)!;
            RasterMask tight = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, wholeRegion: true, maxRasterSide: 2048)!;

            Assert.AreEqual(4, roomy.Scale, "3000 raster pixels plus the margin fit a 4096 texture at scale 4");
            Assert.IsTrue(roomy.Width <= 4096 && roomy.Height <= 4096);
            Assert.AreEqual(8, tight.Scale, "the same polygon is downsampled once more for a 2048 texture");
            Assert.IsTrue(tight.Width <= 2048 && tight.Height <= 2048, $"{tight.Width} x {tight.Height} must fit a 2048 texture");
            Assert.IsTrue(tight.TryGetSetWorldBounds(out Rectangle set));
            Assert.AreEqual(0, set.Left, tight.Scale);
            Assert.AreEqual(12000, set.Right, tight.Scale, "downsampling loses no extent");
        }

        [TestMethod]
        public void AcceptingADownsampledEditSendsOnlyTheTilesThePolygonTouchesNotTheDilateMargin()
        {
            Vector2[] ring = [new(0, 0), new(12000, 0), new(12000, 3000), new(0, 3000)];
            RasterMask mask = RasterMask.FromRings([ring], downsample: 1, marginPixels: 128, wholeRegion: true, maxRasterSide: 2048)!;
            SegmentationMaskUnion seeds = new();
            seeds.Add(SegmentationSeed.FromMask(mask)!.Value);

            List<MaskTile> tiles = SegmentationMaskTiles.BuildForeground(seeds, Template, null, 0, 1);
            Func<int, int, bool> on = Rebuild(tiles);

            // 12000 x 3000 reaches 12 columns by 3 rows of tiles. The raster's margin (128 pixels of 8 each side, a whole tile) reaches more,
            // and none of those tiles is sent.
            Assert.AreEqual(12 * 3, tiles.Count);
            Assert.IsTrue(tiles.All(tile => tile.Coord.Row >= 0 && tile.Coord.Row <= 2 && tile.Coord.Col >= 0 && tile.Coord.Col <= 11), "no tile of the margin");
            Assert.IsTrue(SegmentationMaskTiles.TilesSpanned(mask.WorldBounds, 1) >= tiles.Count, "the guard's bound covers what is sent");
            Assert.IsTrue(SegmentationMaskTiles.TilesSpanned(mask.WorldBounds, 1) > tiles.Count, "and the margin is why it is larger");
            for (int y = 0; y < 3000; y += 37)
                for (int x = 0; x < 12000; x += 37)
                    if (!on(x, y))
                        Assert.Fail($"({x}, {y}) of the edited mask is not in the tiles");
        }

        [TestMethod]
        public void TheTilesAreSentInMessagesOfAtMostEight()
        {
            List<MaskTile> tiles = Enumerable.Range(0, 20).Select(i => new MaskTile { Coord = Coord(0, i) }).ToList();

            List<Viking.gRPC.SegmentationServiceTypes.V1.MaskTiles> messages = SegmentationMaskTiles.Chunk(tiles).ToList();

            CollectionAssert.AreEqual(new[] { 8, 8, 4 }, messages.Select(message => message.Tiles.Count).ToArray());
            Assert.AreEqual(0, SegmentationMaskTiles.Chunk([]).Count());
        }

        [TestMethod]
        public void TheScaleSettingIsOneUnlessItSaysFour()
        {
            Assert.AreEqual(1, SegmentationMaskTiles.ReadScale(null));
            Assert.AreEqual(1, SegmentationMaskTiles.ReadScale("1"));
            Assert.AreEqual(1, SegmentationMaskTiles.ReadScale("2"), "no other scale is accepted by the server");
            Assert.AreEqual(1, SegmentationMaskTiles.ReadScale("junk"));
            Assert.AreEqual(4, SegmentationMaskTiles.ReadScale("4"));
        }

        [TestMethod]
        public void MaskTilesAreUsedOnlyWhenTheServerSaysItTakesThem()
        {
            Assert.IsFalse(SegmentationMaskTiles.IsSupported(null));
            Assert.IsFalse(SegmentationMaskTiles.IsSupported(new Viking.gRPC.SegmentationServiceTypes.V1.ServerStatusResponse()));
            Assert.IsTrue(SegmentationMaskTiles.IsSupported(new Viking.gRPC.SegmentationServiceTypes.V1.ServerStatusResponse { MaskTiles = true }));
        }

        private static Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest StartRequest(params TileCoord[] startTiles)
        {
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest request = new();
            request.Tiles.AddRange(startTiles);
            return request;
        }

        [TestMethod]
        public void AClickOnlyRequestAnnouncesZeroForegroundTilesAndStillSendsTheStartWindowsBackground()
        {
            // Two start windows' image tiles, no seed mask: the user only clicked.
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest request = StartRequest(Coord(2, 3), Coord(2, 4));
            SegmentationMaskUnion neighbours = UnionOf(Box(4 * Tile + 100, 2 * Tile + 100, 4 * Tile + 400, 2 * Tile + 400));

            SegmentationMaskTiles.StartTiles start = SegmentationMaskTiles.PrepareStart(request, null, Template, null, 0, neighbours, 1);

            Assert.IsTrue(request.HasForegroundMaskTileCount, "the count is sent even for a click-only request: it is the signal to ask for the background");
            Assert.AreEqual(0u, request.ForegroundMaskTileCount);
            Assert.AreEqual(0, start.Foreground.Count);
            Assert.AreEqual(2, start.Background.Count, "one background tile for each start window's image tile");
            CollectionAssert.AreEqual(new[] { (2, 3), (2, 4) }, start.Background.Select(tile => (tile.Coord.Row, tile.Coord.Col)).ToArray());
            Assert.IsTrue(start.Background.All(tile => tile.Kind == MaskKind.Background));
            Assert.IsTrue(start.Background[0].Png.IsEmpty && !start.Background[0].Full, "tile (2, 3) has no neighbour and is sent empty");
            Assert.IsFalse(start.Background[1].Png.IsEmpty, "tile (2, 4) holds the neighbour");
        }

        [TestMethod]
        public void AClickOnlyRequestWithNoNeighboursSendsEmptyBackgroundTilesAndNoForegroundMessage()
        {
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest request = StartRequest(Coord(0, 0), Coord(0, 1), Coord(1, 0));

            SegmentationMaskTiles.StartTiles start = SegmentationMaskTiles.PrepareStart(request, null, Template, null, 0, null, 1);
            List<Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesStreamRequest> messages = start.Messages().ToList();

            Assert.IsTrue(request.HasForegroundMaskTileCount);
            Assert.AreEqual(0u, request.ForegroundMaskTileCount);
            Assert.AreEqual(1, messages.Count, "only the background tiles are written");
            Assert.IsTrue(messages.All(message => message.BodyCase == Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesStreamRequest.BodyOneofCase.MaskTiles));
            Assert.AreEqual(3, messages[0].MaskTiles.Tiles.Count);
            Assert.IsTrue(messages[0].MaskTiles.Tiles.All(tile => tile.Kind == MaskKind.Background && !tile.Full && tile.Png.IsEmpty),
                "an empty tile is sent, so 'no neighbour' differs from 'not sent yet'");
        }

        [TestMethod]
        public void ASeededRequestAnnouncesItsTileCountAndWritesTheForegroundBeforeTheBackground()
        {
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest request = StartRequest(Coord(0, 0), Coord(0, 1));

            SegmentationMaskTiles.StartTiles start = SegmentationMaskTiles.PrepareStart(
                request, UnionOf(Box(100, 100, 2500, 800)), Template, null, 0, null, 1);
            List<Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesStreamRequest> messages = start.Messages().ToList();

            Assert.AreEqual(3u, request.ForegroundMaskTileCount, "the seed touches tiles (0, 0), (0, 1) and (0, 2)");
            Assert.AreEqual(3, start.Foreground.Count);
            Assert.AreEqual(2, start.Background.Count);
            IEnumerable<MaskKind> kinds = messages.SelectMany(message => message.MaskTiles.Tiles.Select(tile => tile.Kind));
            CollectionAssert.AreEqual(
                new[] { MaskKind.Foreground, MaskKind.Foreground, MaskKind.Foreground, MaskKind.Background, MaskKind.Background },
                kinds.ToArray(),
                "the server waits for every announced foreground tile, so they come first");
        }

        [TestMethod]
        public void TheStartRequestSetsTheForegroundCountEvenWhenItIsZero()
        {
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentTilesRequest request = new();
            Assert.IsFalse(request.HasForegroundMaskTileCount, "unset means an older client");

            request.ForegroundMaskTileCount = 0;

            Assert.IsTrue(request.HasForegroundMaskTileCount, "set to 0 still tells the server this client uses mask tiles");
        }
    }
}
