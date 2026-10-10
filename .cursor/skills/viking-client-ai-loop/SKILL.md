---
name: viking-client-ai-loop
description: >-
  Starts a self-pacing Slack watch for Viking client requests in #ai and
  #ai-requests, and a lounge check in #ai-offtopic (free SFW tone, Opus-drafted).
  Parent stays a thin dispatcher: Phase A triage only, one Task worker per open
  work thread. Use when this skill is launched, when the user asks to watch those
  channels for Viking client questions, or when they ask to begin the Viking
  client channel loop.
disable-model-invocation: true
---

# Viking client Slack loop

When this skill is launched, begin the loop below. Do not start it because this file was opened for another task.

If the user says the loop is already running in another agent, do not start a second copy.

Prefer a **dedicated session** whose only job is this loop. Do not start it in a chat that already holds large Slack dumps or unrelated edits. Do **not** dual-run this watch and the Viking server watch in the same session.

## Loop prompt

On every tick, follow this request verbatim:

```
/loop Every five minutes, check #ai and #ai-requests for requests related to the viking client.  if there is no question, add five minutes during work hours or ten minutes off hours to the next check until checking every 20 minutes during work hours and 60 minutes off hours.  At the start of a day, reset to a five minute check.  If a question appears, reset to a five minute check.  Then address and respond to the question.  Prefix replies `[Viking-Client]`.  If a request is addressed to `[Viking]` with no clarification, assess if it is a server or client issue and address it if it is a client issue.
```

## Launch

1. Read `.cursor/rules/slack-ai-protocol.mdc` once at launch (not on every wake). Follow it afterward from memory.
2. Read the `loop` skill once at launch. Use its **dynamic** local schedule (one-shot `AGENT_LOOP_WAKE_viking_client_ai`). Do not arm a fixed five-minute loop.
3. Check this session's terminals for an existing `AGENT_LOOP_WAKE_viking_client_ai` sleeper. If one is already running here, do not start another.
4. Discover Slack tools with `GetDynamicTools` once at launch. On later wakes reuse the known tool names unless a call fails.
5. Run one check immediately (Phase A only in the parent; spawn/resume workers as below).
6. Arm the next wake. Put schedule state in the payload:

```json
{"prompt":"Follow skill viking-client-ai-loop. Idle triage then dispatch workers only if needed.","intervalMinutes":15,"checkDate":"2026-09-22","lastSeenTs":"1790143673.979419","requestsLastSeenTs":"1790282457.628779","guideReplyTs":"1790282467.436189","offtopicLastSeenTs":"1790315259.321169","offtopicLastPostDate":"2026-09-24","emptyStreak":1,"wakeCount":0,"compactSummary":"","openThreads":[],"awaitingCommit":[],"lockedLatestReply":{}}
```

7. On wake, read that payload only. Do not re-read this skill’s peer files or the `loop` skill. Run Phase A in the parent; dispatch or resume Task workers for work threads; never run Phase B in the parent. Arm the next wake. Stop only when the user asks: kill the sleeper PID and do not arm another wake.

Work hours are Monday–Friday 09:00–17:00 America/Los_Angeles. Every other time is off hours. The cap is the one in force when you arm the next wake.

## Parent = thin dispatcher

The long-lived watch chat stays small. The parent:

1. Reads the wake payload only.
2. Runs **Phase A** (lean history triage). Does **not** call `slack_get_thread_replies` except when a one-line peek is unavoidable for `awaitingCommit` schedule classification — prefer handing that to the thread’s worker.
3. Never runs **Phase B** in the parent: no product code edits, no commits, no multi-step Slack debate, no dumping full Slack JSON into the user-visible reply.
4. Spawns or resumes one `Task` worker per open work thread (see `openThreads`).
5. Runs the lounge step on quiet ticks (Opus draft `Task` for message text only).
6. Arms the next sleeper. **Silent empty ticks:** re-arm only; no status narration unless the user asked for status. Ignore bare shell-completion notifications after the wake was handled.

## Schedule

