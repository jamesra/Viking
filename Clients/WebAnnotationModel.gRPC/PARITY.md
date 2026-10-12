# gRPC store parity with the WCF client

Compares `VikingClient/Clients/WebAnnotationModel/Store` (WCF, `Legacy`) with this project, member by member. First version, built by the improvement loop from member names only: "done" means an equivalent member exists, not that behavior (ordering, null/empty results, timestamps, cancellation) has been verified by a paired test. The Test and Benchmark columns are empty until `Benchmarks/AnnotationParity` and paired tests exist. Tally: 28 done, 2 partial, 0 missing, 3 n/a.

| WCF member | gRPC equivalent | Status | Test | Benchmark |
|---|---|---|---|---|
| StoreBaseWithKey.GetObjectsForSection / GetObjectsForSectionAsynch | `GetObjectsForSectionAsync` | done | | |
| GetLocalObjectsForSection (all stores) | `SectionIndexedStore.GetLocalObjectsForSection` | done | | |
| GetLastQueryTimeForSection / AdvanceLastQueryTimeForSection | `GetLastQueryTimeForSection`, `AdvanceSectionQueryTime`, `TouchSectionQueryTime` | done | | |
| HasOutstandingSectionQuery, RemoveSection, FreeExcessSections | same names (plus `CancelExcessSectionQueries`) | done | | |
| ProxyGetByID / ProxyGetByIDs | `TryGetObjectByID`, `TryGetObjectsByIDs` | done | | |
| ProxyGetBySection / Begin/Callback | `LocationsClient` `GetLocationChanges*` (async) | done | | |
| ProxyGetBySectionRegion (+ Begin/Callback) | `LocationRegionLoader.GetObjectsInRegionAsync`, `GetLocationChangesInMosaicRegion` | partial: region cell cache semantics differ; unverified vs WCF MinRadius filtering | | |
| ProxyUpdate (batched save) | `Save` on stores and updaters | done | | |
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
| StoreBaseWithKeyAndParent.InternalUpdate (ParentID changed by the server) | `StoreBaseWithKeyAndParent.CallOnCollectionChanged` re-wires `UpdatedObjects` (RootObjects, parent `Children`); `ParentID` setters keep the assigned value | done: server batches only; structure types are loaded once and never updated | `StructureReparentTests` | |
| StoreBaseWithKeyAndParent.InternalAdd (ParentID on first load; WCF objects wrap the server data) | `StructureServerToClientConverter` copies the server parent id and leaves DBAction NONE; `CallOnCollectionChanged` wires `AddedObjects` | done | `StructureServerCreateParentTests` | |
| StructureTypeStore.LoadStructureTypes | `GetAll`, `Create` (`StructureTypeServerToClientConverter` leaves server-created types DBAction NONE, so `Create` does not queue them for Save) | done | `StructureTypeServerCreateTests` | |
| StructureLinkStore / PermittedStructureLinkStore (Create, sections) | `GetLinks`, `Save`, `MergeServerLinksAsync` / `Save` | done | | |
| Interfaces.cs, IKeyGenerator, LongIndexGenerator (client-side keys) | server-assigned IDs | n/a: ID generation is server-side | | |
