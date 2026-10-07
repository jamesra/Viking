# Where to look and what to look for

Read by the scout. Protected areas are in [protected.md](protected.md); the rules a change must pass are in [gates.md](gates.md).

## Where to look first

Prefer code that is both changed often and complex, not code that only looks untidy. At launch and with each daily report, rebuild `hotspots` in the ledger:

- **Churn:** `git log --since="6 months ago" --name-only --format= -- Clients`, counted per file, restricted to Scope. Skip generated code and `bin`/`obj`.
- **Size and complexity:** line count and deepest nesting of those files.
- **Cheap signals:** compiler warnings from the last build, and `TODO`/`HACK`/`FIXME`/"temporary" comments.

Rank by churn times size. Within each category, search hotspots before the rest of the scope.

## What to look for

Each wake, start at the category after `lastCategory` and search for an instance. If a category has none, move to the next. A wake is empty only after every category comes up empty.

Refactoring:

1. **Near-duplicates.** Code that differs only by a type, constant, field, or delegate. Merge into one generic method, parameterized helper, or `Func`/`Action`-taking method.
2. **Copies across programs.** The same logic in Viking and MonogameTestbed (or Jotunn). Move it into the shared library both reference and delete the copies.
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
9. **Long methods and deep nesting.** Methods over about 60 lines, nesting deeper than three levels, or methods mixing I/O, geometry, and UI. Use guard clauses and extract the reusable piece into a pure, tested function.
10. **Speculative generality and middlemen.** Interfaces with one implementation and no test or plugin use, abstract classes with one subclass, pass-through wrappers, and parameters every caller passes the same value for. Inline them. Check reflection, MEF/extension loading, XAML, and settings strings for the name before removing anything.
11. **Dead weight.** Unused members, unread settings, environment switches with no live path, stale or repeated documentation. Same reflection check as above.
12. **Resolvable debt notes.** `TODO`/`HACK`/`FIXME` comments whose condition no longer holds or that the loop can now resolve within the gates.
13. **Bugs likely in real use.** Crashes, wrong results (including math and precision errors), leaks, and concurrency defects that real data can reach. See Bug fixes and Math and precision in [gates.md](gates.md).
14. **Untested code.** Production code in a hotspot or shared library with no test that would fail if it broke. Add tests only; see Test coverage in [gates.md](gates.md).
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

## Proposals

A proposal is a short plan for work the loop may not or cannot do: protected-area changes, performance work with no feasible benchmark, and deferred work too large for one wake. Each entry has: title, area, path(s), problem, evidence, proposed change, risk, how to test or benchmark it, and estimated size. Add proposals to the ledger `proposals` list. Present new proposals in the next report; the final report lists them all. Do not open PRs or issues for them.
