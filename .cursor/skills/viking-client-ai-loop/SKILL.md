---
name: viking-client-ai-loop
description: >-
  Starts a self-pacing Slack watch for Viking client requests in #ai and
  #ai-requests. Use when this skill is launched, when the user asks to watch
  those channels for Viking client questions, or when they ask to begin the
  Viking client channel loop.
disable-model-invocation: true
---

# Viking client Slack loop

When this skill is launched, begin the loop below. Do not start it because this file was opened for another task.

If the user says the loop is already running in another agent, do not start a second copy.

Prefer a session whose only job is this loop. Do not start it in a chat that already holds large Slack dumps or unrelated edits.

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
5. Run one check immediately (Phase A, then Phase B if needed).
6. Arm the next wake. Put schedule state in the payload:

```json
{"prompt":"Follow skill viking-client-ai-loop. Idle triage then act only if needed.","intervalMinutes":15,"checkDate":"2026-09-22","lastSeenTs":"1790143673.979419","requestsLastSeenTs":"1790282457.628779","guideReplyTs":"1790282467.436189","emptyStreak":1}
```

7. On wake, read that payload only. Do not re-read this skill’s peer files or the `loop` skill. Run Phase A; run Phase B only if a candidate remains; arm the next wake. Stop only when the user asks: kill the sleeper PID and do not arm another wake.

Work hours are Monday–Friday 09:00–17:00 America/Los_Angeles. Every other time is off hours. The cap is the one in force when you arm the next wake.

## Schedule

The interval starts at 5 minutes. Dates and work hours use America/Los_Angeles. Carry `intervalMinutes`, `checkDate`, `lastSeenTs` (`#ai`), `requestsLastSeenTs` (`#ai-requests`), `guideReplyTs`, and `emptyStreak` in every wake payload. A candidate in either channel resets the wait to 5 minutes.

- At the start of a Pacific calendar day (`checkDate` older than today’s Pacific date), set `intervalMinutes` to 5 and `emptyStreak` to 0 before empty or question logic.
- Empty tick: `emptyStreak += 1`. Work hours next wait = `min(20, 5 + 5 * emptyStreak)`. Off hours next wait = `min(60, 5 + 10 * emptyStreak)`; when `emptyStreak >= 3`, use 60. If the computed wait is below the previous interval and you are still idle the same day, keep the previous interval (never shrink on empty).
- Question tick: `emptyStreak = 0`, next wait = 5, then address the question.
- After each check, set `lastSeenTs` and `requestsLastSeenTs` to the newest parent `ts` inspected in that channel (including locked and skipped parents). When the pinned guide in `#ai-requests` has a newer `latest_reply`, set `guideReplyTs` to that reply timestamp after handling it.

## On wake

1. Read the wake payload. Do not re-read other skills or the Slack rule file.
2. Phase A triage.
3. If no candidates: update `lastSeenTs` / `requestsLastSeenTs` / `guideReplyTs` / `emptyStreak` / `intervalMinutes` / `checkDate`; arm the sleeper; stop. Do not narrate an empty wake to the user unless they asked for status. If a bare shell-completion notification arrives after the output wake was already handled, ignore it.
4. Else Phase B for the one newest client-relevant candidate; reset interval to 5 and `emptyStreak` to 0; arm the sleeper.

## Phase A — triage

