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

Each wake, pick the track (see SKILL.md Scope), then start at the track's category after `lastCategory` and search for an instance. Client wakes use 1-19; Docs wakes use 24. If a category has none, move to the next. A wake is empty only after every category comes up empty.

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
9. **Long methods and deep nesting.** Methods over about 60 lines, nesting deeper than three levels, or methods mixing I/O, geometry, and UI. Length alone is not a reason to split a method. Extract a piece only when it removes a copy (the same steps exist elsewhere, or another caller needs them) or when it makes the method easier to understand: the piece has a name that says what it does, a clear input and output, and can be tested on its own, ideally as a pure function. Guard clauses that remove nesting qualify. Do not cut a method into pieces that only run one after another and pass its state between them; that moves code without making it simpler. Take the worst entry in `complexity` first; when no extraction of either kind exists for it, add it to `rejected` with that reason and move on.
10. **Speculative generality and middlemen.** Interfaces with one implementation and no test or plugin use, abstract classes with one subclass, pass-through wrappers, and parameters every caller passes the same value for. Inline them. Check reflection, MEF/extension loading, XAML, and settings strings for the name before removing anything.
11. **Dead weight.** Unused members, unread settings, environment switches with no live path, stale or repeated documentation. Same reflection check as above.
12. **Resolvable debt notes and warnings.** `TODO`/`HACK`/`FIXME` comments whose condition no longer holds or that the loop can now resolve within the gates. Compiler warnings in hotspot files (counted in `warningBaseline`) count when fixing them states a real contract: a nullable annotation that matches what callers pass, an obsolete API replaced, an unused variable whose removal shows dead logic, an XML doc `<param>` that names the real parameter. Silencing a warning with `!`, `#pragma` or a suppression does not.
13. **Bugs likely in real use.** Crashes, wrong results (including math and precision errors), leaks, and concurrency defects that real data can reach. Search for the Bug patterns below first, then a build failure in `buildBaseline`, then anything else, including list-taking store and geometry APIs that fail on an empty, repeated, self-referencing or oversized input (Test coverage 5). See Bug fixes and Math and precision in [gates.md](gates.md).
14. **Untested code.** Production code in a hotspot or shared library with no test that would fail if it broke. Add tests only; see Test coverage in [gates.md](gates.md). A missing closed-set classification test or an untested documented guarantee (Test coverage 3 and 4) qualifies. Until one exists, a settings schema drift test is a standing candidate here: the shipped `Clients/Viking/WebAnnotation/UserSettings.xml` validates against `WebAnnotationUserSettings.xsd`, and every element, attribute and enumeration value in the XSD has its member in the generated `webannotationusersettings.cs`, failing on any difference not on a named, commented allow-list. It is test-only; a drift it finds is a proposal, because the generated class and settings persistence are protected.
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

Docs:

24. **Docs and consistency.** The checks in [docs.md](docs.md): docs that no longer match scripts, compose files, or settings; duplicate instructions; missing required docs; stale root-level summary files (propose).

## Bug patterns

Defect shapes this loop and its server counterpart have fixed. Category 13 searches for these first, and each bug fix adds its pattern here if it is new (Bug fixes step 6 in [gates.md](gates.md)). Each line gives the pattern, what to search for, and an example fix; a sha marked (Viking) is on `dev` in `d:\src\git\Viking`, the rest are on `Legacy`.

