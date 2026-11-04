using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.gRPC.Geometry.V1;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.SegmentationPrompts.Geometry;
using GeometryPoint = Viking.SegmentationPrompts.Geometry.Point;

namespace Viking.SegmentationPrompts.Tests
{
    [TestClass]
    public class PromptBuilderTests
    {
        private const long Cell = 1;
        private const long Synapse = 34;          // a type whose parent is not Cell
        private const long CellChildType = 80;    // a type whose parent is Cell (e.g. a cell subtype)

        // Structures: 10 the target's cell, 11 its child, 12 the child's child, 20 another cell, 21 a cell-subtype
        // structure, 30 a synapse.
        private static readonly PromptStructure[] Structures =
        {
            new PromptStructure { Id = 10, TypeId = Cell },
            new PromptStructure { Id = 11, ParentId = 10, TypeId = Cell },
            new PromptStructure { Id = 12, ParentId = 11, TypeId = Cell },
            new PromptStructure { Id = 20, TypeId = Cell },
            new PromptStructure { Id = 21, TypeId = CellChildType, TypeParentId = Cell },
            new PromptStructure { Id = 30, ParentId = 20, TypeId = Synapse, TypeParentId = 0 },
        };

        private static PromptAnnotation Circle(long id, double x, double y, double radius, long structure = 10, long ticks = 1000) =>
            new PromptAnnotation { Id = id, TypeCode = 1, X = x, Y = y, Radius = radius, StructureId = structure, LastModifiedTicks = ticks };

        private static PromptAnnotation Area(long id, int typeCode, string wkt, long structure = 10, long ticks = 1000) =>
            new PromptAnnotation { Id = id, TypeCode = typeCode, MosaicWkt = wkt, StructureId = structure, LastModifiedTicks = ticks };

        private static string Wkt(params IEnumerable<GeometryPoint>[] rings) =>
            "POLYGON (" + string.Join(", ", rings.Select(ring =>
                "(" + string.Join(", ", ring.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:R} {1:R}", p.X, p.Y))) + ")")) + ")";

        private static string Square(double x0, double y0, double size) => Wkt(new[]
        {
            new GeometryPoint(x0, y0), new GeometryPoint(x0 + size, y0), new GeometryPoint(x0 + size, y0 + size),
            new GeometryPoint(x0, y0 + size), new GeometryPoint(x0, y0),
        });

        private static PromptInput Input(IEnumerable<PromptAnnotation> targets, IEnumerable<PromptAnnotation>? candidates = null)
        {
            var input = new PromptInput { Volume = "RC2", Section = 1305, Tileset = "TEM", AnnotationContextId = 7, ModelId = "v5" };
            input.Targets.AddRange(targets);
            input.Candidates.AddRange(candidates ?? Enumerable.Empty<PromptAnnotation>());
            input.Structures.AddRange(Structures);
            return input;
        }

        private static SegmentTilesRequest Start(PromptInput input) =>
            SegmentTilesRequest.Parser.ParseFrom(PromptBuilder.BuildStart(input.ToByteArray()));

        private static List<(double X, double Y)> Points(Ring ring) => ring.Points.Select(p => (p.X, p.Y)).ToList();

        private static List<(double X, double Y)> Open(List<GeometryPoint> ring) =>
            ring.Take(ring.Count - 1).Select(p => (p.X, p.Y)).ToList();

        [TestMethod]
        public void ACircleIsItsTrueDiscAndOneClickAtItsCentreOnTheTilesetsMosaicGrid()
        {
            SegmentTilesRequest start = Start(Input(new[] { Circle(5, 1500.25, 700.5, 80, ticks: 638000000000000000) }));

            Assert.AreEqual("RC2", start.Grid.Volume);
            Assert.AreEqual(1305, start.Grid.Section);
            Assert.AreEqual("TEM", start.Grid.Channel);
            Assert.AreEqual("TEM", start.Grid.Transform);
            Assert.AreEqual(1, start.Grid.Downsample);
            Assert.AreEqual(CoordinateSpace.Mosaic, start.Grid.Space);
            CollectionAssert.AreEqual(new[] { (0, 1) }, start.Tiles.Select(c => (c.Row, c.Col)).ToArray());
            Assert.AreEqual(1, start.ForegroundFeature.Shapes.Count);
            Assert.AreEqual(80, start.ForegroundFeature.Shapes[0].Circle.Radius, "the TRUE radius: the server scales it");
            Assert.AreEqual((1500.25, 700.5), (start.ForegroundFeature.Points.Single().X, start.ForegroundFeature.Points.Single().Y));
            Assert.IsNull(start.CorrectionFeature);
            Assert.AreEqual((5L, 638000000000000000L), (start.Locations.Single().Id, start.Locations.Single().LastModifiedTicks));
            Assert.AreEqual(7, start.AnnotationContextId);
            Assert.AreEqual("v5", start.ModelId);
            Assert.IsFalse(start.HasMaskThreshold);
            Assert.IsFalse(start.HasJobId);
        }

