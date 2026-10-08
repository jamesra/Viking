# Viking changelog

Committed application version: **1.2.77.0**  
(1.2.70 and 1.2.75 were intermediate bumps and have no section of their own.)

Older 1.1.x notes live in [Documentation/source/Client/versionhistory.rst](Documentation/source/Client/versionhistory.rst).

Some Velopack builds skipped patch numbers in a single bump (for example 1.2.48 → 1.2.60). Entries below are keyed to the version recorded in `Clients/Viking/Viking/Viking.csproj` when that work shipped.

## 1.2.78 — unreleased

* Measure tool and scale-bar labels pick the unit that puts the number between 1 and 999: 303 nm no longer shows as 0.303 um, and 0.75 um shows as 750 nm.
* The measure tool shows 0 when the drag starts on the clicked point; it showed 1 (for example 1.000 nm).
* Polygons with three or more vertices on one exactly vertical or horizontal line, plus a vertex a fraction of a pixel off that line, now triangulate. Before, they could fail to draw filled, and segmentation fell back to a single centroid prompt for them.
* Viking keeps tile transforms for the six most recently viewed sections and unloads the rest once a minute. Before, every section visited stayed in memory until Viking closed, so memory grew during long browsing sessions. Returning to an unloaded section reloads its transforms from the local cache.

## 1.2.77 — 2026-09-30

* Auto-polygonize proposal rings no longer block translate/scale on the circle underneath. Hover and single-click prefer the annotation; double-click still accepts or dismisses the ring.
* Status-bar chips show **Pen On/Off**, **AutoPoly On/Off/Busy**, and **Seg Ready/Off/Busy**. Click a chip to toggle the mode or open the segmentation service picker.
* Viewer toolbar toggles for **Pen Mode** and **Auto Polygonize** (same actions as the Annotation menu; check state stays in sync with the menu and status chips).
* Interactive Segment skips are no longer silent: zoom-too-coarse, upload/tile failure, empty mask, and service errors show a short status-bar message (no modal dialog).
* Auto-polygonize proposal rings: double-right-click opens an **Accept / Reject** context menu (sibling absorb on accept stays automatic).

## 1.2.76 — 2026-09-29

*(csproj jumped 1.2.68 → 1.2.70 → 1.2.74 → 1.2.76. These notes cover that span. 1.2.74 was the previous committed build.)*

