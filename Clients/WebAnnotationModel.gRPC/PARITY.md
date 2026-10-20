# gRPC store parity with the WCF client

Compares `VikingClient/Clients/WebAnnotationModel/Store` (WCF, `Legacy`) with this project, member by member. First version, built by the improvement loop from member names only: "done" means an equivalent member exists, not that behavior (ordering, null/empty results, timestamps, cancellation) has been verified by a paired test. The Test and Benchmark columns are empty until `Benchmarks/AnnotationParity` and paired tests exist. Tally: 38 done, 2 partial, 0 missing, 4 n/a.

The client above the stores was brought to parity by merging Legacy 00014315 (base f43992ae) into this tree on 2026-10-19: every Legacy client feature now builds against these stores, and the Legacy client tests run against them (WebAnnotationTests, VikingCoreTests, CommonTests, VolumeModelTests). The rows below the table header marked "Legacy 2026-08..10" are store behaviors Legacy added after the fork.

| WCF member | gRPC equivalent | Status | Test | Benchmark |
|---|---|---|---|---|
| StoreBaseWithKey.GetObjectsForSection / GetObjectsForSectionAsynch | `GetObjectsForSectionAsync` | done | | |
| GetLocalObjectsForSection (all stores) | `LocationStore.GetLocalObjectsForSection` (section index); `StoreBaseWithKey` section queries read the same-named store member | done | | |
| GetLastQueryTimeForSection / AdvanceLastQueryTimeForSection | `GetLastQueryTimeForSection`, `AdvanceSectionQueryTime`, `TouchSectionQueryTime` | done | | |
| HasOutstandingSectionQuery, RemoveSection, FreeExcessSections | same names (plus `CancelExcessSectionQueries`) | done | | |
| ProxyGetByID / ProxyGetByIDs | `TryGetObjectByID`, `TryGetObjectsByIDs` | done | | |
| ProxyGetBySection / Begin/Callback | `LocationsClient` `GetLocationChanges*` (async) | done | | |
| ProxyGetBySectionRegion (+ Begin/Callback) | `LocationRegionLoader.GetObjectsInRegionAsync`, `GetLocationChangesInMosaicRegion` | partial: region cell cache semantics differ; unverified vs WCF MinRadius filtering | | |
| ProxyUpdate (batched save; afterwards every saved object's DBAction is reset to NONE and the server's returned objects replace the cached ones) | `Save` on stores and updaters | done: after the update RPC returns, `StoreBaseWithKey.Save` resets every saved object to NONE (which also forgets a structure's per-field edit tracking), then applies the server's returned copies (`UpdatedObjects`) through the store's updater, so LastModified and Username match the server. Inserted objects are skipped (their create path adds them) | `StoreSaveSuccessTests` | |
| RestoreChangedObjects | `StoreBaseWithKey.RestoreChangedObjects` (called when the update RPC fails) | done: a failed update RPC requeues objects and `Save` returns false (WCF rethrows and rolls back INSERTs). When `GetOrCreate` throws, objects are requeued unchanged and the exception is rethrown (WCF `CreateProxy` parity). Not yet parity when conversion throws before the RPC: that outer catch still resets every `DBAction` to NONE (decision D-SaveOuterCatch) | `StoreSaveFailureTests` | |
| IsProxyBroken, CreateProxy, GetProxy | client factory / channel reuse | n/a: WCF proxy lifecycle | | |
| TraceQueryDetails | `QueryLogger` | done | | |
| LocationStore.GetObjectsInRegion(Async) | `LocationRegionLoader` | done | | |
| LocationStore.GetLocalObjectsInRegion / LocationRTree / Intersects | `Contains(o, bounds)`; R-tree owned by loader | partial: no public local region query on the store | | |
| LocationStore.GetLocationsForStructure | `GetStructureLocations`, `GetLocalObjectsForStructure` | done | | |
| LocationStore.GetLastModifiedLocation, GetStructureLocationChangeLog | same names | done | | |
| LocationStore.Create / AddObject / RemoveObject | `Create`, `Add`, `Remove` | done | | |
| LocationStore.TryAddObject / TryRemoveObject | none | n/a: internal to WCF cache | | |
| LocationLinkStore.CreateLink / DeleteLink | same names | done | | |
| LocationLinkStore.GetLinksCrossingSection | `GetLinksForSectionAsync` | done: WCF `GetLinksCrossingSection` only returns the per-section cache; which links belong to a section is decided server-side by the same section TVF in both services (`AnnotateService.GetLocationLinksForSection` vs `LocationService.GetLocationLinksForSection`). Read-through only, no paired test | | |
| LocationLinkStore.ParseQuery / InternalAdd / InternalDelete | `MergeServerLinksAsync` | done | | |
| StructureStore.Create | `Create` | done | | |
| StructureStore.GetAllStructures | `GetAll` | done | | |
| StructureStore.GetChildStructuresForStructure | `GetChildStructures` | done | | |
| StructureStore.GetStructuresOfType, Merge | same names | done | | |
| StructureStore.SplitAtLocationLink | `SplitStructureAtLocationLink` | done | | |
| StructureStore.GetUnfinishedBranches(WithPosition), CheckForOrphan | same names | done | | |
| StructureStore.GetObjectsInRegion(Async) | `GetLocalObjectsInRegion` (mosaic bounding box intersects region, Radius >= MinRadius, as WCF `LocationRTree`), `GetServerObjectsInRegion` | done | `StructureStoreRegionQueryTests` (local only) | |
| StoreBaseWithKeyAndParent.InternalUpdate (ParentID changed by the server) | `StoreBaseWithKeyAndParent.CallOnCollectionChanged` re-wires `UpdatedObjects` (RootObjects, parent `Children`); `ParentID` setters keep the assigned value | done: server batches and the `StructureStore` queries (child, type, region, by-ID); structure types are loaded once and never updated | `StructureReparentTests`, `StructureStoreServerUpdateTests` | |
| StoreBaseWithKey.InternalUpdate (cached object takes the server values; `WCFObjBase.Update` replaced the wrapped `Data`, so DBAction became the server's NONE and a pending local edit was dropped, values and flag) | `StoreServerQueryResultsHandler` with the store's `IObjectUpdater` | partial: every updater applies server values inside `ApplyServerValues`, so a refreshed object stays NONE and is not sent back under the current user's name. Structures keep the fields the user edited since the last save and take the server's value for the rest (decision D-StructureRefreshPendingEdit); locations and structure types with any unsaved edit skip the refresh until it is saved (WCF dropped the edit instead). `StructureTypeStore` and `StructureLinkStore` still drop their updaters | `StructureStoreServerUpdateTests`, `StructureRefreshPendingEditTests`, `ServerRefreshDoesNotDirtyTests` | |
| StoreBaseWithKeyAndParent.InternalAdd (ParentID on first load; WCF objects wrap the server data) | `StructureServerToClientConverter` copies the server parent id and leaves DBAction NONE; `CallOnCollectionChanged` wires `AddedObjects` | done | `StructureServerCreateParentTests` | |
| StoreBaseWithKeyAndParent.InternalAdd (missing parents fetched with InternalGetObjectsByIDs, then children wired) | A child whose parent is not cached waits; after the notify step `ProcessorForServerAnnotationSets` and the `StructureStore` queries fetch uncached parents by ID (`FetchMissingParentsAsync`), and `GetObjectsByIDs` also fetches the returned objects' uncached ancestors; each fetch's `CallOnCollectionChanged` adopts the waiting children | done: each ID is requested once per fetch, so a parent missing on the server leaves the child unwired with its `ParentID` | `StructureOrphanChildTests` | |
| StructureTypeStore.LoadStructureTypes | `GetAll`, `Create` (`StructureTypeServerToClientConverter` leaves server-created types DBAction NONE, so `Create` does not queue them for Save) | done | `StructureTypeServerCreateTests` | |
| StructureLinkStore / PermittedStructureLinkStore (Create, sections) | `GetLinks`, `Save`, `MergeServerLinksAsync` / `Save` | done | | |
| Interfaces.cs, IKeyGenerator, LongIndexGenerator (client-side keys) | server-assigned IDs | n/a: ID generation is server-side | | |
| Legacy 2026-08..10: PermittedStructureLinkStore queues editor-built links (INSERT) on add (a2a05091) | `StoreBaseWithKey.TrackForSaveIfPending`, `PermittedStructureLinkStore.QueueUnsavedLinks` | done | `PermittedStructureLinkSaveQueueTests` (WebAnnotationTests) | |
| Legacy: re-adding a link whose DELETE is queued cancels it; removing an unsaved INSERT sends nothing (f654ad32) | `PermittedStructureLinkStore.QueueUnsavedLinks`, `CancelUnsavedInsert`, `Remove` overrides | done | `PermittedStructureLinkSaveQueueTests` | |
| Legacy: one lock across store, section list and R-tree; duplicate R-tree ID replaces (efaa2d78) | R-tree owned by `RegionLoader` (`SpatialSearch`) | n/a: different index ownership; not re-checked | | |
| Legacy: `LastSaveFault` on the store (c5295588) | `IStore.LastSaveFault`, set when an update RPC fails or `Save` throws and cleared by the next `Save`; Viking's save-failed message and Jotunn's status show it | done | `StoreSaveFailureTests` | |
| Legacy: `LastServerFetchError` so Goto Location reports access denied (05e38245) | `IStoreWithKey.LastServerFetchError`, read by `AnnotationOverlay` deep-link lookups | done | | |
| Legacy: 30 s visible-section poll, outstanding-query guard, watermark from region-load time, `InvalidateSection` on eviction (81fd3192) | `SectionLocationPollPolicy`, `LocationStore.HasOutstandingSectionQuery`/`TouchSectionQueryTime`, `IRegionLoader.InvalidateSection` | done | `SectionLocationPollPolicyTests` | |
| Legacy: region refresh compares `TotalSeconds` (66460820) | `RegionRefreshTiming` | done | | |
| Legacy: cancelled or failed region cell clears its LastQuery; `AreRegionQueriesComplete` (03154886) | `RegionRequestData.ClearLastQuery`, `IRegionLoader.AreRegionQueriesComplete` | done | | |
| Legacy: cache-only lookups `GetObjectByID(id, false)`, `Values` | `GetCachedObject`, `CachedObjects` | done | | |

## Client-side ingest benchmarks

`Benchmarks/AnnotationParity` (BenchmarkDotNet, run in-process: `dotnet run -c Release -f net10.0-windows --project Benchmarks/AnnotationParity -- --filter * --inProcess`). `LocationIngestBenchmarks` merges generated region-load payloads (70% polygons with a 40-vertex ring, 30% circles, two links each) into a `LocationStore` through `StoreServerQueryResultsHandler`. No server is involved, and the WCF baseline needs the old service host, so the WCF column is `baseline: unavailable`; the numbers below compare the gRPC client before and after the 2026-10-19 changes (ShortRun, .NET 10, 5000 locations).

| Case | Before | After | Change |
|---|---|---|---|
| First visit (all new) | 148 ms, 740 MB | 53-60 ms, 101 MB | WKT scanner instead of regexes |
| Revisit, rows unchanged | 240 ms, 1.88 GB | 1.6 ms, 4.2 MB | updater skips a row whose LastModified matches (links still sync) |
| Refresh, every row changed | 176 ms, 1.42 GB | 17 ms, 97 MB | `Polygon.Equals` read rings directly instead of copying the ring per vertex |
| WKT parse of the polygons alone | 114 ms, 140 MB | 17-19 ms, 45 MB | hand-written coordinate and parenthesis scanner |