- **A second action queued after the first ran.** Code calls the server directly, then also marks the object for the next batched save, which repeats the action or undoes a later one. Search: a direct WCF call followed by a base `Remove`/`Update` or a `DBAction` set on the same object in the stores (`Clients/WebAnnotationModel/Store`). Fix: update local state without queueing (`b184370c`, Viking, in the gRPC client; the WCF `LocationLinkStore.DeleteLink` already does this).
- **Identity keyed by ID while objects can be replaced.** Subscriptions, caches or views keyed by ID go stale when a store evicts and reloads an object under the same ID. Search: `Dictionary<long, ...>` of subscriptions or views beside a store that can evict. Fix: key by object, and build from the store's current object (`4353c0b1`).
- **Save and restore over shared state.** An effect saves a value on enter and writes it back on leave, overwriting a change made in between. Search: fields named `old*`/`saved*`/`original*` restored in an exit, hover-end or remove path. Fix: restore only if the value is still the one the effect set (`e60fe8be`).
- **A temporary switch never switched back.** An operation sets shared view state (the viewed section, scene, overlays) per item and captures the old value but never writes it back, or writes it back per item while other items are still running. Search: `original*` locals never read again, and per-item `this.Section =`/`this.Scene =` in export or render loops. Fix: capture once before the loop and restore in a `finally` after every item has drained (`TaskChaining.RestoreAfterAsync`, the Export frames and Export Tiles section restore).
- **A failed item dropped from a throttle.** A loop keeps N tasks in flight with `list.Remove(await Task.WhenAny(list))` and never looks at the finished task, so a faulted or canceled item vanishes and whatever follows (an index file, a "done" message) claims it succeeded; a discarded `_ = Task.Run(...)` or `async` call then hides the rest. Search: `Remove(await Task.WhenAny` and `WhenAny` loops whose result is not awaited or read, and `_ =` in front of export or batch work. Fix: `TaskChaining.DrainToAsync` for the throttle and the final drain, and an awaited caller that reports the error (Export Tiles, `ExportImage`).
- **An exception escaping a path that must not throw.** Display, preview, input-handler and finalizer paths call conversions or validators that throw on real geometry: an unguarded `ToPolygon`, `AddInteriorRing` or `new Polygon` on a SQL boolean's pieces or a smoothed ring. Search: those calls and parsing inside draw, preview, pen or mouse handlers outside a `try`, and doc comments that say "never throws". Fix: a `Try*` wrapper with a fallback (keep the unsmoothed ring, skip the piece), plus the Test coverage 4 test (`fd225dcc`, `a40a6551`).
- **A seed in different units from its comparisons.** A best-so-far value starts as a linear distance but is compared with squared distances (or the reverse), so the seed wins wrongly and also sizes the search box. Search: `Distance`/`DistanceSquared` mixed in one nearest-search loop, and an R-tree query half-width taken from a squared value. Fix: one unit throughout, `sqrt` only for the box and the returned value (`85cbf7cc`).
- **A winner that is never measured.** A search seeds its winner with one candidate but its best value from something else, then skips the current winner in the loop, so the returned value belongs to the seed, not the winner. Search: nearest-search loops with `if (i == best) continue` or an initial winner whose distance is not computed. Fix: measure every candidate, the seed included; strict comparisons keep the loop finite (`d7946808`).
- **An operator whose name hides a different operation.** An overloaded operator does something other than its symbol or doc suggests (`Rectangle + double` bound to `Scale`, not padding), so callers get the wrong result silently. Search: `operator +`/`-`/`*` overloads on geometry types whose body calls a differently named method, and their callers with a scalar operand. Fix: call the named method (`Pad`), and delete the misleading operator once a probe build with it marked `[Obsolete(error)]` finds no other caller (`651fa197`).
- **Classification lists that miss members.** A hand-kept list or `switch` over enum values, command or action types, annotation types or hotkeys silently omits new ones. Search: `HashSet`/arrays of enum or type names and `switch` statements with a `default` that swallows. Fix: the Test coverage 3 test (`3137cfcb`, Viking).
- **API that a target framework lacks.** Code in a multi-targeted project (`Geometry.Core`, `Common`, `VolumeModel`, the test projects) uses an API only the newer target has, such as `MinBy` or index-from-end. Search: the build baseline for each target framework. Fix: a version both targets have (`1a5e7dbb`, Viking).
- **Tests with lasting side effects.** A test changes state nothing undoes: user settings, the Windows Credential Manager, the registry (protocol registration), files outside a per-test temp folder, or a live annotation server. Search: `CredWrite`, `Registry`, `Settings.Default.Save`, `Environment.SpecialFolder`, and server URLs in test projects. Fix: a temp folder or fake per test, and a guard that refuses a real target (`4fa04385`, Viking, the server-side form). Bug fixes in [gates.md](gates.md) still skips defects in test-only code, so this counts only when the effect reaches real state outside the test run.

## Proposals

A proposal is a short plan for work the loop may not or cannot do: protected-area changes, performance work with no feasible benchmark, and deferred work too large for one wake. Each entry has: title, area, path(s), problem, evidence, proposed change, risk, how to test or benchmark it, and estimated size. Add proposals to the ledger `proposals` list. Present new proposals in the next report; the final report lists them all. Do not open PRs or issues for them.