* F1 help follows the idle catalog, the annotation under the mouse or pen, and the active command. Pen choice buttons list their captions. Circle, polygon, and freehand lines match the pen and mouse actions, and the bar notes that **Auto polygonize circles in view** turns idle segmentation off.
* **Resegment** preview outline is 8 screen pixels wide, four times the previous 2-pixel stroke.
* Segmentation uploads **1024×1024 mosaic tiles** (`UploadTile` / `SegmentTilesStream`) for the cells under the pen prompts, then any cells the server asks for as the mask grows into them. A tile the server reports missing is uploaded again. The default tile pyramid is full resolution (`SegmentationTileDownsample` = 1), so zoom no longer steps the upload up to DS2. Upload and segment are skipped when the camera is coarser than **Max Auto-Segment Downsample**. The server keeps the request and continues the same growth when the tiles arrive, so the client no longer repeats the whole segmentation per round. The debug mask overlay is drawn on the mosaic tile bounds, in line with the prompts. A jump sets the camera before tiles preload.
* A closed pen loop that surrounds a circle offers **Convert {location id} to polygon**, including a circle the stroke never entered. The loop has to intersect the circle or contain it.
* **Tab** starts segmentation to place a new structure. Clicks add prompts; click inside the mask to save. The type is the one selected in the structure list, or Cell when none is selected. A type that needs a parent still asks for one before the save.
* Pen choice buttons sit in a column on the right. Each caption sits under its circle, wraps to the button width, and draws above the stroke preview. The column is spaced so the next button does not cover the caption. The Annotation menu can hide the buttons (**Show Pen Action Buttons**); the shape previews stay clickable. In Pen Mode, a structure hotkey places the annotation instead of starting a one-point stroke that was thrown away.
* Retrace cut: **Ctrl** chooses which piece stays on the original location. **Shift** keeps both pieces. The other piece is saved as a second, unlinked annotation on the same structure and section. Help text lists both modifiers.
* A polygon cut preview reuses a triangulation built when the cell is shown, so drawing the cut does not refit the whole outline. Vertices away from the cut stay at their saved positions.
* A saved cut drops the extra samples from the smooth display outline. Points that remain are original vertices.
* Saving a polygon that SQL Server would reject keeps the largest valid piece instead of an unhandled "Geometry invalid" error.
* Continue-last-trace still works after the last location has left the local cache, including across a missing or bad section. The new disc is mapped with the open section only. A click on the section where that location was just placed does not duplicate it.
* `--version` / `/version` prints the entry-assembly version to the parent console before the UI starts. The startup log line includes the same version.
* A second launch or tools-page deep link is rejected while Viking is closing. A deep link to the running instance restores a minimized window and brings it forward, and leaves a maximized window maximized.
* **Goto Location** says when the annotation service denied access. That used to share the missing-ID text.
* Annotation Preferences: **Max Auto-Segment Downsample** (auto-segment does not run when the view is zoomed out past this value). Leave this control alone; it will later be set from the segmentation model you choose. The SAM2 mask overlay is a saved preference, with **Hide segmentation rings** and prompt overlay next to it. Proposal rings use the structure hue. The circle dims while its proposal ring is under the cursor.
* Annotation menu **Update Volume Positions…** (review access only) corrects stored volume positions for a typed section list or range. Progress shows on the viewer status bar.
* Moving the window between an HDR monitor and an SDR monitor recreates the swap chain so the view does not stay washed out.
* A failed location or structure link save shows the inner database or fault message.
* An unreadable shortcuts file is replaced from the server. A failed download leaves the settings already in memory.
* The Python SegmentationClient sample is no longer in this tree. The point-prompt example lives in VikingLegacy at `Servers/SegmentationServer/Example`.
* Stability: evicted tile-cache entries are disposed; SQL spatial types stay on version 16; a stos zip entry that would write outside the cache folder is rejected; in-flight region queries and a failed save no longer leave the canvas empty or the edit half-applied; nested R-tree locks no longer deadlock. Identity discovery failures include the server error text.

## 1.2.68 — 2026-09-22

*(csproj jumped 1.2.65 → 1.2.68 in one bump.)*

* Adopt **Geometry.Core** vectors across Viking so callers share `Vector2` without XNA `Grid*` types.
* Open HTTPS segmentation channels with TLS.
* Decimate unsmoothed polygon prompts before they go to SAM2.
* Empty-mask SAM2 hammering reduced: one largest-triangle avoid mark per polygon, skip empty unions until `LastModified` changes, overlay last prompts in mask debug mode.

## 1.2.65 — 2026-09-20

* Auto-polygonize **prompts and sibling merge**: context-menu and auto-segment share 17-point rings; polygon avoid marks use unsmoothed `MosaicShape`; drop the 40% huge-mask skip.
* **Pen Mode** still emulates a pen unless a hotkey started placement (mouse restored except for those hotkeys).
* Closed-ribbon twists fixed: `RoundCurve` tangents unwrap and clamp.
* `gRPC_Protos` update for `omit_labeled_image`.

## 1.2.64 — 2026-09-19

*(csproj jumped 1.2.62 → 1.2.64 in one bump.)*

