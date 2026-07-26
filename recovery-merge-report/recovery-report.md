# Recovered-Code Merge Report

Generated for the merge of the corrupted recovered tree into the current `dev` tree.

## Environment
- Current tree: `D:\src\git\Viking` on branch **`recovery-merge`** (created off `dev` @ `6eae0f00`).
- Recovered tree: `D:\src\old_git\git\Viking` (HEAD `bb0ff1c5`, an ancestor of `dev`; its git DB is corrupt).
- 3-way merge base: `D:\src\viking-base` (worktree @ `bb0ff1c5`, intact in current repo).
- Tooling, manifests, backups, logs: `D:\src\recovery-merge-tools\`.
- Decisions applied: bring over the **entire repo**; **recovered wins true conflicts**.

## What was done
Every recovered file was classified by a scripted 3-way comparison (base vs recovered vs current),
ignoring `.git`, `bin`, `obj`, `Packages`, `Publish`, `MigrationBackup`, `DataProtectionKeys`, logs,
build/publish output, and submodule internals.

### Files brought in
| Action | Count | Meaning |
|---|---|---|
| ADD (trackable) | 34 | New recovered source files that git will track (IdentityServer Services layer, EF migrations, models, test startups, etc.). |
| ADD (restored to disk, git-ignored) | 240 | New recovered files under intentionally-ignored dirs: `src\` (105), `Triangle.NET\` (98), `Clients\` (36), `Servers\` (1). Preserved on disk, not tracked. |
| OVERWRITE (recovered-only change) | 265 | Tracked files the recovered tree changed but current did not -> took recovered. |
| OVERWRITE (prefer-recovered conflict) | 12 | Files both sides changed -> recovered won per your choice (see list below). |
| FLAG (no auto-restore) | 1 | `Servers\IdentityServer\Dockerfile` (see "Needs your decision"). |

Full action list: `applied-actions.csv`. Skipped noise list: `skipped-noise.csv` (2831 submodule internals + 171 build-output files).

### Corruption recovery (1359 corrupt recovered files total)
- **250 tracked corrupt files**: current already held clean copies -> current copies retained. If any of these had *uncommitted* edits in the recovered tree, those edits were in the corrupted bytes and are **unrecoverable**. Full list: filter `manifest_clean.csv` for `Class=CORRUPT_TRACKED`.
- **Reconstructed by inference** (flagged):
  - `Servers\IdentityServer\Identity.Configuration\VikingIdentityServerOptions.cs` - recovered version was corrupt but the applied recovered code depends on it. Re-added `RoVikingSecret` and `ApiScopeNames` (recovered from salvageable bytes + usage in `Config.cs`, `IdentityServerVikingClientStore.cs`, `VikingLaunchController.cs`). **Build-critical; verified.**
  - `src\gRPCAnnotationServiceCore.Client\Location.cs` - reconstructed from intact siblings + parallel `AnnotationService.WcfProxy\Location.cs` + `Location.Conversions.cs`. (git-ignored `src\` tree.)
- **Reconstructed from clean copies**: 4 `.fx` shaders copied from canonical `Clients\Content\VikingXNAGraphics\` versions.
- **Stub (unrecoverable)**: `Triangle.NET\TestApp\FormExport.resx` written as an empty valid resx (no clean source; `TestApp` is git-ignored).
- **Skipped corrupt noise (~15)**: NuGet doc XML (`Microsoft.OData.*.xml`, `Newtonsoft.Json.xml`, `log4net.xml`, `*.CodeAnalysisLog.xml`) and publish output under `Servers\Deployment\Release\Export\` and `Clients\Viking\...\Modules\`.

Corruption action log: `corruption-actions.csv`.

### Validation
- `Servers\IdentityServer\IdentityServer.sln` -> **Build succeeded, 0 errors** after the `VikingIdentityServerOptions` reconstruction (logs: `identityserver-build.log`, `identityserver-build2.log`).
- Other solutions (net48 Clients, the git-ignored `src\` tree, ConnectomeDataModelCore, etc.) were **not** build-verified.

## Needs your decision / attention

### 1. Recent tracked work reverted by "prefer recovered" (11 CONFLICT_BOTH + 1 CONFLICT_NEW)
Recovered (older) versions overwrote current. Originals saved in `backup-current\conflict_both\` and `...\conflict_new\`:
- `Servers\IdentityServer\Viking.Identity.Server.WebApi\Controllers\PermissionsController.cs` (your `UserAccessibleVolumeTree` endpoint was here)
- `Servers\IdentityServer\Viking.Identity.Server.WebManagement\Program.cs`
- `Servers\IdentityServer\IdentityServerStandalone\Program.cs`
- `Servers\IdentityServer\Viking.Identity.Server.WebManagement\appsettings.Docker.json`
- `Servers\IdentityServer\Viking.Identity.Server.WebManagement\Controllers\ApplicationUsersController.cs`
- `Servers\IdentityServer\docker-compose-all.yml`
- `Servers\IdentityServer\.env`, `.env.All`, `.env.All.Docker`, `.env.Docker`, `env.example`
- `Servers\IdentityServer\Viking.Identity.Server.WebManagement\secrets.json` (secret; git-ignored)

### 2. Kept current's newer commits (recovered did not change these) - 9 files (`MOD_CURRENT_ONLY`)
Not reverted (recovered was identical to the ancestor). Force-recovered only if you want:
`docker-compose.yml`, `README.rst`, `README-Docker-All.md`, `restart_omni.cmd`, `restart_omni_debug.cmd`,
`Views\ApplicationUsers\Details.cshtml`, `Views\ApplicationUsers\Index.cshtml`, `Views\Home\Index.cshtml`, `wwwroot\css\site-modern.css`.

### 3. Delete-vs-modify: `Servers\IdentityServer\Dockerfile`
Current deliberately removed it (relocated to `Servers\IdentityServer.Dockerfile`); recovered had edits. **Not auto-restored.** Recovered copy saved in `review-deleted-in-current\`.

### 4. `src\` + `Triangle.NET\TestApp\` restored but git-ignored
The `src\` gRPC reorg (~105 files) and `Triangle.NET\TestApp` (~60) are restored to disk but excluded by `.gitignore` (`/src/`, `Triangle.NET/.gitignore`) and not referenced by any tracked `.sln`. Decide whether to start tracking them.

### 5. Secrets restored to disk (git-ignored, will NOT be committed)
`certs\star_codepharm.pfx`, `Viking.Identity.Server.WebManagement\secrets.json`. Also the **tracked** `.env` (contains a DB password) was overwritten with recovered content - consider untracking it.

### 6. Files current has that recovered lost - 46 (`REC_MISSING`)
Kept current versions (recovered was missing them, likely a recovery gap, not an intentional delete). List: filter `manifest_clean.csv` for `Class=REC_MISSING`.

## Nothing has been committed
All changes are working-tree only on `recovery-merge`. `dev` is untouched. Review, then commit or discard.
To discard everything: `git checkout dev` then `git branch -D recovery-merge` and `git worktree remove D:\src\viking-base`.