The interval starts at 5 minutes. Dates and work hours use America/Los_Angeles. Carry `intervalMinutes`, `checkDate`, `lastSeenTs` (`#ai`), `requestsLastSeenTs` (`#ai-requests`), `guideReplyTs`, `offtopicLastSeenTs`, `offtopicLastPostDate`, `emptyStreak`, `awaitingCommit`, `openThreads`, `wakeCount`, `compactSummary`, and `lockedLatestReply` in every wake payload. `lockedLatestReply` is a map of `"<channelId>:<parentTs>"` → `"<latest_reply ts when locked or last confirmed still-closed>"` for cheap reopen detection. `awaitingCommit` is a list of `{ "thread": "<ts>", "channel": "<id>", "askedAt": "<ISO>", "remindedAt": null, "files": [...] }`, one per `#ai-requests` thread where this bot asked whether to commit. A work candidate in `#ai` or `#ai-requests` resets the wait to 5 minutes.

**Active chat (off hours):** If this tick you posted or replied in the lounge, `#ai`, `#ai-requests`, or to a person (DM / human thread), set `intervalMinutes` to **5** and `emptyStreak` to 0 — even when there was no work candidate. Stay at the minimum while the conversation is active. When the chat goes quiet again, empty-tick backoff resumes. During work hours, lounge-only traffic still does not reset the wait (work candidates do).

- At the start of a Pacific calendar day (`checkDate` older than today’s Pacific date), set `intervalMinutes` to 5 and `emptyStreak` to 0 before empty or question logic.
- Empty tick (no work candidate and no active-chat reset): `emptyStreak += 1`. Work hours next wait = `min(20, 5 + 5 * emptyStreak)`. Off hours next wait = `min(60, 5 + 10 * emptyStreak)`; when `emptyStreak >= 3`, use 60. If the computed wait is below the previous interval and you are still idle the same day, keep the previous interval (never shrink on empty).
- Question tick or off-hours active-chat tick: `emptyStreak = 0`, next wait = 5, then dispatch workers / continue the chat.
- After each check, set `lastSeenTs`, `requestsLastSeenTs`, and `offtopicLastSeenTs` to the newest parent `ts` inspected in that channel (including locked and skipped parents). When the pinned guide in `#ai-requests` has a newer `latest_reply`, set `guideReplyTs` to that reply timestamp after handling it. After a lounge post, set `offtopicLastPostDate` to today’s Pacific date.

## On wake

1. Read the wake payload. Do not re-read other skills or the Slack rule file.
2. Phase A triage. Collect work candidates and reconcile `openThreads` (spawn / resume / archive). Do **not** run Phase B in the parent.
3. For each `awaitingCommit` entry: ensure its `thread` has a worker (spawn or resume). The **worker** owns commit resolution (yes → commit listed files, `white_check_mark`, close `#ai` topic; more changes → treat as work; 24h remind; 24h after remind → commit if tests pass). Never push. Parent only keeps the list for schedule reset — a due entry counts as a work candidate. Parent does not read full thread dumps for this.
4. If no work candidates and no workers to resume: run the lounge step, update cursors / `emptyStreak` / `intervalMinutes` / `checkDate`, run compactness hygiene, arm the sleeper; stop. Do not narrate an empty wake unless the user asked for status.
5. Else: dispatch/resume workers for candidates (cap below), reset interval to 5 and `emptyStreak` to 0 when any work candidate exists, run compactness hygiene, arm the sleeper.

## Phase A — triage (parent only)

