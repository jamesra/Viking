using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.DependencyInjection;
using Viking.Services.Grpc;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the static <see cref="ServiceLocator"/>: it throws until initialized, rejects a null provider,
    /// disposes a replaced or reset provider, shuts down the channel manager on reset, and rebuilds the
    /// provider from the retained service collection. Every test resets the static state before and after.
    /// </summary>
    [TestClass]
    public class ServiceLocatorTests
    {
        private sealed class Marker
        {
        }

        private sealed class FakeChannelManager : IGrpcChannelManager
        {
            public int Shutdowns;

            public Channel GetOrCreateChannel() => null!;

            public bool IsChannelHealthy() => false;

            public void ResetChannel()
            {
            }

            public void Shutdown() => Shutdowns++;
        }

        private sealed class DisposableProvider : IServiceProvider, IDisposable
        {
            public int Disposals;

            public object? GetService(Type serviceType) => null;

            public void Dispose() => Disposals++;
        }

        [TestInitialize]
        public void Setup() => ServiceLocator.Reset();

        [TestCleanup]
        public void Cleanup() => ServiceLocator.Reset();

        [TestMethod]
        public void BeforeInitialize_IsNotInitializedAndProviderThrows()
        {
            Assert.IsFalse(ServiceLocator.IsInitialized);
            Assert.ThrowsException<InvalidOperationException>(() => ServiceLocator.ServiceProvider);
            Assert.ThrowsException<InvalidOperationException>(() => ServiceLocator.GetService<Marker>());
        }

        [TestMethod]
        public void Initialize_NullProvider_ThrowsAndLeavesStateUntouched()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ServiceLocator.Initialize(null!));
            Assert.IsFalse(ServiceLocator.IsInitialized);
        }

        [TestMethod]
        public void Initialize_ExposesProviderAndResolvesServices()
        {
            Marker marker = new();
            ServiceCollection services = new();
            services.AddSingleton(marker);
            IServiceProvider provider = services.BuildServiceProvider();

            ServiceLocator.Initialize(provider);

            Assert.IsTrue(ServiceLocator.IsInitialized);
            Assert.AreSame(provider, ServiceLocator.ServiceProvider);
            Assert.AreSame(marker, ServiceLocator.GetService<Marker>());
            Assert.AreSame(marker, ServiceLocator.GetRequiredService<Marker>());
        }

        [TestMethod]
        public void GetService_Missing_ReturnsNullButGetRequiredServiceThrows()
        {
            ServiceLocator.Initialize(new ServiceCollection().BuildServiceProvider());

            Assert.IsNull(ServiceLocator.GetService<Marker>());
            Assert.ThrowsException<InvalidOperationException>(() => ServiceLocator.GetRequiredService<Marker>());
        }

        [TestMethod]
        public void GrpcChannelManager_ReturnsRegisteredManager()
        {
            FakeChannelManager manager = new();
            ServiceCollection services = new();
            services.AddSingleton<IGrpcChannelManager>(manager);
            ServiceLocator.Initialize(services.BuildServiceProvider());

            Assert.AreSame(manager, ServiceLocator.GrpcChannelManager);
        }

        [TestMethod]
        public void Initialize_Again_DisposesPreviousProviderOnly()
        {
            DisposableProvider first = new();
            DisposableProvider second = new();

            ServiceLocator.Initialize(first);
            Assert.AreEqual(0, first.Disposals);

            ServiceLocator.Initialize(second);

            Assert.AreEqual(1, first.Disposals);
            Assert.AreEqual(0, second.Disposals);
            Assert.AreSame(second, ServiceLocator.ServiceProvider);
            Assert.IsTrue(ServiceLocator.IsInitialized);
        }

        [TestMethod]
        public void Reset_ShutsDownChannelManagerDisposesProviderAndClearsState()
        {
            FakeChannelManager manager = new();
            ServiceCollection services = new();
            services.AddSingleton<IGrpcChannelManager>(manager);
            ServiceLocator.Initialize(services.BuildServiceProvider(), services);

            ServiceLocator.Reset();

            Assert.AreEqual(1, manager.Shutdowns);
            Assert.IsFalse(ServiceLocator.IsInitialized);
            Assert.ThrowsException<InvalidOperationException>(() => ServiceLocator.ServiceProvider);
            Assert.ThrowsException<InvalidOperationException>(
                () => ServiceLocator.RebuildServiceProvider(_ => { }),
                "Reset must also drop the retained service collection.");
        }

        [TestMethod]
        public void Reset_DisposesDisposableProvider()
        {
            DisposableProvider provider = new();
            ServiceLocator.Initialize(provider);

            ServiceLocator.Reset();

            Assert.AreEqual(1, provider.Disposals);
        }

        [TestMethod]
        public void Reset_WhenNeverInitialized_IsSafeAndRepeatable()
        {
            ServiceLocator.Reset();
            ServiceLocator.Reset();

            Assert.IsFalse(ServiceLocator.IsInitialized);
        }

        [TestMethod]
        public void RebuildServiceProvider_NullDelegate_Throws()
        {
            ServiceLocator.Initialize(new ServiceCollection().BuildServiceProvider(), new ServiceCollection());

            Assert.ThrowsException<ArgumentNullException>(() => ServiceLocator.RebuildServiceProvider(null!));
        }

        [TestMethod]
        public void RebuildServiceProvider_WithoutCollection_Throws()
        {
            ServiceLocator.Initialize(new ServiceCollection().BuildServiceProvider());

            Assert.ThrowsException<InvalidOperationException>(() => ServiceLocator.RebuildServiceProvider(_ => { }));
        }

        [TestMethod]
        public void RebuildServiceProvider_AppliesConfigurationAndReplacesProvider()
        {
            ServiceCollection services = new();
            services.AddSingleton<IGrpcChannelManager>(new FakeChannelManager());
            IServiceProvider original = services.BuildServiceProvider();
            ServiceLocator.Initialize(original, services);
            Assert.IsNull(ServiceLocator.GetService<Marker>());
            Marker marker = new();

            ServiceLocator.RebuildServiceProvider(s => s.AddSingleton(marker));

            Assert.IsTrue(ServiceLocator.IsInitialized);
            Assert.AreNotSame(original, ServiceLocator.ServiceProvider);
            Assert.AreSame(marker, ServiceLocator.GetService<Marker>());
            Assert.IsNotNull(ServiceLocator.GetService<IGrpcChannelManager>());
        }

        [TestMethod]
        public void RebuildServiceProvider_DisposesOldProvider()
        {
            // The rebuilt provider replaces a disposable one; the old one must be disposed exactly once.
            DisposableProvider old = new();
            ServiceLocator.Initialize(old, new ServiceCollection());

            ServiceLocator.RebuildServiceProvider(_ => { });

            Assert.AreEqual(1, old.Disposals);
        }
    }
}
