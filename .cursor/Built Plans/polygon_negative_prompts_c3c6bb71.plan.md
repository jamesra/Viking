---
name: Polygon negative prompts
overview: Use constrained polygon triangulation and one centroid per interior triangle for negative segmentation prompts. This is more robust than 90% centroid scaling for concave polygons and holes, while reusing the geometry code already used to render polygons.
todos:
  - id: polygon-prompts
    content: Generate polygon negative prompts from constrained-triangulation face centroids with safe fallback
    status: completed
  - id: polygon-prompt-tests
    content: Add concave, hole, and triangle-count tests for polygon negative prompts
    status: completed
  - id: verify-viking
    content: Run focused tests and build the complete Viking application
    status: completed
isProject: false
---

# Polygon negative prompt sampling

## Decision
Use constrained triangulation centroids. Scaling vertices toward a polygon centroid is not reliably interior for concave polygons (the centroid itself may be outside), while centroids from the existing constrained `GridPolygon.Triangulate()` mesh remain inside the polygon and exclude holes.

## Implementation
- Update [`Clients/Viking/WebAnnotation/Extensions.cs`](Clients/Viking/WebAnnotation/Extensions.cs):
  - Replace the single polygon-centroid branch in `AnnotationPointExtensions.GetAnnotationRepresentativePoints` with a helper that triangulates each `POLYGON`, `CURVEPOLYGON`, or `CLOSEDCURVE` and returns each mesh-face centroid translated back from the triangulator's centroid-relative coordinates.
  - Preserve current behavior for circles, ellipses, points, and open paths.
  - Catch known invalid/nonconforming triangulation failures and fall back to the current centroid only when it is inside the polygon, so one malformed annotation does not abort segmentation.
  - Update the method documentation to describe the new polygon sampling contract.
- Keep [`Clients/Viking/WebAnnotation/UI/Commands/Segmentation/CircleSegmentationPrompts.cs`](Clients/Viking/WebAnnotation/UI/Commands/Segmentation/CircleSegmentationPrompts.cs) unchanged: both auto-circle and interactive segmentation already consume `GetAnnotationRepresentativePoints`, so the improved negatives flow through both paths without duplicate logic.

## Tests and verification
- Extend [`Clients/Viking/WebAnnotationTests/Commands/AutoCirclePolygonizeTests.cs`](Clients/Viking/WebAnnotationTests/Commands/AutoCirclePolygonizeTests.cs) with direct polygon-sampling tests covering:
  - a simple polygon produces one negative point per triangle;
  - all generated points are contained by a concave polygon;
  - generated points do not fall inside an interior hole.
- Build [`Clients/Viking/Viking/Viking.csproj`](Clients/Viking/Viking/Viking.csproj) and run the focused WebAnnotation segmentation tests.