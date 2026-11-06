using System.Collections.Concurrent;
using DataExport.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataExport.Tests;

/// <summary>
/// MVC builds a new <see cref="MorphologyController"/> for every request, so the Scale and residual-field
/// caches must outlive the controller. These tests activate controllers the way MVC does, one per request scope.
/// </summary>
[TestClass]
public class MorphologyExportCacheTest
{
    private const string NoResidualDirectoryMessage = "Residual field: no published directory";

    private static WebApplicationFactory<Program> CreateFactory(CountingLoggerProvider logs)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:ODataURL"] = "http://127.0.0.1:1/OData",
                ["Corrections:ServiceUrl"] = "",
                ["Corrections:RootDirectory"] = "",
            }));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
    }

    private static MorphologyController ActivateForRequest(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        return ActivatorUtilities.CreateInstance<MorphologyController>(scope.ServiceProvider);
    }

    [TestMethod]
    public async Task ResidualField_LoadsOnceAcrossRequests()
    {
        CountingLoggerProvider logs = new();
        using WebApplicationFactory<Program> factory = CreateFactory(logs);

        await ActivateForRequest(factory.Services).GetOrLoadResidualFieldAsync(CancellationToken.None);
        await ActivateForRequest(factory.Services).GetOrLoadResidualFieldAsync(CancellationToken.None);

        Assert.AreEqual(1, logs.Count(NoResidualDirectoryMessage));
    }

    [TestMethod]
    public async Task Scale_LaterRequestsUseScaleLoadedByEarlierRequest()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(new CountingLoggerProvider());
        UnitsAndScale.Scale loaded = CreateScale();
        await factory.Services.GetRequiredService<MorphologyExportCache>().GetOrLoadScaleAsync(() => Task.FromResult(loaded));

        // The configured OData URL is unreachable, so a request that refetched the Scale would throw.
        Assert.AreSame(loaded, await ActivateForRequest(factory.Services).GetOrFetchScaleAsync());
        Assert.AreSame(loaded, await ActivateForRequest(factory.Services).GetOrFetchScaleAsync());
    }

    [TestMethod]
    public async Task Scale_ConcurrentCallersLoadOnce()
    {
        MorphologyExportCache cache = new();
        int loads = 0;
        TaskCompletionSource<UnitsAndScale.Scale> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<UnitsAndScale.Scale> Load()
        {
            Interlocked.Increment(ref loads);
            return release.Task;
        }

        Task<UnitsAndScale.Scale>[] callers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => cache.GetOrLoadScaleAsync(Load))).ToArray();
        await Task.Delay(50);
        release.SetResult(CreateScale());
        UnitsAndScale.Scale[] results = await Task.WhenAll(callers);

        Assert.AreEqual(1, loads);
        Assert.IsTrue(results.All(r => ReferenceEquals(r, results[0])));
    }

    [TestMethod]
    public async Task Scale_FailedLoadIsRetried()
    {
        MorphologyExportCache cache = new();
        UnitsAndScale.Scale loaded = CreateScale();

        await Assert.ThrowsExceptionAsync<HttpRequestException>(
            () => cache.GetOrLoadScaleAsync(() => throw new HttpRequestException("OData unavailable")));

        Assert.AreSame(loaded, await cache.GetOrLoadScaleAsync(() => Task.FromResult(loaded)));
    }

    [TestMethod]
    public async Task ResidualField_NoFieldPublishedIsCached()
    {
        MorphologyExportCache cache = new();
        int loads = 0;
        Task<AnnotationVizLib.NeighborResidualField?> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<AnnotationVizLib.NeighborResidualField?>(null);
        }

        Assert.IsNull(await cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None));
        Assert.IsNull(await cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None));
        Assert.AreEqual(1, loads);
    }

    [TestMethod]
    public async Task ResidualField_ConcurrentCallersLoadOnce()
    {
        MorphologyExportCache cache = new();
        int loads = 0;
        TaskCompletionSource<AnnotationVizLib.NeighborResidualField?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AnnotationVizLib.NeighborResidualField?> Load(CancellationToken _)
        {
            Interlocked.Increment(ref loads);
            return release.Task;
        }

        Task<AnnotationVizLib.NeighborResidualField?>[] callers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None))).ToArray();
        await Task.Delay(50);
        release.SetResult(null);
        await Task.WhenAll(callers);

        Assert.AreEqual(1, loads);
    }

    [TestMethod]
    public async Task ResidualField_CancelledWaiterStopsWaitingAndDoesNotLoad()
    {
        MorphologyExportCache cache = new();
        int loads = 0;
        TaskCompletionSource<AnnotationVizLib.NeighborResidualField?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AnnotationVizLib.NeighborResidualField?> Load(CancellationToken _)
        {
            Interlocked.Increment(ref loads);
            return release.Task;
        }

        Task<AnnotationVizLib.NeighborResidualField?> holder = Task.Run(() => cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None));
        while (Volatile.Read(ref loads) == 0)
            await Task.Delay(10);

        using CancellationTokenSource cts = new();
        Task<AnnotationVizLib.NeighborResidualField?> waiter = cache.GetOrLoadResidualFieldAsync(Load, cts.Token);
        cts.Cancel();

        // A waiter whose request was aborted must give up on the lock instead of blocking until the load finishes.
        Assert.AreSame(waiter, await Task.WhenAny(waiter, Task.Delay(TimeSpan.FromSeconds(5))));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => waiter);

        release.SetResult(null);
        Assert.IsNull(await holder);
        Assert.AreEqual(1, loads);
    }

    [TestMethod]
    public async Task ResidualField_LoadReceivesCallersToken()
    {
        MorphologyExportCache cache = new();
        using CancellationTokenSource cts = new();
        CancellationToken received = CancellationToken.None;

        await cache.GetOrLoadResidualFieldAsync(token =>
        {
            received = token;
            return Task.FromResult<AnnotationVizLib.NeighborResidualField?>(null);
        }, cts.Token);

        Assert.AreEqual(cts.Token, received);
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        MorphologyExportCache cache = new();
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => cache.GetOrLoadScaleAsync(null!));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => cache.GetOrLoadResidualFieldAsync(null!, CancellationToken.None));
        Assert.ThrowsException<ArgumentNullException>(() => new MorphologyController(
            Mock.Of<IWebHostEnvironment>(), new ConfigurationBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MorphologyController>.Instance, null!));
    }

    [TestMethod]
    public async Task ResidualField_FailedLoadIsRetried()
    {
        MorphologyExportCache cache = new();
        int loads = 0;
        Task<AnnotationVizLib.NeighborResidualField?> Load(CancellationToken _)
        {
            if (++loads == 1)
                throw new InvalidOperationException("No published correction set");
            return Task.FromResult<AnnotationVizLib.NeighborResidualField?>(null);
        }

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None));
        Assert.IsNull(await cache.GetOrLoadResidualFieldAsync(Load, CancellationToken.None));
        Assert.AreEqual(2, loads);
    }

    private static UnitsAndScale.Scale CreateScale()
    {
        return new UnitsAndScale.Scale(new UnitsAndScale.AxisUnits(2.18, "nm"), new UnitsAndScale.AxisUnits(2.18, "nm"),
            new UnitsAndScale.AxisUnits(90, "nm"));
    }

    private sealed class CountingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public int Count(string fragment) => _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));

        public ILogger CreateLogger(string categoryName) => new CountingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CountingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
