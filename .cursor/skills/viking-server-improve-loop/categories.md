# Where to look and what to look for

Read by the scout. Protected areas are in [protected.md](protected.md); the rules a change must pass are in [gates.md](gates.md).

## Where to look first

Prefer code that is both changed often and complex, not code that only looks untidy. At launch and with each daily report, rebuild `hotspots` in the ledger:

- **Churn:** `git log --since="6 months ago" --name-only --format=`, counted per file in the repo, restricted to the paths in [scope.md](scope.md). Skip generated code and `bin`/`obj`.
- **Size and complexity:** line count and deepest nesting of those files.
- **Cheap signals:** compiler warnings from the last build, and `TODO`/`HACK`/`FIXME`/"temporary" comments.

Rank by churn times size. Within each category, search hotspots before the rest of the scope.

With `hotspots`, rebuild `complexity`: for each of the top 20 hotspot files, every method over about 60 lines or nested deeper than three levels, as `{ file, method, lines, depth }`. The gate "Complexity does not grow" in [gates.md](gates.md) reads it, and category 9 works through it worst first.

## What to look for

Each wake, pick the track (see SKILL.md Scope), then start at the track's category after `lastCategory` and search for an instance. gRPC-client wakes use 1-19 plus 20-21; Server uses 1-19 and 22; SQL uses 23 and 13-14; Parity uses 20-21; Docs uses 24; Deploy uses 25. If a category has none, move to the next. A wake is empty only after every category comes up empty.

Refactoring:

1. **Near-duplicates.** Code that differs only by a type, constant, field, or delegate. Merge into one generic method, parameterized helper, or `Func`/`Action`-taking method.
2. **Copies across programs.** The same logic in two services, or in a service and the gRPC client. Move it into the shared library both reference and delete the copies.
3. **Data clumps and long parameter lists.** The same three or more parameters appear together in two or more signatures, a method takes five or more parameters, a tuple is passed or returned across several methods, or a group of fields is always set together. Introduce one type and pass it instead:
   - `readonly struct` or `readonly record struct` for small immutable value data, especially on hot paths;
   - `sealed record` for reference data;
   - a plain class only when the group must be mutable.
   Then move behavior that only uses those values onto the new type. On .NET 4.8, records and `init` need an internal `IsExternalInit` shim; copy `Clients/Viking/NGVV/IsExternalInit.cs` into the project if it lacks one.
4. **Primitive obsession and magic values.** The same literal in several places becomes a named constant. A raw `int`/`double`/`string` whose validation or conversion is repeated in two or more places becomes a small value type that owns that rule.
5. **Parallel branching and type-check chains.** Two or more `switch`/`if` chains over the same key, or `is`/cast ladders. Replace with one table, dictionary, polymorphic member, or one `switch` expression with patterns.
6. **Feature envy and message chains.** A method that mostly reads another class's data moves to that class. A repeated `a.B.C.D` chain becomes one member on the owner.
7. **Hand-rolled helpers.** Re-implementations of BCL, LINQ, MonoGame, or existing shared helpers. Call the existing one.
8. **Over-specific APIs.** A parameter typed to one concrete type when callers pass several. Widen to `IEnumerable<T>`, `IReadOnlyList<T>`, or an existing interface.
9. **Long methods and deep nesting.** Methods over about 60 lines, nesting deeper than three levels, or methods mixing I/O, geometry, and UI. Use guard clauses and extract the reusable piece into a pure, tested function. Take the worst entry in `complexity` first.
10. **Speculative generality and middlemen.** Interfaces with one implementation and no test or plugin use, abstract classes with one subclass, pass-through wrappers, and parameters every caller passes the same value for. Inline them. Check reflection, MEF/extension loading, XAML, and settings strings for the name before removing anything.
11. **Dead weight.** Unused members, unread settings, environment switches with no live path, stale or repeated documentation. Same reflection check as above.
12. **Resolvable debt notes and warnings.** `TODO`/`HACK`/`FIXME` comments whose condition no longer holds or that the loop can now resolve within the gates. Compiler warnings in hotspot files count when fixing them states a real contract (a nullable annotation that matches what callers pass, an obsolete API replaced, an unused variable whose removal shows dead logic); silencing a warning with `!`, `#pragma` or a suppression does not.
13. **Bugs likely in real use.** Crashes, wrong results (including math and precision errors), leaks, and concurrency defects that real data can reach. Search for the Bug patterns below first, then a build failure in `buildBaseline`, then anything else. See Bug fixes and Math and precision in [gates.md](gates.md).
14. **Untested code.** Production code in a hotspot or shared library with no test that would fail if it broke. Add tests only; see Test coverage in [gates.md](gates.md). A missing closed-set classification test or an untested documented guarantee (Test coverage 3 and 4) qualifies.
15. **Smoothing a .NET 10 port.** Make .NET Framework 4.8 code easier to port later, without changing behavior on 4.8:
    - replace APIs that are missing or obsolete on modern .NET (`WebClient`, `Thread.Abort`, `AppDomain` creation, Remoting, `ServicePointManager`) with APIs available on both;
    - move platform-neutral code out of net48-only projects into libraries that already multi-target or target `netstandard2.0`;
    - keep `#if` forks to one small boundary instead of scattering them;
    - do not add new net48-only dependencies.
    Add a `net10.0` target only to a library that already builds for `net9.0` or `netstandard2.0` and has no Windows-only or MonoGame dependency, and verify it with `dotnet build -f net10.0`. WCF and token handling, MonoGame 3.7, and settings persistence block a full port; record findings there as proposals.

