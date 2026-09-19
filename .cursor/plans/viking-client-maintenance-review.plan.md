---
name: Viking client maintenance review
overview: "Evidence-based maintenance review of the Viking client and its non-Geometry dependencies (2026-09-19). Ten ownership chunks were reviewed in parallel. This plan is one ranked list: safest and highest-impact first, riskiest last."
todos:
  - id: settings-upgrade
    content: Fix Settings upgrade gate so Velopack updates do not skip Upgrade() (rank 1)
    status: completed
  - id: terminal-save
    content: Persist ToggleLocationIsTerminalCommand instead of constructing an unstarted Task (rank 2)
    status: completed
  - id: command-queue-cast
    content: Stop CommandQueue.Push from casting constructor entries to CommandQueueEntry (rank 3)
    status: completed
  - id: region-refresh
    content: Use TimeSpan.TotalSeconds for RegionLoader refresh (rank 4)
    status: completed
  - id: mosaic-tile-y
    content: Use TileSizeY for mosaic ScaledTileSizeY (rank 5)
    status: completed
  - id: circle-ib-reset
    content: Rebuild unit-circle index buffer after device reset (rank 6)
    status: completed
  - id: alpha-basiceffect
    content: Cache SolidPolygonView Alpha BasicEffect per device (rank 7)
    status: completed
  - id: hide-bookmarks
    content: Honor BookmarksVisible in BookmarkOverlay Draw/hit-test (rank 9)
    status: completed
  - id: do-not-yet-risky
    content: Defer risky items (auto-polygonize transaction, overlay GetResult, OAuth/gRPC, device recreate) until the safe list lands
    status: pending
isProject: true
---

# Viking Client Maintenance Review

**Date:** 2026-09-19  
**Status:** Safe ranks 1–22 implemented as individual commits on `Legacy` (see table below). Moderate/Risky ranks 23–53 remain review-only.  
**Solution:** `Clients/Viking.sln`  
**Entry:** `Clients/Viking/Viking/Viking.csproj` (WinExe, net48)

## Safe ranks — cherry-pick table

Each Safe item is its own commit so later cherry-picks stay small. Rank 10 landed earlier.

| Rank | Commit | Notes |
|------|--------|--------|
| 1 | `087e19e2` | `UpgradeRequired` on Settings partial; no `Reload()` |
| 2 | `a1543a75` | Sync save + FaultException rollback |
| 3 | `3504a090` | `ICommandQueueEntry` rebuild; also unhooks pen button handlers (rank 18 unsubscribe) |
| 4 | `66460820` | `RegionRefreshTiming` + 181s/30s tests |
| 5 | `820b90d4` | `ScaledTileExtent(TileSizeY)` + metrics tests |
| 6 | `3c55b61d` | Index-buffer Remove-then-Add; clear IB/VB/`BasicEffect` caches on reset |
| 7 | `bfcc5452` | Alpha path uses `CircleView.GetOrCreateBasicEffect` |
| 8 | `411b5c4f` | Null `AnnotationSet` / null locs |
| 9 | `b25b555a` | `BookmarksVisible` gates draw and hit-test |
| 10 | `562f0a82` | Multi-file live bookmark documents (prior plan) |
| 11 | `e4a1455f` | `TrySetTarget` returns `result`; deactivate when invalid |
| 12 | `7d7c1d79` | `VolumePath` HTTP `/`; cache still `Path.Combine` |
| 13 | `52c64285` | `return` after clearing OverlappedLinks; adjacent-circle parent-null also in this commit |
| 14 | `8a0488b7` | `BeginGetLocationLinksForSection` |
| 15 | `7492aa45` | Line / overlapped-location parent-null (circles were in 13) |
| 16 | `c6c8fd3b` | `LocationAction.REMOVEHOLE` |
| 17 | `96267723` | `ResizeCircleCommand` and `PlacePolylineCommand` `base.OnMouseUp` |
| 18 | `ca3d70fe` | Barrel `PenFlags` fires Down/Up (unsubscribe shipped in rank 3) |
| 19 | `531dc641` | `_IsLocal = true`; `GridCoordFormat` in tile names |
| 20 | *(with 4, 5, 12)* | Region/tile/URL tests in WebAnnotationTests. `CopyModules` rewrite deferred |
| 21 | `e83ebce1` | MathNet, CLI login prefill, synchronized log writer, measure help |
| 22 | `04c863d6` | `IInitEffect : IDisposable` + `EffectManagerLifetime`; shaders not merged |

Do not cherry-pick Geometry / auto-polygonize dirt with these. Moderate items start at rank 23.

---

## Scope

In: Viking.exe, VikingCore (NGVV), WebAnnotation (UI + tests), WebAnnotationModel, VikingXNAGraphics / `MonogameXNAGraphicsShared` / VikingXNAWinForms, Viking.UI.WPF, WebAnnotation WPF/ViewModels, VolumeModel, Common, Utils, UnitsAndScale, WCFTokenInjector, AnnotationServiceTypes, AnnotationInterfaces, SegmentationServiceTypes.gRPC (client consumption), LocalBookmarks, Measurement, RTree, SqlGeometryUtils, SqlGeometryAnnotationExtensions.

Out: **Geometry** (project, tests, DLL). Other clients (Jotunn, Bifrost, VikingAU, testbeds). Servers except client-side contracts. AnnotationVizLib / GraphLib / OData (in the `.sln` but not on the Viking.exe reference path). `obj/`, `bin/`, generated WCF `Reference.cs`, `.xnb`, untracked `Modules/` copies.

Chunk map (in/out and ProjectReference notes): [viking-client-maintenance-review.chunk-map.md](viking-client-maintenance-review.chunk-map.md)

## Methodology

Chunks were cut from **real ProjectReference graphs**, not folder guesses. Geometry was excluded even where referenced. Ten sibling reviewers read production `.cs` first, then tests, and returned evidence-backed findings only. This document **dedupes** those lists into one ranking:

1. Primary sort: **Safe → Moderate → Risky**
2. Secondary sort within a risk band: **High → Med → Low** impact on maintenance / user-visible correctness
3. True duplicates across chunks were merged; related but distinct defects stay separate

All ten chunks completed (`turn_ended` success). No chunk is missing.

---

## Master ranked list

### 1. Settings upgrade never runs after a version bump

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** App shell — `Clients/Viking/Viking/Services/SettingsManager.cs` (`UpgradeSettingsIfNeeded`); `Properties/Settings.Designer.cs` (`VolumeURLs` default); `Properties/Settings.settings`
- **Evidence:** Upgrade is skipped when `VolumeURLs != null && Count > 0`. Designer default is a one-item collection (`http://connectomes.utah.edu/Rabbit`). A new Velopack `user.config` is empty, so the default applies and `Upgrade()` never runs.
- **Recommendation:** Dedicated `UpgradeRequired` (default true) / `Upgrade()` once per version. Do not treat a non-empty default `VolumeURLs` as “already migrated.”
- **Why this rank:** One boolean/gate change; recovers recents, identity, and launch-exchange settings on every update.
- **Done:** `087e19e2` — `UpgradeRequired` on the Settings partial (no `Reload()`); `UpgradeSettingsIfNeeded` upgrades once then Save.

### 2. Terminal toggle never saves

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation commands — `UI/Commands/ToggleLocationIsTerminalCommand.cs`
- **Evidence:** `Execute()` flips `target.Terminal` then `new Task(() => SaveLocationsWithMessageBoxOnError())` and never `Start()`s it. `ToggleTagCommand` in the same folder saves synchronously.
- **Recommendation:** Call the save helper on the UI path; roll back `Terminal` on `FaultException`. Delete the unused `Task`.
- **Why this rank:** Local one-liner; annotators already think the flag persisted.
- **Done:** `a1543a75` — `Store.Locations.Save()` with FaultException rollback, matching `ToggleTagCommand`.

### 3. CommandQueue.Push throws when constructor entries are already queued

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** VikingCore — `NGVV/UI/Command/Command.cs`; `SectionViewerControl.EnqueueCommand`
- **Evidence:** Queue stores `CommandConstructorQueueEntry`. `Push` does `Select(v => (CommandQueueEntry)v)`. Inject-after-enqueue (polyline adjust, etc.) hits `InvalidCastException`.
- **Recommendation:** Rebuild as `ICommandQueueEntry` without the struct cast.
- **Why this rank:** Isolated cast; crashes the live command path.
- **Done:** `3504a090` — rebuild as `ICommandQueueEntry`. Also unsubscribes `OnPenButtonDown/Up` (rank 18).

### 4. Region refresh never fires (`TimeSpan.Seconds` vs 180)

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Clients/WebAnnotationModel/RegionLoader/RegionLoader.cs`
- **Evidence:** `RegionIsDueForRefresh` uses `TimeSpan.FromTicks(...).Seconds > 180`. `.Seconds` is the 0–59 component, never `> 180`. After the first `SetQuery`, refresh is dead.
- **Recommendation:** Compare `TotalSeconds` Unit-test 181s vs 30s.
- **Why this rank:** One property name; stale annotations on any parked FOV.
- **Done:** `66460820` — `RegionRefreshTiming.IsIntervalElapsed` uses `TotalSeconds`; tests at 30s and 181s.

### 5. Mosaic tile grid uses `TileSizeX` for Y spacing

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Volume — `Clients/VolumeModel/TileGridMappingBase.cs` (`RecursiveVisibleTiles`)
- **Evidence:** `ScaledTileSizeY = this.TileSizeX * roundedDownsample`. Volume mappings correctly use `TileSizeY`. Non-square tiles select the wrong Y rows.
- **Recommendation:** Use `TileSizeY`. Test `TileXDim != TileYDim`.
- **Why this rank:** One identifier; mosaic vs volume already disagree.
- **Done:** `820b90d4` — `ScaledTileExtent(TileSizeY, downsample)`; `TileGridMetricsTests`.

### 6. Unit-circle index buffer cache crashes after device reset

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Graphics — `MonogameXNAGraphicsShared/Views/Global.cs`; callers `CircleView`, `RectangleView`, `TextureOverlayView`; `GraphicsDeviceService.ResetDevice`
- **Evidence:** `GetUnitCircleIndexBuffer` `Add`s under the same device key when `ib.IsDisposed`. Vertex-buffer helper correctly `Remove`s first. First window grow past the backbuffer can `ArgumentException`.
- **Recommendation:** Mirror the vertex-buffer path; clear both dictionaries in `ClearDeviceDependentCaches`.
- **Why this rank:** Same pattern already exists next door; first resize after grow is a crash.
- **Done:** `3c55b61d` — Remove-then-Add for the index buffer; `GlobalPrimitives`/`CircleView` caches cleared in `GraphicsDeviceService`.

### 7. Alpha polygon draw allocates an undisposed `BasicEffect` every frame

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Graphics — `Views/SolidPolygonView.cs`, `Views/MeshView.cs`
- **Evidence:** Alpha path `new BasicEffect(device)` every paint; Luma uses `DeviceEffectsStore`. `CircleView` already caches per device.
- **Recommendation:** Cache one `BasicEffect` per device; dispose/recreate on reset.
- **Why this rank:** Copy an existing helper; stops a per-frame GPU leak.
- **Done:** `bfcc5452` — Alpha `MeshView`/`SolidPolygonView` use `CircleView.GetOrCreateBasicEffect`.

### 8. Region query failure NRE in annotation parse

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Store/LocationStore.cs`, `Store/StructureStore.cs`
- **Evidence:** Failed WCF leaves `serverAnnotations` null then `ProcessAnnotationSet` dereferences `.Structures`. Structure-by-region callback does `locs.Select(l => l.Parent)` when `locs` is null.
- **Recommendation:** Null-guard both; cancelled region load should empty the canvas, not crash.
- **Why this rank:** Two null checks; dropped connection currently can take down the load path.
- **Done:** `411b5c4f` — empty inventory if `serverAnnotations` is null; structure callback skips null locs/parents.

