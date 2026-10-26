using Geometry;
using AnnotationService.Types;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotationModel;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using GridCell = Viking.gRPC.Geometry.V1.GridCell;
using MaskKind = Viking.gRPC.SegmentationServiceTypes.V1.MaskKind;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// What the client sends and reads on <c>SegmentTilesStream</c>: circles and polygons as shapes in the tiles' frame, the
    /// neighbouring annotations as background shapes as the object reaches them, a painted bitmap as announced mask tiles,
    /// answers that list only failures, and results with one segment per island placed by their logits' origin.
    /// </summary>
    [TestClass]
    public class SegmentationProtocolTests
    {
        private const int Tile = 1024;

        /// <summary>volume = (3x, y): a stretch, so a radius carried across is the five-point mean scale (2), not copied.</summary>
        private sealed class StretchTransform : IVolumeToSectionTransform
        {
            public long ID => 0;
            public Rectangle? SectionBounds => null;
            public Rectangle? VolumeBounds => null;

            private static Vector2 Forward(Vector2 p) => new(p.X * 3, p.Y);
            private static Vector2 Inverse(Vector2 p) => new(p.X / 3, p.Y);

            public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = Forward(P); return true; }
            public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = Inverse(P); return true; }
            public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Forward)]; return [.. Points.Select(_ => true)]; }
            public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = [.. Points.Select(Inverse)]; return [.. Points.Select(_ => true)]; }
            public Vector2 SectionToVolume(Vector2 P) => Forward(P);
            public Vector2 VolumeToSection(Vector2 P) => Inverse(P);
            public Vector2[] SectionToVolume(Vector2[] Points) => [.. Points.Select(Forward)];
            public Vector2[] VolumeToSection(Vector2[] Points) => [.. Points.Select(Inverse)];
        }

        /// <summary>A location whose mosaic and volume shapes are both <paramref name="wkt"/>, as the server would deliver it.</summary>
        private static LocationObj Location(long id, long parentId, LocationType type, string wkt)
        {
            SqlGeometry shape = SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(wkt), 0);
            return new LocationObj(new Location
            {
                ID = id,
                ParentID = parentId,
                TypeCode = (short)type,
                Section = 1,
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            });
        }

        private static SegmentationViewportSession.TileSignature Signature(int downsample, CoordinateSpace space = CoordinateSpace.Volume) =>
            new("vol", 7, "TEM", "Grid|Grid", downsample, space);

        private static SegmentationViewportSession.StartMessage Start(
            int downsample,
            IReadOnlyList<Vector2> foreground,
            IReadOnlyList<SegmentationSeed>? seeds = null,
            IReadOnlyList<Vector2>? background = null,
            SegmentationBackgroundShapes? backgroundShapes = null,
            ISet<int>? sent = null)
        {
            List<TileCell> cells = SegmentationTileGrid.CellsContainingPoints(
                [.. foreground, .. (seeds ?? []).Select(seed => seed.Center)], downsample);
            return SegmentationViewportSession.BuildStart(
                Signature(downsample), cells, foreground, background, seeds, backgroundShapes, sent ?? new HashSet<int>(),
                pointRadiusPixels: 0, maskTileScale: 1);
        }

        // ---- circles -------------------------------------------------------------------------------------------------

        [TestMethod]
        public void ACircleIsSentAsAShapeWithItsTrueRadiusInTheTileFrameCarriedByTheMeanScale()
        {
            // A mosaic circle of radius 10 at (100, 100). The stretch maps the rim points 30, 10, 30 and 10 from the centre, so
            // the volume radius is the mean scale (2) times 10, at the volume centre (300, 100).
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                CircleSegmentationPrompts.ToVolumePoints(
                    CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(new Vector2(100, 100), 10)), new StretchTransform()),
                out CircleSegmentationPrompts.StartingPrompt prompt));

            SegmentationViewportSession.StartMessage start = Start(2, prompt.Points, [prompt.Seed]);
            SegmentationServiceTypes.SegmentTilesRequest request = start.Request;

            ProtoShape shape = request.ForegroundFeature.Shapes.Single();
            Assert.AreEqual(ProtoShape.KindOneofCase.Circle, shape.KindCase);
            Assert.AreEqual(150.0, shape.Circle.Center.X, 1e-9, "the volume centre at the grid's downsample");
            Assert.AreEqual(50.0, shape.Circle.Center.Y, 1e-9);
            Assert.AreEqual(10.0, shape.Circle.Radius, 1e-9, "20 volume units at downsample 2, not scaled by any circle_diameter_scale");
            Assert.AreEqual(0u, request.MaskTileCount, "the circle is not rasterized");
            Assert.AreEqual(0, start.MaskTiles.Count);
            Assert.AreEqual(1, start.Messages().Count(), "only start");
            Assert.AreEqual(4, request.ForegroundFeature.Points.Count, "the four axis clicks go with it");
            Assert.AreEqual((300 + 19.0) / 2, request.ForegroundFeature.Points[0].X, 1e-9, "a click at 0.95 of the radius, in the grid frame");
        }

        [TestMethod]
        public void TheRequestNamesItsGridOnceAndItsTilesAsCells()
        {
            SegmentationViewportSession.StartMessage start = Start(1, [new Vector2(Tile * 2 + 5, Tile * 3 + 5)]);
            SegmentationServiceTypes.SegmentTilesRequest request = start.Request;

            Assert.AreEqual(CoordinateSpace.Volume, request.Grid.Space);
            Assert.AreEqual("Grid|Grid", request.Grid.Transform);
            Assert.AreEqual(1, request.Grid.Downsample);
            Assert.AreEqual(7, request.Grid.Section);
            CollectionAssert.AreEqual(new[] { (3, 2) }, request.Tiles.Select(cell => (cell.Row, cell.Col)).ToArray(), "row along Y, column along X");
            Assert.IsNull(request.BackgroundFeature, "no background clicks or shapes: no background Feature");
        }

        [TestMethod]
        public void APolygonSeedIsSentAsAPolygonWithItsHolesAndNoRepeatedVertex()
        {
            Vector2[] exterior = [new(0, 0), new(400, 0), new(400, 400), new(0, 400), new(0, 0)];
            Vector2[] hole = [new(100, 100), new(200, 100), new(200, 200), new(100, 100)];
            SegmentationSeed seed = SegmentationSeed.FromPolygon(new Polygon(exterior, [hole]), new Vector2(300, 300));

            ProtoShape shape = Start(2, [new Vector2(300, 300)], [seed]).Request.ForegroundFeature.Shapes.Single();

            Assert.AreEqual(ProtoShape.KindOneofCase.Polygon, shape.KindCase);
            Assert.AreEqual(4, shape.Polygon.Exterior.Points.Count);
            Assert.AreEqual(200.0, shape.Polygon.Exterior.Points[1].X, 1e-9);
            Assert.AreEqual(3, shape.Polygon.Holes.Single().Points.Count);
            Assert.IsNull(shape.Polygon.Fit, "a traced polygon is straight segments");
        }

        // ---- background ------------------------------------------------------------------------------------------------

        private static SegmentationBackgroundShapes NeighbourShapes()
        {
            // Mosaic shapes; the stretch triples X into volume. Structure 5 is the one being segmented.
            List<LocationObj> visible =
            [
                Location(11, 9, LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(110 100, 100 110, 90 100, 100 90, 110 100))"),
                Location(12, 9, LocationType.POLYGON, "POLYGON((10 10, 60 10, 60 60, 10 60, 10 10))"),
                Location(13, 9, LocationType.POLYLINE, "LINESTRING(20 500, 80 520, 120 560)"),
                Location(14, 9, LocationType.CURVEPOLYGON, "POLYGON((2000 10, 2100 10, 2100 100, 2000 100, 2000 10))"),
                Location(15, 5, LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(210 200, 200 210, 190 200, 200 190, 210 200))"),
            ];
            return CircleSegmentationPrompts.CreateOtherStructureBackgroundShapes(
                visible, new StretchTransform(), excludeLocationIds: [1], excludeStructureId: 5, structureParentLookup: _ => null,
                curveInterpolations: closed => closed ? 6u : 4u);
        }

        [TestMethod]
        public void NeighboursAreSentAsTheirOwnShapesCirclesWithTheMeanScaleRadius()
        {
            SegmentationBackgroundShapes shapes = NeighbourShapes();

            Assert.AreEqual(4, shapes.Count, "the location of the segmented structure is not background");
            List<ProtoShape> all = [.. shapes.ToShapes(Enumerable.Range(0, shapes.Count), 1)];
            CollectionAssert.AreEqual(
                new[] { ProtoShape.KindOneofCase.Circle, ProtoShape.KindOneofCase.Polygon, ProtoShape.KindOneofCase.Line, ProtoShape.KindOneofCase.Polygon },
                all.Select(shape => shape.KindCase).ToArray());
            Assert.AreEqual(300.0, all[0].Circle.Center.X, 1e-9);
            Assert.AreEqual(20.0, all[0].Circle.Radius, 1e-3, "the mosaic radius 10 (read from its ring) times the mean scale 2");
            Assert.IsFalse(all[2].Line.Closed);
            Assert.AreEqual(3, all[2].Line.Points.Count);
            Assert.IsNull(all[2].Line.Fit, "a polyline has no fit");
            Assert.AreEqual(2.0, all[2].Line.Width, 1e-9, "the minimum width 1 carried by the mean scale");
            Assert.IsNull(all[1].Polygon.Fit);
            Assert.AreEqual(Viking.gRPC.Geometry.V1.CurveMethod.CatmullRom, all[3].Polygon.Fit.Method, "a curve polygon keeps its control points and fit");
            Assert.AreEqual(6u, all[3].Polygon.Fit.Interpolations, "the closed-curve interpolations");
        }

        [TestMethod]
        public void TheStartCarriesTheNeighboursReachingItsTilesAndAnAnswerCarriesTheRestOnce()
        {
            SegmentationBackgroundShapes shapes = NeighbourShapes();
            HashSet<int> sent = [];

            SegmentationViewportSession.StartMessage start = Start(1, [new Vector2(500, 500)], background: [new Vector2(10, 10)], backgroundShapes: shapes, sent: sent);

            SegmentationServiceTypes.Feature background = start.Request.BackgroundFeature;
            Assert.AreEqual(1, background.Points.Count);
            Assert.AreEqual(3, background.Shapes.Count, "the circle, polygon and line reach tile (0, 0); the curve polygon at x 6000 does not");

            // The object grows into the cell holding the curve polygon (volume x 6000..6300 is column 5).
            SegmentationServiceTypes.TilesNeeded needed = new();
            needed.BackgroundCells.Add(new GridCell { Row = 0, Col = 5 });
            needed.BackgroundCells.Add(new GridCell { Row = 0, Col = 0 });
            SegmentationServiceTypes.TilesAnswer answer = new();
            SegmentationViewportSession.AddBackgroundAnswer(answer, needed, shapes, 1, sent);

            Assert.AreEqual(ProtoShape.KindOneofCase.Polygon, answer.BackgroundShapes.Single().KindCase, "the shapes of tile (0, 0) went with start");
            Assert.AreEqual(6000.0, answer.BackgroundShapes[0].Polygon.Exterior.Points[0].X, 1e-9);

            SegmentationServiceTypes.TilesAnswer again = new();
            SegmentationViewportSession.AddBackgroundAnswer(again, needed, shapes, 1, sent);
            Assert.AreEqual(0, again.BackgroundShapes.Count, "nothing is sent twice");
        }

        [TestMethod]
        public void NeighboursGoOnlyAsShapesAndTheBackgroundClicksAreOnlyTheOnesTheUserPlaced()
        {
            SegmentationBackgroundShapes shapes = NeighbourShapes();

            // No click placed: the neighbours reach the server as shapes, and no click is derived from them.
            SegmentationServiceTypes.Feature shapesOnly = Start(1, [new Vector2(500, 500)], backgroundShapes: shapes).Request.BackgroundFeature;
            Assert.AreEqual(0, shapesOnly.Points.Count, "no background click is derived from a neighbour");
            Assert.AreEqual(3, shapesOnly.Shapes.Count, "the neighbours reaching tile (0, 0)");

            // One click placed: it is the only background point, unchanged, and the shapes are the same.
            Vector2 placed = new(700, 40);
            SegmentationServiceTypes.Feature withClick = Start(
                1, [new Vector2(500, 500)], background: [placed], backgroundShapes: shapes).Request.BackgroundFeature;
            Assert.AreEqual(1, withClick.Points.Count, "only the user's click");
            Assert.AreEqual(placed.X, withClick.Points[0].X, 1e-9);
            Assert.AreEqual(placed.Y, withClick.Points[0].Y, 1e-9);
            Assert.AreEqual(3, withClick.Shapes.Count);

            // Nothing turns annotations into background clicks any more.
            Assert.IsFalse(
                typeof(CircleSegmentationPrompts)
                    .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Any(method => typeof(IEnumerable<Vector2>).IsAssignableFrom(method.ReturnType) &&
                                   method.GetParameters().Any(parameter => typeof(IEnumerable<LocationObj>).IsAssignableFrom(parameter.ParameterType))),
                "no method derives points from other annotations");
        }

        [TestMethod]
        public void AMessageCarriesAtMostTheServersShapeLimitAndAFitAtMostItsInterpolationLimit()
        {
            SegmentationBackgroundShapes shapes = new();
            for (int i = 0; i < SegmentationBackgroundShapes.MaxShapesPerMessage + 10; i++)
                shapes.AddCircle(new Circle(new Vector2(10 + i, 10), 5));
            HashSet<int> sent = [];

            Assert.AreEqual(SegmentationBackgroundShapes.MaxShapesPerMessage, shapes.TakeReaching([new TileCell(0, 0)], 1, sent).Count);
            Assert.AreEqual(10, shapes.TakeReaching([new TileCell(0, 0)], 1, sent).Count, "the rest go with the next message for the cell");
            Assert.AreEqual(SegmentationPromptShapes.MaxInterpolations, SegmentationPromptShapes.CatmullRom(500).Interpolations);
        }

        // ---- painted bitmaps -------------------------------------------------------------------------------------------

        [TestMethod]
        public void APaintedMaskIsSentAsTheBitmapsTilesRightAfterStartAndCounted()
        {
            Vector2[] ring = [new(10, 10), new(2500, 10), new(2500, 1500), new(10, 1500)];
            RasterMask painted = RasterMask.FromRings([ring], downsample: 1, marginPixels: 0)!;
            SegmentationSeed seed = SegmentationSeed.FromMask(painted)!.Value;

            SegmentationViewportSession.StartMessage start = Start(1, [seed.Center], [seed]);
            List<SegmentationServiceTypes.SegmentTilesStreamRequest> messages = [.. start.Messages()];

            Assert.AreEqual(0, start.Request.ForegroundFeature.Shapes.Count, "a bitmap is not a shape");
            Assert.AreEqual(6u, start.Request.MaskTileCount, "three columns by two rows of tiles");
            Assert.AreEqual(SegmentationServiceTypes.SegmentTilesStreamRequest.BodyOneofCase.Start, messages[0].BodyCase);
            List<SegmentationServiceTypes.MaskTile> tiles = [.. messages.Skip(1).SelectMany(message => message.MaskTiles.Tiles)];
            Assert.AreEqual(6, tiles.Count);
            Assert.IsTrue(tiles.All(tile => tile.Kind == MaskKind.Foreground));
            CollectionAssert.AreEquivalent(
                new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) },
                tiles.Select(tile => (tile.Cell.Row, tile.Cell.Col)).ToArray());
        }

        [TestMethod]
        public void APaintedBackgroundIsSentAsBackgroundTilesCountedWithTheForeground()
        {
            RasterMask foreground = RasterMask.FromRings([[new(10, 10), new(900, 10), new(900, 900), new(10, 900)]], downsample: 1, marginPixels: 0)!;
            RasterMask background = RasterMask.FromRings([[new(1100, 10), new(1500, 10), new(1500, 400), new(1100, 400)]], downsample: 1, marginPixels: 0)!;
            SegmentationSeed seed = SegmentationSeed.FromMask(foreground)!.Value;
            SegmentationMaskUnion paintedBackground = new();
            paintedBackground.Add(SegmentationSeed.FromMask(background)!.Value);

            SegmentationViewportSession.StartMessage start = SegmentationViewportSession.BuildStart(
                Signature(1), [new TileCell(0, 0)], [seed.Center], null, [seed], null, new HashSet<int>(),
                pointRadiusPixels: 0, maskTileScale: 1, backgroundBitmap: paintedBackground);
            List<SegmentationServiceTypes.MaskTile> tiles = [.. start.Messages().Skip(1).SelectMany(message => message.MaskTiles.Tiles)];

            Assert.AreEqual(2u, start.Request.MaskTileCount, "one foreground and one background tile, counted together");
            CollectionAssert.AreEqual(
                new[] { (MaskKind.Foreground, 0, 0), (MaskKind.Background, 0, 1) },
                tiles.Select(tile => (tile.Kind, tile.Cell.Row, tile.Cell.Col)).ToArray(),
                "the foreground first, then the background");
        }

        // ---- answers ---------------------------------------------------------------------------------------------------

        [TestMethod]
        public async Task AnAnswerListsOnlyTheTilesThatCouldNotBeUploaded()
        {
            SegmentationServiceTypes.TilesNeeded needed = new();
            needed.Tiles.Add(new GridCell { Row = 0, Col = 0 });
            needed.Tiles.Add(new GridCell { Row = 0, Col = 1 });
            needed.Tiles.Add(new GridCell { Row = 1, Col = 0 });
            List<(int, int)> uploaded = [];
            int ready = 0;

            SegmentationServiceTypes.TilesAnswer answer = await SegmentationViewportSession.AnswerNeededTilesAsync(
                needed,
                cell =>
                {
                    uploaded.Add((cell.Row, cell.Col));
                    return Task.FromResult(cell.Col != 1);
                },
                () => ready++);

            CollectionAssert.AreEqual(new[] { (0, 0), (0, 1), (1, 0) }, uploaded, "every tile is tried, in order");
            Assert.AreEqual(3, ready);
            CollectionAssert.AreEqual(new[] { (0, 1) }, answer.Unavailable.Select(cell => (cell.Row, cell.Col)).ToArray());

            SegmentationServiceTypes.TilesAnswer allUploaded = await SegmentationViewportSession.AnswerNeededTilesAsync(needed, _ => Task.FromResult(true));
            Assert.AreEqual(0, allUploaded.Unavailable.Count, "an uploaded tile is settled by its upload, not listed");
        }

        // ---- results ---------------------------------------------------------------------------------------------------

        [TestMethod]
        public void LogitsPlacedByTheirOriginLandWhereTheOldResponseOriginPutThem()
        {
            // The old protocol placed a segment in a response image: the image's bottom-left at (ox, oy) in grid pixels, the
            // image H samples of s pixels tall, and the segment's logits at image pixel (lx, ly), Y down from the image top.
            // The same logits now carry their own origin, the pixel index of sample (0, 0)'s top-left pixel, Y up:
            // (ox + lx, oy + H * s - 1 - ly).
            const int ox = 1234, oy = 567, width = 60, height = 40, scale = 4, lx = 36, ly = 28;
            const int downsample = 2;
            SegmentationServiceTypes.SegmentResult island = SegmentationLogitFieldTests.SegmentWithLogits(
                14, 9, scale, ox + lx, oy + (height * scale) - 1 - ly,
                (column, row) => (float)(6 - Math.Abs(column - 6.3) - Math.Abs(row - 4.2) * 1.5));
            SegmentField field = SegmentationLogitField.Decode(island, iso: 0, sigma: 0);

            Rectangle oldBounds = new(
                new Vector2(ox * (double)downsample, oy * (double)downsample),
                new Vector2((ox + width * scale) * (double)downsample, (oy + height * scale) * (double)downsample));
            IReadOnlyList<Polygon> old = SegmentationMaskPolygonizer.CreatePolygons(
                field.Data!, field.Width, field.Height, lx / scale, ly / scale, width, height,
                SegmentationMaskPolygonizer.CenterSampleBounds(oldBounds, width, height, scale),
                0, null, 0, out _);

            Rectangle newBounds = SegmentationViewportSession.FieldWorldBounds(field, downsample);
            IReadOnlyList<Polygon> placed = SegmentationMaskPolygonizer.CreatePolygons(
                field.Data!, field.Width, field.Height, 0, 0, field.Width, field.Height,
                SegmentationMaskPolygonizer.CenterSampleBounds(newBounds, field.Width, field.Height, scale),
                0, null, 0, out _);

            Assert.AreEqual(1, old.Count);
            Assert.AreEqual(1, placed.Count);
            Vector2[] a = old[0].ExteriorRing;
            Vector2[] b = placed[0].ExteriorRing;
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 1e-6, $"vertex {i}");
                Assert.AreEqual(a[i].Y, b[i].Y, 1e-6, $"vertex {i}");
            }

            Assert.AreEqual(SegmentationViewportSession.SegmentWorldBounds(island, downsample), newBounds);
            Assert.AreEqual((ox + lx) * (double)downsample, newBounds.Left, 1e-9);
            Assert.AreEqual((oy + height * scale - ly) * (double)downsample, newBounds.Top, 1e-9);
        }
    }
}