Performance (hot paths first: per-frame rendering, per-annotation or per-vertex loops, tile and geometry math, mesh generation, network result processing). Every performance change needs the benchmark in [gates.md](gates.md):

16. **Allocation.** Allocations in loops or per frame: LINQ in hot loops, boxing, closures, `params` arrays, string concatenation, temporary lists, `ToArray`/`ToList` that are read once. Reuse buffers, use pooling (`ArrayPool<T>`), pre-size collections, and use `StringBuilder`.
17. **Value types.** Small immutable types that should be `readonly struct`. Large structs passed by value that should be `in` or `ref readonly`. Mutable structs that cause defensive copies. Classes with short lifetimes that are allocated in bulk. Overly precise types for the data they hold (see Math and precision in [gates.md](gates.md)) belong here when the fix is about memory or speed.
18. **Loops and data access.** Repeated work inside loops, repeated dictionary lookups (`TryGetValue` instead of `ContainsKey` + indexer), multiple enumeration of `IEnumerable`, `foreach` over interfaces in hot paths, and bounds checks that can be hoisted.
19. **Algorithm and data structure.** Quadratic scans that should be a hash set, sort, or spatial index (the existing R-tree), linear searches over sorted data, repeated recomputation that should be cached, and lock contention or blocking waits on async paths. Any other change that measurably reduces time or memory on a real path qualifies here.

Server, SQL, parity, deploy, and docs. Categories 1-19 apply to server and gRPC-client C# too; these add track-specific work:

20. **Parity gap.** A `missing` or `partial` row in `PARITY.md` ([parity.md](parity.md)): implement it in the gRPC client or service with a parity test. Build the matrix first if it does not exist.
21. **Parity performance.** A benchmark row over the line in [parity.md](parity.md): batching, streaming, message size, channel reuse, serialization, server query cost. Needs the paired benchmark.
22. **Server defects.** Missing cancellation or deadline handling, unbounded result sets without paging or streaming, per-call `DbContext` or channel creation, N+1 queries, unawaited tasks, swallowed exceptions, unauthenticated or over-permissive endpoints (the last is a proposal), and error codes that differ from what the clients expect. Batch RPCs that fail on an empty, repeated, self-referencing or oversized input (see Batch and boundary inputs in [gates.md](gates.md)).
23. **SQL.** Missing or unused indexes on hot queries (check the query plan on realistic data), non-sargable predicates, scalar functions in `WHERE`, cursors or row-by-row loops that should be set-based, missing `SET NOCOUNT ON`, implicit conversions, procedures without header comments, and schema drift between `AnnotationDatabase.sqlproj`, `minimal-schema.sql`, and the EF model. Until one exists, a schema drift test is a standing candidate here: it lists the tables, columns, procedures and triggers in each of the three, and fails on any difference not on a named, commented allow-list. Every change is verified on the throwaway database; see SQL in [gates.md](gates.md).
24. **Docs and consistency.** The checks in [docs.md](docs.md): docs that no longer match scripts, compose files, or settings; duplicate instructions; missing required docs; stale root-level summary files (propose).
25. **Deploy.** The next unbuilt or broken step of the per-database deploy target in [deploy.md](deploy.md), or a mismatch between the script, compose files, and READMEs.