### 9. Hide Bookmarks never hides bookmarks

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Modules — `LocalBookmarks/BookmarkMenu.cs`, `Global.cs`, `BookmarkOverlay.cs`
- **Evidence:** `Global.BookmarksVisible` is toggled and used only by the menu. `Draw` / `ObjectAtPosition` ignore it. Measurement already gates on `ShowScaleBar`.
- **Recommendation:** Gate draw and hit-test on `BookmarksVisible`.
- **Why this rank:** Two boolean checks; menu already invalidates the viewer.
- **Done:** `b25b555a` — `Draw` and `ObjectAtPosition` return when `!BookmarksVisible`.

### 10. Bookmark XML import overwrites the source file - Already Done in another plan -

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Modules — `LocalBookmarks/FolderUIObj.cs`, `FolderTreeControl.cs`
- **Evidence:** After import, `OnImportXML` `Save`s back to `fileDialog.FileName`. Drag-drop uses `GetData(typeof(string))` for `FileDrop` (`string[]`).
- **Recommendation:** Merge in memory and `Global.Save()` to the volume bookmark path. Use `DataFormats.FileDrop`.
- **Why this rank:** Import can destroy the user’s backup; drag-drop is already dead.
- **Done:** `562f0a82` — live extra bookmark documents; FileDrop `string[]`; Local first and undeletable.

### 11. Link command ignores its own target validation

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation commands — `UI/Commands/LinkAnnotationsCommand.cs`
- **Evidence:** `TrySetTarget` computes `result` then `return nearest_target`. Invalid hover stays; mouse-up neither links nor deactivates.
- **Recommendation:** `return result;`. Deactivate on mouse-up when no valid link was created.
- **Why this rank:** One return; tool looks stuck until Escape.
- **Done:** `e4a1455f` — `return result`; mouse-up deactivates when no valid link.

### 12. Windows `\` inserted into HTTP tile/stos URLs

- **Risk:** Safe · **Impact:** High · **Category:** Bug
- **Chunks / files:** Volume — `Volume.cs` (`LoadStos`), `TileGridMapping.cs`, `FixedTileCountMapping.cs`, `TilesToSectionMapping.cs`
- **Evidence:** `Host + Path.DirectorySeparatorChar + stosFileName` is `\` on Windows. Mosaic paths already use `/`.
- **Recommendation:** Build remote paths with `'/'` or `Uri`; keep `Path.Combine` for local cache only.
- **Why this rank:** String join change; stos/tile HTTP can 404 only on Windows.
- **Done:** `7d7c1d79` — `VolumePath.Combine`/`JoinRelative`; `VolumePathTests`. Local cache still uses `Path.Combine`.

### 13. OverlappedLinks setter never clears (missing `return`)

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation overlay — `View/LocationCircleView.cs`, `LocationPolygonView.cs`, `LocationClosedCurveView.cs`
- **Evidence:** Empty-value branch nulls the view then immediately constructs a new `OverlappedLinkCircleView`. `Distance` then `Min`s an empty set.
- **Recommendation:** `return` after nulling; test add-then-remove overlap.
- **Why this rank:** Three identical setters; clear path is dead code.
- **Done:** `52c64285` — `return` after nulling. Adjacent-circle parent-null color is in this commit too (rank 15 file overlap).

### 14. LocationLink async Begin/End methods do not match

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Store/LocationLinkStore.cs`
- **Evidence:** `ProxyBeginGetBySection` calls `BeginGetLocationChanges`; callback `EndGetLocationLinksForSection`. Copy-paste from `LocationStore`.
- **Recommendation:** Begin `GetLocationLinksForSection`.
- **Why this rank:** One method name; WCF contract mismatch if that path is used.
- **Done:** `8a0488b7` — `BeginGetLocationLinksForSection`.

### 15. Adjacent / overlap views NRE when `Parent` is null

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation overlay — `View/LocationCircleView.cs`, `LocationLineView.cs`, `OverlappedLocationView.cs`
- **Evidence:** Current-section circles use gray when `Parent` is null. Adjacent/overlap views use `modelObj.Parent.Type.Color` with no guard.
- **Recommendation:** Same null-parent skip as `LocationCircleView`.
- **Why this rank:** Parentless locations already work on Z; Z±1 throws.
- **Done:** `7492aa45` — line and overlapped-location views. Adjacent circles shipped in rank 13.

### 16. Remove-hole action is typed as cut-hole

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation commands — `UI/Actions/RemoveHoleAction.cs`, `RemoveHoleActionView.cs`
- **Evidence:** `Type => LocationAction.CUTHOLE` while the enum has `REMOVEHOLE`. Icons disagree (Plus vs Minus).
- **Recommendation:** Set `Type` to `REMOVEHOLE`. One icon source.
- **Why this rank:** Enum constant; any `switch (action.Type)` conflates the two tools.
- **Done:** `c6c8fd3b` — `Type => LocationAction.REMOVEHOLE`.

### 17. Resize-circle mouse-up calls mouse-down

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation commands — `UI/Commands/ResizeCircleCommand.cs`
- **Evidence:** `OnMouseUp` then `base.OnMouseDown`. Can start the default command or step sections on XButtons.
- **Recommendation:** Call `base.OnMouseUp`. Audit the folder for the same mix-up.
- **Why this rank:** One method name.
- **Done:** `96267723` — `ResizeCircleCommand` and the same mix-up in `PlacePolylineCommand.OnMouseUp`.

