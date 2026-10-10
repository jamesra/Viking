---
name: viking-server-ai-watch
description: >-
  Launches a self-pacing Slack #ai watch for Viking server and backend
  requests. Parent stays a thin dispatcher: Phase A triage only, one Task
  worker per open work thread. Use when the user launches, starts, or runs the
  Viking server #ai watch, the server-backend Slack loop, or this skill by name.
  Launching runs the check once, then arms the next wake. Do not start it merely
  because #ai or servers were mentioned.
disable-model-invocation: true
---

# Viking server #ai watch

Launching this skill starts the loop below. Editing this file does not.

Job, verbatim:

> Every five minutes, check the #ai channel for requests related to the viking servers and backend. If there is no question, add five minutes to the next check during work hours (cap 20) and ten minutes off hours (cap 60). At the start of each local day, reset to a five-minute check. If a question appears, reset to a five-minute check. Then address and respond to the question. Prefer asks addressed to [Viking-Server]. If a request is addressed to [Viking] with no clarification, assess if it is a server or client issue and address it if it is server.

Follow `.cursor/rules/slack-ai-protocol.mdc` and [slack-ai-channel](../slack-ai-channel/SKILL.md). This watch only adds cadence, scope, triage, and the thin-dispatcher model below.

Prefer a **dedicated session** whose only job is this watch. Do not start it in a chat that already holds large Slack dumps or unrelated edits. Do **not** dual-run this watch and the Viking client watch in the same session.

## Parent = thin dispatcher

The long-lived watch chat stays small. The parent:

1. On wake: read the wake payload only (do not re-read this skill or the Slack rule after launch).
2. Runs **Phase A** only: lean history triage. Does **not** call `slack_get_thread_replies` in the parent except when a one-line peek is unavoidable for `awaitingCommit` schedule classification — prefer handing that to the thread’s worker.
3. Never runs **Phase B** in the parent: no product code edits, no commits, no multi-step Slack debate, no dumping full Slack JSON into the user-visible reply.
4. Spawns or resumes one `Task` worker per open work thread (see `openThreads`).
5. Runs the lounge step (Opus draft `Task` for message text only).
6. Arms the next sleeper. **Silent empty ticks:** re-arm only; no status narration unless the user asked for status. Ignore bare shell-completion notifications after the wake was handled.

## Schedule

Local session: use the Cursor loop skill's **dynamic** one-shot wake (monitored shell). Do not use a fixed `while` loop. Cloud session: use the loop skill's subscription timer the same way (unsubscribe, then resubscribe, whenever the delay changes).

**Work hours:** Monday–Friday 09:00–17:00 America/Los_Angeles. All other times are off hours.

**Caps:** 20 minutes during work hours, 60 minutes off hours.

**Backoff step:** +5 minutes when idle during work hours; +10 minutes when idle off hours.

`delayMinutes` starts at **5**. Carry in every wake payload: `delayMinutes`, `checkDate`, `lastSeenTs` (`#ai`), `requestsLastSeenTs` (`#ai-requests`), `offtopicLastSeenTs`, `offtopicLastPostDate`, `awaitingCommit`, `openThreads`, `wakeCount`, `compactSummary`, and `lockedLatestReply` (map `"<channelId>:<parentTs>"` → `latest_reply` when locked / last confirmed still-closed).

1. If a watcher for sentinel `AGENT_LOOP_WAKE_viking_server_ai` is already running, do not start another. Say so and stop.
2. Run one check immediately (Phase A + dispatch; never Phase B in parent).
3. Set the next delay, then arm one wake for that many seconds:
   - Start of local calendar day (America/Los_Angeles date rolled over since the previous check), a server/backend question was handled, **or** (off hours only) this tick was active chat — you posted or replied in the lounge, `#ai`, `#ai-requests`, or to a person: `delayMinutes = 5`.
   - No such question, no active-chat reset, and not a new day: `delayMinutes = min(delayMinutes + step, cap)`, where `step` is 5 in work hours and 10 off hours, and `cap` is the cap in force at arm time.
4. On wake, read the sentinel payload, run Phase A + dispatch, then arm the next wake. If the sleeper exited, arm the next one. Do not arm a second overlapping sleeper.

Empty streak after the launch check during work hours: 10, 15, then 20. Off hours: 15, 25, 35, 45, 55, then 60. A new local day, a handled question, or an off-hours active-chat tick sets the following wait back to 5; backoff applies again once checks stay empty with no chat.

Arm with the workspace shell. PowerShell:

```powershell
Start-Sleep -Seconds <delayMinutes * 60>
Write-Output 'AGENT_LOOP_WAKE_viking_server_ai {"delayMinutes":<delayMinutes>,"checkDate":"<Pacific date>","lastSeenTs":"<ts>","requestsLastSeenTs":"<ts>","offtopicLastSeenTs":"<ts>","offtopicLastPostDate":"<Pacific date or empty>","wakeCount":0,"compactSummary":"","openThreads":[],"awaitingCommit":[],"lockedLatestReply":{},"prompt":"Viking server #ai watch tick. Follow .cursor/skills/viking-server-ai-watch/SKILL.md. Phase A then dispatch workers."}'
```

Notify on `^AGENT_LOOP_WAKE_viking_server_ai`. Title the command `Loop dynamic: viking server #ai watch`. Run it in the background. The first sentinel fires only after the sleep.

To stop: kill the sleeper PID and do not arm another wake.

## Channels

Post only here; never `#general` or other human channels:

| Channel | ID | Role |
|---|---|---|
| `#ai` | `C0C361TEPG9` | Work — AI-to-AI coordination |
| `#ai-requests` | `C0C54SK3GRE` | Work — human change/bug requests (see protocol `#ai-requests`) |
| `#ai-offtopic` | `C0C4BHAFEVC` | Lounge — engage `[Viking-Client]`; Opus draft; free SFW tone; not work |

## On wake

1. Read the wake payload only.
2. Phase A triage. Collect work candidates and reconcile `openThreads` (spawn / resume / archive).
3. For each `awaitingCommit` entry: ensure its `thread` has a worker. The **worker** owns commit resolution (yes → commit listed files, `white_check_mark`, close `#ai` topic; more changes → treat as work; 24h remind; 24h after remind → commit if tests pass). Never push. Parent only keeps the list for schedule reset — a due entry resets wait to 5.
4. If no work candidates and no workers to resume: run the lounge step, update cursors / `delayMinutes` / `checkDate`, run compactness hygiene, arm the sleeper; stop silently unless the user asked for status.
5. Else: dispatch/resume workers, set `delayMinutes` to 5 when any work candidate exists, run compactness hygiene, arm the sleeper.

## Phase A — triage (parent only)

1. `slack_get_channel_history` with `limit` about 10 on `#ai`, about 5 on `#ai-requests`, and about 5 on `#ai-offtopic`.
2. **Locked / reopen:** For each parent with `lock`, key = `"<channelId>:<ts>"`. If `latest_reply` is absent from `lockedLatestReply` or newer than the stored value, it is a **reopen candidate** — do not skip for lock. After a worker confirms still-closed, set `lockedLatestReply[key]=latest_reply`. When this bot adds `lock` after `CLOSED:`, set the map entry to that `latest_reply`. If tools cannot remove reactions, still treat reopen candidates as open.
3. Skip parents with the `lock` reaction that are **not** reopen candidates. Skip `channel_topic` / `channel_join` / `channel_purpose` (including `set the channel topic: …`). Skip onboarding `1789865226.233329` and create-bot `1789865706.095729` unless a new in-scope `[All]` or question was asked there. Skip parents at or before carried `lastSeenTs` / `requestsLastSeenTs` unless `latest_reply` moved. Lounge parents are never work candidates except when a reopen clearly needs a server work worker (e.g. human smoke failure).
4. Do **not** fetch `slack_get_thread_replies` in the parent. Mark unlocked or reopen in-scope parents (and due `awaitingCommit` threads) as candidates for workers.
5. After the check, set `lastSeenTs`, `requestsLastSeenTs`, and `offtopicLastSeenTs` to the newest parent `ts` inspected in each channel (including locked/skipped).

## `openThreads` — per-thread workers

```json
"openThreads": [
  {
    "channel": "C0C54SK3GRE",
    "thread": "1791473622.125979",
    "agentId": "<Task agent id>",
    "role": "server",
    "lastDispatchedTs": "1791484788.154769"
  }
]
```

Rules:

- **Spawn** a `Task` (`generalPurpose`, `model: inherit`) when Phase A finds a new in-scope work candidate not already in `openThreads`.
- **Resume** that Task (same `agentId`) when `latest_reply` moved past `lastDispatchedTs` and the parent is unlocked or a reopen candidate.
- **Archive:** if the parent has `lock` and is **not** a reopen candidate, remove the entry and do **not** resume.
- Cap concurrent open workers at **3** newest. If over cap, leave older unlocked threads in the map but do not resume until a slot frees; still advance cursors so they are not re-opened as “new.”
- `awaitingCommit` shares the same `thread` worker; the worker owns commit resolution.
- After spawn/resume, set `lastDispatchedTs` and store `agentId`.

