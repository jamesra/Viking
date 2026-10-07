---
name: viking-client-improve-loop
description: >-
  Runs a timed improvement loop over the Viking client and MonogameTestbed:
  one refactoring, bug-fix, test, or performance change per wake, gated by
  tests and benchmarks, committed when green, published to Viking Test after
  every six Viking commits, with dated review reports. Deep links, auth, and
  on-disk formats are proposal-only. Use when the user launches this skill or
  a wake payload says to follow viking-client-improve-loop.
disable-model-invocation: true
---

# Viking client improvement loop

When this skill is launched, start the loop. Do not start it because this file was opened for another task. If `AGENT_LOOP_WAKE_viking_client_improve` is already sleeping in this session, do not start a second copy.

Files in this skill (each subagent reads only its own; see Each wake):

- [protected.md](protected.md): areas the loop never edits.
- [categories.md](categories.md): hotspots, the 19 categories, proposals.
- [gates.md](gates.md): what a change must pass; tests, benchmarks, commit, publish.
- [reports.md](reports.md): report format and learning from reverts.

## Arguments

`/viking-client-improve-loop [X] [Y]`

- X is the run length; default 24 hours. Y is the sleep after each wake; default 10 minutes.
- Accept `30m`, `4h`, `1d`, or a bare number of minutes.
- Deadline = launch time + X. Use America/Los_Angeles in messages to the user.

## Scope

Edit `VikingClient` on branch `Legacy` only: `Clients/Viking`, `Clients/MonogameTestbed`, shared client libraries those two compile, and `Clients/VikingBenchmarks` once it exists. Confirm the branch before the first edit. Do not edit generated code, comment code out to compile, push, or start server work. Do not mirror into `VikingServers`; append each commit sha to `mirrorPending`.

## Sync from the remote

At the start of every wake, before the scout runs, the main session brings both checkouts up to date with git; no subagent is needed.

- `d:\src\git\VikingSlackUsersAI\VikingClient` (`Legacy`) and `d:\src\git\VikingSlackUsersAI\VikingServers` (`dev`): `git fetch origin`, then `git merge --ff-only @{u}`. VikingServers is reference only; the loop never edits it.
- Fast-forward only. If a repo cannot fast-forward (diverged history, or uncommitted local edits the incoming commits touch), leave it as it is, work from the current tree, and record `{repo, reason, date}` in `syncSkips`. Never stash, reset, rebase, force, or discard anything to make a pull succeed, and never push.
- If the pull touches the `candidate`'s files, the scout re-checks the candidate before the implementer runs. If it breaks a build or a green test, do not fix it as part of the sync; it shows up at the next baseline run.

## Time box

If a wake passes about 60 minutes of work without a candidate clearing the gates, revert, add it to `deferred`, and end the wake.

## Launch

1. Read the `loop` skill once. Use its dynamic local schedule with sentinel `AGENT_LOOP_WAKE_viking_client_improve`. Never arm a fixed repeating sleeper.
2. Create the reports folder, `.loop\ledger.json`, and `STEERING.md` if missing. Run Sync from the remote. Build `hotspots`, run the test baseline, then run one wake immediately.
3. Arm the next wake with:

    {"prompt":"Follow skill viking-client-improve-loop.","deadline":"<ISO-8601>","intervalMinutes":<Y>}

## Each wake

The main session only schedules, relays, and talks to the user. All work runs in fresh `generalPurpose` subagents (models per Model routing), in the foreground, one at a time, so the main session's context does not grow. Give each subagent only the files named here and the ledger path.

1. Read the payload. Do not re-read the `loop` skill. Record any decision answers the user gave in chat into the ledger. Run Sync from the remote and note it in `lastSync` and `syncSkips`.
2. **Scout** (reads SKILL.md, protected.md, categories.md, the ledger, `STEERING.md`; reports.md and gates.md Test baseline only when a report or baseline is due). Prompt: "Run the scout step of skill viking-client-improve-loop. Skill folder: <path>. Ledger: <path>." In order, the scout:
   1. reads `STEERING.md` and the ledger;
   2. stops on `stop`, the deadline, or `emptyWakes` reaching 3, writing the `final` report;
   3. when 24 hours have passed since `lastReportAt` (or `startedAt`), writes the `daily` report, rebuilds `hotspots`, re-runs the test baseline, and checks for reworked commits;
   4. on `pause`, ends the wake;
   5. otherwise picks one candidate and writes it to `candidate`: category, files, a one-paragraph plan, and a risk level (`low` or `high`, per Model routing). It edits no production code. A protected or unbenchmarkable candidate becomes a proposal or decision, and the wake ends.
   It returns at most four lines: the candidate and its risk or why there is none; new decisions; report path if written; whether to stop.
