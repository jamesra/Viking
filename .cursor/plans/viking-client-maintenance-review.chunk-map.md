# Viking Client Maintenance Review — Chunk Map

Date: 2026-09-19  
Solution: `Clients/Viking.sln`  
Entry project: `Clients/Viking/Viking/Viking.csproj` (WinExe, net48, plugins via `ReferenceOutputAssembly=false`)

Chunks were chosen from **real ProjectReference graphs**, grouped by ownership/layer so each is one project or a tight folder cluster. Geometry is excluded everywhere.

## In scope (10 chunks)

| # | Chunk | Paths | Notes |
|---|--------|-------|--------|
| 1 | App shell | `Clients/Viking/Viking/`, `Clients/Viking/VikingTests/` | Exe, Velopack, protocol/single-instance, module copy MSBuild |
| 2 | VikingCore viewer | `Clients/Viking/NGVV/` | Viewer forms, tiles/textures, commands, extension host |
| 3 | WebAnnotation commands | `Clients/Viking/WebAnnotation/UI/Commands/`, `UI/AutoPolygonize/`, `UI/Actions/`, `UI/ActionViews/` | Annotation commands, pen, segmentation, auto-polygonize |
| 4 | WebAnnotation overlay/views | Rest of `Clients/Viking/WebAnnotation/` + `Clients/Viking/WebAnnotationTests/` | Overlay, View, ViewModel, Cache; skip `Service References/` generated WCF |
| 5 | WebAnnotation model | `Clients/WebAnnotationModel/`, `Clients/WebAnnotationModelTest/` | Stores, location/structure objects; skip generated service refs |
| 6 | Graphics / XNA | `Clients/MonogameXNAGraphicsShared/`, `Clients/VikingXNAGraphics/`, `Clients/Viking/VikingXNAWinForms/` | net48 graphics; skip `.xnb` / contentproj |
| 7 | WPF UI | `Clients/Viking.UI.WPF/`, `Clients/WebAnnotationWPFControls/`, `Clients/WebAnnotationViewModels/` | Login, volume/channel, annotation dialogs |
| 8 | Volume + shared libs | `Clients/VolumeModel/`, `Clients/Common/`, `Utils/`, `UnitsAndScale/` | Volume XML/tiles/mappings; small shared helpers |
| 9 | Networking / auth / contracts | `Clients/WCFTokenInjector/`, `AnnotationServiceTypes/`, `AnnotationInterfaces/`, `SegmentationServiceTypes.gRPC/` | Client-side WCF/gRPC contracts. Skip generated gRPC/WCF stubs |
| 10 | Modules + spatial helpers | `Clients/Viking/LocalBookmarks/`, `Clients/Viking/MeasurementExtension/`, `RTree/`, `SqlGeometryUtils/`, `SqlGeometryAnnotationExtensions/` | Plugin modules + spatial utils used by the client (not Geometry) |

## Out of scope

- **Geometry** (`Geometry/`, `GeometryTests/`, Geometry.dll) — excluded by request even though referenced
- **Other clients:** Jotunn, Bifrost, MeasureDistance, VikingAU, MonogameTestbed, XNATestbed
- **Servers** (unless compiled as an inseparable client-side contract; gRPC types project is in chunk 9 as a client dependency)
- **AnnotationVizLib** + OData visualization clients (in `Viking.sln` but not referenced by Viking.exe / VikingCore / WebAnnotation)
- **GraphLib** (same: solution member, not on the Viking client reference path)
- **Triangle.NET** / TriangleNetGeometryExtensions (low-level tessellation; skip unless a client bug is obviously in the wrapper call site)
- **nornir** workspace
- `obj/`, `bin/`, generated `Reference.cs` / `*.Designer.cs` / Settings, `.xnb` content, untracked module copy output under `Clients/Viking/Viking/Modules/`

## Dependency notes (verified)

Viking.exe directly references: VikingCore, Viking.UI.WPF, SqlServerTypesLoader; plugin-builds WebAnnotation, LocalBookmarks, Measurement.

VikingCore references: VolumeModel, Common, Utils, UnitsAndScale, WCFTokenInjector, VikingXNAWinForms, VikingXNAGraphics (net48), AnnotationServiceTypes, Geometry (excluded).

WebAnnotation additionally references: WebAnnotationModel, WebAnnotationViewModels, WebAnnotationWPFControls, RTree, SqlGeometry*, SegmentationServiceTypes.gRPC, Triangle.NET.
