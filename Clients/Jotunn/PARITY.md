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

## Not yet in Jotunn

- **Interactive SAM2 segmentation (Tab) and auto-polygonize.** `SegmentationViewportSession` uses Viking's `SectionViewerControl` for:
  - camera, downsample, size and `ScreenToWorld`
  - current channel and transform
  - `RenderSceneToTexture`

  A host interface implemented by `SectionSceneHost` is the way in. The command itself (about 1,700 lines, WinForms input) would then need a tool.
- **Pen mode**: strokes that create, cut holes in, or retrace shapes.
- **Remaining location menu items:**
  - convert shape and simplify polygon
  - export (Tulip/morphology)
  - SBFSEM tools
  - unverified branch terminals
  - random color

  Several of these call `AnnotationOverlay.CurrentOverlay.Parent` and need host hooks like `GoToLocationHandler`.
- **Change location type hotkeys** (`ChangeLocationAnnotationTypeAction`): reported as not supported.
- **Context menus inside the property sheet's lists**: these come from Viking's `ExtensionManager` context-menu providers, which Jotunn does not register. The menu shows empty.
