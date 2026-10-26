using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Geometry;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that <see cref="StructureStore.Create"/> puts the new structure's first location into the location store
    /// with an Add event, as <see cref="IStructureStore.Create"/> promises and the WCF store did. Returning a detached
    /// copy left a new polygon out of the section index and every section view until a later server poll, so it had
    /// no adjacent-section marker on the next section. Runs without a server: the create client is a fake.
    /// </summary>
    /// <remarks>Rebinds the process-wide <see cref="Store"/>, because Create reaches the location store through it.</remarks>
    [TestFixture]
    public class StructureStoreCreateTests
    {
        private const long TypeId = 1;
        private const long StructureId = 40;
        private const long LocationId = 400;
        private const long Section = 17;
        private const string Square = "POLYGON ((0 0, 20 0, 20 20, 0 20, 0 0))";

        private StructureStore _structures;
        private LocationStore _locations;

        /// <summary>Set by the location store's Add event, which may run on the thread pool (InvokeEventAction).</summary>
        private TaskCompletionSource<NotifyCollectionChangedEventArgs> _locationAdded;

        [SetUp]
        public async Task SetUp()
        {
            var response = new CreateStructureResponseParameter(ServerStructure(), ServerLocation());
            var repository = StructureStoreRegionQueryTests.FakeInterface<IStructureRepository>
                .Create("Create", _ => Task.FromResult<ICreateStructureResponseParameter>(response));
            var repositoryFactory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IStructureRepository>>
                .Create("GetOrCreate", _ => repository);
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("Create goes through the structure repository"));

            var types = new StructureTypeStore(null, null, null, null);
            var locationLinks = new LocationLinkStore(null, null, null, null);
            _structures = new StructureStore(factory, repositoryFactory, null, new StructureServerToClientConverter(),
                null, new LocationServerToClientConverter(), new StructureLinkStore(null, null, new StructureLinkServerToClientConverter()));
            _locations = new LocationStore(null, null, null, new LocationServerToClientConverter(), _structures, locationLinks);
            _locationAdded = new TaskCompletionSource<NotifyCollectionChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            _locations.OnCollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                    _locationAdded.TrySetResult(e);
            };

            await Store.InitializeAsync(new Stores(_locations, _structures, types, locationLinks));
        }

        private static Structure ServerStructure() => new Structure
        {
            Id = StructureId,
            TypeId = TypeId,
            Label = "label",
            Username = "user",
            Created = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        };

        private static Location ServerLocation() => new Location
        {
            Id = LocationId,
            ParentId = StructureId,
            Section = Section,
            MosaicGeometry = Square.ParseWKT().ToShape(LocationType.POLYGON, Viking.gRPC.Geometry.V1.CoordinateSpace.Mosaic, null),
            VolumeGeometry = Square.ParseWKT().ToShape(LocationType.POLYGON, Viking.gRPC.Geometry.V1.CoordinateSpace.Volume, null),
            Username = "user",
            LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        };

        [Test]
        public async Task Create_AddsLocationToStore_WithAddEvent()
        {
            var (structure, location) = await _structures.Create(new StructureObj(), new LocationObj(-1, -1));

            Assert.That(structure.ID, Is.EqualTo(StructureId));
            Assert.That(location, Is.Not.Null);
            Assert.That(_locations.TryGetObjectByID(LocationId, out LocationObj stored), Is.True, "location cached");
            Assert.That(location, Is.SameAs(stored), "Create returns the cached instance, not a detached copy");

            Task first = await Task.WhenAny(_locationAdded.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(first, Is.SameAs(_locationAdded.Task), "views learn of the new location from the store's Add event");
            Assert.That(_locationAdded.Task.Result.NewItems, Does.Contain(stored));
            Assert.That(_locations.GetLocalObjectsForSection(Section).ContainsKey(LocationId), Is.True,
                "adjacent-section views seed from the section index");
        }

        /// <summary>The stores <see cref="StructureStore.Create"/> reaches through <see cref="Store"/>.</summary>
        private sealed class Stores : IAnnotationStores
        {
            public Stores(ILocationStore locations, IStructureStore structures, IStructureTypeStore types, ILocationLinkStore locationLinks)
            {
                Locations = locations;
                Structures = structures;
                StructureTypes = types;
                LocationLinks = locationLinks;
            }

            public ILocationStore Locations { get; }
            public IStructureStore Structures { get; }
            public IStructureTypeStore StructureTypes { get; }
            public IStructureLinkStore StructureLinks => null;
            public ILocationLinkStore LocationLinks { get; }
            public IPermittedStructureLinkStore PermittedStructureLinks => null;
            public IRegionLoader<LocationObj> LocationsByRegion => null;
            public Task InitializeAsync(CancellationToken token = default) => Task.CompletedTask;
        }
    }
}