        [TestMethod]
        public void ACurvePolygonIsAnOutlineToCorrectWithEveryRingFittedAsTheExporterFitsItAndNoClick()
        {
            var fixtures = ClosedCurveFitTests.Fixtures();
            List<GeometryPoint> exterior = fixtures["real_cell"].Control;
            var (hole, fittedHole) = fixtures["triangle"];
            List<GeometryPoint> holeInside = hole.Select(p => new GeometryPoint(p.X + 24640, p.Y + 31600)).ToList();
            // The fit itself is checked against the exporter in ClosedCurveFitTests.
            List<GeometryPoint> fittedExterior = ClosedCurveFit.FitClosedRing(exterior);
            List<GeometryPoint> fittedHoleInside = ClosedCurveFit.FitClosedRing(holeInside);

            SegmentTilesRequest start = Start(Input(new[] { Area(5, 6, Wkt(exterior, holeInside)) }));

            Assert.IsNull(start.ForegroundFeature, "no seed and no click: the server clicks the island's deepest pixel");
            Polygon polygon = start.CorrectionFeature.Shapes.Single().Polygon;
            CollectionAssert.AreEqual(Open(fittedExterior), Points(polygon.Exterior), "fitted, without the closing point");
            CollectionAssert.AreEqual(Open(fittedHoleInside), Points(polygon.Holes.Single()));
            Assert.IsNull(polygon.Fit, "the ring is sent fitted, not as control points");
            Assert.AreEqual(fittedHole.Count, fittedHoleInside.Count);
        }

        [TestMethod]
        public void APolygonIsSentAsDrawnWithoutItsClosingPoint()
        {
            SegmentTilesRequest start = Start(Input(new[] { Area(5, 4, Square(100, 200, 50)) }));

            CollectionAssert.AreEqual(
                new[] { (100.0, 200.0), (150.0, 200.0), (150.0, 250.0), (100.0, 250.0) },
                Points(start.CorrectionFeature.Shapes.Single().Polygon.Exterior));
        }

        [TestMethod]
        public void AMergedPairStartsOnEachTargetsOwnCellsInRowOrderWithBothLocations()
        {
            SegmentTilesRequest start = Start(Input(new[] { Circle(9, 5000, 5000, 50), Circle(4, 500, 500, 50) }));

            CollectionAssert.AreEqual(new[] { (0, 0), (4, 4) }, start.Tiles.Select(c => (c.Row, c.Col)).ToArray(), "not the cells between");
            CollectionAssert.AreEqual(new long[] { 4, 9 }, start.Locations.Select(l => l.Id).ToArray());
            Assert.AreEqual(500, start.ForegroundFeature.Points[0].X, "shapes and clicks in location id order");
        }

        [TestMethod]
        public void TheBackgroundIsOtherCellsNearTheTargetNotItsStructureOrDescendantsNorOtherTypes()
        {
            PromptAnnotation[] candidates =
            {
                Circle(101, 600, 600, 40, structure: 20),                 // another cell: background
                Circle(102, 650, 650, 40, structure: 10),                 // the target's structure
                Circle(103, 660, 660, 40, structure: 12),                 // a descendant of it
                Area(104, 4, Square(300, 300, 60), structure: 21),        // a cell-subtype structure: background
                Circle(105, 700, 700, 10, structure: 30),                 // a synapse: not what v5 saw as negative
                Area(106, 3, "POLYGON ((0 0, 10 0, 10 10, 0 0))", structure: 20), // a polyline type code
                Circle(107, 5000, 5000, 40, structure: 20),               // out of reach
                Circle(5, 500, 500, 50),                                  // the target itself
                Circle(108, 610, 610, 40, structure: 999),                // an unknown structure
            };

            SegmentTilesRequest start = Start(Input(new[] { Circle(5, 500, 500, 50) }, candidates));

            CollectionAssert.AreEqual(new long[] { 101, 104 }, start.Neighbors.Select(n => n.Id).ToArray());
            Assert.AreEqual(2, start.BackgroundFeature.Shapes.Count);
            Assert.AreEqual(40, start.BackgroundFeature.Shapes[0].Circle.Radius, "a circle neighbour at its TRUE radius");
        }

