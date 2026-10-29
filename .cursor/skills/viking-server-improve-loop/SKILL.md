---
name: viking-server-improve-loop
description: >-
  Runs a timed improvement loop over the Viking gRPC client, the gRPC
  servers, and the annotation SQL database in d:\src\git\Viking (branch
  dev): one refactoring, bug-fix, test, performance, parity, deploy, or docs
  change per wake, gated by tests and benchmarks, committed when green, with
  dated review reports. Drives the WCF-to-gRPC migration to feature and
  performance parity. Auth, wire formats, real databases, and production data
  are proposal-only. Use when the user launches this skill or a wake payload
  says to follow viking-server-improve-loop.
disable-model-invocation: true
---

# Viking server improvement loop

The server half of the former `viking-improve-loop`. The WCF client and the Viking Test publish moved to `viking-client-improve-loop` in `d:\src\git\VikingLegacy`; the two loops run independently, each with its own ledger and steering file. When this skill is launched by the user, start the loop. Do not start it because this file was opened for another task. If the ledger's `nextWakeAt` is in the future and a wake is already scheduled in this session, do not start a second copy.

The loop covers three things: the **gRPC client** (migration target), the **Server** (gRPC services), and **SQL** (annotation database). Three cross-cutting goals run beside them: **parity** (gRPC client matches the WCF client in features and speed), **deploy** (one command deploys the gRPC server per annotation database), and **docs** (consistent code docs and solid, short user docs).

Files in this skill (each subagent reads only its own; see Each wake):

- [scope.md](scope.md): tracks, repos and branches, builds, safety rules.
- [protected.md](protected.md): areas the loop never edits.
- [categories.md](categories.md): hotspots, the categories, proposals.
- [gates.md](gates.md): what a change must pass; tests, benchmarks, commit.
- [parity.md](parity.md): parity matrix, parity tests, paired benchmarks.
- [deploy.md](deploy.md): per-database gRPC server deploy target.
- [docs.md](docs.md): code and user documentation standards.
- [reports.md](reports.md): report format and learning from reverts.

## Arguments

`/viking-server-improve-loop [X] [Y]`

- X is the run length; default 24 hours. Y is the sleep after each wake; default **15 minutes**.
- Accept `30m`, `4h`, `1d`, or a bare number of minutes.
- A **wake prompt** (see Launch) is not a launch: `/viking-server-improve-loop wake deadline=<ISO-8601> interval=<Y>m` carries the deadline and interval and means "run one wake".
- Deadline = launch time + X. Use America/Los_Angeles in messages to the user.
- The deadline limits when a wake may **start**, not when it finishes. A wake that starts before the deadline picks a normal-sized candidate and runs it to the end, even past the deadline; do not shrink candidates or skip wakes as the deadline nears. The first wake that starts after the deadline writes the `final` report.

## Session and context

Prefer a **dedicated chat** for this loop so wake noise does not fill a human-work thread. Durable state lives in the ledger and `STEERING.md`, not in chat history.

Keep the parent transcript thin:

- Per wake, the parent launches **sequential foreground subagents** with the Agent tool (`general-purpose`): scout (includes Sync), implementer, and for high-risk commits a reviewer. Each returns at most six lines; their tool output stays out of the parent.
- Act only on the wake prompt that `ScheduleWakeup` delivers. Never poll or schedule extra wakes.
- User chat: one or two sentences only when a commit landed, a decision is new, or the loop stops. Empty/reject wakes: re-arm silently (no status line unless the user asked for status).
- **Compactness hygiene:** increment `wakeCount` in the ledger when scheduling each wake. Every **25** wakes, write a one-paragraph `compactSummary` (deadline, interval, last outcome, open decisions count), tell the user **once** that `/compact` or a fresh session can resume from the ledger and that summary (same deadline and `intervalMinutes`), then reset `wakeCount` to 0. Between notices, stay silent on empty wakes.

## Scope

One repo, one branch: `d:\src\git\Viking` on `dev`, edited through the tracks in [scope.md](scope.md). Parity reads the WCF client in `d:\src\git\VikingLegacy` (`Legacy`) and never edits it. The scout picks the track as well as the category, rotating so no track starves; `STEERING.md` `focus:` wins.

Priority when several tracks have candidates: a bug likely in real use, then a parity gap (`missing` before `partial` before a performance row over the line), then deploy and docs mismatches, then the rest.

## Sync from the remote

**Launch** runs Sync once in the main session. On **later wakes**, the **scout** runs Sync first (not main), so fetch/merge output stays out of the long-lived chat context.

- `d:\src\git\Viking` (`dev`): `git fetch origin`, then `git merge --ff-only @{u}`. Sync never edits. Parity also needs `d:\src\git\VikingLegacy` current; fetch it, but leave merging it to the client loop or the user.
- Fast-forward only. If a repo cannot fast-forward (diverged history, or uncommitted local edits the incoming commits touch), leave it as it is, work from the current tree, and record `{repo, reason, date}` in `syncSkips`, keeping one entry per repo and reason (update its date instead of appending a duplicate; divergence normally persists until the user reconciles it by hand). Never stash, reset, rebase, force, or discard anything to make a pull succeed, and never push.
- If the pull touches the `candidate`'s files, the scout re-checks the candidate before the implementer runs. If it breaks a build or a green test, do not fix it as part of the sync; it shows up at the next baseline run.

