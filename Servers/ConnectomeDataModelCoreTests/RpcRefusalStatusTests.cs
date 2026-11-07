using FsCheck;
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

        /// <summary>dbo.SplitStructure's refusal on a cell whose locations are all linked: THROW 50000 from the procedure call.</summary>
        private const string SplitRefusal =
            "The split structure is connected to the entire keep cell.  Break a location link to create two subgraphs and try again";

        /// <summary>
        /// A procedure's THROW 50000 reaches the caller as FailedPrecondition with the procedure's message, logged as a
        /// warning, whether the procedure call raised it or a trigger did during SaveChanges (inside a DbUpdateException).
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FailureTurnsADatabaseRefusalIntoFailedPrecondition(bool fromSaveChanges)
        {
            var log = new RecordingLogger();
            Exception sql = SqlExceptionFactory.Create((50000, SplitRefusal));
            var thrown = fromSaveChanges ? new DbUpdateException("save failed", sql) : sql;

            var ex = AnnotationRpc.Failure(log.For<RpcRefusalStatusTests>(), "Split", thrown, new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.FailedPrecondition, SplitRefusal), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new[] { LogLevel.Warning }, log.Levels);
        }

        /// <summary>
        /// The detail is the 50000 error's own message, not SqlException.Message, which joins every error's text (a
        /// procedure that leaves a transaction open adds error 266 after its THROW).
        /// </summary>
        [Fact]
        public void DatabaseRefusalDetailIsTheRefusingErrorsMessageAlone()
        {
            var sql = SqlExceptionFactory.Create((266, "Transaction count after EXECUTE indicates a mismatching number of BEGIN and COMMIT statements."), (50000, SplitRefusal));

            var ex = AnnotationRpc.Failure(new RecordingLogger().For<RpcRefusalStatusTests>(), "Split", sql, new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.FailedPrecondition, SplitRefusal), (ex.StatusCode, ex.Status.Detail));
        }

        /// <summary>Of two refusals in one SqlException, the first raised is the reason reported.</summary>
        [Fact]
        public void FirstOfSeveralDatabaseRefusalsIsReported()
        {
            var sql = SqlExceptionFactory.Create((50000, "first"), (50000, "second"));

            var ex = AnnotationRpc.Failure(new RecordingLogger().For<RpcRefusalStatusTests>(), "Split", sql, new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.FailedPrecondition, "first"), (ex.StatusCode, ex.Status.Detail));
        }

        /// <summary>
        /// Any other SQL error is a server fault: a primary or foreign key violation, a deadlock, a timeout, or a user
        /// error number other than 50000 stays Unknown with the RPC's name, logged as an error.
        /// </summary>
        [Theory]
        [InlineData(2627)]
        [InlineData(547)]
        [InlineData(1205)]
        [InlineData(-2)]
        [InlineData(50001)]
        [InlineData(49999)]
        public void OtherSqlErrorsStayUnknown(int number)
        {
            var log = new RecordingLogger();

            var ex = AnnotationRpc.Failure(log.For<RpcRefusalStatusTests>(), "Split", SqlExceptionFactory.Create((number, "why")), new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.Unknown, "Split"), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new[] { LogLevel.Error }, log.Levels);
        }

        /// <summary>Only a DbUpdateException is unwrapped: a 50000 inside any other exception is not a database answer to the request.</summary>
        [Fact]
        public void DatabaseRefusalInsideAnotherExceptionStaysUnknown()
        {
            var thrown = new InvalidOperationException("wrapped", SqlExceptionFactory.Create((50000, SplitRefusal)));

            var ex = AnnotationRpc.Failure(new RecordingLogger().For<RpcRefusalStatusTests>(), "Split", thrown, new LocationServiceUpdateDeleteTests.TestCallContext());

            Assert.Equal((StatusCode.Unknown, "Split"), (ex.StatusCode, ex.Status.Detail));
        }

        /// <summary>
        /// SqlExceptions of one to three errors drawn from 50000 and common server faults, raised directly or inside a
        /// DbUpdateException: FailedPrecondition with the first 50000 error's message exactly when one is present,
        /// otherwise Unknown with the RPC's name.
        /// </summary>
        [Fact]
        public void RefusedExactlyWhenSomeErrorIs50000()
        {
            var error =
                from number in Gen.Elements(50000, 50000, 50000, 266, 2627, 547, 1205, -2, 50001, 0)
                from message in Gen.Elements("a", "b", "c")
                select (number, message);
            var errors =
                from count in Gen.Choose(1, 3)
                from list in Gen.ArrayOf(count, error)
                from wrapped in Arb.Generate<bool>()
                select (Errors: list, Wrapped: wrapped);

            Prop.ForAll(Arb.From(errors), c =>
            {
                Exception sql = SqlExceptionFactory.Create(c.Errors);
                var thrown = c.Wrapped ? new DbUpdateException("save failed", sql) : sql;

                var ex = AnnotationRpc.Failure(new RecordingLogger().For<RpcRefusalStatusTests>(), "Op", thrown, new LocationServiceUpdateDeleteTests.TestCallContext());

                var refusal = c.Errors.Where(e => e.number == 50000).Select(e => e.message).FirstOrDefault();
                Assert.Equal(refusal is null ? (StatusCode.Unknown, "Op") : (StatusCode.FailedPrecondition, refusal), (ex.StatusCode, ex.Status.Detail));
            }).QuickCheckThrowOnFailure();
        }

        private static object[] Case(string name, string detail, Func<AnnotationContext, RecordingLogger, ServerCallContext, Task> call) =>
            new object[] { name, detail, call };
    }
}
