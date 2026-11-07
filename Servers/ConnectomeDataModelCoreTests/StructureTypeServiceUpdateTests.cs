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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;
using EfStructureType = Viking.DataModel.Annotation.StructureType;
using ProtoStructureType = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins that the batch <see cref="StructureTypeService.Update"/> reports each created row with the
    /// Id the database assigned, as <see cref="StructureTypeService.CreateStructureType"/> and the
    /// structure batch Update do, and that each change sees the changes before it in the same batch:
    /// an update or delete of a type the batch deleted fails its own row and the delete stands. A delete of a
    /// type still in use, or a create or move under a parent that is missing or that the batch deleted, fails its own row instead of
    /// failing the whole save, and so does a move of a type under itself or one of its descendants. No database is contacted: interceptors stand in for the connection, the Id lookups and
    /// SaveChanges, which assigns identity values the way SQL Server would.
    /// </summary>
    public class StructureTypeServiceUpdateTests
    {
        /// <summary>Unroutable endpoint; the interceptors suppress the connection, queries and save.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task BatchCreateRepliesWithDatabaseAssignedIds()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity);

            var response = await Update(db,
                new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "A", Code = "A" } },
                new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "B", Code = "B" } });

            Assert.Equal(1, identity.SaveCount);
            Assert.All(response.Results, r => Assert.True(r.Success));
            Assert.Equal(new long[] { 1000, 1001 }, response.Results.Select(r => r.Created.Id).ToArray());
            Assert.Equal(new[] { "A", "B" }, response.Results.Select(r => r.Created.Name).ToArray());
        }

        [Fact]
        public async Task ValidBatchAppliesEachChangeToItsOwnId()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old"), Stored(2, "Gone")));

            var response = await Update(db, UpdateOf(1, "New"), DeleteOf(2), UpdateOf(3, "Missing"));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { true, true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal("New", response.Results[0].Updated.Name);
            Assert.Equal(2, response.Results[1].DeletedId);
            Assert.Equal(new[] { (EntityState.Deleted, 2L, "Gone"), (EntityState.Modified, 1L, "New") },
                identity.Types.OrderBy(t => t.State).ToArray());
        }

        [Fact]
        public async Task UpdateThenDeleteOfOneIdDeletesIt()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, UpdateOf(1, "New"), DeleteOf(1));

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        [Fact]
        public async Task DeleteThenUpdateKeepsTheDeleteAndFailsTheUpdate()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, DeleteOf(1), UpdateOf(1, "New"));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[1].Updated);
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        [Fact]
        public async Task RepeatedDeleteFailsTheRepeat()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, DeleteOf(1), DeleteOf(1));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        /// <summary>Every foreign key into StructureType that is not the type's own ParentID.</summary>
        public static TheoryData<string, string> TypeReferences => new()
        {
            { "Structure", "TypeID" },
            { "StructureTemplates", "StructureTypeID" },
            { "PermittedStructureLink", "SourceTypeID" },
            { "PermittedStructureLink", "TargetTypeID" },
        };

        [Theory]
        [MemberData(nameof(TypeReferences))]
        public async Task DeleteOfReferencedTypeFailsItsRowAndKeepsTheRest(string table, string column)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Used"), Stored(2, "Free"), Stored(3, "Old"))
            {
                References = { (table, column, 1) },
            };
            using var db = NewContext(identity, tables);

            var response = await Update(db, DeleteOf(1), DeleteOf(2), UpdateOf(3, "New"));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { false, true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(0, response.Results[0].DeletedId);
            Assert.Equal(new[] { (EntityState.Unchanged, 1L, "Used"), (EntityState.Deleted, 2L, "Free"), (EntityState.Modified, 3L, "New") },
                identity.Types.OrderBy(t => t.Id).ToArray());
        }

        [Fact]
        public async Task DeleteOfParentWithLiveChildFails()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1)));

            var response = await Update(db, DeleteOf(1));

            Assert.False(Assert.Single(response.Results).Success);
            Assert.DoesNotContain(identity.Types, t => t.State == EntityState.Deleted);
        }

        [Fact]
        public async Task DeleteOfParentAndChildTogetherDeletesBoth()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1)));

            var response = await Update(db, DeleteOf(1), DeleteOf(2));

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.All(identity.Types, t => Assert.Equal(EntityState.Deleted, t.State));
        }

        [Fact]
        public async Task DeleteOfAncestorsOfAReferencedTypeFailsTheWholeChain()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Root"), Stored(2, "Middle", parentId: 1), Stored(3, "Leaf", parentId: 2))
            {
                References = { ("Structure", "TypeID", 3) },
            };
            using var db = NewContext(identity, tables);

            var response = await Update(db, DeleteOf(1), DeleteOf(2), DeleteOf(3));

            Assert.Equal(new[] { false, false, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.DoesNotContain(identity.Types, t => t.State == EntityState.Deleted);
        }

        [Fact]
        public async Task CreateUnderMissingParentFailsItsRowAndKeepsTheRest()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Old")));

            var response = await Update(db, CreateOf("Orphan", parentId: 99), CreateOf("Child", parentId: 1), UpdateOf(2, "New"));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { false, true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[0].Created);
            Assert.Equal(1000, response.Results[1].Created.Id);
            Assert.Equal(new[] { (EntityState.Modified, "New"), (EntityState.Added, "Child") },
                identity.Types.OrderBy(t => t.State).Select(t => (t.State, t.Name)).ToArray());
        }

        [Fact]
        public async Task CreateUnderParentTheBatchDeletedFailsItsRow()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent")));

            var response = await Update(db, DeleteOf(1), CreateOf("Child", parentId: 1));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DeleteOfParentTheBatchGaveAChildFailsAndKeepsTheChild(bool byCreate)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Other")));

            var response = await Update(db, byCreate ? CreateOf("Child", parentId: 1) : UpdateOf(2, "Moved", parentId: 1), DeleteOf(1));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.DoesNotContain(identity.Types, t => t.State == EntityState.Deleted);
        }

        /// <summary>
        /// A delete that fails leaves the type in place, so a later create or move under it must still succeed:
        /// only a delete the batch applies takes the parent out of the live set.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task FailedDeleteOfParentKeepsItLiveForALaterChild(bool byCreate)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1), Stored(3, "Other")));

            var response = await Update(db, DeleteOf(1), byCreate ? CreateOf("New", parentId: 1) : UpdateOf(3, "Moved", parentId: 1));

            Assert.Equal(new[] { false, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(1, byCreate ? response.Results[1].Created.ParentId : response.Results[1].Updated.ParentId);
            Assert.DoesNotContain(identity.Types, t => t.State == EntityState.Deleted);
        }

        [Fact]
        public async Task UpdateUnderMissingParentFailsItsRowAndKeepsTheRest()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Old"), Stored(3, "Other")));

            var response = await Update(db, UpdateOf(2, "Lost", parentId: 99), UpdateOf(3, "Moved", parentId: 1));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { false, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[0].Updated);
            Assert.Equal(1, response.Results[1].Updated.ParentId);
            Assert.Equal(new[] { (EntityState.Unchanged, 1L, "Parent"), (EntityState.Unchanged, 2L, "Old"), (EntityState.Modified, 3L, "Moved") },
                identity.Types.OrderBy(t => t.Id).ToArray());
        }

        [Fact]
        public async Task UpdateUnderParentTheBatchDeletedFailsItsRow()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Old")));

            var response = await Update(db, DeleteOf(1), UpdateOf(2, "Moved", parentId: 1));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[1].Updated);
            Assert.Equal(new[] { (EntityState.Deleted, 1L, "Parent"), (EntityState.Unchanged, 2L, "Old") },
                identity.Types.OrderBy(t => t.Id).ToArray());
        }

        /// <summary>Batches that edit a stored child, keeping its parent, around deletes of both.</summary>
        public static TheoryData<int> KeptParentOrders => new() { 0, 1 };

        [Theory]
        [MemberData(nameof(KeptParentOrders))]
        public async Task UpdateThatKeepsItsParentAroundTheirDeletesSucceeds(int order)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1)));

            var response = order == 0
                ? await Update(db, DeleteOf(1), UpdateOf(2, "New", parentId: 1), DeleteOf(2))
                : await Update(db, UpdateOf(2, "New", parentId: 1), DeleteOf(1), DeleteOf(2));

            Assert.Equal(new[] { true, true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.All(identity.Types, t => Assert.Equal(EntityState.Deleted, t.State));
        }

        [Fact]
        public async Task BatchNamingNoParentRunsNoParentQuery()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1));
            using var db = NewContext(identity, tables);

            var response = await Update(db, UpdateOf(2, "Root"), CreateOf("Free"));

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.False(response.Results[0].Updated.HasParentId);
            Assert.DoesNotContain(tables.TypeSelects, s => s == "ID");
        }

        [Fact]
        public async Task ParentsOfABatchAreLookedUpInOneQuery()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Other"));
            using var db = NewContext(identity, tables);

            var response = await Update(db, CreateOf("A", parentId: 1), CreateOf("B", parentId: 1), UpdateOf(2, "Moved", parentId: 1));

            Assert.All(response.Results, r => Assert.True(r.Success));
            Assert.Single(tables.TypeSelects, s => s == "ID");
        }

        [Fact]
        public async Task MoveUnderItselfFailsItsRowAndKeepsTheRest()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Old"), Stored(2, "Other"));
            using var db = NewContext(identity, tables);

            var response = await Update(db, UpdateOf(1, "Self", parentId: 1), UpdateOf(2, "Moved", parentId: 1));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { false, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[0].Updated);
            Assert.Equal(new[] { (EntityState.Unchanged, 1L, "Old"), (EntityState.Modified, 2L, "Moved") },
                identity.Types.OrderBy(t => t.Id).ToArray());
            Assert.Null(TrackedParents(db)[1]);
        }

        /// <summary>Stored chain 1 (root) &lt;- 2 &lt;- 3 &lt;- 4: the root moved under any of its descendants.</summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task MoveUnderOwnDescendantFailsItsRow(long descendant)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(
                Stored(1, "Root"), Stored(2, "Child", parentId: 1), Stored(3, "Grandchild", parentId: 2), Stored(4, "Leaf", parentId: 3)));

            var response = await Update(db, UpdateOf(1, "Looped", parentId: descendant));

            Assert.False(Assert.Single(response.Results).Success);
            Assert.Null(response.Results[0].Updated);
            Assert.DoesNotContain(identity.Types, t => t.State == EntityState.Modified);
        }

        [Fact]
        public async Task MoveUnderADescendantMadeEarlierInTheBatchFailsTheLaterRow()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "A"), Stored(2, "B")));

            var response = await Update(db, UpdateOf(2, "B", parentId: 1), UpdateOf(1, "A", parentId: 2));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(new long?[] { null, 1 }, TrackedParents(db).OrderBy(p => p.Key).Select(p => p.Value).ToArray());
        }

        [Fact]
        public async Task MoveUnderSiblingOrAncestorOrSameParentSucceeds()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(
                Stored(1, "Root"), Stored(2, "Left", parentId: 1), Stored(3, "Right", parentId: 1), Stored(4, "Leaf", parentId: 3)));

            var response = await Update(db, UpdateOf(2, "Left", parentId: 3), UpdateOf(4, "Leaf", parentId: 1), UpdateOf(3, "Right", parentId: 1));

            Assert.All(response.Results, r => Assert.True(r.Success));
            Assert.Equal(new long?[] { 3, 1, 1 }, response.Results.Select(r => (long?)r.Updated.ParentId).ToArray());
        }

        /// <summary>
        /// A cycle already stored (1 and 2 name each other) that does not pass through the moved type ends the walk
        /// instead of looping forever, and the move under it stands.
        /// </summary>
        [Fact]
        public async Task StoredCycleAboveTheNewParentEndsTheWalk()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "A", parentId: 2), Stored(2, "B", parentId: 1), Stored(3, "C")));

            var response = await Task.Run(() => Update(db, UpdateOf(3, "C", parentId: 1))).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(Assert.Single(response.Results).Success);
        }

        /// <summary>A row that keeps its parent, or names itself, needs no lookup beyond the batch's own two queries.</summary>
        [Theory]
        [InlineData(1L)]
        [InlineData(2L)]
        public async Task KeptOrSelfParentRunsNoAncestorQuery(long parentId)
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var tables = new FakeStructureTypeTable(Stored(1, "Parent"), Stored(2, "Child", parentId: 1));
            using var db = NewContext(identity, tables);

            var response = await Update(db, UpdateOf(2, "Child", parentId: parentId));

            Assert.Equal(parentId == 1, Assert.Single(response.Results).Success);
            Assert.Equal(2, tables.TypeSelects.Count);
        }

        /// <summary>
        /// Moves (moved, newParent) over a random stored forest, applied in order: each fails exactly when the new
        /// parent is the moved type or below it in the tree the earlier moves left, and the saved tree is that tree.
        /// </summary>
        [Fact]
        public void MovesFailExactlyWhenTheyWouldCloseACycle()
        {
            var cases =
                from n in Gen.Choose(2, 7)
                from parents in Gen.Sequence(Enumerable.Range(1, n).Select(i => Gen.Choose(0, i - 1)))
                from count in Gen.Choose(1, 4)
                from moves in Gen.ArrayOf(count, Gen.Two(Gen.Choose(1, n)))
                select (Parents: parents.ToArray(), Moves: moves);

            Prop.ForAll(Arb.From(cases), c =>
            {
                var expectedParents = Enumerable.Range(1, c.Parents.Length).ToDictionary(i => (long)i, i => c.Parents[i - 1] == 0 ? (long?)null : c.Parents[i - 1]);
                var expectedSuccess = c.Moves.Select(m =>
                {
                    for (long? p = m.Item2; p.HasValue; p = expectedParents[p.Value])
                    {
                        if (p == m.Item1)
                            return false;
                    }
                    expectedParents[m.Item1] = m.Item2;
                    return true;
                }).ToArray();

                using var db = NewContext(new IdentityOnSave(firstId: 1000),
                    new FakeStructureTypeTable(expectedParents.Keys.Select(id => Stored(id, "T", c.Parents[id - 1] == 0 ? null : c.Parents[id - 1])).ToArray()));
                var response = Task.Run(() => Update(db, c.Moves.Select(m => UpdateOf(m.Item1, "T", parentId: m.Item2)).ToArray())).GetAwaiter().GetResult();

                Assert.Equal(expectedSuccess, response.Results.Select(r => r.Success).ToArray());
                Assert.All(TrackedParents(db), p => Assert.Equal(expectedParents[p.Key], p.Value));
            }).QuickCheckThrowOnFailure();
        }

        private static Dictionary<long, long?> TrackedParents(AnnotationContext db) =>
            db.ChangeTracker.Entries<EfStructureType>().ToDictionary(e => e.Entity.Id, e => e.Entity.ParentId);

        private static Task<UpdateStructureTypesResponse> Update(AnnotationContext db, params StructureTypeChangeRequest[] changes)
        {
            var request = new UpdateStructureTypesRequest();
            request.Objs.AddRange(changes);
            return new StructureTypeService(db, NullLogger<StructureTypeService>.Instance).Update(request, new TestCallContext());
        }

        private static StructureTypeChangeRequest UpdateOf(long id, string name, long? parentId = null)
        {
            var type = new ProtoStructureType { Id = id, Name = name, Code = "C" };
            if (parentId.HasValue)
                type.ParentId = parentId.Value;
            return new() { Update = type };
        }

        private static StructureTypeChangeRequest CreateOf(string name, long? parentId = null)
        {
            var type = new ProtoStructureType { Name = name, Code = name };
            if (parentId.HasValue)
                type.ParentId = parentId.Value;
            return new() { Create = type };
        }

        private static StructureTypeChangeRequest DeleteOf(long id) => new() { Delete = id };

        private static EfStructureType Stored(long id, string name, long? parentId = null) => new()
        {
            Id = id, ParentId = parentId, Name = name, Code = "C", MarkupType = "Point", HotKey = "\0", Username = "stored", Version = new byte[8],
            Created = new DateTime(2020, 1, 1), LastModified = new DateTime(2020, 1, 1),
        };

        private static AnnotationContext NewContext(params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptors)
                .Options);

        /// <summary>
        /// Suppresses the database write, gives each added structure type the next identity value, leaving
        /// the tracked entity as it would be after a real insert, and records the state of every tracked type.
        /// </summary>
        private sealed class IdentityOnSave(long firstId) : SaveChangesInterceptor
        {
            private long _next = firstId;

            /// <summary>Number of SaveChanges calls seen; the batch must save once.</summary>
            public int SaveCount { get; private set; }

            /// <summary>State, Id and Name of every tracked structure type at the last save.</summary>
            public (EntityState State, long Id, string Name)[] Types { get; private set; } = Array.Empty<(EntityState, long, string)>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                var entries = eventData.Context.ChangeTracker.Entries<EfStructureType>().ToList();
                Types = entries.Select(e => (e.State, e.Entity.Id, e.Entity.Name)).ToArray();
                var added = entries.Where(e => e.State == EntityState.Added).ToList();
                foreach (var entry in added)
                {
                    var id = entry.Property(e => e.Id);
                    id.CurrentValue = _next++;
                    id.IsTemporary = false;
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(added.Count));
            }
        }

        /// <summary>
        /// Stands in for the StructureType table and the tables that reference it: opening the connection is
        /// suppressed, and each query (<c>WHERE [t].[Column] = @p</c> or <c>IN (@p1, ...)</c>) is answered with
        /// the rows of its table whose column holds a parameter value: StructureType rows from <c>rows</c>, in
        /// the column order its SELECT list names, and the referencing type Ids from <see cref="References"/>.
        /// </summary>
        private sealed class FakeStructureTypeTable(params EfStructureType[] rows) : IDbConnectionInterceptor, IDbCommandInterceptor
        {
            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");
            private static readonly Regex FromTable = new(@"FROM \[(\w+)\]");
            private static readonly Regex Filter = new(@"WHERE \[\w+\]\.\[(\w+)\] (?:=|IN)");

            /// <summary>Rows of other tables naming a structure type: (table, column, type Id).</summary>
            public List<(string Table, string Column, long TypeId)> References { get; } = new();

            /// <summary>Selected columns, comma-joined, of each StructureType query in the order they ran.</summary>
            public List<string> TypeSelects { get; } = new();

            private static readonly Dictionary<string, (Type Type, Func<EfStructureType, object> Value)> Columns = new()
            {
                ["ID"] = (typeof(long), t => t.Id),
                ["ParentID"] = (typeof(long), t => t.ParentId),
                ["Abstract"] = (typeof(bool), t => t.Abstract),
                ["Code"] = (typeof(string), t => t.Code),
                ["Color"] = (typeof(int), t => t.Color),
                ["Created"] = (typeof(DateTime), t => t.Created),
                ["HotKey"] = (typeof(string), t => t.HotKey),
                ["LastModified"] = (typeof(DateTime), t => t.LastModified),
                ["MarkupType"] = (typeof(string), t => t.MarkupType),
                ["Name"] = (typeof(string), t => t.Name),
                ["Notes"] = (typeof(string), t => t.Notes),
                ["StructureTags"] = (typeof(string), t => t.StructureTags),
                ["Tags"] = (typeof(string), t => t.Tags),
                ["Username"] = (typeof(string), t => t.Username),
                ["Version"] = (typeof(byte[]), t => t.Version),
            };

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                var text = command.CommandText;
                var ids = command.Parameters.Cast<DbParameter>().Select(p => Convert.ToInt64(p.Value)).ToHashSet();
                var selectList = text.Substring(0, text.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).ToArray();
                var fromTable = FromTable.Match(text).Groups[1].Value;
                var filterColumn = Filter.Match(text).Groups[1].Value;

                var table = new DataTable();
                if (fromTable == "StructureType")
                {
                    TypeSelects.Add(string.Join(",", columns));
                    foreach (var column in columns)
                        table.Columns.Add(column, Columns[column].Type);
                    foreach (var row in rows.Where(r => Columns[filterColumn].Value(r) is long v && ids.Contains(v)))
                        table.Rows.Add(columns.Select(c => Columns[c].Value(row) ?? DBNull.Value).ToArray());
                }
                else
                {
                    table.Columns.Add(Assert.Single(columns), typeof(long));
                    foreach (var typeId in References.Where(r => r.Table == fromTable && r.Column == filterColumn && ids.Contains(r.TypeId))
                                 .Select(r => r.TypeId).Distinct())
                        table.Rows.Add(typeId);
                }

                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }
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
