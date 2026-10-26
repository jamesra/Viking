using System;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that a section's location watermark (the time the 30s poll asks for changes after) holds only server query
    /// times. A region load stamped it with the client's "now" even when every cell was served from cache, so the poll
    /// skipped changes made since the cells were fetched: a polygon created there, or another user's edit. The first
    /// load seeds it with the cells' server time; later loads leave it alone. Runs without a server: the region client
    /// is a fake that reports a fixed server time.
    /// </summary>
    [TestFixture]
    public class SectionWatermarkTests
    {
        private const int Section = 17;
        private static readonly Rectangle View = new Rectangle(left: 0, right: 100, bottom: 0, top: 100);
        private const double PixelSize = 1;

        /// <summary>
        /// The server's QueryExecutedTime: a little behind the client's clock, as when the fetched cells are still fresh
        /// (cells refresh by comparing it with the client's clock).
        /// </summary>
        private DateTime ServerTime;

        private LocationStore _locations;
        private LocationRegionLoader _loader;
        private int _serverQueries;

        [SetUp]
        public async Task SetUp()
        {
            _serverQueries = 0;
            ServerTime = DateTime.UtcNow.AddSeconds(-5);
            var structures = new StructureStore(null, null, null, null, null, null, null);
            var locationLinks = new LocationLinkStore(null, null, null, null);
            _locations = new LocationStore(null, null, null, new LocationServerToClientConverter(), structures, locationLinks);

            var client = StructureStoreRegionQueryTests.FakeInterface<IServerSpatialAnnotationsClient<long, AnnotationSet>>
                .Create("GetAsync", _ =>
                {
                    Interlocked.Increment(ref _serverQueries);
                    return Task.FromResult(new ServerUpdate<long, AnnotationSet[]>(ServerTime, new[] { new AnnotationSet() }, Array.Empty<long>()));
                });
            var clientFactory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, AnnotationSet>>>
                .Create("GetOrCreate", _ => client);
            var processor = new ProcessorForServerAnnotationSets(_locations, structures, locationLinks, new StructureLinkStore(null, null, null),
                new LocationServerToClientConverter(), new LocationServerToClientUpdater(),
                new StructureServerToClientConverter(), new StructureServerToClientUpdater(),
                new LocationLinkServerToClientConverter(), new StructureLinkServerToClientConverter());

            _loader = new LocationRegionLoader(_locations, clientFactory, processor, new LocationIdDeleteHandler(_locations),
                new LocationServerToMosaicShapeConverter(), locationLinks);

            // Eviction invalidates the section's region cells through the static Store. Structure types are bound too:
            // the structure converter looks them up once Store is initialized, in this and later fixtures.
            await Store.InitializeAsync(new TestAnnotationStores
            {
                Locations = _locations,
                Structures = structures,
                StructureTypes = new StructureTypeStore(null, null, null, null),
                LocationLinks = locationLinks,
                LocationsByRegion = _loader,
            });
        }

        private Task Load() => _loader.GetObjectsInRegionAsync(View, PixelSize, Section, QueryTargets.Server, CancellationToken.None, null);

        [Test]
        public async Task FirstRegionLoad_SeedsWatermarkWithServerQueryTime()
        {
            await Load();

            Assert.That(_serverQueries, Is.GreaterThan(0));
            Assert.That(_locations.GetLastQueryTimeForSection(Section), Is.EqualTo(ServerTime),
                "the server's time, not the client's clock");
        }

        [Test]
        public async Task CacheOnlyRegionLoad_LeavesWatermark()
        {
            await Load();
            int queriesAfterFirstLoad = _serverQueries;

            await Load();

            Assert.That(_serverQueries, Is.EqualTo(queriesAfterFirstLoad), "warm cells are not queried again");
            Assert.That(_locations.GetLastQueryTimeForSection(Section), Is.EqualTo(ServerTime),
                "a load that asked the server nothing must not move the watermark past unsent changes");
        }

        [Test]
        public async Task RegionLoad_KeepsSectionForEviction()
        {
            await Load();

            _locations.FreeExcessSections(LoadedSectionLimit: 1, LoadingSectionLimit: 1);
            Assert.That(_locations.GetLastQueryTimeForSection(Section), Is.EqualTo(ServerTime), "within the limit, kept");

            _locations.FreeExcessSections(LoadedSectionLimit: 0, LoadingSectionLimit: 0);
            Assert.That(_locations.GetLastQueryTimeForSection(Section), Is.EqualTo(DateTime.MinValue), "over the limit, evicted");
        }
    }
}