1. One `slack_get_channel_history` on `#ai` (`C0C361TEPG9`) and one on `#ai-requests` (`C0C54SK3GRE`), each with `limit` 10, and one on `#ai-offtopic` (`C0C4BHAFEVC`) with `limit` 5. Do not list channels. Do not re-read the pinned guide except as step 3 describes.
2. On the `#ai-requests` and `#ai-offtopic` pages, before relevance review, add `lock` to each parent whose newest of `ts` and `latest_reply` is more than 7 days old. Skip the `#ai-requests` guide (`1790282457.628779`) and the lounge guide (`1790315255.321149`). Do not open the thread to lock it. Do not walk older pages.
3. **Locked / reopen:** For each parent with a `lock` reaction, key = `"<channelId>:<ts>"`. If `latest_reply` is absent from `lockedLatestReply` or newer than the stored value, it is a **reopen candidate** — do **not** skip for lock; hand to a worker (work channels) or the lounge step (`#ai-offtopic`). After a worker confirms the thread is still closed (last message is still the `CLOSED:` summary, no later reply), set `lockedLatestReply[key]=latest_reply` and skip next time. When this bot adds `lock` after `CLOSED:`, set `lockedLatestReply[key]` to that thread’s `latest_reply` (the `CLOSED:` ts). If Slack tools cannot remove reactions, still treat reopen candidates as open while `lock` remains.
4. Walk parents newest first. Skip when:
   - reactions include `lock` **and** it is not a reopen candidate (step 3)
   - subtype is `channel_topic`, `channel_join`, or `channel_purpose` (Slack system lines such as "set the channel topic: …" are not agent notifications)
   - in `#ai`: `ts` is less than or equal to payload `lastSeenTs` and `latest_reply` (if any) is not newer than `lastSeenTs`
   - in `#ai-requests`: `ts` is less than or equal to payload `requestsLastSeenTs` and `latest_reply` (if any) is not newer than `requestsLastSeenTs`
   - in `#ai-offtopic`: non-reopen parents (the lounge step handles lounge; it is never a work candidate unless a human bug/request in a reopen clearly needs a work worker)
   - `latest_reply` text is known to be from `[Viking-Client]` / this bot without opening the thread — if unsure and `ts` is new, treat as a candidate for a worker (do not open in the parent)
   - pinned guideline parents. In `#ai`, ONBOARDING and CREATE BOT, unless newly addressed to `[Viking-Client]`, `[Viking]`, or `[All]`. In `#ai-requests`, the guide `1790282457.628779`, unless `latest_reply` is newer than `guideReplyTs` or a new message tags `[Viking-Client]` or `[Viking]`
5. An `#ai` candidate is an unlocked (or reopen) parent that looks like an unanswered Viking-client ask, `[Viking-Client]`, `[Viking]` (unsure client vs server), or `[All]` since `lastSeenTs`.
6. An `#ai-requests` candidate is an unlocked (or reopen) parent tagged `[Viking-Client]` or `[Viking]`, or an untagged human post this bot has not marked `see_no_evil`. A new reply on the guide is a candidate only so a worker can add a bot introduction to the tag list. It is not a client request.
7. If none: empty tick (unless the lounge step posts — see **Active chat** under Schedule). One work candidate across `#ai` or `#ai-requests` is enough to reset the wait. During work hours, lounge traffic alone does not reset `emptyStreak`. Off hours, a lounge post or reply this tick resets to 5. A lounge address waits until a quiet work tick so a work reply stays the only post that wake.

## `openThreads` — per-thread workers

Carry in every wake payload:

```json
"openThreads": [
  {
    "channel": "C0C54SK3GRE",
    "thread": "1791473622.125979",
    "agentId": "<Task agent id>",
    "role": "client",
    "lastDispatchedTs": "1791484788.154769"
  }
]
```

Rules:

- **Spawn** a `Task` (`generalPurpose`, `model: inherit`) when Phase A finds a new in-scope work candidate not already in `openThreads`.
- **Resume** that Task (same `agentId`) when `latest_reply` moved past `lastDispatchedTs` and the parent is unlocked or a reopen candidate.
- **Archive:** if the parent has `lock` and is **not** a reopen candidate, remove the entry from `openThreads` and do **not** resume. Reopen candidates stay/spawn a worker.
- Cap concurrent open workers at **3** newest. If over cap, leave older unlocked threads in the map but do not resume until a slot frees; still advance `lastSeenTs` / `requestsLastSeenTs` so the parent does not re-open them as “new.”
- `awaitingCommit` entries share the same `thread` worker when present; the worker owns commit resolution.
- After spawn/resume, set `lastDispatchedTs` to the newest known reply ts for that parent and store the returned `agentId`.

### Worker prompt skeleton

Parent launches/resumes with a fixed brief:

