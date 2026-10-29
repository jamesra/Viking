# Viking changelog

<!--
Notes for whoever updates this file. Comments like this one are not shown in Viking.

- Viking installs this file beside Viking.exe and shows it from Help > Change Log, and once the first time each version starts.
- One "## <version> — <date, or 'not yet released'>" heading per version, newest first, each followed by a bulleted list of
  changes written for users: what they can see or do differently, new or changed hotkeys, menus and preferences. No
  developer detail; put that in a comment.
- Add a bullet in the same commit as each user-visible change. When the version in Clients/Viking/Viking/Viking.csproj
  changes, rename the unreleased heading to the new version; when a version ships, replace "not yet released" with the date
  and channel, for example "2026-10-26 (Viking Test, unsigned)".

Committed application version: 1.2.135.0.
1.2.70 and 1.2.75 were intermediate bumps and have no section of their own. Neither do the builds between 1.2.78 and 1.2.109;
their changes are listed under 1.2.109. Builds 1.2.128 and 1.2.129 are listed under 1.2.130 (1.2.129 went out as Viking Test
on 2026-10-27). The pen and link fixes of builds 1.2.123 to 1.2.126 are listed under 1.2.127.

Some Velopack builds skipped patch numbers in a single bump (for example 1.2.48 to 1.2.60). Entries below are keyed to the
version recorded in Clients/Viking/Viking/Viking.csproj when that work shipped. Older 1.1.x notes live in
Documentation/source/Client/versionhistory.rst.
-->

## 1.2.135 — not yet released