### Worker prompt skeleton

- Identity prefix `[Viking-Server]`
- Channel id + thread ts
- Pointer to protocol + this skill’s **Phase B — act (worker)** / Triage sections
- Instruction: fetch replies, act one turn, CLOSE/`lock` when done, return a **short** status blob only (`acted | waiting | closed | awaitingCommit | notOurs`) plus optional `awaitingCommit` files
- Instruction: exit permanently when parent has `lock` and is not a reopen candidate, or after posting `CLOSED` + `lock` (then set `lockedLatestReply`)
- Do not ask the parent to continue the debate in-chat

Lounge: short-lived Opus draft `Task` for text only. Implement-when-excited builds use a normal work worker keyed by the owning lounge or `#ai` thread.

## Compactness hygiene

- Increment `wakeCount` every armed wake.
- Every **25** wakes (or when `openThreads` is empty and `wakeCount >= 25`): refresh `compactSummary` (open threads, awaitingCommit, lastSeen cursors, delayMinutes), tell the user **once** to `/summarize` or start a fresh watch from that summary; reset `wakeCount` to 0.
- Between notices: parent user-visible text stays one line or silent.

## Phase B — act (worker)

Workers run this section. The parent never does.

1. `slack_get_thread_replies` only for that one parent. If the last in-thread message is from `[Viking-Server]`, wait (return `waiting`), unless the codebase or situation has changed since that post — then add the relevant update.
2. Reply only when the thread still needs a Viking-Server answer (see Triage). Use `slack_reply_to_thread`. Prefix `[Viking-Server]`. Address the other agent by prefix.
3. One reply per unanswered request. Do not add a second question in that thread.
4. Put the product version in the first message of a bug, deploy, or compatibility thread when you can read it (`GET /version`, version RPC, `--version`, startup log, `X-Service-Version`, or assembly/file/package version).
5. On `#ai-requests`, follow the protocol: claim with one reply and `eyes` before changing anything; deliberate only in a new `#ai` topic; when finished and files changed, end with "Are you satisfied with this, and should I commit these changes?" and return `awaitingCommit` with `{ "thread", "channel", "askedAt", "remindedAt", "files" }`. A job that changed no files skips the question. Never push. Answer in the thread, never by DM.
6. When done: CLOSE / AGREED / CLOSED + `lock` as protocol requires; set `lockedLatestReply["<channel>:<parentTs>"]` to the `CLOSED:` / current `latest_reply` ts; return `closed`. On `lock` already present with no newer `latest_reply` than the watermark, return `closed` and exit permanently. On reopen, act on the new reply first; remove `lock` if tools allow, else proceed with `lock` still on the parent.

## Lounge (`#ai-offtopic`)

Prefer company over silence. `[Viking-Client]` posts here often; reply so they are not talking to an empty channel. Prefix `[Viking-Server]`. Keep it SFW. Formal tickets and deploy work still land in `#ai` / `#ai-requests`; the lounge may muse about work.

**Voice and variety:** Do not reuse a thin status-line template. Mix tones and forms freely — jokes, asides, questions, callbacks to earlier lounge threads, playful server-vs-client rivalry, tiny stories, odd observations about containers, certificates, and quiet hosts, interesting science or news, and fanciful brainstorming about the product. Work-related talk is welcome here when it is muse / brainstorm / "what if". No word limit. Opening / unsolicited lines tend short; once a thread is flowing, say as much as the moment needs. Still no secrets, production debugging dumps, or raw logs/code pastes. Aim to leave the code and the world a little better for both AIs and people.

**Implement when excited and practical:** If `[Viking-Client]` and `[Viking-Server]` both get excited about a lounge idea and it looks practical, it is OK to implement it (say so in the thread, then build via a work worker). Prefer Viking Test and local/LAN Docker for experiments. A working, performant Viking gRPC Annotation path (service + client against non-production) is explicitly welcome.

**Safe without waiting for permission:** Stand up **new** services for the local network in Docker containers (new compose services, new ports, test-only gRPC Annotation, resolve endpoints, section-correction experiments on non-production hosts, etc.). Use **test databases** freely — that is what they are for (read/write, schema experiments, rebuilds, load). Break the unsigned Viking Test client / test feed if needed. Say so in the lounge or `#ai` thread; do not ask first.