### 18. Pen barrel-button path never fires and leaks handlers

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** VikingCore — `NGVV/UI/PenEventManager.cs`, `UI/Command/Command.cs`
- **Evidence:** `FireOnPenButtonDown/Up` are never called. `SubscribeToInterfaceEvents` hooks them; `Unsubscribe` does not.
- **Recommendation:** Detect barrel in `UpdatePenState`; unsubscribe both.
- **Why this rank:** Wire existing events; unsubscribe is required even before firing is fixed.
- **Done:** `ca3d70fe` — barrel Down/Up from `PenFlags.Barrel`. Unsubscribe shipped in rank 3 (`3504a090`).

### 19. `Volume.IsLocal` is never true; `CoordFormat` unused

- **Risk:** Safe · **Impact:** Med · **Category:** Bug
- **Chunks / files:** Volume — `Volume.cs` ctor; `TileGridMappingBase.cs` / `TileGridMapping.cs`
- **Evidence:** Local path branch sets `_IsLocal = false`. `GridCoordFormat` is parsed then ignored (`{iX:D3}` hardcoded).
- **Recommendation:** Set `_IsLocal` for file URIs; format names with `GridCoordFormat`.
- **Why this rank:** Local volumes still take the HTTP texture path.
- **Done:** `531dc641` — `_IsLocal = true` for file URIs; tile file names use `GridCoordFormat`.

### 20. Client tests miss the shell and overlay; module copy is order-dependent

- **Risk:** Safe · **Impact:** Med · **Category:** Maintenance
- **Chunks / files:** App shell — `Viking.csproj` (`CopyModules`, `CopyGraphicsContent`); `VikingTests` (net9.0-windows, no exe reference). Overlay — `WebAnnotationTests` covers commands/auto-polygonize/deep links, not overlay/cache.
- **Evidence:** Modules copy twice then delete duplicate DLLs. Content is globbed from `VikingXNAGraphics\bin` with no project reference. Empty test stubs.
- **Recommendation:** One copy into `bin\Modules`. Depend on graphics content. Point tests at net48 helpers (upgrade, pipe ACK, OverlappedLinks, region refresh).
- **Why this rank:** Safe scaffolding; none of ranks 1–19 would have been caught here.
- **Done:** Tests landed with ranks 4, 5, and 12 (`RegionRefreshTimingTests`, `TileGridMetricsTests`, `VolumePathTests`). `CopyModules` / `CopyGraphicsContent` rewrite was not done.

### 21. Dead MathNet init, unused CLI login fields, inverted measure help

- **Risk:** Safe · **Impact:** Low · **Category:** Duplication
- **Chunks / files:** App shell — `Program.InitializeMathnet` (never called); `ShowLoginWindow` ignores user/pwd; `CreateDebugListener` local vs static writer. Modules — `MeasureCommand` help says the opposite of `OnDraw`.
- **Evidence:** MKL never enables. `-u`/`-p` do not prefill login. Help overlay: Shift vs Ctrl swapped vs draw.
- **Recommendation:** Call `InitializeMathnet`. Prefill login. Match help to `OnDraw`. Assign the synchronized writer to the static.
- **Why this rank:** Safe cleanup; user-visible only for the help string.
- **Done:** `e83ebce1` — `InitializeMathnet` at startup; `-u`/`-p` prefill LoginWindow; static synchronized writer; Shift=horizontal in help.

### 22. RoundLine and RoundCurve are a forked copy

- **Risk:** Safe · **Impact:** Med · **Category:** Duplication
- **Chunks / files:** Graphics — `RoundLine.cs`, `RoundCurve.cs` (recent tangent work)
- **Evidence:** Same manager fields, save/restore wrappers, mesh init. Curve was updated; line was not. `IInitEffect` has no `Dispose`.
- **Recommendation:** Shared manager lifetime only; do not merge shaders; do not start a net9 rewrite.
- **Why this rank:** Safe extract; every line/curve fix will otherwise drift.
- **Done:** `04c863d6` — `IInitEffect : IDisposable`, `EffectManagerLifetime.DisposeOwnedBuffers`, DeviceEffectsStore/DeviceFontStore dispose on clear. Shaders not merged. Unrelated RoundCurve tangent work left uncommitted.

---

### 23. EF SQL types pinned to 14.0 while runtime is 16.0

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** App shell — `Program.cs` (~142); `Viking.csproj` (NuGet 160 + native 140/160); unused `Viking/SqlServerTypes/Loader.cs` (140 only). VikingAU already uses `16.0.0.0`.
- **Evidence:** `SqlServerTypesAssemblyName` is `Version=14.0.0.0` while the shared loader prefers Spatial160.
- **Recommendation:** Pin managed name to 16.0.0.0; one native loader.
- **Why this rank:** One string, but wrong assembly identity can fail spatial/EF at runtime.

### 24. Cancelled texture requests hang or permanently block a tile

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** VikingCore — `Threading/TextureRequestQueue.cs`, `PendingTextureQueue.cs`, `ViewModels/TileView.cs`
- **Evidence:** Cancel drops items without `CompleteRequest` or leaving `_pendingTileViews`. `AbortRequest` nulls the token copy so `SectionLoadingCancelled` becomes false. Related: file-claim released by two owners; `TextureReaderV2` never disposed; `SetMaxWorkers` abandons the semaphore.
- **Recommendation:** One cancel protocol that always completes the TCS and pending sets. Single owner for `_loadingFiles`. Transfer GPU texture ownership before disposing the reader.
- **Why this rank:** Section change / preload can hang; several lifetime bugs share one protocol.

