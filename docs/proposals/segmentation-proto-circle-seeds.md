# Proposal: circle seeds, carried shapes and islands in `segmentation.proto`

Status: superseded, 2026-10-25. The design that was applied is in `gRPC_Protos` (`Common/geometry.proto`, `Segmentation/SAM2/segmentation.proto`) and `Documentation/source/developerdocs/grpc/segmentation.rst`; this draft is kept as the review record. Includes a shared geometry proto for every annotation shape. Target file: `gRPC_Protos/Segmentation/SAM2/segmentation.proto`
(the `gRPC_Protos` submodule, branch `master`); the Sam2Trainer copy (`annotation-gallery/seg_proto/segmentation.proto`)
follows. Background: Sam2Trainer `docs/adjacent_circle_seed_plan.md` (section 4) and `docs/depth_relative_trust.md`.

## Why

- Today the client rasterizes a circle seed into 1-bit FOREGROUND mask tiles, so the server never sees the centre and
  radius. The v7 encoding trusts a circle by its radius ($w(\rho)$, $\rho = |p - c| / R$), which needs the circle itself.
- The client is supposed to scale a circle seed by the model's `circle_diameter_scale` before rasterizing, and does not.
  With the circle in the request, the server applies whatever the served model was trained with, and clients never scale.
- Neighbouring circle annotations arrive merged into the background mask, so the server can only approximate their
  radius trust (the v5 contract says so). Sent as circles, they get the trust the model was trained with.
- Carried polygons (a linked polygon moved through the section correction) are trusted only in their deep part, by
  depth with local thickness, and a branching one splits into islands that production returns as separate masks.

## Shared geometry: `gRPC_Protos/Common/geometry.proto`

Every geometric concept of an annotation, defined once and imported by the annotation, section-correction and
segmentation protos. Today the same ideas are defined three times (annotation `AnnotationPoint` and
`BoundingRectangle` in double, correction `Vector2` in double, segmentation `Point`, `BoundingBox` and `Polygon` in
int32), and annotation shapes travel only as opaque WKT (`Geometry`).

```proto
syntax = "proto3";

package viking.geometry.v1;
option csharp_namespace = "Viking.gRPC.Geometry.V1";

// ---------- Coordinates ----------

// A point or vector in a 2-D frame (Frame). Double precision everywhere: mosaic coordinates reach 1e5-1e6 pixels and
// a circle's radius must survive a coarse downsample.
message Vector2 {
  double x = 1;
  double y = 2;
}

// Which coordinate system a shape's numbers are in. Was segmentation's CoordinateSpace; moved here unchanged.
enum CoordinateSpace {
  COORDINATE_SPACE_UNSPECIFIED = 0;
  // A section's own mosaic, unregistered: pixels at downsample 1 unless Frame.downsample says otherwise.
  COORDINATE_SPACE_MOSAIC = 1;
  // The registered volume (the section mapped through its slice-to-volume transform).
  COORDINATE_SPACE_VOLUME = 2;
}

// The frame a shape's coordinates are in: X right, Y up, pixels of the space at the downsample.
message Frame {
  CoordinateSpace space = 1;
  // The section (z) the coordinates belong to.
  int64 section = 2;
  // Mosaic or volume pixels per coordinate unit; 0 or 1 is full resolution.
  double downsample = 3;
}

// Axis-aligned rectangle, inclusive corners.
message Rectangle {
  double x_min = 1;
  double y_min = 2;
  double x_max = 3;
  double y_max = 4;
}

// ---------- Shapes (one per AnnotationType) ----------

// A closed ring of vertices. The first vertex is not repeated at the end; orientation is not significant.
message Ring {
  repeated Vector2 points = 1;
}

// POLYGON: an exterior ring and its holes, straight edges.
message Polygon {
  Ring exterior = 1;
  repeated Ring holes = 2;
}

// CIRCLE. The centre is the location's position (never the shape's first point); the radius is the TRUE radius in
// this frame. Between mosaic and volume (or between sections) the radius is carried by the five-point mean scale,
// never copied (Viking .cursor/rules/circle-mapping-scale.mdc).
message Circle {
  Vector2 center = 1;
  double radius = 2;
}

// ELLIPSE: centre, semi-axes and the rotation of the major axis from +X, counter-clockwise, in radians.
message Ellipse {
  Vector2 center = 1;
  double semi_major = 2;
  double semi_minor = 3;
  double rotation = 4;
}

// POLYLINE: straight segments through the vertices, drawn with a line width.
message Polyline {
  repeated Vector2 points = 1;
  // Line width in this frame's units.
  double width = 2;
}

// How a curve's drawn outline is made from its control points, so any consumer reproduces Viking's outline.
enum CurveMethod {
  CURVE_METHOD_UNSPECIFIED = 0;
  // Centripetal Catmull-Rom through the control points (Geometry/CurveFitting/CatmullRom.cs, FitCurve).
  CURVE_METHOD_CATMULL_ROM = 1;
}

message CurveFit {
  CurveMethod method = 1;
  // Points added between consecutive control points (NumInterpolations). 0 draws the control polygon.
  uint32 interpolations = 2;
}

// OPENCURVE (closed false) and CLOSEDCURVE (closed true): a fitted line through control points, with a line width.
message Curve {
  repeated Vector2 control_points = 1;
  bool closed = 2;
  double width = 3;
  CurveFit fit = 4;
}

// CURVEPOLYGON: a polygon whose exterior and hole rings are control rings, each fitted closed.
message CurvePolygon {
  Ring exterior = 1;
  repeated Ring holes = 2;
  CurveFit fit = 3;
}

// One annotation shape. The case is the annotation type; field numbers follow AnnotationType + 1.
message Shape {
  oneof kind {
    Vector2 point = 1;          // POINT
    Circle circle = 2;          // CIRCLE
    Ellipse ellipse = 3;        // ELLIPSE
    Polyline polyline = 4;      // POLYLINE
    Polygon polygon = 5;        // POLYGON
    Curve open_curve = 6;       // OPENCURVE
    CurvePolygon curve_polygon = 7;  // CURVEPOLYGON
    Curve closed_curve = 8;     // CLOSEDCURVE
  }
}

// A shape with the frame its numbers are in.
message PlacedShape {
  Shape shape = 1;
  Frame frame = 2;
}
```

