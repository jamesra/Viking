using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the status code each gRPC annotation service reports when the caller cancels
    /// mid-call: <see cref="StatusCode.Cancelled"/> with no error-level log, rather than
    /// <see cref="StatusCode.Unknown"/> plus an error. Every case runs with an already
    /// cancelled token against an unroutable SQL endpoint, so no database is contacted.
    /// </summary>
    public class RpcCancellationStatusTests
    {
        /// <summary>Unreachable endpoint; a pre-cancelled token stops EF before any connect attempt completes.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        /// <summary>One read RPC per service that routes its failures through the shared mapping.</summary>
        public static IEnumerable<object[]> CancelledCalls()
        {
            yield return new object[] { "Location.GetLocationsByID", (Func<AnnotationContext, RecordingLogger, ServerCallContext, Task>)(
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .GetLocationsByID(new GetLocationsByIDRequest { Ids = { 1L } }, ctx)) };
            yield return new object[] { "Structure.GetStructures", (Func<AnnotationContext, RecordingLogger, ServerCallContext, Task>)(
                (db, log, ctx) => new StructureService(db, log.For<StructureService>())
                    .GetStructures(new GetStructuresRequest(), ctx)) };
            yield return new object[] { "StructureType.GetStructureTypes", (Func<AnnotationContext, RecordingLogger, ServerCallContext, Task>)(
                (db, log, ctx) => new StructureTypeService(db, log.For<StructureTypeService>())
                    .GetStructureTypes(new GetStructureTypesRequest(), ctx)) };
            yield return new object[] { "PermittedStructureLinks.GetPermittedStructureLinks", (Func<AnnotationContext, RecordingLogger, ServerCallContext, Task>)(
                (db, log, ctx) => new PermittedStructureLinksService(db, log.For<PermittedStructureLinksService>())
                    .GetPermittedStructureLinks(new GetPermittedStructureLinksRequest(), ctx)) };
            yield return new object[] { "MetaData.Scale", (Func<AnnotationContext, RecordingLogger, ServerCallContext, Task>)(
                (db, log, ctx) => new MetaDataService(db, log.For<MetaDataService>())
                    .Scale(new ScaleRequest(), ctx)) };
        }

        [Theory]
        [MemberData(nameof(CancelledCalls))]
        public async Task CancelledCallReportsCancelledWithoutErrorLog(string name, Func<AnnotationContext, RecordingLogger, ServerCallContext, Task> call)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var options = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .Options;
            using var db = new AnnotationContext(options);
            var log = new RecordingLogger();

            var ex = await Assert.ThrowsAsync<RpcException>(() => call(db, log, new FakeCallContext(cts.Token)));

            Assert.True(ex.StatusCode == StatusCode.Cancelled, $"{name}: expected Cancelled, got {ex.StatusCode}");
            Assert.DoesNotContain(LogLevel.Error, log.Levels);
        }

        [Fact]
        public void NonCancellationFailureStillReportsUnknownAndLogsError()
        {
            var log = new RecordingLogger();

            var ex = AnnotationRpc.Failure(log.For<RpcCancellationStatusTests>(), "Op", new InvalidOperationException("boom"), new FakeCallContext(CancellationToken.None));

            Assert.Equal(StatusCode.Unknown, ex.StatusCode);
            Assert.Equal("Op", ex.Status.Detail);
            Assert.Contains(LogLevel.Error, log.Levels);
        }

        /// <summary>
        /// A cancellation the caller did not ask for is a server-side fault, not a benign cancel, so it
        /// keeps the Unknown status and the error log instead of being downgraded to Cancelled at Debug.
        /// </summary>
        [Fact]
        public void CancellationNotRequestedByCallerStillReportsUnknownAndLogsError()
        {
            var log = new RecordingLogger();

            var ex = AnnotationRpc.Failure(log.For<RpcCancellationStatusTests>(), "Op", new OperationCanceledException(), new FakeCallContext(CancellationToken.None));

            Assert.Equal(StatusCode.Unknown, ex.StatusCode);
            Assert.Contains(LogLevel.Error, log.Levels);
        }

        [Fact]
        public void CancellationRequestedByCallerReportsCancelledAtDebug()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var log = new RecordingLogger();

            var ex = AnnotationRpc.Failure(log.For<RpcCancellationStatusTests>(), "Op", new OperationCanceledException(), new FakeCallContext(cts.Token));

            Assert.Equal(StatusCode.Cancelled, ex.StatusCode);
            Assert.Equal("Op", ex.Status.Detail);
            Assert.Equal(new[] { LogLevel.Debug }, log.Levels);
        }

        /// <summary>Collects the level of every log entry written through any logger it hands out.</summary>
        public sealed class RecordingLogger
        {
            /// <summary>Levels in write order; not thread-safe, the tests log from one flow.</summary>
            public List<LogLevel> Levels { get; } = new();

            /// <summary>A typed logger that appends to <see cref="Levels"/>.</summary>
            public ILogger<T> For<T>() => new Typed<T>(Levels);

            private sealed class Typed<T>(List<LogLevel> levels) : ILogger<T>
            {
                public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                    => levels.Add(logLevel);
            }
        }

        /// <summary>Minimal call context whose only meaningful member is the caller's cancellation token.</summary>
        private sealed class FakeCallContext(CancellationToken token) : ServerCallContext
        {
            protected override string MethodCore => "test";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = new();
            protected override CancellationToken CancellationTokenCore => token;
            protected override Metadata ResponseTrailersCore { get; } = new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null;

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
