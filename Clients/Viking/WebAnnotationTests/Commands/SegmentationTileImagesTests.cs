using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The captured-tile store, its sampler and the places the edge snap is hooked in.
    /// </summary>
    [TestClass]
    public class SegmentationTileImagesTests
    {
        private const int Size = SegmentationTileGrid.TileSize;

        private static byte[] Tile(Func<int, int, byte> pixel)
        {
            byte[] gray = new byte[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                    gray[(y * Size) + x] = pixel(x, y);
            }

            return gray;
        }

        private static TileImageSet SetOf(int downsample, params (int Row, int Column, byte[] Gray)[] tiles) =>
            new(tiles.ToDictionary(t => (t.Row, t.Column), t => t.Gray), downsample, edgeScale: 10);

        [TestMethod]
        public void SamplingReadsTheImageRowThatIsFlippedFromTheMosaicRow()
        {
            // Image rows run down; world Y runs up. Brightness = the image row, so it falls as world Y rises.
            byte[] tile = Tile((x, y) => (byte)(y / 4));
            TileImageSet set = SetOf(1, (2, 3, tile));

            // Mosaic pixel (x = 3*1024 + 10, y = 2*1024 + 100): image row 1023 - 100 = 923, so 230.
            double centre = set.Sample(new Vector2((3 * Size) + 10.5, (2 * Size) + 100.5));
            double higher = set.Sample(new Vector2((3 * Size) + 10.5, (2 * Size) + 500.5));

            Assert.AreEqual(230, centre, 0.5);
            Assert.IsTrue(higher < centre, "higher in world Y is nearer the top of the image, which is darker here");
            Assert.AreEqual((1023 - 500) / 4.0, higher, 1.0);
        }

        [TestMethod]
        public void SamplingScalesWorldUnitsByTheDownsampleAndInterpolatesBetweenPixelCentres()
        {
            byte[] tile = Tile((x, y) => (byte)(x % 2 == 0 ? 100 : 200));
            TileImageSet set = SetOf(2, (0, 0, tile));

            // At downsample 2 pixel (x = 10) has its centre at world X = 21; pixel 11's at 23.
            Assert.AreEqual(100, set.Sample(new Vector2(21, 21)), 1e-9);
            Assert.AreEqual(200, set.Sample(new Vector2(23, 21)), 1e-9);
            Assert.AreEqual(150, set.Sample(new Vector2(22, 21)), 1e-9, "halfway between two pixel centres");
        }

        [TestMethod]
        public void SamplingIsUnknownOutsideTheTilesHeldAndContinuousAcrossATileJoin()
        {
            byte[] left = Tile((x, y) => 80);
            byte[] right = Tile((x, y) => 80);
            TileImageSet set = SetOf(1, (0, 0, left), (0, 1, right));

            Assert.AreEqual(80, set.Sample(new Vector2(Size, 500)), 1e-9, "across the join between two held tiles");
            Assert.IsTrue(double.IsNaN(set.Sample(new Vector2(2 * Size, 500))), "a tile that is not held");
            Assert.IsTrue(double.IsNaN(set.Sample(new Vector2(500, -5))), "below the first row");
            Assert.IsTrue(double.IsNaN(set.Sample(new Vector2(0, 500))), "the pixel left of the first column is not held");
        }

        [TestMethod]
        public void TheStoreKeepsTilesUntilFullThenDropsTheOldestAndRefreshesOnRestore()
        {
            SegmentationTileImages store = new();
            byte[] tile = Tile((x, y) => 1);
            for (int i = 0; i < SegmentationTileImages.Capacity; i++)
                store.Store($"tile {i}", tile);

            store.Store("tile 0", tile);          // refreshed: no longer the oldest
            store.Store("one more", tile);

            Assert.AreEqual(SegmentationTileImages.Capacity, store.Count);
            Assert.IsNotNull(store.Get("tile 0"), "restoring a tile makes it the newest");
            Assert.IsNull(store.Get("tile 1"), "the oldest was dropped");
            Assert.IsNotNull(store.Get("one more"));

            store.Clear();
            Assert.AreEqual(0, store.Count);
            Assert.IsNull(store.Get("tile 0"));
        }

        [TestMethod]
        public void StoringAnImageThatIsNotAFullTileIsRefused()
        {
            Assert.ThrowsException<ArgumentException>(() => new SegmentationTileImages().Store("short", new byte[10]));
            Assert.ThrowsException<ArgumentException>(() => SegmentationTileImages.PrepareGray(new byte[10], 4, 4));
        }

        [TestMethod]
        public void BlurringKeepsAFlatImageAndSoftensAStepWithoutMovingIt()
        {
            byte[] flat = SegmentationTileImages.PrepareGray(Enumerable.Repeat((byte)120, 64 * 64).ToArray(), 64, 64);
            Assert.IsTrue(flat.All(value => value == 120));

            byte[] step = new byte[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                    step[(y * 64) + x] = x < 32 ? (byte)40 : (byte)200;
            }

            byte[] blurred = SegmentationTileImages.PrepareGray(step, 64, 64);

            int row = 20 * 64;
            Assert.IsTrue(blurred[row + 28] < blurred[row + 31] && blurred[row + 31] < blurred[row + 32] && blurred[row + 32] < blurred[row + 35], "monotonic across the step");
            Assert.IsTrue(Math.Abs((blurred[row + 31] + blurred[row + 32]) - 240) <= 2, "centred on the original step (mean of the two sides)");
            Assert.AreEqual(40, blurred[row + 5], 1);
            Assert.AreEqual(200, blurred[row + 58], 1);
        }

        [TestMethod]
        public void TheSalientEdgeOfATileIsItsNinetiethPercentileGradient()
        {
            byte[] flat = Tile((x, y) => 100);
            Assert.AreEqual(0, SegmentationTileImages.EdgeScaleOf(flat, Size, Size), 1e-9);

            // A ramp of 2 levels per pixel in x (clamped to bytes by wrapping the position): gradient 2 everywhere inside.
            byte[] ramp = Tile((x, y) => (byte)((x % 100) * 2));
            double scale = SegmentationTileImages.EdgeScaleOf(ramp, Size, Size);
            Assert.AreEqual(2.0, scale, 0.51, $"scale {scale}");
        }

        [TestMethod]
        public void ASnapshotHoldsTheTilesAroundACoverageRectangleAndTheirMedianEdgeScale()
        {
            SegmentationTileImages store = new();
            byte[] quiet = Tile((x, y) => 100);
            byte[] ramped = Tile((x, y) => (byte)((x % 100) * 2));
            store.Store("k 0 0", quiet);
            store.Store("k 0 1", ramped);
            store.Store("k 5 5", ramped);   // far away: not part of the snapshot

            TileImageSet? set = store.Snapshot(1, (row, column) => $"k {row} {column}", new Rectangle(new Vector2(100, 100), new Vector2(Size + 100, 900)));

            Assert.IsNotNull(set);
            Assert.AreEqual(2, set!.Count);
            Assert.IsTrue(double.IsNaN(set.Sample(new Vector2(5 * Size + 50, 5 * Size + 50))) == true);
            Assert.IsNull(store.Snapshot(1, (row, column) => $"missing {row} {column}", new Rectangle(new Vector2(0, 0), new Vector2(10, 10))));
        }

        [TestMethod]
        public void ARefinerThatReturnsAnotherPolygonIsUsedAndOneThatThrowsOrReturnsNothingIsIgnored()
        {
            byte[] mask = new byte[20 * 20];
            for (int y = 4; y < 16; y++)
            {
                for (int x = 4; x < 16; x++)
                    mask[(y * 20) + x] = 255;
            }

            Rectangle bounds = new(new Vector2(0, 0), new Vector2(200, 200));
            int calls = 0;
            Polygon Trace(Func<Polygon, Polygon>? refine)
            {
                IReadOnlyList<Polygon> polygons = SegmentationMaskPolygonizer.CreatePolygons(
                    mask, 20, 20, 0, 0, 20, 20, bounds, 0.03, null, 0, out _, null, refine);
                return polygons.Single();
            }

            Polygon plain = Trace(null);
            Polygon moved = Trace(polygon =>
            {
                calls++;
                return new Polygon([.. polygon.ExteriorRing.Select(point => new Vector2(point.X + 7, point.Y))]);
            });
            Polygon threw = Trace(_ => throw new InvalidOperationException("no tiles"));
            Polygon nothing = Trace(_ => null!);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(plain.BoundingBox.Left + 7, moved.BoundingBox.Left, 1e-6, "the refined polygon is the one returned");
            Assert.AreEqual(plain.BoundingBox.Left, threw.BoundingBox.Left, 1e-6, "a refiner that throws leaves the traced contour");
            Assert.AreEqual(plain.BoundingBox.Left, nothing.BoundingBox.Left, 1e-6, "a refiner that returns nothing leaves it too");
        }

        [TestMethod]
        public void AMaskSourceSnapsOnlyWhileThePreferenceIsOn()
        {
            byte[] mask = new byte[20 * 20];
            for (int y = 4; y < 16; y++)
            {
                for (int x = 4; x < 16; x++)
                    mask[(y * 20) + x] = 255;
            }

            int calls = 0;
            AutoPolygonizeMaskOverlay overlay = new(
                mask,
                20,
                20,
                new Rectangle(new Vector2(0, 0), new Vector2(200, 200)),
                1,
                polygon =>
                {
                    calls++;
                    return polygon;
                });
            AutoPolygonizeMaskSource source = new(overlay, []);

            bool original = WebAnnotation.Properties.Settings.Default.SegmentationEdgeSnapEnabled;
            try
            {
                WebAnnotation.Properties.Settings.Default.SegmentationEdgeSnapEnabled = true;
                Assert.IsNotNull(source.CreatePolygon(0.03, 0));
                Assert.AreEqual(1, calls, "on: the refiner runs");

                WebAnnotation.Properties.Settings.Default.SegmentationEdgeSnapEnabled = false;
                Assert.IsNotNull(source.CreatePolygon(0.03, 0));
                Assert.AreEqual(1, calls, "off: the contour is left as traced");
            }
            finally
            {
                WebAnnotation.Properties.Settings.Default.SegmentationEdgeSnapEnabled = original;
            }
        }

        [TestMethod]
        public void OverlappingOverlaysMergeByPerPixelMaximumInBothLayersAndTraceAsOne()
        {
            static byte[] Columns(int width, int height, int from, int to, byte value)
            {
                byte[] data = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = from; x < to; x++)
                        data[(y * width) + x] = value;
                }

                return data;
            }

            // A covers world x 0..10 and is object (200) over x 0..6; B covers x 4..14 and is object (220) over x 5..14.
            AutoPolygonizeMaskOverlay a = new(
                Columns(10, 10, 0, 6, 200), 10, 10, new Rectangle(new Vector2(0, 0), new Vector2(10, 10)), 1,
                serverMaskData: Columns(10, 10, 0, 6, 180));
            AutoPolygonizeMaskOverlay b = new(
                Columns(10, 10, 1, 10, 220), 10, 10, new Rectangle(new Vector2(4, 0), new Vector2(14, 10)), 1,
                serverMaskData: Columns(10, 10, 1, 10, 240));

            AutoPolygonizeMaskOverlay? merged = AutoPolygonizeMaskOverlay.TryOr([a, b]);

            Assert.IsNotNull(merged);
            Assert.AreEqual(14, merged!.Width);
            Assert.AreEqual(10, merged.Height);
            int row = 5 * merged.Width;
            Assert.AreEqual(200, merged.MaskData[row + 1], "only A covers x 1");
            Assert.AreEqual(220, merged.MaskData[row + 12], "only B covers x 12");
            Assert.AreEqual(220, merged.MaskData[row + 5], "where both cover, the larger wins");
            Assert.AreEqual(180, merged.DisplayData[row + 1], "the server layer is merged on its own");
            Assert.AreEqual(240, merged.DisplayData[row + 12]);

            IReadOnlyList<Polygon> traced = SegmentationMaskPolygonizer.CreatePolygons(
                merged.MaskData, merged.Width, merged.Height, 0, 0, merged.Width, merged.Height, merged.TraceBounds,
                0.03, null, 0, out _, null, null);
            Assert.AreEqual(1, traced.Count, "one outline for the union");
            Assert.IsTrue(traced[0].BoundingBox.Width > 12.5, $"spans both masks: {traced[0].BoundingBox}");
        }

        [TestMethod]
        public void AMergedOverlayPutsEachSourceAtItsOwnWorldPositionVertically()
        {
            // Masks are top-origin and world Y is up. A covers world y 0..10 and is object in its top two rows
            // (world y 8..10). B covers y 5..25 and is object in its bottom two rows (world y 5..7).
            byte[] aData = new byte[10 * 10];
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 10; x++)
                    aData[(y * 10) + x] = 200;
            }

            byte[] bData = new byte[10 * 20];
            for (int y = 18; y < 20; y++)
            {
                for (int x = 0; x < 10; x++)
                    bData[(y * 10) + x] = 220;
            }

            AutoPolygonizeMaskOverlay a = new(aData, 10, 10, new Rectangle(new Vector2(0, 0), new Vector2(10, 10)), 4);
            AutoPolygonizeMaskOverlay b = new(bData, 10, 20, new Rectangle(new Vector2(0, 5), new Vector2(10, 25)), 4);

            AutoPolygonizeMaskOverlay? merged = AutoPolygonizeMaskOverlay.TryOr([a, b]);

            Assert.IsNotNull(merged);
            Assert.AreEqual(25, merged!.Height);
            Assert.AreEqual(4, merged.PixelScale, "the merged canvas keeps the sample size of its finest source");
            byte At(double worldY) => merged.MaskData[(((int)Math.Floor(25 - worldY)) * merged.Width) + 5];
            Assert.AreEqual(200, At(9.5), "A's object stays at world y 8..10");
            Assert.AreEqual(200, At(8.5));
            Assert.AreEqual(220, At(5.5), "B's object stays at world y 5..7");
            Assert.AreEqual(220, At(6.5));
            Assert.AreEqual(0, At(7.5));
            Assert.AreEqual(0, At(20.5));
            Assert.AreEqual(0, At(2.5));
        }

        [TestMethod]
        public void TheEdgeSnapPreferenceIsOnByDefaultInTheStoredSettingsDefinition()
        {
            System.Reflection.PropertyInfo property = typeof(WebAnnotation.Properties.Settings)
                .GetProperty(nameof(WebAnnotation.Properties.Settings.SegmentationEdgeSnapEnabled))!;
            System.Configuration.DefaultSettingValueAttribute attribute =
                (System.Configuration.DefaultSettingValueAttribute)Attribute.GetCustomAttribute(
                    property,
                    typeof(System.Configuration.DefaultSettingValueAttribute))!;

            Assert.AreEqual("True", attribute.Value);
            Assert.IsNotNull(Attribute.GetCustomAttribute(property, typeof(System.Configuration.UserScopedSettingAttribute)));
        }
    }
}