### How each proto adopts it

- **Annotation** (`gRPCAnnotationServiceTypes/Protos`, which the repo rule says belongs in `gRPC_Protos`; moved to
  `gRPC_Protos/Annotation/` as part of this change): `Location` gains `viking.geometry.v1.Shape mosaic_geometry` and
  `volume_geometry` in place of the WKT `Geometry mosaic_shape` / `volume_shape` (one installation: the WKT fields are
  reserved, not kept beside them); `AnnotationPoint` positions become `Vector2` (the section is already
  `Location.section`); `BoundingRectangle` becomes `Rectangle`. `radius`, `width` and `closed` on `Location` move into
  the shape (`Circle.radius`, `Polyline.width`, `Curve.width`, `Curve.closed`) and are reserved. `AnnotationType`
  stays (it names a location's type before its shape is loaded) and matches `Shape`'s cases.
- **Section correction** (`section_correction_v2.proto`): its `Vector2` is replaced by `viking.geometry.v1.Vector2`
  (same field numbers and types, so wire-compatible).
- **Segmentation** (`segmentation.proto`): `CoordinateSpace` moves to the shared file; clicks and rectangles become
  `Vector2` and `Rectangle` (double; new field numbers, the int32 ones reserved, since int32 to double is not
  wire-compatible); the traced result `Polygon` becomes the shared `Polygon`; circle seeds use the shared `Circle`
  (below).

## Segmentation changes

### Circles use the shared `Circle`

`viking.geometry.v1.Circle` in the request's frame: mosaic pixels at the tiles' downsample, X right, Y up, in the
tiles' `TileCoord.space`. For volume-space tiles the client maps a section circle to volume with the five-point mean
scale before sending it. The radius is the TRUE radius, as annotated or as carried; the client never scales it, and the
server applies the served model's `circle_diameter_scale` and trust.

### `Feature`: circles

```proto
message Feature {
  reserved 1, 2, 3;
  reserved "mask";
  // Clicks and rectangle hints, now double (were int32 Point and BoundingBox, fields 1 and 2).
  repeated viking.geometry.v1.Vector2 points = 6;
  repeated viking.geometry.v1.Rectangle rectangles = 7;
  MaskRole mask_role = 4;
  // Foreground Feature: circle seeds; the object contains them. Several circles are one object (an overlap group of
  // circle annotations of one cell); their seeds combine by the maximum. The server builds each seed the way the
  // served model was trained (ModelInfo.prompt_encoding): radius-relative trust for edge-confidence-v7, the disc
  // scaled by circle_diameter_scale with edge confidence for v6. A request with foreground circles sends no
  // FOREGROUND mask tiles (INVALID_ARGUMENT if it does); its cells are still named in SegmentTilesRequest.tiles.
  // Background Feature: neighbouring circle annotations; each is trusted by its radius (the model's
  // circle_annotation_trust) instead of being painted into the BACKGROUND mask tiles, from which the client then
  // leaves them out. The server refuses more than its limit (default 256 per Feature) with RESOURCE_EXHAUSTED.
  repeated viking.geometry.v1.Circle circles = 5;
}
```

### `MaskRole`: carried shapes

```proto
enum MaskRole {
  MASK_ROLE_UNSPECIFIED = 0;
  MASK_ROLE_SEED = 1;
  MASK_ROLE_CORRECTION = 2;
  // A shape carried from an adjacent section: a linked polygon moved through the section correction. Only its deep
  // part is trusted: depth-relative trust with depth normalised by local thickness (full trust deeper than half the
  // local depth, nothing in the outer circle_diameter_scale band). Its trusted cores are its islands; see
  // SegmentTilesRequest.split_islands. A model that does not list "carried" in client_contract.mask_roles refuses it
  // with FAILED_PRECONDITION.
  MASK_ROLE_CARRIED = 3;
}
```

### `SegmentTilesRequest`: one mask per island

```proto
  // With a MASK_ROLE_CARRIED foreground mask: segment each island of the carried shape separately and return one
  // SegmentResult per island (SegmentResult.island), so a process that branches between sections comes back as one
  // mask per branch. False segments the shape as one object. Ignored for other roles.
  bool split_islands = 16;
```

### `SegmentResult`: which island

```proto
  // With split_islands: the island of the carried foreground shape this segment was grown from, counted from 1 in
  // decreasing order of the island's trusted area. 0 when the request did not split.
  uint32 island = 13;
```

### Contract and status

- `client_contract.circle_diameter_scale` stays in `contract_json` for information; clients stop applying it.
- `client_contract.mask_roles` gains `"carried"` for a model trained with carried shapes (v7).
- `ModelInfo.prompt_encoding` reports `edge-confidence-v7` for such a model; the server implements v6 and v7 and picks by
  the model's manifest.
- No capability flag: server and clients are deployed together (one installation), and the client's circle
  rasterization path is removed, not kept beside the new one.

## Client changes this implies

- Auto-segment, the circle context menu and the overlap-merge group send circles in `foreground_feature.circles` with the
  true radius instead of FOREGROUND mask tiles; the paint command keeps sending its painted mask as tiles.
- Background: neighbouring circles go in `background_feature.circles`; polygons stay BACKGROUND mask tiles.
- Carried polygons (auto-segment carrying a linked polygon to the adjacent section) go as FOREGROUND mask tiles with
  `MASK_ROLE_CARRIED` and `split_islands = true`; the client keeps every returned island and de-duplicates results that
  overlap.

## Open questions

0. Shared geometry scope: move the annotation protos into `gRPC_Protos/Annotation/` and replace `Location`'s WKT shapes
   with structured `Shape`s in this change (a large client and server migration: every reader and writer of
   `mosaic_shape` / `volume_shape`), or first add the shared file and adopt it in segmentation and correction only?
0b. Ellipse: Viking has the `ELLIPSE` type but no tool creates one; keep it in `Shape` for completeness (proposed) or
   leave it out until used?
1. Should several foreground circles be allowed (overlap groups), or one circle per request with groups sent as tiles?
2. `split_islands` per request, or always on for carried shapes?
3. Circles on the background Feature: does the client also stop sending circle neighbours in BACKGROUND tiles (proposed),
   or send both during a transition (the one-installation rule says no)?
4. Island numbering by trusted area (proposed) or by position.
