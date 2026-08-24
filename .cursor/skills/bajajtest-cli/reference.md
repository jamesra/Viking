# BAJAJTEST visual reference

## Correct vs incorrect (Bajaj C1–C3)

| Signal | Correct | Incorrect |
|--------|---------|-----------|
| Incomplete vertices (orange/red / dark-red overlay) | Gone after later mesh/line stages except real open ends | Persist on interior contour verts that should have tiled |
| 3D mesh | Closed strip, contours recovered, no bow-ties | Holes, self-intersections, combs, caps that ignore the contour |
| Edge colors | Contour cyan, corresponding gold, surface blue; medial-axis/untiled only where contours do not overlap | Lots of pink FLYING, red INTERNAL, or black UNTILED on regions that should tile |
| OTV chords | Glow = accepted and in the mesh; ladder = rejected | Accepted chords that cross, or obvious pairings left rejected |
| Stage names | Pipeline reaches face gen / caps without `error` in manifest | Stops at Delaunay, invalid-edge, or region-close |

C1: no self-intersection. C2: no vertical comb topology. C3: resampling a slice reproduces input contours.

## HUD legend (vertex / overlay)

| Entry | Color |
|-------|--------|
| Medial axis vertex | MediumPurple |
| Corresponding vertex | DarkSlateBlue |
| Face complete (upper / lower) | LimeGreen / ForestGreen |
| Incomplete vertex (upper / lower) | Orange / Red |
| Vertex missing shape index | Aqua |
| Incomplete-vertices overlay | DarkRed |
| Current triangulation edges | Black |
| Accepted chord | LightGray, Glow line |
| Rejected chord | LightGray, Ladder line |
| Region polygon | random per region |

## Line-view `EdgeType` colors

From `ColorExtensions.GetColor`: INVALID ghost, FLYING pink, CONTOUR cyan, SURFACE blue, CORRESPONDING gold, INTERNAL red, FLAT brown, INVAGINATION orange, HOLE purple, UNTILED black, MEDIALAXIS light cyan, CONTOUR_TO_MEDIALAXIS dark cyan, ARTIFICIAL yellow-green.

## Default capture stages

Mesh view names written by `BajajOTVAssignmentView.GenerateMesh` (2D and 3D each):

- Remove Invalid Edges
- CompleteCorrespondingVertexFaces
- MergeAndCloseRegionsPass
- FirstPassSliceChordGeneration
- FirstPassFaceGeneration
- Second MergeAndCloseRegionsPass
- Cap upper polygons
- Cap lower polygons

Also: `overview-2d`, `otv-chords`, each `listLineViews` name (e.g. FirstPassDelaunay), `region-0` …

PNG slug is `{index:D2}-{sanitizedStage}-2d.png` or `-3d.png`. Because the overview stage is already named `overview-2d`, that file is `00-overview-2d-2d.png`.

## Interactive gamepad (when not capturing)

| Control | Action |
|---------|--------|
| A | Cycle `MeshViews` |
| B | Cycle line/edge passes |
| Y / X | Cycle region passes forward / back |
| Left shoulder | Toggle 2D / 3D |
| Start | Rebuild mesh for the current case |
| Right shoulder / stick | Vertex label mode / position labels |
| Left stick | Cull mode |
| Back | Reset 3D camera |

Keyboard F-keys / numpad still switch `TestMode` (numpad 4 = BAJAJTEST).

## ReproSet

Canonical list: `BajajAssignmentTest.ReproSet` in `Clients/MonogameTestbed/BajajTest.cs`. Each row is `(location IDs, endpoint, historical exception text)`. Index 5 = RPC1 locations 145431, 145428, “Region with no perimeter”.
