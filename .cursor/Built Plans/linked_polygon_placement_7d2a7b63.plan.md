---
name: Linked polygon placement
overview: Restore direct polygon copy when no segmentation service is configured. When it is configured, the standard path is a translate/scale preview, then SegmentationCommand with sparse unsmoothed triangle-centroid prompts, reusing a viewport image auto-segment already uploaded. Escape on that command places the dragged polygon so a slow service does not discard the placement.
todos:
  - id: direct-place
    content: Keep TranslatePolygonCommand then save the transformed mosaic polygon when no segmentation endpoint is configured
    status: completed
  - id: triangle-prompts
    content: Replace medial-axis foreground points with unsmoothed triangle centroids of the transformed polygon, mapped to volume
    status: completed
  - id: escape-accepts-drag
    content: Standard SegmentationCommand Escape, when a fallback polygon was supplied, cancels the in-flight request and places that polygon
    status: completed
  - id: share-viewport-image
    content: Reuse an already-uploaded viewport image across auto-segment and SegmentationCommand instead of uploading the same screen again
    status: completed
isProject: false
---

# Linked polygon placement

Both outcomes start from the existing warp in [`CreateCommandForPolygon`](Clients/Viking/WebAnnotation/View/LocationActions.cs) (`CREATELINKEDLOCATION`): move the source mosaic shape to the click, map it onto the current section, then show [`TranslatePolygonCommand`](Clients/Viking/WebAnnotation/UI/Commands/TranslatePolygonCommand.cs). That command already draws the polygon (smoothing is display-only in `CreateUpdateView`; the callback polygon stays the control-point ring). Release still places or segments from that transformed ring.

```mermaid
flowchart TD
  click[Pull polygon through]
  preview[TranslatePolygonCommand shows polygon]
  gate{Segmentation endpoint configured}
  place[Save transformed mosaic polygon as linked location]
  prompts[Centroids of unsmoothed mesh triangles]
  segment[SegmentationCommand waits for the mask]
  escape[Escape places the dragged polygon]
  click --> preview --> gate
  gate -->|no| place
  gate -->|yes| prompts --> segment
  segment -->|mask arrives| placeSeg[Save segmented polygon]
  segment -->|Escape| escape
```

## No segmentation server

Keep the `else` branch: `!Global.IsSegmentationServiceAvailable` (no valid endpoint URL). On release, write `mosaicPolygon` with `SetShapeFromGeometryInSection` and enqueue `CreateNewLinkedLocationCommand`. Do not open `SegmentationCommand`.

Today a configured URL always takes the other branch, even if nothing is listening. That branch never saves the dragged polygon. The direct-place path is what should run when the endpoint is not configured.

## With segmentation

Delete the `MedialAxisFinder.ApproximateMedialAxisImproved` call. It runs on the pre-drag mosaic polygon, so the prompts ignore translate/scale, and the axis is far denser than a mesh.

On release, take the callback’s transformed mosaic polygon (not a smoothed copy) and call the existing [`AnnotationPointExtensions.GetPolygonTriangleCentroidPoints`](Clients/Viking/WebAnnotation/Extensions.cs). Map those mosaic points with `TrySectionToVolume` and pass them as foreground to `SegmentationCommand`, with an empty initial background list. Also pass `loc.ID` and `loc.ParentID` as the exclude ids so the source annotation is not turned into a red avoid mark. Fallback when meshing fails is already the polygon centroid inside that helper.

During the preview, pass those same unsmoothed centroids into the `TranslatePolygonCommand` extra-points constructor so the green clicks move with the shape. Recompute them from the transformed polygon at release; do not reuse the pre-drag axis or the single temporary centroid.

Pass the transformed volume polygon into `SegmentationCommand` as the placement fallback. That is part of this standard path, not a slow-service mode. The command still waits for the mask with no timeout and no background replace.

## Shared viewport image

Auto-segment and `SegmentationCommand` each own a [`SegmentationViewportSession`](Clients/Viking/WebAnnotation/UI/Commands/Segmentation/SegmentationViewportSession.cs). Dropping the polygon always calls `UploadCurrentImage` in `OnActivate`, even when auto-segment already uploaded that screen. `AdoptUploadedImage` exists, but only auto-polygonize uses it, and only for the same circle id via [`AutoPolygonizeCache.TryGetUploadContext`](Clients/Viking/WebAnnotation/UI/AutoPolygonize/AutoPolygonizeCache.cs). `SegmentationCommand.OnDeactivate` and pan-away call `DeleteCurrentImageAsync` on that id, which would delete an image another caller still needs.

One lease for the current view, shared by auto-segment and every segmentation command:

- Key the latest upload by world bounds and downsample, not by location id. Reuse when [`AreViewportBoundsSimilar`](Clients/Viking/WebAnnotation/UI/Commands/Segmentation/SegmentationViewportSession.cs) matches (1% of the longer side) and downsample has not moved by 2× ([`DownsampleChangedByFactorOfTwo`](Clients/Viking/WebAnnotation/UI/AutoPolygonize/AutoPolygonizeSelection.cs)).
- On activate, adopt that image and take a hold ([`AcquireBatchHold`](Clients/Viking/WebAnnotation/UI/AutoPolygonize/AutoPolygonizeCache.cs)). `SegmentAsync` then skips upload because `CurrentImageId` is set. Prompt mapping uses the adopted world rectangle, which `AdoptUploadedImage` already installs.
- If an upload of those bounds is still in flight, await that task. Do not start a second capture.
- If nothing matches, upload once and publish the id on the same lease so the next command can adopt it.
- Release the hold on deactivate or when the view moves past the 1% test. `DeleteImage` runs only when the last hold drops, which the cache already does through `ImageLeaseReleased`. Do not delete from `SegmentationCommand` while auto-segment still holds the id.

Escape is how that same command stays usable when the service is slow. Today [`Command.OnKeyPress`](Clients/Viking/NGVV/UI/Command/Command.cs) calls `CancelCommand`, which clears the queue and drops the drag. When a fallback polygon was supplied:

- Cancel the in-flight upload and `SegmentAsync` before accepting, so a late mask cannot overwrite the placement.
- Call the same success callback used for a finished mask, with the fallback volume polygon.
- Do not call `CancelCommand`. That clears the queue and would drop the `CreateNewLinkedLocationCommand` the callback just enqueued.
- Deactivate after the callback.

Callers that pass no fallback (resegment, circle-to-polygon) keep the current Escape behavior: cancel and save nothing. Add one help line when a fallback is present: Escape places the dragged polygon without waiting.

## Tests

`GetPolygonTriangleCentroidPoints` is already covered (one point per triangle, inside the ring, unsmoothed vs smoothed) in [`AutoCirclePolygonizeTests.cs`](Clients/Viking/WebAnnotationTests/Commands/AutoCirclePolygonizeTests.cs). No new UI-command test. After the edit, `LocationActions.cs` should have no `ApproximateMedialAxisImproved` call on this path.