        [TestMethod]
        public void BackgroundBeyondTheStartCellsIsListedAtOnceAndSentWhenTheServerAsks()
        {
            PromptAnnotation[] candidates = { Circle(101, 600, 600, 40, structure: 20), Circle(102, 1500, 600, 40, structure: 20) };
            PromptInput input = Input(new[] { Circle(5, 500, 500, 50) }, candidates);

            SegmentTilesRequest start = Start(input);
            CollectionAssert.AreEqual(new long[] { 101, 102 }, start.Neighbors.Select(n => n.Id).ToArray(), "the key lists both");
            Assert.AreEqual(600, start.BackgroundFeature.Shapes.Single().Circle.Center.X, "only the start cell's neighbour");

            PromptBackground Ask(int row, int col, params long[] sent)
            {
                var needed = new TilesNeeded();
                needed.BackgroundCells.Add(new GridCell { Row = row, Col = col });
                return PromptBackground.Parser.ParseFrom(PromptBuilder.BackgroundFor(input.ToByteArray(), needed.ToByteArray(), sent));
            }

            CollectionAssert.AreEqual(new long[] { 102 }, Ask(0, 1).SentIds.ToArray());
            Assert.AreEqual(1500, Ask(0, 1).Shapes.Single().Circle.Center.X);
            Assert.AreEqual(0, Ask(0, 1, 102).Shapes.Count, "sent before");
            Assert.AreEqual(0, Ask(0, 0).Shapes.Count, "the start cell's background went with the start");
        }

        [TestMethod]
        public void TheReachIsTheTargetsCellsGrownByOneCell()
        {
            PromptInput input = Input(new[] { Circle(5, 1500, 700, 50) });

            Rectangle reach = PromptReach.Parser.ParseFrom(PromptBuilder.Reach(input.ToByteArray())).Reach;

            Assert.AreEqual((0.0, -1024.0, 3072.0, 2048.0), (reach.XMin, reach.YMin, reach.XMax, reach.YMax));
        }

        [TestMethod]
        public void TheBytesDependOnTheInputsContentNotItsOrder()
        {
            PromptAnnotation[] targets = { Circle(9, 900, 900, 30), Circle(4, 500, 500, 50) };
            PromptAnnotation[] candidates =
            {
                Circle(101, 600, 600, 40, structure: 20), Area(104, 4, Square(300, 300, 60), structure: 21),
                Circle(103, 800, 800, 30, structure: 20),
            };

            byte[] forward = PromptBuilder.BuildStart(Input(targets, candidates).ToByteArray());
            byte[] backward = PromptBuilder.BuildStart(Input(targets.Reverse(), candidates.Reverse()).ToByteArray());

            CollectionAssert.AreEqual(forward, backward);
        }

        [TestMethod]
        public void TheRequestForAKnownInputIsUnchanged()
        {
            // Changing these bytes changes every client's cache key: do it only when the prompt rules change on purpose.
            PromptInput input = Input(
                new[] { Area(5, 6, Wkt(ClosedCurveFitTests.Fixtures()["real_cell"].Control), ticks: 638600000000000000) },
                new[] { Circle(101, 24500, 31500, 40, structure: 20, ticks: 638500000000000000) });
            input.MaskThreshold = 0.5f;
            input.RequestId = 77;
            input.JobId = 3;

            string digest = Convert.ToHexString(SHA256.HashData(PromptBuilder.BuildStart(input.ToByteArray())));

            Assert.AreEqual(GoldenDigest, digest);
            // The same input as a file, for the native build's check on Linux (Native/check_native.py). Set
            // SP_WRITE_GOLDEN=1 to rewrite it after changing the input above.
            string fixture = Path.Combine(FixturesSource(), "golden_prompt_input.hex");
            string hex = Convert.ToHexString(input.ToByteArray());
            if (Environment.GetEnvironmentVariable("SP_WRITE_GOLDEN") == "1")
                File.WriteAllText(fixture, hex + "\n");
            Assert.AreEqual(hex, File.ReadAllText(fixture).Trim(), "golden_prompt_input.hex is the input above");
        }

        private static string FixturesSource([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures");

        /// <summary>SHA-256 of that request; the gallery's native build on Linux must give the same (Native README).</summary>
        internal const string GoldenDigest = "3DF6A759AAC0978266EA1E7410BC61EBEF1FB1B04359792C1B27D87DD26821D6";

        [TestMethod]
        public void CirclesAndAreasInOnePromptAreRefused()
        {
            PromptInput input = Input(new[] { Circle(4, 500, 500, 50), Area(5, 4, Square(100, 200, 50)) });

            Assert.ThrowsException<ArgumentException>(() => PromptBuilder.BuildStart(input.ToByteArray()));
        }

        [TestMethod]
        public void APromptOverSixtyFourTilesIsTooLarge()
        {
            PromptInput input = Input(new[] { Area(5, 4, Square(0, 0, 9 * 1024 - 1)) });

            Assert.ThrowsException<PromptTooLargeException>(() => PromptBuilder.BuildStart(input.ToByteArray()));
        }

        [TestMethod]
        public void ATargetThatIsNotACircleOrAnAreaIsRefused()
        {
            PromptInput input = Input(new[] { Area(5, 3, "POLYGON ((0 0, 10 0, 10 10, 0 0))") });

            Assert.ThrowsException<ArgumentException>(() => PromptBuilder.BuildStart(input.ToByteArray()));
        }
    }
}
