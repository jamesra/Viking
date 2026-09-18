# VikingLegacy Refactoring Assessment

Refactoring assessment of the VikingLegacy monorepo. Items marked **Status: completed** have been implemented; remaining items are still open.

**Scope:** Clients, Servers, Geometry/MorphologyMesh, shared annotation libs, Scripts/Docs.  
**Quarantine (do not deep-edit):** Triangle.NET, Collada generated schema dumps, `Servers/Libs/NetTopologySuite*`, EF migration dumps, SQL `.dbmdl`/`.jfm`, `bin`/`obj`.  
**Non-goals:** Bajaj algorithm redesign, streaming annotation feature work, NGINX/ops changes, GitHub issues/commits/PRs.

**Coverage:** All three deep-review waves included:
- **Wave 1** ([aea6ec5d](aea6ec5d-4517-4585-95ca-9835635681a9)): C01–C07 — **137** opportunities
- **Wave 2** ([44fcff09](44fcff09-0f88-4860-b528-9cdb00e04828)): C08–C14 — **206** opportunities
- **Wave 3** ([36d833ca](36d833ca-335d-493b-9c1e-7cd2ac3d6d56)): C15–C21 — **205** opportunities
- **Deduped listing:** 548 raw → **540** unique opportunities (8 cross-chunk aliases folded; see *See also* on canonicals).

---

## 1. Executive summary (top opportunities by impact × feasibility)

| Rank | ID | Title | Why high value |
|------|-----|-------|----------------|
| 1 | C02-001 | Archive/delete Identity `source/` (net45/IS3) | **completed** — tree removed from repo |
| 2 | C05-001..003 | Archive Bifrost + delete dead web stubs | Orphaned build-graph / missing csproj husks; AnnotationVizLibOData stub |
| 3 | C06-001 | Archive Neo4j island if unused | Self-referenced net48 MVC; committed test passwords; no product consumers |
| 4 | C03-001 | Delete orphan `gRPCSegmentAnything` stub | Simulated processing; not the live Python SAM2 path |
| 5 | C15-002≈C16-012 | Route Viking tiles through SectionSceneRenderer | Highest dual-client tile-draw fork; Jotunn already uses shared renderer |
| 6 | C16-001≈C15-003≈C17-024 | Unify AnnotationOverlay / AnnotationScene draw | Single annotation draw orchestrator; stop second-renderer drift |
| 7 | C21-001≈C10-001 / C15-009 | WCF retirement + delete orphan WCF clients/refs | Viking already on gRPC; WCF model/Service References/tests are dead weight |
| 8 | C14-001 / secrets | Canonical SQL + remove hardcoded passwords | SSDT vs script trees; CreateUpdate / Neo4j / Identity plaintext credentials |
| 9 | C09-001≈C21-002 | Collapse triple OData/WCF morphology clients | One OData client; delete unused WCF viz client |
| 10 | C19-015≈C21-007 | gRPC parity checklist then drop WCF from docker | Unblocks combined-image slim-down and EF6 retirement path |
| 11 | C20-001 | Modernize or freeze ConnectomeODataV4 (net48/EF6) | Last major EF6 host beside WCF; blocks stack collapse |
| 12 | C01-001 / C01-005 | Quarantine Collada schema; retire SmoothMesh husks | ~16.8k generated LOC + empty-mesh stubs (MeshXNAVizLib completed) |
| 13 | C13-001 / C13-002 | Unify MonoGame content / retire net48 graphics path | Shared C# with diverging `.fx`; dual MG 3.7/3.8 tax |
| 14 | C12-001 / C15-001 | Split Volume.cs + SectionViewerControl god files | Largest maintainability sinks on client volume/viewer path |
| 15 | C07 / C04 ops | Collapse docker/docs/portal deploy sprawl | Sphinx still documents dead C05 services; duplicate deploy scripts |

---

## 2. Cross-cutting themes

1. **Dead / orphaned product surfaces** — Identity `source/` (IS3/net45, **completed**), Bifrost, ConnectomicsWebsite, ConnectomeOpenData, WebVisualization, Neo4j island, `gRPCSegmentAnything`, SmoothMesh (MeshXNAVizLib completed), orphan WCF client projects.
2. **Dual WCF / gRPC stacks** — Combined docker still ships WCF AnnotationService + ODataV4 (net48) beside gRPC + DataExport; first-party clients already bootstrap gRPC.
3. **Annotation draw / tile-path fork** — WinForms `AnnotationOverlay.Draw` vs Jotunn `AnnotationScene.Draw`; Viking tile draw still bypasses shared `SectionSceneRenderer` (deduped C15-002/C16-012 and C16-001/C15-003/C17-024).
4. **Triple (plus) OData clients** — Generated `ODataClient`, `AnnotationVizLibODataClient`, SimpleOData (MorphologyView), unused WCF viz client, dead server OData stub.
5. **Committed secrets / credential debt** — Identity archive configs, Neo4j tests, CreateUpdateDatabase logins, anonymous `"connectome"` defaults; modern hosts already use env resolvers.
6. **Dual EF6 vs EF Core + SQL dual trees** — `ConnectomeDataModel` vs Core; SSDT vs `Servers/SQL` / CreateUpdate with same-named divergent objects.
7. **Dual-TFM graphics** — Same Shared sources into MG 3.7 (net48) and 3.8 (net9/10); technique shaders already diverge; DesktopGL/WindowsDX mismatch.
8. **God files** — SectionViewerControl, AnnotationOverlay, BajajMeshGenerator, AnnotateService, StoreBaseWithKey×2, Volume.cs, Polygon.cs, mega settings/tests, Collada schema (quarantine).
9. **Ops/docs drift** — Sphinx describes archived C05 services; duplicate export/IIS/docker scripts; root one-off markdown sprawl.
10. **Cross-chunk ID overlaps** — Folded aliases listed under canonical IDs (C15-002, C16-001, C15-009, C17-006, C19-015, C09-001, C10-001).

---

## 3. Opportunities by chunk

### C01 — Morphology mesh

**Summary:** Contour→slice-graph→Bajaj mesh pipeline (`MorphologyMesh`), Collada export (`ColladaIO`), visual harness (`MonogameTestbed`). Core libs already SDK/net9. LOC whale is generated Collada schema (~16.8k). Top remaining: quarantine generated Collada; retire SmoothMesh husks; automate ReproSet offline. (MeshXNAVizLib, Poly WKT dedup, and several small cleanups are completed.)

#### C01-001 — Quarantine generated Collada 1.5 schema
- **Paths:** `ColladaIO/collada_schema_1_5_fixed.cs` (~16840 lines); hand-written `COLLADA.cs`, `MeshSerializer.cs`, `DynamicRenderMeshColladaSerializer.cs`, `Extensions.cs`
- **Category:** archive
- **Recommendation:** Treat schema as generated artifact (exclude from review/refactor); own only ~615 hand-written lines.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** xsd-generated; ~511 KB / ~295 types; currently compiled with no quarantine.

#### C01-002 — Split `BajajMeshGenerator` god file
- **Paths:** `MorphologyMesh/Generators/BajajMeshGenerator.cs`
- **Category:** split
- **Recommendation:** Extract chord caches/RTree/OTV, theorem validators, face-generation phases into focused types (no algorithm redesign).
- **Effort:** L  
- **Risk:** med  
- **Evidence:** ~2197 lines hosting multiple subsystems.

#### C01-003 — Split BajajTest / share harness helpers
- **Paths:** `Clients/MonogameTestbed/BajajTest.cs`, `BajajMultiTest.cs`
- **Category:** split
- **Recommendation:** Extract ReproSet, HUD/shot helpers, mesh-stage views; share camera/input with MultiTest.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** BajajTest ~2012 LOC; MultiTest ~1628; overlapping Init/Update/Draw/GenerateMesh.

#### C01-004 — Delete duplicate PolyA–D WKT blobs
- **Status:** completed
- **Paths:** `BajajTest.cs`, `BranchAssignmentTest.cs`
- **Category:** dedup
- **Recommendation:** Move polygons to shared `Testdata/` files; load once.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Four strings byte-identical; ~160k chars duplicated per file.

#### C01-005 — Retire SmoothMesh stub stack
- **Paths:** `SmoothMeshGenerator.cs`, `MeshGraph.cs`, `MorphologyTest.cs`, `MeshTest.cs`, `PolywrappingTest.cs`, …
- **Category:** delete
- **Recommendation:** Delete/quarantine SmoothMesh-era types; remove MORPHOLOGY/MESH/POLYWRAPPING modes or retarget to Bajaj.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Stub returns empty mesh; MeshTest has many `return null`; `SmoothMeshGraphGenerator` type missing.

#### C01-006 — Delete dead MeshXNAVizLib husk
- **Status:** completed
- **Paths:** `MeshXNAVizLib/ClassLibrary1/MeshXNAVizLib.csproj`
- **Category:** delete
- **Recommendation:** Remove from tree.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** net461; AssemblyName `ClassLibrary1`; broken MorphologyMesh path; not in Everything.sln.

#### C01-007 — Remove unused NuGet packages from MonogameTestbed
- **Status:** completed
- **Paths:** `Clients/MonogameTestbed/MonogameTestbed.csproj`
- **Category:** modernize
- **Recommendation:** Drop unused PackageReferences (EF6, Microsoft.Net.Http, OData pkgs if only transitive).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Zero CS hits for several listed packages.

#### C01-008 — Delete duplicate SqlServerTypes loaders
- **Status:** completed
- **Paths:** `MonogameTestbed/Loader.cs`, `MonogameTestbed/SqlServerTypes/Loader.cs`
- **Category:** dedup
- **Recommendation:** Keep the one used at startup; delete twin.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Near-identical LoadNativeAssemblies; startup uses one.

#### C01-009 — Remove agent debug logger with absolute path
- **Status:** completed
- **Paths:** `DebugAgentLog.cs`; BajajTest call sites
- **Category:** delete
- **Recommendation:** Delete or gate behind CLI/`#if DEBUG`; no machine paths.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Writes `d:\src\git\VikingLegacy\debug-be41e5.log`; `// #region agent log`.

#### C01-010 — Finish or delete broken Collada production path
- **Paths:** `MorphologyMesh/Serialization/MorphologyColladaView.cs`; ColladaIOTest; BajajMultiTest.SaveMeshes
- **Category:** bug
- **Recommendation:** Implement AddModel from working SaveMeshes pattern, or delete broken API and fix tests.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** `NotImplementedException`; ColladaIOTest always throws via `view.Add`.

#### C01-011 — ColladaIOTest cannot run under `dotnet test`
- **Paths:** `ColladaIOTest/ColladaIOTest.csproj`
- **Category:** test-gap
- **Recommendation:** Add TestAdapter + Test.Sdk; Ignore live-OData scripts.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Framework only, no adapter; lab scripts not assertions.

#### C01-012 — Automate Bajaj ReproSet as offline regression
- **Paths:** `BajajTest.cs` (ReproSet); `MorphologyMeshTest/`
- **Category:** test-gap
- **Recommendation:** Snapshot key Repro polygons; assert GenerateFaces/manifold offline.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** ~42 visual-only Repros; one Morph fixture; diagnostics live-OData.

#### C01-013 — Mesh stage coverage gap (OTV/chords/caps)
- **Paths:** `BajajMeshGenerator.cs`; MorphologyMeshTest
- **Category:** test-gap
- **Recommendation:** Unit-test OTV table, chord validity, first-pass chords, end-caps with synthetic polygons.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Tests cover Delaunay/region/faces; little OTV/chord matrix coverage.

#### C01-014 — Split/archive live-OData diagnostic “tests”
- **Paths:** `MorphologyMeshTest/*Diagnostic.cs`, `ODataPagingTests.cs`, …
- **Category:** archive
- **Recommendation:** Move to diagnostics console; keep CI offline.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** ~1544 LOC diagnostics; many `[Ignore]`; hardcoded lab URLs.

#### C01-015 — Dedup Endpoint maps
- **Status:** completed
- **Paths:** `DataSources.cs`; ColladaIOTest; Morph diagnostics
- **Category:** dedup
- **Recommendation:** Single shared endpoint catalog (config/env).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Same OData bases in ≥3 places.

#### C01-016 — Flatten nested MorphologyMesh/MorphologyMesh/
- **Status:** completed
- **Paths:** `MorphologyMesh/MorphologyMesh/*.cs`
- **Category:** modernize
- **Recommendation:** Flatten folder layout.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Dual layout confuses navigation.

#### C01-017 — Split oversized core type files
- **Paths:** MorphRenderMesh (~970), SliceTopology (~847), SliceGraph (~813), RegionGraphExtensions (~764), MeshAssemblyPlanner (~762), EdgeType (~500+)
- **Category:** split
- **Recommendation:** Separate classification, region-close, assembly, topology init.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Line counts; EdgeType mixes enums + matrices.

#### C01-018 — Dedup edge-classification viz
- **Status:** completed
- **Paths:** BranchAssignmentTest, Delaunay3DTest, EdgeType.cs
- **Category:** dedup
- **Recommendation:** Drive viz from EdgeType colors; remove parallel hand classification.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Identical distinctive comments in both tests.

#### C01-019 — Remove dead NotImplemented API surface
- **Paths:** Theorem1, CreatePointToPolyMap, MorphologyColladaView.AddModel, CleanOutputPath
- **Category:** delete
- **Recommendation:** Delete unused stubs or implement referenced ones.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Public APIs that only throw.

#### C01-020 — Delete commented SmoothMeshGraph block
- **Paths:** `BajajMeshGenerator.cs` (~453–479)
- **Category:** delete
- **Recommendation:** Remove commented region; history in git.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Multi-dozen-line commented block referencing missing type.

#### C01-021 — MonogameTestbed package/TFM cleanup
- **Paths:** `MonogameTestbed.csproj`
- **Category:** modernize
- **Recommendation:** Plan off preview MonoGame; one Drawing stack; review ClickOnce signing.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** `3.8.5-preview.3`; dual Drawing refs; SignManifests + thumbprint.

#### C01-022 — Avoid GeometryTests ref from WinExe testbed
- **Paths:** MonogameTestbed.csproj; VikingDelaunay2DTest; Polygon*
- **Category:** modernize
- **Recommendation:** Move FsCheck explorers to a test project.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** ProjectReference to GeometryTests.

#### C01-023 — Collada/MorphologyMesh boundary hygiene
- **Paths:** MorphologyMesh/Serialization; ColladaIO serializer
- **Category:** split
- **Recommendation:** Keep IColladaScene contracts thin; avoid wrong-layer pulls.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** ColladaIO → MorphologyMesh for IColladaScene.

#### C01-024 — Replace System.Drawing colors in MorphologyMesh
- **Status:** completed
- **Paths:** MorphologyColladaView.cs, ColladaExtensions.cs
- **Category:** modernize
- **Recommendation:** RGBA struct / portable color type for net9 library.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `System.Drawing.Color` in net9 classlib.

#### C01-025 — Clear dead TestMode registrations
- **Paths:** `MonogameTestbed.cs` TestMode; Morphology/Mesh/Polywrapping tests
- **Category:** delete
- **Recommendation:** Remove superseded modes from menu.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Modes registered; MorphologyTest throws on init.

#### C01-026 — Extract shared input trackers
- **Status:** completed
- **Paths:** GamePad/Keyboard trackers across *Test.cs
- **Category:** dedup
- **Recommendation:** Prefer shared manipulators; drop per-test duplicates.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Nearly every graphics test owns trackers.

#### C01-027 — MonogameTestbedTests cover planner UI only
- **Paths:** `Clients/MonogameTestbedTests/*`
- **Category:** test-gap
- **Recommendation:** Keep planner tests; put mesh-algorithm coverage in MorphologyMeshTest.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** ~271 LOC planner/bbox only.

#### C01-028 — Known-failing BajajMesh property test hygiene
- **Paths:** `MorphologyMeshTest/BajajMesh.cs`
- **Category:** test-gap
- **Recommendation:** Explicit quarantine list or delete until region-pairing revisited.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `[Ignore("Known-failing…")]`.

#### C01-029 — Triangle.NET quarantine note
- **Paths:** Triangle.NET; TriangleNetGeometryExtensions
- **Category:** archive
- **Recommendation:** Do not refactor vendored Triangle; optionally drop net48 TFM when unused.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Vendored multi-TFM; consumers are net9.

#### C01-030 — Papers gitignored — keep out of scope
- **Paths:** `MorphologyMesh/Papers/`
- **Category:** archive
- **Recommendation:** Leave local/gitignored; do not commit.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `.gitignore` entry; ~13 MB locally.

#### C01-031 — Hardcoded HTTP lab endpoints in tests
- **Paths:** DataSources.cs; Morph diagnostics; ColladaIOTest
- **Category:** security
- **Recommendation:** Prefer HTTPS/config; avoid baking prod URLs into unit assemblies.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Multiple `http://websvc.codepharm.net/...`.

#### C01-032 — ClickOnce signing residue
- **Status:** completed
- **Paths:** MonogameTestbed.csproj
- **Category:** security
- **Recommendation:** Disable SignManifests unless used; remove thumbprint.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `ManifestCertificateThumbprint=2BADCE24…`.

#### C01-033 — Leftover MorphologyMesh App.config
- **Status:** completed
- **Paths:** `MorphologyMesh/App.config`
- **Category:** delete
- **Recommendation:** Delete if unused by SDK net9 classlib.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Small App.config beside SDK-style project.

#### C01-034 — Large embedded data in VikingDelaunay2DTest
- **Status:** completed
- **Paths:** `VikingDelaunay2DTest.cs`
- **Category:** split
- **Recommendation:** Move payloads to Content/Testdata.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ~616 lines but ~1.1 MB on disk.

#### C01-035 — Corresponding-points NotImplemented still in ReproSet
- **Paths:** MorphRenderMesh.cs; BajajTest.ReproSet
- **Category:** bug
- **Recommendation:** Implement or demote those Repros to expected-failure catalog.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Throws for corresponding points; ReproSet includes that text for locs 100418/100419. ### Cross-chunk (C01) Geometry, GraphLib, AnnotationVizLib, SqlGeometryUtils, RTree, UnitsAndScale, AnnotationVizLibODataClient (C09), Monographics shared (C13). No server project refs — mesh is client/tooling-side; production Collada incomplete. ---

**Cross-chunk:** Geometry, GraphLib, AnnotationVizLib, SqlGeometryUtils, RTree, UnitsAndScale, AnnotationVizLibODataClient (C09), Monographics shared (C13). No server project refs — mesh is client/tooling-side; production Collada incomplete. ---

*Chunk opportunity count (after dedupe): 35*

---

### C02 — Identity

**Summary:** Active Duende 7.3.2 stack: Standalone + WebApi + WebManagement + Identity.Models/DataContext. `source/` IdentityServer3 archive **deleted**. Package/docs/wwwroot LibMan hygiene and permission-query move to Extensions completed. Top remaining: rotate tracked certs/`ro.viking.secret` (C02-003/004); fix ProfileService host registration (C02-008); consolidate duplicate stores/Permissions/Email (C02-005/006/009/010).

#### C02-001 — Delete/archive entire `source/` net45 IdentityServer3 tree
- **Status:** completed
- **Paths:** `Servers/IdentityServer/source/**`, `source/IdentityManager.sln`
- **Category:** archive
- **Recommendation:** Remove from active repo (tag/archive repo) or delete.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** net45/451; Thinktecture IS3; not in IdentityServer.sln/Everything.sln; AspNetIdentity.Tests refs missing projects; 63 files / ~1.8 MB.

#### C02-002 — Hardcoded secrets in legacy source/Host/Web.config
- **Status:** completed
- **Paths:** `source/Host/Web.config`, `source/Host/IdSvr/Certificate.cs`
- **Category:** security
- **Recommendation:** Remove with C02-001; rotate anything still live.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** `EmbeddedX509Password=9220lb8.5x11500`; `VikingClientSecret=secret`; internal SQL/AD hosts.

#### C02-003 — Production TLS material / tempkeys tracked in git
- **Paths:** `IdentityServer/certs/*.pfx|pem`; `**/tempkey.jwk|rsa`
- **Category:** security
- **Recommendation:** Purge from history if real; mount at deploy; rotate.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Tracked despite certs/.gitignore `*`.

#### C02-004 — Hardcoded `ro.viking.secret`; unused RoVikingSecret option
- **Paths:** Standalone `Config.cs`; VikingIdentityServerOptions; ClientStore
- **Category:** security
- **Recommendation:** Env/user-secrets only; prefer ClientStore-only defs.
- **Effort:** S  
- **Risk:** high  
- **Evidence:** `new Secret("ro.viking.secret".Sha256())`; option never read.

#### C02-005 — Dual InMemory clients/resources + custom stores
- **Paths:** Standalone Program.cs, Config.cs, ClientStore, ResourceStore
- **Category:** bug
- **Recommendation:** Keep custom stores; drop AddInMemoryClients / redundant Config.GetClients.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Both registration paths; drift risk for secrets/scopes.

#### C02-006 — Near-duplicate ResourceStore in Standalone and WebManagement
- **Paths:** both `IdentityServerCustomResourceStore.cs`
- **Category:** dedup
- **Recommendation:** One copy in Extensions; delete Management copy.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ~177 matching body lines; Management never AddResourceStore.

#### C02-007 — Dead IdentityServer leftovers in WebManagement
- **Status:** completed
- **Paths:** Config.Get*, ParameterizedScopeParser, PersistedGrantDb IS4 entities
- **Category:** delete
- **Recommendation:** Keep AuthenticationSchemes only; delete unused store/parser/IS4 migrations.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** JWT/introspection only; IS4 entity snapshots remain.

#### C02-008 — IProfileService on WebManagement, not Standalone
- **Paths:** WebManagement Program.cs; Extensions profile service; Standalone Program.cs
- **Category:** bug
- **Recommendation:** Register on Standalone; remove from Management.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Management has profile service without AddIdentityServer.

#### C02-009 — Duplicate Permissions API
- **Paths:** WebApi + WebManagement PermissionsController
- **Category:** dedup
- **Recommendation:** Single impl in WebApi; Management proxy or drop.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Parallel endpoints; Management comment “temporary workaround”.

#### C02-010 — Two EmailSender stacks
- **Paths:** Standalone EmailSender; Extensions EmailSender + EmailOptions
- **Category:** dedup
- **Recommendation:** One MailKit sender in Extensions; Standalone adapts.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Parallel options/senders; divergent EnableSending defaults.

#### C02-011 — Docker/config explosion
- **Paths:** compose×2, Dockerfiles×4, appsettings×18
- **Category:** dedup
- **Recommendation:** One compose entrypoint; shared templates; slim images.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Root Dockerfile ~266 lines + supervisor + vsdbg; duplicated env blocks.

#### C02-012 — Unused ConfigConnection / IdentityConfig DB
- **Paths:** appsettings, docker-compose env
- **Category:** delete
- **Recommendation:** Remove unless restoring AddConfigurationStore.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ConfigConnection only in json/yml; no AddConfigurationStore in C#.

#### C02-013 — Dual EF: AspNet Identity vs Duende operational store
- **Paths:** Identity.DataContext; Standalone PersistedGrant migrations; Management IS4 copies
- **Category:** modernize
- **Recommendation:** Keep ApplicationDbContext shared; PersistedGrant only on Standalone; delete Management IS4 copies; document two DBs.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** IdentityViking + IdentityPersistedGrants; Management 2020 IS4 snapshots.

#### C02-014 — Package version skew
- **Status:** completed
- **Paths:** Identity *.csproj
- **Category:** modernize
- **Recommendation:** Align Duende 7.3.x and EF/Identity 9.0.x; drop Http.Abstractions 2.2.0 if possible.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 7.3.2 vs 7.3.1; Identity 9.0.9 vs EF 9.0.8.

#### C02-015 — Naming/namespace hygiene
- **Status:** completed
- **Paths:** Identity.Models RootNamespace; `Extentions/` typo; validator namespace
- **Category:** modernize
- **Recommendation:** Fix RootNamespace; rename folder; move validator off WebManagement namespace.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** RootNamespace=`IdentityServer.Models`; folder typo.

#### C02-016 — DataContext polluted with xUnit / middleware
- **Status:** completed
- **Paths:** DataContext Startup.cs; InitializeDatabase(IApplicationBuilder)
- **Category:** split
- **Recommendation:** Move test attribute to Identity.Tests; move migrate helper to host/Extensions.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** xunit package on library; CollectionBehavior in DataContext.

