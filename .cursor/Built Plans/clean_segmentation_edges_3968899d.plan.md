---
name: Clean Segmentation Edges
overview: Remove SAM2’s connected pixel-scale fringe before polygon tracing while preserving the raw server mask and real segmentation holes. Reduce response size and encoding work with 1-bit mask PNGs and by allowing Viking to omit the unused labeled image.
todos:
  - id: edge-cleanup-setting
    content: Add the configurable segmentation edge-cleanup radius preference.
    status: completed
  - id: mask-morphology
    content: Apply padded opening/closing before marching squares with safe fallback.
    status: completed
  - id: profile-cleanup
    content: Profile cleanup duration and mask pixel-count changes.
    status: completed
  - id: encode-one-bit-masks
    content: Encode cropped SAM2 masks as 1-bit grayscale PNGs.
    status: completed
  - id: omit-labeled-image
    content: Let Viking request omission of the unused full-frame labeled image.
    status: completed
  - id: validate-cleanup
    content: Add tests, build Viking, deploy SAM2, and assess quality and payload improvements.
    status: completed
isProject: false
---

# Clean SAM2 Mask Edges

## Recommended approach
- Add a segmentation preference for edge cleanup radius in screen pixels, defaulting to `2`; `0` disables cleanup. Wire it through [Settings.settings](Clients/Viking/WebAnnotation/Properties/Settings.settings), [Global.cs](Clients/Viking/WebAnnotation/Global.cs), and the Segmentation section of [AnnotationPreferencesDialog.xaml](Clients/WebAnnotationWPFControls/Forms/AnnotationPreferencesDialog.xaml).
- In [SegmentationMaskPolygonizer.cs](Clients/Viking/WebAnnotation/UI/Commands/Segmentation/SegmentationMaskPolygonizer.cs), pad the binary mask and apply morphological opening followed by closing before marching squares. Opening removes narrow connected wisps; closing removes similarly narrow inward notches. Keep the server response raw and binary.
- Continue existing hole classification, configurable hole dropping, and polygon simplification after cleanup. Validate the resulting exterior and holes before replacing the unfiltered polygon; fall back to the original mask result if cleanup produces invalid geometry.
- Extend `[SegmentationProfile]` output with mask-cleanup time and before/after foreground-pixel counts so aggressiveness and cost are measurable.

## SAM2 response optimization
- In [server.py](Servers/SegmentationServer/segmentation_server/segmentation_server/server.py), use Pillow to encode each cropped boolean mask as a true 1-bit grayscale PNG instead of OpenCV’s 8-bit grayscale PNG. Keep PNG dimensions embedded so the existing ImageSharp decoder and marching-squares input require no format-specific client branch.
- Add an `omit_labeled_image` option to `SegmentationRequest` and `MultiSegmentationRequest` in the canonical [segmentation.proto](gRPC_Protos/Segmentation/SAM2/segmentation.proto). Default it to `false` for backward compatibility; Viking sets it to `true`, while existing clients continue receiving the full-frame labeled image.
- Regenerate C# and Python protobuf outputs through the repository’s existing generation process. Do not hand-edit generated files.
- Skip full-frame labeled-image PNG construction when omission is requested. Extend profiling with mask encoding time, labeled-image encoding time, and total response bytes to verify that encoding changes improve end-to-end latency rather than only reducing nominal bit depth.

## Validation
- Add focused tests in [SegmentationMaskPolygonizerTests.cs](Clients/Viking/WebAnnotationTests/Commands/SegmentationMaskPolygonizerTests.cs) for outward wisps, inward notches, retained substantial holes, disabled cleanup, and valid/simple output.
- Add server tests confirming 1-bit PNG output decodes to the original boolean mask and labeled-image omission remains opt-in and backward compatible.
- Build Viking, rebuild and deploy the SAM2 Docker image in use, then compare raw versus cleaned boundaries and response sizes on the supplied example. If a 2-pixel cleanup radius removes legitimate thin structures, lower the default to 1; if broad low-confidence fringe remains, evaluate server-side SAM2 logit thresholding or tighter image crops as a separate change.