* Fix **`viking://` location jump** and instance reuse.
* Port geometry readonly-struct optimizations; continue auto-polygonize work.
* Many stability / correctness fixes from that day’s batch, including:
  * linked polygon create no longer throws `MissingMethodException` (bind queued `SegmentationCommand` constructors by assignability)
  * CLI login prefill; measure-help matches Shift/Ctrl draw
  * file-URI volumes marked local; tile names use `GridCoordFormat` instead of hardcoded `D3`
  * pen barrel-button events from `PenFlags.Barrel`
  * resize-circle / polyline `OnMouseUp` call `base` so XButtons cannot start a new command
  * cut-hole vs remove-hole action IDs no longer conflated
  * adjacent/overlapped views gray when `Parent` is null
  * location-link async begin/end pairing fixed; empty overlap set not immediately rebuilt
  * HTTP tile and stos URLs use forward slashes (Windows separators could 404)
  * link tool no longer sticks on invalid hover until Escape
  * **Hide Bookmarks** honored in overlay draw and hit-test
  * null-guard failed region annotation parses; drop disposed unit-circle GPU buffers on device reset
  * mosaic tile Y spacing from `TileSizeY` for non-square tiles
  * region refresh uses `TotalSeconds` so parked views update after three minutes
  * `CommandQueue.Push` rebuild; terminal toggles sync-save with rollback on `FaultException`
  * settings `Upgrade()` once per version; extra bookmark XML loaded as live documents

## 1.2.62 — 2026-09-16

*(csproj jumped 1.2.60 → 1.2.62 in one bump.)*

* Optional **Auto polygonize circles** mode (Annotation Preferences and Annotation menu, off by default). After the view idles and FOV annotations have loaded, each circle inset 20% from the edges is sent to the segmentation service with all other annotations as background. Hollow result polylines stay on screen; double-click accepts, double-right-click dismisses. Encode/polygonize run off the UI thread; traces Douglas-Peucker simplified.
* Annotation Preferences: **auto-polygonize min size** (default 1% of screen area) with a circle preview; the same slider is used for **Smallest Rendered Size**, **Segmentation Point Radius**, and **Polygon Point Radius**. Pixel-size maxima follow the property-page width instead of the old 10/15/50 px caps.
* Closing the main window no longer deadlocks on WPF `Application.Shutdown` (Viking exits after close).
* Report DXGI `GetDeviceRemovedReason` when the GPU device is lost.
* Single-instance / deep-link plumbing expanded (`VikingDeepLinkActivation`, `VikingSingleInstance`).

## 1.2.61 — 2026-09-11

* Each VikingXML **StosGroup** zip extracts into `StosZip/{groupName}/` instead of one shared folder. SliceToVolume1 and SliceToVolumeLinear1 reuse the same stos filenames; the shared cache let Linear overwrite SliceToVolume1 (RC2 section **962** looked crushed while 961 looked fine).
* `<StosGroup zip="…">` members are loaded from that group’s zip. Previously only a volume-level `StosZip` attribute was fetched.
* Annotation **shapes** (circles, polygons, holes) draw again when labels are visible. A cached `BlendState` kept `ColorWriteChannels` at `None` after the Z-only background pass.
* A `viking://` link for the **already-open volume** is handed to the running instance instead of starting a second Viking. Unknown location IDs show which volume was searched.
* Volume load failures on the splash screen cancel cleanly so the error dialog can report the URL and reason.
* Annotation region loads: locations already in the store appear immediately; a cancelled or failed query no longer stamps the region as up to date while the canvas stays empty.
* Location links whose endpoints cannot map into volume space are skipped instead of throwing.

## 1.2.60 — 2026-09-11

*(csproj jumped 1.2.48 → 1.2.60 in one bump; includes 1.2.59-era work plus deep links.)*

* Bidirectional **SBFSEM-tools** deep links: `viking://open` with a one-use Identity launch code (volume-scoped token, auto-open volume, go to location); **Open in SBFSEM-tools** bounces through Identity so the browser can SSO without putting tokens in the URL.
* Tile HTTP **429** responses honor `Retry-After` instead of tight-looping the image server.

## 1.2.59 — 2026-08-19

