using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A union sums squares, polygons and bitmaps into one mask that is drawn a grid tile at a time (row 0 of a tile is its highest y).
    /// </summary>
    [TestClass]
    public class SegmentationMaskUnionTests
    {
        private const int Tile = SegmentationTileGrid.TileSize;

        private static byte[]? DrawTile(
            SegmentationMaskUnion union, int row = 0, int col = 0, int downsample = 1, IReadOnlyList<Vector2>? holes = null, double radius = 0, int scale = 1)
            => union.RasterizeTile(downsample, new TileCell(row, col), holes, radius, scale);

        /// <summary>Whether mosaic pixel (x, y) of tile (0, 0) is set.</summary>
        private static bool On(byte[] tile, int x, int y) => tile[(Tile - 1 - y) * Tile + x] != 0;

        [TestMethod]
        public void AnEmptyUnionHasNoTiles()
        {
            SegmentationMaskUnion union = new();

            Assert.AreEqual(0, union.TilesTouched(1).Count);
            Assert.IsNull(DrawTile(union));
        }

        [TestMethod]
        public void TwoSeparateShapesAreOneMaskAndTheGapStaysOff()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(400, 400), new Vector2(420, 420)));
            union.Add(new Rectangle(new Vector2(600, 600), new Vector2(620, 620)));

            byte[] tile = DrawTile(union)!;

            Assert.IsTrue(On(tile, 410, 410));
            Assert.IsTrue(On(tile, 610, 610));
            Assert.IsFalse(On(tile, 510, 510));
            Assert.IsFalse(On(tile, 410, 610));
        }

        [TestMethod]
        public void APolygonsHoleStaysOpenAndAnotherShapeMayFillIt()
        {
            Vector2[] outer = [new(450, 450), new(550, 450), new(550, 550), new(450, 550)];
            Vector2[] hole = [new(490, 490), new(510, 490), new(510, 510), new(490, 510)];
            SegmentationMaskUnion union = new();
            union.Add([outer, hole]);

            byte[] open = DrawTile(union)!;
            Assert.IsTrue(On(open, 460, 460));
            Assert.IsFalse(On(open, 500, 500));

            union.Add(new Rectangle(new Vector2(495, 495), new Vector2(505, 505)));
            byte[] filled = DrawTile(union)!;
            Assert.IsTrue(On(filled, 500, 500));
        }

        [TestMethod]
        public void DownsampleScalesTheShapeToMosaicPixels()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(1900, 1900), new Vector2(2100, 2100)));

            byte[] tile = DrawTile(union, downsample: 4)!;

            Assert.IsTrue(On(tile, 475, 475));
            Assert.IsTrue(On(tile, 524, 524));
            Assert.IsFalse(On(tile, 474, 500));
            Assert.IsFalse(On(tile, 525, 500));
        }

        [TestMethod]
        public void ACircleSeedIsMaskedAsTheCircleNotItsInscribedSquare()
        {
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                [new Vector2(500, 500), new Vector2(600, 500), new Vector2(500, 600), new Vector2(400, 500), new Vector2(500, 400)],
                out CircleSegmentationPrompts.StartingPrompt prompt));
            Assert.AreEqual(100.0, prompt.Seed.Circle!.Value.Radius, 1e-9);
            SegmentationMaskUnion union = new();
            union.Add(prompt.Seed);

            byte[] tile = DrawTile(union)!;

            Assert.IsTrue(On(tile, 500, 500));
            Assert.IsTrue(On(tile, 425, 500), "inside the circle, outside the inscribed square (half side 70.7)");
            Assert.IsTrue(On(tile, 500, 590));
            Assert.IsFalse(On(tile, 580, 580), "the corner of the circle's bounding box, 113 from the centre");
            Assert.IsFalse(On(tile, 500, 610));
        }

        [TestMethod]
        public void ACircleOfAnyRadiusStaysWithinAQuarterPixelOfTheTrueCircle()
        {
            foreach (double radius in new[] { 10.0, 100.0, 1000.0, 10000.0, 100000.0 })
            {
                int segments = SegmentationMaskUnion.CircleSegmentsFor(radius);
                double sagitta = radius * (1 - Math.Cos(Math.PI / segments));

                Assert.IsTrue(segments >= 180, $"radius {radius}");
                Assert.IsTrue(sagitta <= 0.25 + 1e-9 || segments == 4096, $"radius {radius}: {segments} segments leave {sagitta} between the edge and the circle");
            }
        }

        [TestMethod]
        public void ABackgroundPointCutsAHoleInTheForegroundMask()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(400, 400), new Vector2(600, 600)));

            byte[] tile = DrawTile(union, holes: [new Vector2(450, 450)], radius: 10)!;

            Assert.IsFalse(On(tile, 450, 450), "the background click is not overruled by the mask");
            Assert.IsFalse(On(tile, 455, 450));
            Assert.IsTrue(On(tile, 470, 450), "beyond the click's radius");
            Assert.IsTrue(On(tile, 550, 550));
        }

        [TestMethod]
        public void AMaskCutEntirelyAwayByBackgroundPointsHasNoTile()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(495, 495), new Vector2(505, 505)));

            Assert.IsNull(DrawTile(union, holes: [new Vector2(500, 500)], radius: 20));
        }

        [TestMethod]
        public void ADrawnTileIsNotTheScratchRasterAndSurvivesTheNextDraw()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(100, 100), new Vector2(200, 200)));
            union.Add(new Rectangle(new Vector2(Tile + 100, 100), new Vector2(Tile + 300, 300)));

            byte[] first = DrawTile(union, col: 0)!;
            int firstCount = first.Count(p => p != 0);
            byte[] second = DrawTile(union, col: 1)!;

            Assert.AreNotSame(first, second);
            Assert.AreEqual(firstCount, first.Count(p => p != 0), "the first tile was not overwritten by the second draw");
            Assert.AreEqual(100 * 100, firstCount);
            Assert.AreEqual(200 * 200, second.Count(p => p != 0));
        }

        [TestMethod]
        public void AUnionSharedByConcurrentTasksDrawsTheSameTilesAsOneThread()
        {
            // The first draws build the tile index lazily; a request abandoned by the user may still be drawing while the next starts.
            static SegmentationMaskUnion Build()
            {
                SegmentationMaskUnion union = new();
                for (int i = 0; i < 300; i++)
                    union.Add(new Rectangle(new Vector2(i * 37, i * 11), new Vector2(i * 37 + 900, i * 11 + 700)));

                return union;
            }

            SegmentationMaskUnion shared = Build();
            SegmentationMaskUnion reference = Build();
            TileCell[] cells = [.. Enumerable.Range(0, 6).SelectMany(row => Enumerable.Range(0, 12).Select(col => new TileCell(row, col)))];
            int[] expected = [.. cells.Select(cell => reference.RasterizeTile(1, cell)?.Count(p => p != 0) ?? -1)];

            int[][] results = Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                cells.Select(cell => shared.RasterizeTile(1, cell)?.Count(p => p != 0) ?? -1).ToArray()))).GetAwaiter().GetResult();

            foreach (int[] result in results)
                CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AShapeThatEndsOnATileBoundaryTouchesOnlyTheTilesItFills()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(0, 0), new Vector2(2 * Tile, Tile)));

            IReadOnlyList<TileCell> touched = union.TilesTouched(1);

            CollectionAssert.AreEqual(new[] { new TileCell(0, 0), new TileCell(0, 1) }, touched.ToArray());
        }

        [TestMethod]
        public void ACoarseBitmapBecomesWholeFullResolutionBlocksInATile()
        {
            // A raster at scale 4 with a 10 x 10 block of set pixels is 40 x 40 mosaic pixels; nothing is lost and nothing is added.
            byte[] pixels = new byte[300 * 300];
            for (int row = 100; row < 110; row++)
                for (int column = 100; column < 110; column++)
                    pixels[row * 300 + column] = 255;
            RasterMask coarse = new(downsample: 1, xMin: 0, yMax: 1199, width: 300, height: 300, pixels: pixels, scale: 4);
            SegmentationMaskUnion union = new();
            union.Add(SegmentationSeed.FromMask(coarse)!.Value);

            byte[] tile = DrawTile(union)!;

            Assert.AreEqual(40 * 40, tile.Count(p => p != 0));
            Assert.IsTrue(On(tile, 400, 760));
            Assert.IsTrue(On(tile, 439, 799));
            Assert.IsFalse(On(tile, 440, 799));
            Assert.IsFalse(On(tile, 439, 800));
        }
    }
}