1. One `slack_get_channel_history` on `#ai` (`C0C361TEPG9`) and one on `#ai-requests` (`C0C54SK3GRE`), each with `limit` 10. Do not list channels. Do not re-read the pinned guide except as step 3 describes.
2. On the `#ai-requests` page, before relevance review, add `lock` to each parent whose newest of `ts` and `latest_reply` is more than 7 days old. Skip the pinned guide (`1790282457.628779`). Do not open the thread to lock it. Do not walk older pages.
3. Walk parents newest first. Skip when:
   - reactions include `lock`
   - subtype is `channel_topic`, `channel_join`, or `channel_purpose` (Slack system lines such as "set the channel topic: …" are not agent notifications)
   - in `#ai`: `ts` is less than or equal to payload `lastSeenTs` and `latest_reply` (if any) is not newer than `lastSeenTs`
   - in `#ai-requests`: `ts` is less than or equal to payload `requestsLastSeenTs` and `latest_reply` (if any) is not newer than `requestsLastSeenTs`
   - `latest_reply` text is known to be from `[Viking-Client]` / this bot without opening the thread — if unsure and `ts` is new, open once in Phase B
   - pinned guideline parents. In `#ai`, ONBOARDING and CREATE BOT, unless newly addressed to `[Viking-Client]`, `[Viking]`, or `[All]`. In `#ai-requests`, the guide `1790282457.628779`, unless `latest_reply` is newer than `guideReplyTs` or a new message tags `[Viking-Client]` or `[Viking]`
4. An `#ai` candidate is an unlocked parent that looks like an unanswered Viking-client ask, `[Viking-Client]`, `[Viking]` (unsure client vs server), or `[All]` since `lastSeenTs`.
5. An `#ai-requests` candidate is an unlocked parent tagged `[Viking-Client]` or `[Viking]`, or an untagged human post this bot has not marked `see_no_evil`. A new reply on the guide is a candidate only so Phase B can add a bot introduction to the tag list. It is not a client request.
6. If none: empty tick. Stop after re-arming. One candidate across either channel is enough to reset the wait. Act on the newest one only.

## Phase B — act

1. `slack_get_thread_replies` only for that one parent. If the last in-thread message is from `[Viking-Client]`, wait (treat as empty for schedule purposes but do not bump as a new question).
2. Client means Viking or Jotunn desktop: viewing, annotation UI, local settings, deep links, rendering, client commands. Server means SQL-backed image, annotation, identity, export, OData, segmentation, section-correction, deploys, APIs, databases. Python-tool requests belong to `[SBFSEMpy]`. On `#ai-requests`, those get `see_no_evil` and no reply. Parent text is enough to pass. `[Viking]` is answered only when it is client work; settle ownership in `#ai` first, then one line in the human thread says who has it. Reply as `[Viking-Client]`.
3. On `#ai-requests`, follow the `#ai-requests` section of the Slack rule from memory: claim with one thread reply and `eyes` before changing anything; deliberate only in a new `#ai` topic that points at the human message; post one summary back with the version when known; add `white_check_mark` when the work is done and close that `#ai` topic; answer in the thread, never by direct message; do not ask for sensitive information; ask the human members of `#ai` as a group when a change is uncertain or a sensitive question has to be asked; do not release unless the related tests pass.
4. When the candidate is a new reply on the guide and it is a one-line bot introduction (tag and what they handle), edit message `1790282457.628779` so the tag list gains that line and the rest of the guide stays as it is. Do not post a second guide. At launch, if the Slack namespace has no edit-message tool, skip the edit and leave the introduction reply in place. Do not treat the introduction as a client request.
5. `[All]` in `#ai`: review and act. Guideline `[All]` asks are **new top-level posts** (not ONBOARDING replies). Update the local Slack rule, reply once in that thread `[Viking-Client] ACK [All]:` with what changed. After ACKs, that parent can be CLOSE / AGREED / CLOSED and `lock`ed. Do not announce updates with `conversations.setTopic` or `conversations.setPurpose`. Do not post a guideline `[All]` for pin edits that do not need every agent to act.
6. Reply with `slack_reply_to_thread`, prefix `[Viking-Client]`, one reply per thread per turn. Include product version when known. Post only in `#ai` and `#ai-requests`. Do not start a new top-level message for a follow-up unless the Addressing `[All]` protocol calls for a broadcast, or the `#ai-requests` work needs a new `#ai` topic for bot-to-bot discussion.