## Time box

If a wake passes about 60 minutes of work without a candidate clearing the gates, revert, add it to `deferred`, and end the wake.

## Launch

1. Start the session in `d:\src\git\Viking` so this project skill loads. Scheduling uses the `ScheduleWakeup` tool, one wake at a time. Never create a repeating schedule.
2. Create the reports folder, `.loop\ledger.json`, and `STEERING.md` if missing. Run Sync from the remote. Build `hotspots` and `complexity`, run the baselines (gates.md Baselines: tests, builds, warnings), then run one wake immediately.
3. Schedule the next wake with `ScheduleWakeup`: `delaySeconds` = Y in seconds (the tool clamps to 60-3600; for a longer Y, set `nextWakeAt` in the ledger and re-schedule silently, doing no work, until it is due), `noop` = true when the wake did nothing, a short `reason`, and this `prompt`:

    /viking-server-improve-loop wake deadline=<ISO-8601> interval=<Y>m

   Use the slash form verbatim each time; this skill is user-invoked only, so a plain-text prompt would not load it. Write `nextWakeAt` to the ledger.
4. Tell the user the deadline, the interval, and that `STEERING.md` is read every wake. If `STEERING.md` contains `pause`, say so: the loop will wake but do no work.

## Each wake

The main session only launches the subagents, talks to the user when needed, and schedules the next wake. Code work and Sync stay out of the parent context after launch.