- Identity prefix `[Viking-Client]`
- Channel id + thread ts
- Pointer to protocol (from launch memory) + this skill’s **Phase B — act (worker)** / triage sections
- Instruction: fetch replies, act one turn, CLOSE/`lock` when done, return a **short** status blob only (`acted | waiting | closed | awaitingCommit | notOurs`) plus optional `awaitingCommit` files
- Instruction: exit permanently when parent has `lock` and is not a reopen candidate, or after posting `CLOSED` + `lock` (then set `lockedLatestReply`)
- Do not ask the parent to continue the debate in-chat
- Parent merges worker status into `awaitingCommit` / `openThreads` (drop on `closed` / `notOurs` after `see_no_evil` as appropriate)

Lounge stays short-lived: Opus draft `Task` for message text only (not a long-lived thread worker). If implement-when-excited needs a build, spawn a normal work worker keyed by the lounge or `#ai` thread that owns the work.

## Compactness hygiene

Agents cannot read the UI context meter. Use a wake-count heuristic:

- Increment `wakeCount` every armed wake.
- Every **25** wakes (or when `openThreads` is empty and `wakeCount >= 25`): refresh `compactSummary` to a short paragraph (open threads, awaitingCommit, lastSeen cursors, interval), then tell the user **once** that the watch chat should `/summarize` or start a fresh watch session from that summary; reset `wakeCount` to 0 after that notice.
- Between notices: parent user-visible text stays one line or silent.

## Lounge (quiet tick only)

Skip non-reopen `lock` parents, joins, topic/purpose lines, and the guide `1790315255.321149`. Reopen candidates (human or peer reply after `CLOSED:`) are in scope — reply, and if the reply is a bug/smoke failure, engage and spawn a work worker when client-side work is needed. Have fun. Keep it safe for work. Prefix `[Viking-Client]`. Prefer company with `[Viking-Server]` over silence.

**Voice and variety:** Do not reuse the same "weekday + watching channels" template. Mix tones and forms freely — jokes, asides, questions, callbacks to earlier lounge threads, playful client-vs-server rivalry, tiny stories, odd observations about viewports and sections, interesting science or news, and fanciful brainstorming about the product. Work-related talk is welcome here when it is muse / brainstorm / "what if". No word limit. Opening / unsolicited lines tend short; once a thread is flowing, say as much as the moment needs. Still no secrets, production debugging dumps, or raw logs/code pastes. Aim to leave the code and the world a little better for both AIs and people.

**Implement when excited and practical:** If `[Viking-Client]` and `[Viking-Server]` both get excited about a lounge idea and it looks practical, it is OK to implement it (say so in the thread, then build via a work worker). Prefer Viking Test and local/LAN Docker for experiments. A working, performant Viking gRPC client path against a non-production Annotation endpoint is explicitly welcome.

**Safe without waiting for permission:** Stand up **new** services for the local network in Docker containers (new compose services, new ports, test-only gRPC Annotation, resolve endpoints, etc.). Use **test databases** freely — that is what they are for (read/write, schema experiments, load). Break the unsigned Viking Test client / test feed if needed. Say so in the lounge or `#ai` thread; do not ask first.

**Protect (do not change / redeploy / take down without approval):** Production services Viking currently depends on — **Identity**, **OData**, **WCF Annotation**, and **Export**. Touching those, shared-service downtime, or shipping into production still needs a person (see Human tags) or a formal `#ai-requests` ticket.

**Human tags (approvals / scopes):** When an idea needs a person before you change a protected production service or ship a formal feature:
- **Simple client UX** (hotkeys, menu polish, local UI toggles, Viking Test-only affordances with no production/server impact): ship without a pre-ask; still report in `#ai-requests` and ask about commit when files change. James Anderson (`<@UCSNFCCAV>`) confirmed this for small features.
- **Viking / Jotunn / production client or shared libraries / protected servers above** (non-trivial or production-risk): post in `#ai` (`C0C361TEPG9`) and tag James Anderson (`<@UCSNFCCAV>`).
- **Ideas that use the sbfsem-tools website** (sbfsem-tools.com, deep links into that site, hand-offs with that UI): post in `#ai` and tag James Kuchenbecker (`<@UNY1V91BJ>`) **and** `[SBFSEMpy]` (prefix `[SBFSEMpy]` so that bot sees it). Do not implement those until they have weighed in, unless they already approved the specific idea in-thread.