#### C02-017 — Permission query logic in giant DataContext extensions
- **Status:** completed
- **Paths:** ApplicationDBContextExtensions.cs (~27KB)
- **Category:** split
- **Recommendation:** Push algorithms into Extensions IPermissionService; thin DataContext.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Largest non-migration source; Management bypasses service.

#### C02-018 — Overlapping Identity docs / port contradictions
- **Status:** completed
- **Paths:** README.rst (~40KB), README.md, README-Docker-All.md
- **Category:** dedup
- **Recommendation:** One canonical README; fix Standalone 6000 vs Docker 5000 role swap.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Port roles reversed across docs.

#### C02-019 — Vendored wwwroot/lib
- **Status:** completed
- **Paths:** WebManagement/wwwroot/lib/*
- **Category:** modernize
- **Recommendation:** LibMan/npm; stop committing vendor trees.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** bootstrap/jquery MBs under lib.

#### C02-020 — Utility/sample projects clutter solution
- **Status:** completed
- **Paths:** Client/ (net48), SmtpTest/, DevTestAPI/; keep DevTest
- **Category:** archive
- **Recommendation:** Move samples to tools/; exclude from default build; keep DevTest for gRPC tests.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Client is IdentityModel sample; DevTest wired in compose.

#### C02-021 — Stub Management endpoints
- **Status:** completed
- **Paths:** UserRolesController Details empty; scaffold TODOs
- **Category:** delete
- **Recommendation:** Finish or remove dead actions/views.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty Details View(); commented scaffolds.

#### C02-022 — Test gap for hosts/HTTP APIs
- **Paths:** Identity.Tests
- **Category:** test-gap
- **Recommendation:** WebApplicationFactory for Permissions + discovery/token.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Tests cover Models/DataContext/Extensions only; no host refs.

#### C02-023 — Misleading “in-memory operational store” log
- **Paths:** Standalone Program.cs
- **Category:** bug
- **Recommendation:** Fix/remove stale log after SQL PersistedGrant migrate.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** AddOperationalStore + Migrate, then “in-memory” log.

#### C02-024 — Resource Owner Password still first-class
- **Paths:** ClientStore, Config, DevTest/Config
- **Category:** modernize
- **Recommendation:** Confine ROPC to DevTest; prod → auth code + PKCE.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** ro.viking + several clients allow ROPC.

#### C02-025 — vsdbg in production multi-service image
- **Paths:** IdentityServer/Dockerfile
- **Category:** security
- **Recommendation:** Debugger only in debug stage.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** getvsdbgsh in base used by final.

#### C02-026 — Large commented Config blocks
- **Paths:** Standalone/WebManagement Config.cs
- **Category:** delete
- **Recommendation:** Delete commented GetApiResources/GetApiScopes corpses.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Multi-block `/* */` dead code.

#### C02-027 — Active stack already on Duende 7
- **Status:** completed
- **Paths:** Standalone/WebManagement/Extensions csprojs
- **Category:** modernize
- **Recommendation:** Treat Duende migration done; only clean IS3/IS4 remnants (source/, old snapshots).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Duende 7.3.x on active hosts.

#### C02-028 — Models/DataContext/Server split mostly correct
- **Status:** completed
- **Paths:** Identity.Models, DataContext, apps
- **Category:** split
- **Recommendation:** Preserve three-way split; fix leakage (queries, duplicated host stores).
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Clean entity namespace; leakage in query/store duplication.

#### C02-029 — ApplicationUser extension helpers duplicated
- **Status:** completed
- **Paths:** Extensions + WebManagement + Models Extentions
- **Category:** dedup
- **Recommendation:** One extension set.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Three similarly named classes.

#### C02-030 — Migration Designer bloat
- **Paths:** Identity.DataContext/Migrations/*.Designer.cs
- **Category:** modernize
- **Recommendation:** Optional squash after backup; don’t hand-edit.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** ~6.8k migration Designer LOC. ### Cross-chunk (C02) Viking/Jotunn/Tokens (C11); gRPC Annotation + DevTest compose; Docker/reverseproxy ports; SQL IdentityViking + IdentityPersistedGrants; Annotation/OData permission scopes. ---

**Cross-chunk:** Viking/Jotunn/Tokens (C11); gRPC Annotation + DevTest compose; Docker/reverseproxy ports; SQL IdentityViking + IdentityPersistedGrants; Annotation/OData permission scopes. ---

*Chunk opportunity count (after dedupe): 30*

---

### C03 — Segmentation

**Summary:** Live path: Python SegmentationServer (SAM2) ↔ `gRPC_Protos/.../segmentation.proto` ↔ SegmentationServiceTypes.gRPC ↔ Viking SegmentationCommand. `gRPCSegmentAnything` is abandoned C# stub. Top 3: fix proto↔Python GetServerStatus drift; delete dead C#/Generated copies; Docker security hygiene + tests.

#### C03-001 — Archive dead C# SegmentAnything prototype
- **Paths:** `Servers/gRPCSegmentAnything/`
- **Category:** archive
- **Recommendation:** Remove; incompatible API; not in Everything.sln/compose; does not match generated types.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Different proto (SegmentByPoints); not in solution; echo stub.

#### C03-002 — Proto drift: GetServerStatus missing from canonical proto
- **Paths:** gRPC_Protos segmentation.proto; Python pb2*/server.py
- **Category:** bug
- **Recommendation:** Restore RPC to submodule proto or remove from Python and regen.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Proto lacks GetServerStatus; Python stubs/server implement it.

#### C03-003 — Committed Python stubs + import-time codegen
- **Paths:** segmentation_grpc `*_pb2*.py`, `__init__.py`, generate_grpc.py
- **Category:** modernize
- **Recommendation:** CI regen + fail on drift; stop import-time generate; fix mtime skip.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** generate at import; stubs ahead of proto.

#### C03-004 — Stale Generated/python package
- **Paths:** `Generated/python/segmentation_grpc/`
- **Category:** delete
- **Recommendation:** Delete; old SegmentImage-only proto.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Tracked duplicate; not live package path.

#### C03-005 — Local segment.proto violates gRPC_Protos rule
- **Paths:** gRPCSegmentAnything/Protos/segment.proto
- **Category:** delete
- **Recommendation:** Delete with C03-001; no new protos under Servers/*/Protos.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Cursor grpc-protos-folder rule.

#### C03-006 — SegmentationServiceTypes.gRPC codegen hygiene
- **Paths:** SegmentationServiceTypes.gRPC.csproj
- **Category:** modernize
- **Recommendation:** Align RootNamespace with csharp_namespace; bump Grpc packages; keep dual TFM for Viking.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Namespace mismatch; packages 3.17.2 / 2.38.0 vs newer elsewhere.

#### C03-007 — Legacy Grpc.Core channel for segmentation
- **Paths:** NGVV GrpcChannelManager; types package; SegmentationCommand
- **Category:** modernize
- **Recommendation:** Migrate toward Grpc.Net.Client; address Insecure credentials via proxy.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** ChannelCredentials.Insecure; Grpc.Core on types package. *(Cross: C15/C11)*

#### C03-008 — Docker image security and build fragility
- **Paths:** SegmentationServer Dockerfile, start.sh, compose volumes
- **Category:** security
- **Recommendation:** Remove ssh/password/sudo; fix SAM2 YAML fetch; env-driven volumes; drop fake web UI 8080.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** openssh + user:user; wget GitHub blob; hardcoded Windows paths.

#### C03-009 — Upstream README noise
- **Paths:** SegmentationServer README.md, LICENSE
- **Category:** modernize
- **Recommendation:** Viking-specific runbook (compose, proto regen, GPU).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Markets peasant98/sam2 / ROS.

#### C03-010 — Unused SAM2AutomaticMaskGenerator
- **Paths:** segmentation_service.py
- **Category:** delete
- **Recommendation:** Remove unless auto-segment RPC planned.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Constructed; never referenced.

#### C03-011 — MultiSegmentImage unused by clients
- **Paths:** proto; server.py; no C# caller
- **Category:** test-gap
- **Recommendation:** Wire from client or document experimental + smoke test.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** No C# MultiSegment usage.

#### C03-012 — Python SegmentationClient brittle
- **Paths:** Clients/SegmentationClient/
- **Category:** test-gap
- **Recommendation:** Fix module name; cover Upload/Delete; or archive if Viking-only.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Wrong `-m SegmentationServer`; hardcoded ~/SAM2-Docker paths.

#### C03-013 — No automated server tests
- **Paths:** Servers/SegmentationServer/
- **Category:** test-gap
- **Recommendation:** Unit-test ImageCache; contract tests with mocked predictor.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Zero test files.

#### C03-014 — Dual setup.py + pyproject.toml
- **Paths:** segmentation_grpc + segmentation_server packaging
- **Category:** dedup
- **Recommendation:** pyproject-only; pin Python ≥3.11/3.13.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Parallel metadata; Dockerfile uses 3.13; requires-python ≥3.7.

#### C03-015 — Missing proto readme referenced by IMPLEMENTATION-SUMMARY
- **Paths:** claimed segmentation.proto.readme.rst
- **Category:** delete
- **Recommendation:** Add to submodule or remove reference.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** File absent; submodule only has proto.

#### C03-016 — No merge of Python vs C# hosts
- **Paths:** SegmentationServer vs gRPCSegmentAnything
- **Category:** dedup
- **Recommendation:** Archive C# (C03-001); production is Python only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Distinct APIs; only Python in compose. ### Cross-chunk (C03) gRPC_Protos submodule; Viking WebAnnotation/NGVV (C15/C17); Jotunn SegmentationServiceUrl; Identity volume settings; docker-compose + reverseproxy 50051; Annotation gRPC dual-TFM pattern (C10/C19). ---

**Cross-chunk:** gRPC_Protos submodule; Viking WebAnnotation/NGVV (C15/C17); Jotunn SegmentationServiceUrl; Identity volume settings; docker-compose + reverseproxy 50051; Annotation gRPC dual-TFM pattern (C10/C19). ---

*Chunk opportunity count (after dedupe): 16*

---

### C04 — Export portal

**Summary:** Static HTML/JS/CSS portal (~1.5k LOC, no TFM, not in .sln) driving per-volume DataExport. Already modern vs Utah UI. Top 3: externalize hosts/seed; XSS on `?root=`; retire predecessor UI inside DataExport (C20).

#### C04-001 — Hardcoded production service hosts
- **Paths:** `ExportPortal/js/portal.js` (IDENTITY_VOLUME_TREE, DEFAULT_SERVICE_ROOT)
- **Category:** modernize
- **Recommendation:** Externalize via config.json / build inject.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** identity.codepharm.net:6001 and websvc.codepharm.net constants.

#### C04-002 — Hardcoded SEED_VOLUMES inventory drift
- **Paths:** portal.js SEED_VOLUMES
- **Category:** dedup
- **Recommendation:** Prefer identity tree; seed as optional override.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 13 hard-coded names.

#### C04-003 — Dead image tulip.png
- **Paths:** `ExportPortal/img/tulip.png`
- **Category:** delete
- **Recommendation:** Delete unused asset.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Not referenced in HTML/JS.

#### C04-004 — Unused CSS rules
- **Paths:** portal.css
- **Category:** delete
- **Recommendation:** Remove `.step.disabled`, `.lengthbar-fill.bad` or implement.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Classes never toggled.

#### C04-005 — `?root=` reflected into innerHTML (XSS)
- **Paths:** portal.js deriveServiceRoot / loadVolumes
- **Category:** security
- **Recommendation:** textContent/createElement; validate http(s) root.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** SERVICE_ROOT embedded in innerHTML after query override.

#### C04-006 — Monolithic portal.js
- **Paths:** portal.js (~968 LOC)
- **Category:** split
- **Recommendation:** ES modules (config, parse, odata, volumes, download, ui).
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Single file owns all concerns.

#### C04-007 — No automated tests for portal logic
- **Paths:** ExportPortal/
- **Category:** test-gap
- **Recommendation:** Unit-test parseEntries/range/separator rules.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Pure JS business rules; no tests.

#### C04-008 — Unoptimized hero/card images
- **Paths:** img/*.jpg
- **Category:** modernize
- **Recommendation:** Compress/WebP; banner/network dominate.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** banner ~408KB, network ~546KB.

#### C04-009 — Route catalog duplicated vs DataExport
- **Paths:** portal.js REPORTS; DataExport Controllers
- **Category:** dedup
- **Recommendation:** Generate REPORTS from OpenAPI/shared JSON or `/Export/capabilities`.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Hard-coded paths; legacy GetTLP aliases only on server. *(Cross: C20)*

#### C04-010 — Predecessor UI still in DataExport
- **Paths:** DataExport Scripts/src/source.js, Views/, Scripts/** (~3.4MB)
- **Category:** delete
- **Recommendation:** Delete unused Razor/vendor Scripts (C20 work).
- **Effort:** M  
- **Risk:** low  
- **Evidence:** source.js targets utah.edu; portal is replacement. *(Cross: C20)*

#### C04-011 — Stale per-volume help Index.html
- **Paths:** DataExport Content/Index.html; MapFallbackToFile
- **Category:** delete
- **Recommendation:** Point to /Export/ or true 404.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Documents utah hosts, Motifs plural, DAE. *(Cross: C20)*

#### C04-012 — Morphology JSON offered despite empty output
- **Paths:** portal.js hint; Morphology JSON in C20
- **Category:** bug
- **Recommendation:** Hide JSON until fixed, or fix MorphologyJSONView.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** UI warns “returned empty by the service.” *(Cross: C20)*

#### C04-013 — Same-origin / no-CORS constraint
- **Paths:** portal.js SAME_ORIGIN; DataExport no CORS
- **Category:** modernize
- **Recommendation:** Keep IIS co-hosting; only add strict CORS if required.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Cross-origin falls back to GET + URL limits. *(Cross: C20)*

#### C04-014 — Deploy web.config missing security headers
- **Paths:** Scripts/Deploy-ExportPortal.ps1
- **Category:** security
- **Recommendation:** CSP + X-Content-Type-Options; pair with C04-005.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Deployed config is redirect/handler/default-doc only. *(Cross: C07)*

#### C04-015 — No build pipeline / package metadata
- **Paths:** ExportPortal/
- **Category:** modernize
- **Recommendation:** Optional eslint/esbuild; README pointing to deploy script.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Raw copy deploy; no package.json.

#### C04-016 — Secrets inventory clean
- **Paths:** ExportPortal/**
- **Category:** security
- **Recommendation:** No secret rotation needed; treat hostnames as env config (C04-001).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Public HTTPS only; no passwords/keys.

#### C04-017 — Unused legacy export endpoints (C20)
- **Paths:** DataExport legacy GetX routes; commented DAE
- **Category:** delete
- **Recommendation:** Deprecate after telemetry; portal already omits DAE/legacy forms.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Portal generates short routes only. *(Cross: C20)*

#### C04-018 — downloadOne double filenameFromResponse
- **Paths:** portal.js downloadOne
- **Category:** delete
- **Recommendation:** Call once; reuse name.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Invoked twice on same response. ### Cross-chunk (C04) Hard dep on C20 DataExport; Identity WebApi volume tree; per-volume OData; C07 Deploy-ExportPortal; Sphinx export docs. ---

**Cross-chunk:** Hard dep on C20 DataExport; Identity WebApi volume tree; per-volume OData; C07 Deploy-ExportPortal; Sphinx export docs. ---

*Chunk opportunity count (after dedupe): 18*

---

### C05 — Dead client/web (thorough)

**Summary:** All five trees unused by active Viking/Jotunn/ODataV4/DataExport path; no docker-compose, reverseproxy, or CI hits.   **Safe DELETE:** AnnotationVizLibOData stub, ConnectomeOpenData (csproj missing since 2015), ConnectomicsWebsite.   **ARCHIVE then remove:** Bifrost, WebVisualization.   **Keep (not C05):** `AnnotationVizLibODataClient` (C09), `ConnectomeODataV4`.

#### C05-001 — Bifrost experimental WPF/MonoGame client — ARCHIVE
- **Paths:** `Clients/Bifrost/` (~5.7k src LOC; nested Monographics/MonogameTestbed)
- **Category:** archive
- **Recommendation:** Archive then remove; strip from Everything.sln + Clients/Viewer.sln. Do not confuse nested Bifrost copies with live Clients/Monographics|MonogameTestbed.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** In Everything.sln + Viewer.sln; no ProjectReferences from Viking/Jotunn; empty Bifrost/Jotunn; no docker/CI product use.

#### C05-002 — ConnectomeOpenData — DELETE
- **Paths:** `Servers/ConnectomeOpenData/`
- **Category:** delete
- **Recommendation:** Delete folder; remove dead entry from Connectome Project.sln.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ConnectomeOpenData.csproj **absent** (deleted ~2015); remnants only (.svc, JSONP, publish); no ConnectomeDataService class; replaced by ConnectomeODataV4.

#### C05-003 — ConnectomicsWebsite — DELETE
- **Paths:** `Servers/ConnectomicsWebsite/`
- **Category:** delete
- **Recommendation:** Delete; drop orphan ConnectomicsWebsite.sln (tests path outside repo).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Stock MVC scaffold; not in Everything.sln; no ProjectReferences; no docker/CI.

#### C05-004 — WebVisualization / ConnectomeViz — ARCHIVE
- **Paths:** `Servers/WebVisualization/` (~17k app LOC + large vendor; ~32MB+)
- **Category:** archive
- **Recommendation:** Archive then delete; confirm no lingering external IIS.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Not in Everything.sln; namespace only internal; HintPaths to developer desktop; no compose/reverseproxy refs.

#### C05-005 — AnnotationVizLibOData stub — DELETE
- **Paths:** `Servers/AnnotationVizLibOData/`
- **Category:** delete
- **Recommendation:** Safe delete. Keep root `AnnotationVizLibODataClient/` (C09).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** net451 stub; ErrorGeneratingOutput T4; **zero** .sln entries; **zero** ProjectReferences; distinct from live AnnotationVizLib.OData net9 client.

#### C05-006 — Solution hygiene follow-ups
- **Paths:** Everything.sln, Viewer.sln, Connectome Project.sln, ConnectomicsWebsite.sln
- **Category:** delete
- **Recommendation:** When removing trees, prune Bifrost/OpenData entries and orphan sln.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Bifrost still Build.0 in Everything; OpenData entry broken. ### Cross-chunk (C05) C09 AnnotationVizLibODataClient (live — do not delete); C20 ConnectomeODataV4 successor; AnnotationVizLib historical `.svc` URL strings only; live ColladaIO ≠ WebVisualization/ColladaGenerator; Bifrost nested Monographics ≠ Clients/Monographics. ---

**Cross-chunk:** C09 AnnotationVizLibODataClient (live — do not delete); C20 ConnectomeODataV4 successor; AnnotationVizLib historical `.svc` URL strings only; live ColladaIO ≠ WebVisualization/ColladaGenerator; Bifrost nested Monographics ≠ Clients/Monographics. ---

*Chunk opportunity count (after dedupe): 6*

---

### C06 — Neo4j

**Summary:** Generator (net9 JSON→Neo4j) + Service (net48 Cypher proxy, ~80% HelpPage LOC). In Everything.sln but **no Docker/reverseproxy/client consumers**; `.dockerignore` excludes both. Top 3: rotate committed Neo4j password; archive stack after ops confirm; if kept, strip template + close open Cypher proxy.

#### C06-001 — No runtime consumers; archive candidate
- **Paths:** Neo4JGenerator/, Neo4JService/, Neo4JService.Tests/
- **Category:** archive
- **Recommendation:** Confirm no live IIS/batch; drop from default builds or archive.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Zero client refs; no Docker/reverseproxy; .dockerignore lists both; only docs/scripts mention Generator.

#### C06-002 — Generator vs Service complementary, not shared
- **Paths:** Program.cs; QueryController.cs
- **Category:** dedup
- **Recommendation:** Do not merge unless resurrecting; optional tiny shared driver helper.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No project reference between them; write-CLI vs read-API.

#### C06-003 — Heavy ASP.NET template dead weight
- **Paths:** Areas/HelpPage/**, Content/Scripts, Identity/OAuth scaffolding
- **Category:** delete
- **Recommendation:** Strip HelpPage (~2441/~3005 CS LOC) and unused OAuth if Service kept.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** No AccountController; [Authorize] commented on QueryController.

#### C06-004 — Stale credentials in Tests App.config
- **Paths:** Neo4JService.Tests/App.config
- **Category:** security
- **Recommendation:** Rotate password; remove from git; use secrets/env.
- **Effort:** S  
- **Risk:** high  
- **Evidence:** `bolt://155.100.106.48:7687`, user Neo4JWebService, password `4%w%o06` (verified).

#### C06-005 — README env names mismatch AppSettings mapping
- **Paths:** Neo4JService README; WebAppSettings mapper
- **Category:** bug
- **Recommendation:** Document NEO4JDATABASE/USER/PASSWORD (dots→underscore→upper), not NEO4J_DATABASE.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Mapper Replace(".", "_").ToUpperInvariant().

#### C06-006 — Open Cypher proxy + weak write filter
- **Paths:** QueryController.cs; WebApiConfig
- **Category:** security
- **Recommendation:** Require auth + read-only role, or retire service.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** //[Authorize] commented; substring keyword blocklist; body executed as-is.

#### C06-007 — Generator default credentials + string-built Cypher
- **Paths:** CommandLineOptions.cs; Program.cs EncodeProperty
- **Category:** security
- **Recommendation:** Require secrets; parameterized UNWIND Cypher.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Defaults Anonymous/connectome; quote concatenation.

#### C06-008 — Spatial enrichment broken vs current OData
- **Paths:** SpatialAdapter.cs StructureSpatialViews; ConnectomeODataV4 StructureSpatialCaches
- **Category:** bug
- **Recommendation:** Delete --odata path or fix entity set name.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Generator queries Views; OData exposes StructureSpatialCaches. *(Cross: C09/C20)*

#### C06-009 — Overlap with DataExport network JSON
- **Paths:** Generator; AnnotationVizLib NeuronJSONView; DataExport NetworkController
- **Category:** dedup
- **Recommendation:** Keep JSON production in DataExport; Generator is sink only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Fixture matches nw-* hops export schema. *(Cross: C20)*

#### C06-010 — Unused Generator project/package refs
- **Paths:** Neo4JGenerator.csproj, packages.config
- **Category:** delete
- **Recommendation:** Drop unused Geometry/SqlGeometryUtils/GraphLib refs; delete stale packages.config.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No usings for those libs in *.cs.

#### C06-011 — Dead methods / unused options in Generator
- **Paths:** Program.cs, SpatialAdapter, CommandLineOptions
- **Category:** delete
- **Recommendation:** Remove CreateIDIndex, Quiet unused, duplicate fetchers.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** CreateIDIndex never called; Quiet never read.

#### C06-012 — Driver/TFM/tests inconsistency
- **Paths:** both csproj; Tests; test_command_line_errors.ps1
- **Category:** modernize
- **Recommendation:** Archive Tests or modernize Service + Driver 5.x together; fix CLI script TFM path.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Apps Driver 1.3; Tests 5.15; script expects net48 exe for net9 Generator.

#### C06-013 — Large committed JSON fixture
- **Paths:** nw-ALL_hops_1.json (~6.2MB)
- **Category:** archive
- **Recommendation:** LFS / generate on demand / tiny sample only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** CopyToOutput; DataExport naming pattern.

#### C06-014 — AllowInsecureHttp + LocalDb Identity store
- **Paths:** Startup.Auth.cs; Web.config connectionStrings
- **Category:** security
- **Recommendation:** Remove with Identity scaffolding (C06-003).
- **Effort:** S  
- **Risk:** med  
- **Evidence:** AllowInsecureHttp=true; unused LocalDb Identity MDF. ### Cross-chunk (C06) C20 DataExport JSON producer; C09 OData spatial name drift; WebAppSettings; unused Geometry/GraphLib refs; no Identity/Docker integration. ---

**Cross-chunk:** C20 DataExport JSON producer; C09 OData spatial name drift; WebAppSettings; unused Geometry/GraphLib refs; no Identity/Docker integration. ---

*Chunk opportunity count (after dedupe): 14*

---

### C07 — Ops + docs