### 25. Single-instance listen starts too late and dispose races the pipe thread

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** App shell — `Program.cs` (~204–216); `VikingSingleInstance.cs`
- **Evidence:** `StartListening` is after `context.Initialize()` (splash). Second `viking://` during splash starts another process. `StopListening` disposes the CTS/mutexes without joining `ListenLoop`. Handler ACKs `OK` after `BeginInvoke`, before navigate (`VikingDeepLinkActivation`).
- **Recommendation:** Own the pipe before splash. Join listen tasks before dispose. ACK after UI work, or treat `NOT_READY` as retry.
- **Why this rank:** Duplicate windows and lost places; needs careful pipe/UI ordering.

### 26. TokenInjector is a process-wide static; expired tokens still ship

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** Networking — `WCFTokenInjector/TokenInjector.cs`; `Program.cs` (~470–478); `LoginWindow.xaml.cs`
- **Evidence:** Unsynchronized public statics. Injects iff both token and authority are non-null; authority is never compared to the URI. No expiry/refresh. Existing `Authorization` header is not overwritten. `AfterReceiveReply` is a no-op.
- **Recommendation:** Session object with expiry + one 401 refresh. Gate on a real access token.
- **Why this rank:** Long sessions fail opaquely; missed authority assignment silently drops auth.

### 27. Overlay Shutdown does not tear down the static singleton

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation overlay — `UI/AnnotationOverlay.cs`
- **Evidence:** Ctor sets `_CurrentOverlay` and store `OnCollectionChanged`. `Shutdown` only cancels workers. Static `LastMouseOverObject`, cache, `basicEffect` remain. `InvalidateParent` uses `Parent.IsHandleCreated` with no null check.
- **Recommendation:** Instance-owned overlay. Unsubscribe, dispose CTS, clear cache, null statics.
- **Why this rank:** Closed viewer still mutates statics; second viewer reuses the first device’s `BasicEffect`.

### 28. `OutstandingQuery` is inverted; overlapping region loads drop callbacks

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `RegionLoader/RegionLoader.cs`
- **Evidence:** Property is `AsyncResult.IsCompleted` (true when **done**). Comments and `AreRegionQueriesComplete` already say it is inverted. In-flight requests do not attach a second viewer callback.
- **Recommendation:** In-flight = `AsyncResult != null && !IsCompleted`. Test second load during an open request.
- **Why this rank:** Couples with rank 4; region loading is both sticky and racy.

### 29. ParentID updates invert root vs child bookkeeping

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Store/StoreBaseWithIndexKeyAndParent.cs`
- **Evidence:** On ParentID change, a child calls `TryRemoveRootObject`; a former root is not removed from `RootObjects`. Comments contradict the branches.
- **Recommendation:** If it was a root, remove root; if it had a parent, detach **before** applying server data. Tests for root→child and child→root.
- **Why this rank:** Merge/reparent UI trees drift from `ParentID`.

### 30. Failed `Save` drops dirty objects; INSERT rollback is dead

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Store/StoreBaseWithKey.cs`, `StoreBaseWithIndexKey.cs`
- **Evidence:** Public `Save()` drains `ChangedObjects` then the index-key path returns false without restore. Fault handler sets `DBAction = NONE` **then** checks INSERT. Two copy-pasted Save bodies with opposite exception policy.
- **Recommendation:** One save/rollback: restore `ChangedObjects` on failure; check INSERT before clearing `DBAction`.
- **Why this rank:** Local cache and server diverge with no retry. Same bug class as 1D action save (rank 36).

### 31. `TimeQueueCache` races plus stuck `TileTasks` freeze tiles

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** Volume + Common — `TimeQueueCache.cs`, `VolumeModel/TileCache.cs`, `TileGridMappingBase.cs`
- **Evidence:** Two cleaners can `Task.Run` without `Interlocked`. `GetOrAdd` drops the loser entry. Faulted tile create never `TryRemove`s the key. Overlay cache (`SectionLocationViewModelCache`) uses the same base and has an empty `Dispose`.
- **Recommendation:** Factory `GetOrAdd`; `CompareExchange` on `CleaningTask`; always `TryRemove` in `finally`. Evict overlay views by unsubscribing.
- **Why this rank:** Pan/zoom can permanently skip a tile; shared cache is also the overlay trim path.

### 32. `FetchStosZip` reports success after failure (auth, zip-slip)

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** Volume — `Volume.cs` (`FetchStosZip`)
- **Evidence:** Generic catch traces then `return true`. `UserCredentials` unused. Entries write `Path.Combine(LocalCachePath, entry.FullName)` with no `..` check. Client not disposed.
- **Recommendation:** Return false on any failure; reject escaping `FullName`; authenticated shared client.
- **Why this rank:** Bad zip looks cached; untrusted members can write outside the cache dir.

### 33. RTree `TryAdd`/`Delete` take a nested read lock

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** Modules / spatial — `RTree/RTree.cs` (used by WebAnnotation views/stores)
- **Evidence:** `ReaderWriterLockSlim` default **NoRecursion**. Upgradeable lock then `Contains()` → `EnterReadLock`. `Update` and indexer take no lock.
- **Recommendation:** Check membership while already holding the write/upgradeable lock. Lock `Update`.
- **Why this rank:** Annotation spatial insert can `LockRecursionException`; hover then disagrees with draw.

### 34. Bookmark Initialize swallows failures and always reports success

- **Risk:** Moderate · **Impact:** High · **Category:** Bug
- **Chunks / files:** Modules — `LocalBookmarks/Global.cs`; `NGVV/ExtensionManager.cs` ignores the bool
- **Evidence:** Empty `catch`. `_BookmarkXMLDoc` can stay null (`FolderRoot` NRE). V1 migrate can persist XML without `MosaicPosition`. `UndoFileNames` has no `{0}` placeholder.
- **Recommendation:** On load failure, restore undo or create new. Honor `Initialize` in ExtensionManager. Persist mosaic even when volume map fails.
- **Why this rank:** Corrupt bookmarks crash later instead of recovering.

### 35. Texture/view pipeline is a large leftover surface

