# Built Plans

Archive of Cursor agent plans whose todos are fully completed. Active or in-progress plans stay in `.cursor/plans/` (this repo) or the Cursor plans directory.

Each linked plan file keeps its original frontmatter, overview, and implementation notes.

| Plan | Primary deliverables |
|------|---------------------|
| [auto_polygonize_mode_624b6b2f.plan.md](auto_polygonize_mode_624b6b2f.plan.md) | Preference-gated idle auto-polygonize of in-view circles; accept or dismiss proposals |
| [auto-segment_translate_refresh_606bfc54.plan.md](auto-segment_translate_refresh_606bfc54.plan.md) | Auto-polygonize cache keeps SAM2 image id so a translate can segment without a new upload |
| [clean_segmentation_edges_3968899d.plan.md](clean_segmentation_edges_3968899d.plan.md) | Strip SAM2 pixel fringe before polygon tracing; 1-bit mask PNGs |
| [coalesce_segmentation_requests_c1cbb0e6.plan.md](coalesce_segmentation_requests_c1cbb0e6.plan.md) | One follow-up SegmentImage after in-flight clicks; drop stale responses |
| [dev_identity_into_legacy_cf3bfda9.plan.md](dev_identity_into_legacy_cf3bfda9.plan.md) | Selective Identity and `Servers/SQL` import onto Legacy; symlink retirement |
| [guarantee_capture_tiles_c3785a90.plan.md](guarantee_capture_tiles_c3785a90.plan.md) | Capture only after a still camera and loaded tiles; no orphaned SAM2 uploads |
| [linked_polygon_placement_7d2a7b63.plan.md](linked_polygon_placement_7d2a7b63.plan.md) | Direct polygon copy without a segmentation service; Escape keeps the dragged polygon |
| [multi-file_bookmarks_ce2afbd5.plan.md](multi-file_bookmarks_ce2afbd5.plan.md) | Bookmark file drop, Local-first roots, and per-document save |
| [off-ui_auto-seg_0c2460e1.plan.md](off-ui_auto-seg_0c2460e1.plan.md) | UI-thread capture; background encode and polygonize |
| [pen_mode_ui_bugs_e925c3fe.plan.md](pen_mode_ui_bugs_e925c3fe.plan.md) | Unmodified Pen Mode stays free-draw; unimplemented pen actions no longer throw |
| [polygon_negative_prompts_c3c6bb71.plan.md](polygon_negative_prompts_c3c6bb71.plan.md) | Negative SAM prompts from interior-triangle centroids |
| [pull_geometry_from_dev_7b68a2c2.plan.md](pull_geometry_from_dev_7b68a2c2.plan.md) | Geometry.Core and Geometry tree from `dev` @ e62c6546 onto Legacy |
| [reuse_viking_instance_469ca75c.plan.md](reuse_viking_instance_469ca75c.plan.md) | Same-volume `viking://` links forward to the running process |
| [sbfsem_viking_deep_links_1926f475.plan.md](sbfsem_viking_deep_links_1926f475.plan.md) | SBFSEM context menu and `viking://` place (Location ID or x,y,z) |
| [screen_area_cutoff_prefs_ef6115cd.plan.md](screen_area_cutoff_prefs_ef6115cd.plan.md) | Auto-polygonize minimum size as a percent of screen area |
| [segment_nearest_first_bcb8a012.plan.md](segment_nearest_first_bcb8a012.plan.md) | Auto-polygonize processes circles nearest the view center first |
| [segmentation_back_button_a09722ae.plan.md](segmentation_back_button_a09722ae.plan.md) | Segmentation Back button and mouse XButton1 cancel |
| [segmentation_polygon_quality_5e091725.plan.md](segmentation_polygon_quality_5e091725.plan.md) | Client marching-squares polygonize of the SAM2 mask, with holes |
| [sibling_polygon_resubmit_7226721a.plan.md](sibling_polygon_resubmit_7226721a.plan.md) | Overlapping same-cell proposals resubmit once and keep one location |
| [smooth_deep_link_auth_6c58487a.plan.md](smooth_deep_link_auth_6c58487a.plan.md) | Volume-scoped launch token into Viking; Identity bounce back to SBFSEM |

## Plans still open (not archived)

- [viking_ux_pen_seg_improvement.plan.md](../plans/viking_ux_pen_seg_improvement.plan.md) — consolidated pen / segmentation / annotation UX plan from 8-chunk review (P0–P4)
- [viking-client-maintenance-review.plan.md](../plans/viking-client-maintenance-review.plan.md) — safe ranks 1–22 are done; moderate and risky ranks remain
- `viking_client_chunk_review_0b040014.plan.md` — still in the Cursor plans directory; chunk review loop is not finished
- Repo-root proposals `async-odataspatialdatafactory-creation.plan.md` and `async-odatamorphologyfactory-optimization.plan.md` — checklists are still unchecked
