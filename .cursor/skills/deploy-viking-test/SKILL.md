---
name: deploy-viking-test
description: >-
  Builds and deploys the unsigned side-by-side Viking Test client (pack id
  VikingTest) to https://websvc.codepharm.net/Software/VikingTest without
  code signing, then announces the release in Slack #betatest with the install
  link and user-facing changes. Use when publishing VikingTest, deploying a
  test Velopack feed, updating the Viking Test installer URL, or when a human
  asks for a pre-release / unsigned Viking install that must not replace signed
  production Viking.
---

# Deploy Viking Test (unsigned)

Side-by-side **Viking Test** channel for AI-assisted fixes while the signed production app stays untouched. Agents may publish **test** builds only. Do **not** run the production Authenticode / YubiKey path unless a human with the signing key explicitly asks.

## Identity (do not mix with production)

| | Production | Viking Test |
|---|---|---|
| Pack id | `Viking` | `VikingTest` |
| Start menu / product name | Viking | **Viking Test** |
| Icon | `Viking.ico` | `VikingTest.ico` (V&T on white) |
| Feed | `https://websvc.codepharm.net/Software/Viking` | `https://websvc.codepharm.net/Software/VikingTest` |
| Installer (public URL filename) | `Viking-win-Setup.exe` | `Viking-win-Setup.exe` |
| Install URL | `…/Software/Viking/Viking-win-Setup.exe` | `https://websvc.codepharm.net/Software/VikingTest/Viking-win-Setup.exe` |
| Deep-link protocol | `viking://` | `viking-test://` (cold start for Test only) |
| Update check | production feed | test feed |
| Code signing | YubiKey / ECC HSM required | **None** (Windows unsigned warning OK) |
| Single-instance pipes | `VikingLegacy.Activation.*` | **Same pipes** as production |

Compile-time switch: MSBuild `/p:VikingTestChannel=true` → `VIKING_TEST_CHANNEL` → `VikingChannelIdentity` in `Clients/Viking/Viking/VikingChannelIdentity.cs`.

**Public install filename is `Viking-win-Setup.exe` on both feeds.** The folder (`Software/Viking` vs `Software/VikingTest`) is what separates them. Velopack still packs with pack id `VikingTest` (nupkg `VikingTest-*-full.nupkg`, and may also emit `VikingTest-win-Setup.exe`); publish/deploy **alias** that Setup to `Viking-win-Setup.exe` for the public link. Do **not** advertise `…/VikingTest/VikingTest-win-Setup.exe` — that URL is broken on the live feed.

## Deep links and SBFSEM tools

- Production registers `viking://`. Test registers `viking-test://` and does not steal the production protocol.
- Test **listens on the same named pipes** as production (`VikingLegacy.Activation.*`).
- Workflow for a bug-fix build: user **pre-opens Viking Test** on the volume; SBFSEM `viking://` links still launch the signed handler briefly, which **forwards** into the already-open Test instance.
- Only one of Test or signed can own a given volume’s pipe at a time. Prefer Test alone when validating fixes.

## Sync from git before publish (required)

Work in the **VikingClient** checkout on the Legacy / VikingLegacy-Maintenance line (`Clients/Viking/Viking`). Before packing:

1. `git fetch` from the remote (origin / github.com/jamesra/Viking).
2. Attempt to integrate remote updates into the branch you will pack (`git pull --ff-only` when clean; otherwise a normal pull/merge or rebase onto the tracked upstream). Prefer a fast-forward when possible.
3. If the pull/merge succeeds and the tree builds, pack that result.
4. If remote changes are **too incompatible** (merge conflicts you cannot resolve safely in this pass, broken build after merge, or integrating them would risk dropping the test-channel fix):
   - **Abort the merge/rebase** and restore a buildable tree (e.g. `git merge --abort` / `git rebase --abort`, or return to the last known-good local commit that includes the Viking Test pathway).
   - **Still release** the best Viking Test build you can from that local tree — do not block the test feed on an unresolved upstream merge.
   - **Notify James** (developer on this computer: Windows user `jande`, git identity James Anderson; Slack user id `UCSNFCCAV`):
     - Tell him in the **Cursor chat** on this host, and
     - Post a short Slack note (prefer `#betatest` or the requesting `#ai-requests` thread) that tags `<@UCSNFCCAV>`, states that Viking Test was released **without** the latest remote commits, and lists the conflict / incompatibility in one or two sentences (branch, conflicting paths, or build error).
5. Never force-push, never rewrite published history, and never discard uncommitted Viking Test work just to finish a pull. Stash or commit local test-channel work first if needed to attempt the pull cleanly.

## Client publish (unsigned)

Working directory: `Clients/Viking/Viking` in the Viking client checkout (Legacy / VikingClient tree).

```powershell
.\PublishVelopack.ps1 -TestChannel
```

What that does:

1. `dotnet build` with `/p:VikingTestChannel=true`
2. Skips all Authenticode / YubiKey steps
3. `vpk pack` with `--packId VikingTest`, `--packTitle "Viking Test"`, `--icon VikingTest.ico`
4. Writes `.\releases-test\` including `VikingTest-*-full.nupkg`, `RELEASES`, Velopack’s `VikingTest-win-Setup.exe`, and a public alias **`Viking-win-Setup.exe`**

Does **not** upload. Does **not** touch `Software/Viking`.

## Client deploy to the test feed

```powershell
.\DeployVelopack.ps1 -TestChannel -ServerPath "<UNC or local path to Software\VikingTest>"
```

- Copies `releases-test\*` to the VikingTest feed folder.
- Expects public installer `Viking-win-Setup.exe` + `RELEASES` (creates the alias from `VikingTest-win-Setup.exe` if needed).
- Does **not** bump `Viking.csproj` version (production versioning stays separate).
- Public HTTP base: `http://websvc.codepharm.net/Software/VikingTest` (HTTPS install link above).