- **Risk:** Moderate · **Impact:** High · **Category:** Maintenance
- **Chunks / files:** VikingCore — dual queues, commented `StreamToTexture.cs`, unused `LoadingVolumeForm`, `CreateSectionOverlays` MessageBox + rethrow
- **Evidence:** Near-identical sorts and two pending HashSets. Almost every texture bug must be fixed twice.
- **Recommendation:** One pending-set API. Delete commented clone and unused volume-load form. Do not rethrow after removing a bad overlay.
- **Why this rank:** High maintenance cost; do after rank 24’s protocol, not as a rewrite of decode vs GPU pump.

### 36. Annotation command/action save and view forks

- **Risk:** Moderate · **Impact:** Med · **Category:** Bug / Duplication
- **Chunks / files:** Commands — `AddRemoveLineControlPointCommand` (closed-ring first-vertex); `ChangeTypeAction` / `ChangeContourAction` (1D skip rollback); `Actions/`* vs `ActionViews/`* plus leftover V1 pen commands
- **Evidence:** Closed-ring remove leaves first≠last (fix is commented). 2D actions restore on `FaultException`; 1D/structure-link do not. Dual confirmation UIs; V1 `NumCurveInterpolations` throws.
- **Recommendation:** Restore ring closure + test. One save helper. Views only in `ActionViews`; delete or `#error` V1 pen commands.
- **Why this rank:** Several copy-paste defects; each is small, the fork is the cost.

### 37. Section annotation views: off-thread init, Debug/Release draw, eviction without dispose

- **Risk:** Moderate · **Impact:** Med · **Category:** Bug
- **Chunks / files:** Overlay — `AnnotationViewFactory.cs`, `LocationPolygonView.cs`, `SectionLocationViewModelCache.cs`, `SectionAnnotationsView.cs`
- **Evidence:** `Task.Run(Initialize)`; Debug always draws control points, Release does not. Eviction `Dispose()` is empty. Z±1 stored both as adjacent clones and full cache entries. Draw dispatch is a duplicated `typeof` switch (`LocationObjRenderer`).
- **Recommendation:** Init on load/UI path; `finally` reset `_Initializing`. One draw dispatcher. Evict by unsubscribing. Prefer one section view in cache.
- **Why this rank:** Visual/hit-test drift and leaked listeners; touches render timing.

### 38. GPU thread/state hygiene (DeviceStateManager, CurveLabel, GpuSynchronization)

- **Risk:** Moderate · **Impact:** Med · **Category:** Bug
- **Chunks / files:** Graphics — `DeviceStateManager.cs` (static slot, not a stack); `CurveLabel.cs` (no dispose/cancel; steals render target); `GPUSynchronizationContext.cs` (fallback posts to the thread pool)
- **Evidence:** Nested circle + line draws overwrite outer blend/depth. `LabelView` already has the correct cancel/dispose/restore pattern. `new SynchronizationContext()` is not a GPU marshal.
- **Recommendation:** Stack or per-call locals. Port `LabelView` to `CurveLabel`. Require a captured UI context at device create.
- **Why this rank:** Wrong blend or off-thread GPU after nested draws / label edits.

### 39. WPF converter / command / picker duplication

- **Risk:** Moderate · **Impact:** Med · **Category:** Duplication
- **Chunks / files:** WPF — `StructureIDToStructureObjConverter` (uses empty `IDs` list); three `RelayCommand` types (`async void` Execute; dead `CanExecuteChanged`); `VolumeSelectionViewModel` / `SegmentationServiceSelectionViewModel` clones with hardcoded Identity port **6001**; unused `PerformVolumeAuthenticationAsync`
- **Evidence:** Integer ID collections resolve to nothing. Volume tree ignores XML `IdentityApi`. Login exceptions escape as dispatcher crashes.
- **Recommendation:** Cast `value`. One ICommand helper (`Task` + requery). One resource-picker VM; one Identity API URL helper.
- **Why this rank:** Bindings and login stages already drifted; not a rewrite of the wizard.

### 40. Volume HTTP/mapping forks and empty concurrent init

- **Risk:** Moderate · **Impact:** Med · **Category:** Duplication / Bug
- **Chunks / files:** Volume — `TileGridToVolumeMapping` / `OCPTileServerToVolumeMapping` / hull copy-paste (`X + QuarterHeight` on top-edge X); `HttpClientFactory` unused by Volume/Utils; `SectionToVolumeMapping.Initialize` returns if `InitializationInProgress` before the semaphore
- **Evidence:** OCP sync `VisibleTiles` is unwarped. Factory exists but `FetchStosZip` / `Utils.IO` use bare `HttpClient`. Second init waiter gets `[]`.
- **Recommendation:** Shared volume-mapping base; override OCP `VisibleTiles`. One authenticated client. All waiters take the semaphore.
- **Why this rank:** Screenshot/export vs mosaic geometry; HTTPS mosaic can fail auth.

### 41. Annotation contracts forked (`DBACTION`, keys, unused `IAnnotate*`)

- **Risk:** Moderate · **Impact:** Med · **Category:** Duplication
- **Chunks / files:** Networking — `AnnotationServiceTypes` vs `AnnotationInterfaces` (`Int64` vs `ulong` IDs; three `DBACTION`s). Client stores use generated `WebAnnotationModel.Service.IAnnotateLocations`, not hand-written `AnnotationService.Interfaces.IAnnotate`*. `StructureLinkKey.CompareTo` both sides compare `B_High` to itself.
- **Evidence:** WebAnnotation references both assemblies. A later `public` on the internal interface is CS0433. Store/link-key Save/Remove already drifted (`StoreBaseWithKey` vs `IndexKey`; `LocationStore.Remove` vs `StructureStore.Remove`).
- **Recommendation:** One DBACTION and key width. Treat AnnotationServiceTypes as DTO/protobuf for the client. Shared Save/link-key helpers. Do not edit hand-written `IAnnotate`* expecting Viking to pick it up.
- **Why this rank:** Protocol adds will drift; link sort is already wrong twice.