1. Read the wake prompt. Record any decision answers the user gave in chat into the ledger. **Do not Sync in main** after launch.
2. Launch the subagents below one after another (Agent tool, `subagent_type: general-purpose`, foreground, `model` per Model routing). Give each the skill folder path, the ledger path, and only the steps it owns; the next subagent starts from the ledger, not from the previous one's transcript.

   0. **Sync** (scout, first; see Sync from the remote): write `lastSync` and `syncSkips`.
   1. **Scout** (reads SKILL.md, scope.md, protected.md, categories.md, the ledger, `STEERING.md`; plus parity.md, deploy.md, or docs.md for those tracks; reports.md and gates.md Baselines only when a report or baseline is due):
      - reads `STEERING.md` first; on `pause` it ends the wake right after Sync, with no report, hotspot rebuild, baseline, or candidate;
      - reads the ledger;
      - stops on `stop`, the deadline, or `emptyWakes` reaching 3, writing the `final` report;
      - when 24 hours have passed since `lastReportAt` (or `startedAt`), writes the `daily` report, rebuilds `hotspots` and `complexity`, re-runs the baselines, and checks for reworked commits;
      - otherwise picks one candidate and writes it to `candidate`: track, category, files, a one-paragraph plan, and a risk level (`low` or `high`, per Model routing). It edits no production code. A protected or unbenchmarkable candidate becomes a proposal or decision, and the wake ends.
   2. **Implement** (reads SKILL.md Scope and Time box, scope.md, protected.md, gates.md, docs.md, the ledger, plus the track's file): run the candidate through the gates, tests, mutation check, and benchmark when needed; commit if green; clear `candidate`; write the ledger. If the candidate is riskier than scouted, stop without committing and set risk to `high` for the next wake.
   3. **Review (high risk only)** (reads protected.md, gates.md, the commit's diff): a fresh subagent given only the diff, protected.md, and gates.md (not the author's reasoning) reviews the commit against the gates and reruns the commit's mutation check itself (gates.md Mutation check); an implementer claim the reviewer cannot reproduce counts as a gate violation. On a gate violation or likely defect, fix in a follow-up commit, or revert when a fix is not clear. Record the outcome on the commit's ledger entry.

   Together the steps return only this block to the parent:

   ```text
   outcome: <sha + summary | why nothing passed>
   risk/review: low | high accept | high revert
   decisions: none new | <ids>
      stop: yes|no
      ```

3. Parent: if `stop` is yes, do not schedule a wake (and have a subagent write the `final` report if the scout did not). Else increment `wakeCount`, run compactness hygiene if due (see Session and context), and schedule the next wake. Post to the user only when there is a commit, a new decision (as a question), a six-commit notice, a compact notice, or a stop; otherwise stay silent.
4. When the user asks for a report, launch a scout-model subagent to write a `requested` report; the loop keeps running. When the user asks to stop, set `stop` in the ledger, call `ScheduleWakeup` with `stop: true`, launch a subagent for the `final` report, and schedule nothing.
5. A wake that fires after `stop` was recorded writes nothing new and does not re-schedule. The first wake that starts after the deadline writes the `final` report (unless one already exists for this run), sets `stop`, and does not re-schedule. A wake that fires before `nextWakeAt` ends silently and re-schedules.

## Steering

`d:\src\git\VikingSlackUsersAI\Automatic Improvement Reports\Server\STEERING.md` is the user's; the loop reads it every wake and never edits it. Lines it understands:

- `focus: <path or category>`: search there first.
- `avoid: <path, pattern, or category>`: treat as protected for this run.
- `answer: <decision id> <choice>`: answers a pending decision.
- `pause`: skip work but keep arming wakes. `stop`: write the final report and stop.

Anything else is guidance the loop follows when it applies. Create the file with a short commented template at launch if it is missing.

## Decisions

When a candidate needs the user's call (two reasonable designs, a behavior change that might be wanted, a protected-area proposal that blocks other work), do not guess. Add it to `decisions` with an id, the question, and the options; send the question's first line with the `PushNotification` tool (load it with ToolSearch first); and move to another candidate. The main session posts the question in chat. An answer in chat or as an `answer:` line in `STEERING.md` is recorded in the ledger, and a later wake acts on it. Open decisions appear in every report.

Record every answer with its source in `answerSource`: `chat <ISO date>` for an answer the user gave in chat, or `STEERING.md` for a steering line. Only the main session records chat answers, and only from the user's own words, never from a subagent's or peer session's report. An answer with no `answerSource` is not an answer: set it back to `null`, add an `answerNote` saying so, keep the decision open, and ask again in the next report.

## Ledger

`d:\src\git\VikingSlackUsersAI\Automatic Improvement Reports\Server\.loop\ledger.json`, shared by every fresh subagent. Read it at the start of each wake; write it before the wake ends.

```json
{
  "startedAt": "", "deadline": "", "intervalMinutes": 15, "nextWakeAt": "", "stop": false,
  "hotspots": [], "baselineFailures": [], "flaky": [],
  "lastCategory": 0, "lastTrack": "", "emptyWakes": 0,
  "wakeCount": 0, "compactSummary": "",
  "parity": { "done": 0, "partial": 0, "missing": 0, "overLine": [] },
  "candidate": { "track": "", "category": 0, "files": [], "plan": "", "risk": "low|high" },
  "commits": [{ "repo": "", "program": "", "track": "", "sha": "", "category": 0, "summary": "", "lineDelta": 0,
                "benchmark": "", "mutation": "", "risk": "", "models": {}, "review": "" }],
  "countsSincePublish": { "gRPCClient": 0, "Servers": 0, "SQL": 0 },
  "rejected": [], "deferred": [], "proposals": [],
  "decisions": [{ "id": "", "question": "", "options": [], "askedAt": "", "answer": null, "answerSource": null }],
  "buildBaseline": [], "warningBaseline": {}, "complexity": [],
  "reworked": [],
  "lastSync": {}, "syncSkips": [], "modelSubstitutions": [],
  "lastReportAt": null, "reports": []
}
```

This loop starts a fresh ledger here. The old shared ledger in `Automatic Improvement Reports\.loop` stays where it is as history; read its `parity`, `rejected`, `deferred`, `proposals`, and open `decisions` for the gRPC client, Server, SQL, Parity, Deploy, and Docs tracks once at launch and copy those entries into this ledger. Handling quirks of the real file:

- It has a UTF-8 BOM: read with `utf-8-sig` and write it back with a BOM.
- PowerShell-written fields may be wrapped as `{"value":[...]}` (seen on `hotspots`). Unwrap on read and write the flat shape shown above.
- Never hand-edit the large file, and do not write ad-hoc read-modify-write code for it. Use [ledger.py](ledger.py) in this folder, run with `python -I`: `get`, `set`, `append`, `incr`, `update KEY FIELD=VALUE {json}` (merge into matching list entries, for example a commit by `sha`) and `remove KEY FIELD=VALUE`. It unwraps `{"value":...}`, keeps the BOM and line endings, copies the old file to `ledger.json.bak`, and writes a temp file then renames; `update` and `remove` fail when nothing matches. `ledger.json.corrupt.bak` and `repair_ledger.py` in `.loop` show what a bad write cost before.

## Model routing

Pass these as the Agent tool's `model` (`sonnet`, `opus`, `haiku`, `fable`). Context window size cannot be set; keep each subagent's input small.

- **Scout, reports, ledger upkeep:** `sonnet`. Use `haiku` only for a report that just formats ledger data.
- **Low-risk implementer:** `sonnet`. Dead weight (11), resolvable debt notes (12), hand-rolled helpers (7), test-only wakes (14), and docs-only wakes (24), when the change stays inside one project and away from protected areas.
- **High-risk implementer:** `opus`. Everything else, plus any low-risk category that touches a shared library, more than one program, a numeric type, a file next to a protected area, a gRPC service contract, SQL objects, or the deploy script.
- **Reviewer:** `fable`. Only after high-risk commits, so the second opinion comes from a different model than the implementer. If `fable` is rejected, use `opus` in a fresh subagent and record it.

The main session can run on any model; it does no code work. Each step records its model in the commit's ledger entry (`models`). If an Agent call rejects a model, retry once with `sonnet` and log it in `modelSubstitutions`.
