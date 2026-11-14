using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryTests
{
    /// <summary>
    /// Mosaic point to tile pixel and back (<see cref="MosaicTileLocator"/>), and moving whole shapes from an old mosaic to a
    /// rebuilt one through shared tiles (<see cref="TileRemap"/>). Mosaics are written as .mosaic text and parsed by
    /// <see cref="TransformFactory.LoadMosaic"/>, so the parser is covered too.
    /// </summary>
    [TestClass]
    public class MosaicTileLocatorTests
    {
        const double TileSize = 1000;

        /// <summary>A format-1 .mosaic whose tiles are translations: tile number, then its lower-left corner in mosaic space.</summary>
        static string[] TranslatedMosaic(params (int Tile, double X, double Y)[] tiles) =>
            MosaicText(tiles.Select(t => (t.Tile,
                FormattableString.Invariant($"TranslationTransform_double_2_2 vp 2 {t.X} {t.Y} fp 4 0 0 {TileSize / 2} {TileSize / 2}"))));

        static string[] MosaicText(IEnumerable<(int Tile, string Transform)> tiles)
        {
            var list = tiles.ToList();
            StringBuilder text = new();
            text.AppendLine($"number_of_images: {list.Count}");
            text.AppendLine("pixel_spacing: 1");
            text.AppendLine("use_std_mask: 0");
            text.AppendLine("format_version_number: 1");
            foreach (var (tile, transform) in list)
            {
                text.AppendLine("image:");
                text.AppendLine($"{tile:D6}.png");
                text.AppendLine(transform);
            }

            return text.ToString().Split(["\r\n", "\n"], StringSplitOptions.None);
        }

        static MosaicTileLocator Locator(string[] mosaic) =>
            new(TransformFactory.LoadMosaic("test", mosaic, DateTime.UtcNow));

        /// <summary>Two tiles side by side, overlapping by 100 px: tile 1 at x 0-1000, tile 2 at x 900-1900.</summary>
        static MosaicTileLocator TwoTiles(double shiftTile2X = 0, double shiftTile2Y = 0) =>
            Locator(TranslatedMosaic((1, 0, 0), (2, 900 + shiftTile2X, shiftTile2Y)));

        [TestMethod]
        public void ParsesTileNumbersFromFileNames()
        {
            MosaicTileLocator mosaic = Locator(TranslatedMosaic((17, 0, 0), (123, 2000, 0)));
            Assert.AreEqual(2, mosaic.TileCount);
            CollectionAssert.AreEquivalent(new[] { 17, 123 }, mosaic.TileNumbers.ToArray());
        }

        [TestMethod]
        public void LocateThenMapThroughTheSameMosaicIsTheIdentity()
        {
            MosaicTileLocator mosaic = TwoTiles();
            foreach (Vector2 p in new[] { new Vector2(10, 10), new Vector2(500, 999), new Vector2(950, 400), new Vector2(1890, 5) })
            {
                TilePoint t = mosaic.Locate(p);
                Assert.AreEqual(TileMappingStatus.Mapped, t.Status);
                Assert.IsTrue(mosaic.TryMapFromTile(t.TileNumber, t.Pixel, out Vector2 back));
                Assert.AreEqual(0, Vector2.Distance(p, back), 0.01);
            }
        }

        [TestMethod]
        public void OverlapPicksTheTileWithTheClosestCentre()
        {
            MosaicTileLocator mosaic = TwoTiles();
            // Tile centres are (500, 500) and (1400, 500); the midpoint between them is x = 950.
            TilePoint left = mosaic.Locate(new Vector2(940, 500), out TilePoint? leftAlternate);
            TilePoint right = mosaic.Locate(new Vector2(960, 500), out TilePoint? rightAlternate);

            Assert.AreEqual(1, left.TileNumber);
            Assert.AreEqual(2, right.TileNumber);
            Assert.AreEqual(2, leftAlternate?.TileNumber);
            Assert.AreEqual(1, rightAlternate?.TileNumber);
            Assert.AreEqual(940, left.Pixel.X, 1e-6);
            Assert.AreEqual(60, right.Pixel.X, 1e-6);
        }

        [TestMethod]
        public void PointOutsideEveryTileHasNoTile()
        {
            TilePoint t = TwoTiles().Locate(new Vector2(5000, 5000), out TilePoint? alternate);
            Assert.AreEqual(TileMappingStatus.NoTile, t.Status);
            Assert.IsNull(alternate);
        }

        [TestMethod]
        public void PointInsideARotatedTilesOutlineButOffItsImageIsExtrapolated()
        {
            // A rigid tile (4080 px) rotated 45 degrees about its centre: the corners of its outline box are off the image.
            string[] text = MosaicText([(5, FormattableString.Invariant($"Rigid2DTransform_double_2_2 vp 3 {Math.PI / 4} 0 0 fp 2 2040 2040"))]);
            ITransform[] transforms = TransformFactory.LoadMosaic("test", text, DateTime.UtcNow);
            MosaicTileLocator mosaic = new(transforms);
            Vector2 centre = transforms[0].Transform(new Vector2(2040, 2040));

            Assert.AreEqual(TileMappingStatus.Mapped, mosaic.Locate(centre).Status);
            TilePoint corner = mosaic.Locate(centre + new Vector2(2000, 2000));
            Assert.AreEqual(TileMappingStatus.Extrapolated, corner.Status);
            Assert.AreEqual(5, corner.TileNumber);
        }

        [TestMethod]
        public void MovedTileCarriesItsPointsAndOthersStay()
        {
            MosaicTileLocator oldMosaic = TwoTiles();
            MosaicTileLocator newMosaic = TwoTiles(shiftTile2X: 25, shiftTile2Y: -40);

            TilePoint onTile1 = oldMosaic.Locate(new Vector2(300, 300));
            TilePoint onTile2 = oldMosaic.Locate(new Vector2(1500, 300));

            Assert.IsTrue(newMosaic.TryMapFromTile(onTile1.TileNumber, onTile1.Pixel, out Vector2 p1));
            Assert.IsTrue(newMosaic.TryMapFromTile(onTile2.TileNumber, onTile2.Pixel, out Vector2 p2));
            Assert.AreEqual(0, Vector2.Distance(new Vector2(300, 300), p1), 1e-6);
            Assert.AreEqual(0, Vector2.Distance(new Vector2(1525, 260), p2), 1e-6);
        }

        [TestMethod]
        public void TileMissingFromTheNewMosaicDoesNotMap()
        {
            MosaicTileLocator newMosaic = Locator(TranslatedMosaic((1, 0, 0)));
            Assert.IsFalse(newMosaic.ContainsTile(2));
            Assert.IsFalse(newMosaic.TryMapFromTile(2, new Vector2(10, 10), out _));
        }

        [TestMethod]
        public void DuplicateTileNumbersAreRefused()
        {
            Assert.ThrowsException<ArgumentException>(() => Locator(TranslatedMosaic((1, 0, 0), (1, 2000, 0))));
        }

        // ---- TileRemap: whole shapes ----

        static Polygon PolygonWithHoleAcrossBothTiles() =>
            new([new Vector2(100, 100), new Vector2(1800, 100), new Vector2(1800, 900), new Vector2(100, 900), new Vector2(100, 100)],
                [new[] { new Vector2(300, 300), new Vector2(300, 700), new Vector2(1600, 700), new Vector2(1600, 300), new Vector2(300, 300) }]);

        [TestMethod]
        public void PolygonAcrossTwoTilesSplitsIntoRunsAndRebuildsUnderTheSameMosaic()
        {
            MosaicTileLocator mosaic = TwoTiles();
            Polygon polygon = PolygonWithHoleAcrossBothTiles();

            ToTilesResult tiles = TileRemap.ToTiles(polygon, mosaic);
            Assert.IsNotNull(tiles.Shape);
            TilePart exterior = tiles.Shape.Parts.Single(p => p.Role == TilePart.Exterior);
            Assert.IsTrue(exterior.Runs.Count >= 2, "The exterior ring crosses from tile 1 to tile 2");
            CollectionAssert.IsSubsetOf(new[] { 1, 2 }, exterior.Runs.Select(r => r.Tile).Distinct().ToArray());
            Assert.AreEqual(polygon.ExteriorRing.Length, exterior.VertexCount);
            Assert.AreEqual(1, tiles.Shape.Parts.Count(p => p.Role == TilePart.Hole));

            FromTilesResult rebuilt = TileRemap.FromTiles(tiles.Shape, mosaic);
            Assert.IsTrue(rebuilt.Succeeded, rebuilt.Failure);
            Vector2[] before = [.. TileRemap.Vertices(polygon)];
            Vector2[] after = [.. TileRemap.Vertices(rebuilt.Shape!)];
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(0, Vector2.Distance(before[i], after[i]), 1e-6);
        }

        [TestMethod]
        public void PolygonVerticesFollowTheirOwnTile()
        {
            Polygon polygon = PolygonWithHoleAcrossBothTiles();
            ToTilesResult tiles = TileRemap.ToTiles(polygon, TwoTiles());
            FromTilesResult moved = TileRemap.FromTiles(tiles.Shape!, TwoTiles(shiftTile2X: 10));
            Assert.IsTrue(moved.Succeeded, moved.Failure);

            Vector2[] before = [.. TileRemap.Vertices(polygon)];
            for (int i = 0; i < before.Length; i++)
            {
                double expectedShift = before[i].X > 950 ? 10 : 0;
                Assert.AreEqual(before[i].X + expectedShift, moved.NewVertices[i].X, 1e-6, $"vertex {i} at {before[i]}");
                Assert.AreEqual(before[i].Y, moved.NewVertices[i].Y, 1e-6);
            }
        }

        [TestMethod]
        public void CircleMapsItsCentreAndKeepsItsRadius()
        {
            // Both mosaics place the same tile images at full resolution, so the radius carries over unchanged. The circle
            // reaches onto tile 2, which the new mosaic moves 30,000 px; only the centre's tile (1) decides where it goes.
            // Found on RC2 1240, where mapping rim points through such a neighbour gave 10,000 px radii.
            Circle circle = new(new Vector2(940, 500), 100);
            ToTilesResult tiles = TileRemap.ToTiles(circle, TwoTiles());
            Assert.AreEqual(100.0, tiles.Shape!.Radius);
            Assert.AreEqual(1, tiles.Shape.Parts.Single().VertexCount);
            Assert.AreEqual(1, tiles.Shape.Parts.Single().Runs.Single().Tile);

            FromTilesResult moved = TileRemap.FromTiles(tiles.Shape, TwoTiles(shiftTile2X: 30000));
            Assert.IsTrue(moved.Succeeded, moved.Failure);
            Circle result = (Circle)moved.Shape!;
            Assert.AreEqual(0, Vector2.Distance(circle.Center, result.Center), 1e-6);
            Assert.AreEqual(100, result.Radius, 1e-9);
        }

        [TestMethod]
        public void CircleWhoseRimLeavesEveryTileStillMapsByItsCentre()
        {
            // Centre on tile 2 near its right edge (x = 1890); the circle reaches past every tile, which no longer matters.
            Circle circle = new(new Vector2(1890, 500), 300);
            ToTilesResult tiles = TileRemap.ToTiles(circle, TwoTiles());
            Assert.IsNotNull(tiles.Shape);

            FromTilesResult moved = TileRemap.FromTiles(tiles.Shape, TwoTiles(shiftTile2X: 12));
            Assert.IsTrue(moved.Succeeded, moved.Failure);
            Assert.AreEqual(1902, ((Circle)moved.Shape!).Center.X, 1e-6);
            Assert.AreEqual(300, ((Circle)moved.Shape!).Radius, 1e-9);
        }

        [TestMethod]
        public void OverlapDisagreementIsReportedForVerticesInTheOverlap()
        {
            Polyline line = new([new Vector2(500, 500), new Vector2(940, 500)]);
            ToTilesResult tiles = TileRemap.ToTiles(line, TwoTiles());
            FromTilesResult moved = TileRemap.FromTiles(tiles.Shape!, TwoTiles(shiftTile2X: 6));

            Assert.IsTrue(moved.Succeeded, moved.Failure);
            Assert.AreEqual(1, moved.OverlapDisagreements.Count);
            Assert.AreEqual(6, moved.OverlapDisagreements[0], 1e-6);
        }

        [TestMethod]
        public void ShapeWithAVertexOffEveryTileIsNotExpressedInTiles()
        {
            Polyline line = new([new Vector2(500, 500), new Vector2(5000, 500), new Vector2(600, 600)]);
            ToTilesResult tiles = TileRemap.ToTiles(line, TwoTiles());
            Assert.IsNull(tiles.Shape);
            Assert.AreEqual(1, tiles.Uncovered.Count);
        }

        [TestMethod]
        public void ShapeOnATileTheNewMosaicDropsFailsWhole()
        {
            Polyline line = new([new Vector2(500, 500), new Vector2(1500, 500)]);
            ToTilesResult tiles = TileRemap.ToTiles(line, TwoTiles());
            FromTilesResult moved = TileRemap.FromTiles(tiles.Shape!, Locator(TranslatedMosaic((1, 0, 0))));

            Assert.IsFalse(moved.Succeeded);
            Assert.IsNull(moved.Shape);
            CollectionAssert.AreEqual(new[] { 2 }, moved.MissingTiles.ToArray());
        }

        [TestMethod]
        public void TileShapeSurvivesJsonRoundTrip()
        {
            ToTilesResult tiles = TileRemap.ToTiles(PolygonWithHoleAcrossBothTiles(), TwoTiles());
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(tiles.Shape);
            TileShape back = Newtonsoft.Json.JsonConvert.DeserializeObject<TileShape>(json)!;

            FromTilesResult a = TileRemap.FromTiles(tiles.Shape!, TwoTiles(shiftTile2X: 3));
            FromTilesResult b = TileRemap.FromTiles(back, TwoTiles(shiftTile2X: 3));
            CollectionAssert.AreEqual(a.NewVertices, b.NewVertices);
        }
    }
}