**Summary:** Live ops: Aug/Sep 2026 Export Deploy/Diagnose scripts. Windows combined IIS Docker pack is incomplete (missing compose/docs). Sphinx mostly 2015-era with a few current pages (mesh, sbfsem-tools). Top 3: decide/fix-or-archive combined Docker; keep Export scripts; triage Sphinx (preserve mesh + sbfsem-tools).

#### C07-001 — Missing combined-Docker artifacts still referenced
- **Paths:** absent docker-compose.combined.yml, Docker-Combined-Services-README.md, etc.; refs in Everything.sln, ValidateDockerSetup.ps1, VolumeAnnotationServices README
- **Category:** bug
- **Recommendation:** Restore from backup or remove all references/Solution Items.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Test-Path false; never in git ls-files; Validate requires missing compose.

#### C07-002 — Combined Docker container rename incomplete
- **Paths:** BuildAndRunCombined.ps1; TestDockerImage.ps1; RELOCATION-NOTES
- **Category:** bug
- **Recommendation:** Align image and container to viking-annotation-services (or revert docs).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Image new name; container still viking-services.

#### C07-003 — VolumeAnnotationServices is packaging only, not live compose
- **Paths:** VolumeAnnotationServices/Dockerfile, README
- **Category:** archive
- **Recommendation:** Keep only if Windows IIS multi-service image is a goal; else archive with combined scripts. Active Docker is Linux compose (gRPC/Identity/segmentation).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** No csproj; not in root docker-compose.yml; “Needs testing” history.

#### C07-004 — IIS config duplicated Dockerfile vs ConfigureIIS.ps1
- **Paths:** VolumeAnnotationServices/Dockerfile; Scripts/ConfigureIIS.ps1
- **Category:** dedup
- **Recommendation:** Single source of truth for pools/apps.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Same AnnotationService/OData/DataExport pools in both.

#### C07-005 — Combined Docker script pack provisional/obsolete
- **Paths:** BuildAndRunCombined*, ValidateDockerSetup, TestDockerImage, DOCKER-COMBINED-*
- **Category:** archive
- **Recommendation:** Archive as unit if C07-003 archives image; else fix 001/002 first.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Validate fails without compose; docs describe nonexistent files.

#### C07-006 — Live IIS Export ops scripts — KEEP
- **Paths:** Deploy-ExportApplication.ps1, Deploy-ExportPortal.ps1, Diagnose-ExportDeployments.ps1
- **Category:** modernize
- **Recommendation:** Keep; optional DRY helpers; parameterize host defaults.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Recent 2026-08/09 history; production IIS Export path. *(Cross: C04/C20)*

#### C07-007 — Host-hardcoded DiagnoseIIS / FixIISPermissions
- **Paths:** DiagnoseIIS.ps1, FixIISPermissions.ps1
- **Category:** archive
- **Recommendation:** Superseded by Diagnose-ExportDeployments; archive or parameterize.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Hard-coded NeitzTemporalMonkey / net9-TemporalMonkey paths.

#### C07-008 — FindDuplicateDlls + SharedAssemblies.txt unused
- **Paths:** FindDuplicateDlls.ps1, SharedAssemblies.txt
- **Category:** archive
- **Recommendation:** Archive or wire into packaging; no csproj consumes SharedAssemblies.txt.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Hardcoded Viking net48 Debug path; no project refs.

#### C07-009 — Duplicate/divergent root Docker documentation
- **Paths:** DOCKER-COMBINED-* vs Docker-Configuration-README.md + docker-compose.yml
- **Category:** dedup
- **Recommendation:** One active Docker story (Linux compose); mark Windows combined experimental/legacy.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Competing stacks/ports; both as Solution Items.

