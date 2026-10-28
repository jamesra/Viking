using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that <see cref="LocationLinkStore.DeleteLink"/> deletes on the server once and queues nothing for
    /// <c>Save()</c>. <c>LinkLocationAction</c> calls <c>LocationLinks.Save()</c> after every <c>CreateLink</c>, so a
    /// queued DELETE would be sent again: the server answers NotFound (Save throws), or, when the same link was
    /// re-created in between, deletes the new link. Runs without a server against an in-memory link set.
    /// </summary>
    [TestFixture]
    public class LocationLinkDeleteTests
    {
        private FakeLinkServer _server;
        private LocationLinkStore _links;

        [SetUp]
        public void SetUp()
        {
            _server = new FakeLinkServer();
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>>
                .Create("GetOrCreate", _ => _server);
            _links = new LocationLinkStore(factory, new LocationLinkClientToServerConverter(),
                new LocationLinkServerToClientConverter(), null);
        }

        [Test]
        public async Task DeleteThenSave_SendsOneDelete()
        {
            await _links.CreateLink(1, 2);
            var removed = WaitForRemove();

            Assert.That(await _links.DeleteLink(1, 2), Is.True);
            Assert.That(removed.Wait(TimeSpan.FromSeconds(5)), Is.True, "DeleteLink raised no Remove event");
            Assert.That(removed.Result, Is.EqualTo(new LocationLinkObj(1, 2)));
            Assert.That(_links.Contains(new LocationLinkKey(1, 2)), Is.False);

            await _links.Save();

            Assert.That(_server.Deletes, Is.EqualTo(new[] { new LocationLinkKey(1, 2) }));
        }

        [Test]
        public async Task DeleteRecreateThenSave_KeepsRecreatedLink()
        {
            await _links.CreateLink(1, 2);
            await _links.DeleteLink(1, 2);
            await _links.CreateLink(2, 1);

            await _links.Save();

            Assert.That(_server.Deletes, Has.Count.EqualTo(1));
            Assert.That(_server.Links, Does.Contain(new LocationLinkKey(1, 2)));
            Assert.That(_links.Contains(new LocationLinkKey(1, 2)), Is.True);
        }

        [Test]
        public async Task DeleteOfUncachedLink_DeletesOnServerAndReturnsFalse()
        {
            await _server.Create(new LocationLinkObj(3, 4), CancellationToken.None);

            Assert.That(await _links.DeleteLink(3, 4), Is.False);
            await _links.Save();

            Assert.That(_server.Deletes, Is.EqualTo(new[] { new LocationLinkKey(3, 4) }));
            Assert.That(_server.Links, Is.Empty);
        }

        /// <summary>Completes with the first item of the store's next Remove event.</summary>
        private Task<LocationLinkObj> WaitForRemove()
        {
            var tcs = new TaskCompletionSource<LocationLinkObj>(TaskCreationOptions.RunContinuationsAsynchronously);
            _links.OnCollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Remove)
                    tcs.TrySetResult(e.OldItems.Cast<LocationLinkObj>().First());
            };
            return tcs.Task;
        }

        /// <summary>
        /// Server link set: Create adds the link, Delete removes it and throws for a missing link as the gRPC
        /// service's NotFound does. Every Delete call is recorded.
        /// </summary>
        private sealed class FakeLinkServer : IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>
        {
            public readonly HashSet<LocationLinkKey> Links = new HashSet<LocationLinkKey>();
            public readonly List<LocationLinkKey> Deletes = new List<LocationLinkKey>();

            public Task<ILocationLink> Create(ILocationLink obj, CancellationToken token)
            {
                Links.Add(new LocationLinkKey((long)obj.A, (long)obj.B));
                return Task.FromResult(obj);
            }

            public Task<LocationLinkKey?> Delete(LocationLinkKey key, CancellationToken token)
            {
                Deletes.Add(key);
                if (!Links.Remove(key))
                    throw new InvalidOperationException($"NotFound: location link {key}");
                return Task.FromResult<LocationLinkKey?>(key);
            }

            public Task<ILocationLink> GetAsync(LocationLinkKey key, CancellationToken token) => throw new NotSupportedException();
            public Task<IList<ILocationLink>> GetAsync(IEnumerable<LocationLinkKey> keys, CancellationToken token) => throw new NotSupportedException();
            public Task<UpdateResults<LocationLinkKey, ILocationLink>> UpdateAsync(ILocationLink obj, CancellationToken token) => throw new NotSupportedException();
            public Task<UpdateResults<LocationLinkKey, ILocationLink>> UpdateAsync(IEnumerable<ILocationLink> objs, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