### 42. Plugin leftover: stale bookmark sprites, measurement About.xml NRE, dual SqlGeometry converters

- **Risk:** Moderate · **Impact:** Med · **Category:** Bug / Duplication
- **Chunks / files:** Modules — `BookmarkUIObj` views not refreshed after transform; `MeasurementExtension/Global.cs` `GetResult()` then `GetVolumeElement` on null; `SqlGeometryUtils.ToShape2D` vs `ToIShape2D` (POINT/CURVEPOLYGON disagree); `Centroid` uses null `STCentroid()`
- **Evidence:** Draw uses construction-time `GridPosition`; hit-test uses live position. Missing About.xml throws during module init. Linked-location mesh code that calls `ToIShape2D` mishandles circles.
- **Recommendation:** `UpdateView` after transform. Null-check XML; no UI-thread `GetResult()`. One converter; centroid fallback on `geometry`.
- **Why this rank:** Three small plugin defects; none needs a redesign.

### 43. Settings `Reload()` can wipe in-memory values

- **Risk:** Moderate · **Impact:** Med · **Category:** Bug
- **Chunks / files:** App shell — `Properties/Settings.Extensions.cs`; login persist in `Program.cs`
- **Evidence:** First touch of an extension key `Reload()`s. Later first-touch of `SbfsemTools`* / `LaunchExchangeBaseUrl` drops unsaved recents. Defaults duplicated in three places.
- **Recommendation:** Register extension properties once after `Upgrade()`; never `Reload()` per getter. One default constant.
- **Why this rank:** After rank 1; still loses data if login Save and a later getter interleave.

---

### 44. Grouped auto-polygonize accept is a two-phase save

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** WebAnnotation commands — `UI/AutoPolygonize/AutoCirclePolygonizeController.cs`, `LocationSiblingMerge.cs`
- **Evidence:** `ApplyVolumePolygon` already `Save()`s the survivor. Then links/deletes/saves again with no rollback. Partial success = polygon + leftover same-cell circles.
- **Recommendation:** One store transaction with full rollback. Do not treat `ApplyVolumePolygon` as a complete grouped accept.
- **Why this rank:** New merge path; a botched accept is duplicate geometry on the server. Needs a transaction design.

### 45. Draw blocks the UI thread on sync-over-async `GetOrCreate`

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** Overlay — `UI/AnnotationOverlay.cs` (`GetOrCreateAnnotationsForSection` → `GetResult()` around `SemaphoreSlim.WaitAsync`; called from `Draw` ~2374)
- **Evidence:** Classic deadlock/jank: render thread waits on a pool continuation that may wait the semaphore or marshal back.
- **Recommendation:** Draw only `Fetch`. Create views on the load worker. Delete the sync wrapper from the UI path.
- **Why this rank:** First visit to a section hitches or deadlocks; must not leave Draw blocking.

### 46. `StructureTypeObjViewModel` registers `NewPermits` on the wrong owner

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** WPF — `WebAnnotationViewModels/ViewModels/StructureTypeObjViewModel.cs`; converter allocates a VM per tree click; no unsubscribe
- **Evidence:** DP registered as `typeof(PermittedStructureLinkViewModel)`. Both types register `"NewPermits"` on that owner with a shared default collection.
- **Recommendation:** Register DPs on the actual owner with `null` default; unsubscribe; cache the VM.
- **Why this rank:** Type-init throw + leak on every selection. DP registration mistakes are hard to revert safely.

### 47. Launch-code `TokenResponse` is built by reflection

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** WPF — `LoginWindow.xaml.cs`, `LoginViewModel.cs`
- **Evidence:** Writes `AccessToken` via backing-field names and swallows failure. Launch path uses the API token as the volume bearer; password login does a second ROPC hop. `PrepareSegmentationStageAsync` clears `IsLoading` too early (`async void OnVolumeSelected`).
- **Recommendation:** Construct `TokenResponse` through IdentityModel (or a wrapper). Fail closed if AccessToken is empty. Keep loading until the wizard leaves the volume stage.
- **Why this rank:** A no-op reflection write looks like skip-login; WCF then 401s.

### 48. Hardcoded OAuth client secrets (and a space-mismatch variant)

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** Networking — `WCFTokenInjector/BearerTokenUtils.cs`; `LoginViewModel`; `Viking/app.config`
- **Evidence:** Default `"Correct Horse Battery Staple"` vs `CreateFromAppSettings()` `"CorrectHorseBatteryStaple"`. ClientIds `"api"` / `"Viking"` / `"ro.viking"`. Secret is in source and config.
- **Recommendation:** Public client + PKCE. Until then, one secret source and one ClientId per grant. Treat the space mismatch as a live auth-break risk.
- **Why this rank:** Security + lockout. Do not “just change the string” without IdentityServer coordination.

### 49. Segmentation gRPC is `ChannelCredentials.Insecure` with no bearer

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** Networking / VikingCore — `NGVV/Services/Grpc/GrpcChannelManager.cs`; `SegmentationViewportSession.cs`
- **Evidence:** `new Channel(serviceUrl, ChannelCredentials.Insecure)`. RPCs have deadline only. WCF annotation can be bearer-authed while SAM2 upload is plaintext and anonymous.
- **Recommendation:** TLS + the same bearer (or a segmentation scope). Insecure only behind an explicit localhost/dev flag.
- **Why this rank:** Viewport images leave the machine without TLS or the user token. Needs a server-supported auth story.

### 50. Device-lost is reported but `GraphicsDevice` is never recreated

