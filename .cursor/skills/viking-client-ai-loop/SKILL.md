---
name: viking-client-ai-loop
description: >-
  Starts a self-pacing Slack #ai watch for Viking client requests. Use when
  this skill is launched, when the user asks to watch #ai for Viking client
  questions, or when they ask to begin the Viking client channel loop.
disable-model-invocation: true
---

# Viking client #ai loop

When this skill is launched, begin the loop below. Do not start it because this file was opened for another task.

If the user says the loop is already running in another agent, do not start a second copy.

Prefer a session whose only job is this loop. Do not start it in a chat that already holds large Slack dumps or unrelated edits.

## Loop prompt

On every tick, follow this request verbatim:

```
/loop Every five minutes, check the #ai channel for requests related to the viking client.  if there is no question, add five minutes during work hours or ten minutes off hours to the next check until checking every 20 minutes during work hours and 60 minutes off hours.  At the start of a day, reset to a five minute check.  If a question appears, reset to a five minute check.  Then address and respond to the question.  If a request is addressed to [Viking] with no clarification.  Assess if it is a server or client issue and address it if it is a client issue.
```

## Launch

1. Read `.cursor/rules/slack-ai-protocol.mdc` once at launch (not on every wake). Follow it afterward from memory.
2. Read the `loop` skill once at launch. Use its **dynamic** local schedule (one-shot `AGENT_LOOP_WAKE_viking_client_ai`). Do not arm a fixed five-minute loop.
3. Check this session's terminals for an existing `AGENT_LOOP_WAKE_viking_client_ai` sleeper. If one is already running here, do not start another.
4. Discover Slack tools with `GetDynamicTools` once at launch. On later wakes reuse the known tool names unless a call fails.
5. Run one check immediately (Phase A, then Phase B if needed).
6. Arm the next wake. Put schedule state in the payload:

```json
{"prompt":"Follow skill viking-client-ai-loop. Idle triage then act only if needed.","intervalMinutes":15,"checkDate":"2026-09-22","lastSeenTs":"1790143673.979419","emptyStreak":1}
```

7. On wake, read that payload only. Do not re-read this skill’s peer files or the `loop` skill. Run Phase A; run Phase B only if a candidate remains; arm the next wake. Stop only when the user asks: kill the sleeper PID and do not arm another wake.

Work hours are Monday–Friday 09:00–17:00 America/Los_Angeles. Every other time is off hours. The cap is the one in force when you arm the next wake.

## Schedule

The interval starts at 5 minutes. Dates and work hours use America/Los_Angeles. Carry `intervalMinutes`, `checkDate`, `lastSeenTs`, and `emptyStreak` in every wake payload.

- At the start of a Pacific calendar day (`checkDate` older than today’s Pacific date), set `intervalMinutes` to 5 and `emptyStreak` to 0 before empty or question logic.
- Empty tick: `emptyStreak += 1`. Work hours next wait = `min(20, 5 + 5 * emptyStreak)`. Off hours next wait = `min(60, 5 + 10 * emptyStreak)`; when `emptyStreak >= 3`, use 60. If the computed wait is below the previous interval and you are still idle the same day, keep the previous interval (never shrink on empty).
- Question tick: `emptyStreak = 0`, next wait = 5, then address the question.
- After each check, set `lastSeenTs` to the newest parent `ts` inspected in channel history (including locked and skipped parents).

## On wake

1. Read the wake payload. Do not re-read other skills or the Slack rule file.
2. Phase A triage.
3. If no candidates: update `lastSeenTs` / `emptyStreak` / `intervalMinutes` / `checkDate`; arm the sleeper; stop. Do not narrate an empty wake to the user unless they asked for status. If a bare shell-completion notification arrives after the output wake was already handled, ignore it.
4. Else Phase B for the one newest client-relevant candidate; reset interval to 5 and `emptyStreak` to 0; arm the sleeper.

## Phase A — triage

1. One `slack_get_channel_history` on `#ai` (`C0C361TEPG9`) with `limit` 10. Do not list channels.
2. Walk parents newest first. Skip when:
   - reactions include `lock`
   - subtype is `channel_topic`, `channel_join`, or `channel_purpose` (Slack system lines such as "set the channel topic: …" are not agent notifications)
   - `ts` is less than or equal to payload `lastSeenTs` and `latest_reply` (if any) is not newer than `lastSeenTs`
   - `latest_reply` text is known to be from `[Viking]` / this bot without opening the thread (history fields / reply_users pointing only at this bot after your last turn) — if unsure and `ts` is new, open once in Phase B
   - pinned guideline parents (ONBOARDING, CREATE BOT) unless newly addressed to `[Viking]` or `[All]` since `lastSeenTs`
3. A candidate is an unlocked parent that looks like an unanswered Viking-client ask, `[Viking]` with no client/server split, or `[All]` since `lastSeenTs`.
4. If none: empty tick. Stop after re-arming.

## Phase B — act

1. `slack_get_thread_replies` only for that one parent. If the last in-thread message is from `[Viking]`, wait (treat as empty for schedule purposes but do not bump as a new question).
2. Client means Viking or Jotunn desktop: viewing, annotation UI, local settings, deep links, rendering, client commands. Server means SQL-backed image, annotation, identity, export, OData, segmentation, section-correction, deploys, APIs, databases — leave those unanswered by this loop.
3. `[All]`: review and act. For onboarding or guidelines, update the local Slack rule, reply once `[Viking] ACK [All]:` with what changed. Do not announce updates with `conversations.setTopic` or `conversations.setPurpose` — those only produce `channel_topic` / `channel_purpose` system lines, not an agent ask.
4. Reply with `slack_reply_to_thread`, prefix `[Viking]`, one reply per thread per turn. Include product version when known. Do not post to other channels. Do not start a new top-level message for a follow-up.
