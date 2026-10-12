using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins the <c>ForgetLocally(KEY)</c> contract shared with the WCF store: forgetting a key that was never
    /// loaded returns null instead of throwing, because <c>StructureStore.Merge</c> and
    /// <c>SplitStructureAtLocationLink</c> call it after the server already succeeded and ignore the result.
    /// Runs without a server; the client factory is never used.
    /// </summary>
    [TestFixture]
    public class StoreForgetLocallyTests
    {
        private StructureStore _structures;

        [SetUp]
        public void SetUp()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no server in this test"));
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", _ => null);
            _structures = new StructureStore(factory, null, toServer, null, null, null, null);
        }

        [Test]
        public void UnloadedKey_ReturnsNull()
        {
            Assert.That(_structures.ForgetLocally(42L), Is.Null);
        }

        [Test]
        public void LoadedKey_IsReturnedAndRemoved()
        {
            var loaded = _structures.GetOrAdd(7L, id => new StructureObj(id, 1), out bool added);
            Assert.That(added, Is.True);

            var forgotten = _structures.ForgetLocally(7L);

            Assert.That(forgotten, Is.SameAs(loaded));
            Assert.That(_structures.Contains(7L), Is.False);
            Assert.That(_structures.ForgetLocally(7L), Is.Null);
        }
    }
}
