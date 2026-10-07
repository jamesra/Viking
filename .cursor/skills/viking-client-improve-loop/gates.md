# Gates: what a change must pass

Read by the implementer and the reviewer. Protected areas are in [protected.md](protected.md).

## Accept a change only if all hold

- **Not protected.** The change edits nothing in Protected areas.
- **Real callers.** Every generalization serves two or more existing call sites. No code for hypothetical callers.
- **Net simpler.** For refactoring, production code gets shorter, or a rule that lived in several places now lives in one. Performance changes may add a little code if the benchmark gain is real and the code stays readable.
- **No new flags.** Do not merge methods with a `bool` or mode parameter that picks between old bodies.
- **Structural, not cosmetic.** Renames, formatting, `var` or style changes, attribute-only edits, and `using` sorting do not count as a wake's change on their own. They may ride along with a structural change in the same lines.
- **Semantics traps checked.** Records gain value equality (check dictionary keys, `HashSet`, `==`, reference identity). A class becoming a struct changes copy and null behavior. `in` on a non-readonly struct adds defensive copies. LINQ is lazy where a loop was eager. Async changes timing and thread. Check each caller for the trap that applies.
- **Same behavior.** A test pins the old behavior before the edit and passes after it. Bug fixes are the one exception; they follow Bug fixes.
- **Reviewable size.** About 300 changed lines at most. Larger work goes to `deferred` with a one-line plan; do a smaller slice now if one exists.
- **Builds everywhere it is used.** Shared-library edits build Viking (.NET Framework 4.8) and MonogameTestbed (.NET 9). `Span<T>` and `ArrayPool<T>` on .NET 4.8 need `System.Memory`/`System.Buffers`; do not add a package only to save a few allocations.
- **Comments.** Keep comments that state a contract, threading rule, or edge case. Edited types and members keep or gain `///` docs.

If no candidate passes, revert and add `{ path, category, reason }` to `rejected`. Skip rejected paths later unless the code around them changed.

## Bug fixes

The loop may fix bugs that are likely to occur in real use, without asking first. A bug qualifies when ordinary use, real volume data, or real server responses can reach it:

- crashes or unhandled exceptions: null data from the server, empty collections, missing sections or tiles, `nan` transforms;
- wrong results: off-by-one errors in section or index math, wrong coordinate space, unit or scale mix-ups, culture-sensitive number parsing or formatting, exact `float`/`double` equality, and the math and precision errors below;
- resource leaks: undisposed graphics resources, GDI objects, or streams, and event subscriptions never removed;
- concurrency defects: `async void` outside event handlers, `.Result`/`.Wait()` on the UI thread, races on shared state, swallowed exceptions that hide failures.

Bugs that need contrived input, or that sit only in dead or test-only code, are not worth a wake; log them in `rejected`.

How to fix one:

1. Reproduce it first with a test that fails on the current code. Prefer a property test that generates the inputs that trigger it. Commit that test only if it passes after the fix; never commit a failing test.
2. Make the smallest fix that turns the test green without breaking other tests. The rest of the gates still apply.
3. Fix only the defect. If the fix would change user-visible behavior beyond removing the defect (UI flow, defaults, what gets saved), add it to `decisions` instead.
4. For a bug a Viking user could notice, add one line to `VikingChangelog.md` in the same commit, so the next Viking Test announcement lists it.
5. If the bug is in a protected area, or cannot be reproduced outside the UI or a live server, write a proposal with the evidence and the likely fix.

## Math and precision

This is scientific code: annotation geometry, section transforms, and measurements feed research results. A math or precision error counts as a bug likely in real use whenever real data can reach it. Check types against the data they hold: volume coordinates reach the hundreds of thousands of pixels, section numbers run into the thousands, and measurements are later converted to physical units.

Math errors to look for:

- integer division or `int` overflow where a real or 64-bit result is meant (areas, pixel counts, products of tile dimensions);
- truncation where `Math.Floor` is meant, `%` on negative coordinates, and banker's rounding where away-from-zero is meant;
- degrees versus radians, and swapped `x`/`y` or row/column order;
- wrong coordinate space (mosaic, section, volume, screen) or a missing unit conversion (pixels to nm or µm);
- catastrophic cancellation: subtracting nearly equal large numbers in orientation, area, intersection, or distance tests;
- normalizing a zero-length vector, inverting a singular or near-singular transform, and `NaN` or infinity spreading silently;
- long sums that drift (use compensated summation or sum in `double`), and squared distances that overflow `float`;
- precision round trips: a value narrowed (`double` to `float`, `long` to `int`) to fit one parameter or field, then widened again by the caller or the next step because the rest of the pathway uses the higher precision. The precision is lost for nothing, and every conversion costs time, plus an array copy for buffers. Match the pathway's precision instead: widen the parameter rather than narrowing the data. Being overly precise at one step is better than a lossy round trip. Narrow once, at the true boundary where the lower precision is consumed (GPU upload, file write, display), and check or clamp there when the value can exceed the target's range. This includes implicit narrowing through `Vector2`/`Vector3`, mixed `float`/`double` arithmetic, and floating point to integer or signed to unsigned conversions.

Precision of types:

- **Under-precise:** `float` holding volume-space coordinates, accumulated transforms, or long sums, where its roughly 7 significant digits lose sub-pixel accuracy at real magnitudes. Use `double` for geometry and measurement. Rendering may keep `float` for MonoGame and GPU vertex data; subtract a nearby origin (camera or section offset) in `double` before converting, rather than widening the vertex type.
- **Overly precise:** `double` or `decimal` for data that is inherently coarse, such as pixel intensities, colors, tile indices, or GPU-bound vertices. These waste memory and bandwidth and imply accuracy the data does not have. Narrow them only with a benchmark and a test showing results stay within the data's real resolution. Narrowing must not add conversions at the boundaries: if neighbors in the pathway would cast back up, leave the type alone.
- **Cost of changing precision:** count the conversions the change adds or removes along the whole pathway, not just at the edited member, including CPU cost, array copies, and precision lost. Prefer the option with fewer conversions. When precision and speed conflict, keep precision unless a benchmark on real data shows the gain matters and a test shows the loss stays within the data's resolution.
- **Comparisons:** a fixed absolute epsilon is wrong across magnitudes. Use a relative or ULP-based tolerance sized to the data, and name the constant with a comment on where the value comes from.

Changing a numeric type in a serialized, persisted, or wire type is a format change: write a proposal.

## Test coverage

Every change is covered by a test that would fail if the change were wrong.

1. **Property tests with FsCheck**, the default. FsCheck is already referenced by `MonogameTestbed`, `GeometryTests`, and `MorphologyMeshTest`; add the package to another test project when it needs one. Good properties: round trips (serialize then parse, transform then inverse), invariants (bounds contain every point, area is never negative), old-versus-new equivalence for refactors on generated inputs, and agreement with a slow, obviously correct reference implementation. Write custom generators for domain types (polygons, transforms, section ranges) and keep them in the test project for reuse. For numerical code:
   - generate values across the real magnitude range, not only small numbers;
   - include near-degenerate cases (collinear points, tiny or huge polygons, near-singular transforms) and `NaN` or infinity where the input can contain them;
   - assert with tolerances derived from the data's precision;
   - where it helps, compare against a higher-precision reference: `double` for `float` code, or `decimal` or exact rational arithmetic for `double` code.
2. **Example tests** when a property would only restate the code, or for one known regression input. Keep each regression input as a named example next to the property that generalizes it.

For category 14 (test-only wakes), pick untested code from hotspots and shared libraries first. A test-only commit counts toward the six-commit publish only if it would have caught a real defect. Every new or changed test also goes through the Mutation check.

## Test baseline

At launch and with each daily report, run every client test project in scope once and record failing tests in `baselineFailures`. Re-run failures once; a test that passes on the re-run goes in `flaky`. "Green" means no failure outside `baselineFailures` and `flaky`. Do not fix baseline failures unless one is the wake's chosen candidate.

## Mutation check

When a wake adds or changes tests, run Stryker.NET (`dotnet stryker`, installed once as a global tool) from the covering test project, scoped to the changed production file with `--mutate`. Mutants on changed lines that survive must be killed by a stronger test, or listed in the commit message as equivalent with one line of reasoning. Run it only on a tree with no other uncommitted changes in that project. If the run passes about 15 minutes, stop it and record `mutation: not run (time)`.

## Benchmarking performance changes

Every performance change needs before and after numbers. Without them, do not commit it.

1. Use BenchmarkDotNet in `Clients/VikingBenchmarks` (create it on first need: console project multi-targeting `net48` and `net9.0`, referencing the projects under test; add it to the solutions that own those projects). Run on the runtime the production caller uses: `net48` for Viking code, `net9.0` for MonogameTestbed-only code, both for shared code. Release builds, `[MemoryDiagnoser]`, the old code as the baseline method in the same run; setup in `[GlobalSetup]`; no hand-written loops in benchmark bodies. If `[IterationSetup]` is unavoidable, each invocation must run at least 100 ms.
2. Use realistic inputs: real fixtures such as test resources, `MorphologyMeshTest/DifficultCases`, captured annotation or volume data, and sizes seen in real volumes (thousands of locations, full-section tile sets, real contour vertex counts). Do not use tiny synthetic inputs that make a change look better than it is in practice.
3. Run with `--statisticalTest 5%`. Accept only if the result is `Faster` and the time improves by at least 5%, or allocated bytes drop by at least 20%, with no regression on the other metric. Re-run a 5-10% result with `--iterationCount 30`; if it stays marginal, do not commit.
4. Note in the ledger whether a build, test run, or other agent was loading the machine during the run. A result taken under load gets one re-run before acceptance.
5. Keep each accepted benchmark in `Clients/VikingBenchmarks` as a regression check. Put the before/after summary in the commit message and in the ledger.

If a realistic benchmark needs a live server, a GPU frame loop, UI interaction, or more setup than fits one wake, do not edit. Write a proposal (see [categories.md](categories.md)) with the suspected cost, the evidence, and the benchmark that would prove it.

## Before commit and commit

Read the full diff once as a reviewer: callers still compile and behave the same, no comment lost, nothing outside scope or in Protected areas changed. Build the touched solutions and run the covering test projects, the mutation check, and for performance changes, the benchmark.

Commit only when green. Stage only the files this wake changed, by path; never `git add -A` or `git add .`. If a file the candidate needs already has uncommitted edits the loop did not make, choose a different candidate so those edits never land in a loop commit. A new characterization test may be its own commit just before the change it protects; the pair counts as one change. The commit message states what improved, the category, and the line delta (and the benchmark summary for performance changes), and ends with the trailer `Improvement-Loop: <category number>` so later wakes can find loop commits. On failure, revert and log it; the wake does not count toward publish.

## Publish

Count only commits this loop creates, per program.

- **Viking:** on the 6th commit, follow skill `deploy-viking-test`, then set the Viking count to 0. Publish finishes before the next sleep is armed.
- **Other programs:** no test-feed skill exists. On the 6th commit, tell the user once and reset that count.
