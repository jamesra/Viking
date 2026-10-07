# Reports and learning from your changes

Read by whichever subagent writes a report (the scout model), and by the main session when it relays one.

## Reports

Folder: `d:\src\git\VikingSlackUsersAI\Automatic Improvement Reports\` (create it if missing). It is outside every repo; never `git add` it and never write reports inside a repo.

File name: `YYYY-MM-DD_HHmm_<kind>.md` in America/Los_Angeles time, where `<kind>` is `final` (loop stopped), `daily` (24 hours since `lastReportAt`), or `requested` (user asked). Never overwrite an existing file; add `_2` if the name is taken.

When to write:

- **Final:** when the loop stops for any reason.
- **Daily:** on the first wake at least 24 hours after `lastReportAt` (or `startedAt` if no report yet).
- **Requested:** whenever the user asks, without waiting for the next wake.

An interim (`daily` or `requested`) report covers the period since `lastReportAt`. A `final` report covers the whole run. After writing, set `lastReportAt`, append the path to `reports`, and post the path and a two-line summary in chat.

Keep each report reviewable in a few minutes:

1. **Totals:** period covered, wakes, commits, total production line delta, total test line delta, Viking Test publishes (with versions), commit count per category, and any model substitutions.
2. **Commits, riskiest first:** every commit in the period, sorted by risk. Weigh shared-library or cross-program reach, latent-bug fixes, performance changes, closeness to protected areas, and size. One line per commit: short sha, category, program, summary, line delta, and the models that worked on it; performance commits also show the before/after headline number, and changed-test commits show surviving mutants. When a commit carries a notable risk, add one indented line saying what it is and what to check. Commits with no notable risk get no extra line.
3. **Open decisions:** each pending entry in `decisions` with its id and options.
4. **Reworked:** loop commits you reverted or rewrote, and the pattern now avoided.
5. **Proposals:** each proposal in full, protected areas first, then unbenchmarkable performance work, then deferred refactors.
6. **Follow-ups:** `mirrorPending` shas, whether a Viking Test publish is owed, `baselineFailures` and `flaky` tests, `syncSkips` (repos that could not fast-forward and why), categories that came up empty, and the current top five hotspots.

## Learning from your changes

With each daily report, check every loop commit (found by the `Improvement-Loop:` trailer) from the last 14 days. A commit that was reverted, or whose changed lines a non-loop commit has since rewritten, goes in `reworked`. Add its pattern (category, file, kind of change) to `rejected` so later wakes avoid repeating it, and flag it in the report.