* Fix: an opacity changed in Annotation Preferences could jump back to its old value after **OK** on the annotation that was under the mouse when the dialog opened, once the mouse moved off it.
* Fix: after **Export frames** or **Export Tiles** (Section menu), the viewer stayed on the last section exported instead of the section you were viewing. It now goes back to that section when the export finishes or is canceled.
<!-- RenderSceneToTexture switches this.Section per tile and never restored it. The frame and tile exports now restore the viewed section once, through TaskChaining.RestoreAfterAsync, after every tile has drained (ExportTiles now waits for each section's tiles before the next). -->
* Fix: **Export Tiles** (Section menu) skipped a tile it could not render or save (for example a full disk or a locked file) without a word, and still wrote that section's index file as if the section were complete. It now stops, as **Export frames** does: the tiles already being written finish, the progress window closes, the viewer goes back to the section you were viewing, and a message says why. The section that failed gets no index file; sections finished before it keep theirs. Run the export again to finish it: tiles already on disk are not redone.
<!-- ExportTiles' throttle and per-section drain used Task.WhenAny + Remove, which dropped faulted tiles; both now use TaskChaining.DrainToAsync. The progress form moved to the UI thread (it was created on a thread-pool thread and never closed when the export threw); menuExportTiles_Click awaits the export and shows the error like menuExportFrames_Click. -->


## 1.2.134 — 2026-10-28 (Viking Test, unsigned)

* Fix: a pen stroke that cuts across a polygon near one of its holes could crash Viking ("Duplicate point found in exterior ring") when the cut left a hole touching itself at one point, or when the polygon already had such a hole. That piece is no longer offered as a choice (the other pieces still are), and a polygon with such a hole gets no cut-out choice instead of a crash.
* Fix: a moved annotation could stay drawn at its old place, and snap back there after every later move, although the move was saved (the next drag started from where it was dropped). Moved and reshaped annotations now always redraw where they are.
<!-- SectionAnnotationsView keyed views, "known" IDs and subscriptions by location ID. When the store held a new LocationObj for an ID (section evicted and reloaded while a region load still held the old object), the view kept the old object and the new one was never subscribed. LocationViewIndex now builds each view from the store's current object and subscribes per object; a geometry change rebuilds whatever view is present; a failed add leaves nothing half registered. -->

## 1.2.133 — 2026-10-28 (Viking Test, unsigned)

* While you draw a stroke with the pen, and while its choice buttons are shown, location links are hidden: the up and down link markers inside locations, the link lines between sections and their labels. They no longer cover what you are tracing, and a stroke across a marker is treated as a stroke across the location under it. Links come back when the stroke ends or a choice is made. They stay visible while the pen is idle and while you make a link (**Shift** + pen on a polygon away from its centre, then drag to the other annotation). A stroke from an annotation onto a location on the adjacent section still offers to link the two.
* Pen strokes that reshape a hole: when a stroke runs from a polygon across one of its holes and back, splitting the hole in two, click the part of the hole you want filled and it becomes part of the polygon; the other part stays a hole. Clicking the polygon itself fills the smaller part. Each part is shaded on its own, and the buttons read **Fill smaller part of hole** and **Fill larger part of hole**. Strokes across the outer boundary work as before: the piece you click is the one kept.
* Fix: a pen stroke on a polygon with a hole close to its outline could crash Viking ("Replacement inner polygon is not a valid addition") while showing the choice buttons. Where the smoothed hole would cross the smoothed outline, the hole is now drawn unsmoothed.
<!-- RetraceAndReplacePathCommand (pen press inside a hole) follows the same rule if it ever splits a hole: the smaller part is filled, Ctrl fills the larger, the part to fill is green. In pen mode it starts inside a hole, so it grows holes and this case is not normally reached. -->

## 1.2.132 — 2026-10-28 (Viking Test, unsigned)

* Fix: dragging a polygon vertex could move a different vertex, and **Ctrl**-clicking a vertex to remove it could remove a different one. Near the polygon's first vertex, Viking picked that vertex instead of the one under the cursor; it now always picks the nearest vertex, including vertices of holes.
* Fix: with **Default Line Width** in **Annotation Preferences** set below 2, **Ctrl**-clicking a polygon vertex toward the outside of the polygon did nothing instead of removing the vertex.
* Fix: holding **Alt** as well as **Shift** or **Ctrl** no longer cancels the Shift or Ctrl click and pen actions on annotations (translate, link, add or remove control points, cut or remove holes), or Shift's slower mouse-wheel resize. Holding **Ctrl** and **Shift** together now counts as holding both, instead of as holding neither: where Shift and Ctrl have different actions, Shift's is used, and a click that needs no modifier (such as creating a linked annotation) is not made.

## 1.2.131 — 2026-10-28 (Viking Test, unsigned)

* Change Log: **Page Up** and **Page Down** jump to the previous or next version, with its heading at the top of the window.
* Fix: **Select Segmentation Service** now checks each server at the same address and port that segmentation then connects to. A server entered without a port, or with a scheme other than `http://` or `https://`, could be shown as available when segmentation could not use it. Segmentation also connects to a server entered as an IPv6 address, which failed before.
* Fix: a pen stroke that grew or shrank a hole in a polygon so that the hole would cross the outline or another hole crashed Viking with "Replacement inner polygon is not a valid addition". That choice is now left out, and the stroke is treated as cutting out or adding the region it encloses, which can open the hole to the outside.

## 1.2.130 — 2026-10-27 (Viking Test, unsigned)

* Auto segmentation no longer stops returning proposed polygons while the segmentation server is busy. Viking checked its connection to the server every 30 seconds, more often than the server allows while it is quiet, so a server busy for a couple of minutes closed the connection ("Too many pings") and every request on it failed. Viking now checks every 6 minutes, and only while a request is in progress; an idle connection is no longer checked at all, so the server never closes it for that, and the next request reconnects if anything else dropped it.
* Location links (the hourglass between two linked locations on different sections) are easier to see next to the locations they join and stay out of the way in between. Each end is drawn strongly, with a contrasting shadow around it (black around a light link, white around a dark one), and fades to nothing toward the middle; the invisible middle cannot be clicked. Move the mouse over a link and the whole link fades in fully over a third of a second, with its labels, and can be followed all the way along; it fades back when the mouse leaves. The thin border added in 1.2.120 now really is black or white; before, it took on the brightness of the image under it and barely showed.
* **Link visibility (%)** in Annotation Preferences sets how strongly links and their shadows are drawn when the mouse is not over them, from 10% to 100% (default 60%).
* While the mouse is over a location link, each end is labelled, just beyond the location, with the ID of the location at the other end. At the end on the section you are viewing, an up arrow on the line above the ID means the partner is on a higher section, and a down arrow on the line below means a lower one; the end on the adjacent section shows the ID alone. Labels are sized from the location's radius, like location labels, so they zoom with the image. They are left out where they would be too small to read or the link is too short for both, and can be turned off with **Label link ends with the linked location ID** in Annotation Preferences.
* Location links attached to polygons now leave the polygon's outline, where the link heads for its partner, and are no wider than the cell is thick there. Before, a link treated a polygon as a circle around its centre, so on a long cell it started buried under the fill and on a thin one it started in empty space with a gap. A link to a polygon on the adjacent section now ends on the circle marker drawn for it.
* Location links attached to lines and open curves now leave the line's edge at the point nearest the linked location, as wide as the link's other end, instead of a circle half the line's length around its centre. A link to an open curve on the adjacent section ends on the circle marker drawn for it.
* Right-click a location link for **Go to partner**, which moves the view to the end of the link farther from the mouse, staying on the section you are viewing and keeping the zoom.
* A link to a location on the adjacent section that is hidden under a location on this section (shown only as the small up/down symbol inside it) now ends on that covering location, instead of in empty space where the hidden location is. When such a location becomes hidden or visible again (an annotation is drawn, moved or deleted, or a link is added or removed), its other links re-point at once, without changing section.
* A location link between two locations close together, which used to be drawn as a large solid block, is now easier to see through: the closer the locations, the more transparent it is, down to half its usual opacity where they touch. Links long enough to fade out in the middle look as before.
* Location link colours are toned down: the red, orange and blue keep their hue (a burnt orange stays burnt orange) but are a little less vivid, the section numbers on overlapped-link markers use exactly the same colour as a link going that way, and the shadow, border and opacity are unchanged, so links are as easy to see.
* Del now hides the location link under the mouse, as it hides annotations, and Insert brings it back. A link also hides while either of its locations is hidden.
* More preferences change the view as you adjust them, before Apply or OK; Cancel puts them back. In Annotation Preferences: link visibility, link waist width, drawing links under markers, link end labels, the label outline, polygon point size, opacity and hiding in pen mode, the smallest rendered size, the auto-segment mask, prompt and ring overlays, the shimmer settings, and the section correction settings. In Viewer Preferences: the image loading window, min images loaded per window, how often loading priorities update, and loading textures for adjacent sections. The maximum number of concurrent texture requests still waits for Apply.
* The arrow markers inside a circle, polygon or closed curve (they stand for a link to an overlapping location on an adjacent section) can now be handled like link lines: Del hides the marker under the mouse (before, it hid the whole location), Insert brings it back, and right-clicking it offers **Delete Link** (before, the menu was empty). A hidden marker is not clicked by mistake.
* Del and Insert also work on locations shown from an adjacent section and on structure links, not only on locations and location links of the section you are viewing. Hidden links and polylines fade back in like other annotations, and Insert also finds a hidden link marker that lies outside its polygon's outline.
* Annotation Preferences has a new **Visuals** section under Basic Settings. It gathers the settings that only change how the view looks (text sizes and outline, annotation opacities, adjacent-section size, smallest rendered size, curve display points, control point size and opacity, segmentation prompt point radius, link drawing, and the auto-segmentation shimmer), grouped under Text, Annotations, Control points, Location links and Auto-segmentation. Settings that change behavior stay where they were; no setting has changed meaning.
* Pen: touching the section number strip at the left edge while a pen command is drawing (the pen was only lifted between strokes) now goes to the command instead of jumping to another section in the middle of the outline. Tapping the strip when no pen command is drawing still changes section.
* Pen cut: drawing a stroke across a location that has a link marker inside it no longer stops at the marker. The stroke over the marker used to count as leaving the location, so the cut or add was not offered (the log said the path crossed the outline once).
* Pen outlines keep their points. Cutting or extending a polygon, closed curve or hole with the pen used to refit the whole outline each time, so membranes bulged outward and parts you had not touched drifted a little with every edit. Now only the stretch you drew is spliced in, and the control points elsewhere stay exactly where they were. The pen's fitting tolerance is capped at 2 volume pixels (it was 3 times the downsample). A stroke that crosses the outline more than twice, or crosses into a hole, cuts out the region it encloses, or adds it when the stroke ends inside, for example to cut a bridge out of a cell; each resulting piece is offered as a choice. Reshaping keeps the polygon's holes, and a hole you replace is now saved in the right coordinates.
* **Help > Change Log** shows this list of what changed in each version of Viking. It also opens by itself the first time a new version starts.
* Section corrections: near the edge of an annotated area, where the correction lattice has only some of its points, the image is now shifted by the same amount the correction service uses for annotations there. Before, Viking worked out the shift its own way and could disagree with the service by up to a lattice spacing.
* Paint segmentation: **Ctrl+Z** undoes the last completed brush stroke, one stroke at a time.
* The section numbers in the strip at the edge of the view are drawn as white digits with a soft black outline, so they stay readable over light and dark images, whatever the label outline preference.
* Auto-segment sends a circle to the segmentation server as its shape alone, without the four foreground clicks near its rim. On a circle drawn larger than the cell those clicks could land outside it and pull in the background.

## 1.2.127 — 2026-10-26 (Viking Test, unsigned)

* Circles: when you draw, resize, move or link a circle, the circle on the other side of the section-to-volume warp now gets the radius the warp's local scale implies, instead of a copy of the radius. Auto-segment of a circle also tolerates a rim point that does not map.
* Segmentation uses the segmentation server's own tile caches before asking Viking to render tiles, so repeat segmentations over the same area start faster. Views with a blended or colored channel set, or with colorize or mesh overlays on, no longer share tiles with a plain grayscale view.
* Pen: tapping the animated section number strip at the left edge now shows the tapped section instead of starting a stroke of the current pen command. A tapped number the volume does not have goes to the next existing section in that direction.
* Pen input no longer throws an OverflowException when Windows reports a mouse message that came from the pen.
* A location on an adjacent section whose marker could not be built (for example while its mapping was not ready) is tried again on the next load; before, it was remembered as shown and its marker never appeared on that section.
* Pen cut: a hole that touches the smoothed outline no longer makes the cut fail with an error; the hole keeps its smoothed outline.
* Location links no longer log an error each time a linked location leaves memory in the middle of an update, and changing section no longer fills the log with "does not contain expected location" lines.

## 1.2.122 — not yet released

* Segment to Polygon on a circle now opens the paintbrush segmentation with the circle painted as the starting mask (it started empty in 1.2.121).
* Edit mask (grow or shrink a polygon with the wheel) now sends the edited mask to the paintbrush segmentation, so you can paint on it before accepting, instead of the older point placing screen.

## 1.2.121 — 2026-10-23 (Viking Test, unsigned)

* Segment to Polygon, Resegment and linked polygon placement now open the paintbrush segmentation (the polygon is painted as the starting mask) instead of point placing segmentation. The proposal menu's Add points entry is gone; use Paint mask.
* Annotation Preferences: "Max Auto-Segment Downsample" can now be set up to 6 (it stopped at 4), for the main columns of large cells such as Muller cells.
* Auto-segmented polygons keep every green point you placed: simplifying the outline or cutting it away from neighboring annotations no longer drops one. Where a green point lies inside a neighbor, the new polygon overlaps that neighbor instead.

## 1.2.120 — 2026-10-22 (Viking Test, unsigned)

* Location links (the hourglass between two markers) now have a thin border, black on a light link and white on a dark one, so they stand out from both light and dark images.
* A structure's **Locations** tab no longer holds Viking up while it loads. The locations are fetched in the background and the list fills in when the server answers, with a wait cursor in between, and opening another structure's properties starts a fresh list instead of keeping the previous structure's. A failed load is tried again the next time the tab is shown.
* A login that cannot reach the identity server (server down, firewall, no network) now shows the friendly "did not respond in time, check your network" message instead of the raw "An error occurred while sending the request."
* Tab (new structure) now segments by painting instead of placing points: drag the left button to paint the object (green), Shift+drag to paint what is not the object (red), Shift+drag starting on paint erases the nearest kind of paint. A moment after you release, the painted masks are sent to the segmentation server and the result is shown; double-click the result or press Enter to accept, Escape to cancel. The sideways wheel or Shift+wheel changes the brush size.
* Pen: the same painting with the pen's barrel button in place of Shift; the on-screen check button accepts and the red X cancels.
* Annotation Preferences has a new "Barrel button" setting: press "Click your pen button..." and then the button on your pen, so every command that uses the barrel button uses yours.
* The menu of an auto-segment proposal (double right-click the ring) has a new "Paint mask" item next to "Add points": it opens the mask painter with the proposed polygon already painted as the object and segmented; paint, erase or add background to adjust it, then double-click the result or press Enter to accept. Escape brings the proposal back.

## 1.2.119 — 2026-10-22 (Viking Test, unsigned)

* A structure hotkey in `UserSettings.xml` whose `Tags` list has a blank entry (empty `Tags=""`, a trailing `;`, or `;;`) no longer adds a nameless tag to the new structure, and spaces around a tag are ignored. A tag listed twice no longer switches itself off again.
* After stepping to a new section, annotations that had not loaded or been drawn no longer wait for a pan or zoom: Viking checks about 0.25 s after the step and asks for them again. Each check is also written to `%TEMP%\viking-section-load-check.log` (section, zoom, step direction and run length, what it found) to show which case occurs.
* Marking an annotation Terminal now plays a short confetti burst from the annotation, and marking it Off Edge shows a frown face that slides down and fades (about 1.5 to 2 seconds). Drawn on the screen only, with no extra server or tile requests; clearing either flag shows nothing.
* A .stos transform that the server fails with a temporary error (408, 429, 500, 502, 503, 504) is now retried instead of skipped. If any section still has no transform after the volume loads, Viking shows a "Transforms Missing" warning with the count before the main window opens.

## 1.2.118 — 2026-10-21 (Viking Test, unsigned)

* A recent volume that was saved by its URL now shows its file name without the extension (for example `SliceToVolume`). Before, the last letter was cut off too (`SliceToVolum`), and any extension other than `.vikingxml` was left on. Entries saved earlier keep the name they were saved with.
* Ctrl-copying a curve's control points while moving or reshaping an open curve now copies from the curve nearest the mouse instead of an arbitrary one under it, and the copied shape shows right away. It no longer errors when the section has no annotations.
* Per-channel colors saved in the section reference settings (section-references.json) now come back with their transparency intact. They were reloaded with the opacity inverted (fully opaque became fully transparent), which hid or washed out the channel in the section view.
* Adding or removing a root structure type while the Structure Types form (F7) is open no longer throws an error; the form is refreshed instead.
* The Structure Types panel has an eye at the right edge of each type. Clicking it hides every annotation of that type from the section view and from mouse picking; clicking a parent type's eye also hides or shows all of its child types, and a parent whose children are partly hidden shows a half-filled eye. The hidden set is kept only on your computer for the current session; nothing is saved to the server.
* After rotating or scaling a Ctrl-copied curve, the preview now shows the rotation and scale right away instead of looking unrotated until the mouse moves.
* A password containing the `|` character is now remembered correctly by the saved-login option. Before, the stored login was cut short at the `|` and signing in with it failed.

## 1.2.117 — 2026-10-20 (Viking Test, unsigned)

* No change you can see. Internal fixes: location views with very large IDs no longer drop out of the adjacent-section list, and a structure's tag text can no longer overflow the stack when something reads it.

## 1.2.116 — 2026-10-20 (Viking Test, unsigned)

* `Del` and `Insert` now also hide and bring back the auto-segmentation ring under the mouse (the ring is checked first, then the annotation). A hidden ring stays hidden for the Hide annotation time, then fades back in before it can be clicked again.
* Closing Viking no longer waits on segmentation or section-correction requests that are still running; they are cancelled when the window closes. If closing still stalls for 3 seconds, Viking writes a dump of the stuck process before it exits.

## 1.2.115 — 2026-10-20 (Viking Test, unsigned)

* Annotation text (structure ID, tags, parent ID) is drawn with a thin white outline so it stays readable over both light and dark images. In Annotation Preferences, **Outline annotation text** turns it on or off, and **Outline colors** switches between black text with a white outline (the default) and white text with a black outline. Text in another color, such as the orange of an unverified terminal, keeps its color.

## 1.2.114 — 2026-10-20 (Viking Test, unsigned)

* An annotation hidden with Del now fades back in over the last 5% of its hide time (1.5 s of the default 30 s) before it can be clicked again, so you can see it return. `Insert` starts the same fade immediately.

## 1.2.113 — 2026-10-20 (Viking Test, unsigned)

* Del hides the annotation on the current section under the mouse for 30 seconds, so an annotation beneath it (for example one from an adjacent section that you want to drag onto this section to create a linked location) can be picked. Insert brings the hidden annotation under the mouse back early. The time is **Hide annotation time (seconds)** in the annotation preferences.

## 1.2.112 — 2026-10-19 (Viking Test, unsigned)

* Deleting a location no longer hides the structure link lines of its structure on that section while the structure still has other locations there. Before, the lines between it and its linked structures disappeared until the section was reloaded; they now stay, drawn to the nearest remaining location. Deleting a structure's last location on the section still removes its link lines.
* Loading a section's .mosaic or a .stos transform file that ends before its reported size (for example a file cut short while it was being copied or downloaded) no longer freezes Viking. Before, the read waited forever for the missing bytes; it now uses the lines that are present.
* Mapping between the volume and a section no longer fails when a transform's points repeat a volume position. Before, the repeated section position was kept, which could break mapping in both directions near that point; repeats of both kinds are now dropped.
* New Shimmer style preference (Annotation Preferences, next to Shimmer brightness and duration) for the shimmer over new auto-segmentations. Sweep is the existing look: a soft band sweeps across a fading glow. Ripple looks like a drop of water landing in the middle of the segmentation, with three rings spreading out slowly to its edges; the first ring is the widest and each following ring is half as wide as the one before it. Random picks Sweep or Ripple each time and is the default.

## 1.2.111 — 2026-10-19 (Viking Test, unsigned)

* Closing Viewer Preferences or Annotation Preferences with the window's close box (X), Alt+F4 or Escape now works like Cancel: the live preview (section number overlay, polygon and circle opacity, segmentation sliders) goes back to the values from when the dialog opened (or the last Apply). Before, closing that way kept the previewed values in effect without OK or Apply (the overlay and opacities stayed changed on screen while reopening the dialog showed the old values), and Escape did nothing.
* Changing sections in quick succession while the computer is busy no longer risks showing the new section without its annotations until you pan or zoom. Before, a late-running update for the previous section could cancel the new section's annotation load.
* Structure Type Management (F7) no longer marks a structure type as changed when its color did not change, for example when you open the color picker's Advanced view or pick the color it already has. Before, any type with an opaque color was then shown as modified (the Close button became enabled) and the next Apply re-saved it to the server unchanged.
* Structure Type Management (F7): changes on the Permitted Links tab are now saved when you click Apply. Before, an added link was never sent to the server, and a removed link was only removed on the server if a link had been added earlier in the same Viking session, so after restarting Viking added links were missing and removed links came back.
* Structure Type Management (F7): removing a permitted link and adding it back before Apply now keeps the link. Before, Apply deleted it on the server while the editor still showed it, so it was gone after restarting Viking. Removing a link you added since the last Apply now simply cancels the add.
* Review Changes no longer lists locations as deleted after you merge two structures or split a structure. Before, every loaded location of the structures involved was turned into a "deleted" row (hidden by the default Hide deleted setting, so those changes disappeared from the list) even though the locations still exist. They now show at their last change time, which is the time of the merge or split for locations that moved to another structure.

## 1.2.110 — 2026-10-19 (Viking Test, unsigned)

* Auto-polygonize now groups two proposals of the same cell when one outline lies inside the other along its edge, including two identical outlines (two circles of one cell in the same blob that get the same mask). Before, those were left as separate proposals, so accepting them saved two stacked copies of the same polygon for one cell, and stacked saved copies were never offered for merging.
* Review Changes: clicking Apply again before the previous filter's results arrive (or right after opening the tab) now always shows the results for the last filter applied. Before, if the earlier request was slower, its results arrived last and replaced the list, so the cards matched the old filter (and an earlier failure message could replace a newer success) until Apply was clicked again.
* Review Changes: a number in the structure filter now also brings in recent changes to structures whose label contains it. Before, typing **12** loaded only changes to structure 12 from the server, so a change to a structure labelled "Cell 12" was missing from the list even though the filter allows it, and showed up only if it was edited again later.
* Viking no longer sends the server the same structure request many times over when annotations appear before their structure has loaded. Before, every location drawn for that structure asked for it two or three times, all at once, which slowed loading sections with many annotations whose structure was deleted or not yet loaded; now one request per structure is outstanding at a time.
* The F1 help list now shows the Pen Mode toggle: **Numpad \*** or **Shift+8** turns Pen Mode on and off.
* **Shift+=** and **Shift+-** (step ten sections) now stop at the last or first section when fewer than ten remain. Before, at section 295 of a volume ending at 300, Shift+= did nothing.
* **Hotkey change: auto-polygonize resubmit moved from F8 to Shift+F8.** Shift+F8 sends every circle in view to auto-polygonize again, discarding the proposals shown for this section. **F8** now runs the **Toggle Done Tag** shortcut from your user settings, which the resubmit key had been blocking. F6 (favorite structure types) and F7 (structure type management) are unchanged. The F1 help list shows Shift+F8.
* **Hotkey changes in the shared shortcuts file** (take effect once the updated `WebAnnotationUserSettings.xml` is on the server; each of these shortcuts never fired before): **Toggle In Progress Tag** F6 → **F4**; **Toggle Check Tag** F7 → **Shift+F4** (F6 and F7 keep opening favorite structure types and structure type management); **Annotate IPL-GCL Boundary** Ctrl+Z → **Ctrl+Shift+I** (Ctrl+Z stays Undo); **Toggle Cell Morphology Region Boundary Tag** Shift+R → **Ctrl+Shift+R** (Shift+R stays Toggle Ribbon Tag); **Toggle Marker Tag** Shift+M → **Ctrl+Shift+M** (Shift+M stays Toggle Marginal Triad Associated Tag).
* The Enter Location ID and Enter Structure ID boxes (Go To Location ID / Go To Structure ID) no longer crash Viking when the box holds a number too large for an ID, such as 20 typed or pasted digits; OK and Go stay disabled as for other invalid text.
* Choosing a file with Browse in the Screenshot dialog now fills the Folder and File boxes with the chosen folder and file name. Before, the File box received the folder and the Folder box kept its old value, so the capture was saved to the wrong path (or failed) and its .txt description was written to the old folder.
* Cancel in Viewer Preferences now puts the section number overlay back the way it was when the dialog opened (or at the last Apply). Before, slider changes previewed live on the viewer and stayed on screen after Cancel, and Minimum opacity for non-center sections and Center magnification were not reverted in the dialog either. Saved settings were never affected.
* Cancel in Annotation Preferences now puts polygon and circle opacity back the way it was when the dialog opened (or at the last Apply). Before, the opacity sliders previewed live on the viewer and the previewed opacity stayed on screen after Cancel. Saved settings were never affected.
* **Reset to Defaults** in Annotation Preferences now restores the shipped defaults: closed curve points 3, pen simplify threshold 3, and max auto-segment downsample 2. Before, it set curve points and simplify threshold to 4 (saved on OK or Apply) and max auto-segment downsample to 8, which the slider cannot show and which took effect as 4, so auto-segment kept running at downsample 3 and 4.
* Dragging the polygon opacity slider in Annotation Preferences quickly, or moving it and the circle opacity slider together, no longer leaves some polygons at an earlier opacity than the slider shows, and no longer risks an error from two previews cancelling each other at the same moment.
* Closing the section viewer window and opening a section again no longer leaves the old viewer in memory. Before, every close and reopen kept the closed viewer alive and still notified it of each selection change and finished texture load, so memory use grew over a long session.
* Annotation edits no longer slow down after opening and closing section viewers many times. Before, every closed viewer's annotation layer stayed alive and still processed each location or link added or removed, plus each volume transform change, so each edit did the work once per viewer ever opened and memory grew. This now also holds when the viewer is closed while it is still switching sections, or after paging quickly through sections.
* **Flip** in the Split structure dialog (right-click a location link) now swaps the boxes when one is empty. Before, with Keep empty and a Split ID entered, Flip copied the Split ID into Keep, so both boxes showed the same ID and Split stayed disabled.
* The shimmer on a new auto-polygonize proposal no longer has stripes. A white glow, stronger and wider than before, with a soft white band sweeping across it at a random angle each time, at three quarters of the speed it had before. Both fade in from nothing to full brightness halfway through and back out to nothing along a sine curve, so the shimmer is no longer bright for most of its length.
* When Merge Structures or Split Structure fails for a reason other than a server error (for example the connection drops or the request times out), the error dialog now shows the actual error message with an error icon, titled "Merge error" or "Split error". Before, the dialog body only said "Merge error" or "Split error" and the real message was squeezed into the title bar, where longer messages were cut off.

## 1.2.109 — 2026-10-18 (Viking Test, unsigned)

* Pen action buttons are easier to see and use: the choice buttons on the right edge are larger with the caption inside the button (black or white, whichever reads better), and all pen buttons are framed by the same ring as polygon vertices. A red X cancel button labeled Cancel now stays in the top right corner during a pen stroke, the choice buttons, and segmentation; tapping it cancels the command like Escape. The buttons have a thin soft border and a half-transparent fill so the image shows through, and they stay clear of the toolbar, status bar, and F1 help bar docked over the viewer.
* **Ctrl+Z** now undoes your last annotation edit, and repeated presses step back through up to 50 edits. It covers moving or reshaping a location (adjust, add or remove a vertex, change contour or type), cutting or removing a hole, and toggling Terminal or Off Edge. Creating, deleting, linking, merging, and splitting are not undoable yet. Ctrl+Z does nothing while another command (a drag or pen stroke) is active, and it takes precedence over a Ctrl+Z shortcut set in your user settings.
* Changing a very large polygon's boundary with the pen now shows a fill right away on slow computers. While the full polygon is still being meshed in the background, only the part of the polygon near the view is meshed and drawn (with flat edges along the clip border), then replaced by the normal fill. Ctrl+Z also undoes a pen boundary change. Pen steps (cut, apply, save, temporary fill) are timed into `%TEMP%iking-polygon-view-timing.log` as `pen-step` lines.
* Change Shape on a circle now offers **Convert to Polygon**, which replaces the circle with a 24-vertex polygon tracing its outline. It needs no segmentation service or Reviewer role, so any annotator with write access can use it; **Segment to Polygon...** still appears when a segmentation service is connected.
* Copy-link URLs for bookmarks now write coordinates with the invariant culture. On a PC set to a comma-decimal locale (for example German), X, Y, and DS were written as 123456,8, which broke the link.
* Geometry sent to or read from SQL as WKT (points, lines, circles, polygons) now uses the invariant culture. On a comma-decimal locale the text was written as 10,50 20,25, which SQL Server rejects or misreads.
* Export Frames no longer adds an extra 512 x 512 frame at X 0, Y 0, section 0 for the grid's empty last row, and a row with X, Y, or Z left blank is reported as unparseable instead of being exported at 0. A row pasted from a spreadsheet with Width, Height, or Down Sample left blank now uses 512, 512, and 1 instead of failing with "Could not parse row".
* Export Tiles over a range of sections now sizes each section's tile grid (and the GridDimX/GridDimY in its XML) from that section's own bounds. Before, every section used the bounds of the section open in the viewer, so larger sections lost their edge tiles. Sections with no mapping for the current channel and transform are skipped instead of stopping the export.
* Clicking on the middle of a polyline annotation now selects it. A wide line scored the centre as farther away than its edge, so a click there could pick a circle on the adjacent section, or a more distant line, instead.
* Pasting coordinates into Go To Location (Ctrl+V) keeps the minus sign of negative X or Y and a leading decimal point (.5), and a comma-separated list such as 100, 200, 34 no longer sets Z to 0.
* Two bidirectional structure links that share the same lower structure ID (for example 5 <-> 10 and 5 <-> 20) are now both drawn on a section; before, only the first one loaded was shown. The pen tool also recognizes an existing bidirectional link that was created from the other structure, instead of offering to create it again.
* The pen tool's structure-link scribble (passing three or four times between two structures) now counts passes per structure. When the other structure had two locations on the section and the scribble crossed both, the passes were split and no link was offered, or the same link was offered twice.
* A polygon whose hole has a vertex where the section's mapping has a gap is now treated as unmappable, like a polygon whose outer boundary does; before, drawing or editing it threw a NullReferenceException.
* Moving, rotating or scaling a polygon so part of it leaves the section's mapped area no longer throws a NullReferenceException; the preview stays at the last position that could be mapped, and releasing it there leaves the polygon where it was. Drawing a new structure with the pen across an unmapped area now creates nothing instead of throwing.
* Changing a location to a polygon or a polyline (Change Type) with a shape that reaches past the section's mapped area no longer throws a NullReferenceException and leaves the location unchanged; before, the location could be left marked with the new type but its old shape. If saving a change to a polygon fails, the location also gets its old type back.
* Retracing a polygon's boundary with the pen, or drawing a hole into it, where the new outline (or, with Shift, the split-off piece) reaches past the section's mapped area no longer throws a NullReferenceException; the cut is not applied and the location keeps its shape.
* Redrawing a polyline (open curve) with the pen where the new line reaches past the section's mapped area no longer throws a NullReferenceException; the polyline keeps its shape.
* Adding, removing or dragging a polygon vertex where the edited polygon reaches past the section's mapped area no longer throws a NullReferenceException; the polygon keeps its shape.
* A pen stroke over a circle from the adjacent section that reaches past this section's mapped area no longer offers a **Linked location** choice that threw a NullReferenceException when picked; no linked location is offered for that stroke.
* Clicking a line, closed curve or polygon from the adjacent section (or continuing the last trace) where its shape reaches past either section's mapped area no longer throws a NullReferenceException; no linked location is started.
* Pen and touch input no longer throws an unhandled exception on 64-bit Windows when the pointer is above or left of the primary monitor (negative coordinates).
* Section view no longer throws a KeyNotFoundException when a location is removed at the same moment its overlapped-link highlight is updated.
* Retracing a polygon's boundary with the pen no longer drops the corner just before the new outline's starting point. That vertex was removed however far it stuck out, so a cut across a shape with sharp corners could lose a corner (a rectangle cut in two could come back as a triangle).
* Choosing an Open Connectome Project server whose public-token list is valid JSON of the wrong shape (for example an error object) now shows the parser message in the volume list instead of throwing an unhandled exception.
* The Merge Structures and Split Structures dialogs no longer throw an unhandled exception when an ID is typed or pasted that is too large for a 32-bit number, and they accept IDs above 2,147,483,647; typing a Location ID that does not exist in Split Structures now shows "No Location found" instead of crashing.
* Go To Location no longer throws an unhandled exception when X, Y, Z, or downsample holds a number too large for its type (such as a pasted 20-digit value); the field reads as 0 like other invalid text.
* A damaged entry in the saved favorite structure types (non-numeric, negative, or too large) is now skipped instead of throwing every time the favorites list is opened.
* Exporting tiles or frames from the section viewer now waits for each PNG to finish writing before counting the tile as done, so the memory throttle holds and the export no longer ends with files still being written.
* Exporting a frame larger than one render pass now splits it into a grid centered on the chosen rectangle (tiles used to run off to the right and up from its center), and a frame that is too tall but not too wide is now split instead of rendered as one oversized tile.
* Export Frames and Screen Capture now show an error message when a PNG cannot be written (disk full, locked or read-only file) instead of finishing silently with the file missing.
* Typing a section range with a number too large for a section (such as a pasted 20-digit value) in the volume-position dialog now shows "Could not read ..." instead of throwing an unhandled exception.
* The Measurement extension reads the volume's UnitsPerPixel with the invariant culture. On a PC set to a comma-decimal locale (for example German), a scale of 2.5 was read as 25, so measure-tool and scale-bar lengths were wrong by orders of magnitude.
* Zoom In, Zoom Out, and Home no longer spread NaN or infinity when toolbar magnification is corrupt; Home snaps to 0.5 and zoom clicks leave the bad value unchanged.
* The scale bar no longer draws with invalid math when units per pixel is zero or not a number; it is skipped instead (the old check missed zero because it produced infinity).
* The scale bar no longer crashes the viewer when the visible section width is zero (collapsed viewport or invalid scale).
* Measure tool and scale-bar labels no longer crash when a distance is not a number or is infinite; the label keeps the starting unit instead of throwing.
* Measure tool and scale-bar labels pick the unit that puts the number between 1 and 999: 303 nm no longer shows as 0.303 um, and 0.75 um shows as 750 nm.
* The measure tool shows 0 when the drag starts on the clicked point; it showed 1 (for example 1.000 nm).
* Polygons with three or more vertices on one exactly vertical or horizontal line, plus a vertex a fraction of a pixel off that line, now triangulate. Before, they could fail to draw filled, and segmentation fell back to a single centroid prompt for them.
* Image tiles no longer stay blank for the rest of the session after a quick section change. A tile whose image was decoded but not yet shown when the section changed was never requested again.
* An image tile whose cached copy on disk cannot be read (for example, file permissions deny reading it) is downloaded from the server again instead of staying blank.
* Polygon area centroids at large mosaic coordinates (hundreds of thousands of pixels) no longer drift by tens of millipixels when the ring is stored closed; labels and geometry that use the centroid match the vertex mean again.
* The Review change feed structure/label filter no longer hides every row when the text is only separators such as a lone comma; it now shows all rows, as the server query already did.
* When only some corners of the visible area map into a section (view near the edge of the aligned data), the region loaded for annotations and tiles no longer treats two corners on the same side as opposite corners, which shrank the loaded region to a square on that side and could leave part of the view without annotations.
* Hovering or clicking near a location link or structure link that was just removed no longer risks an error: link hit-testing skips links deleted at the same moment.
* Location links to the adjacent section fade where they narrow: the thin middle of the link is drawn at half the opacity of its wide ends. Its edges are also softer, fading out over about three screen pixels instead of one.
* Hovering the mouse over a location link now fades it like other annotations; before, the link's hover highlight had no effect.

## 1.2.77 — 2026-09-30

* Auto-polygonize proposal rings no longer block translate/scale on the circle underneath. Hover and single-click prefer the annotation; double-click still accepts or dismisses the ring.
* Status-bar chips show **Pen On/Off**, **AutoPoly On/Off/Busy**, and **Seg Ready/Off/Busy**. Click a chip to toggle the mode or open the segmentation service picker.
* Viewer toolbar toggles for **Pen Mode** and **Auto Polygonize** (same actions as the Annotation menu; check state stays in sync with the menu and status chips).
* Interactive Segment skips are no longer silent: zoom-too-coarse, upload/tile failure, empty mask, and service errors show a short status-bar message (no modal dialog).
* Auto-polygonize proposal rings: double-right-click opens an **Accept / Reject** context menu (sibling absorb on accept stays automatic).

## 1.2.76 — 2026-09-29

<!-- *(csproj jumped 1.2.68 → 1.2.70 → 1.2.74 → 1.2.76. These notes cover that span. 1.2.74 was the previous committed build.)* -->

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

<!-- *(csproj jumped 1.2.65 → 1.2.68 in one bump.)* -->

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

<!-- *(csproj jumped 1.2.62 → 1.2.64 in one bump.)* -->

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

<!-- *(csproj jumped 1.2.60 → 1.2.62 in one bump.)* -->

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

<!-- *(csproj jumped 1.2.48 → 1.2.60 in one bump; includes 1.2.59-era work plus deep links.)* -->

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

<!-- *(csproj jumped 1.2.39 → 1.2.43.)* -->

* **Section number overlay** view: working overlay, preferences/converter wiring, race fixes, mouse-cursor tweak; PID parameters for overlay animation.

## 1.2.39 — 2026-01-26

<!-- *(csproj jumped 1.2.28 → 1.2.39.)* -->

* Line-ending normalization for git.
* Opacity honored for newly loaded annotations.
* Keys no longer flicker non-default command cursors.
* Foreground/background segmentation points never null; cursor hints for those points.
* Ignore Velopack `releases` folder in source control.

## 1.2.28 — 2026-01-22

<!-- *(csproj jumped 1.2.2 → 1.2.28.)* -->

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

<!-- See git history on Legacy for commit-level detail. -->

## 1.1.x (through 1.1.280, 2019-04-23)

<!-- Notes for 1.1.x: Documentation/source/Client/versionhistory.rst -->

* Last 1.1 release: the Find Volumes link works again (1.1.280).
