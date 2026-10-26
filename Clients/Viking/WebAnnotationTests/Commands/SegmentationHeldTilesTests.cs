using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.gRPC.SegmentationServiceTypes.V1;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The space a tile names and the GetHeldTiles question the prefetch asks before uploading.
    /// </summary>
    [TestClass]
    public class SegmentationHeldTilesTests
    {
        private static SegmentationViewportSession.TileSignature Signature(CoordinateSpace space, string transform = "|Grid", int downsample = 2) =>
            new("Test", 7, "TEM", transform, downsample, space);

        [DataTestMethod]
        [DataRow(CoordinateSpace.Volume, "Grid|Grid")]
        [DataRow(CoordinateSpace.Mosaic, "|Grid")]
        public void TilesNameTheViewportsSpace(CoordinateSpace space, string transform)
        {
            TileCoord coord = SegmentationViewportSession.ToCoord(Signature(space, transform, 4), new TileCell(2, -3));

            Assert.AreEqual(space, coord.Space);
            Assert.AreEqual(transform, coord.Transform);
            Assert.AreEqual("Test", coord.Volume);
            Assert.AreEqual(7, coord.Section);
            Assert.AreEqual("TEM", coord.Channel);
            Assert.AreEqual(4, coord.Downsample);
            Assert.AreEqual(2, coord.Row);
            Assert.AreEqual(-3, coord.Col);
        }

        [TestMethod]
        public void AMosaicTileAndAVolumeTileHaveDifferentKeys()
        {
            Assert.AreNotEqual(
                SegmentationViewportSession.TileKey(Signature(CoordinateSpace.Volume), 0, 0),
                SegmentationViewportSession.TileKey(Signature(CoordinateSpace.Mosaic), 0, 0));
        }

        [TestMethod]
        public async Task HeldCellsAreTheOnesTheServerSaysItHoldsAskedInBatches()
        {
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
                Signature(CoordinateSpace.Mosaic), cells, 42, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { 256, 44 }, asked.Select(r => r.Cells.Count).ToArray());
            Assert.IsTrue(asked.All(r => r.AnnotationContextId == 42 && r.Grid.Space == CoordinateSpace.Mosaic &&
                                         r.Grid.Transform == "|Grid" && r.Grid.Downsample == 2 && r.Grid.Section == 7));
            CollectionAssert.AreEquivalent(cells.Where(c => c.Row % 3 == 0).ToList(), held.ToList());
        }

        [TestMethod]
        public async Task AServerWithoutGetHeldTilesHoldsNothingSoEveryTileIsUploaded()
        {
            HashSet<TileCell> held = await SegmentationViewportSession.HeldCellsAsync(
                _ => throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unimplemented, "no such method")),
                Signature(CoordinateSpace.Volume), [new TileCell(0, 0)], 0, CancellationToken.None);

            Assert.AreEqual(0, held.Count);
        }

        [TestMethod]
        public async Task AnAnswerOfTheWrongLengthHoldsNothing()
        {
            HashSet<TileCell> held = await SegmentationViewportSession.HeldCellsAsync(
                _ => Task.FromResult(new HeldTilesResponse { Held = { true } }),
                Signature(CoordinateSpace.Volume), [new TileCell(0, 0), new TileCell(0, 1)], 0, CancellationToken.None);

            Assert.AreEqual(0, held.Count);
        }
    }
}
