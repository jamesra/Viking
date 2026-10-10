using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins <see cref="StructureStore.GetLocalObjectsInRegion"/> to the WCF client's semantics
    /// (R-tree intersect over each location's mosaic bounding box, then Radius &gt;= MinRadius).
    /// Runs without a server: <see cref="Store"/> is bound to an in-memory location cache.
    /// </summary>
    /// <remarks>
    /// Rebinds the process-wide <see cref="Store"/>; the live smoke tests rebind it in their own SetUp,
    /// so these tests must not run in parallel with them (NUnit runs fixtures sequentially by default).
    /// </remarks>
    [TestFixture]
    public class StructureStoreRegionQueryTests
    {
        private const int Section = 7;
        private const double LocationRadius = 16;

        private ConcurrentDictionary<long, LocationObj> _sectionLocations;
        private StructureStore _structures;

        [SetUp]
        public async Task SetUp()
        {
            _sectionLocations = new ConcurrentDictionary<long, LocationObj>();
            var locations = FakeInterface<ILocationStore>.Create(nameof(ILocationStore.GetLocalObjectsForSection),
                args => (long)args[0] == Section ? _sectionLocations : new ConcurrentDictionary<long, LocationObj>());
            var stores = FakeInterface<IAnnotationStores>.Create(nameof(IAnnotationStores.Locations), _ => locations);
            await Store.InitializeAsync(stores);

            _structures = new StructureStore(null, null, null, null, null, null, null);
        }

        private StructureObj AddCircleLocation(long locationId, long structureId, Vector2 center)
        {
            var parent = new StructureObj(structureId, 1);
            var shape = new Circle(center, LocationRadius);
            _sectionLocations[locationId] = new LocationObj(parent, shape, shape, Section, LocationType.CIRCLE);
            return parent;
        }

        /// <summary>
        /// Centre outside the cell, circle reaching in: WCF returns the structure, the old centre-only test did not.
        /// Checked at small and real-volume magnitudes.
        /// </summary>
        [TestCase(1000.0, 1000.0)]
        [TestCase(250000.0, 180000.0)]
        public async Task LocationStraddlingCellEdge_ReturnsItsStructure(double cellLeft, double cellBottom)
        {
            var bounds = new Rectangle(cellLeft, cellLeft + 512, cellBottom, cellBottom + 512);
            var parent = AddCircleLocation(1, 10, new Vector2(cellLeft - LocationRadius / 2, cellBottom + 256));

            var result = await _structures.GetLocalObjectsInRegion(Section, bounds, 0);

            Assert.That(result, Is.EquivalentTo(new[] { parent }));
        }

        [Test]
        public async Task LocationBoundingBoxDisjointFromCell_IsExcluded()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            AddCircleLocation(1, 10, new Vector2(1000 - LocationRadius - 1, 1256));

            var result = await _structures.GetLocalObjectsInRegion(Section, bounds, 0);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task BoundingBoxTouchingCellEdge_IsIncluded()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            var parent = AddCircleLocation(1, 10, new Vector2(1000 - LocationRadius, 1256));

            var result = await _structures.GetLocalObjectsInRegion(Section, bounds, 0);

            Assert.That(result, Is.EquivalentTo(new[] { parent }));
        }

        [Test]
        public async Task RadiusBelowMinRadius_IsExcluded()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            AddCircleLocation(1, 10, new Vector2(1256, 1256));

            Assert.That(await _structures.GetLocalObjectsInRegion(Section, bounds, LocationRadius + 1), Is.Empty);
            Assert.That(await _structures.GetLocalObjectsInRegion(Section, bounds, LocationRadius), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task SeveralLocationsOfOneStructure_ReturnTheStructureOnce()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            var parent = new StructureObj(10, 1);
            foreach (var (id, center) in new[] { (1L, new Vector2(1100, 1100)), (2L, new Vector2(1400, 1400)) })
            {
                var shape = new Circle(center, LocationRadius);
                _sectionLocations[id] = new LocationObj(parent, shape, shape, Section, LocationType.CIRCLE);
            }

            var result = await _structures.GetLocalObjectsInRegion(Section, bounds, 0);

            Assert.That(result, Is.EquivalentTo(new[] { parent }));
        }

        [Test]
        public async Task LocationWithoutMosaicShape_IsSkipped()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            var parent = AddCircleLocation(1, 10, new Vector2(1256, 1256));
            _sectionLocations[2] = new LocationObj(2, 11);

            var result = await _structures.GetLocalObjectsInRegion(Section, bounds, 0);

            Assert.That(result, Is.EquivalentTo(new[] { parent }));
        }

        [Test]
        public async Task OtherSection_IsNotSearched()
        {
            var bounds = new Rectangle(1000, 1512, 1000, 1512);
            AddCircleLocation(1, 10, new Vector2(1256, 1256));

            Assert.That(await _structures.GetLocalObjectsInRegion(Section + 1, bounds, 0), Is.Empty);
        }

        /// <summary>
        /// Minimal interface fake: one named member answers through <c>handler</c>; every other member throws,
        /// so a test fails loudly if the code under test reaches further into the store than expected.
        /// </summary>
        public class FakeInterface<T> : DispatchProxy where T : class
        {
            private string _member;
            private Func<object[], object> _handler;

            /// <summary>Creates a <typeparamref name="T"/> whose method or property getter <paramref name="member"/> calls <paramref name="handler"/>.</summary>
            public static T Create(string member, Func<object[], object> handler)
            {
                T proxy = Create<T, FakeInterface<T>>();
                var fake = (FakeInterface<T>)(object)proxy;
                fake._member = member;
                fake._handler = handler;
                return proxy;
            }

            /// <inheritdoc />
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == _member || targetMethod.Name == "get_" + _member)
                    return _handler(args);
                if (targetMethod.Name == nameof(IAnnotationStores.InitializeAsync))
                    return Task.CompletedTask;
                throw new NotSupportedException($"{typeof(T).Name}.{targetMethod.Name} is not faked.");
            }
        }
    }
}
