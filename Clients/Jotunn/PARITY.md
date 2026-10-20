# Jotunn feature parity with Viking

Jotunn (WPF) shares Viking's annotation library (`Clients/Viking/WebAnnotation`). The pieces Jotunn drives directly are:

- `AnnotationScene`, which draws, hit-tests, loads and polls sections.
- The tool framework in `WebAnnotation/Tools` (`AnnotationToolHost`, `IAnnotationTool`, `IAnnotationToolPreview`).

The tools reuse Viking's geometry and rules rather than copying them, for example:

- add/remove control point
- link candidate search
- link validity
- `UndoManager`
- property pages
- merge/split dialogs

## Hosting notes

Jotunn has no Viking shell, so it fills in what Viking's shell would have set up:

- **Viking UI state.** After login, `App.ShareSessionWithVikingState` sets `Viking.UI.State.volume`, `UserCredentials` and `UserBearerToken`.
- **Section views.** `AnnotationOverlay.SectionViewLookup` points at the scene's section views.
- **Go to location.** `AnnotationOverlay.GoToLocationHandler` points at Jotunn's go-to.
- **Property pages.** `ExtensionManager.RegisterPropertyPages(WebAnnotation)` registers them, since Viking's module loader does not run.
- **Scale.** `AnnotationBootstrap` sets `Global.Scale` (nm per pixel) from the volume.
- **`Global` initialization.** `Global`'s settings paths are computed when used, so `Global` initializes without a Viking volume.

## Done

| Viking feature | Jotunn |
|---|---|
| Key map | Done:<br>- +/- step sections (Shift steps 10); Insert also steps<br>- PageUp/PageDown zoom<br>- Arrows pan<br>- Home snaps the zoom<br>- Backspace goes to the last edited annotation<br>- F5 reloads<br>- Delete and Ctrl+D delete with confirmation<br>- Enter commits a tool or continues the trace<br>- Ctrl+C copies the position<br>- Ctrl+Z undoes |
| UserSettings hotkeys | `UserSettingsHotkeys` reads the volume's `DefaultWebAnnotationUserSettings` file, cached for a day:<br>- Structure shortcuts arm a placement for the next click (circle, curve, polygon), with their tags.<br>- Tag and terminal shortcuts act on the annotation under the pointer.<br>- Keys Viking's overlay owns are skipped. |
| Empty click | Places an armed shortcut, else the type chosen in the type list, else continues the last trace. Jotunn no longer preselects a structure type. |
| Selection, hover | A click selects. Hovering outlines the annotation and names what a click would do. |
| Move, resize circle | Drag, then save on release. Escape restores the location. Recorded for undo. |
| Vertex editing | Adjust (drag), add and remove control points on polygons, curve polygons, polylines and curves. Recorded for undo. |
| Drag to link | Location links (same structure, other section) and structure links. The line is green or red as in Viking. |
| Placement previews | Polyline/polygon rubber band and vertices. The measure line shows its length in nm or µm. |
| Context menu | Right click without panning offers:<br>- the structure's property sheet<br>- the Terminal and Off edge toggles<br>- copy location/structure ID<br>- center view<br>- merge<br>- split at a chosen link<br>- delete |
| Merge / split | Viking's dialogs: Annotation > Merge structures, and from the context menu. |
| Undo | Ctrl+Z or Annotation > Undo edit, through Viking's `UndoManager`. |
| Other users' edits | Visible sections are polled every 30 s with Viking's policy. Section views beyond `NumSectionsInMemory` are evicted. |
| Review Changes tab | Viking's WPF feed view and backfill, for Review/Admin users. The scene's store events and poll feed it. |
| UI exceptions | Reported, and the session is kept (`DispatcherUnhandledException`). |
| Parent structure | A new structure whose type has a parent type asks for its parent (`PickParentStructureTool`) before anything is saved. This applies to click placement, polylines, segmentation and pen strokes. Escape discards it. |
| SAM2 segmentation | Tab, or Tools > Segmentation:<br>- left click adds an object point, right click a background point, middle click removes the nearest point<br>- Enter or a double click accepts<br>- runs through Viking's `SegmentationViewportSession` over `SectionSceneHost` (an `ISegmentationViewport`)<br>- not yet verified against a live server |
| Change shape | Context menu:<br>- Circle<br>- Convert to polygon<br>- Segment to polygon (circles)<br>- Resegment (polygons)<br>- Simplify shape<br>All recorded for undo. |
| Change-type hotkeys | Circle converts in place. Curves and polygons are clicked out from the pointer and replace the location's shape. |
| Pen Mode | Numpad * or Shift+8. A stroke offers what Viking's overlay offers (retrace, redraw, link, continue, convert circle, cut hole, create structure), in a menu at the pointer. Pointing at a choice draws Viking's preview of it; the retraced cell's children are hidden during the stroke. |
| SBFSEM-tools | "Open in SBFSEM-tools" in the location menu. |
| Property sheet list menus | Viking's context menu providers are registered (`ExtensionManager.RegisterContextMenuProviders`). |
| Segmentation service | Tools > Select segmentation service (Viking's window). Tools and auto-polygonize switch to the chosen service. |
| Dilate/erode mask | Polygon menu > Change shape > Dilate/erode mask:<br>- the wheel grows or shrinks the rasterized polygon (Shift for fine steps, Ctrl still zooms)<br>- Enter segments from the edited mask, as Viking does |
| Segmentation background | Other structures in view are background points plus a background mask, as in Viking. |
| Export | Location menu Export > Tulip URL (morphology, network), and Annotations > Export motifs, when the volume has an export service. |
| Annotations menu | Favorite structure types (F6), Pen mode, Randomize all colors, Update volume positions (Review/Admin; progress in the status bar). |
| Unmarked process terminals | Location menu. The structure's unfinished branch ends, grouped by section; choosing one goes there. |
| Random color | Polygons, for the session, as in Viking. |
| Auto-polygonize | Viking's `AutoCirclePolygonizeController` runs unchanged over `SectionSceneHost` (an `IAutoPolygonizeHost`):<br>- double click accepts a proposal; right click opens Accept / Add points / Dismiss<br>- "Add points" uses the segment tool<br>- Tools > Auto-polygonize circles toggles Viking's setting<br>- not yet verified against a live server |

## Not yet in Jotunn

- **Auto-polygonize in a grid of several sections**: proposals cover the active cell's section only (sized from that cell's view).
