using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking.gRPC.SegmentationServiceTypes.V1;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using TileGrid = Viking.gRPC.Geometry.V1.TileGrid;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="SegmentationViewportSession"/> runs over any <see cref="ISegmentationViewport"/> (Jotunn's scene host),
    /// not only Viking's section viewer.
    /// </summary>
    [TestClass]
    public class SegmentationViewportInterfaceTests
    {
        sealed class FakeViewport(bool inVolumeSpace = true) : ISegmentationViewport
        {
            public double CameraDownsample => 2;
            public int ViewportWidth => 100;
            public int ViewportHeight => 50;
            public Geometry.Vector2 ScreenToWorld(double x, double y) => new(x * 2, y * 2);
            public int SectionNumber => 7;
            public string VolumeName => "Test";
            public bool InVolumeSpace => inVolumeSpace;
            public string VolumeTransform => inVolumeSpace ? "Grid" : string.Empty;
            public string SectionTransform => "Grid";
            public string Channel => "TEM";
            public bool IsGrayscale => true;

            public Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY, int sectionNumber,
                bool showOverlays, bool asyncTextureLoad, CancellationToken token) => Task.FromResult<RenderTarget2D>(null);
        }

        [TestMethod]
        public void ViewportBoundsComeFromTheInterface()
        {
            SegmentationViewportSession session = new(new FakeViewport());

            Geometry.Rectangle bounds = session.GetCurrentViewportBounds();

            Assert.AreEqual(new Geometry.Rectangle(new Geometry.Vector2(0, 0), new Geometry.Vector2(200, 100)), bounds);
        }

        [DataTestMethod]
        [DataRow(true, "Grid|Grid", CoordinateSpace.Volume)]
        [DataRow(false, "|Grid", CoordinateSpace.Mosaic)]
        public void TilesNameTheViewportsSpace(bool inVolumeSpace, string transform, CoordinateSpace space)
        {
            var signature = SegmentationViewportSession.TileSignature.Of(new FakeViewport(inVolumeSpace), 4);

            TileGrid grid = SegmentationViewportSession.ToGrid(signature);

            Assert.AreEqual(space, grid.Space);
            Assert.AreEqual(transform, grid.Transform);
            Assert.AreEqual("Test", grid.Volume);
            Assert.AreEqual(7, grid.Section);
            Assert.AreEqual("TEM", grid.Channel);
            Assert.AreEqual(4, grid.Downsample);
        }

        [TestMethod]
        public void AMosaicTileAndAVolumeTileHaveDifferentKeys()
        {
            var volume = SegmentationViewportSession.TileSignature.Of(new FakeViewport(true), 1);
            var mosaic = SegmentationViewportSession.TileSignature.Of(new FakeViewport(false), 1);

            Assert.AreNotEqual(SegmentationViewportSession.TileKey(volume, 0, 0), SegmentationViewportSession.TileKey(mosaic, 0, 0));
        }

        [TestMethod]
        public async Task HeldCellsAreTheOnesTheServerSaysItHoldsAskedInBatches()
        {
            var signature = SegmentationViewportSession.TileSignature.Of(new FakeViewport(false), 2);
            List<TileCell> cells = [.. Enumerable.Range(0, 300).Select(i => new TileCell(i, -i))];
            List<HeldTilesRequest> asked = [];

            HashSet<TileCell> held = await SegmentationViewportSession.HeldCellsAsync(
                request =>
                {
                    asked.Add(request);
                    HeldTilesResponse response = new();
                    response.Held.AddRange(request.Cells.Select(cell => cell.Row % 3 == 0));
                    return Task.FromResult(response);
                },
                signature, cells, 42, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { 256, 44 }, asked.Select(r => r.Cells.Count).ToArray());
            Assert.IsTrue(asked.All(r => r.AnnotationContextId == 42 && r.Grid.Space == CoordinateSpace.Mosaic &&
                                         r.Grid.Transform == "|Grid" && r.Grid.Downsample == 2 && r.Grid.Section == 7));
            CollectionAssert.AreEquivalent(cells.Where(c => c.Row % 3 == 0).ToList(), held.ToList());
        }

        [TestMethod]
        public async Task AServerWithoutGetHeldTilesHoldsNothingSoEveryTileIsUploaded()
        {
            var signature = SegmentationViewportSession.TileSignature.Of(new FakeViewport(), 1);

            HashSet<TileCell> held = await SegmentationViewportSession.HeldCellsAsync(
                _ => throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unimplemented, "no such method")),
                signature, [new TileCell(0, 0)], 0, CancellationToken.None);

            Assert.AreEqual(0, held.Count);
        }

        [TestMethod]
        public async Task AnAnswerOfTheWrongLengthHoldsNothing()
        {
            var signature = SegmentationViewportSession.TileSignature.Of(new FakeViewport(), 1);

            HashSet<TileCell> held = await SegmentationViewportSession.HeldCellsAsync(
                _ => Task.FromResult(new HeldTilesResponse { Held = { true } }),
                signature, [new TileCell(0, 0), new TileCell(0, 1)], 0, CancellationToken.None);

            Assert.AreEqual(0, held.Count);
        }

        [TestMethod]
        public void CirclePromptIsFourClicksInsideTheCircle()
        {
            var circle = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(40 50, 50 40, 60 50, 50 60, 40 50))");

            Assert.IsTrue(WebAnnotation.Tools.SegmentTool.TryCreateCirclePrompt(
                circle, WebAnnotationTests.Actions.ChangeTypeActionTests.CreateBoundedTransform(), out var prompt));

            Assert.AreEqual(4, prompt.Points.Count);
            foreach (Geometry.Vector2 point in prompt.Points)
                Assert.IsTrue(Geometry.Vector2.Distance(point, new Geometry.Vector2(50, 50)) < 10);
        }

        [TestMethod]
        public async Task SegmentingWithoutAServiceReportsNoClient()
        {
            SegmentationViewportSession session = new(new FakeViewport());

            var response = await session.SegmentAsync([new Geometry.Vector2(10, 10)], [], CancellationToken.None);

            Assert.IsNull(response);
            Assert.AreEqual(SegmentationSkipKind.NoClient, session.ConsumeLastSkip(out _));
        }
    }
}
