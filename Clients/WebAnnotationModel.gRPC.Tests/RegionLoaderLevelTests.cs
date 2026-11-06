using System;
using System.Collections.Concurrent;
using System.Linq;
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
    /// Pins the region pyramid level a view loads, as the WCF client picks it: for eight screen pixels, so the MinRadius
    /// sent to the server and the cell size follow GetLevel(ScreenPixelSizeInVolume * 8). The port dropped the factor, which
    /// downloaded annotations too small to see and split a zoomed-out view into many more cells than WCF asked for.
    /// The completion check and the watermark seed must read the cells the load filled. Runs without a server.
    /// </summary>
    [TestFixture]
    public class RegionLoaderLevelTests
    {
        private const int Section = 23;
        private const double PowerScale = 3;
        private const double LevelZeroCellWidth = 2000;
        private static readonly Rectangle View = new Rectangle(left: 10, right: 110, bottom: 10, top: 110);

        private readonly DateTime ServerTime = DateTime.UtcNow.AddSeconds(-5);
        private ConcurrentQueue<(Rectangle Bounds, double MinRadius)> _requests;
        private LocationStore _locations;
        private LocationRegionLoader _loader;

        [SetUp]
        public async Task SetUp()
        {
            _requests = new ConcurrentQueue<(Rectangle, double)>();
            var structures = new StructureStore(null, null, null, null, null, null, null);
            var locationLinks = new LocationLinkStore(null, null, null, null);
            _locations = new LocationStore(null, null, null, new LocationServerToClientConverter(), structures, locationLinks);

            var client = StructureStoreRegionQueryTests.FakeInterface<IServerSpatialAnnotationsClient<long, AnnotationSet>>
                .Create("GetAsync", args =>
                {
                    _requests.Enqueue(((Rectangle)args[1], (double)args[2]));
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

            await Store.InitializeAsync(new TestAnnotationStores
            {
                Locations = _locations,
                Structures = structures,
                StructureTypes = new StructureTypeStore(null, null, null, null),
                LocationLinks = locationLinks,
                LocationsByRegion = _loader,
            });
        }

        private Task Load(double screenPixelSizeInVolume) =>
            _loader.GetObjectsInRegionAsync(View, screenPixelSizeInVolume, Section, QueryTargets.Server, CancellationToken.None, null);

        /// <summary>Expected level from the WCF rule, floor(log3(8 * pixel size)) clamped at 0; inputs avoid exact powers of three.</summary>
        [TestCase(1.0 / 16, 0)]
        [TestCase(0.25, 0)]
        [TestCase(0.5, 1)]
        [TestCase(1.0, 1)]
        [TestCase(4.0, 3)]
        [TestCase(12.0, 4)]
        public async Task Load_UsesLevelForEightScreenPixels(double screenPixelSizeInVolume, int expectedLevel)
        {
            double expectedMinRadius = Math.Pow(PowerScale, expectedLevel);

            await Load(screenPixelSizeInVolume);

            Assert.That(_requests, Is.Not.Empty);
            Assert.That(_requests.Select(r => r.MinRadius), Is.All.EqualTo(expectedMinRadius),
                "MinRadius sent to the server is the level's, picked for eight screen pixels as in WCF");
            Assert.That(_requests.Select(r => r.Bounds.Width), Is.All.EqualTo(LevelZeroCellWidth * expectedMinRadius).Within(1e-9),
                "cell size follows the same level");
        }

        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(4.0)]
        public async Task CompletionAndWatermark_ReadTheCellsTheLoadFilled(double screenPixelSizeInVolume)
        {
            await Load(screenPixelSizeInVolume);

            Assert.That(_loader.AreRegionQueriesComplete(View, screenPixelSizeInVolume, Section), Is.True,
                "the completion check reads the level the load used");
            Assert.That(_locations.GetLastQueryTimeForSection(Section), Is.EqualTo(ServerTime),
                "the first load seeds the watermark from the same level's cells");
        }
    }
}
