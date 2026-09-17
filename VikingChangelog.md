# Viking changelog

Current version: **1.2.61.0**

Older 1.1.x notes live in [Documentation/source/Client/versionhistory.rst](Documentation/source/Client/versionhistory.rst).

### 1.2.62 — 2026-09-16

* Optional **Auto polygonize circles** mode (Annotation Preferences and Annotation menu, off by default). After the view idles and FOV annotations have loaded, each circle inset 20% from the edges is sent to the segmentation service with all other annotations as background. Hollow result polylines stay on screen; double-click accepts, double-right-click dismisses.
* Annotation Preferences: **auto-polygonize min size** (default 1% of screen area) with a circle preview; the same slider is used for **Smallest Rendered Size**, **Segmentation Point Radius**, and **Polygon Point Radius**. Pixel-size maxima follow the property-page width instead of the old 10/15/50 px caps.

### 1.2.61 — 2026-09-11

* Each VikingXML **StosGroup** zip extracts into `StosZip/{groupName}/` instead of one shared folder. SliceToVolume1 and SliceToVolumeLinear1 reuse the same stos filenames; the shared cache let Linear overwrite SliceToVolume1 (RC2 section **962** looked crushed while 961 looked fine).
* `<StosGroup zip="…">` members are loaded from that group’s zip. Previously only a volume-level `StosZip` attribute was fetched.
* Annotation **shapes** (circles, polygons, holes) draw again when labels are visible. A cached `BlendState` kept `ColorWriteChannels` at `None` after the Z-only background pass.
* A `viking://` link for the **already-open volume** is handed to the running instance instead of starting a second Viking. Unknown location IDs show which volume was searched.
* Volume load failures on the splash screen cancel cleanly so the error dialog can report the URL and reason.
* Annotation region loads: locations already in the store appear immediately; a cancelled or failed query no longer stamps the region as up to date while the canvas stays empty.
* Location links whose endpoints cannot map into volume space are skipped instead of throwing.

### 1.2.60 — 2026-09-11

* Bidirectional **SBFSEM-tools** deep links: `viking://open` with a one-use Identity launch code (volume-scoped token, auto-open volume, go to location); **Open in SBFSEM-tools** bounces through Identity so the browser can SSO without putting tokens in the URL.
* Tile HTTP **429** responses honor `Retry-After` instead of tight-looping the image server.

### 1.2.59 — 2026-08-19

* **Go to Structure** opens with its top-right corner on the section viewer’s top-right. **Go to Location** opens on the viewer’s bottom-right. Both stay above Viking but are no longer always-on-top, so other applications can cover them.
* New leftmost **Edit** menu: **Viewer Preferences** and **Annotation Preferences** (moved off Commands / Annotation).
* **Max Concurrent Texture Requests** defaults to **0 (Auto)**. Auto is `(4096 / tile width) × 2`: 32 threads at 256×256, 16 at 512×512, 8 at 1024×1024. Values 1–256 override Auto.
* Stos transforms that contain `nan` are skipped instead of crashing Viking.
* Texture loads prefer the local disk cache before hitting the network. The viewer pauses painting while the window is minimized.
* Volume load failures show the volume URL and a short reason instead of a raw exception dump.

### 1.2.48 — 2026-02-07

* Version bump for Velopack / ClickOnce packaging.

### 1.2.0 — 1.2.47

* 64-bit Viking on .NET Framework 4.8 (XNA removed).
* Identity token login, segmentation commands, and Velopack installers.
* Texture request queue with visibility / Z / downsample priority and adjacent-section prefetch.
* Viewer preferences for texture loading and the section-number overlay.

See git history on `Legacy` for commit-level detail.

### 1.1.x (through 1.1.280, 2019-04-23)

See [versionhistory.rst](Documentation/source/Client/versionhistory.rst). Last 1.1 release notes: Find Volumes URL fix (1.1.280).
