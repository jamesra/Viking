# Viking changelog

<!--
Notes for whoever updates this file. Comments like this one are not shown in Viking.

- This is the change log of the gRPC Viking client built from this repository (branch dev). The WCF client in VikingLegacy
  keeps its own VikingChangelog.md with its own version numbers; the two are separate files.
- Viking installs this file beside Viking.exe and shows it from Help > Change Log, and once the first time each version starts.
- One "## <version> — <date, or 'not yet released'>" heading per version, newest first, each followed by a bulleted list of
  changes written for users: what they can see or do differently, new or changed hotkeys, menus and preferences. No
  developer detail; put that in a comment.
- Add a bullet in the same commit as each user-visible change. When the version in Clients/Viking/Viking/Viking.csproj
  changes, rename the unreleased heading to the new version (ChangelogTests in VikingCoreTests checks this); when a version
  ships, replace "not yet released" with the date and channel, for example "2026-10-26 (Viking Test, unsigned)".

Committed application version: 1.2.111.0. The file was started on 2026-10-27; the first section lists what this client
gained from about 2026-10-20 on. Earlier changes are not listed.
-->

## 1.2.111 — not yet released

* **Help > Change Log** shows this list of changes, and Viking opens it the first time each new version starts.
* **Del** hides the annotation, location link or auto-segmentation ring under the mouse for a while, so the one beneath it (for example a location on an adjacent section that you want to drag onto this section) can be picked. Near the end of the time it fades back in, and it can be clicked again once it is fully back. **Insert** over a hidden one brings it back early. The time is **Hide annotation time (seconds)** in Annotation Preferences (default 30 seconds). The F1 help lists both keys.
* Annotation Preferences has a new **Visuals** section, under Basic Settings, for the settings that only change how the view looks: text sizes, annotation opacities and sizes, control points, location links and the auto-segmentation shimmer. Several preferences now show their effect in the view while the dialog is still open.
* Location links (the hourglass between two linked locations on different sections) are drawn strongly next to the locations they join and fade to nothing in the middle, which cannot be clicked. Move the mouse over a link to see all of it; each end is then labelled with the ID of the location at the other end, with an up or down arrow for a higher or lower section. Right-click a link for **Go to partner**. Links leave a polygon from its outline and attach to lines, and their colours are softer.
* A link to an overlapping location on an adjacent section is drawn as an arrow marker inside the location, numbered with that section in the link's colour. Right-click a marker to **Delete Link** or **Split structure**.
* Polygon labels sit in the widest part of the cell.
* A pen tap on the section numbers at the left edge of the view shows the tapped section. While you are drawing with the pen, touching that strip no longer changes sections in the middle of the stroke.
* Auto-segmentation answers sooner: the circle under the cursor goes first, image tiles are sent to the server ahead of time, and several requests run at once.
* The segmentation server picker shows each server's model and load, refreshed every second; servers that are offline are grayed out and cannot be picked.
* Viking waits longer for a busy segmentation server and no longer loses its connection to it while it is busy or idle.
* If the connection to the annotation server drops or the server restarts while Viking is loading structure types, the volume scale or a structure's child structures, Viking tries again instead of failing, as it already did for locations and structures.
* A new structure's first location shows its marker on the next section straight away, and a marker on an adjacent section that could not be drawn is tried again.
* Status bar tooltips show above the status bar, so they no longer flash.
* Opening Viking from a link to a location jumps there once; zooming afterwards no longer resets the view.
* Circle annotations keep their size when they are carried between a warped section and the volume.
* Linking two locations after deleting a location link no longer fails with an error, and deleting a link and then linking the same two locations again keeps the new link.
* Deleting two linked locations before the changes are saved no longer makes the whole save fail on the server.
* Section corrections: near the edge of an annotated area, where the correction lattice has only some of its points, the image is now shifted by the same amount the correction service uses for annotations there. Before, Viking worked out the shift its own way and could disagree with the service by up to a lattice spacing.
* Fix: dragging a polygon vertex could move a different vertex, and **Ctrl**-clicking a vertex to remove it could remove a different one. Near the polygon's first vertex, Viking picked that vertex instead of the one under the cursor; it now always picks the nearest vertex, including vertices of holes.

<!--
Developer notes for 1.2.111: the Del/Insert hiding, Visuals section, pen-on-overlay fix and this change log are ports of
VikingLegacy 57dd42bd..f946d5b0, 200003d6, 402c7da1 and 5c33f0a4. Location link rendering mirrors Legacy (a83fce1b, 1df31a12).

Database change, not a client bullet: 697a6185 fixes dbo.MergeStructures (AnnotationDatabase.sqlproj) so merging into a
structure whose Notes is NULL keeps the merged notes. Users get it when the database project is published to their
annotation database, whatever client version they run, so it is not listed under a client version. Add a user bullet
("Merging one structure into another keeps the merged structure's notes when the structure you keep had none") only if
a release note for the database publish is wanted here.
-->