#### C07-010 — Sphinx tree largely outdated (2015)
- **Paths:** Documentation/source/conf.py, server/*, Client/*
- **Category:** archive
- **Recommendation:** Mark historical or rewrite; remove VS2013/XNA/IdentityServer3/SQL2014 claims.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** copyright 2015; IdentityServer3 links; Jotunn “missing annotations”.

#### C07-011 — Keep high-value current Sphinx pages
- **Paths:** developerdocs/mesh/overview.rst; server/Identity/sbfsem-tools.rst
- **Category:** modernize
- **Recommendation:** Preserve or promote to Markdown next to code/skills.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** contours-to-mesh skill refs mesh overview; sbfsem-tools committed 2026-08-30.

#### C07-012 — Orphan methods papers + triple export formats
- **Paths:** Documentation/source/methods/*
- **Category:** archive
- **Recommendation:** Keep one format (.md); archive PDF/DOCX/HTML duplicates.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Not in any toctree; HTML duplicates MD.

#### C07-013 — Empty/broken Sphinx leaves
- **Paths:** export/excel.rst (0 bytes); index.rst link target mismatch
- **Category:** delete
- **Recommendation:** Delete/fill excel.rst; fix 4.8 text vs 4.6 link; fix useage typo.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 0-byte file; link definition mismatch.

#### C07-014 — Dual Sphinx build dirs + Eclipse scaffolding
- **Paths:** Documentation/build/, _build/, .venv-docs/, .project/.pydevproject
- **Category:** modernize
- **Recommendation:** One BUILDDIR; drop Eclipse metadata; add requirements.txt.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Makefile BUILDDIR=build; gitignore _build; both trees exist.

#### C07-015 — Docs sprawl / broken Store + cert placement
- **Paths:** Documentation/README.md; MicrosoftStoreSubmission.md; IdentityServer-cert-renewal.md
- **Category:** modernize
- **Recommendation:** Move cert-renewal to IdentityServer; fix/remove CreateStorePackage ref; Sphinx build README.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Missing Scripts/CreateStorePackage.ps1; cert doc under Documentation/.

#### C07-016 — Secrets/credentials posture in docs/scripts
- **Paths:** identity.rst default admin; Deploy-* host defaults; BuildAndRunCombined -DBPassword
- **Category:** security
- **Recommendation:** Caveat/remove default admin from published docs; prefer env mounts over CLI passwords.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** identity.rst documents default admin password; no hardcoded passwords in Scripts themselves.

#### C07-017 — No SQL under Scripts/ (discovery)
- **Paths:** Scripts/ (PS1 only); SQL in AnnotationDatabase/GrpcAnnotationService
- **Category:** archive
- **Recommendation:** Do not invent SQL under Scripts/; consolidate with C14/C19.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Scripts = 12 PS1/bat/txt; no SQL. *(Cross: C14)*

#### C07-018 — Solution Items / compose hygiene
- **Paths:** Everything.sln Solution Items; docker-compose.yml
- **Category:** delete
- **Recommendation:** Prune Solution Items to existing current files.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Broken items from C07-001; active compose = identity-devtest + grpc + segmentation. ### Cross-chunk (C07) VolumeAnnotationServices Dockerfile → AnnotationService (C21), ODataV4 (C20), DataExport (C20); Export scripts → C04/C20; Sphinx Identity vs live Duende (C02); mesh overview → C01; Linux compose → C03/C19/C02; database.rst → C14. ---

**Cross-chunk:** VolumeAnnotationServices Dockerfile → AnnotationService (C21), ODataV4 (C20), DataExport (C20); Export scripts → C04/C20; Sphinx Identity vs live Duende (C02); mesh overview → C01; Linux compose → C03/C19/C02; database.rst → C14. --- ## Wave 1 cross-cutting themes (for parent merge) 1. **Safe deletes first:** C05 stubs (OpenData, ConnectomicsWebsite, AnnotationVizLibOData), C01 SmoothMesh husks (MeshXNAVizLib completed), C03 gRPCSegmentAnything + Generated/python, C02 `source/` (**completed**). 2. **Archive after confirm:** Bifrost, WebVisualization, Neo4j stack, VolumeAnnotationServices combined Windows Docker. 3. **Secrets (do before archive):** Identity certs/tempkeys/`ro.viking.secret`/source Web.config; Neo4JServi

*Chunk opportunity count (after dedupe): 18*

---

### C08 — Geometry foundation

**Summary:** Shared math layer (~32k LOC): Geometry.Core primitives (~11.8k), Geometry meshing/transforms/indexes (~16.9k), GraphLib (~0.7k), custom RTree fork (~1.9k), Utils/UnitsAndScale/SIMeasurement (~0.4k). **Top 3:** (1) Delete dead stubs/orphan `*_4.8` projects. (2) Remove remaining BinaryFormatter transform paths. (3) Clarify RTree vs QuadTree vs BoundingBoxIndex strategy; drop log4net from RTree.

#### C08-001 — Delete dead QuadTreeTemplatePoint duplicate
- **Paths:** `Geometry/DataStructures/QuadTreeTemplatePoint.cs`, `QuadTreeNodeTemplatePoint.cs`
- **Category:** delete
- **Recommendation:** Remove both; live code uses `QuadTree<T>` only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `rg QuadTreeTemplatePoint` hits only those files (~476 LOC).

#### C08-002 — Delete commented-out GridTransformFactory
- **Paths:** `Geometry/GridTransformFactory.cs`
- **Category:** delete
- **Recommendation:** Delete; parsing lives in `Transforms/TransformFactory.cs`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Entire type in `/* */`; no references (~253 LOC).

#### C08-003 — Delete empty MeshPathing stub
- **Paths:** `Geometry/Meshing/Algorithms/MeshPathing.cs`
- **Category:** delete
- **Recommendation:** Remove empty static class file.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 6 LOC empty class; no callers.

#### C08-004 — Delete unused Smoothing.Gaussian
- **Paths:** `Geometry/CurveFitting/Smoothing.cs`
- **Category:** delete
- **Recommendation:** Remove file.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No call sites outside defining file.

#### C08-005 — Delete commented IndexedGridVector2 husk
- **Paths:** `Geometry/DataStructures/IndexedGridVector2.cs`
- **Category:** delete
- **Recommendation:** Delete fully-commented struct.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Entire type in `/* */`.

#### C08-006 — Delete unused PolygonList
- **Paths:** `Geometry.Core/Primitives/PolygonList.cs`
- **Category:** delete
- **Recommendation:** Remove; callers use `List<Polygon>` + `PolygonIndex`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No external type refs.

#### C08-007 — Quarantine or drop production VectorN
- **Paths:** `Geometry.Core/Primitives/VectorN.cs`
- **Category:** delete
- **Recommendation:** Move to tests or delete (~286 LOC); production uses Vector2/3.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only VectorN.cs + GeometryTests FSCheck.

#### C08-008 — Delete orphan net48 dual projects
- **Paths:** `RTree/RTree_4.8.csproj`, `Utils/Utils_4.8.csproj`
- **Category:** archive
- **Recommendation:** Delete; SDK netstandard2.0 projects are live.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No `.sln`/`.csproj` references to `*_4.8`.

#### C08-009 — Remove dead Utils.ToCsv extension
- **Paths:** `Utils/EnumerableExtensions.cs`
- **Category:** delete
- **Recommendation:** Delete; odd `namespace Viking` with zero callers.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `rg ToCsv(` finds no usages.

#### C08-010 — Split Geometry.Core Polygon god-class
- **Paths:** `Geometry.Core/Primitives/Polygon.cs` (~2700 LOC)
- **Category:** split
- **Recommendation:** Extract index/relation/mutators into partials; keep API stable.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** ~2700 LOC / ~118 public members.

#### C08-011 — Consolidate spatial indexes
- **Paths:** `RTree/`, `Geometry.Core/.../BoundingBoxIndex.cs`, `Geometry/DataStructures/QuadTree.cs`, `LineSearchGrid.cs`
- **Category:** dedup
- **Recommendation:** Document when each is allowed; prefer one AABB strategy for large sets.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Core docs say BoundingBoxIndex replaces RTree; clients still heavy on RTree.

#### C08-012 — Keep customized RTree fork or wrap NuGet
- **Paths:** `RTree/`
- **Category:** modernize
- **Recommendation:** Quarantine as vendored fork (Update/TryAdd/IntersectionGenerator/3D Z) or façade a package; don’t drop casually.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** LGPL port headers; Viking-specific APIs used widely.

#### C08-013 — Remove or gate RTree log4net
- **Paths:** `RTree/RTree.csproj`, `RTree.cs`
- **Category:** modernize
- **Recommendation:** Replace with Trace/MEL; drop hard log4net dep.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** PackageReference log4net; hot-path Debug/Error spam.

#### C08-014 — Dedup Rectangle/Point across Geometry vs RTree
- **Paths:** `Geometry.Core/.../Rectangle.cs`, `RTree/Rectangle.cs`, `RTree/Point.cs`
- **Category:** dedup
- **Recommendation:** Internalize RTree types or accept Geometry AABB directly; keep `ToRTreeRect` shims interim.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Parallel types + conversion bridge throughout clients.

#### C08-015 — Factor Path vs Polyline shared predicates
- **Paths:** `Geometry/Primitives/Path.cs`, `Geometry.Core/Primitives/Polyline.cs`
- **Category:** dedup
- **Recommendation:** Do not merge types (explicit comment); extract shared predicates into Core helpers.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Both implement IPolyLine2D with parallel relation code.

#### C08-016 — Remove BinaryFormatter transform serialization
- **Paths:** `Geometry/Transforms/TransformSerialization.cs`, `RBFTransform.cs`
- **Category:** security
- **Recommendation:** Delete BF load/save; VolumeModel already JSON-only.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** BF still in Geometry; VolumeModel rejects legacy BF blobs.

#### C08-017 — Split non-geometry concerns out of Geometry.Global
- **Paths:** `Geometry/Global.cs`, `Common.cs`
- **Category:** split
- **Recommendation:** Move HTTP/cache/delay helpers to Utils/VolumeModel.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** `GetRandomRequestDelay` used by TextureReaderV2; infra living in Geometry.

#### C08-018 — Make MathNet MKL packages optional
- **Paths:** `Geometry/Geometry.csproj`
- **Category:** modernize
- **Recommendation:** PrivateAssets or Geometry.Native package so consumers don’t always pull natives.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** MKL Win/Linux + OpenBLAS always referenced; few entry points call TryUseNativeMKL.

#### C08-019 — Geometry.Core dual TFM cleanup
- **Paths:** `Geometry.Core/Geometry.Core.csproj`
- **Category:** modernize
- **Recommendation:** Drop unused `net9.0` if packaging doesn’t need it.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `netstandard2.0;net9.0`; consumers pull via Geometry netstandard.

#### C08-020 — Enable nullable on Geometry/GraphLib/RTree/Utils
- **Status:** completed
- **Paths:** those csprojs
- **Category:** modernize
- **Recommendation:** Cascade `<Nullable>enable</Nullable>` from Core.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Only Geometry.Core enables nullable.

#### C08-021 — Keep GraphLib; avoid new graph engines
- **Paths:** `GraphLib/`, AnnotationVizLib GraphViewEngine, WebVisualization Graph
- **Category:** dedup
- **Recommendation:** Keep GraphLib as topology core; view/DTO graphs stay separate.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** MorphologyGraph/MeshGraph inherit GraphLib; WebVisualization has separate DTO Graph.

#### C08-022 — UnitsAndScale vs SIMeasurement overlap
- **Paths:** `UnitsAndScale/`, `SIMeasurement/`
- **Category:** dedup
- **Recommendation:** Bridge or later merge (~200 LOC total); SIMeasurement only MeasurementExtension.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** UnitsAndScale widely referenced; SIMeasurement narrow.

#### C08-023 — SIMeasurement.ToString format loop never decrements
- **Paths:** `SIMeasurement/LengthMeasurement.cs`
- **Category:** bug
- **Recommendation:** Decrement `scale` in `while (scale > 0)` path.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Infinite loop when `PreserveNonSignificant: false`.

#### C08-024 — Relocate Geometry.Graphics.Color out of Geometry
- **Paths:** `Geometry/Color.cs`
- **Category:** split
- **Recommendation:** Move to VolumeModel/UI helpers.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Used by ChannelInfo/bookmarks/WPF — not Core predicates.

#### C08-025 — Shape2DCollection barely used outside tests/WKT
- **Paths:** `Geometry.Core/Primitives/Shape2DCollection.cs`
- **Category:** archive
- **Recommendation:** Mark internal or document as packaging API.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Usages in GeometryTests + GeometrySqlTypeMapper.

#### C08-026 — Document O(n) UniquePointSet/BoundingBoxIndex ceiling
- **Paths:** those Core primitives
- **Category:** test-gap
- **Recommendation:** Add large-polygon benchmarks; reintroduce tree if needed without Core→RTree dep.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Linear scan implementations.

#### C08-027 — Complete DynamicRenderMesh vs MeshBase consolidation
- **Paths:** `Geometry/Meshing/DynamicRenderMesh.cs`, `MeshBase.cs`
- **Category:** dedup
- **Recommendation:** Finish TODOs consolidating onto MeshBase.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Explicit TODOs; dual hierarchies maintained.

#### C08-028 — Remove obsolete MedialAxis forwarders
- **Paths:** `Geometry/Algorithms/MedialAxis.cs`
- **Category:** modernize
- **Recommendation:** Delete `[Obsolete]` overloads after caller audit.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Obsolete forwarders present.

#### C08-029 — Document Geometry.Core packing vs Geometry facade
- **Paths:** `Geometry.Core.csproj`
- **Category:** modernize
- **Recommendation:** Keep shared `namespace Geometry`; document PackageId Viking.Geometry + InternalsVisibleTo.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** RootNamespace Geometry, AssemblyName Geometry.Core.

#### C08-030 — Keep Utils for VikingXML helpers
- **Paths:** `Utils/IO.cs`, `Exceptions.cs`
- **Category:** archive
- **Recommendation:** Keep; only prune EnumerableExtensions (C08-009). Optional rename Viking.XmlUtils.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** VolumeModel/WebAnnotation/TransformFactory depend on Utils.

*Chunk opportunity count (after dedupe): 30*

---

### C09 — Viz + OData clients

**Summary:** AnnotationVizLib (~5.2k) + three transport adapters (Microsoft.OData net9 ~1.6k; Simple.OData net48 ~1.3k; WCF net48 ~3k incl. generated Reference) + generated ODataClient net48 (~4.1k) + SqlGeometry helpers + tiny IShape2D projects. **Top 3:** (1) Archive WCF viz client; migrate MorphologyView off Simple.OData; keep one OData factory. (2) Retarget ODataClient off net48-only + align OData package versions. (3) Finish IShape2D migration; drop SqlGeometryAnnotationExtensions.

#### C09-001 — Archive dead AnnotationVizLibWCFClient
- **Paths:** `AnnotationVizLibWCFClient/` + Viking/Viewer/Connectome slns
- **Category:** archive
- **Recommendation:** Remove from solutions; delete (~3k LOC).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No consuming `.csproj` ProjectReference; no C# call sites outside project.
- **See also:** Deduped with C21-002 (OData/WCF client collapse vs orphan WCF clients); those IDs are not listed separately.

#### C09-002 — Collapse Simple.OData into Microsoft.OData client
- **Paths:** `*ODataClient/*Factory.cs`, WCF factories
- **Category:** dedup
- **Recommendation:** Canonicalize on AnnotationVizLib.OData.*; delete Simple after C09-003.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Parallel Morphology/Neuron/Motif/Spatial factories.

#### C09-003 — Port Jotunn MorphologyView off Simple.OData/net48
- **Paths:** `Clients/Jotunn/MorphologyView/`
- **Category:** modernize
- **Recommendation:** Retarget module; switch to ODataMorphologyFactory; then delete Simple.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Sole live SimpleOData consumer; host is net10.

#### C09-004 — Retarget ODataClient off net48; align OData 8.x
- **Paths:** `ODataClient/`, `AnnotationVizLibODataClient.csproj`
- **Category:** modernize
- **Recommendation:** Multi-target netstandard/net9; bump Microsoft.OData.Client from 7.12.3 → 8.1.0.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Generator net48+7.12.3; consumers net9+8.1.0.

#### C09-005 — Delete Servers/AnnotationVizLibOData stub
- **Paths:** `Servers/AnnotationVizLibOData/`
- **Category:** delete
- **Recommendation:** Delete net451 stub.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty Class1; OData 6.15; no refs (also C05 overlap).

#### C09-006 — Delete orphan Clients/SqlGeometryUtils
- **Paths:** `Clients/SqlGeometryUtils/`
- **Category:** delete
- **Recommendation:** Delete stray Loader folder.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No csproj; root SqlGeometryUtils is real.

#### C09-007 — Consolidate SqlServerTypes Loaders
- **Paths:** Loaders under Viz clients; `SqlServerTypesLoader/`
- **Category:** dedup
- **Recommendation:** Point all at SqlServerTypesLoader; remove local copies.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 15+ Loader.cs copies; shared project already used by Viking/Jotunn.

#### C09-008 — Finish IShape2D path; drop SqlGeometryAnnotationExtensions
- **Paths:** `SqlGeometryAnnotationExtensions/`, `Viking.Annotation.Geometry/`, `.Mapping/`, `Annotation.Geometry/`
- **Category:** dedup
- **Recommendation:** Wire Viking/AU to IShape2D extensions; delete SqlGeometry near-duplicates.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Side-by-side ShapeSmoothing/Mapping/LocationObjExtensions; LocationObj already IShape2D.

#### C09-009 — Orphan Annotation.Geometry project
- **Paths:** `Annotation.Geometry/WebAnnotationModel.Geometry.csproj`
- **Category:** archive
- **Recommendation:** Wire as migration target or remove from Everything.sln.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only in Everything.sln; no ProjectReferences.

#### C09-010 — Unused Viking.Annotation.Geometry ref on WebAnnotationModel
- **Paths:** `WebAnnotationModel.csproj`
- **Category:** delete
- **Recommendation:** Remove unused ref or start calling extensions.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ProjectReference; no usings in model.

#### C09-011 — Drop EF6/DbGeometry from SqlGeometryUtils after Simple gone
- **Paths:** `SqlGeometryUtils.csproj`, Extensions `#if NET48`
- **Category:** modernize
- **Recommendation:** Remove EF package + DbGeometry helpers.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** EF Condition net48; only Simple uses DbGeometry.

#### C09-012 — AnnotationVizLib dual-TFM / System.Drawing tax
- **Paths:** `AnnotationVizLib.csproj`, ColorMapping
- **Category:** modernize
- **Recommendation:** Keep dual while MorphologyView/legacy need net48; later single TFM + non-Drawing color.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** net48;net9.0; CrossPlatformImage ifdefs; System.Drawing.Color APIs.

#### C09-013 — Delete dead AnnotationUtils.GraphVizEngine
- **Paths:** `AnnotationVizLib/GraphVizEngine.cs` vs ViewModels version
- **Category:** delete
- **Recommendation:** Delete root AnnotationUtils copy.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No `using AnnotationUtils`; DOT views use ViewModels.

#### C09-014 — Remove unused AnnotationVizLib ref from ConnectomeODataV4
- **Paths:** `ConnectomeODataV4.csproj`
- **Category:** delete
- **Recommendation:** Remove ProjectReference.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No AnnotationVizLib usings in host.

#### C09-015 — ConnectomeViz stale WCF BuildGraph calls
- **Paths:** `Servers/WebVisualization/*`
- **Category:** bug
- **Recommendation:** Migrate to OData factories or archive ConnectomeViz (C05).
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Calls NeuronGraph.BuildGraph APIs that only exist on WCF factories.

#### C09-016 — DataExport GetIDsFromQuery stub
- **Paths:** `Servers/DataExport/Utils/RequestVariables.cs`
- **Category:** bug
- **Recommendation:** Implement via ODataClient; today returns empty with TODO.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Explicit TODO + `Array.Empty<long>()`.

#### C09-017 — MeasureDistance broken OData namespace
- **Paths:** `Clients/MeasureDistance/Program.cs`
- **Category:** bug
- **Recommendation:** Fix to `ODataClient.ConnectomeDataModel.Container`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Uses nonexistent `ConnectomeODataV4.Container`.

#### C09-018 — WCF MosaicGeometry null-scale bug
- **Paths:** `AnnotationVizLibWCFClient/WCFLocationAdapter.cs`
- **Category:** bug
- **Recommendation:** Moot if C09-001; else fix null `_MosaicShape`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Assigns VolumeShape then scales null MosaicShape.

#### C09-019 — Drop unused Monographics→AnnotationVizLib ref
- **Paths:** `Monographics.csproj`
- **Category:** delete
- **Recommendation:** Remove.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No usings in Shared/Monographics.

#### C09-020 — Tests pull Simple.OData package without Simple project
- **Paths:** `AnnotationVizLibTests/`
- **Category:** test-gap
- **Recommendation:** Remove package; rename misnamed SimpleOData* tests that use OData factory.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Package present; InitializeSharedGraph calls ODataMorphologyFactory.

#### C09-021 — Delete unused SpatialDataFactory twins
- **Paths:** both OData SpatialDataFactory.cs
- **Category:** delete
- **Recommendation:** Delete or wire.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Definitions only.

#### C09-022 — Hand-rolled Simple DTOs vs generated entities
- **Paths:** Simple `Location.cs` etc. vs `ODataClient/Generated/`
- **Category:** dedup
- **Recommendation:** Eliminated by C09-002/003.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Parallel models.

#### C09-023 — Sync-over-async OData factory throttle
- **Paths:** `ODataMorphologyFactory.cs`
- **Category:** modernize
- **Recommendation:** Prefer true async OData APIs.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** GetAwaiter().GetResult + Task.Run throttle comments.

#### C09-024 — Fix AnnotationVizLib Description metadata
- **Paths:** csproj
- **Category:** modernize
- **Recommendation:** Fix “image processing library” text.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Wrong Description element.

#### C09-025 — Tier MeshXNAVizLib/ColladaIOTest/MeasureDistance
- **Status:** partially completed (MeshXNAVizLib deleted via C01-006; ColladaIOTest/MeasureDistance still open)
- **Paths:** those projects
- **Category:** archive
- **Recommendation:** Confirm need; archive or OData-only.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Mixed/stale stacks; MeasureDistance partially broken.

*Chunk opportunity count (after dedupe): 25*

---

### C10 — Annotation model

**Summary:** Shared WebAnnotationModel (~4.4k) + gRPC adapter (~5.9k) + orphaned WCF adapter (~3.5k) + AnnotationInterfaces (~1.1k) + dual DTO stacks. Mega-test `Test.cs` ~3641 LOC / 48 tests. StoreBaseWithKey ~989 (gRPC) vs ~1148 (WCF) with incompatible generics. **Top 3:** (1) Retire orphaned WebAnnotationModel.WCF (no sln/consumer refs). (2) Clean gRPC StoreBase commented WCF residue + unused SectionIndexedStore. (3) Split mega-tests; collapse triple DBACTION / type stacks.

#### C10-001 — Retire orphaned WebAnnotationModel.WCF
- **Paths:** `Clients/WebAnnotationModel.WCF/**`
- **Category:** delete
- **Recommendation:** Archive/delete (~3.5k LOC).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Grep finds only self-csproj; Viking uses gRPC only; WCF `new LocationObj(Location)` incompatible with current `ILocation` ctors.
- **See also:** Deduped with C21-001 (WCF store archive vs WCF AnnotationService retirement); those IDs are not listed separately.

#### C10-002 — Do not merge WCF+gRPC StoreBaseWithKey
- **Paths:** both StoreBaseWithKey.cs
- **Category:** dedup
- **Recommendation:** Dedup ROI is delete WCF (C10-001), not forced merge.
- **Effort:** S  
- **Risk:** high if forced merge  
- **Evidence:** Incompatible generics PROXY/WCFOBJECT vs SERVER_OBJECT+DI.

#### C10-003 — Delete commented WCF blocks in gRPC StoreBaseWithKey
- **Paths:** `WebAnnotationModel.gRPC/StoreBaseWithKey.cs`
- **Category:** delete
- **Recommendation:** Remove ~362 LOC block comments.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Mentions PROXY/WCFOBJECT; would not compile.

#### C10-004 — Delete dead hierarchy path comments in StoreBaseWithKeyAndParent
- **Paths:** that file
- **Category:** delete
- **Recommendation:** Remove ~128 LOC “DEAD WCF-era” block.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Explicit do-not-uncomment comments.

#### C10-005 — Split gRPC mega-test Test.cs
- **Paths:** `WebAnnotationModel.gRPC.Tests/Test.cs`
- **Category:** test-gap
- **Recommendation:** Split by converters/RPC/region/auth; reuse host.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** ~3641 LOC, 48 tests, one class.

#### C10-006 — Consolidate test auth/host boilerplate
- **Paths:** Test.cs, AnnotationStoresTestHost, SmokeTests
- **Category:** dedup
- **Recommendation:** Shared config+token+channel helper.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Duplicate Identity/Grpc endpoint loading.

#### C10-007 — Retire WebAnnotationModelTest WCF-era suite
- **Paths:** `Clients/WebAnnotationModelTest/`
- **Category:** archive
- **Recommendation:** Replace with gRPC smoke or delete.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Points at service.svc; MSTest vs NUnit.

#### C10-008 — Type stack AnnotationServiceTypes vs gRPC partials
- **Paths:** both type projects
- **Category:** dedup
- **Recommendation:** Long-term AnnotationInterfaces+proto; sunset WCF DTOs with server.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Parallel Location/Structure/*Link shapes.

#### C10-009 — Triple DBACTION enum
- **Paths:** AnnotationInterfaces + AnnotationServiceTypes Types/Enums + Types/DBACTION
- **Category:** dedup
- **Recommendation:** Single canonical in AnnotationInterfaces.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Three identical 0–3 enums, three namespaces.

#### C10-010 — Duplicate interfaces inside AnnotationServiceTypes
- **Paths:** `AnnotationServiceTypes/Interfaces/IChangeAction.cs` etc.
- **Category:** dedup
- **Recommendation:** Delete non-public duplicates; reference AnnotationInterfaces.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Same namespace, different DBACTION binding.

#### C10-011 — Unused SectionIndexedStore vs LocationStore index
- **Paths:** `SectionIndexedStore.cs`, `LocationStore.cs`
- **Category:** delete
- **Recommendation:** Wire or delete unused abstraction (~278 LOC).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Nothing subclasses SectionIndexedStore; LocationStore reimplements.

#### C10-012 — Move State into shared WebAnnotationModel
- **Paths:** gRPC/WCF State.cs
- **Category:** dedup
- **Recommendation:** One State; drop credential field.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Same type name in both adapters.

#### C10-013 — Remove WCF-era MixedLocalAndRemoteQueryResults / ISectionQuery
- **Paths:** shared Store + WCF copy
- **Category:** delete
- **Recommendation:** Delete after UI confirmed on RegionLoader.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Identical 20-line duplicates; gRPC uses RegionLoader.

#### C10-014 — Delete GetObjectBySectionCallbackState
- **Paths:** gRPC + WCF copies
- **Category:** delete
- **Recommendation:** Delete with commented APM + WCF.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Documented WCF-era bag; only commented refs in gRPC.

#### C10-015 — Delete dead ServerObjBase* hierarchy
- **Paths:** `AnnotationInterfaces/Server/*`
- **Category:** delete
- **Recommendation:** Delete ~340 LOC unused.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No subclasses; AnnotationModelObjBase is live.

#### C10-016 — Keep AnnotationInterfaces as transport-neutral contract
- **Paths:** `AnnotationInterfaces/**`
- **Category:** modernize
- **Recommendation:** Slim (C10-015); don’t merge WCF DTOs in.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Referenced by model, VizLib, MorphologyMesh, gRPC partials.

#### C10-017 — Converter dual-registration boilerplate
- **Paths:** gRPC Converters/*, Configuration.cs
- **Category:** dedup
- **Recommendation:** One converter instance for concrete+interface.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Dual IObjectConverter registrations × entity families.

#### C10-018 — Sync-over-async in location converters
- **Paths:** `Converters/Locations.cs`
- **Category:** modernize
- **Recommendation:** Async converters or sync setters.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** GetAwaiter().GetResult on SetAttributes/SetLinks.

#### C10-019 — Split LocationsClient god client
- **Paths:** `LocationsClient.cs` (~428 LOC)
- **Category:** split
- **Recommendation:** Split CRUD/spatial/section/link clients.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Factory implements 6 factory interfaces.

#### C10-020 — Proto AnnotationType vs LocationType bridge
- **Paths:** proto + AnnotationInterfaces/LocationType.cs
- **Category:** dedup
- **Recommendation:** Document/single-source numeric constants.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Cast bridge in Location.Partial.cs.

#### C10-021 — Proto files under gRPCAnnotationServiceTypes vs gRPC_Protos
- **Paths:** `gRPCAnnotationServiceTypes/Protos/`
- **Category:** modernize
- **Recommendation:** Align with grpc-protos-folder rule or update rule.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Protobuf Include in client types project.

#### C10-022 — Upgrade stale Grpc.Core package stack
- **Paths:** gRPC model/types csprojs
- **Category:** modernize
- **Recommendation:** Move to current Grpc.Net.Client-only.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Grpc.Core 2.43 / Grpc.Net.Client 2.41 / Protobuf 3.19.

#### C10-023 — Split oversized shared model files
- **Paths:** LocationObj (~699), IStore (~375), RegionLoader (~364)
- **Category:** split
- **Recommendation:** Extract interfaces/extensions.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Line counts.

#### C10-024 — Keep IAnnotate* contracts with WCF server only
- **Paths:** AnnotationServiceTypes/Interfaces
- **Category:** archive
- **Recommendation:** Policy: not for gRPC client path.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only WCF Proxy* uses them.

#### C10-025 — Drop unused AnnotationServiceTypes.WCF from Jotunn AnnotationViewModel
- **Paths:** that csproj
- **Category:** delete
- **Recommendation:** Audit and drop.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ProjectReference; no Types usings found.

#### C10-026 — SimpleOData/Connectome.sln block AnnotationServiceTypes removal
- **Paths:** SimpleOData csproj, Connectome sln
- **Category:** archive
- **Recommendation:** Track with C09 collapse.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** ProjectReference to AnnotationServiceTypes.WCF.

#### C10-027 — Unify Add/GetOrAdd/CollectionChanged post-hooks
- **Paths:** StoreBaseWithKey*, updaters
- **Category:** bug
- **Recommendation:** One post-add hook for parents/section/links.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Comments warn divergent ingest paths.

#### C10-028 — Thin ConcurrentObservable* subclasses
- **Paths:** ConcurrentObservable*Set.cs
- **Category:** dedup
- **Recommendation:** Optional aliases; low ROI.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Constructor-only subclasses.

#### C10-029 — NotNullWhenAttribute polyfill
- **Paths:** Store/NotNullWhenAttribute.cs
- **Category:** modernize
- **Recommendation:** PolySharp or keep for netstandard2.0.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Internal #if NETSTANDARD2_0 polyfill.

#### C10-030 — Validate gRPC link stores after WCF delete
- **Paths:** LocationLinkStore WCF vs gRPC sizes
- **Category:** test-gap
- **Recommendation:** Tests for former WCF behaviors; don’t port bulk back.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** WCF LocationLinkStore ~323 vs gRPC ~107.

#### C10-031 — Document single gRPC composition root
- **Paths:** `Store/Store.cs`
- **Category:** modernize
- **Recommendation:** Remove WCF Init myths from comments.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Comment already points at ConfigureAnnotationModel.

#### C10-032 — Prefer RegionLoader over resurrecting section APM
- **Paths:** RegionLoader, IRegionLoader
- **Category:** modernize
- **Recommendation:** Extend region/cell model; delete section watermarks after audit.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** LocationStore docs + DI register LocationRegionLoader.

*Chunk opportunity count (after dedupe): 32*

---

### C11 — Auth / tokens / Common

**Summary:** Viking.Tokens, Clients/Common, Viking.UI.WPF login, WebAppSettings. Secrets partially env-driven (`IDENTITY_SERVER_SECRET`) but fragmented keys + tracked test secrets + baked `anonymous`/`connectome`. Misnamed orphan `Common_4.8.csproj` actually targets net9.0. **Top 3:** (1) Secrets out of source + one resolver. (2) Delete Common_4.8; clarify Viking.Common naming. (3) Dedupe token stores / LoginWindow / WindowsCredentialManager / ResourceScopeNames.

#### C11-001 — Hardcoded/tracked client secrets and test passwords
- **Paths:** gRPC.Tests appsettings/secrets.json; docker-compose.identity-devtest; WebAppSettings Web.config; DataExport.Tests
- **Category:** security
- **Recommendation:** Env/user-secrets/examples only; rotate anything ever in git.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Tracked non-empty secret keys (values redacted).

#### C11-002 — Anonymous password baked into defaults
- **Paths:** Settings.settings, LoginViewModel, Volume.cs, State.cs, Program CLIs
- **Category:** security
- **Recommendation:** No default password in source; configure anonymous account.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Multiple `anonymous`/`connectome` pairs.

#### C11-003 — Desktop holds confidential client secret
- **Paths:** IdentityAppSettings, app.configs, BearerTokenUtils
- **Category:** security
- **Recommendation:** Long-term public client+PKCE; short-term env-only.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Desktop Resolve + password grant.

#### C11-004 — Fragmented secret key names / dual resolvers
- **Paths:** IdentityClientSecret, IdentityAppSettings, WebAppSettings, unused VikingClientSecret
- **Category:** dedup
- **Recommendation:** One canonical key; WebAppSettings call IdentityClientSecret.Resolve.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Three names in error text; UI only reads ApiClientSecret.

#### C11-005 — Legacy credential file encryption key in source
- **Paths:** NGVV UserCredentialsControl.cs
- **Category:** security
- **Recommendation:** Retire control for WPF+CredMan.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Hardcoded passkey; encryptString NotImplemented.

#### C11-006 — Access token in exception message
- **Paths:** BearerTokenUtils.GetUserId
- **Category:** security
- **Recommendation:** Don’t embed raw token.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Exception includes `{accessToken}`.

#### C11-007 — Delete misnamed Common_4.8.csproj
- **Paths:** `Clients/Common/Common_4.8.csproj`
- **Category:** delete
- **Recommendation:** Delete orphan.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Targets **net9.0**, RootNamespace Common; no refs (verified).

#### C11-008 — Duplicate ResourceScopeNames client↔Identity.Models
- **Paths:** Clients/Common + Servers/Identity.Models
- **Category:** dedup
- **Recommendation:** Shared netstandard package.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** “Keep in sync” comments; near-identical bodies.

#### C11-009 — Duplicate WindowsCredentialManager
- **Paths:** Common/Services + NGVV/Services
- **Category:** dedup
- **Recommendation:** Keep Common; delete NGVV copy.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Same namespace, different hashes; both public.

#### C11-010 — Dual bearer token storage
- **Paths:** TokenStore vs State.UserBearerToken
- **Category:** dedup
- **Recommendation:** Single store.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Dual-write/read comments in provider.

#### C11-011 — LoginWindow reimplements volume auth (double-fetch)
- **Paths:** LoginWindow.xaml.cs vs VolumeAuthHelper
- **Category:** dedup
- **Recommendation:** Always use RequestVolumeBearerTokenWithApiTokenAsync.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Extra ROPC round-trips.

#### C11-012 — AnnotationService GetTokenHelper duplicated + .Result
- **Paths:** AuthenticationManager, IdentityServerPrincipal
- **Category:** dedup
- **Recommendation:** One factory; async path.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Near-identical helpers; sync-over-async.

#### C11-013 — AnnotationService uses Viking.Tokens without ProjectReference
- **Paths:** AnnotationService.csproj
- **Category:** bug
- **Recommendation:** Add ProjectReference or stop depending.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** `using Viking.Tokens`; no Tokens ref in csproj.

#### C11-014 — Drop CreateFromAppSettings reflection
- **Paths:** BearerTokenUtils
- **Category:** modernize
- **Recommendation:** Inject IIdentityServerSettings.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Type.GetType reflection.

#### C11-015 — ClientId inconsistency ro.viking vs Viking
- **Paths:** BearerTokenHelper default vs login/VolumeAuth
- **Category:** bug
- **Recommendation:** Named constants from Identity client store.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Defaults diverge from call sites.

#### C11-016 — Introspection uses scope string as ClientId
- **Paths:** BearerTokenHelper.CheckClaims
- **Category:** bug
- **Recommendation:** Introspect with API client id+secret; check claims for scope.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** `ClientId = scope` in TokenIntrospectionRequest.

#### C11-017 — Split BearerTokenUtils god-file
- **Paths:** BearerTokenUtils.cs (~530 LOC)
- **Category:** split
- **Recommendation:** Split by concern; drop unused UriHelper.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Single file holds DTOs + helpers; UriCombine unused.

#### C11-018 — Dead Thinktecture.IdentityModel.Client package
- **Paths:** VikingCore, WebAnnotation csprojs
- **Category:** delete
- **Recommendation:** Remove package.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Package ref; no Thinktecture usings.

#### C11-019 — WebAppSettings mixed concerns + secrets in Web.config
- **Paths:** Servers/WebAppSettings
- **Category:** split
- **Recommendation:** Split Identity/DB from color maps; env/IConfiguration; clear EndpointPassword templates.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Classic net48; Web.config passwords; color txts.

#### C11-020 — HttpClient sprawl vs Common factory
- **Paths:** SharedResources, BearerTokenHelper dict, AnnotationService
- **Category:** modernize
- **Recommendation:** One strategy.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Three patterns in chunk.

#### C11-021 — ROPC as primary desktop auth
- **Paths:** BearerTokenHelper, LoginViewModel
- **Category:** modernize
- **Recommendation:** Prefer auth code+PKCE / launch-code.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** RequestPasswordTokenAsync is login path.

#### C11-022 — Remove Settings EncryptedPassword after CredMan migration
- **Paths:** LoginViewModel, Settings
- **Category:** delete
- **Recommendation:** CredMan only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Both paths present.

#### C11-023 — Prefer omitting empty secret keys; env-only
- **Paths:** app.configs, AnnotationService web.config
- **Category:** security
- **Recommendation:** Document IDENTITY_SERVER_SECRET; UserSecrets for local.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty keys encourage config-file secrets.

#### C11-024 — Common grab-bag naming
- **Paths:** Clients/Common
- **Category:** split
- **Recommendation:** Set AssemblyName Viking.Common; optional later UI split.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Auth beside WinForms UI attrs; default AssemblyName Common.

*Chunk opportunity count (after dedupe): 24*

---

### C12 — Volume model

**Summary:** ~5.9k LOC, multi-TFM net48;net9;net10. Volume.cs ~1167 LOC (~20%) is the god-file. Shared by Viking/Jotunn/Scene/VikingAU. Empty test stub. **Top 3:** (1) Split Volume.cs; fix IsLocal/VolumeXML bugs. (2) Dedupe TileGrid/OCP volume mappings + transform providers. (3) Real tests; delete dead Tile/ITile and broken cache path.

#### C12-001 — Split Volume.cs by responsibility
- **Paths:** `Volume.cs`
- **Category:** split
- **Recommendation:** Extract DTOs, XmlLoader, StosZipFetcher, TransformComposer; Volume as façade.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** ~1167 LOC spanning DTOs→I/O→orchestration→compose.

#### C12-002 — Unify Viking async vs Jotunn sync construction
- **Paths:** Volume.cs + VikingApplicationContext + Jotunn App.xaml.cs
- **Category:** modernize
- **Recommendation:** Prefer CreateAsync→Initialize only.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Dual ctors; sync ctor duplicates private setup.

#### C12-003 — Remove/fix dead VolumeXML field
- **Paths:** Volume.cs
- **Category:** bug
- **Recommendation:** Drop field; Initialize already uses VolumeElement.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Field never assigned from ctor param; Initialize reads null.

#### C12-004 — Fix broken IsLocal
- **Paths:** Volume.cs; TextureRequestQueue
- **Category:** bug
- **Recommendation:** Set `_IsLocal = IsVolumePathLocal(path)` or remove.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Sets false when path *is* local.

#### C12-005 — Delete unused SectionToReferenceSectionBelow
- **Paths:** Volume.cs
- **Category:** delete
- **Recommendation:** Remove dictionary.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Written; never read.

#### C12-006 — DefaultStosGroup never consumed
- **Paths:** Volume.cs
- **Category:** delete
- **Recommendation:** Wire into default selection or remove parsing.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Set at load; unused for defaults.

#### C12-007 — Static ChannelNames race + double AddChannel
- **Paths:** Volume.cs
- **Category:** bug
- **Recommendation:** Instance state; remove duplicate call.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** TODO race; AddChannel called twice.

#### C12-008 — Dedupe TileGrid/OCP volume-warp mappings
- **Paths:** TileGridToVolumeMapping, OCPTileServerToVolumeMapping
- **Category:** dedup
- **Recommendation:** Shared warp helper/base.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Near-identical Try*/CalculateVerticies/RecursiveVisibleTiles.

#### C12-009 — Collapse VolumeTransformProvider vs Viking VM
- **Paths:** VolumeTransformProvider vs VolumeViewModel
- **Category:** dedup
- **Recommendation:** Viking VM delegate to provider.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Parallel Identity vs named stos logic.

#### C12-010 — Delete dead ITile/Tile and unused vertex structs
- **Paths:** Tile.cs, VertexStructs.cs
- **Category:** delete
- **Recommendation:** Keep only used PositionNormalTextureVertex.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No external ITile usages.

#### C12-011 — Dead Section.VolumeTransformList + empty LoadLocal
- **Paths:** Section.cs
- **Category:** delete
- **Recommendation:** Remove unused list and commented LoadLocal.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Add-only list; LoadLocal fully commented.

#### C12-012 — Dead NotImplemented binary transform cache
- **Paths:** Volume.cs Load/SaveSerializedTransformFromCache
- **Category:** delete
- **Recommendation:** Delete NotImplemented path; keep ITK/JSON.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Load throws immediately; call site commented.

#### C12-013 — Fix RegistrationTreeNode parent bug
- **Paths:** RegistrationTree.cs
- **Category:** bug
- **Recommendation:** Parent = parentSection not sectionNumber; add tests.
- **Effort:** S  
- **Risk:** high  
- **Evidence:** Ctor sets Parent to sectionNumber.

#### C12-014 — Namespace VolumeModel vs Viking.VolumeModel
- **Paths:** JsonTransformSerializer.cs
- **Category:** modernize
- **Recommendation:** Move into Viking.VolumeModel.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only file in namespace VolumeModel.

#### C12-015 — Rename MappingsManager.cs → MappingManager.cs
- **Paths:** MappingsManager.cs
- **Category:** modernize
- **Recommendation:** Match type name.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Class MappingManager.

#### C12-016 — Replace static Global.TileCache with injected cache
- **Paths:** Global.cs, TileCache.cs
- **Category:** modernize
- **Recommendation:** DI/instance; thin static shim interim.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Singleton used from all mapping bases + hosts.

#### C12-017 — HttpClient anti-patterns in volume I/O
- **Paths:** Volume.cs LoadHTTPAsync/FetchStosZip
- **Category:** modernize
- **Recommendation:** Reuse SharedResources; pass credentials to zip fetch.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** New HttpClient per attempt; FetchStosZip ignores credentials.

#### C12-018 — Reduce System.Drawing in ChannelInfo
- **Paths:** ChannelInfo.cs
- **Category:** modernize
- **Recommendation:** Use Geometry.Graphics.Color; FormColor in UI adapters.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** System.Drawing.Color + Drawing.Common package.

#### C12-019 — Condition obsolete package refs to net48
- **Paths:** VolumeModel.csproj
- **Category:** modernize
- **Recommendation:** Compression/Http/Memory net48-only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Unconditional refs on net9/10.

#### C12-020 — Split interfaces out of MappingBase.cs
- **Paths:** MappingBase.cs
- **Category:** split
- **Recommendation:** Separate IVolumeTransformProvider files.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Interfaces + large abstract class co-located.

#### C12-021 — Deduplicate Pyramid construction
- **Paths:** Pyramid.cs
- **Category:** dedup
- **Recommendation:** One factory; delete unused protected ctor.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Protected ctor duplicates CreateFromElement.

#### C12-022 — Clean obsolete Volume I/O residue
- **Paths:** Volume.cs, Global.cs, CreateStosGridTransformThreadingObj.cs
- **Category:** delete
- **Recommendation:** Delete commented sync APIs; rename file to LoadStosResult.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ThreadingObj name but only DTO.

#### C12-023 — Encapsulate public mutable Volume fields
- **Paths:** Volume.cs, Section.cs
- **Category:** modernize
- **Recommendation:** Properties/read-only views (incl. credentials default).
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Public SortedLists + NetworkCredential defaults.

#### C12-024 — Real VolumeModel test suite
- **Paths:** empty VikingTests/VolumeModel/UnitTest1.cs
- **Category:** test-gap
- **Recommendation:** XML parse, RegistrationTree, wrap selection, JSON transforms, CreateAsync fixtures.
- **Effort:** L  
- **Risk:** low  
- **Evidence:** Empty TestVolumeModel only.

#### C12-025 — Align progress reporting APIs
- **Paths:** Volume.cs
- **Category:** modernize
- **Recommendation:** Standardize IProgress&lt;ProgressInfo&gt;.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Commented IProgressReporter remnants.

#### C12-026 — Section.Channels vs ChannelNames redundancy
- **Paths:** Section.cs
- **Category:** dedup
- **Recommendation:** One channel inventory API.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Dual lists during parse vs computed Channels.

#### C12-027 — Move TryMapShape* closer to WebAnnotation
- **Paths:** Extensions.cs
- **Category:** split
- **Recommendation:** Optional; keep mosaic bounds approx in VolumeModel.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Logs “WebAnnotation”; used by annotation tools.

#### C12-028 — Async CreateVolumeTransforms; remove empty finally
- **Paths:** Volume.cs
- **Category:** modernize
- **Recommendation:** Eliminate GetAwaiter().GetResult in compose.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Sync compose from async Initialize; blocking cache load.

*Chunk opportunity count (after dedupe): 28*

---

### C13 — Shared MonoGame graphics

**Summary:** Shared C# (~10k) linked into Monographics (net9/net10, MG 3.8.5-preview) and VikingXNAGraphics/MonographicsNet48 (net48, MG 3.7.1). PNGs identical ×3; technique .fx all differ (OPENGL ifdefs). No Shared .csproj; stale sln paths. **Top 3:** (1) Single-source PNG/font + unify technique .fx. (2) Collapse dual-shell / dual-TFM cost when consumers allow. (3) Delete dead XNA sln/refs, unused types, bak files.

#### C13-001 — Point mgcb at one PNG/font source
- **Paths:** three Content trees
- **Category:** dedup
- **Recommendation:** Build both mgcbs from Shared assets.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 10 PNGs + spritefont SHA-identical ×3.

#### C13-002 — Unify technique .fx into one tree
- **Paths:** Monographics/Content/*.fx vs VikingXNAGraphics/Content/*.fx
- **Category:** dedup
- **Recommendation:** Keep Monographics OPENGL variants; point net48 mgcb at them.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** All 13 pairs hash-different; Shared helpers already #included.

#### C13-003 — Deduplicate ImageContent PSDs
- **Paths:** Shared/ImageContent + Monographics/ImageContent
- **Category:** archive
- **Recommendation:** One authoring folder; not in mgcb.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ~5 MB ×2; not in Content.mgcb.

#### C13-004 — Drop Monographics dual TFM when consumers allow
- **Paths:** Monographics.csproj
- **Category:** modernize
- **Recommendation:** Move Testbed to net10 then single TFM.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** net9+net10; no Shared #if TFM splits; doubles content copy.

#### C13-005 — Real Shared project instead of wildcard Compile Include
- **Paths:** both shells
- **Category:** modernize
- **Recommendation:** Multi-TFM Shared csproj or one multi-TFM graphics project.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Shared has zero csproj; wildcard include.

#### C13-006 — Fix dead solution project entries
- **Paths:** Viking.sln, Viewer.sln
- **Category:** delete
- **Recommendation:** Retarget to Monographics + MonographicsNet48 (Everything.sln already correct).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Paths under Shared missing on disk.

#### C13-007 — Fix broken refs to deleted Shared Monographics
- **Paths:** MonogameWPFLibrary, MeshXNAVizLib
- **Category:** bug
- **Recommendation:** Point at Clients/Monographics or remove.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Reference missing Shared\Monographics.csproj.

#### C13-008 — Align folder/csproj/assembly/namespace naming
- **Paths:** MonographicsNet48 vs Monographics
- **Category:** modernize
- **Recommendation:** Match AssemblyName; longer-term drop “XNA” namespaces.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Assembly Monographics vs VikingXNAGraphics; RootNamespace VikingXNAGraphics both.

#### C13-009 — MonoGamePlatform DesktopGL vs WindowsDX mismatch
- **Paths:** Monographics.csproj, Content.mgcb
- **Category:** bug
- **Recommendation:** Align to Windows for WindowsDX; match Jotunn copy paths.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** DesktopGL platform; Content\bin\Windows exists; README notes DesktopGL unused.

#### C13-010 — Unify content-build strategies
- **Paths:** MGCB Task vs custom MGCB.exe
- **Category:** modernize
- **Recommendation:** Prefer one (dotnet-mgcb) for both.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Directory.Build.props hardcodes MG 3.0 Tools path for Net48.

#### C13-011 — Reconcile conflicting dotnet-tools.json versions
- **Paths:** Monographics/dotnet-tools.json vs .config/
- **Category:** dedup
- **Recommendation:** One manifest matching preview.3 packages.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** develop.13 vs preview.3.

#### C13-012 — Remove unused AnnotationVizLib/Common refs from Monographics
- **Paths:** Monographics.csproj
- **Category:** delete
- **Recommendation:** Remove (pairs C09-019).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No usings in Shared.

#### C13-013 — Delete dead Shared types
- **Paths:** BitmapFile, SharedEffectFunctions, ServiceContainer, Tetrahedron, obsolete ConvertToHSL
- **Category:** delete
- **Recommendation:** Remove after confirm.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No external callers.

#### C13-014 — Rename files to match public types
- **Paths:** many Shared effect/view files
- **Category:** modernize
- **Recommendation:** git mv mismatches (TileBlendEffect→TileLayoutEffect etc.).
- **Effort:** M  
- **Risk:** low  
- **Evidence:** 12+ filename≠type mismatches.

#### C13-015 — Remove MonogameContent orphan shaders
- **Paths:** Monographics/MonogameContent/*.fx
- **Category:** delete
- **Recommendation:** Delete unused.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No mgcb refs.

#### C13-016 — Remove .bak mgcb and duplicate xlsx
- **Paths:** Content.mgcb.bak, AnnotationOverlayShaderReference.xlsx ×2
- **Category:** delete
- **Recommendation:** Delete bak; one xlsx.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Identical hashes.

#### C13-017 — Drop legacy app.config from modern Monographics
- **Paths:** Monographics/app.config
- **Category:** delete
- **Recommendation:** Remove net48 sku config from net9/10 lib.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Declares .NETFramework v4.8; included twice.

#### C13-018 — Clean Net48 dual MonoGame references
- **Paths:** MonographicsNet48.csproj
- **Category:** modernize
- **Recommendation:** Single PackageReference; remove HintPath Reference.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Package 3.7.1.189 + HintPath 3.7.1.

#### C13-019 — Remove unnecessary UseWindowsForms on Net48 graphics
- **Paths:** MonographicsNet48.csproj
- **Category:** modernize
- **Recommendation:** Remove unless required.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Shared has no WinForms usings.

#### C13-020 — Fix AllowUnsafeBlocks only on Release
- **Paths:** Monographics.csproj
- **Category:** modernize
- **Recommendation:** Project-wide or remove (no unsafe found).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Flag only under Release.

#### C13-021 — Stop custom OutputPath bin\MonoGame
- **Paths:** Monographics.csproj
- **Category:** modernize
- **Recommendation:** Default SDK layout.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Custom OutputPath + also bin\x64.

#### C13-022 — Remove unreachable net9 Monographics conditions
- **Paths:** LocalBookmarks, WebAnnotationTests, etc.
- **Category:** delete
- **Recommendation:** Delete dead Condition branches or restore multi-TFM.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** TFM net48-only with net9 Conditions.

#### C13-023 — Single-source content copy for hosts
- **Paths:** Jotunn.csproj, Viking.csproj
- **Category:** dedup
- **Recommendation:** One output convention + one copy target.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Two different Content layouts.

#### C13-024 — Bifrost full Monographics fork
- **Paths:** Clients/Bifrost/Monographics
- **Category:** archive
- **Recommendation:** Absorb or mark archive (C05).
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Parallel fork net461 MG 3.5.1.

#### C13-025 — Rename ToXNA* APIs
- **Paths:** Extensions.cs
- **Category:** modernize
- **Recommendation:** Alias ToMonoGame* with obsolete wrappers.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** ~17 ToXNA matches; types are MonoGame.

#### C13-026 — Split oversized Shared units
- **Paths:** Extensions (~801), LabelView, SectionNumberOverlayView
- **Category:** split
- **Recommendation:** Split by concern.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Large mixed files.

#### C13-027 — Fix Shared Content README drift
- **Paths:** MonogameXNAGraphicsShared/Content/README.md
- **Category:** modernize
- **Recommendation:** Document helpers shared but PNGs still triplicated.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** README claims mgcb relative Shared assets — false for PNGs.

#### C13-028 — Collapse MG 3.7 Net48 shell when Viking retires
- **Paths:** VikingXNAGraphics/
- **Category:** archive
- **Recommendation:** After net48 Viking retirement, delete Net48 shell.
- **Effort:** L  
- **Risk:** high if premature  
- **Evidence:** Workspace rule: Viking net48 must stay working.

*Chunk opportunity count (after dedupe): 28*

---

### C14 — SQL schema

**Summary:** Dual trees — AnnotationDatabase SSDT (~151 sql, canonical for test/Docker) vs Servers/SQL kitchen-sink (~190 sql, CreateUpdateDatabase ~7.4k lines / DBVersion 1–83, not in Everything.sln). ≥29 overlapping basenames all content-diverged. Tracked .dbmdl ~6 MB; hardcoded passwords in CreateUpdate. **Top 3:** (1) Declare AnnotationDatabase SSDT as SoT; archive Servers/SQL. (2) Quarantine .dbmdl/.jfm; strip secrets. (3) Kill drifted duplicates / stop Building scratch scripts.

#### C14-001 — Declare AnnotationDatabase SSDT as sole schema SoT
- **Paths:** AnnotationDatabase.sqlproj; CreateUpdateDatabase.sql; docs
- **Category:** modernize
- **Recommendation:** Docs: SSDT publish = SoT; CreateUpdate = history only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Docker/gRPC use Apply-MinimalSchema; Everything.sln includes AnnotationDatabase not SQL.

#### C14-002 — Freeze/archive CreateUpdateDatabase.sql
- **Paths:** `Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql`
- **Category:** archive
- **Recommendation:** Archive; new changes only in SSDT.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** DBVersion 1–83; bootstrap Location still pre-geometry; hardcoded passwords (redacted: hello123 etc.).

#### C14-003 — Stop treating Servers/SQL as second SSDT DB
- **Paths:** SQL.sqlproj, SQL.sln
- **Category:** modernize
- **Recommendation:** Scripts catalog with None; don’t Build utilities.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Builds CreateUpdate + Utility + Repair + InDev; TFM v4.5.

#### C14-004 — Triple create-database scripts
- **Paths:** CreateUpdate; UtilityScripts/CreateAnnotationDatabase*.sql; SSDT; minimal-schema
- **Category:** dedup
- **Recommendation:** Keep SSDT + minimal; archive 2010 dumps.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** 2010 dump with binary passwords.

#### C14-005 — DeletedStructures only in minimal-schema
- **Paths:** minimal-schema.sql vs dbo/Tables
- **Category:** bug
- **Recommendation:** Add to SSDT or remove from minimal.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Table in minimal only; missing from SSDT/CreateUpdate.

#### C14-006 — Location create-path vs modern geometry model
- **Paths:** CreateUpdate vs Location.sql
- **Category:** bug
- **Recommendation:** Never cite CreateUpdate bootstrap as reference.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Scalar X/Y/Z vs geometry MosaicShape/VolumeShape.

#### C14-007 — Graph schema SSDT vs GraphDatabaseCreation scripts
- **Paths:** graph/Tables; SQL GraphDatabaseCreation.sql ×2
- **Category:** dedup
- **Recommendation:** Keep graph in SSDT; archive scripts.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** CreateUpdate has 0 graph hits; two differing GraphDatabaseCreation files.

#### C14-008 — Spatial cache SSDT triggers vs SQL/SpatialCache
- **Paths:** Location.sql trigger; SQL/SpatialCache
- **Category:** dedup
- **Recommendation:** SSDT wins; demote SpatialCache folder.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Overlapping SelectNetwork*SpatialData sizes differ.

#### C14-009 — ≥29 basename overlaps with content drift
- **Paths:** shared SP/function names across trees
- **Category:** dedup
- **Recommendation:** Diff each; keep AnnotationDatabase; demote SQL copies.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Hash compares all DIFFER (e.g. SelectSectionLocationsAndLinks 1208B vs 2924B).

#### C14-010 — DeepDeleteStructure in three forms
- **Paths:** AD SP; SQL SP; UtilityScripts ad-hoc with hardcoded ID
- **Category:** dedup
- **Recommendation:** One SP in SSDT; repair script without hardcoded ID.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Three different sizes; UtilityScripts not CREATE PROCEDURE.

#### C14-011 — ufnStructureVolume triple copy
- **Paths:** AD Functions; SQL Functions; SQL StoredProcedures
- **Category:** dedup
- **Recommendation:** Single SSDT function.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Three DIFFER sizes; misfiled under SP.

#### C14-012 — Functions misfiled under SQL/StoredProcedures
- **Paths:** LocationHasTag, StructureHasTag, ufn*
- **Category:** modernize
- **Recommendation:** Fix folders or delete after SSDT wins.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** CREATE FUNCTION under StoredProcedures.

#### C14-013 — Scale constants duplicated
- **Paths:** SQL/Constants vs AD Functions XYScale*
- **Category:** dedup
- **Recommendation:** SSDT SCHEMABINDING versions win.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Same return 2.176; AD has SCHEMABINDING.

#### C14-014 — SQL root duplicates of InDevelopment
- **Paths:** ArborVisualization identical pairs etc.
- **Category:** dedup
- **Recommendation:** Keep one; delete duplicates.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Identical hashes; double-dot Prototype filenames.

#### C14-015 — Build includes non-schema scripts
- **Paths:** SQL.sqlproj
- **Category:** modernize
- **Recommendation:** None/Content for scratch/repair/InDev.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Scratch.sql, SQLQuery30, CreateUpdate Built.

#### C14-016 — 46 .sql on disk not in sqlproj
- **Paths:** Becca, Migration, RLPfeiffer, etc.
- **Category:** archive
- **Recommendation:** Inventory as None or delete.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Disk vs Build list gap.

#### C14-017 — Typo/scratch filenames as Build items
- **Paths:** spSelec*, SQLQuery30, Maintenence, ScratchPad
- **Category:** delete
- **Recommendation:** Rename/delete; exclude Build.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Files Built.

#### C14-018 — InBounds vs MosaicBounds naming fork
- **Paths:** SQL *InBounds vs AD *InMosaic/VolumeBounds
- **Category:** modernize
- **Recommendation:** Standardize Mosaic/Volume names.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Both schemes; CreateUpdate v83 uses Mosaic/Volume.

#### C14-019 — spSelect* vs Select* dual naming
- **Paths:** UtilityScripts vs SSDT
- **Category:** archive
- **Recommendation:** Treat UtilityScripts as historical pads.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Parallel naming families.

#### C14-020 — Quarantine/untrack .dbmdl/.jfm
- **Paths:** SQL.dbmdl (~6 MB), *.jfm
- **Category:** archive
- **Recommendation:** git rm --cached; gitignore.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Tracked; no gitignore rules.

#### C14-021 — Remove Import Schema Logs
- **Paths:** AnnotationDatabase/Import Schema Logs
- **Category:** delete
- **Recommendation:** Delete + ignore.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Tracked Nov 2021 logs.

#### C14-022 — Ensure sqlproj.user stays ignored
- **Paths:** AnnotationDatabase.sqlproj.user
- **Category:** archive
- **Recommendation:** Confirm ignore works.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** On disk; *.user ignored.

#### C14-023 — Strip lab-specific Security principals from portable SSDT
- **Paths:** Security/AD_*, personal logins
- **Category:** security
- **Recommendation:** Env publish profiles; roles only in core.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Personal UIDs Built into sqlproj.

#### C14-024 — Remove hardcoded passwords from CreateUpdate/old dumps
- **Paths:** CreateUpdate; CreateAnnotationDatabase.sql
- **Category:** security
- **Recommendation:** Redact archive copies.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Plaintext passwords in scripts (redacted).

#### C14-025 — Archive InDevelopment
- **Paths:** Servers/SQL/InDevelopment
- **Category:** archive
- **Recommendation:** Archive; remove from Build.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Explicit InDevelopment; Scratch Built.

#### C14-026 — Archive Migration/
- **Paths:** Servers/SQL/Migration
- **Category:** archive
- **Recommendation:** Historical one-shots; exclude Build.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** SynapsesToLines/XMLToJSON exploratory.

#### C14-027 — Archive Becca/ personal scripts
- **Paths:** Servers/SQL/Becca
- **Category:** archive
- **Recommendation:** Move out or None.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Personal folder; not in sqlproj.

#### C14-028 — Archive Repair/Backup as ops toolkit
- **Paths:** RepairScripts, BackupScripts
- **Category:** archive
- **Recommendation:** Keep manual; not SSDT Build.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Destructive ops currently Built.

#### C14-029 — Archive RLPfeiffer* research SPs
- **Paths:** StoredProcedures/RLPfeiffer*
- **Category:** archive
- **Recommendation:** Research package or archive.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Schema-qualified RLPfeiffer.*; on disk not Built.

#### C14-030 — Archive ContactPatchArea utility cluster
- **Paths:** UtilityScripts/ContactPatchArea
- **Category:** archive
- **Recommendation:** Archive; overlaps ufnStructureArea.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Multiple scratch variants; not in sqlproj.

#### C14-031 — AnnotationDatabaseTest stub
- **Paths:** AnnotationDatabaseTest
- **Category:** test-gap
- **Recommendation:** Replace with integration tests vs minimal-schema.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Empty UnitTest1; designer DeepDelete test.

#### C14-032 — Broken ErrorCorrection→AnnotationDatabase.csproj ref
- **Paths:** ErrorCorrection.csproj
- **Category:** bug
- **Recommendation:** Remove bogus reference (no .csproj exists).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** GUID mismatch; only .sqlproj exists.

#### C14-033 — GrpcAnnotationService.sln wrong AnnotationDatabase GUID
- **Paths:** GrpcAnnotationService.sln
- **Category:** bug
- **Recommendation:** Align GUID with sqlproj/Everything.sln.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** {27F8…} vs {4DD9…}.

#### C14-034 — Automate full SSDT publish for AnnotationTest
- **Paths:** Apply-MinimalSchema; README Option B
- **Category:** modernize
- **Recommendation:** CI dacpac+SqlPackage; keep minimal for smoke.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** README: full publish not automated.

#### C14-035 — Clarify three deploy options in README
- **Paths:** config-template/README.md
- **Category:** modernize
- **Recommendation:** Emphasize SoT + EF EnsureCreated unsupported.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** A/B/C documented but easy to confuse with CreateUpdate.

#### C14-036 — DAB config is MCP/dev-only
- **Paths:** dab-config.annotation-test.json
- **Category:** archive
- **Recommendation:** Keep; don’t conflate with schema SoT.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Unauthenticated local tooling.

#### C14-037 — Review lab DB settings in sqlproj
- **Paths:** DelayedDurability FORCED, Change Tracking, FullText
- **Category:** modernize
- **Recommendation:** Publish profile vs portable schema.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** sqlproj properties + Storage partitions Built.

#### C14-038 — Align minimal-schema with SSDT table set
- **Paths:** minimal-schema vs dbo/Tables
- **Category:** dedup
- **Recommendation:** Generate subset or document omissions; fix DeletedStructures.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** SSDT has StructureTemplates/DBVersion/SpatialCache absent from minimal.

#### C14-039 — Clarify graph schema maturity
- **Paths:** graph/Tables
- **Category:** archive
- **Recommendation:** Confirm prod use; optional publish profile if experimental.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** In SSDT Build; absent from CreateUpdate/minimal.

*Chunk opportunity count (after dedupe): 39*

---

### C15 — Viking NGVV / shell

**Summary:** WinForms Viking shell: NGVV (~20.7k LOC), exe (~1.2k), VikingXNAWinForms (~1.1k), LocalBookmarks (~4k). **Top 3:** (1) Viking still forks tile draw from `SectionSceneRenderer` and annotations via WinForms `AnnotationOverlay` vs Jotunn’s `AnnotationScene`; (2) split god files `SectionViewerControl` (~2951), `TextureReaderV2` (~922), `ExtensionManager` (~868); (3) delete dead WCF metadata / WebAnnotation Service Reference (~3.7k) and stale net9 conditions. Also noted: VikingAU (SqlServerTypes loader drift), MeasureDistance (separate OData CLI).

#### C15-001 — Split `SectionViewerControl` god file
- **Paths:** `Clients/Viking/NGVV/UI/Controls/SectionViewerControl.cs` (~2951 LOC), `.Designer.cs`, `.ViewportHost.cs`
- **Category:** split
- **Recommendation:** Extract draw/cache, input, channel compositing, overlay orchestration; thin host over shared scene APIs.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** ~118 method-like members; Draw/DrawSection* ~860 lines; input ~600 lines.

#### C15-002 — Viking tile draw still forks from `SectionSceneRenderer`
- **Paths:** `SectionViewerControl.cs`; `Clients/Viking.Scene/SectionSceneRenderer.cs` (~554); `JotunnVolumeView/SectionSceneHost.cs`
- **Category:** dedup
- **Recommendation:** Migrate Viking `Draw`/`DrawSection*` to `SectionSceneRenderer` (as Jotunn). Do not evolve tile algorithms only in NGVV.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Grep: **zero** `SectionSceneRenderer` under NGVV; Jotunn constructs it; Scene comments claim shared use.
- **See also:** Deduped with C16-012 (Viking tile draw vs SectionSceneRenderer); those IDs are not listed separately.

#### C15-004 — `ISectionOverlayExtension` hard-couples to `SectionViewerControl`
- **Paths:** `NGVV/Common/Interfaces.cs`; `AnnotationOverlay.SetParent(SectionViewerControl)`
- **Category:** modernize
- **Recommendation:** Change `SetParent` to `IViewportHost` (already on ViewportHost partial).
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Concrete `SectionViewerControl` in interface; ViewportHost exists.

#### C15-005 — Relocate linked tile pipeline into `Viking.Scene`
- **Paths:** `Viking.Scene.csproj` Link includes; sources still under `NGVV/{Threading,ViewModels,TextureCache.cs,...}`
- **Category:** split
- **Recommendation:** Move TextureReaderV2/queues/TileView/caches into Scene; stop “edit NGVV = Viking-only” forks.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** VikingCore Compile Remove + Scene Link; Scene Global stub for UI-free compile.

#### C15-006 — Split / harden `TextureReaderV2`
- **Paths:** `NGVV/Threading/TextureReaderV2.cs` (~922); queues/cache siblings
- **Category:** split
- **Recommendation:** Separate HTTP fetch, disk cache, decode/upload, abort policy; test abort+corrupt cache.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** TODO ~557: abort can leave corrupt cache entries.

#### C15-007 — Split `ExtensionManager` god class
- **Paths:** `NGVV/ExtensionManager.cs` (~868); `AuthenticodeVerifier.cs`
- **Category:** split
- **Recommendation:** Split discovery/load, DI, menus/pages/commands/overlays; keep Authenticode gate.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Static ctor + modules + reflection; multi-region responsibilities.

#### C15-008 — Remove empty NGVV WCF metadata stub
- **Paths:** `VikingCore.csproj` (`WCFMetadata Include="Service References\"`)
- **Category:** delete
- **Recommendation:** Delete WCFMetadata ItemGroup.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No `Service References` folder under NGVV.

#### C15-009 — Delete dead WCF Service Reference (WebAnnotation)
- **Paths:** `WebAnnotation/Service References/Service/Reference.cs` (~3747) + WSDL/XSD; csproj
- **Category:** delete
- **Recommendation:** Compile Remove/delete for net48 (already done net10); keep gRPC path.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Types in net48 DLL; no live `WebAnnotation.Service.` callers outside Reference.cs; runtime uses WebAnnotationModel + gRPC.
- **See also:** Deduped with C21-003 (WCF Service References / WCF client retirement overlap); those IDs are not listed separately.

#### C15-010 — Replace leftover `FaultException` catches
- **Paths:** Many under `WebAnnotation/**`
- **Category:** modernize
- **Recommendation:** Catch store/gRPC faults; drop ServiceModel dependency with Service Reference.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Dozens of `FaultException` catches; Store is gRPC-based.

#### C15-011 — Remove stale `net9.0-windows` conditions on net48-only projects
- **Paths:** `VikingCore.csproj`; `LocalBookmarks.csproj`
- **Category:** delete
- **Recommendation:** Delete dead PropertyGroups/ItemGroups for net9.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Single TFM net48; conditions never true.

#### C15-012 — Align VikingXNAWinForms TFMs with consumers
- **Paths:** `VikingXNAWinForms.csproj` (`net48;net9.0-windows`)
- **Category:** modernize
- **Recommendation:** Drop unused TFM or align to net10 if needed — avoid half-migrated multi-TFM.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Multi-TFM while Viking shell is net48-only.

#### C15-013 — Dedup `GraphicsDeviceService` (WinForms vs Scene)
- **Paths:** `VikingXNAWinForms/GraphicsDeviceService.cs` (~198); `Viking.Scene/GraphicsDeviceService.cs` (~133)
- **Category:** dedup
- **Recommendation:** Share one device-service parameterized by host, or document intentional fork.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Parallel AddRef/Release, PresentationParameters, HiDef/Reach.

#### C15-014 — Quarantine/delete PlantMap leftover comments
- **Paths:** `SectionViewerControl.cs`, `VikingControl.cs`, `ObjectListView.cs`, `ExtensionManager.cs`
- **Category:** delete
- **Recommendation:** Remove commented PlantMap blocks.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only comments; no PlantMap namespace in repo.

#### C15-015 — Remove empty `ICommands` + dead `ExportToExcel`
- **Paths:** `NGVV/Objects/Common/Interfaces.cs`; `ObjectListView.cs`
- **Category:** delete
- **Recommendation:** Delete empty interface; remove/implement ExportToExcel (`NotImplementedException`).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty `ICommands`; ExportToExcel has no callers.

#### C15-016 — Consolidate duplicate Interfaces / UIObjectsShared orphan
- **Paths:** `NGVV/Common/Interfaces.cs`; `NGVV/Objects/Common/Interfaces.cs`; `Clients/Viking/UIObjectsShared/**`
- **Category:** dedup
- **Recommendation:** Single contracts assembly; delete or wire UIObjectsShared.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** UIObjectsShared unreferenced by other csproj/sln greps.

#### C15-017 — Archive NGVV design-time / CVS junk
- **Paths:** `NGVV/ClassDiagram1.cd`; `NGVV/Properties/CVS/`
- **Category:** archive
- **Recommendation:** Remove from project/tree.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Listed in csproj; CVS dir present.

#### C15-018 — Remove vendored NuGet folders from Viking shell
- **Paths:** `Viking/System.Buffers.4.6.1/`; `System.Numerics.Vectors.4.6.1/`
- **Category:** delete
- **Recommendation:** PackageReference restore only; delete checked-in trees.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Folders beside PackageReference; not gitignored.

#### C15-019 — Remove non-product binaries (mp3, pfx)
- **Paths:** `Viking/*.mp3`; `Viking_*TemporaryKey.pfx`
- **Category:** archive
- **Recommendation:** Remove mp3; move signing keys to secure store if unused.
- **Effort:** S  
- **Risk:** low (pfx med if ClickOnce still used)  
- **Evidence:** Present in project directory.

#### C15-020 — Default CLI credentials in `Program.cs`
- **Paths:** `Clients/Viking/Viking/Program.cs`
- **Category:** security
- **Recommendation:** Remove default password `"connectome"`; require args/prompt.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** `[Option('p', "pwd", Default = "connectome", ...)]`.

#### C15-021 — Segmentation gRPC uses legacy `Grpc.Core` + insecure channel
- **Paths:** `VikingCore.csproj`; `Services/Grpc/GrpcChannelManager.cs`
- **Category:** modernize
- **Recommendation:** Prefer `Grpc.Net.Client`; restrict insecure to local/dev.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** `ChannelCredentials.Insecure`; Grpc.Core 2.38.0.

#### C15-022 — Prune obsolete package references on VikingCore
- **Status:** completed
- **Paths:** `VikingCore.csproj`
- **Category:** modernize
- **Recommendation:** Drop System.Net.Http 4.3.4 / old crypto where BCL suffices; remove dead net9 Duende block.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** PackageReference list + unused conditional packages.

#### C15-023 — Split large command / pen / gesture helpers
- **Paths:** `Command.cs` (~744); `PenInputHelper.cs` (~569); `TouchSupport.cs` (~438)
- **Category:** split
- **Recommendation:** Separate command queue from pen math; trim unused touch paths.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** LOC ranking; commented `RegisterTouchWindow` in VikingMain.

#### C15-024 — LocalBookmarks generated schema bulk
- **Paths:** `bookmarkschema.cs` (~884); `BookmarkSchemaV2.cs` (~906); MigrateV1ToV2
- **Category:** archive
- **Recommendation:** Quarantine LinqToXsd output; avoid hand-editing; keep V1 migration.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Both schemas compiled; V1 still loaded.

#### C15-025 — Test gap for shell/god paths
- **Paths:** `Clients/Viking/VikingTests/`
- **Category:** test-gap
- **Recommendation:** Tests for texture abort/cache, ExtensionManager ShouldLoad, Scene renderer parity; align TFM.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Almost no tests for SectionViewerControl/TextureReaderV2/ExtensionManager; VikingTests net9 vs Viking net48.

#### C15-026 — ObjectListView / ObjectTreeView size
- **Paths:** `ObjectListView.cs` (~651); `ObjectTreeView.cs`
- **Category:** split
- **Recommendation:** Extract export/preferences/binding; delete PlantMap/Excel stub.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** LOC + TODOs.

#### C15-027 — Remove legacy XNA registry probes
- **Status:** completed
- **Paths:** `Viking/Program.cs` (`XNAFrameworkInstalled`)
- **Category:** modernize
- **Recommendation:** Remove XNA 4.0 registry checks; MonoGame is runtime.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Registry probes while MonoGame 3.7.1 referenced.

#### C15-028 — Adjacent: VikingAU SqlServerTypes loader drift
- **Paths:** `Clients/VikingAU/`; `Viking/SqlServerTypes/Loader.cs`
- **Category:** dedup
- **Recommendation:** Use `SqlServerTypesLoader` only; delete divergent Loaders.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Different Loader hashes; VikingAU already references shared loader.

#### C15-029 — Adjacent: MeasureDistance is separate CLI
- **Paths:** `Clients/MeasureDistance/`
- **Category:** archive
- **Recommendation:** Track under tooling/OData, not C15 shell.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** net9 OData console; no NGVV refs.

#### C15-030 — `ViewerControl` vs Scene host divergence
- **Paths:** `VikingXNAWinForms/ViewerControl.cs` (~523); Scene MonoGameHwndHost / SectionSceneRenderer
- **Category:** dedup
- **Recommendation:** Share effect acquisition; no draw/annotation logic here.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Owns Camera/Scene/AnnotationOverlayEffect; Jotunn uses Scene host.

**Deduped aliases (not listed separately):** C15-003 → C16-001

**Cross-chunk:** - Tile/annotation unify → C16 (`Viking.Scene`, AnnotationScene, Jotunn host) - Dead WCF delete → C17/C21 (Service References, FaultException) - gRPC client modernize → Segmentation + WebAnnotation gRPC - Identity Thinktecture→Duende → C11 - Shared graphics TFMs → C13 - SqlServerTypes → VikingAU / shared loader ---

*Chunk opportunity count (after dedupe): 29*

---

### C16 — WebAnnotation views / scene

**Summary:** Shared scene/view layer: `WebAnnotation/View/` ~6096 LOC (`SectionAnnotationsView` ~1298), ViewModels ~2457, `AnnotationScene` ~231, Overlay ~2158, `Viking.Scene` ~946, `JotunnVolumeView` ~1732 (`SectionSceneHost` ~816). **Top 3:** (1) Unify Draw/hit into `AnnotationScene` (anti-fork); (2) delete empty stubs + archive PyramidViewer/quarantined AnnotationView; (3) route Viking tiles through `SectionSceneRenderer`.

#### C16-001 — Unify annotation Draw into AnnotationScene (anti-fork)
- **Paths:** `AnnotationScene.cs`; `AnnotationOverlay.Draw`; `LocationObjRenderer.cs`; `SectionSceneRenderer`
- **Category:** dedup
- **Recommendation:** Shared draw orchestration on AnnotationScene; Overlay delegates; keep LocationObjRenderer as only shape renderer.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Parallel pipelines (backgrounds → links → structure links → labels); only Jotunn uses SectionSceneRenderer today.
- **See also:** Deduped with C15-003, C17-024 (Unify AnnotationOverlay.Draw / AnnotationScene.Draw); those IDs are not listed separately.

#### C16-002 — Hit-test twin: ObjectAtPosition vs HitTest
- **Paths:** `AnnotationOverlay.ObjectAtPosition`; `AnnotationScene.HitTest`; `Extensions.NearestObjectOnCurrentSectionThenAdjacent`
- **Category:** dedup
- **Recommendation:** Single shared hit helper; overlay + tools call it.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Near-identical logic; Scene comment calls HitTest “equivalent of ObjectAtPosition.”

#### C16-003 — Adjacent visibility filter already diverged
- **Paths:** Overlay FindVisible*; AnnotationScene.Draw visibility LINQ
- **Category:** bug
- **Recommendation:** One filter (Parent/Type + IsVisible) for both hosts.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Overlay adjacent requires Parent.Type; Scene adjacent only IsVisible.

#### C16-004 — Dual region-load coalescing
- **Paths:** `SectionAnnotationsViewBase.LoadAnnotationsInRegion`; `AnnotationScene.LoadVisibleAsync`
- **Category:** dedup
- **Recommendation:** One in-flight/LOD skip policy.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Separate locks/dictionaries for equivalent skip logic.

#### C16-005 — Dual SectionAnnotationsView lifetime (cache vs dict)
- **Paths:** Overlay + `SectionLocationViewModelCache`; `AnnotationScene._sections`
- **Category:** split
- **Recommendation:** Shared ownership/cache service; keep Net10 SectionViewLookup for Actions.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** TimeQueueCache vs unbounded `_sections`.

#### C16-006 — SectionAnnotationsView god-file split
- **Paths:** `View/SectionAnnotationsView.cs` (~1298 LOC / ~1543 lines)
- **Category:** split
- **Recommendation:** Split Base / Adjacent / primary / hit-test / volume-position.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Multiple classes in one file; primary ~1037 lines.

#### C16-007 — LocationObjRenderer type-switch clone
- **Paths:** `View/LocationObjRenderer.cs` (~195)
- **Category:** dedup
- **Recommendation:** Factor shared type→Draw dispatch; delete unused SplitLabel.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Duplicate typeof ladders; SplitLabel has zero callers.

#### C16-008 — Delete empty / unused View stubs
- **Paths:** AnnotationCircle, LocationCircleLabelView, Location_ShapeViewModel, StructureTypeButtonView, OverlappedLocationView
- **Category:** delete
- **Recommendation:** Delete stubs; live overlap is OverlappedLinkCircleView.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty bodies; XML marks OverlappedLocationView unused; no constructions.

#### C16-009 — Dead ViewModels (canvas/shape)
- **Paths:** `Location_CanvasViewModel.cs`; `Location_ShapeViewModel.cs`
- **Category:** delete
- **Recommendation:** Delete if unused; keep Location_PropertyPageViewModel.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Grep only definitions; net10 already Compile Remove’d several VMs.

#### C16-010 — Archive PyramidViewer + WPF3D converters
- **Paths:** PyramidViewer.xaml(.cs); TileViewModelToGeometry3DConverter; RectToCenterPointConverter; Test.cs; VikingVolumeViewModule
- **Category:** archive
- **Recommendation:** Move to archive/delete; keep SectionSceneHost live.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** csproj Compile/Page Remove; converters only from PyramidViewer.

#### C16-011 — Quarantined Jotunn AnnotationView / AnnotationViewModel
- **Paths:** `Clients/Jotunn/AnnotationView/` (+ QUARANTINED.md); `AnnotationViewModel/`
- **Category:** archive
- **Recommendation:** Keep quarantined; never revive as second renderer.
- **Effort:** S  
- **Risk:** high if revived  
- **Evidence:** QUARANTINED.md “Do not revive”; live path AnnotationScene + WebAnnotation.

#### C16-013 — SectionSceneHost size / responsibilities
- **Paths:** `SectionSceneHost.cs` (~816)
- **Category:** split
- **Recommendation:** Split input, texture CTS, checkpoint, draw loop; thin host for renderer + Annotations.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Owns MonoGameHwndHost, IViewportHost, multi-cell draw, tools surface.

#### C16-014 — Net10AnnotationOverlay shim vs WinForms overlay
- **Paths:** `Net10AnnotationOverlay.cs` (47); `UI/AnnotationOverlay.cs` (2158)
- **Category:** modernize
- **Recommendation:** Move Actions off static Overlay toward IAnnotationScene / injected lookup.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Same type name for Actions; TFM Compile Remove swaps.

#### C16-015 — `#if NETFRAMEWORK` sprawl in View layer
- **Paths:** LocationCanvasView, LocationLinkView, LocationActions, Overlapped*, SectionAnnotationsView
- **Category:** modernize
- **Recommendation:** Isolate WinForms menus/CreateCommand behind IAnnotationTool; keep draw TFM-agnostic.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Multiple NETFRAMEWORK blocks; context menus removed on net10.

#### C16-016 — AnnotationViewFactory adjacent polygon → circle (documented)
- **Paths:** `AnnotationViewFactory.cs` (~140)
- **Category:** modernize
- **Recommendation:** No fork; optionally extract mapping table; document intentional inscribed circle.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** CreateAdjacent comments + CalculateInscribedCircle().

#### C16-017 — Test gap: View / Scene / SectionAnnotationsView
- **Paths:** `WebAnnotationTests/`
- **Category:** test-gap
- **Recommendation:** Hit-test / visibility / factory tests before Draw consolidate.
- **Effort:** M  
- **Risk:** low (tests); high if refactor untested  
- **Evidence:** No test matches for those types.

#### C16-018 — Commented dead load method in AnnotationOverlay
- **Paths:** `AnnotationOverlay.cs` ~2189–2234
- **Category:** delete
- **Recommendation:** Remove commented `LoadSectionAnnotations` block.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Entire method in `/* */`.

#### C16-019 — Unreachable throw after return in AnnotationViewFactory
- **Paths:** `AnnotationViewFactory.cs` ~140–142
- **Category:** bug
- **Recommendation:** Delete dead throw after return.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** `default:` returns then throws.

#### C16-020 — Do not introduce a second annotation renderer
- **Paths:** Shared View/LocationObjRenderer/AnnotationScene; hosts Overlay / SectionSceneHost
- **Category:** security
- **Recommendation:** Process constraint: reject PRs adding Jotunn-local Location*View or reviving quarantined WPF AnnotationView.
- **Effort:** S  
- **Risk:** high if violated  
- **Evidence:** Workspace rule; Jotunn references WebAnnotation; QUARANTINED docs.

**Deduped aliases (not listed separately):** C16-012 → C15-002

**Cross-chunk:** - WebAnnotationModel / Store region load; Viking.Input + Tools; VolumeModel/TileView; VikingXNAGraphics stencil; NGVV host (C15); Jotunn MainWindowShell; UI/Actions (C17); WPF VMs (C17); quarantined AnnotationView (C18) ---

*Chunk opportunity count (after dedupe): 19*

---

### C17 — WebAnnotation UI / commands

**Summary:** UI/commands/overlay/settings + WPF controls/VMs. Dominated by AnnotationOverlay (~2158) and SegmentationCommand (~1706). Preference apply duplicated Viking vs Jotunn; structure-type UX triplicated; Draw fork risk live. **Top 3:** (1) Delete unused V1 pen commands + stubs; (2) unify preference apply + clamp constants; (3) converge Draw on AnnotationScene before any new renderer work.

#### C17-001 — Delete unused V1 pen free-draw commands
- **Paths:** `AnnotationPenFreeDrawCommand.cs`, `AnnotationOverlayPenFreeDrawCommand.cs`; live V2
- **Category:** delete
- **Recommendation:** Delete V1 after confirming no reflection registration; Overlay constructs V2 only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** V2 at ~L834; V1 `new` only commented; ~430 LOC.

#### C17-002 — Delete / archive stub command files
- **Paths:** `StaticExtensionCommands.cs` (commented); `CutHoleCommand.cs` (help-strings only); `FavoriteStructureIDsCommands.cs` (empty)
- **Category:** delete
- **Recommendation:** Delete stubs; move help strings next to live CutHole types.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No live constructions/usages.

#### C17-003 — Dead field `LastIntersectedObject`
- **Paths:** `AnnotationOverlay.cs`
- **Category:** delete
- **Recommendation:** Delete; keep `LastMouseOverObject`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Declared “Unused leftover”; only assignment is null init.

#### C17-004 — Dead WPF Settings FavoriteStructureIDs
- **Paths:** `WebAnnotationWPFControls/Properties/Settings.Designer.cs`
- **Category:** delete
- **Recommendation:** Drop WPF duplicate; favorites use WebAnnotation.Properties.Settings + Global.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No WPF.Settings consumers outside generated files.

#### C17-005 — Archive SampleData + MockData
- **Paths:** `WebAnnotationWPFControls/SampleData/*`, `MockData/*`
- **Category:** archive
- **Recommendation:** DesignTime-only; exclude from runtime packaging.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ~12 mock files; runtime binds Store/VMs.

#### C17-006 — Duplicate preference apply: AnnotationMenu vs Jotunn
- **Paths:** `AnnotationMenu.cs`; `Jotunn/MainWindowShell.xaml.cs`; `AnnotationPreferencesDialogViewModel.cs`
- **Category:** dedup
- **Recommendation:** Shared `AnnotationSettingsBridge.LoadFrom/ApplyTo` (or bind VM to Global).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Near-identical 19-parameter LoadCurrentSettings + assign blocks.
- **See also:** Deduped with C18-016 (Preference dialogs / module preference overlap); those IDs are not listed separately.

#### C17-007 — Clamp/defaults duplicated: Global vs Preference VM
- **Paths:** `Global.AnnotationSettings` MIN/MAX; `AnnotationPreferencesDialogViewModel` inline clamps
- **Category:** dedup
- **Recommendation:** Export ranges from AnnotationSettings; VM uses those.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Same numeric bounds in both places.

#### C17-008 — StructureType dual view-models
- **Paths:** `WebAnnotation/ViewModel/StructureType.cs` (~191); `WebAnnotationViewModels/.../StructureTypeObjViewModel.cs` (~303)
- **Category:** dedup
- **Recommendation:** One shared adapter over StructureTypeObj; thin WinForms/WPF shells.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Both wrap same store; WinForms tree vs WPF converter.

#### C17-009 — Structure type tree WinForms vs WPF
- **Paths:** `UI/StructureTypesTree.cs`; `StructureTypeTree.xaml(.cs)`
- **Category:** dedup
- **Recommendation:** Prefer WPF for shared; keep WinForms while Viking docking needs it; share root-load.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Both subscribe OnCollectionChanged; similar root load.

#### C17-010 — Create-structure path triplication
- **Paths:** CreateNewStructureCommand, PlaceStructureCommand, CreateStructureAction, AnnotationToolActions, SegmentationCommand
- **Category:** dedup
- **Recommendation:** Extract `IAnnotationMutationService`; UI supplies geometry + type.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Multiple EnqueueCommand sites; Tools CreateStructureAtAsync writes Store directly.

#### C17-011 — Actions vs Commands overlap
- **Paths:** `UI/Actions/*`, `ActionViews/*`, ActionSelectionCanvasControl (~508), confirmation commands
- **Category:** split
- **Recommendation:** Actions = commit; Commands = capture UX; collapse confirmation wrappers.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Pen V2 → PathAnnotationInteractions → ActionSelection → confirmation; parallel classic Commands.

#### C17-012 — Commands vs Tools dual interaction stack
- **Paths:** `UI/Commands/*` (33 files); `Tools/*`
- **Category:** modernize
- **Recommendation:** Tools as long-term host-agnostic layer; avoid new WinForms-only commands for Jotunn features.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Jotunn AnnotationPlaceKind via Tools; Viking CommandQueue + Overlay.

#### C17-013 — Split AnnotationOverlay god-object
- **Paths:** `UI/AnnotationOverlay.cs`
- **Category:** split
- **Recommendation:** Extract load worker, hit-test, hotkeys, pen, draw (prefer shared scene drawer).
- **Effort:** L  
- **Risk:** high  
- **Evidence:** ~2158 LOC owning cache/load/input/dialogs/favorites/segmentation/Draw.

#### C17-014 — Split SegmentationCommand
- **Paths:** `SegmentationCommand.cs` (~1706); `SegmentationExtensions.cs`
- **Category:** split
- **Recommendation:** Split client/upload, mask→polygon, point-set, preview; thin Command.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Single class mixes gRPC, ImageSharp, PointSetView, timers, creation.

#### C17-015 — Preference VM + Segmentation settings coupling
- **Paths:** SegmentationCommand; prefs dialog; Global SegmentationPointRadius/Url
- **Category:** modernize
- **Recommendation:** One AnnotationSettings surface; re-read/listen so Apply updates live command.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Command repeatedly reads Global; prefs expose same props.

#### C17-016 — Modernize settings persistence
- **Paths:** Settings.Designer.cs; Global.AnnotationSettings; UserSettings XML
- **Category:** modernize
- **Recommendation:** Single model for display prefs vs hotkey XML; finish PolygonPointRadius→diameter migration.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Dual Properties.Settings + UserSettings.xml; legacy keys remain.

#### C17-017 — RelayCommand / DelegateCommand proliferation
- **Paths:** Preference VM RelayCommand; Viking.UI.WPF RelayCommands; DelegateCommand.cs
- **Category:** dedup
- **Recommendation:** One shared ICommand helper; fix/delete broken DelegateCommand ctor.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Identical “Simple RelayCommand” copies; broken ctor can NRE.

#### C17-018 — Retire Bifrost GoToLocationForm
- **Paths:** WPF GoTo/Find forms (live); `Bifrost/.../GoToLocationForm`
- **Category:** archive
- **Recommendation:** Confirm unused; archive. Pattern for other dialogs.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Overlay + Jotunn use WPF; Bifrost has commented Overlay call.

#### C17-019 — Merge/Split remain WinForms-only
- **Paths:** MergeStructuresForm, SplitStructuresForm, AnnotationMenu, LocationLink context menu
- **Category:** modernize
- **Recommendation:** When Jotunn needs merge/split: extract service + WPF shell — do not reimplement.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** WinForms only; DialogSmoke construct-only; no WPF equivalent.

#### C17-020 — WinForms property pages vs WPF StructureTypeManagement
- **Paths:** Structure*Page.cs; ListStructures/Locations; WPF StructureTypeManagementForm
- **Category:** dedup
- **Recommendation:** WinForms Viking-only; don’t add features unless mirrored; retire change-log WIP page.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Many [PropertyPage]; WPF uses StructureTypeObjViewModel; change-log page commented TODO.

#### C17-021 — PropertyPage index collision
- **Paths:** `StructureNotesPage.cs`, `StructureChildStructuresPage.cs`
- **Category:** bug
- **Recommendation:** Assign unique `[PropertyPage(typeof(Structure), n)]` indices.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Both use index `2`.

#### C17-022 — StructureType.ChildChanged remove bug
- **Paths:** `WebAnnotation/ViewModel/StructureType.cs`
- **Category:** bug
- **Recommendation:** Fix `remove => modelObj.ChildChanged -= value`.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Verified: both add and remove use `+=` (lines 72–73).

#### C17-023 — Net10AnnotationOverlay stub vs WinForms overlay
- **Paths:** `Net10AnnotationOverlay.cs`; `UI/AnnotationOverlay.cs`; Actions using CurrentOverlay
- **Category:** modernize
- **Recommendation:** Keep stub minimal; never grow draw/input into stub; inject host interfaces long-term.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Same type name; stub documents “No input or draw.”

#### C17-025 — PenAnnotationViewForm / PenMode dual UX
- **Paths:** PenAnnotationViewForm*; Global.PenMode; AnnotationMenu; Overlay branches
- **Category:** modernize
- **Recommendation:** If form legacy, archive; single PenMode; no third pen UX in Jotunn.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Overlay moved to PenMode; form still menu-creatable.

#### C17-026 — Command sprawl / deep inheritance
- **Paths:** PlaceCurvesWithPen*, PlaceOpenCurve, PlacePolyline, Translate*
- **Category:** modernize
- **Recommendation:** Prefer composition (geometry strategy + commit); align with Tools.
- **Effort:** L  
- **Risk:** med  
- **Evidence:** Deep bases; 300–440 LOC files; 33 command files.

#### C17-027 — Test gap: overlay, prefs, tools, most commands
- **Paths:** `WebAnnotationTests/*`
- **Category:** test-gap
- **Recommendation:** Extract pure functions; prefs clamp/apply tests; Tools place/commit; stop testing only copy-pasted helpers.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** SegmentationCommandTests reimplements helpers; few Overlay/prefs/WPF VM tests.

#### C17-028 — SqlServerTypes loader in WPF controls
- **Paths:** `WebAnnotationWPFControls/SqlServerTypes/Loader.cs`
- **Category:** modernize
- **Recommendation:** Keep with geometry/SQL host, not UI controls (unless design-time required).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Loader under WPF controls; unrelated to dialogs/VMs.

#### C17-029 — UserSettings hotkey dispatch Overlay-centric
- **Paths:** AnnotationOverlay key handling; webannotationusersettings.cs; Global UserSettings
- **Category:** modernize
- **Recommendation:** Shared hotkey resolver for Overlay + Jotunn; do not copy XSD switch into Jotunn.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Overlay switches on XSD actions; Jotunn uses GlobalCommands.

#### C17-030 — SegmentationServiceUrl user-configurable
- **Paths:** Global.AnnotationSettings.SegmentationServiceUrl; SegmentationCommand; prefs
- **Category:** security
- **Recommendation:** Validate scheme/host allowlist; document trusted endpoints.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** User-scoped URL; gRPC to that endpoint.

**Deduped aliases (not listed separately):** C17-024 → C16-001

**Cross-chunk:** - View/LocationObjRenderer (C16); AnnotationScene/Tools/Net10Overlay (C16/C18); SectionSceneHost (C16); Global/Settings; Jotunn MainWindowShell (C18); Viking.UI.WPF prefs pattern (C11); WebAnnotationModel Store (C10); Segmentation gRPC (C03); Bifrost GoTo (C05) ---

*Chunk opportunity count (after dedupe): 29*

---

### C18 — Jotunn shell

**Summary:** Live Jotunn is **net10 + direct composition (no MEF)**. Quarantined Prism/MEF MorphologyView/AnnotationView/MonogameWPFLibrary remain on disk. WPF Media3D tile stack still compiles in VolumeViewModel while live tiles use Viking.Scene. Annotation path correctly shared — fork risk high if duplicated. **Top 3:** (1) Delete quarantined MEF trees; (2) strip WPF Media3D tile VMs from VolumeViewModel; (3) standing anti-fork constraint on AnnotationScene/SectionAnnotationsView.

#### C18-001 — Delete quarantined MorphologyView MEF module
- **Paths:** `Clients/Jotunn/MorphologyView/**`
- **Category:** delete
- **Recommendation:** Delete tree; do not port to net10.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** QUARANTINED.md; net48 + Prism.Mef; not in .sln; hardcoded OData URL.

#### C18-002 — Delete quarantined AnnotationView + AnnotationViewModel
- **Paths:** `AnnotationView/**`, `AnnotationViewModel/**`
- **Category:** delete
- **Recommendation:** Delete both; live path WebAnnotation/AnnotationScene.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Both QUARANTINED; net48 Prism; empty stub SectionAnnotationViewModel; not in .sln.

#### C18-003 — Delete MonogameWPFLibrary (Morphology-only)
- **Paths:** `Clients/Jotunn/MonogameWPFLibrary/**`
- **Category:** delete
- **Recommendation:** Delete with MorphologyView.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only MorphologyView references; MonoGame 3.5.1; not in .sln.

#### C18-004 — Remove Compile-removed MEF shell leftovers
- **Paths:** BootStrapper.cs, RegionNames.cs, IShellParameters.cs, VisibleRegionInfo.cs, SqlServerTypes/Loader.cs under Jotunn/
- **Category:** delete
- **Recommendation:** Delete files already Compile Remove’d.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Jotunn.csproj Compile Remove; BootStrapper : MefBootstrapper.

#### C18-005 — Remove Compile-removed VolumeView MEF/WPF3D leftovers
- **Paths:** VikingVolumeViewModule, Test.cs, PyramidViewer, converters, Settings/Resources Designers
- **Category:** delete
- **Recommendation:** Delete already excluded files.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** VolumeView.csproj excludes; PyramidViewer is WPF Viewport3D.

#### C18-006 — Drop MEF RegionNames from live Common
- **Paths:** `JotunnCommon/RegionNames.cs`
- **Category:** delete
- **Recommendation:** Delete; only MEF modules call AddToRegion.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Grep: RegionNames only in Morphology/Annotation/VikingVolumeView modules.

#### C18-007 — Retire or inline IShellParameters
- **Paths:** `JotunnCommon/IShellParameters.cs`; `ShellParametersService.cs`
- **Category:** modernize
- **Recommendation:** App uses concrete ShellParameterService; drop MEF export surface.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** BootStrapper was sole ComposeExportedValue.

#### C18-008 — Remove IShellView / ShowView remnant
- **Paths:** `IShowView.cs`; MainWindowShell
- **Category:** delete
- **Recommendation:** Drop interface; ShowView only calls Show(), never via interface.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No IShellView callers outside MainWindow.

#### C18-009 — Strip WPF Media3D tile VMs from VolumeViewModel
- **Paths:** TileViewModel, TileMappingViewModel, TileViewModelCache, ImageBrushCache, SectionViewModel.DefaultMapping, Global.BrushCache
- **Category:** delete
- **Recommendation:** Remove; live cache is Viking.Scene.TileLoadEnvironment.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** SectionSceneHost uses TileLoadEnvironment; WPF TileViewModel uses GeometryModel3D.

#### C18-010 — Delete unused SectionStackViewModel
- **Paths:** `VolumeViewModel/SectionStackViewModel.cs`
- **Category:** delete
- **Recommendation:** Delete (never referenced).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Class only; no external refs.

#### C18-011 — Delete unused VolumeViewModelSharedView
- **Paths:** `VolumeViewModel/VolumeViewModel.cs`
- **Category:** delete
- **Recommendation:** Delete unused class (~40 lines).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Grep: definition only.

#### C18-012 — Delete unused NumberGrid control
- **Paths:** `JotunnUIControls/NumberGrid.cs` (~236)
- **Category:** delete
- **Recommendation:** Delete; live grid is VirtualizingGrid.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** No XAML/CS refs outside file.

#### C18-013 — Empty / stale theme dictionaries
- **Paths:** `Jotunn/Themes/Generic.xaml` (empty); VolumeView Themes Generic
- **Category:** delete
- **Recommendation:** Confirm SectionList need; remove empty Jotunn Generic.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Empty/commented; WPF card template may be unused by MonoGame host.

#### C18-014 — Stale packages.config on SDK projects
- **Paths:** Jotunn, VolumeView, VolumeViewModel, JotunnCommon, JotunnUIControls (+ quarantined)
- **Category:** modernize
- **Recommendation:** Delete Prism.Mef-era packages.config.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** SDK net10 projects; packages.config still Prism/MEF.

#### C18-015 — Stale app.config on migrated projects
- **Paths:** Jotunn/app.config, VolumeViewModel/app.config, etc.
- **Category:** modernize
- **Recommendation:** Audit binding redirects; drop if unused under net10.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Present beside SDK projects.

#### C18-017 — Thin MainWindow command surface
- **Paths:** MainWindowShell; GlobalCommands
- **Category:** modernize
- **Recommendation:** Optional command-to-tool map; lower priority than deletes.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** ~25 CommandBindings → AnnotationToolHost one-liners.

#### C18-018 — Remove debug HitTest tracing
- **Paths:** MainWindowShell HitTestCore overrides
- **Category:** delete
- **Recommendation:** Remove Trace.WriteLine hit diagnostics.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Logs control.Name + " was hit".

#### C18-019 — BindingErrorTraceListener always on
- **Paths:** BindingErrorTraceListener.cs; MainWindow ctor
- **Category:** modernize
- **Recommendation:** Gate behind DEBUG/settings.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** SetTrace() always in ctor.

#### C18-020 — Consider collapsing tiny Common project
- **Paths:** JotunnCommon after RegionNames/IShellParameters gone
- **Category:** modernize
- **Recommendation:** Merge GlobalCommands into shell/VolumeViewModel if desired.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** ~5 source files today.

#### C18-021 — Duplicate TileLoadEnvironment bind
- **Paths:** App.xaml.cs + SectionSceneHost.OnLoaded
- **Category:** dedup
- **Recommendation:** Document intentional (HWND timing) or centralize with lifecycle comments.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Comments explain re-bind after HWND.

#### C18-022 — Do not fork annotation renderer
- **Paths:** AnnotationScene, SectionAnnotationsView, SectionSceneRenderer, SectionSceneHost; dead AnnotationView
- **Category:** security
- **Recommendation:** Keep single SectionAnnotationsView + LocationObjRenderer path.
- **Effort:** S  
- **Risk:** high  
- **Evidence:** AnnotationScene docstring; QUARANTINED.md forbids revive.

#### C18-023 — Guard WebAnnotation dual-TFM edits
- **Paths:** `WebAnnotation.csproj` (`net48;net10.0-windows`)
- **Category:** modernize
- **Recommendation:** Shell net10-only; library multi-targets for Viking — build both until Viking retires.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Multi-TFM; #if !NETFRAMEWORK in AnnotationScene.

#### C18-024 — Jotunn shell already net10 — no further TFM work
- **Paths:** Live Jotunn projects
- **Category:** modernize
- **Recommendation:** Done for shell; remaining net48 under Clients/Jotunn is quarantined only.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Live csprojs net10.0-windows.

#### C18-025 — Shared deps still dual-TFM (gated outside C18)
- **Paths:** Viking.Scene, WebAnnotation, WebAnnotationWPFControls
- **Category:** modernize
- **Recommendation:** Retire net48 TFMs only when Viking/VikingAU stop needing them.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Multi-target shared libs.

#### C18-026 — GlobalCommands.MeasureDistance ≠ CLI MeasureDistance
- **Paths:** JotunnCommon/GlobalCommands vs Clients/MeasureDistance
- **Category:** archive
- **Recommendation:** Naming note only; no shared code.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Distinct projects; CLI not in Jotunn refs.

#### C18-027 — VikingAU has no Jotunn coupling
- **Paths:** Clients/VikingAU
- **Category:** archive
- **Recommendation:** Note: net48 Viking sibling; no Jotunn refs.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Grep: no Jotunn hits.

#### C18-028 — MorphologyView Module PostBuild paths
- **Paths:** MorphologyView.csproj
- **Category:** delete
- **Recommendation:** Goes away with C18-001 (Modules copy for dead MEF).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** OutputPath/PostBuild into Jotunn Modules.

**Deduped aliases (not listed separately):** C18-016 → C17-006

**Cross-chunk:** - Scene host deep dive → C16; shared annotation → C16/C17; Viking.Scene dual TFM → C13/C15; prefs dedupe → C17-006; AnnotationVizLib only via MorphologyView (delete severs); MeasureDistance naming only; VikingAU independent ---

*Chunk opportunity count (after dedupe): 27*

---

### C19 — gRPC annotation server

**Summary:** Modern net10 gRPC + ConnectomeDataModelCore (EF Core 10 + NTS). Clients on WebAnnotationModel.gRPC. Still carries Visual ReCode scaffolding, dual WKT/circle workarounds, ~45 unused EFPT procedures. Parity incomplete vs WCF. **Top 3:** (1) Unify circle WKT + trim unused EFPT; (2) implement/drop change-log RPCs + restore username preservation; (3) plan EF6→Core retirement with C21/C20.

#### C19-001 — Retire / trim unused EF Power Tools procedures
- **Paths:** AnnotationContextProcedures.cs (~1490); Select*Result.cs; efpt.config.json
- **Category:** delete
- **Recommendation:** Keep only gRPC-called procs; slim EFPT allowlist.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** gRPC calls ~9 + FullAsync; **45** scaffolded methods unused by gRPC.

#### C19-002 — Dead CurvePolygonConverter
- **Paths:** `ConnectomeDataModelCore/ValueConverters/CurvePolygonConverter.cs`
- **Category:** delete
- **Recommendation:** Delete; circle via interceptor + PersistCircleShapesAsync.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Never registered; only self-references.

#### C19-003 — Unify circle WKT emitters
- **Paths:** GeometryExtensions.ToCircleWKT; GeometrySqlTypeMapper ToWKT/FromWKT; Location.EF.Conversion; WebAnnotationModel.gRPC Locations converter
- **Category:** dedup
- **Recommendation:** Single canonical circle WKT in GeometryOGCMapper; delete dual emitters + CIRCULARSTRING strip regex.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Two incompatible WKT shapes; conversion strips CIRCULARSTRING for ParseWKT.

#### C19-004 — Deduplicate Location/Structure service helpers
- **Paths:** LocationService.cs; StructureService.cs
- **Category:** dedup
- **Recommendation:** Extract BoundsOf, Failure/RpcException, Persist*Links, Attach*Links, ToUtcTimestamp.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Near-identical helpers across services/converters.

#### C19-005 — Replace custom CollectionExtensions.Chunk with BCL
- **Paths:** `GrpcAnnotationService/CollectionExtensions.cs`
- **Category:** modernize
- **Recommendation:** Use Enumerable.Chunk (net10); delete custom chunker.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Custom chunker for SQL Contains batching on net10.

#### C19-006 — Move protos to gRPC_Protos
- **Paths:** gRPCAnnotationServiceTypes/Protos/*.proto; grpc-protos-folder rule
- **Category:** modernize
- **Recommendation:** Relocate under gRPC_Protos/Annotation/; retarget Protobuf Include.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Rule requires gRPC_Protos; annotation protos live under types project (Visual ReCode headers).

#### C19-007 — Modernize AnnotationServiceTypes.gRPC package versions
- **Paths:** AnnotationServiceTypes.gRPC.csproj vs GrpcAnnotationService.csproj
- **Category:** modernize
- **Recommendation:** Align Protobuf/Grpc.Tools with server (3.35 / 2.80).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Types: Protobuf 3.19.1 / Grpc 2.43; Server: 3.35.1 / AspNetCore 2.80.

#### C19-008 — Implement or remove change-log RPCs
- **Paths:** GetLocationChangeLog / GetStructureChangeLog; empty Result DTOs; WCF AnnotateService
- **Category:** bug
- **Recommendation:** Implement real columns or remove RPCs; don’t leave Unimplemented if clients call.
- **Effort:** L (impl) / S (remove)  
- **Risk:** high if clients depend  
- **Evidence:** gRPC Unimplemented; WCF location change-log still works via EF6.

#### C19-009 — Restore volume-only update username preservation
- **Paths:** LocationService.ApplyUpdate; WCF LocationExtensions.Sync
- **Category:** bug
- **Recommendation:** Port WCF “volume-shape-only → don’t overwrite Username” into gRPC ApplyUpdate.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** WCF skips Username on volume-only; gRPC always stamps Username + LastModified.

#### C19-010 — Dual data models EF6 + EF Core
- **Paths:** ConnectomeDataModel (EF6); ConnectomeDataModelCore (EF Core 10)
- **Category:** modernize
- **Recommendation:** Migrate WCF (C21) and OData (C20) to Core; document Core as SoT for new work.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** WCF→EF6; gRPC→Core; OData still legacy; same SQL.

#### C19-011 — Hand-written mosaic SP vs scaffolded broken variant
- **Paths:** AnnotationContextProcedures.UserDefined.cs FullAsync; scaffolded SelectSectionAnnotationsInMosaicBoundsAsync
- **Category:** dedup
- **Recommendation:** Keep ADO.NET multi-result FullAsync only; ignore/delete scaffolded single-result wrapper.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** FullAsync reads four sets + bypasses interceptor; scaffolded unused.

#### C19-012 — Circle persistence modernization
- **Paths:** SqlServerCircleShapeCommandInterceptor; PersistCircleShapesAsync; Location EF conversion
- **Category:** modernize
- **Recommendation:** Long-term POINT+Radius or NTS curves; short-term one repository write path.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Interceptor rewrites reads; writes raw STGeomFromText; NTS can’t round-trip CurvePolygon.

#### C19-013 — Dead / commented Visual ReCode mapping residue
- **Paths:** Structure.EF.Conversion.cs; StructureType.EF.Conversion.cs
- **Category:** delete
- **Recommendation:** Remove commented LocationLink blocks and obsolete Sync/To* duplication.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Multi-line commented link mapping; StructureType hardcodes MarkupType/HotKey.

#### C19-014 — Hardcoded StructureType MarkupType / HotKey
- **Paths:** StructureType.EF.Conversion.cs
- **Category:** bug
- **Recommendation:** Map MarkupType/HotKey from DB ↔ proto (add fields if missing).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Create always sets constants; ToProtobuf omits MarkupType.

#### C19-015 — WCF-only ICircuit / graph APIs not on gRPC
- **Paths:** WCF ICircuit / AnnotateService graph methods; gRPC protos (absent)
- **Category:** archive
- **Recommendation:** Port for viz need, or keep on WCF/OData with deprecation plan.
- **Effort:** L (port) / S (document)  
- **Risk:** med for viz  
- **Evidence:** getGraph/getSynapses/ApproximateStructure* on WCF only.
- **See also:** Deduped with C21-007 (WCF→gRPC parity / retirement overlap); those IDs are not listed separately.

#### C19-016 — GetDeletedLocations / ApproximateStructureLocation parity gaps
- **Paths:** WCF AnnotateService; gRPC Location/Structure
- **Category:** archive
- **Recommendation:** Document intentional (deletes in region responses) or add RPCs.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** WCF exposes; gRPC embeds deletes in section/region responses.

#### C19-017 — Legacy binary timestamp on one LocationLinks RPC
- **Paths:** Location.proto GetLocationLinksForSectionRequest.modified_after_this_time
- **Category:** modernize
- **Recommendation:** Align with google.protobuf.Timestamp; keep decoder during migration.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** int64 ToBinary-style vs Timestamp elsewhere.

#### C19-018 — Unused DbSets on gRPC path
- **Paths:** StructureTemplates, UserActivities, StructureSpatialCaches
- **Category:** split
- **Recommendation:** Keep for OData/future; don’t expose via gRPC until needed.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** No GrpcAnnotationService refs; OData uses StructureSpatialCaches.

#### C19-019 — MorphologyPaths / UDT dead weight in Core
- **Paths:** StoredProcedures/MorphologyPaths.cs; UDT/*; Functions.Partial
- **Category:** archive
- **Recommendation:** Confirm no consumer; remove or move to morphology project.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** EntityFrameworkExtras wrapper; no gRPC usage.

#### C19-020 — gRPC service integration test gap
- **Paths:** gRPC_Tests/LocationTests.cs; ConnectomeDataModelCoreTests
- **Category:** test-gap
- **Recommendation:** Cover circle create/update, links, incremental deletes, auth, merge/split.
- **Effort:** L  
- **Risk:** low  
- **Evidence:** Single hard-coded GetLocationByID; CoreTests unit-level only.

#### C19-021 — Auth/policy modernization notes
- **Paths:** Startup.cs PolicySchemeSelector JWT + introspection
- **Category:** security
- **Recommendation:** Keep dual JWT/opaque; secrets via env only; guard sensitive logging.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Viking.Annotation scope; empty appsettings secrets; docker template placeholders.

#### C19-022 — RpcException maps most failures to Unknown
- **Paths:** Services/*.cs Failure helpers
- **Category:** modernize
- **Recommendation:** Map concurrency/FK/validation to FailedPrecondition/InvalidArgument/AlreadyExists.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Catch-all → Unknown except few NotFound/InvalidArgument.

#### C19-023 — Duplicate StructureType ToStructureType vs Sync
- **Paths:** StructureType.EF.Conversion.cs; StructureTypeService.cs
- **Category:** dedup
- **Recommendation:** One apply-to-entity method (mirror Location ApplyUpdate).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Nearly duplicate field assignments.

#### C19-024 — FromWKT unreachable throw / regex parser maintenance
- **Paths:** GeometrySqlTypeMapper/FromWKT.cs
- **Category:** modernize
- **Recommendation:** Remove dead throw; consider NTS WKTReader + custom circles only (with C19-003).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Unreachable throw after switch; custom regex vs NTS readers.

#### C19-025 — Streaming RPCs — share query core with unary
- **Paths:** LocationService.Stream*; unary region getters
- **Category:** dedup
- **Recommendation:** Factor shared mosaic-region load/chunk pipeline.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Stream reimplements batching/AttachLocationLinks/DeletedIdsSince.

#### C19-026 — GetLocationLinksForSectionInMosaicRegion uses BoundingRectangle
- **Paths:** Location.proto; region Geometry elsewhere
- **Category:** modernize
- **Recommendation:** Standardize on Geometry (or always bbox) for one BoundsOf helper.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Most region RPCs take Geometry; this takes BoundingRectangle.

#### C19-027 — Startup still classic Startup/Program
- **Paths:** Program.cs; Startup.cs
- **Category:** modernize
- **Recommendation:** Optional minimal hosting; low priority.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** UseStartup&lt;Startup&gt;() on net10.

#### C19-028 — WCF GraphClasses / ErrorCorrection as C21 archive
- **Paths:** AnnotationService GraphClasses; ErrorCorrection
- **Category:** archive
- **Recommendation:** Track under C21; don’t reimplement circuit graph in C19 without product need.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Graph only in WCF; Core scaffolds network SPs partially used.

#### C19-029 — IncludeConnectionString in EFPT config
- **Paths:** efpt.config.json; DesignTimeDbContextFactory
- **Category:** security
- **Recommendation:** Ensure no secrets committed; prefer user-secrets/env.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** IncludeConnectionString: true.

#### C19-030 — Shared auth caller naming — extend to WCF later
- **Paths:** Services/AnnotationRpc.cs
- **Category:** modernize
- **Recommendation:** Keep CallerName; when consolidating C21, replace ServiceModelUtil with same claim precedence.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Documents JWT name/preferred_username/sub vs WCF util.

**Cross-chunk:** - C21 WCF parity (username, change logs, ICircuit, EF6 exit) - C10 types/protos/clients (WKT, packages, MarkupType, timestamps) - GeometryOGCMapper shared with WebAnnotationModel - C20 OData still on EF6 — blocks Core-only DbSet cleanup - Identity JWT/introspection ---

*Chunk opportunity count (after dedupe): 30*

---

### C20 — OData + DataExport

**Summary:** ConnectomeODataV4 still **net48 + EF6** while gRPC uses Core. Controllers scaffold-heavy/duplicated; write CRUD commented dead. DataExport half-modern (net9): OData-over-HTTP, duplicated controllers, stubbed `$query`, leftover SqlServerTypes, OData client TFM skew. **Top 3:** (1) Dual EF models block modernization; (2) delete dead/duplicated OData surface; (3) finish DataExport (shared helpers, PathBase, `$query`, client align).

#### C20-001 — Migrate ConnectomeODataV4 to ASP.NET Core OData
- **Paths:** ConnectomeODataV4.csproj; Global.asax.cs; Program.cs; Web.config
- **Category:** modernize
- **Recommendation:** Replace System.Web / AspNet.OData 7.x with AspNetCore.OData on modern TFM.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** net48 Library; Microsoft.AspNet.OData 7.7.8; IIS hosting.

#### C20-002 — Delete unused ASP.NET Core stub Program.cs
- **Paths:** ConnectomeODataV4/Program.cs
- **Category:** delete
- **Recommendation:** Remove stub (Compile Remove’d; TODOs never implemented).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Excluded from compile; no Core packages in csproj.

#### C20-003 — Align EF package versions on OData path
- **Paths:** OData csproj EF 6.5.1; ConnectomeDataModel 6.4.4
- **Category:** modernize
- **Recommendation:** Unify EF6 version until Core migration.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Version skew across projects.

#### C20-004 — Remove unused appsettings.json on Framework OData
- **Paths:** ConnectomeODataV4/appsettings.json
- **Category:** delete
- **Recommendation:** Delete or wire after Core migration.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Framework reads Web.config; appsettings unused.

#### C20-005 — Retire Unity DI when moving to Core
- **Paths:** Global.asax.cs; Unity packages
- **Category:** modernize
- **Recommendation:** Use IServiceCollection on Core (with C20-001).
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Unity registers ConnectomeEntities + ILogger.

#### C20-006 — OPTIONS / CORS cleanup
- **Paths:** OptionsModule.cs; Web.config
- **Category:** dedup
- **Recommendation:** One CORS/OPTIONS strategy.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** OptionsModule returns 200 with headers commented; Web.config has CORS headers.

#### C20-007 — Retarget OData to ConnectomeDataModelCore
- **Paths:** ConnectomeODataV4; ConnectomeDataModel; ConnectomeDataModelCore
- **Category:** modernize
- **Recommendation:** After Core host: inject AnnotationContext; remap network/spatial procs.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** OData→EF6; gRPC→Core; same schema.

#### C20-008 — Eliminate dual network procedure wrappers
- **Paths:** NetworkProcedures.cs / ConnectomeEntities; AnnotationContextProcedures
- **Category:** dedup
- **Recommendation:** One SelectNetwork* implementation (prefer Core async).
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Parallel network/spatial procs in both models.

#### C20-009 — Drop stale generated EF6 context twin
- **Paths:** ConnectomeDataModel.Context.cs (Compile Remove’d); DataModel.Context.cs
- **Category:** delete
- **Recommendation:** Delete unused twin; clarify T4 outputs.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** csproj removes ConnectomeDataModel.Context.cs.

#### C20-010 — Resolve EntityFrameworkExtras package + project double reference
- **Paths:** ConnectomeDataModel.csproj
- **Category:** modernize
- **Recommendation:** Use NuGet **or** Servers/Libs project, not both.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Both PackageReference and ProjectReference.

#### C20-011 — Schema/API shape drift: Structure links navigation
- **Paths:** EF6 Structure SourceOfLinks/TargetOfLinks vs Core Structure; StructureLink keyless
- **Category:** modernize
- **Recommendation:** Plan EDM/key mapping before OData→Core.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Core lacks link collections; StructureLink keyless in README.

#### C20-012 — Naming / casing divergence (ID vs Id)
- **Paths:** Entity classes both models
- **Category:** modernize
- **Recommendation:** Adapter/DTO for EDM stability if switching store.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** EF6 ID/TypeID; Core Id/TypeId + Column attributes.

#### C20-013 — Remove commented scaffolded CRUD
- **Paths:** OData Controllers/*
- **Category:** delete
- **Recommendation:** Delete commented PUT/POST/PATCH/DELETE blocks.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Write CRUD wrapped in `/* */` across controllers.

#### C20-014 — Remove or wire SelectStructureLocationsController
- **Paths:** SelectStructureLocationsController.cs; WebApiConfig.GetModel
- **Category:** delete
- **Recommendation:** Delete or add entity set + tests.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Controller exists; no EntitySet in GetModel.

#### C20-015 — Add controller for PermittedStructureLinks or drop from EDM
- **Paths:** WebApiConfig.AddPermittedStructureLinks; no controller
- **Category:** bug
- **Recommendation:** Implement controller or remove entity set.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** EDM registers set; no controller file.

#### C20-016 — Deduplicate Structures vs StructureSpatialCaches controllers
- **Paths:** StructuresController.cs; StructureSpatialCachesController.cs
- **Category:** dedup
- **Recommendation:** Extract shared helpers; keep entity-specific + network on right controller.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Near-identical GetLocations/Children/Scale/Links/Labels (~524 LOC combined).

#### C20-017 — Single owner for unbound Scale()
- **Paths:** Both controllers + WebApiConfig.AddScaleType
- **Category:** dedup
- **Recommendation:** One ODataRoute Scale() implementation.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Duplicate GetScale().

#### C20-018 — Single owner for StructureLocationLinks / DistinctLabels
- **Paths:** Same controllers
- **Category:** dedup
- **Recommendation:** One route owner each.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Duplicate ODataRoute methods.

#### C20-019 — Remove obsolete leaking helper
- **Paths:** WebApiConfig.StructureLocationLinks(long)
- **Category:** delete
- **Recommendation:** Delete Obsolete helper with dispose leak note.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Marked Obsolete; unused.

#### C20-020 — Remove generator scaffold comments
- **Paths:** All OData controllers
- **Category:** delete
- **Recommendation:** Delete “WebApiConfig may require…” blocks (wrong namespaces).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Present at top of every controller.

#### C20-021 — Fix GetStructureLinks full materialization
- **Paths:** StructureLinksController.GetStructureLinks
- **Category:** bug
- **Recommendation:** Delete `StructureLink[] sl = [.. _db.StructureLinks];` — loads entire table for nothing.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Materialize then returns query.

#### C20-022 — Fix composite-key GetStructureLink lookups
- **Paths:** StructureLinksController
- **Category:** bug
- **Recommendation:** Align with EDM composite key (SourceID, TargetID, Bidirectional) or document intentional by-source API.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** HasKey composite; GetStructureLink takes single long key filtered by SourceID.

#### C20-023 — Remove dead NetworkCells remnants
- **Paths:** Controllers + WebApiConfig commented NetworkCells
- **Category:** delete
- **Recommendation:** Purge commented function/implementations.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Commented in EDM and both structure controllers.

#### C20-024 — Cap OData MaxTop for safety
- **Paths:** WebApiConfig.Register
- **Category:** security
- **Recommendation:** Prefer explicit MaxTop (or PageSize).
- **Effort:** S  
- **Risk:** med  
- **Evidence:** MaxTop(null) allows unbounded queries.

#### C20-025 — DistinctLabels signature vs EDM
- **Paths:** StructuresController.DistinctLabels(ODataActionParameters)
- **Category:** bug
- **Recommendation:** Verify function vs action pattern against clients/tests.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** EDM collection function; method takes ActionParameters; AnnotationVizLibTests references DistinctLabels.

#### C20-026 — Extract shared export controller base
- **Paths:** NetworkController, MorphologyController, MotifController
- **Category:** dedup
- **Recommendation:** Shared GetODataUrl/GetVolumeUrl/output-dir/filename helpers.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Identical private methods across three controllers.

#### C20-027 — Collapse duplicate GET/POST export actions
- **Paths:** Same controllers
- **Category:** dedup
- **Recommendation:** Shared private ExportX(ids); keep multiple route attributes.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Nearly identical GET/POST bodies.

#### C20-028 — Implement or remove OData $query ID resolution
- **Paths:** Utils/RequestVariables.cs
- **Category:** bug
- **Recommendation:** Wire AnnotationVizLibODataClient or remove query/$query params.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** GetIDsFromQuery returns empty + Trace TODO.

#### C20-029 — Remove dead DAE export blocks
- **Paths:** MorphologyController commented PostDAE/GetDAE
- **Category:** delete
- **Recommendation:** Delete commented DAE blocks.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Large `/* */` using old AppSettings.

#### C20-030 — Remove unused SqlServerTypes from DataExport
- **Paths:** DataExport/SqlServerTypes/; csproj native DLLs
- **Category:** delete
- **Recommendation:** Remove; DataExport talks OData not SQL spatial; no LoadNativeAssemblies calls.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Loader present; no call sites; spatial DLLs still copied.

#### C20-031 — Fix csproj Content includes of source
- **Paths:** DataExport.csproj Content Include Controllers/Utils
- **Category:** modernize
- **Recommendation:** Remove Content includes of compiled sources.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Controllers/Utils compiled and also Content.

#### C20-032 — Honor PathBase from Docker config
- **Paths:** Program.cs; appsettings.Docker.json
- **Category:** bug
- **Recommendation:** Add UsePathBase for `/dataexport`.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** PathBase in Docker json; Program has no UsePathBase.

#### C20-033 — Soften hardcoded web.config publish paths
- **Paths:** DataExport/web.config
- **Category:** modernize
- **Recommendation:** Template per environment (not hardcoded C:\Services\Release\...).
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Hardcoded processPath vs Docker inetpub layout.

#### C20-034 — Bump DataExport TFM toward net10
- **Paths:** DataExport.csproj net9 vs Core net10
- **Category:** modernize
- **Recommendation:** Align Export with AnnotationVizLib/OData client stack.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** TFM skew vs ConnectomeDataModelCore.

#### C20-035 — Stream exports instead of disk PhysicalFile
- **Paths:** Controllers writing under Output/
- **Category:** modernize
- **Recommendation:** FileStreamResult / temp + cleanup; avoid Output growth and multi-instance races.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** All actions write then PhysicalFile.

#### C20-036 — Finish MODERNIZATION_SUMMARY Next Steps
- **Paths:** DataExport docs
- **Category:** modernize
- **Recommendation:** Health/OpenAPI/caching/rate limits/Serilog still open.
- **Effort:** M  
- **Risk:** low  
- **Evidence:** Documented unfinished items.

#### C20-037 — Case-rewrite middleware → routing
- **Paths:** Program.cs middleware; RouteOptions.LowercaseUrls
- **Category:** modernize
- **Recommendation:** Prefer case-insensitive routing / legacy redirect map over regex rewrite.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Regex rewrite; /export/ has no Export controller (legacy via Scripts).

#### C20-038 — Unit-test hack hardcodes IDs
- **Paths:** RequestVariables.GetIDsFromQueryData
- **Category:** bug
- **Recommendation:** Fail tests instead of silent fake IDs `[180,476,514]`.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Comment “hack… unit testing”.

#### C20-039 — Retarget ODataClient off net48-only
- **Paths:** ODataClient.csproj; AnnotationVizLibODataClient
- **Category:** modernize
- **Recommendation:** Multi-target or regenerate for Core; currently net48 under DataExport net9 graph.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** ODataClient TFM net48; assets show net48 under net9.

#### C20-040 — Align Microsoft.OData.* versions
- **Paths:** Server 7.x; DataExport/VizLibODataClient 8.1.0; ODataClient 7.12.3
- **Category:** modernize
- **Recommendation:** One OData major across CSDL + client + runtime; regenerate after bump.
- **Effort:** M  
- **Risk:** high  
- **Evidence:** Version pins across three csproj files.

#### C20-041 — Regenerate OData client after EDM cleanup
- **Paths:** ODataClient/Generated/; CSDL
- **Category:** modernize
- **Recommendation:** Regenerate after removing dead entity sets/functions.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Generated Container mirrors server functions.

#### C20-042 — OData docs claim completion vs remaining debt
- **Paths:** IMPLEMENTATION_SUMMARY.md
- **Category:** archive
- **Recommendation:** Treat as historical; track Core migration as open.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Summary says done; Core still “optional future”.

#### C20-043 — DataExport docs vs TFM drift
- **Paths:** QUICK_START.md; MODERNIZATION_SUMMARY.md
- **Category:** archive
- **Recommendation:** Update when TFM changes (C20-034).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Explicit net9 wording.

**Cross-chunk:** - C19 Core migration / dual network procs / Structure nav shapes - C21 AnnotationService still on EF6 — blocks EF6 delete - AnnotationVizLib + ODataClient contract (C09) - UnitsAndScale / VikingWebAppSettings Scale() - VolumeAnnotationServices Dockerfile (msbuild OData + dotnet DataExport) - Reverse proxy PathBase /export/ ---

*Chunk opportunity count (after dedupe): 43*

---

### C21 — WCF AnnotationService

**Summary:** Mid-migration: Viking/VikingAU use gRPC; WCF host (~5.2k LOC, AnnotateService ~2335 measured) still live for Binary endpoints + **ICircuit** (not on gRPC). Highest-confidence deletes: orphaned `WebAnnotationModel.WCF` (~3.5k, no consumers) and `AnnotationVizLibWCFClient` (~3k, in slns but zero ProjectReferences). Retirement blocked by WebVisualization + ICircuit gap. **Top 3:** (1) Delete WCF client orphans; (2) port/replace ICircuit + fix WebViz wiring; (3) then retire AnnotationService host + types.

#### C21-004 — Archive ErrorCorrection one-off
- **Paths:** `Servers/AnnotationService/ErrorCorrection/`
- **Category:** archive
- **Recommendation:** Remove or move to tools archive.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Hardcoded SQL Express; LINQ-to-SQL; not in main solutions.

#### C21-005 — Delete WebAnnotationTest (commented shell)
- **Paths:** `Servers/AnnotationService/WebAnnotationTest/`
- **Category:** delete
- **Recommendation:** Delete; tests commented; stub Reference ~410 bytes.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** UnitTest1 methods commented.

#### C21-006 — Retire AnnotationService host after gRPC parity + WebViz migration
- **Paths:** `Servers/AnnotationService/`
- **Category:** delete
- **Recommendation:** Gate on C21-007 + C21-008; then delete host/Identity WCF/web.config/ServiceTest.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** Only in Connectome Project.sln; web.config activates Annotate.svc.

#### C21-008 — Repair or replace WebVisualization WCF client wiring
- **Paths:** WebVisualization Models/State.cs; Controllers; Service References
- **Category:** bug
- **Recommendation:** Rebind to OData/gRPC or restore compiling Service Reference.
- **Effort:** L  
- **Risk:** high  
- **Evidence:** `using AnnotationVizLib.AnnotationService` namespace absent; CreateCircuitClient called but only CreateNetworkClient exists; AnnotationService Reference not compiled.

#### C21-009 — Delete unused ConnectomeViz AnnotationService Service Reference artifacts
- **Paths:** WebVisualization/Service References/AnnotationService/; DataSources
- **Category:** delete
- **Recommendation:** After C21-008, delete unused WSDL/XSD/Reference (~73KB).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Only ObjectFileService Reference compiled.

#### C21-010 — Split AnnotateService god class
- **Paths:** AnnotateService.cs (~2335 LOC measured)
- **Category:** split
- **Recommendation:** Prefer not investing if retirement &lt;1 quarter; else split by contract like gRPC Services/*.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Single class implements 7 contracts.

#### C21-011 — Dedup EF↔DTO conversions (WCF vs gRPC)
- **Paths:** AnnotationService Service/Types/*Extensions; GrpcAnnotationService Protos/*.EF.Conversion
- **Category:** dedup
- **Recommendation:** After WCF gone keep only gRPC/Core; while dual-live treat as intentional fork.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** EF6 SqlGeometry vs NTS Core parallel conversions.

#### C21-012 — Drop AnnotationServiceTypes.WCF after consumers migrate
- **Paths:** `AnnotationServiceTypes/`
- **Category:** delete
- **Recommendation:** Replace refs with AnnotationInterfaces + gRPC protos; fix broken sln path `.csproj` vs `.WCF.csproj`.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** Referenced by WebAnnotationModel.WCF, Jotunn AnnotationViewModel, SimpleODataClient; sln points at missing name.

#### C21-013 — Remove dead AnnotationServiceTypes refs from Jotunn / SimpleOData
- **Paths:** Jotunn AnnotationViewModel.csproj; AnnotationVizLibSimpleODataClient.csproj
- **Category:** delete
- **Recommendation:** Remove WCF types refs; use AnnotationInterfaces if needed.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** AnnotationViewModel .cs has no `using AnnotationService`; SimpleOData uses AnnotationInterfaces.

#### C21-014 — Strip stale WCF client config from migrated apps
- **Paths:** Viking/app.config; VikingAU/App.config; WebAnnotationModelTest; WebAnnotationModel Objects/app.config
- **Category:** delete
- **Recommendation:** Delete Binary/Annotate.svc endpoint sections.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** VikingAU Program is gRPC; App.config still lists Annotate.svc.

#### C21-015 — Retire ServiceTest or migrate assertions to gRPC tests
- **Paths:** ServiceTest/; gRPC_Tests/; WebAnnotationModel.gRPC.Tests
- **Category:** test-gap
- **Recommendation:** Map ServiceTest cases to gRPC before WCF delete; keep unique ICircuit cases until C21-007.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** ServiceTest in Connectome Project.sln; Everything.sln has gRPC tests only.

#### C21-016 — Remove deploy cruft / dual configs
- **Paths:** DeployProduction.cmd.bak; OldDeployment.cmd; webText.config; .vsmdi; packages/repositories.config
- **Category:** delete
- **Recommendation:** Delete backup/legacy deploy artifacts.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Present; unused by build.

#### C21-017 — Remove empty WCFMetadata from Jotunn / NGVV
- **Paths:** AnnotationViewModel.csproj; VikingCore.csproj
- **Category:** delete
- **Recommendation:** Remove WCFMetadata when no Service References.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** WCFMetadata present; no matching content. (Same as C15-008.)

#### C21-018 — Dedup DBACTION / IChangeAction across type stacks
- **Paths:** AnnotationInterfaces/DBACTION.cs; AnnotationServiceTypes Types/Enums.cs; IChangeAction.cs
- **Category:** dedup
- **Recommendation:** Single source in AnnotationInterfaces.
- **Effort:** S  
- **Risk:** med  
- **Evidence:** Both define NONE/INSERT/UPDATE/DELETE; WCF has ProtoContract.

#### C21-019 — Security: WebVisualization trusts any cert + hardcoded anon creds
- **Paths:** WebVisualization/Models/State.cs
- **Category:** security
- **Recommendation:** Remove always-true cert validate; stop embedding anonymous/connectome.
- **Effort:** M  
- **Risk:** med  
- **Evidence:** NetworkCredential("anonymous","connectome"); RemoteCertificateValidate returns true.

#### C21-020 — Dual auth stack only needed for WCF lifetime
- **Paths:** AnnotationService/Identity/*; web.config identityServerBehavior
- **Category:** delete
- **Recommendation:** Delete with service; use gRPC auth middleware.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** JwtMessageInspector / RoleAuthorizationManager; UserNameOverTransport required.

#### C21-021 — Program.cs console host unused
- **Paths:** AnnotationService/Program.cs
- **Category:** delete
- **Recommendation:** Delete or document IIS-only (OutputType Library).
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Library OutputType with ServiceHost Main.

#### C21-022 — Incomplete WCF store methods (NotImplementedException)
- **Paths:** WebAnnotationModel.WCF *Store.cs
- **Category:** delete
- **Recommendation:** Do not revive; delete with C21-001.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Many Proxy* methods throw NotImplementedException.

#### C21-023 — GraphClasses only serve ICircuit
- **Paths:** AnnotationService/GraphClasses/
- **Category:** delete
- **Recommendation:** Delete with ICircuit or move into shared viz model if reimplemented.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Used by getGraph/synapse stats; not in gRPC protos.

#### C21-024 — Roles.Modify deprecated but still wired
- **Paths:** AnnotateService Roles + ICredentials.Roles()
- **Category:** modernize
- **Recommendation:** Document or drop with retirement; align Identity roles.
- **Effort:** S  
- **Risk:** low  
- **Evidence:** Comment: Modify deprecated ≈ Write; still appended.

**Deduped aliases (not listed separately):** C21-001 → C10-001; C21-002 → C09-001; C21-003 → C15-009; C21-007 → C19-015

**Cross-chunk:** - C19 gRPC replacement + ICircuit gap + auth - C10 dual EF models / conversions - C15/C17 Service References delete - C09 AnnotationVizLibWCFClient / WebAnnotationModel.WCF orphans - C05 WebVisualization / ConnectomeViz as last live WCF consumer - C20 OData still needs EF6 until Core migration ### Retirement checklist (practical order) 1. Delete orphans: C21-001, 002, 003, 005, 013, 014, 016, 017 (+ C15-008/009) 2. Port/replace ICircuit + fix WebViz: C21-007, 008, 009 (+ C19-015) 3. Map ServiceTest → gRPC: C21-015 4. Delete host + types + Identity WCF: C21-006, 012, 020, 023 5. Then EF6 ConnectomeDataModel exit with C20-007 / C19-010 --- ## Wave 3 cross-cutting themes (for parent merge) 1. *

*Chunk opportunity count (after dedupe): 20*

---

## 4. Suggested sequencing (recommendations only)

### Phase A — Safe deletes / archives (low risk, high LOC win)
1. C02-001 Identity `source/` archive/delete (**completed**; still rotate any mirrored secrets that were live)
2. C05 Bifrost + AnnotationVizLibOData stub + confirm/archive dead websites
3. C06 Neo4j keep-or-archive; if unused, archive entire island
4. C03-001 `gRPCSegmentAnything` delete-or-implement
5. C01-001 quarantine Collada schema; C01-005 retire SmoothMesh husks (C01-006 MeshXNAVizLib completed)
6. C08 dead QuadTree/GridTransform/MeshPathing stubs (Wave 2 deletes)
7. C09-001/C21-002 orphan WCF viz clients; C15-009 Service References; C21 tests/bak
8. C07 root markdown + obsolete IIS/docker script prune

### Phase B — Secrets & contract collapses
1. Rotate Identity/Neo4j/CreateUpdateDatabase credentials (C02/C06/C14)
2. C14 canonical SQL tree (SSDT source of truth)
3. C09 single OData client; migrate MorphologyView/MeasureDistance
4. C19-015/C21-007 gRPC parity checklist + WCF freeze
5. C10-001/C21-001 shared store core; plan WCF model removal
6. C20 ODataV4 upgrade-or-freeze + auth; DataExport as sole export backend

### Phase C — Dual-stack / TFM reduction
1. C13/C15/C18: Jotunn modules to net10; Viking graphics path plan
2. C11 Common naming + secrets policy sweep
3. C07 docker image without WCF when ready; refresh Sphinx away from dead C05
4. C15-002/C16-012 route Viking tiles through `SectionSceneRenderer`

### Phase D — God-file splits (behavior-preserving)
1. C01 BajajMeshGenerator / morphology splits + ReproSet offline tests
2. C08 Polygon / geometry splits; C12 Volume.cs
3. C15 SectionViewerControl / TextureReader / ExtensionManager
4. C16-001/C17 AnnotationOverlay + AnnotationScene unify; settings codegen replace
5. C19 AnnotateService parity work stays on gRPC side only

### Phase E — Product UI last
- Preference VM unification (C17-006/C18-016), Jotunn catalog cleanup, bookmark/export UX — only after shared contracts stabilize.

---

## 5. Coverage counts

| Source | Chunks | Raw opportunities | Included |
|--------|--------|-------------------|----------|
| Wave 1 aea6ec5d | C01–C07 | 137 | **Yes** |
| Wave 2 44fcff09 | C08–C14 | 206 | **Yes** |
| Wave 3 36d833ca | C15–C21 | 205 | **Yes** |
| Cross-chunk dedupe | — | −8 aliases | Folded into canonicals |
| **Total listed** | C01–C21 | **540** | |

Per-chunk counts (after dedupe):

| Chunk | Count |
|-------|-------|
| C01 | 35 |
| C02 | 30 |
| C03 | 16 |
| C04 | 18 |
| C05 | 6 |
| C06 | 14 |
| C07 | 18 |
| C08 | 30 |
| C09 | 25 |
| C10 | 32 |
| C11 | 24 |
| C12 | 28 |
| C13 | 28 |
| C14 | 39 |
| C15 | 29 |
| C16 | 19 |
| C17 | 29 |
| C18 | 27 |
| C19 | 30 |
| C20 | 43 |
| C21 | 20 |
| **Total** | **540** |

**Deduped alias map:**

- C15-002 ← C16-012 — Viking tile draw vs SectionSceneRenderer
- C16-001 ← C15-003, C17-024 — Unify AnnotationOverlay.Draw / AnnotationScene.Draw
- C15-009 ← C21-003 — WCF Service References / WCF client retirement overlap
- C17-006 ← C18-016 — Preference dialogs / module preference overlap
- C19-015 ← C21-007 — WCF→gRPC parity / retirement overlap
- C09-001 ← C21-002 — OData/WCF client collapse vs orphan WCF clients
- C10-001 ← C21-001 — WCF store archive vs WCF AnnotationService retirement

---

## Appendix — Quarantine reminders

| Area | Note |
|------|------|
| Triangle.NET | Vendored; prefer “stop editing / NuGet later” |
| Collada schema `collada_schema_1_5_fixed.cs` | Generated; do not hand-edit (C01-001) |
| NetTopologySuite under Servers/Libs | Vendored |
| EF migrations dumps | Not review targets |
| `bin`/`obj` | Ignore |

---

*Merged deliverable for plan `parallel_refactor_chunks_e5f6ff95`. Waves 1+2+3 included. Read-only; no source changes beyond this file.*