## Bug patterns

Defect shapes this loop has fixed. Category 13 searches for these first, and each bug fix adds its pattern here if it is new (Bug fixes step 5 in [gates.md](gates.md)). Each line gives the pattern, what to search for, and an example fix.

- **Pending changes invisible to an existence check.** A check queries only the database while the same unit of work has added or deleted that row, so a second add makes a duplicate key or identity conflict. Search: `AnyAsync`/`Where(...).Any` before `Add` inside a loop over a batch. Fix: `FindAsync` by key, or a set of keys seen in the batch (`516c5880`).
- **Unvalidated batch input.** Duplicates, self-references, or one bad row reach a constraint and fail the whole batch with the wrong status code. Search: RPCs and procedures taking repeated fields or table-valued input. Fix: validate up front with `InvalidArgument`, dedupe by the contract's rule (`f01c49b3`).
- **A second action queued after the first ran.** Code calls the server directly, then also marks the object for the next batched save, which repeats the action or undoes a later one. Search: a direct RPC followed by a base `Remove`/`Update` that sets a `DBAction`. Fix: update local state without queueing (`b184370c`).
- **Identity keyed by ID while objects can be replaced.** Subscriptions, caches or views keyed by ID go stale when a store evicts and reloads an object under the same ID. Search: `Dictionary<long, ...>` of subscriptions or views beside a store that can evict. Fix: key by object, and build from the store's current object (`3401d0f3`).
- **Save and restore over shared state.** An effect saves a value on enter and writes it back on leave, overwriting a change made in between. Search: fields named `old*`/`saved*`/`original*` restored in an exit or remove path. Fix: restore only if the value is still the one the effect set (`ee4da571`).
- **An exception escaping a path that must not throw.** Display, preview, input-handler and finalizer paths call conversions or validators that throw on real geometry. Search: `ToPolygon`, `AddInteriorRing`, `new Polygon` and parsing inside draw, preview or mouse handlers, and doc comments that say "never throws". Fix: a `Try*` wrapper with a fallback, plus the Test coverage 4 test (`449c4eed`, `2a7e5279`).
- **NULL propagation in SQL string building.** `col + N'...'` yields NULL when `col` is NULL. Search: `+` on nullable `varchar`/`nvarchar` columns in procedures. Fix: `ISNULL(col, N'')` or `CONCAT` (`697a6185`).
- **Classification lists that miss members.** A hand-kept list of RPCs, enum values or commands silently omits new ones (a retry list without a read RPC, or with a write). Search: `HashSet`/arrays of method or enum names. Fix: the Test coverage 3 test (`3137cfcb`).
- **API that a target framework lacks.** Code in a multi-targeted project uses an API only the newer target has. Search: the build baseline for each target framework. Fix: a version both targets have (`1a5e7dbb`).
- **Tests with lasting side effects.** A test script changes state its rollback cannot undo (identity seeds, sequences, files) or can run against a real database. Search: `IDENTITY_INSERT`, `DBCC`, `CREATE DATABASE` in test scripts. Fix: a guard on `DB_NAME()` and a note of what persists (`4fa04385`, `8ffac752`).

## Proposals

A proposal is a short plan for work the loop may not or cannot do: protected-area changes, performance work with no feasible benchmark, and deferred work too large for one wake. Each entry has: title, area, path(s), problem, evidence, proposed change, risk, how to test or benchmark it, and estimated size. Add proposals to the ledger `proposals` list. Present new proposals in the next report; the final report lists them all. Do not open PRs or issues for them.
