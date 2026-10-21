using System;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins the DI composition of <c>ConfigureAnnotationModel</c> without a server: Viking and VikingAU
    /// both call it, so a missing registration would otherwise only surface at client start-up.
    /// Channels are created lazily, so resolving the stores opens no connection.
    /// </summary>
    [TestFixture]
    public class AnnotationModelCompositionTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.ConfigureAnnotationModel(
                opts => opts.Endpoint = new Uri("http://localhost:1"),
                (GrpcChannelOptions _) => { });
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false });
        }

        [Test]
        public void EveryStoreInterfaceResolves()
        {
            using var sp = BuildProvider();
            Assert.Multiple(() =>
            {
                Assert.That(sp.GetRequiredService<ILocationStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<IStructureStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<IStructureTypeStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<IStructureLinkStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<ILocationLinkStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<IPermittedStructureLinkStore>(), Is.Not.Null);
                Assert.That(sp.GetRequiredService<IAnnotationStores>(), Is.Not.Null);
            });
        }

        [Test]
        public void StoresAreSingletonsSharedWithAnnotationStores()
        {
            using var sp = BuildProvider();
            var stores = sp.GetRequiredService<IAnnotationStores>();
            Assert.Multiple(() =>
            {
                Assert.That(sp.GetRequiredService<IAnnotationStores>(), Is.SameAs(stores));
                Assert.That(sp.GetRequiredService<ILocationStore>(), Is.SameAs(sp.GetRequiredService<ILocationStore>()));
                Assert.That(sp.GetRequiredService<IStructureStore>(), Is.SameAs(sp.GetRequiredService<IStructureStore>()));
            });
        }
    }
}
