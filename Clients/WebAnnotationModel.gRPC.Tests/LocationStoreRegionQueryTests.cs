using System.Linq;
using System.Threading.Tasks;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins <see cref="LocationStore.GetLocalObjectsInRegion"/> to the WCF client's semantics
    /// (R-tree intersect over each location's mosaic bounding box, then Radius &gt;= MinRadius).
    /// Runs without a server: locations enter the section index through the store's own add notification.
    /// </summary>
    [TestFixture]
    public class LocationStoreRegionQueryTests
    {
        private const int Section = 7;
        private const double LocationRadius = 16;
        private static readonly Rectangle Cell = new Rectangle(1000, 1512, 1000, 1512);

        private LocationStore _locations;

        [SetUp]
        public void SetUp()
        {
            _locations = new LocationStore(null, null, null, new LocationServerToClientConverter(),
                new StructureStore(null, null, null, null, null, null, null), new LocationLinkStore(null, null, null, null));
        }

        /// <summary>Converts a server location and adds it to the store, as a server ingest does.</summary>
        private async Task<LocationObj> AddLocation(Location server)
        {
            var stored = await ((IStoreWithKey<long, LocationObj>)_locations).GetOrAdd(server.Id,
                _ => new LocationServerToClientConverter().Convert(server), out bool added);
            Assert.That(added, Is.True);
            return stored;
        }

        private Task<LocationObj> AddCircleLocation(long id, Vector2 center, long section = Section)
        {
            return AddCircleLocation(id, center, center, section);
        }

        private Task<LocationObj> AddCircleLocation(long id, Vector2 mosaicCenter, Vector2 volumeCenter, long section = Section)
        {
            return AddLocation(new Location
            {
                Id = id,
                ParentId = 10,
                Section = section,
                MosaicGeometry = new Circle(mosaicCenter, LocationRadius).ToShape(LocationType.CIRCLE, CoordinateSpace.Mosaic, null),
                VolumeGeometry = new Circle(volumeCenter, LocationRadius).ToShape(LocationType.CIRCLE, CoordinateSpace.Volume, null),
            });
        }

        [Test]
        public async Task LocationInsideBounds_IsReturned()
        {
            var inside = await AddCircleLocation(1, new Vector2(1256, 1256));

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, 0), Is.EquivalentTo(new[] { inside }));
        }

        /// <summary>
        /// Centre outside the bounds, circle reaching in: WCF returns it, the centre-only <see cref="LocationStore.Contains"/> does not.
        /// Checked at small and real-volume magnitudes.
        /// </summary>
        [TestCase(1000.0, 1000.0)]
        [TestCase(250000.0, 180000.0)]
        public async Task LocationStraddlingEdge_IsReturned(double left, double bottom)
        {
            var bounds = new Rectangle(left, left + 512, bottom, bottom + 512);
            var straddling = await AddCircleLocation(1, new Vector2(left - LocationRadius / 2, bottom + 256));

            Assert.That(_locations.Contains(straddling, bounds), Is.False, "the centre is outside the bounds");
            Assert.That(await _locations.GetLocalObjectsInRegion(Section, bounds, 0), Is.EquivalentTo(new[] { straddling }));
        }

        /// <summary>
        /// The query is in mosaic space, as the WCF R-tree is: a location whose volume shape sits in the bounds but whose
        /// mosaic shape does not is excluded, and the reverse is returned.
        /// </summary>
        [Test]
        public async Task MosaicShapeDecides_NotVolumeShape()
        {
            var farAway = new Vector2(50000, 50000);
            var mosaicInside = await AddCircleLocation(1, new Vector2(1256, 1256), farAway);
            await AddCircleLocation(2, farAway, new Vector2(1256, 1256));

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, 0), Is.EquivalentTo(new[] { mosaicInside }));
        }

        [Test]
        public async Task BoundingBoxTouchingEdge_IsReturned()
        {
            var touching = await AddCircleLocation(1, new Vector2(1000 - LocationRadius, 1256));

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, 0), Is.EquivalentTo(new[] { touching }));
        }

        [Test]
        public async Task BoundingBoxDisjointFromBounds_IsExcluded()
        {
            await AddCircleLocation(1, new Vector2(1000 - LocationRadius - 1, 1256));

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, 0), Is.Empty);
        }

        [Test]
        public async Task RadiusBelowMinRadius_IsExcluded()
        {
            await AddCircleLocation(1, new Vector2(1256, 1256));

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, LocationRadius + 1), Is.Empty);
            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, LocationRadius), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// A placeholder built from IDs alone has no shape; the converter cannot build one (a null shape asserts),
        /// and its section is the default, 0, so the query runs there.
        /// </summary>
        [Test]
        public async Task LocationWithoutMosaicShape_IsSkipped()
        {
            var inside = await AddCircleLocation(1, new Vector2(1256, 1256), section: 0);
            var shapeless = await ((IStoreWithKey<long, LocationObj>)_locations).GetOrAdd(2, id => new LocationObj(id, 11), out _);
            Assert.That(_locations.GetLocalObjectsForSection(0).ContainsKey(shapeless.ID), Is.True, "the test needs it indexed");

            Assert.That(await _locations.GetLocalObjectsInRegion(0, Cell, 0), Is.EquivalentTo(new[] { inside }));
        }

        [Test]
        public async Task OtherSection_IsNotSearched()
        {
            await AddCircleLocation(1, new Vector2(1256, 1256), Section + 1);

            Assert.That(await _locations.GetLocalObjectsInRegion(Section, Cell, 0), Is.Empty);
            Assert.That((await _locations.GetLocalObjectsInRegion(Section + 1, Cell, 0)).Select(l => l.ID), Is.EqualTo(new[] { 1L }));
        }
    }
}
