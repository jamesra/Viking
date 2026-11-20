---
name: mosaic-annotation-remap
description: >-
  Move a section's annotations from an old .mosaic to a rebuilt one (nornir rescue-damaged-section, a re-run
  Translate/Grid refine) through tile coordinates with VikingAU remap-to-tiles / remap-from-tiles, safely: which
  mosaics to use, the checks before writing, the dry runs, how to read the report, and how to undo it. Use whenever
  a section's mosaic was or will be replaced and its annotations must follow, when asked to remap, rescue or
  translate annotations to a new mosaic, or when annotations sit off the tissue after a mosaic install.
---

# Moving annotations to a rebuilt mosaic

Annotations are stored in mosaic space (`project_mosaic-authoritative-volume-cache`): a rebuilt `.mosaic` moves the
tiles under them, so every vertex must follow the tile it was drawn on. The tile images do not change between the
two mosaics, only where they sit, so the mapping is exact:

`p_new = new_t.Transform(old_t.InverseTransform(p_old))`, for the tile `t` that held `p_old`.

Code: `Geometry/Transforms/MosaicTileLocator.cs`, `Geometry/Transforms/TileRemap.cs`,
`Clients/VikingAU/MosaicRemap.cs`; tests `GeometryTests/MosaicTileLocatorTests.cs`. Python twin (same rule, for
cross-checks before any annotation export): nornir `nornir_imageregistration/mosaic_remap.py`
(`MosaicPointRemapper`) and `scripts/remap_mosaic_points.py`. The method and the 1240 measurements are in
`docs/publication_notes.md` ("Moving annotations to a rebuilt mosaic through tile coordinates").

## How it decides (know this before reading a report)
- **Tile choice:** among tiles whose image holds the point, the one whose mapped image centre is nearest (nornir
  Assemble composites the same way, so a point follows the tile the annotator saw). Ties within a pixel are
  possible (1240: tiles 947/973 at 2333.0 vs 2333.1 px); either is a valid answer.