* **Go to Structure** opens with its top-right corner on the section viewer’s top-right. **Go to Location** opens on the viewer’s bottom-right. Both stay above Viking but are no longer always-on-top, so other applications can cover them.
* New leftmost **Edit** menu: **Viewer Preferences** and **Annotation Preferences** (moved off Commands / Annotation).
* **Max Concurrent Texture Requests** defaults to **0 (Auto)**. Auto is `(4096 / tile width) × 2`: 32 threads at 256×256, 16 at 512×512, 8 at 1024×1024. Values 1–256 override Auto.
* Stos transforms that contain `nan` are skipped instead of crashing Viking.
* Texture loads prefer the local disk cache before hitting the network. The viewer pauses painting while the window is minimized.
* Volume load failures show the volume URL and a short reason instead of a raw exception dump.

## 1.2.48 — 2026-02-07

* Packaging / version bump for Velopack.
* Adjacent-section texture preload honored correctly; adjacent section number calculation fixed.
* Additional performance tuning options; dead link references removed.

## 1.2.47 — 2026-02-06

* Texture **request queue**: sort by visibility, Z, and downsample; auto-load textures for adjacent sections.
* Viewer preferences knobs for texture loading behavior.
* Overall section-load performance improved.

## 1.2.46 — 2026-02-06

* Section-load task optimizations; stop using `Trace` in Release builds to avoid a lock contention.
* Refresh clock adjusts based on whether textures remain to load.
* All textures go through a queue so UI-thread texture creation can be messaged safely.
* Automatically mark existing annotations as background when segmenting.
* Shift key mass deletion; anonymous user support.

## 1.2.45 — 2026-02-05

* Broader use of `ConfigureAwait` on async paths (version bump companion).

## 1.2.44 — 2026-02-03

* **Open Structure** dialog sized correctly.

## 1.2.43 — 2026-02-03

*(csproj jumped 1.2.39 → 1.2.43.)*

* **Section number overlay** view: working overlay, preferences/converter wiring, race fixes, mouse-cursor tweak; PID parameters for overlay animation.

## 1.2.39 — 2026-01-26

*(csproj jumped 1.2.28 → 1.2.39.)*

* Line-ending normalization for git.
* Opacity honored for newly loaded annotations.
* Keys no longer flicker non-default command cursors.
* Foreground/background segmentation points never null; cursor hints for those points.
* Ignore Velopack `releases` folder in source control.

## 1.2.28 — 2026-01-22

*(csproj jumped 1.2.2 → 1.2.28.)*

* IDE / Intellisense and compiler cleanups.
* Async method suggestion follow-ups; dead code removal.

## 1.2.2 — 2026-01-20

* **Velopack** deployment scripts and initial installer support (replacing earlier Squirrel packaging path).
* Auto-update path working.
* Projects target latest C# language version; Morphology View moved to SDK-style project.

## 1.2.0 — 2025-10 through 2026-01

First 1.2 line on .NET Framework 4.8 / 64-bit (XNA removed). Highlights from that arc before Velopack:

* Identity **token** login and related WPF login / volume / segmentation-service UI.
* Segmentation commands (SAM2), opacity sliders, smallest renderable size; segmentation server chosen in the client (not sticky from VikingXML).
* Context menus restored / migrated (`ContextMenuStrip`); bookmarks and measurement extensions copy into Modules on build.
* High-DPI property pages; `Viking.UI.WPF` rename from older WPF control library.
* Linked-location polygon medial axis in the correct coordinate space; volume name saved in recent volumes; cancellation of blank segmentation screen captures.
* Random annotation color menu; polygon control-point view refactor.

See git history on `Legacy` for commit-level detail.

## 1.1.x (through 1.1.280, 2019-04-23)

See [versionhistory.rst](Documentation/source/Client/versionhistory.rst). Last 1.1 release notes: Find Volumes URL fix (1.1.280).
