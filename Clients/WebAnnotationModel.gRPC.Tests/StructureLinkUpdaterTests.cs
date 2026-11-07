using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Links added to or removed from the link stores reach StructureObj.Links and StructureTypeObj.PermittedLinks
    /// through the updaters <c>ConfigureAnnotationModel</c> wires into <see cref="AnnotationStores"/>. No server: every
    /// object is added to the local caches, and channels are created lazily.
    /// </summary>
    [TestFixture]
    public class StructureLinkUpdaterTests
    {
        private const int WaitMs = 2000;

        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.ConfigureAnnotationModel(
                opts => opts.Endpoint = new Uri("http://localhost:1"),
                (GrpcChannelOptions _) => { });
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false });
        }

        private static bool LinksTo(StructureObj s, StructureLinkKey key) =>
            s.LinksCopy.Any(l => l.SourceID == key.SourceID && l.TargetID == key.TargetID);

        [Test]
        public async Task AddedAndRemovedStructureLink_ReachesBothCachedStructures()
        {
            using var sp = BuildProvider();
            var stores = sp.GetRequiredService<IAnnotationStores>();
            var source = await stores.Structures.Add(new StructureObj(1001, 1));
            var target = await stores.Structures.Add(new StructureObj(1002, 1));

            var link = await stores.StructureLinks.Add(new StructureLinkObj(1001, 1002, false));

            Assert.That(() => LinksTo(source, link.ID) && LinksTo(target, link.ID), Is.True.After(WaitMs, 10));

            await stores.StructureLinks.Remove(link.ID);

            Assert.That(() => source.NumLinks + target.NumLinks, Is.Zero.After(WaitMs, 10));
        }

        [Test]
        public async Task StructuresAddedAfterTheirLinks_TakeTheCachedLinks_WithoutAServerFetch()
        {
            using var sp = BuildProvider();
            var stores = sp.GetRequiredService<IAnnotationStores>();
            var cached = await stores.Structures.Add(new StructureObj(2001, 1));
            var toLateTarget = await stores.StructureLinks.Add(new StructureLinkObj(2001, 2002, true));
            var fromLateSource = await stores.StructureLinks.Add(new StructureLinkObj(2004, 2001, false));

            Assert.That(() => LinksTo(cached, toLateTarget.ID) && LinksTo(cached, fromLateSource.ID), Is.True.After(WaitMs, 10));
            Assert.That(((StructureStore)stores.Structures).LastServerFetchError, Is.Null,
                "the far end of a link is looked up in the cache, not fetched");

            var late = await stores.Structures.Add(new[]
            {
                new StructureObj(2002, 1), new StructureObj(2003, 1), new StructureObj(2004, 1)
            });
            var lateTarget = late.Single(s => s.ID == 2002);
            var unrelated = late.Single(s => s.ID == 2003);
            var lateSource = late.Single(s => s.ID == 2004);

            Assert.That(() => LinksTo(lateTarget, toLateTarget.ID) && LinksTo(lateSource, fromLateSource.ID), Is.True.After(WaitMs, 10));
            Assert.Multiple(() =>
            {
                Assert.That(lateTarget.NumLinks, Is.EqualTo(1));
                Assert.That(lateSource.NumLinks, Is.EqualTo(1));
                Assert.That(unrelated.NumLinks, Is.Zero);
            });
        }

        [Test]
        public async Task AddedAndRemovedPermittedLink_ReachesBothCachedTypes()
        {
            using var sp = BuildProvider();
            var stores = sp.GetRequiredService<IAnnotationStores>();
            var sourceType = await stores.StructureTypes.Add(new StructureTypeObj(31));
            var targetType = await stores.StructureTypes.Add(new StructureTypeObj(32));
            Assert.That(sourceType.PermittedLinks, Is.Empty);

            var permitted = await stores.PermittedStructureLinks.Add(new PermittedStructureLinkObj(31, 32, false));

            Assert.That(() => sourceType.PermittedLinkTargetTypes, Is.EqualTo(new long[] { 32 }).After(WaitMs, 10));
            Assert.That(() => targetType.PermittedLinks.Count, Is.EqualTo(1).After(WaitMs, 10));

            await stores.PermittedStructureLinks.Remove(permitted.ID);

            Assert.That(() => sourceType.PermittedLinks.Count + targetType.PermittedLinks.Count, Is.Zero.After(WaitMs, 10));
        }
    }
}