- **Risk:** Risky · **Impact:** High · **Category:** Bug
- **Chunks / files:** Graphics — `GraphicsDeviceService.cs`, `GraphicsDeviceControl.cs`, `GpuDeviceRemovedDialog.cs`
- **Evidence:** `Lost` returns an overlay string. `ResetDevice` no-ops when Lost. DXGI `DEVICE_REMOVED` cannot be repaired with `Reset()`. `Release` never nulls the singleton.
- **Recommendation:** Keep the dialog. Recovery = new device, reload Content, rebuild stores. Do not treat `Reset()` as sufficient.
- **Why this rank:** After TDR, Viking stays on cornflower until process restart. Recreating the device is a wide blast radius.

### 51. Startup update check freezes the UI (`DoEvents` + `Sleep`)

- **Risk:** Risky · **Impact:** Med · **Category:** Bug
- **Chunks / files:** App shell — `Services/UpdateService.cs`
- **Evidence:** `CheckForUpdates()` is synchronous. Download pumps `DoEvents` + `Thread.Sleep(50)` and nested `DoEvents` inside `Invoke`.
- **Recommendation:** Check/download off the UI thread; `BeginInvoke` progress; apply/restart on the UI thread.
- **Why this rank:** Nested `DoEvents` can re-enter UI. Touch only with a message-loop plan.

### 52. Thread-local WCF channel is Closed/Aborted/Disposed per request

- **Risk:** Risky · **Impact:** Med · **Category:** Bug
- **Chunks / files:** WebAnnotation model — `Store/StoreBase.cs`, `StoreBaseWithKey.cs`, `LocationStore.cs`
- **Evidence:** `ThreadLocalProxyFactory` reused. Callbacks `Close()` it. `GetObjectsInRegion` `using`s it. Cancel `Abort()`s the same instance. Couples to rank 8.
- **Recommendation:** Per-request channel, or recreate after fault without disposing a channel another call holds.
- **Why this rank:** One close faults every in-flight call on that thread. Lifetime change is easy to get wrong.

### 53. Dual WCF auth: factory copies username once; bearer is easy to omit in config

- **Risk:** Risky · **Impact:** Med · **Category:** Bug
- **Chunks / files:** Networking — `TokenInjectionEndpointBehavior.cs`; store ctors; Viking `StandardBehavior` has `<tokenInjection />` but `protoEndpointBehavior` comments it out; WebAnnotationModel app.config comments the extension out
- **Evidence:** `Credentials.UserName` set at ChannelFactory construction (often `anonymous`/`connectome`). Later TokenInjector updates do not refresh those credentials.
- **Recommendation:** One client auth story (bearer). Apply the behavior in code on the factory after login.
- **Why this rank:** Two mechanisms make “why 401?” hard; changing it can break UserNameOverTransport hosts.

---

## Do not do yet (high-risk appendix)

- **net9 / Monographics rewrite** of the net48 graphics stack. The graphics rule is net48 = VikingXNAGraphics / MonoGame 3.7. Fix ranks 6–7, 22, 38, 50 in place.
- **Unify AnnotationServiceTypes + AnnotationInterfaces + generated WCF** in one pass (rank 41). Pick ID width and DBACTION first; do not “make the internal interface public.”
- **Auto-polygonize grouped accept** (rank 44) until there is a single store transaction and a failing test for mid-accept fault.
- **Recreate GraphicsDevice after TDR** (rank 50) until Content/effect/cache rebuild is enumerated. Dialog-only is safer than a half recreate.
- **PKCE / public-client OAuth** (rank 48) and **gRPC TLS+bearer** (rank 49) without IdentityServer and segmentation-service support.
- **Commenting out code to compile** — not a fix path (repo rule).
- **Geometry** — still out of scope.

## Open questions

1. Are launch-exchange tokens volume-scoped, or must Viking always do the second ROPC hop (rank 47)?
2. Which IdentityServer client secret is live — spaced or unspaced (rank 48)?
3. Is segmentation gRPC intentionally LAN-only, or was TLS omitted (rank 49)?
4. Should `VikingTests` retarget net48 or should shell helpers move to a testable library (rank 20)?
5. Is `TextureRequestQueue` + `PendingTextureQueue` a finished split, or should rank 35 collapse them after rank 24?
6. AnnotationVizLib / GraphLib / OData sit in `Viking.sln` but are not Viking.exe dependencies — remove from this solution or leave for Jotunn?

## Scope surprises

- **In (not obvious):** `SegmentationServiceTypes.gRPC` is a WebAnnotation/VikingCore client dependency (SAM2). `SqlGeometryAnnotationExtensions` is on the WebAnnotation graph. `SqlServerTypesLoader` is a direct Viking.exe reference (separate from the unused local 140 loader).
- **Out (in the `.sln`, not on the client graph):** AnnotationVizLib (+ WCF/OData clients), GraphLib, ODataClient, VikingAU, XNATestbed, Jotunn/Bifrost.
- **Tests surprise:** `VikingTests` is `net9.0-windows` and does not reference Viking.exe. Overlay/cache/view have almost no tests.
- **Auth surprise:** Hand-written `IAnnotate`* in AnnotationServiceTypes is not what the client ChannelFactory uses (generated `WebAnnotationModel.Service.`*).
- **Geometry:** Excluded as requested; VolumeModel and WebAnnotation still call it at many sites.

## Reviewer coverage


| Chunk        | Reviewer | Status   |
| ------------ | -------- | -------- |
| 1 App shell  | 16b89765 | complete |
| 2 VikingCore | bbb83fe7 | complete |
| 3 Commands   | 3b12518b | complete |
| 4 Overlay    | cf38bca6 | complete |
| 5 Model      | 6ade7c7b | complete |
| 6 Graphics   | 218e2e10 | complete |
| 7 WPF        | 9b302a3b | complete |
| 8 Volume     | 7addf558 | complete |
| 9 Networking | c8637fb0 | complete |
| 10 Modules   | 4b286694 | complete |