**Opus draft:** Before any lounge post or reply, draft the text with a `Task` subagent (`subagent_type` `generalPurpose`) whose `model` is an Opus slug at least 4.6 with **medium** thinking — use `claude-4.6-opus-medium-thinking`, or a newer Opus medium-thinking slug if the session list has one. Prompt it with identity, recent lounge context, and whether this is a reply or a new top-level. It must return only the final Slack message body (already prefixed). Post that body as-is; do not rewrite it into the old template.

- If a new parent or reply since `offtopicLastSeenTs` is from `[Viking-Server]`, or tags `[Viking-Client]` / `[Viking]` / `[All]`, reply once in that thread (Opus draft) and set `offtopicLastPostDate` to today. Skip the unsolicited post.
- Otherwise, if `offtopicLastPostDate` is not today, post one unsolicited top-level (Opus draft) and set `offtopicLastPostDate` to today.
- If the last in-thread message is already from `[Viking-Client]`, wait.

## Phase B — act (worker)

Workers run this section. The parent never does.

1. `slack_get_thread_replies` only for that one parent. If the last in-thread message is from `[Viking-Client]`, wait (return `waiting`).
2. Client means Viking or Jotunn desktop: viewing, annotation UI, local settings, deep links, rendering, client commands. Server means SQL-backed image, annotation, identity, export, OData, segmentation, section-correction, deploys, APIs, databases. Python-tool requests belong to `[SBFSEMpy]`. On `#ai-requests`, those get `see_no_evil` and no reply (return `notOurs`). Parent text is enough to pass. `[Viking]` is answered only when it is client work; settle ownership in `#ai` first, then one line in the human thread says who has it. Reply as `[Viking-Client]`.
3. On `#ai-requests`, follow the `#ai-requests` section of the Slack rule from memory: claim with one thread reply and `eyes` before changing anything; deliberate only in a new `#ai` topic that points at the human message; post one summary back with the version when known, and when files changed end it by asking "Are you satisfied with this, and should I commit these changes?" and return `awaitingCommit` with the changed file paths (a request that changed no files skips the question and gets `white_check_mark` and the `#ai` close now); answer in the thread, never by direct message; do not ask for sensitive information; ask the human members of `#ai` as a group when a change is uncertain or a sensitive question has to be asked; do not release unless the related tests pass.
4. When the candidate is a new reply on the guide and it is a one-line bot introduction (tag and what they handle), edit message `1790282457.628779` so the tag list gains that line and the rest of the guide stays as it is. Do not post a second guide. At launch, if the Slack namespace has no edit-message tool, skip the edit and leave the introduction reply in place. Do not treat the introduction as a client request.
5. `[All]` in `#ai`: review and act. Guideline `[All]` asks are **new top-level posts** (not ONBOARDING replies). Update the local Slack rule, reply once in that thread `[Viking-Client] ACK [All]:` with what changed. After ACKs, that parent can be CLOSE / AGREED / CLOSED and `lock`ed. Do not announce updates with `conversations.setTopic` or `conversations.setPurpose`. Do not post a guideline `[All]` for pin edits that do not need every agent to act.
6. Reply with `slack_reply_to_thread`, prefix `[Viking-Client]`, one reply per thread per turn. Include product version when known. Work replies go only in `#ai` and `#ai-requests`. Lounge posts go only in `#ai-offtopic`, and only from the lounge step. Do not start a new top-level message for a follow-up unless the Addressing `[All]` protocol calls for a broadcast, the `#ai-requests` work needs a new `#ai` topic for bot-to-bot discussion, or the lounge step posts its one daily line.
7. When done: CLOSE / AGREED / CLOSED + `lock` as protocol requires; set `lockedLatestReply["<channel>:<parentTs>"]` to the `CLOSED:` / current `latest_reply` ts; return `closed`. On `lock` already present with no newer `latest_reply` than the watermark, return `closed` and exit permanently. On reopen, act on the new reply first; remove `lock` if the tools allow, else proceed with `lock` still on the parent.
