using AnnotationService.Types;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.CellOverlap;
using WebAnnotationModel;
using WebAnnotationTests.Tools;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="CellOverlapIndex"/> recomputes the overlap regions only when a location's shape or the transform changes,
    /// and the regions it keeps are in the volume space of the transform it was last given.
    /// </summary>
    [TestClass]
    public class CellOverlapIndexTests
    {
        private static Polygon Box(double left, double bottom, double size) =>
            new([new Vector2(left, bottom), new Vector2(left + size, bottom), new Vector2(left + size, bottom + size),
                 new Vector2(left, bottom + size), new Vector2(left, bottom)]);

        private static LocationObj Cell(long id, long structureId, double left) =>
            new(new Location
            {
                ID = id,
                ParentID = structureId,
                TypeCode = (short)LocationType.POLYGON,
                Section = 1,
                MosaicShapeWKB = Box(left, 0, 10).ToSqlGeometry().AsBinary(),
                VolumeShapeWKB = Box(left, 0, 10).ToSqlGeometry().AsBinary()
            });

        private static readonly LocationObj[] TwoOverlappingCells = [Cell(1, 101, 0), Cell(2, 102, 5)];

        private static bool Update(CellOverlapIndex index, IVolumeToSectionTransform transform) =>
            index.Update(TwoOverlappingCells, transform, _ => null);

        [TestMethod]
        public void ANewTransformMovesTheRegionsWithIt()
        {
            CellOverlapIndex index = new();
            Assert.IsTrue(Update(index, new ScaleTransform(1, Vector2.Zero)));
            Assert.AreEqual(1, index.Regions.Count);
            Assert.AreEqual(5, index.Regions[0].Shape.BoundingBox.Left, 1e-6);

            Assert.IsTrue(Update(index, new ScaleTransform(2, new Vector2(1000, 0))));

            Assert.AreEqual(1, index.Regions.Count);
            Assert.AreEqual(1010, index.Regions[0].Shape.BoundingBox.Left, 1e-6, "the region is mapped through the new transform");
            Assert.AreEqual(4 * 5 * 10, index.Regions[0].Shape.Area, 1e-6);
            Assert.IsTrue(index.TryHit(new Vector2(1015, 10), out _));
            Assert.IsFalse(index.TryHit(new Vector2(7, 5), out _), "nothing is left where the old transform put the region");
        }

        [TestMethod]
        public void UnderTheSameTransformAnUnchangedPairKeepsItsRegion()
        {
            CellOverlapIndex index = new();
            ScaleTransform transform = new(1, Vector2.Zero);
            Assert.IsTrue(Update(index, transform));
            Polygon before = index.Regions[0].Shape;

            Assert.IsTrue(index.Update([.. TwoOverlappingCells, Cell(3, 103, 500)], transform, _ => null), "a new location changes the signature");

            Assert.AreEqual(1, index.Regions.Count);
            Assert.AreSame(before, index.Regions[0].Shape, "the pair's intersection is not computed again");
        }

        [TestMethod]
        public void TheSameTransformAndShapesChangeNothingUntilCleared()
        {
            CellOverlapIndex index = new();
            ScaleTransform transform = new(1, Vector2.Zero);
            Assert.IsTrue(Update(index, transform));

            Assert.IsFalse(Update(index, transform));

            index.Clear();
            Assert.AreEqual(0, index.Regions.Count);
            Assert.IsTrue(Update(index, transform));
            Assert.AreEqual(1, index.Regions.Count);
        }
    }
}
