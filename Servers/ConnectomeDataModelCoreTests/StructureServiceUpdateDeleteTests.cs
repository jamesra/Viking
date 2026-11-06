using gRPCAnnotationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;
using static ConnectomeDataModelCoreTests.LocationServiceUpdateDeleteTests;
using EfStructure = Viking.DataModel.Annotation.Structure;
using ProtoStructure = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins <see cref="StructureService.Update"/> when one batch edits a structure and also deletes it, or deletes
    /// its parent. DeepDeleteStructure removes the structure and its child structures inside the batch's
    /// transaction, behind EF's back, so a structure the batch edited earlier must not be saved as an UPDATE of a
    /// row that is gone: SQL Server would report no rows affected and EF would fail and roll back the whole batch.
    /// The edit row keeps its success, as for a location edited and then deleted, and the delete stands.
    /// A create under a deleted structure, or an update that moves a structure under one, fails its own row instead
    /// of the batch's save.
    /// No database is contacted: interceptors stand in for the connection, the transaction, the queries, the
    /// procedure and SaveChanges.
    /// </summary>
    public class StructureServiceUpdateDeleteTests
    {
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task UpdatingThenDeletingAStructureCommitsTheDelete()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 0L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(1L, response.Results[0].Updated.Id);
            Assert.Empty(store.Saved);
            Assert.Empty(store.Rows);
            Assert.True(store.Committed);
        }

        /// <summary>The edit may name the deleted parent, as a client resending the whole structure does.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData(1L)]
        public async Task EditingAChildThenDeletingItsParentCommitsTheDelete(long? editedParentId)
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(2, parentId: 1);
            using var db = NewContext(store);
            Track(db, store, 2);

            var response = await NewService(db).Update(Request(Update(2, editedParentId), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 0L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Empty(store.Saved);
            Assert.Empty(store.Rows);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task DeletingThenUpdatingAStructureFailsTheUpdate()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            using var db = NewContext(store);

            var response = await NewService(db).Update(Request(Delete(1), Update(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 1L, false), (false, 0L, false) },
                response.Results.Select(r => (r.Success, r.DeletedId, r.Updated != null)).ToArray());
            Assert.Empty(store.Saved);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task UpdatingAlongsideAnUnrelatedDeleteSavesTheEdit()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(5);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1), Delete(5)), new TestCallContext());

            Assert.Equal(new[] { (true, 0L), (true, 5L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal("edited", response.Results[0].Updated.Label);
            Assert.Equal(new[] { 1L }, store.Saved);
            Assert.Equal(new[] { 1L }, store.Rows.Keys);
            Assert.True(store.Committed);
        }

        /// <summary>A batch with no delete cannot lose a row behind EF's back, so it costs no extra query.</summary>
        [Fact]
        public async Task UpdatingWithoutADeleteRunsOnlyTheLookup()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1), Update(8)), new TestCallContext());

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(new[] { 1L }, store.Saved);
            Assert.Equal(2, store.Queries);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// A create under a structure the batch deletes would fail the save on FK_Structure_Structure and roll back
        /// every row, so only that create fails. The order of the create and the delete does not matter.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreatingAChildOfADeletedStructureFailsOnlyTheCreate(bool deleteFirst)
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(5);
            using var db = NewContext(store);
            Track(db, store, 5);

            var request = deleteFirst
                ? Request(Delete(1), Create(parentId: 1), Create(parentId: null), Update(5))
                : Request(Create(parentId: 1), Delete(1), Create(parentId: null), Update(5));
            var response = await NewService(db).Update(request, new TestCallContext());

            var childRow = deleteFirst ? 1 : 0;
            Assert.Equal(Enumerable.Range(0, 4).Select(i => i != childRow).ToArray(),
                response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[childRow].Created);
            Assert.NotNull(response.Results[2].Created);
            Assert.Equal(new long?[] { null }, store.CreatedUnder);
            Assert.Equal(new[] { 5L }, store.Saved);
            Assert.Equal(new[] { 5L }, store.Rows.Keys);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreatingAChildOfAStructureWhoseParentIsDeletedFailsOnlyTheCreate()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(2, parentId: 1);
            using var db = NewContext(store);

            var response = await NewService(db).Update(Request(Delete(1), Create(parentId: 2)), new TestCallContext());

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Empty(store.CreatedUnder);
            Assert.Empty(store.Rows);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreatingAChildOfALiveStructureBesideAnUnrelatedDeleteSavesTheCreate()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(5);
            using var db = NewContext(store);

            var response = await NewService(db).Update(Request(Create(parentId: 1), Delete(5)), new TestCallContext());

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(1L, response.Results[0].Created.ParentId);
            Assert.Equal(new long?[] { 1 }, store.CreatedUnder);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// An update that moves a structure under one the batch deletes would fail the save on FK_Structure_Structure
        /// and roll back every row, so only that update fails. The order of the update and the delete does not matter.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MovingAStructureUnderADeletedStructureFailsOnlyTheUpdate(bool deleteFirst)
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(5);
            store.AddStructure(9);
            using var db = NewContext(store);
            Track(db, store, 5, 9);

            var request = deleteFirst
                ? Request(Delete(1), Update(5, parentId: 1), Create(parentId: null), Update(9))
                : Request(Update(5, parentId: 1), Delete(1), Create(parentId: null), Update(9));
            var response = await NewService(db).Update(request, new TestCallContext());

            var movedRow = deleteFirst ? 1 : 0;
            Assert.Equal(Enumerable.Range(0, 4).Select(i => i != movedRow).ToArray(),
                response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[movedRow].Updated);
            Assert.NotNull(response.Results[2].Created);
            Assert.Equal(new long?[] { null }, store.CreatedUnder);
            Assert.Equal(new[] { 9L }, store.Saved);
            Assert.Equal(new long?[] { null, null }, new[] { store.Rows[5], store.Rows[9] });
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task MovingAStructureUnderALiveStructureBesideAnUnrelatedDeleteSavesTheUpdate()
        {
            var store = new FakeStructureDatabase();
            store.AddStructure(1);
            store.AddStructure(5);
            store.AddStructure(9);
            using var db = NewContext(store);
            Track(db, store, 5);

            var response = await NewService(db).Update(Request(Update(5, parentId: 1), Delete(9)), new TestCallContext());

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(1L, response.Results[0].Updated.ParentId);
            Assert.Equal(new[] { 5L }, store.Saved);
            Assert.True(store.Committed);
        }

        private static StructureService NewService(AnnotationContext db) => new(db, NullLogger<StructureService>.Instance);

        private static StructureChangeRequest Delete(long id) => new() { Delete = id };

        private static StructureChangeRequest Create(long? parentId)
        {
            var structure = new ProtoStructure { TypeId = 7, Label = "new" };
            if (parentId.HasValue)
                structure.ParentId = parentId.Value;
            return new() { Create = structure };
        }

        private static StructureChangeRequest Update(long id, long? parentId = null)
        {
            var structure = new ProtoStructure { Id = id, TypeId = 7, Label = "edited" };
            if (parentId.HasValue)
                structure.ParentId = parentId.Value;
            return new() { Update = structure };
        }

        private static UpdateStructuresRequest Request(params StructureChangeRequest[] changes)
        {
            var request = new UpdateStructuresRequest();
            request.Objs.AddRange(changes);
            return request;
        }

        /// <summary>
        /// Tracks the stored structures as the service's by-ID query would, so the fake only has to answer that
        /// query with the key (EF returns the tracked instance without reading the other columns).
        /// </summary>
        private static void Track(AnnotationContext db, FakeStructureDatabase store, params long[] ids)
        {
            foreach (var id in ids)
                db.Structures.Attach(new EfStructure { Id = id, ParentId = store.Rows[id], TypeId = 1, Username = "someone" });
        }

        private static AnnotationContext NewContext(FakeStructureDatabase store) =>
            new(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(store)
                .Options);

        /// <summary>
        /// Stands in for the Structure table. Opening the connection is suppressed; commands and the transaction
        /// are fakes. A Structure query returns the key of each stored row whose ID is among its parameter values.
        /// DeepDeleteStructure removes the structure and its children, as the procedure does. SaveChanges is
        /// suppressed and records the structures it would update; an update or delete of one whose row is gone fails
        /// the save with <see cref="DbUpdateConcurrencyException"/>, as EF does when SQL Server reports no rows affected.
        /// An insert or update whose ParentID row is gone fails the save with <see cref="DbUpdateException"/>, as
        /// FK_Structure_Structure does; a saved update stores its ParentID.
        /// </summary>
        private sealed class FakeStructureDatabase : IDbConnectionInterceptor, IDbCommandInterceptor, IDbTransactionInterceptor, ISaveChangesInterceptor
        {
            /// <summary>Stored structures: ID to ParentID.</summary>
            public Dictionary<long, long?> Rows { get; } = new();

            /// <summary>IDs of the structures the batch's save would update.</summary>
            public long[] Saved { get; private set; } = Array.Empty<long>();

            /// <summary>ParentIDs of the structures the batch's save would insert.</summary>
            public long?[] CreatedUnder { get; private set; } = Array.Empty<long?>();

            /// <summary>Number of Structure queries the batch ran.</summary>
            public int Queries { get; private set; }

            public bool Committed { get; private set; }

            public void AddStructure(long id, long? parentId = null) => Rows[id] = parentId;

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public InterceptionResult<DbCommand> CommandCreating(CommandCorrelatedEventData eventData, InterceptionResult<DbCommand> result) =>
                InterceptionResult<DbCommand>.SuppressWithResult(new FakeCommand());

            public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<DbTransaction>.SuppressWithResult(new FakeTransaction(connection, () => Committed = true)));

            public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (!command.CommandText.Contains("[DeepDeleteStructure]", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Unexpected command: {command.CommandText}");

                var id = (long)command.Parameters["DeleteID"].Value;
                foreach (var child in Rows.Where(r => r.Value == id).Select(r => r.Key).ToArray())
                    Rows.Remove(child);
                Rows.Remove(id);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(-1));
            }

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                if (!command.CommandText.Contains("FROM [Structure]", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Unexpected query: {command.CommandText}");

                Queries++;
                var keys = command.Parameters.Cast<DbParameter>().SelectMany(p => KeyValues(p.Value)).ToHashSet();
                var answer = new DataTable();
                answer.Columns.Add("ID", typeof(long));
                foreach (var id in Rows.Keys.Where(keys.Contains))
                    answer.Rows.Add(id);
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(answer.CreateDataReader()));
            }

            public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var entries = eventData.Context.ChangeTracker.Entries<EfStructure>().ToList();
                // EF counts rows for an UPDATE and a DELETE alike, so marking a vanished row Deleted instead of
                // detaching it would fail the save the same way the stale UPDATE did.
                if (entries.Any(e => (e.State == EntityState.Modified || e.State == EntityState.Deleted) && !Rows.ContainsKey(e.Entity.Id)))
                    throw new DbUpdateConcurrencyException("The database operation was expected to affect 1 row(s), but actually affected 0 row(s).");

                var added = entries.Where(e => e.State == EntityState.Added).ToList();
                if (added.Any(e => e.Entity.ParentId.HasValue && !Rows.ContainsKey(e.Entity.ParentId.Value)))
                    throw new DbUpdateException("The INSERT statement conflicted with the FOREIGN KEY SAME TABLE constraint \"FK_Structure_Structure\".");
                CreatedUnder = added.Select(e => e.Entity.ParentId).ToArray();

                var updated = entries.Where(e => e.State == EntityState.Modified).ToList();
                if (updated.Any(e => e.Entity.ParentId.HasValue && !Rows.ContainsKey(e.Entity.ParentId.Value)))
                    throw new DbUpdateException("The UPDATE statement conflicted with the FOREIGN KEY SAME TABLE constraint \"FK_Structure_Structure\".");
                Saved = updated.Select(e => e.Entity.Id).OrderBy(id => id).ToArray();
                foreach (var e in updated)
                    Rows[e.Entity.Id] = e.Entity.ParentId;
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(updated.Count));
            }

            /// <summary>The ID values a parameter carries: one number, or a JSON array of them for a Contains list.</summary>
            private static IEnumerable<long> KeyValues(object value) => value switch
            {
                long l => new[] { l },
                string json when json.StartsWith('[') => JsonSerializer.Deserialize<long[]>(json),
                _ => Array.Empty<long>()
            };
        }
    }
}