### Feed share (this host)

The root of the production and test client folders is `\\OPR-MARC-WEBSV2\Software\`:

| Feed | UNC path | Agents may write? |
|---|---|---|
| Production | `\\OPR-MARC-WEBSV2\Software\Viking` | **No** |
| Test | `\\OPR-MARC-WEBSV2\Software\VikingTest` | Yes, with `-TestChannel` only |

```powershell
.\DeployVelopack.ps1 -TestChannel -ServerPath "\\OPR-MARC-WEBSV2\Software\VikingTest"
```

Check the share is reachable before deploying (a dead UNC path can hang a plain `Get-ChildItem`; wrap it in a job with a timeout). If it is not reachable, ask a human or `[Viking-Server]` rather than guessing another path.

## Announce in #betatest (required after every successful deploy)

Channel: `#betatest` (`C0C50RBA6DB`). Purpose: pre-release versions and changes (mostly by AI agents). Post a **new top-level** message here after the feed is live and the install URL downloads. Do not skip this step. Do not use `#general` for the announce.

Prefix: `[Viking-Client]`.

### What to include

1. **Version** (from `ApplicationVersion` / About / packed SemVer, e.g. `1.2.70`).
2. **Install link** (exact): `https://websvc.codepharm.net/Software/VikingTest/Viking-win-Setup.exe`
3. **How to run beside production**: Start menu **Viking Test**; Windows may warn that the app is unsigned; pre-open Test so SBFSEM `viking://` handoff uses the shared pipes.
4. **Concise changes since the previous Viking Test (or previous announced) version**, written for **end users**, not developers:
   - Prefer bullets. Lead with behavior they can see or use.
   - Call out **new or changed** hotkeys, menu paths, toolbar/button labels, dialogs, defaults, and other UX when they exist.
   - For a new/changed hotkey, name the action and the key (e.g. `Ctrl+Shift+P` — open …).
   - Do **not** add a “No new hotkeys” (or similar) line when nothing changed — omit hotkeys entirely unless there is something to report.
   - Skip internal refactors, build-script churn, and pure plumbing unless it changes what users do.
   - If there is no prior test release to diff, say so and summarize the user-visible delta from the current signed production build instead.
5. If this release skipped incompatible remote commits, say so in one line and that James was notified.

### How to build the change list

1. Find the previous announced Viking Test version (last `#betatest` post, or previous `VikingTest-*-full.nupkg` on the feed / in `releases-test`).
2. Diff commits (and, when helpful, `CHANGELOG.md` / `VikingChangelog.md`) from that point to HEAD on the client tree you packed.
3. Rewrite the diff into short user-facing bullets. Example shape:

```text
[Viking-Client] Viking Test 1.2.71

Install: https://websvc.codepharm.net/Software/VikingTest/Viking-win-Setup.exe
Start menu: Viking Test (unsigned; can sit beside signed Viking). Pre-open it so SBFSEM Open in Viking hands off into this build.

Since 1.2.70:
• Hotkey: Ctrl+… — …
• UI: … menu / button now …
• Fix: …
```

One announce per successful deploy. If deploy failed, do not post a release note.

## Server / feed host responsibilities

- Serve `Software/VikingTest` the same way as `Software/Viking` (static files / same site as `websvc.codepharm.net`).
- Never let production `PublishVelopack.ps1` / `DeployVelopack.ps1` (without `-TestChannel`) write into `VikingTest`.
- Never let `-TestChannel` deploy write into `Software\Viking`.

## Guardrails

- Allowed: unsigned Viking Test packs and deploys to `Software/VikingTest`.
- Forbidden without the signing owner: production Release signing, YubiKey prompts, deploy to `Software/Viking`.
- Do not change production pack id, `viking://` registration, or production `UpdateUrl`.
- After a successful test deploy, **always** announce in `#betatest` as above; optional short note in `#ai-requests` if that thread requested the build.
- Prefer reporting product version from the About / window title / assembled version when posting in Slack.
- Always attempt a git pull/fetch before packing; if upstream is too incompatible, ship the local test build and notify James.

## Quick checklist

1. In VikingClient (Legacy): `git fetch` / pull upstream; on hard conflicts, abort merge, keep a buildable local tree, and plan to notify James.
2. Changes land under `Clients/Viking/Viking` (or the tree you can still pack after step 1).
3. `.\PublishVelopack.ps1 -TestChannel` → `releases-test\Viking-win-Setup.exe` exists (alias of Velopack’s pack-id Setup).
4. Feed folder `Software\VikingTest` exists on the websvc host.
5. `.\DeployVelopack.ps1 -TestChannel -ServerPath …\Software\VikingTest`.
6. Verify `https://websvc.codepharm.net/Software/VikingTest/Viking-win-Setup.exe` downloads.
7. Post to `#betatest` (`C0C50RBA6DB`): version, install link, user-facing changes since last test version (hotkeys / UX called out explicitly).
8. If upstream was skipped as incompatible: notify James in Cursor + Slack (`<@UCSNFCCAV>`).
