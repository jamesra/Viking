using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.DataModel.Annotation;
using Xunit;
using static ConnectomeDataModelCoreTests.RpcCancellationStatusTests;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins that a refusal an annotation service throws on purpose (an <see cref="RpcException"/> such as
    /// InvalidArgument for a missing region) reaches the caller with its own status and detail, not wrapped as
    /// <see cref="StatusCode.Unknown"/> with an error log by the RPC's catch-all. Each refused call fails
    /// before any query, so the unroutable SQL endpoint is never contacted.
    /// </summary>
    public class RpcRefusalStatusTests
    {
        /// <summary>Unroutable endpoint; every case throws before EF opens a connection.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        /// <summary>
        /// RPCs whose deliberate refusal is thrown inside a try that has no <c>catch (RpcException)</c>
        /// pass-through, so it reaches <see cref="AnnotationRpc.Failure"/>.
        /// </summary>
        public static IEnumerable<object[]> RefusedCalls()
        {
            yield return Case("Location.CreateLocationLink self-link", "A location cannot link to itself",
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .CreateLocationLink(new CreateLocationLinkRequest { SourceId = 4, TargetId = 4 }, ctx));
            yield return Case("Location.GetLocationLinksForSectionInMosaicRegion without bbox", "A bounding box is required",
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .GetLocationLinksForSectionInMosaicRegion(new GetLocationLinksForSectionInMosaicRegionRequest { Section = 1 }, ctx));
            yield return Case("Location.GetAnnotationsInMosaicRegion without region", "A region is required",
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .GetAnnotationsInMosaicRegion(new GetAnnotationsInMosaicRegionRequest { Z = 1 }, ctx));
            yield return Case("Location.GetLocationChangesInMosaicRegion without region", "A region is required",
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .GetLocationChangesInMosaicRegion(new GetLocationChangesInMosaicRegionRequest { Z = 1 }, ctx));
            yield return Case("Structure.GetStructuresInMosaicRegion without region", "A region is required",
                (db, log, ctx) => new StructureService(db, log.For<StructureService>())
                    .GetStructuresInMosaicRegion(new GetStructuresInMosaicRegionRequest { Z = 1 }, ctx));
            yield return Case("Structure.GetStructuresInVolumeRegion without region", "A region is required",
                (db, log, ctx) => new StructureService(db, log.For<StructureService>())
                    .GetStructuresInVolumeRegion(new GetStructuresInVolumeRegionRequest { Z = 1 }, ctx));
            // The proto conversion refuses with ArgumentException (Location.EF.Conversion), before any query.
            yield return Case("Location.CreateLocation polygon without its volume shape",
                "POLYGON locations require a volume shape: the client derives it from the mosaic shape under the volume's default transform.",
                (db, log, ctx) => new LocationService(db, log.For<LocationService>())
                    .CreateLocation(new CreateLocationRequest { Obj = PolygonWithoutVolumeShape() }, ctx));
        }

        private static ProtoLocation PolygonWithoutVolumeShape() => new()
        {
            Section = 12,
            MosaicGeometry = AnnotationShapes.PolygonShape(LocationType.POLYGON, Viking.gRPC.Geometry.V1.CoordinateSpace.Mosaic,
                new global::Geometry.Vector2[] { new(0, 0), new(10, 0), new(5, 5), new(0, 0) }, System.Array.Empty<global::Geometry.Vector2[]>(), null),
        };

        [Theory]
        [MemberData(nameof(RefusedCalls))]
        public async Task RefusalKeepsInvalidArgumentWithoutErrorLog(string name, string detail, Func<AnnotationContext, RecordingLogger, ServerCallContext, Task> call)
        {
            using var db = new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .Options);
            var log = new RecordingLogger();

            var ex = await Assert.ThrowsAsync<RpcException>(() => call(db, log, new LocationServiceUpdateDeleteTests.TestCallContext()));

            Assert.True((StatusCode.InvalidArgument, detail) == (ex.StatusCode, ex.Status.Detail),
                $"{name}: expected InvalidArgument '{detail}', got {ex.StatusCode} '{ex.Status.Detail}'");
            Assert.DoesNotContain(LogLevel.Error, log.Levels);
        }

        /// <summary>Every status code, so a code added to Grpc.Core is covered without editing this test.</summary>
        public static IEnumerable<object[]> AllStatusCodes() =>
            Enum.GetValues<StatusCode>().Select(code => new object[] { code });

        /// <summary>
        /// Whatever status a service throws on purpose, the catch-all passes it on unchanged (same status, same
        /// detail) and does not log it as a server error.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllStatusCodes))]
        public void FailurePassesAnRpcExceptionOnUnchanged(StatusCode code)
        {
            var log = new RecordingLogger();
            var thrown = new RpcException(new Status(code, "why"));

            var ex = AnnotationRpc.Failure(log.For<RpcRefusalStatusTests>(), "Op", thrown, new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((code, "why"), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new[] { LogLevel.Debug }, log.Levels);
        }

        /// <summary>
        /// A request the proto conversion refuses (an <see cref="ArgumentException"/>) reaches the caller as InvalidArgument
        /// with the reason, logged as a warning, not as Unknown with the RPC's name and an error log.
        /// </summary>
        [Fact]
        public void FailureTurnsARefusedRequestIntoInvalidArgument()
        {
            var log = new RecordingLogger();

            var ex = AnnotationRpc.Failure(log.For<RpcRefusalStatusTests>(), "Op", new ArgumentException("why"), new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.InvalidArgument, "why"), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new[] { LogLevel.Warning }, log.Levels);
        }

        private static object[] Case(string name, string detail, Func<AnnotationContext, RecordingLogger, ServerCallContext, Task> call) =>
            new object[] { name, detail, call };
    }
}