- **Status per vertex:** Mapped; Extrapolated (inside a tile's outline, off its image: warp edge, still mapped);
  NoTile (no old tile covers it).
- **Whole locations only.** A location only partly on tiles missing from the new mosaic, an edit after the export,
  or a rebuilt shape that is invalid is **left unchanged** and listed under "Skipped". Nothing is saved half-moved.
- **Locations wholly on dropped tiles are deleted** (James Anderson, 2026-11-20). When every tile a location used
  is gone from the new mosaic, the run first links its neighbours to each other in its place (149-150-151 with 150
  dropped becomes 149-151; a run of dropped locations is bridged end to end; a dropped branch point links every
  pair of its neighbours; only neighbours of one structure are linked), then deletes it. Every one is written,
  before anything changes, to `<report>.dropped.json` (`--dropped-log`): id, structure and type, section, X/Y,
  mosaic and volume WKT, radius, width, flags, attributes, links before, and the links added, with the outcome.
  The report lists them, the links added, and **structures left with no location, which are not deleted**. A
  group whose replacement link fails keeps its locations. The dry run writes the same file with outcome
  "planned".
- **NoTile locations are estimated.** A location step 1 found on no tile (drawn past the edge of the imaged area;
  RC1 section 4 had 16 edge circles) is moved as one piece by the shift of the old mosaic's nearest tile reference
  point (image corner, edge midpoint or centre; `MosaicTileLocator.TryEstimateShift`), measured from its X,Y. It is
  a translation only and needs the old `.mosaic` named in `tiles.json`, unchanged (sha256 checked); otherwise the
  location is skipped as before. Listed under "Estimated locations" with the tile and distance used.
- **Circles map their centre and keep their radius.** Old and new mosaic are both mosaic space at tile-pixel
  scale, so the five-point scale rule (`.cursor/rules/circle-mapping-scale.mdc`) does not apply. Never reintroduce
  rim samples: on 1240 they landed on neighbouring tiles moved 40-54k px and gave radii of 10,000+ px.
- **Line widths** are unchanged (same pixel scale).
- **Saves** go through `SetShapeFromGeometryInSection` / `SetShapeFromCircleInSection`, which write the mosaic
  shape and refresh the volume cache under the section's current default volume transform. Never write
  `.MosaicShape =` directly.
- **Overlaps are reported, not clipped.** `cell-boundaries-never-overlap.mdc` still holds: new overlaps between
  different cells in the report must be fixed by hand (or by a clip pass) afterwards.

## Before anything is written

1. **Name the two mosaics exactly.**
   - *Old* = the `.mosaic` the annotations were drawn on: the production file from before the install, from the
     rescue backup (`Y:\_nornir_backups\<volume>\<date>-section<N>-rescue\...\Grid_*.mosaic`) or the
     `annotation_transfer\Grid_old.mosaic`. Not a work-folder intermediate.
   - *New* = the file **as installed** in `Y:\Volumes\<volume>\TEM\<N>\TEM\`, the one Viking draws. If
     `install_rescue.py` anchored the centre tile, the installed file is shifted from the work-folder
     `Grid_new.mosaic`; use the installed one. Translated vs Grid: use the transform Viking's section mapping uses
     (the volume's default, normally `Grid`).
   - Both must carry the same tile numbers for the tiles the annotations sit on. Tile ids come from the tile file
     names, so a renumbered rebuild cannot be remapped this way.
2. **Make sure the annotations have not already been moved.** Remapping twice moves them twice. Check:
   - the newest `LastModified` of the section's locations against the install time of the new mosaic (RC2 1240:
     all 116 at 2026-11-04 04:23, install 11:33 the same day: a bulk update, so ask what it was);
   - in Viking, a few annotations against the tissue: still sitting where the *old* mosaic had the tissue (off the
     cells on the new mosaic) means not yet moved;
   - the publication/repair notes and the nornir section repair notes for an earlier remap or a rigid
     `--translate` of the section.
   If in doubt, stop and ask the user.
3. **The server.** VikingAU talks to a **gRPC** annotation server (review rights: it logs in through the identity
   server with `-u/-p`). A volume served only by the WCF service (RC2 production today) cannot be remapped by
   VikingAU until a gRPC annotation server runs against its database; say so rather than working around it.
4. **Freeze edits on the section** if annotators are active. Locations edited between the two steps are skipped
   (safe, but they then need the run again). Run step 2 soon after step 1.
5. **The user names the section and approves the real run.** Dry runs need no approval; the run without
   `--dry-run` writes production annotations.

## Steps

Build: `dotnet build Clients/VikingAU -f net9.0 -c Release` (or `dotnet run --project Clients/VikingAU -f net9.0 --`
in place of `VikingAU` below). Keep every output file in a dated folder with the two mosaics, e.g.
`Y:\Volumes\_nornir_work\<N>\annotation_transfer\remap-<date>\`.

1. **Export to tile coordinates** (reads the server, changes nothing):
   ```
   VikingAU remap-to-tiles -v <VolumeXML url> -u <user> -p <password> -s <N> --mosaic <old.mosaic> -o tiles.json
   ```
   Read the console: locations, how many were expressed in tiles, and every skipped id with its reason
   (NoMosaicShape, UnsupportedShape, NoTile). NoTile on many locations means the wrong old mosaic.
2. **Round-trip check** (dry run through the *old* mosaic): every shift must be ~0 (exact in C#).
   ```
   VikingAU remap-from-tiles -v <url> -u <user> -p <password> -s <N> --mosaic <old.mosaic> -i tiles.json --dry-run --report roundtrip.txt
   ```
3. **Dry run through the new mosaic** and read the whole report (default `tiles.json.report.txt`):
   ```
   VikingAU remap-from-tiles -v <url> -u <user> -p <password> -s <N> --mosaic <new.mosaic> -i tiles.json --dry-run
   ```
   - *Vertex shift median/p95/max*: should match how the rebuild moved the tiles. A repaired tear gives one
     cluster per piece (1240: about (+200, -3540) and (+190, +3660), max 3669 px). A shift far outside the
     clusters is a wrong tile choice or a bad old registration: look at that location.
   - *Overlap disagreements > 5 px*: vertices drawn where two old tiles overlapped and the new mosaic now puts
     the two tiles' versions far apart (the old mosaic was misregistered there, or the overlap straddles the
     tear). The chosen tile is the one the annotator saw, so the move is right; list these for the annotators.
   - *New overlaps between different cells*: to fix after the run.
   - *Estimated*: on no tile in step 1, moved with the nearest tile. Check the ones far from that tile
     (distance in the list) or involved in a new overlap.
   - *Skipped*: each needs a person (redraw, or move by hand).
   - *Dropped locations*: deleted by the real run. Check the links added and the structures left with no
     location, and show both to the user with the report.
   - Optional cross-check before the first real run on a volume: map the same vertices with nornir
     `MosaicPointRemapper` (CPU: set `NORNIR_COMPUTATIONAL_LIBRARY=numpy`; venv
     `D:\src\git\nornir\venv\pyre314\Scripts\python.exe`). On 1240 the two agreed to 0.09 px median, 1.45 px max
     on the same tile.
4. **Show the user the report and get a yes for the section.** Then the real run: step 3 without `--dry-run`. The
   report then says "(saved)". On "(SAVE FAILED)" some locations may already be saved: read the console error,
   fix the cause, and rerun **this step with the same `tiles.json`** (saved ones are skipped as changed since
   export, the rest are moved). Never re-export (step 1) through the old mosaic after a partial save: the moved
   locations would be moved a second time.
5. **Verify in Viking**: a sample of locations from each shift cluster, the overlap-disagreement and new-overlap
   lists, and the skipped ids. Hand the skipped and overlap lists to the annotators.
6. **Record it** in the section's repair notes (nornir rescue-damaged-section says what `install_rescue.py
   --annotation-rescue-report` takes). Keep `tiles.json` and the reports with the mosaics.

## Afterwards and undo
- **Volume positions follow the current stos.** After a rescue the section's stos are stale (nornir skill,
  "After install"). When the section is re-registered and the volume transform changes, run VikingAU
  `update-positions -s <N>` to refresh the volume cache; the mosaic positions written here stay.
- **Undo:** the old tile file cannot be replayed (every location's LastModified changed, so all are skipped).
  Export again through the *new* mosaic (`remap-to-tiles --mosaic <new>`), then `remap-from-tiles --mosaic <old>`.
  Locations edited by people since the remap are skipped; those keep their edits. Deleted (dropped) locations do
  not come back this way: recreate them from `<report>.dropped.json` and remove the links it lists as added.
- **Do not** remap a section whose annotations were already moved by a rigid `update-positions --translate`, an
  earlier remap, or hand edits for the same rebuild, without first undoing that or exporting from the mosaic they
  now match.