3. **Implementer** (reads SKILL.md Scope and Time box, protected.md, gates.md, the ledger). Prompt: "Run the implement step of skill viking-client-improve-loop for the candidate in the ledger. Skill folder: <path>. Ledger: <path>." It runs the candidate through the gates, tests, mutation check, and benchmark when needed; commits if green; publishes if due; clears `candidate`; and writes the ledger. If it finds the candidate riskier than the scout said, it stops without committing and sets the risk to `high`; the next wake re-runs it with the high-risk model. It returns at most four lines: commit sha and summary or why nothing passed, new decisions, whether a publish ran.
4. **Reviewer, high risk only** (reads protected.md, gates.md). After a high-risk commit: "Review commit <sha> against the gates in <skill folder>. Report problems only." On a gate violation or likely defect, launch the implementer once more to fix it in a follow-up commit, or revert the commit when a fix is not clear. Record the outcome in the commit's ledger entry.
5. Post a one- or two-sentence summary, post any new decision as a question, then arm one Y-minute sleep, or arm nothing when the scout said stop.
6. When the user asks for a report, launch the scout model to write a `requested` report right away; the loop keeps running. When the user asks to stop, kill the tracked sleeper PID, launch the scout model to write the `final` report, and arm nothing.

## Steering

`d:\src\git\VikingSlackUsersAI\Automatic Improvement Reports\STEERING.md` is the user's; the loop reads it every wake and never edits it. Lines it understands:

- `focus: <path or category>`: search there first.
- `avoid: <path, pattern, or category>`: treat as protected for this run.
- `answer: <decision id> <choice>`: answers a pending decision.
- `pause`: skip work but keep arming wakes. `stop`: write the final report and stop.

Anything else is guidance the loop follows when it applies. Create the file with a short commented template at launch if it is missing.

## Decisions

When a candidate needs the user's call (two reasonable designs, a behavior change that might be wanted, a protected-area proposal that blocks other work), do not guess. Add it to `decisions` with an id, the question, and the options; show a Windows toast notification from PowerShell with the question's first line; and move to another candidate. The main session posts the question in chat. An answer in chat or as an `answer:` line in `STEERING.md` is recorded in the ledger, and a later wake acts on it. Open decisions appear in every report.

## Ledger

`d:\src\git\VikingSlackUsersAI\Automatic Improvement Reports\.loop\ledger.json`, shared by every fresh subagent. Read it at the start of each wake; write it before the wake ends.

```json
{
  "startedAt": "", "deadline": "", "intervalMinutes": 10,
  "hotspots": [], "baselineFailures": [], "flaky": [],
  "lastCategory": 0, "emptyWakes": 0,
  "candidate": { "category": 0, "files": [], "plan": "", "risk": "low|high" },
  "commits": [{ "program": "", "sha": "", "category": 0, "summary": "", "lineDelta": 0,
                "benchmark": "", "mutation": "", "risk": "", "models": {}, "review": "" }],
  "countsSincePublish": { "Viking": 0, "MonogameTestbed": 0, "Other": 0 }, "publishes": [],
  "rejected": [], "deferred": [], "proposals": [],
  "decisions": [{ "id": "", "question": "", "options": [], "askedAt": "", "answer": null }],
  "reworked": [], "mirrorPending": [],
  "lastSync": {}, "syncSkips": [], "modelSubstitutions": [],
  "lastReportAt": null, "reports": []
}
```

## Model routing

The user chose these models by writing them here. Pass them as the subagent's `model`. At launch, check each slug against the session's available subagent models. If one is missing, use the closest listed model of the same family and tier, or omit `model` to use the parent's, and record it in `modelSubstitutions`. Context window size cannot be set; keep each subagent's input small.

- **Scout, reports, ledger upkeep:** `composer-2.5`
- **Low-risk implementer:** `claude-sonnet-5-5-high`. Dead weight (11), resolvable debt notes (12), hand-rolled helpers (7), and test-only wakes (14), when the change stays inside one project and away from protected areas.
- **High-risk implementer:** `claude-opus-5-5-high`. Everything else, plus any low-risk category that touches a shared library, more than one program, a numeric type, or a file next to a protected area.
- **Reviewer:** `gpt-5.5-medium`. Only after high-risk commits, so the second opinion comes from a different model family.

The main session can run on Auto or any inexpensive model; it does no code work. Record the model used for each step in the commit's ledger entry.
