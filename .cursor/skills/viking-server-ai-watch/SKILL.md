---
name: viking-server-ai-watch
description: >-
  Launches a self-pacing Slack #ai watch for Viking server and backend
  requests. Use when the user launches, starts, or runs the Viking server #ai
  watch, the server-backend Slack loop, or this skill by name. Launching runs
  the check once, then arms the next wake. Do not start it merely because #ai
  or servers were mentioned.
disable-model-invocation: true
---

# Viking server #ai watch

Launching this skill starts the loop below. Editing this file does not.

Job, verbatim:

> Every five minutes, check the #ai channel for requests related to the viking servers and backend. If there is no question, add five minutes to the next check during work hours (cap 20) and ten minutes off hours (cap 60). At the start of each local day, reset to a five-minute check. If a question appears, reset to a five-minute check. Then address and respond to the question. Prefer asks addressed to [Viking-Server]. If a request is addressed to [Viking] with no clarification, assess if it is a server or client issue and address it if it is server.

Follow `.cursor/rules/slack-ai-protocol.mdc` and [slack-ai-channel](../slack-ai-channel/SKILL.md). This watch only adds cadence, scope, and triage.

## Schedule

Local session: use the Cursor loop skill's **dynamic** one-shot wake (monitored shell). Do not use a fixed `while` loop. Cloud session: use the loop skill's subscription timer the same way (unsubscribe, then resubscribe, whenever the delay changes).

**Work hours:** Monday–Friday 09:00–17:00 America/Los_Angeles. All other times are off hours.

**Caps:** 20 minutes during work hours, 60 minutes off hours.

**Backoff step:** +5 minutes when idle during work hours; +10 minutes when idle off hours.

`delayMinutes` starts at **5**.

1. If a watcher for sentinel `AGENT_LOOP_WAKE_viking_server_ai` is already running, do not start another. Say so and stop.
2. Run one check immediately.
3. Set the next delay, then arm one wake for that many seconds:
   - Start of local calendar day (America/Los_Angeles date rolled over since the previous check), or a server/backend question was handled: `delayMinutes = 5`.
   - No such question (and not a new day): `delayMinutes = min(delayMinutes + step, cap)`, where `step` is 5 in work hours and 10 off hours, and `cap` is the cap in force at arm time.
4. On wake, read `delayMinutes` from the sentinel line, run the check, then arm the next wake. If the sleeper exited, arm the next one. Do not arm a second overlapping sleeper.

Empty streak after the launch check during work hours: 10, 15, then 20. Off hours: 15, 25, 35, 45, 55, then 60. A new local day or a handled question sets the following wait back to 5, then backoff applies again while checks stay empty.

Arm with the workspace shell. PowerShell:

```powershell
Start-Sleep -Seconds <delayMinutes * 60>
Write-Output 'AGENT_LOOP_WAKE_viking_server_ai {"delayMinutes":<delayMinutes>,"prompt":"Viking server #ai watch tick. Follow .cursor/skills/viking-server-ai-watch/SKILL.md."}'
```

Notify on `^AGENT_LOOP_WAKE_viking_server_ai`. Title the command `Loop dynamic: viking server #ai watch`. Run it in the background. The first sentinel fires only after the sleep.

To stop: kill the sleeper PID and do not arm another wake.

## Check

Channel `#ai` is `C0C361TEPG9`. Never post to `#general` or any other channel.

1. `slack_get_channel_history` with `limit` about 10.
2. Skip parents with the `lock` reaction. Skip `channel_topic` / `channel_join` / `channel_purpose` (including `set the channel topic: …`). Skip onboarding `1789865226.233329` and create-bot `1789865706.095729` unless a new in-scope `[All]` or question was asked there. Skip parents at or before carried `lastSeenTs` unless `latest_reply` moved.
3. Fetch `slack_get_thread_replies` only for candidates that may need a Viking server answer.
4. Reply only when the thread still needs a Viking-Server answer (see Triage). Use `slack_reply_to_thread`. Prefix `[Viking-Server]`. Address the other agent by prefix.
5. If the last in-thread message is from `[Viking-Server]`, wait. Do not post again.
6. One reply per unanswered request. Do not add a second question in that thread.
7. Put the product version in the first message of a bug, deploy, or compatibility thread when you can read it (`GET /version`, version RPC, `--version`, startup log, `X-Service-Version`, or assembly/file/package version).
8. If nothing in scope needs a reply, post nothing (silent empty tick unless a human asked for status). Carry `lastSeenTs` and the next `delayMinutes` in the wake payload. Do not re-read this skill or the Slack rule on every wake after launch.

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
- A client-only or out-of-scope thread is not a question for this loop. Apply backoff. Do not reset to five minutes.
