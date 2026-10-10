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

- X is the run length; default 24 hours. Y is the sleep after each wake; default **15 minutes**.
- Accept `30m`, `4h`, `1d`, or a bare number of minutes.
- Deadline = launch time + X. Use America/Los_Angeles in messages to the user.

## Session and context

Prefer a **dedicated chat** for this loop so wake noise does not fill a human-work thread. Durable state lives in the ledger and `STEERING.md`, not in chat history.

Keep the parent transcript thin:

- One **orchestrator** `generalPurpose` Task per wake (Sync → scout → implement → high-risk review inside that Task). Do not stack separate scout/implement/review Tasks into the parent.
- Act only on `AGENT_LOOP_WAKE_viking_client_improve` output. **Ignore** bare shell-completion notifications for the sleeper after that wake was handled — do not reply to the user for those.
- User chat: one or two sentences only when a commit landed, a decision is new, a publish ran, or the loop stops. Empty/reject wakes: re-arm silently (no status line unless the user asked for status).
- **Compactness hygiene:** increment `wakeCount` in the ledger when arming each sleeper. Every **25** wakes, write a one-paragraph `compactSummary` (deadline, interval, last outcome, open decisions count), tell the user **once** to `/summarize` or start a fresh chat re-armed from the ledger + that summary (same deadline / `intervalMinutes`), then reset `wakeCount` to 0. Between notices, stay silent on empty wakes.

## Scope

Edit `VikingClient` on branch `Legacy` only: `Clients/Viking`, `Clients/MonogameTestbed`, shared client libraries those two compile, and `Clients/VikingBenchmarks` once it exists. Confirm the branch before the first edit. Do not edit generated code, comment code out to compile, push, or start server work. Do not mirror into `VikingServers`; append each commit sha to `mirrorPending`.

## Sync from the remote

**Launch** runs Sync once in the main session. On **later wakes**, the **orchestrator** runs Sync first (not main), so fetch/merge output stays out of the long-lived chat context.

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

The main session only launches one orchestrator, talks to the user when needed, and arms the next sleeper. Code work and Sync stay out of the parent context after launch.

1. Read the payload. Do not re-read the `loop` skill. Record any decision answers the user gave in chat into the ledger. **Do not Sync in main** after launch.
2. Launch **one** foreground `generalPurpose` Task (orchestrator). Prefer `composer-2.5` unless a listed model is available for the risk level; record substitutions. Prompt shape:

   > You are the single orchestrator for one wake of viking-client-improve-loop. Run Sync first, then scout → implement → (high-risk review) in this Task; do not spawn further agents. Skill folder: \<path\>. Ledger: \<path\>. Follow SKILL.md Sync and Each-wake steps. Return only the six outcome lines.

   Inside that Task, in order:

   0. **Sync** (see Sync from the remote): write `lastSync` and `syncSkips`.
   1. **Scout** (reads SKILL.md, protected.md, categories.md, the ledger, `STEERING.md`; reports.md and gates.md Test baseline only when a report or baseline is due):
      - reads `STEERING.md` and the ledger;
      - stops on `stop`, the deadline, or `emptyWakes` reaching 3, writing the `final` report;
      - when 24 hours have passed since `lastReportAt` (or `startedAt`), writes the `daily` report, rebuilds `hotspots`, re-runs the test baseline, and checks for reworked commits;
      - on `pause`, ends the wake;
      - otherwise picks one candidate and writes it to `candidate`: category, files, a one-paragraph plan, and a risk level (`low` or `high`, per Model routing). It edits no production code. A protected or unbenchmarkable candidate becomes a proposal or decision, and the wake ends.
   2. **Implement** (reads SKILL.md Scope and Time box, protected.md, gates.md, the ledger): run the candidate through the gates, tests, mutation check, and benchmark when needed; commit if green; publish if due; clear `candidate`; write the ledger. If the candidate is riskier than scouted, stop without committing and set risk to `high` for the next wake.
   3. **Review (high risk only)** (reads protected.md, gates.md): after a high-risk commit, review against the gates. On a gate violation or likely defect, fix in a follow-up commit in the same Task, or revert when a fix is not clear. Record the outcome on the commit's ledger entry.

   Orchestrator return (max six lines):

   ```text
   outcome: <sha + summary | why nothing passed>
   risk/review: low | high accept | high revert
   decisions: none new | <ids>
   publish: no | <version>
   stop: yes|no
   viking_count: <N>/6
   ```

3. Parent: if `stop` is yes, do not arm a sleeper (and write/finalize report if the orchestrator did not). Else increment `wakeCount`, run compactness hygiene if due (see Session and context), arm one Y-minute sleep. Post to the user only when there is a commit, a new decision (as a question), a publish, a compact notice, or a stop — otherwise stay silent.
4. When the user asks for a report, launch a scout-model Task to write a `requested` report; the loop keeps running. When the user asks to stop, kill the tracked sleeper PID, launch a scout-model Task for the `final` report, and arm nothing.
5. If a bare sleeper **completion** notification arrives after the wake output was already handled, ignore it (no user reply, no second wake).

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
  "startedAt": "", "deadline": "", "intervalMinutes": 15,
  "hotspots": [], "baselineFailures": [], "flaky": [],
  "lastCategory": 0, "emptyWakes": 0,
  "wakeCount": 0, "compactSummary": "",
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

The main session can run on Auto or any inexpensive model; it does no code work. The orchestrator records the model used for each step in the commit's ledger entry. When the preferred implementer/reviewer slug is missing, the orchestrator uses the best available substitute and logs `modelSubstitutions`.