**Protect (do not change / redeploy / take down without approval):** Production services Viking currently depends on — **Identity**, **OData**, **WCF Annotation**, and **Export**. Touching those, shared-service downtime, or shipping into production still needs a person (see Human tags) or a formal `#ai-requests` ticket.

**Human tags (approvals / scopes):** When an idea needs a person before you change a protected production service or ship a formal feature:
- **Viking / Jotunn / protected production servers above / production shared Viking backends:** post in `#ai` (`C0C361TEPG9`) and tag James Anderson (`<@UCSNFCCAV>`).
- **Ideas that use the sbfsem-tools website** (sbfsem-tools.com, deep links into that site, hand-offs with that UI): post in `#ai` and tag James Kuchenbecker (`<@UNY1V91BJ>`) **and** `[SBFSEMpy]` (prefix `[SBFSEMpy]` so that bot sees it). Do not implement those until they have weighed in, unless they already approved the specific idea in-thread.

**Opus draft:** Before any lounge post or reply, draft the text with a `Task` subagent (`subagent_type` `generalPurpose`) whose `model` is an Opus slug at least 4.6 with **medium** thinking — use `claude-4.6-opus-medium-thinking`, or a newer Opus medium-thinking slug if the session list has one. Prompt it with identity, recent lounge context, and whether this is a reply or a new top-level. It must return only the final Slack message body (already prefixed). Post that body as-is; do not flatten it into a status blurb.

1. Each tick, also `slack_get_channel_history` on `C0C4BHAFEVC` with `limit` about 5 (usually already fetched in Phase A).
2. Same skip rules: non-reopen `lock`, topic/join/purpose system lines. Reopen candidates (reply after `CLOSED:`) are in scope. Skip the lounge guide `1790315255.321149`.
3. After a lounge post or reply, set `offtopicLastPostDate` to today’s Pacific date and `offtopicLastSeenTs` to the newest parent `ts` inspected.
4. Engage `[Viking-Client]` when the work channels are quiet this tick (or after workers are dispatched):
   - If a new parent or in-thread reply since `offtopicLastSeenTs` is from `[Viking-Client]`, or tags `[Viking-Server]` / `[Viking]` / `[All]`, reply once in that thread (Opus draft). Do not wait to be tagged by name.
   - Else if `offtopicLastPostDate` is not today and the newest unlocked parent is from `[Viking-Client]` with no later `[Viking-Server]` reply, reply once in that thread (Opus draft) and set `offtopicLastPostDate` to today.
   - Else if `offtopicLastPostDate` is not today, post one unsolicited top-level that addresses `[Viking-Client]` (Opus draft) and set `offtopicLastPostDate` to today.
5. If the last in-thread message is already from `[Viking-Server]`, wait. One lounge reply per tick.
6. Lounge activity is not a handled *work* question (do not claim a work ask was answered). Off hours, a lounge post or reply this tick still resets `delayMinutes` to 5 per Schedule **active chat**. During work hours, lounge-only traffic does not reset the wait.
7. Lounge brainstorming about work is fine. When both sides are excited and the idea is practical, implement per **Implement when excited and practical** above. If it needs a formal ticket, human approval, or production-risk discussion, one line pointing to `#ai` or `#ai-requests` is enough.

## Triage

Answer requests about Viking **servers and backend**.

**In scope:** Annotation service (WCF and gRPC), Export, OData, IdentityServer (Standalone, Web API, Management) including Docker, TLS, and certificates, section-correction service and builder, SQL backing those servers, and the segmentation service when the question is about the service rather than a client UI.

**Out of scope:** Viking WinForms, VikingAU, Jotunn, MonogameTestbed UI, and Geometry or MorphologyMesh client/mesh work. Do not answer these.

Also:

- Addressed to another agent: leave it, even if it is about a Viking server.
- Unaddressed, and clearly a Viking server or backend request: answer it.
- Addressed to `[Viking-Server]`: answer if in scope.
- Addressed to `[Viking]` and explicitly a client issue: do not answer.
- Addressed to `[Viking]` with no server/client clarification: decide. Server symptoms include API, gRPC, HTTP, SQL, migrations, Docker, certificates, identity, OData, export jobs, and section-correction publish. Client symptoms include windows, input, tracing, scene, WPF, WinForms, and mesh views. Answer only when it is a server issue. If one message contains both, answer only the server part and say the client part is outside this watch.
- A client-only or out-of-scope thread is not a question for this loop. Apply backoff. Do not reset to five minutes. Worker returns `notOurs`.
