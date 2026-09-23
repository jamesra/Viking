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

## Loop prompt

On every tick, follow this request verbatim:

```
/loop Every five minutes, check the #ai channel for requests related to the viking client.  if there is no question, add five minute to the next check until checking every 20 minutes during work hours and 60 minutes off hours.  If a question appears, reset to a five minute check.  Then address and respond to the question.  If a request is addressed to [Viking] with no clarification.  Assess if it is a server or client issue and address it if it is a client issue.
```

## Launch

1. Read `.cursor/rules/slack-ai-protocol.mdc` and follow it on every tick.
2. Read the `loop` skill. Use its **dynamic** local schedule (one-shot `AGENT_LOOP_WAKE_viking_client_ai`), because the wait changes. Do not arm a fixed five-minute loop.
3. Check this session's terminals for an existing `AGENT_LOOP_WAKE_viking_client_ai` sleeper. If one is already running here, do not start another.
4. Run one check immediately.
5. Arm the next wake for the interval this check selects. Put the interval in the wake payload:

```json
{"prompt":"Follow skill viking-client-ai-loop. Run one #ai check.","intervalMinutes":10}
```

6. On wake, read that payload, run one check, then arm the next wake. Stop only when the user asks: kill the sleeper PID and do not arm another wake.

Work hours are Monday–Friday 09:00–17:00 America/Los_Angeles. Every other time is off hours. The cap is the one in force when you arm the next wake.

## Schedule

The interval starts at 5 minutes.

- A check that finds no question sets the next wait to the current interval plus 5 minutes, capped at 20 minutes during work hours and 60 minutes off hours. If the current interval is already above the cap that applies now, the next wait is the cap.
- A check that finds a question resets the next wait to 5 minutes, then addresses and responds to that question.

The launch check counts. An empty launch check therefore sleeps 10 minutes. Later empty checks sleep 15, then 20 in work hours. Off hours the same steps continue through 25, 30, and so on, until 60.

## Each check

1. Discover the Slack tools with `GetDynamicTools` before calling them. Read `#ai` (`C0C361TEPG9`). The Slack channel list is public-only; use this id.
2. Read channel history. Skip a parent with the `lock` reaction and do not fetch its replies. That reaction means the thread ended `CLOSE:` / `AGREED` / `CLOSED: <summary>`. Otherwise read the target thread before speaking. If the last in-thread message is from `[Viking]`, wait.
3. Act on an unanswered request about the Viking client. Act on a request addressed to `[Viking]` with no client-or-server clarification only after you assess it: address it when it is a client issue. Leave a server issue unanswered by this loop. A message addressed to `[All]` is for every agent: review it and act. For an onboarding or guidelines change, update the local Slack rule to match, then reply once `[Viking] ACK [All]:` with what changed.
4. Client means the Viking or Jotunn desktop client: viewing, annotation UI, local settings, deep links into the client, rendering, and commands the user runs in the client. Server means the SQL-backed image, annotation, identity, export, OData, segmentation, and section-correction services, plus their deploys, APIs, and databases.
5. Reply in that thread with `slack_reply_to_thread`. Prefix every message with `[Viking]`. Do not post to `#general` or any other channel. Do not start a new top-level message for a follow-up. Include the product version when you know it.
6. One reply per thread per turn. Then wait for the other side.
