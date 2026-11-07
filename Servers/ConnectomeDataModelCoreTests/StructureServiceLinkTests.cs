using FsCheck;
using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;
using EfStructureLink = Viking.DataModel.Annotation.StructureLink;
using ProtoStructureLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureLink;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the offline behavior of <see cref="StructureService.CreateStructureLink"/> and
    /// <see cref="StructureService.UpdateLinks"/>: a link from a structure to itself is refused with
    /// InvalidArgument before anything is queried or saved; a link to insert naming a missing structure is
    /// refused with NotFound naming the missing IDs, and nothing is saved; a valid link is saved once, stamped with the
    /// caller's name rather than the payload's; UpdateLinks reads existing links in one query, keeps the
    /// last row of a repeated (SourceId, TargetId) pair, and updates a link that exists instead of inserting it.
    /// Also pins <see cref="StructureService.Merge"/>: a self-merge is refused with InvalidArgument, a missing
    /// structure with NotFound naming it, and a KeepId below MergeId with FailedPrecondition, all before
    /// dbo.MergeStructures runs; and <see cref="StructureService.Split"/>
    /// and <see cref="StructureService.SplitAtLocationLink"/>: a missing location is refused with NotFound naming it, and a
    /// link split of a location from itself with InvalidArgument, before the split procedure runs; a split the procedure
    /// itself refuses (THROW 50000) reaches the caller as FailedPrecondition with the procedure's reason.
    /// No database is contacted: interceptors stand in for the connection, the query and SaveChanges, and
    /// <see cref="RecordingProcedures"/> for the procedure call.
    /// </summary>
    public class StructureServiceLinkTests
    {
        /// <summary>Unroutable endpoint; the interceptor suppresses the save, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task SelfLinkIsRefusedWithoutSaving()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.CreateStructureLink(
                new CreateStructureLinkRequest { NewLink = new ProtoStructureLink { SourceId = 5, TargetId = 5 } },
                new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal(0, saves.Count);
        }

        [Theory]
        [InlineData(99, 2, "99")]
        [InlineData(2, 99, "99")]
        [InlineData(98, 99, "98, 99")]
        public async Task CreateNamingAMissingStructureIsRefusedWithoutSaving(long source, long target, string missing)
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.CreateStructureLink(
                new CreateStructureLinkRequest { NewLink = new ProtoStructureLink { SourceId = source, TargetId = target } },
                new TestCallContext()));

            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
            Assert.Equal($"Structures not found: {missing}", ex.Status.Detail);
            Assert.Equal(0, saves.Count);
        }

        [Fact]
        public async Task CreateLooksUpBothStructuresInOneQuery()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.CreateStructureLink(
                new CreateStructureLinkRequest { NewLink = new ProtoStructureLink { SourceId = 3, TargetId = 8 } },
                new TestCallContext());

            Assert.Equal(1, table.StructureQueries);
            Assert.Equal(2, table.MostStructureParameters);
        }

        [Fact]
        public async Task CreateSavesOnceStampsCallerAndEchoesTheLink()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.CreateStructureLink(
                new CreateStructureLinkRequest
                {
                    NewLink = new ProtoStructureLink { SourceId = 3, TargetId = 8, Bidirectional = true, Tags = "t", Username = "spoofed" }
                },
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { "unknown" }, saves.Usernames);
            Assert.Equal((3L, 8L, true, "t"), (response.Result.SourceId, response.Result.TargetId, response.Result.Bidirectional, response.Result.Tags));
            Assert.Equal("unknown", response.Result.Username);
        }

        [Fact]
        public async Task UpdateLinksRefusesASelfLinkBeforeAnyQueryOrSave()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable();
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.UpdateLinks(
                Request(Link(1, 2), Link(5, 5), Link(3, 4)), new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal("A structure cannot link to itself", ex.Status.Detail);
            Assert.Equal(0, table.Queries);
            Assert.Equal(0, saves.Count);
        }

        [Fact]
        public async Task UpdateLinksKeepsTheLastRowOfARepeatedPair()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(
                Request(Link(3, 8, false, "a"), Link(3, 9, false, "c"), Link(3, 8, true, "b")),
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(
                new[] { (EntityState.Added, 3L, 8L, true, "b"), (EntityState.Added, 3L, 9L, false, "c") },
                saves.Links.OrderBy(l => l.Target).ToArray());
        }

        [Fact]
        public async Task UpdateLinksLooksUpExistingLinksInOneQuery()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(1, 2), Link(1, 3), Link(4, 5)), new TestCallContext());

            Assert.Equal(1, table.LinkQueries);
        }

        [Fact]
        public async Task UpdateLinksLooksUpTheStructuresOfItsInsertsInOneQuery()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(1, 2), Link(1, 3), Link(4, 5)), new TestCallContext());

            Assert.Equal(1, table.StructureQueries);
            Assert.Equal(5, table.MostStructureParameters);
        }

        [Fact]
        public async Task UpdateLinksWithAMissingStructureSavesNothingAndNamesOnlyTheMissingIds()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.UpdateLinks(
                Request(Link(1, 2), Link(3, 99), Link(98, 4), Link(5, 99), Link(6, 7)), new TestCallContext()));

            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
            Assert.Equal(new[] { 98L, 99L }, MissingIds(ex));
            Assert.Equal(0, saves.Count);
        }

        /// <summary>Endpoints of links that are already stored exist (the foreign keys hold them), so an update-only batch looks up no structure.</summary>
        [Fact]
        public async Task UpdateLinksOfStoredLinksOnlyLooksUpNoStructure()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable(new EfStructureLink
            {
                SourceId = 3, TargetId = 8, Tags = "old", Username = "someone", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow
            })
            { StructureExists = _ => false };
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(3, 8, true, "new")), new TestCallContext());

            Assert.Equal(0, table.StructureQueries);
            Assert.Equal(1, saves.Count);
        }

        /// <summary>3,000 distinct structures: the lookup splits into queries under SQL Server's 2,100-parameter limit and the batch still saves once.</summary>
        [Fact]
        public async Task UpdateLinksChunksALargeStructureLookup()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable { StructureExists = _ => true };
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(
                Request(Enumerable.Range(1, 1500).Select(i => Link(i, i + 1500)).ToArray()), new TestCallContext());

            Assert.Equal(2, table.StructureQueries);
            Assert.InRange(table.MostStructureParameters, 1, 2100);
            Assert.Equal(1, saves.Count);
            Assert.Equal(1500, saves.Links.Length);
        }

        private static long[] MissingIds(RpcException ex) =>
            Regex.Matches(ex.Status.Detail, @"\d+").Select(m => long.Parse(m.Value)).OrderBy(id => id).ToArray();

        [Fact]
        public async Task UpdateLinksUpdatesAnExistingLinkInPlace()
        {
            var created = new DateTime(2020, 1, 2, 3, 4, 5);
            var saves = new CaptureSaves();
            var table = new FakeLinkTable(new EfStructureLink
            {
                SourceId = 3, TargetId = 8, Bidirectional = false, Tags = "old", Username = "someone", Created = created, LastModified = created
            });
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(3, 8, true, "new")), new TestCallContext());

            Assert.Equal(new[] { (EntityState.Modified, 3L, 8L, true, "new") }, saves.Links);
            Assert.Equal(new[] { "unknown" }, saves.Usernames);
            var saved = db.ChangeTracker.Entries<EfStructureLink>().Single().Entity;
            Assert.Equal(created, saved.Created);
            Assert.True(saved.LastModified > created);
        }

        /// <summary>
        /// The batch query may return a link that shares a source with one requested pair and a target with
        /// another (the fake returns every stored row, as that over-fetch would); such a link is not requested
        /// and must be left alone, while both requested pairs are inserted.
        /// </summary>
        [Fact]
        public async Task UpdateLinksLeavesAnUnrequestedLinkFromTheQueryAlone()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable(new EfStructureLink
            {
                SourceId = 3, TargetId = 9, Bidirectional = false, Tags = "keep", Username = "someone", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow
            });
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(3, 8, true, "x"), Link(4, 9, true, "y")), new TestCallContext());

            Assert.Equal(
                new[] { (EntityState.Unchanged, 3L, 9L, false, "keep"), (EntityState.Added, 3L, 8L, true, "x"), (EntityState.Added, 4L, 9L, true, "y") },
                saves.Links.OrderBy(l => l.State).ThenBy(l => l.Source).ThenBy(l => l.Target).ToArray());
        }

        [Fact]
        public async Task UpdateLinksWithNoRowsQueriesNothing()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(new UpdateStructureLinksRequest(), new TestCallContext());

            Assert.Equal(0, table.Queries);
        }

        [Fact]
        public async Task SelfMergeIsRefusedBeforeAnyQueryOrProcedure()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.Merge(
                new MergeRequest { KeepId = 5, MergeId = 5 }, new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal("A structure cannot be merged into itself", ex.Status.Detail);
            Assert.Equal(0, table.Queries);
            Assert.Empty(procedures.Merges);
        }

        [Theory]
        [InlineData(99, 2, "99")]
        [InlineData(2, 99, "99")]
        [InlineData(98, 99, "98, 99")]
        public async Task MergeNamingAMissingStructureIsRefusedBeforeTheProcedure(long keep, long merge, string missing)
        {
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable());
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.Merge(
                new MergeRequest { KeepId = keep, MergeId = merge }, new TestCallContext()));

            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
            Assert.Equal($"Structures not found: {missing}", ex.Status.Detail);
            Assert.Empty(procedures.Merges);
        }

        /// <summary>Both structures are roots: one query checks both exist, and one more reads KeepId's ParentID.</summary>
        [Fact]
        public async Task MergeLooksUpBothStructuresInOneQueryThenRunsTheProcedureOnce()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.Merge(new MergeRequest { KeepId = 3, MergeId = 8 }, new TestCallContext());

            Assert.Equal(3, response.KeptId);
            Assert.Equal(new (long?, long?)[] { (3, 8) }, procedures.Merges);
            Assert.Equal(2, table.StructureQueries);
            Assert.Equal(2, table.MostStructureParameters);
        }

        /// <summary>
        /// dbo.MergeStructures gives MergeId's children to KeepId, so a KeepId below MergeId would become its own parent
        /// or close a parent cycle (before DBVersion 94), or fail with error 50000 reported as Unknown (since).
        /// Parents are "child:parent" pairs.
        /// </summary>
        [Theory]
        [InlineData("4:8")]
        [InlineData("4:6 6:8")]
        [InlineData("4:6 6:5 5:8 8:2")]
        public async Task MergeIntoADescendantIsRefusedBeforeTheProcedure(string parents)
        {
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable { Parents = ParseParents(parents) });
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.Merge(
                new MergeRequest { KeepId = 4, MergeId = 8 }, new TestCallContext()));

            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
            Assert.Equal("Structure 8 cannot be merged into 4, which lies below it", ex.Status.Detail);
            Assert.Empty(procedures.Merges);
        }

        /// <summary>KeepId under another structure, above MergeId, beside it, or on a stored cycle that does not reach it.</summary>
        [Theory]
        [InlineData("4:6")]
        [InlineData("8:4")]
        [InlineData("8:6 6:4")]
        [InlineData("4:6 8:6")]
        [InlineData("4:5 5:4")]
        public async Task MergeIntoAStructureNotBelowMergeIdRunsTheProcedureOnce(string parents)
        {
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable { Parents = ParseParents(parents) });
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.Merge(new MergeRequest { KeepId = 4, MergeId = 8 }, new TestCallContext());

            Assert.Equal(4, response.KeptId);
            Assert.Equal(new (long?, long?)[] { (4, 8) }, procedures.Merges);
        }

        /// <summary>
        /// Merges of two distinct structures of a random stored forest: refused exactly when MergeId is on the way up
        /// from KeepId, and otherwise passed to the procedure once.
        /// </summary>
        [Fact]
        public void MergeIsRefusedExactlyWhenKeepIdLiesBelowMergeId()
        {
            var cases =
                from n in Gen.Choose(2, 7)
                from parents in Gen.Sequence(Enumerable.Range(1, n).Select(i => Gen.Choose(0, i - 1)))
                from keep in Gen.Choose(1, n)
                from merge in Gen.Choose(1, n).Where(m => m != keep)
                select (Parents: parents.ToArray(), Keep: (long)keep, Merge: (long)merge);

            Prop.ForAll(Arb.From(cases), c =>
            {
                var stored = Enumerable.Range(1, c.Parents.Length).Where(i => c.Parents[i - 1] != 0)
                    .ToDictionary(i => (long)i, i => (long)c.Parents[i - 1]);
                var below = false;
                for (long? p = c.Keep; p.HasValue && !below; p = stored.TryGetValue(p.Value, out var up) ? up : null)
                    below = p == c.Merge;

                using var db = NewContext(new CaptureSaves(), new FakeLinkTable { Parents = stored });
                var procedures = new RecordingProcedures(db);
                db.Procedures = procedures;
                var merge = Task.Run(() => new StructureService(db, NullLogger<StructureService>.Instance)
                    .Merge(new MergeRequest { KeepId = c.Keep, MergeId = c.Merge }, new TestCallContext()));
                var refused = Record.Exception(() => merge.GetAwaiter().GetResult()) is RpcException { StatusCode: StatusCode.FailedPrecondition };

                Assert.Equal(below, refused);
                Assert.Equal(below ? 0 : 1, procedures.Merges.Count);
            }).QuickCheckThrowOnFailure();
        }

        private static Dictionary<long, long> ParseParents(string pairs) =>
            pairs.Split(' ').Select(p => p.Split(':')).ToDictionary(p => long.Parse(p[0]), p => long.Parse(p[1]));

        [Theory]
        [InlineData(99)]
        [InlineData(0)]
        public async Task SplitAtAMissingLocationIsRefusedBeforeTheProcedure(long location)
        {
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable());
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.Split(
                new SplitRequest { Id = 3, FirstLocationIdOfSplitStructure = location }, new TestCallContext()));

            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
            Assert.Equal($"Locations not found: {location}", ex.Status.Detail);
            Assert.Empty(procedures.Splits);
        }

        [Fact]
        public async Task SplitLooksUpTheLocationOnceThenRunsTheProcedureOnce()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var procedures = new RecordingProcedures(db) { NewStructureId = 42 };
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.Split(
                new SplitRequest { Id = 3, FirstLocationIdOfSplitStructure = 7 }, new TestCallContext());

            Assert.Equal(42, response.SplitStructureId);
            Assert.Equal(new long?[] { 7 }, procedures.Splits);
            Assert.Equal(1, table.LocationQueries);
            Assert.Equal(1, table.Queries);
        }

        [Fact]
        public async Task SplitAtLocationLinkFromItselfIsRefusedBeforeAnyQueryOrProcedure()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.SplitAtLocationLink(
                new SplitAtLocationLinkRequest { LocationIdOfKeepStructure = 5, LocationIdOfSplitStructure = 5 }, new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal("A location cannot be split from itself", ex.Status.Detail);
            Assert.Equal(0, table.Queries);
            Assert.Empty(procedures.LinkSplits);
        }

        [Theory]
        [InlineData(99, 2, "99")]
        [InlineData(2, 99, "99")]
        [InlineData(98, 99, "98, 99")]
        public async Task SplitAtLocationLinkNamingAMissingLocationIsRefusedBeforeTheProcedure(long keep, long split, string missing)
        {
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable());
            var procedures = new RecordingProcedures(db);
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.SplitAtLocationLink(
                new SplitAtLocationLinkRequest { LocationIdOfKeepStructure = keep, LocationIdOfSplitStructure = split }, new TestCallContext()));

            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
            Assert.Equal($"Locations not found: {missing}", ex.Status.Detail);
            Assert.Empty(procedures.LinkSplits);
        }

        [Fact]
        public async Task SplitAtLocationLinkLooksUpBothLocationsInOneQueryThenRunsTheProcedureOnce()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var procedures = new RecordingProcedures(db) { NewStructureId = 42 };
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.SplitAtLocationLink(
                new SplitAtLocationLinkRequest { LocationIdOfKeepStructure = 3, LocationIdOfSplitStructure = 8 }, new TestCallContext());

            Assert.Equal(42, response.SplitStructureId);
            Assert.Equal(new (long?, long?)[] { (3, 8) }, procedures.LinkSplits);
            Assert.Equal(1, table.LocationQueries);
            Assert.Equal(1, table.Queries);
        }

        /// <summary>
        /// dbo.SplitStructure refuses a split whose location is linked to every other location of the cell (THROW 50000):
        /// the caller gets FailedPrecondition with the procedure's reason, not Unknown "Split".
        /// </summary>
        [Fact]
        public async Task SplitRefusedByTheProcedureReportsItsReasonAsFailedPrecondition()
        {
            const string reason = "The split structure is connected to the entire keep cell.  Break a location link to create two subgraphs and try again";
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable());
            var procedures = new RecordingProcedures(db) { SplitFailure = SqlExceptionFactory.Create((50000, reason)) };
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.Split(
                new SplitRequest { Id = 3, FirstLocationIdOfSplitStructure = 7 }, new TestCallContext()));

            Assert.Equal((StatusCode.FailedPrecondition, reason), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new long?[] { 7 }, procedures.Splits);
        }

        /// <summary>dbo.SplitStructureAtLocationLink refuses two unlinked locations (THROW 50000) with FailedPrecondition and its reason.</summary>
        [Fact]
        public async Task SplitAtLocationLinkRefusedByTheProcedureReportsItsReasonAsFailedPrecondition()
        {
            const string reason = "The Split and Keep Location IDs must be linked";
            using var db = NewContext(new CaptureSaves(), new FakeLinkTable());
            var procedures = new RecordingProcedures(db) { SplitFailure = SqlExceptionFactory.Create((50000, reason)) };
            db.Procedures = procedures;
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.SplitAtLocationLink(
                new SplitAtLocationLinkRequest { LocationIdOfKeepStructure = 3, LocationIdOfSplitStructure = 8 }, new TestCallContext()));

            Assert.Equal((StatusCode.FailedPrecondition, reason), (ex.StatusCode, ex.Status.Detail));
            Assert.Equal(new (long?, long?)[] { (3, 8) }, procedures.LinkSplits);
        }

        /// <summary>
        /// Records each dbo.MergeStructures, dbo.SplitStructure and dbo.SplitStructureAtLocationLink call instead of
        /// executing it; a split answers <see cref="NewStructureId"/> as the new structure.
        /// </summary>
        private sealed class RecordingProcedures : AnnotationContextProcedures
        {
            /// <summary>The procedures' OutputParameter setter is internal to ConnectomeDataModelCore.</summary>
            private static readonly MethodInfo SetOutput =
                typeof(OutputParameter<long?>).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.NonPublic);

            public RecordingProcedures(AnnotationContext context) : base(context) { }

            public long? NewStructureId { get; init; }

            /// <summary>Thrown by a split after it is recorded, as the procedure's THROW reaches the service; null to succeed.</summary>
            public Exception SplitFailure { get; init; }

            public List<(long? Keep, long? Merge)> Merges { get; } = new();

            public List<long?> Splits { get; } = new();

            public List<(long? Keep, long? Split)> LinkSplits { get; } = new();

            public override Task<MergeStructuresResult[]> MergeStructuresAsync(long? KeepStructureID, long? MergeStructureID, OutputParameter<int> returnValue = null, CancellationToken cancellationToken = default)
            {
                Merges.Add((KeepStructureID, MergeStructureID));
                return Task.FromResult(Array.Empty<MergeStructuresResult>());
            }

            public override Task<SplitStructureResult[]> SplitStructureAsync(long? LocationIDOfSplitStructure, OutputParameter<long?> SplitStructureID, OutputParameter<int> returnValue = null, CancellationToken cancellationToken = default)
            {
                Splits.Add(LocationIDOfSplitStructure);
                if (SplitFailure != null)
                    throw SplitFailure;
                SetOutput.Invoke(SplitStructureID, new object[] { NewStructureId });
                return Task.FromResult(Array.Empty<SplitStructureResult>());
            }

            public override Task<SplitStructureAtLocationLinkResult[]> SplitStructureAtLocationLinkAsync(long? LocationIDOfKeepStructure, long? LocationIDOfSplitStructure, OutputParameter<long?> SplitStructureID, OutputParameter<int> returnValue = null, CancellationToken cancellationToken = default)
            {
                LinkSplits.Add((LocationIDOfKeepStructure, LocationIDOfSplitStructure));
                if (SplitFailure != null)
                    throw SplitFailure;
                SetOutput.Invoke(SplitStructureID, new object[] { NewStructureId });
                return Task.FromResult(Array.Empty<SplitStructureAtLocationLinkResult>());
            }
        }

        private static UpdateStructureLinksRequest Request(params ProtoStructureLink[] links)
        {
            var request = new UpdateStructureLinksRequest();
            request.Objs.AddRange(links);
            return request;
        }

        private static ProtoStructureLink Link(long source, long target, bool bidirectional = false, string tags = "") =>
            new ProtoStructureLink { SourceId = source, TargetId = target, Bidirectional = bidirectional, Tags = tags };

        private static AnnotationContext NewContext(params IInterceptor[] interceptors) =>
            new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptors)
                .Options);

        /// <summary>
        /// Suppresses the database write, counts SaveChanges calls and records the links being saved: the
        /// usernames of the added and modified ones, and the state and values of every tracked one.
        /// </summary>
        private sealed class CaptureSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }
            public string[] Usernames { get; private set; } = Array.Empty<string>();
            public (EntityState State, long Source, long Target, bool Bidirectional, string Tags)[] Links { get; private set; } =
                Array.Empty<(EntityState, long, long, bool, string)>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                var entries = eventData.Context.ChangeTracker.Entries<EfStructureLink>().ToArray();
                Usernames = entries.Where(e => e.State is EntityState.Added or EntityState.Modified).Select(e => e.Entity.Username).ToArray();
                Links = entries.Select(e => (e.State, e.Entity.SourceId, e.Entity.TargetId, e.Entity.Bidirectional, e.Entity.Tags)).ToArray();
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(eventData.Context.ChangeTracker.Entries().Count()));
            }
        }

        /// <summary>
        /// Stands in for the StructureLink, Structure and Location tables: opening the connection is suppressed, a
        /// Structure or Location query is answered with the IDs among its parameters that <see cref="StructureExists"/>
        /// or <see cref="LocationExists"/> accepts (a Structure query selecting whole rows, as Find does, with each one's
        /// <see cref="Parents"/> entry), and every other query with all stored links, whatever its WHERE clause says, in the column
        /// order its SELECT list names. Counts the queries.
        /// </summary>
        private sealed class FakeLinkTable : IDbConnectionInterceptor, IDbCommandInterceptor
        {
            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");
            private readonly EfStructureLink[] _rows;

            public FakeLinkTable(params EfStructureLink[] rows) => _rows = rows;

            /// <summary>Stored structures: 1 to 9 by default, so a test names a missing structure with 98 or 99.</summary>
            public Func<long, bool> StructureExists { get; init; } = id => id is >= 1 and <= 9;

            /// <summary>Stored ParentIDs; a structure with no entry is a root.</summary>
            public Dictionary<long, long> Parents { get; init; } = new();

            /// <summary>Stored locations: 1 to 9 by default, so a test names a missing location with 98 or 99.</summary>
            public Func<long, bool> LocationExists { get; init; } = id => id is >= 1 and <= 9;

            public int Queries { get; private set; }

            public int LinkQueries => Queries - StructureQueries - LocationQueries;

            public int StructureQueries { get; private set; }

            public int LocationQueries { get; private set; }

            public int MostStructureParameters { get; private set; }

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Queries++;
                var selectList = command.CommandText.Substring(0, command.CommandText.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).ToArray();
                var table = new DataTable();
                if (command.CommandText.Contains("FROM [Structure] AS", StringComparison.Ordinal))
                {
                    StructureQueries++;
                    MostStructureParameters = Math.Max(MostStructureParameters, command.Parameters.Count);
                    return columns.Length == 1
                        ? ExistingIds(table, columns[0], command, StructureExists)
                        : StructureRows(table, columns, command);
                }

                if (command.CommandText.Contains("FROM [Location] AS", StringComparison.Ordinal))
                {
                    LocationQueries++;
                    return ExistingIds(table, Assert.Single(columns), command, LocationExists);
                }

                foreach (var column in columns)
                    table.Columns.Add(column, ColumnType(column));
                foreach (var row in _rows)
                    table.Rows.Add(columns.Select(c => ColumnValue(row, c)).ToArray());
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }

            private static ValueTask<InterceptionResult<DbDataReader>> ExistingIds(DataTable table, string column, DbCommand command, Func<long, bool> exists)
            {
                table.Columns.Add(column, typeof(long));
                foreach (var id in command.Parameters.Cast<DbParameter>().Select(p => Convert.ToInt64(p.Value)).Distinct().Where(exists))
                    table.Rows.Add(id);
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }

            private ValueTask<InterceptionResult<DbDataReader>> StructureRows(DataTable table, string[] columns, DbCommand command)
            {
                foreach (var column in columns)
                    table.Columns.Add(column, StructureColumnType(column));
                foreach (var id in command.Parameters.Cast<DbParameter>().Select(p => Convert.ToInt64(p.Value)).Distinct().Where(StructureExists))
                    table.Rows.Add(columns.Select(c => StructureColumnValue(id, c)).ToArray());
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }

            private static Type StructureColumnType(string column) => column switch
            {
                "ID" or "ParentID" or "TypeID" => typeof(long),
                "Confidence" => typeof(double),
                "Verified" => typeof(bool),
                "Label" or "Notes" or "Tags" or "Username" => typeof(string),
                "Created" or "LastModified" => typeof(DateTime),
                "Version" => typeof(byte[]),
                _ => throw new InvalidOperationException($"Unexpected Structure column {column}")
            };

            private object StructureColumnValue(long id, string column) => column switch
            {
                "ID" => id,
                "ParentID" => Parents.TryGetValue(id, out var parent) ? (object)parent : DBNull.Value,
                "TypeID" => 1L,
                "Confidence" => 0.5,
                "Verified" => false,
                "Username" => "someone",
                "Created" or "LastModified" => new DateTime(2026, 1, 1),
                "Version" => new byte[8],
                _ => DBNull.Value
            };

            private static Type ColumnType(string column) => column switch
            {
                "SourceID" or "TargetID" => typeof(long),
                "Bidirectional" => typeof(bool),
                "Tags" or "Username" => typeof(string),
                "Created" or "LastModified" => typeof(DateTime),
                _ => throw new InvalidOperationException($"Unexpected StructureLink column {column}")
            };

            private static object ColumnValue(EfStructureLink row, string column) => column switch
            {
                "SourceID" => row.SourceId,
                "TargetID" => row.TargetId,
                "Bidirectional" => row.Bidirectional,
                "Tags" => row.Tags,
                "Username" => row.Username,
                "Created" => row.Created,
                "LastModified" => row.LastModified,
                _ => throw new InvalidOperationException($"Unexpected StructureLink column {column}")
            };
        }

        /// <summary>Call context carrying an anonymous HTTP context (as ASP.NET Core hosting supplies) and an uncancelled token.</summary>
        private sealed class TestCallContext : ServerCallContext
        {
            /// <summary>Key Grpc.AspNetCore's GetHttpContext reads from <see cref="ServerCallContext.UserState"/>.</summary>
            private const string HttpContextKey = "__HttpContext";

            public TestCallContext() => UserState[HttpContextKey] = new DefaultHttpContext();

            protected override string MethodCore => "test";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = new();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
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
