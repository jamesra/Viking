"""Mask helper tests that require numpy, OpenCV, and Pillow only."""

from __future__ import annotations

import io

import numpy as np
import pytest
from PIL import Image

from hypothesis import given, settings, strategies as st

from segmentation_server.mask_utils import (
    MIN_BOX_COVERAGE,
    NoMatchingMask,
    box_coverage,
    count_covered_points,
    encode_png,
    ensure_image_within_limit,
    fill_small_holes,
    get_mask_bounds,
    mask_contains_xy,
    mask_covers_box,
    mask_to_polygons,
    max_image_pixels,
    prepare_image_for_sam2,
    process_masks,
    select_mask,
)


def test_get_mask_bounds_empty() -> None:
    mask = np.zeros((4, 4), dtype=np.bool_)
    assert get_mask_bounds(mask) == (0, 0, 0, 0)


def test_get_mask_bounds_region() -> None:
    mask = np.zeros((10, 10), dtype=np.bool_)
    mask[2:5, 3:7] = True
    assert get_mask_bounds(mask) == (3, 2, 4, 3)


def test_mask_to_polygons_returns_closed_shape() -> None:
    mask = np.zeros((32, 32), dtype=np.bool_)
    mask[8:24, 8:24] = True
    polygons = mask_to_polygons(mask)
    assert len(polygons) >= 1
    assert polygons[0].outer.shape[1] == 2
    assert len(polygons[0].outer) >= 3
    assert polygons[0].holes == []


def test_process_masks_empty_uses_provided_shape() -> None:
    masks = np.zeros((0, 0, 0), dtype=np.bool_)
    scores = np.array([], dtype=np.float32)
    labeled, segments = process_masks(masks, scores, empty_shape=(6, 8))
    assert labeled.shape == (6, 8)
    assert segments == []


def test_process_masks_skips_empty_and_labels_are_dense_from_one() -> None:
    masks = np.zeros((2, 4, 4), dtype=np.bool_)
    masks[1, 1:3, 1:3] = True
    scores = np.array([0.1, 0.9], dtype=np.float32)
    labeled, segments = process_masks(masks, scores)
    assert labeled.dtype == np.uint16
    assert int(labeled.max()) == 1
    assert len(segments) == 1
    assert segments[0]["index"] == 0
    assert segments[0]["score"] == pytest.approx(0.9)
    assert int(labeled[1, 1]) == segments[0]["index"] + 1


def test_process_masks_labels_stay_dense_across_empty_masks_in_the_middle() -> None:
    masks = np.zeros((4, 8, 8), dtype=np.bool_)
    masks[0, 0:2, 0:2] = True
    masks[2, 4:6, 4:6] = True
    masks[3, 6:8, 0:2] = True
    labeled, segments = process_masks(masks, np.array([0.9, 0.8, 0.7, 0.6], dtype=np.float32))
    assert [segment["index"] for segment in segments] == [0, 1, 2]
    assert sorted(int(v) for v in np.unique(labeled) if v) == [1, 2, 3]


def test_encode_png_roundtrip_uint8() -> None:
    array = np.zeros((8, 8), dtype=np.uint8)
    array[2:6, 2:6] = 255
    encoded = encode_png(array)
    assert encoded[:8] == b"\x89PNG\r\n\x1a\n"


def test_prepare_image_for_sam2_rgb() -> None:
    image = Image.new("RGB", (5, 7), color=(10, 20, 30))
    buf = io.BytesIO()
    image.save(buf, format="PNG")
    array = prepare_image_for_sam2(buf.getvalue())
    assert array.shape == (7, 5, 3)
    assert tuple(array[0, 0]) == (10, 20, 30)


def test_prepare_image_for_sam2_rejects_garbage() -> None:
    with pytest.raises(OSError):
        prepare_image_for_sam2(b"not-an-image")


def test_fill_small_holes_keeps_disconnected_blobs() -> None:
    mask = np.zeros((20, 20), dtype=np.bool_)
    mask[1:4, 1:4] = True
    mask[12:18, 12:18] = True
    filled = fill_small_holes(mask)
    assert filled[2, 2]
    assert filled[15, 15]
    assert int(np.sum(filled)) == int(np.sum(mask))


def test_fill_small_holes_leaves_solid_blob() -> None:
    mask = np.zeros((12, 12), dtype=np.bool_)
    mask[2:10, 2:10] = True
    filled = fill_small_holes(mask)
    assert np.array_equal(filled, mask)


def test_mask_contains_xy_bounds_and_value() -> None:
    mask = np.zeros((4, 5), dtype=np.bool_)
    mask[2, 3] = True
    assert mask_contains_xy(mask, 3, 2)
    assert not mask_contains_xy(mask, 0, 0)
    assert not mask_contains_xy(mask, 5, 0)
    assert not mask_contains_xy(mask, 0, 4)


def _ring(size: int, outer: int, inner: int) -> np.ndarray:
    mask = np.zeros((size, size), dtype=np.bool_)
    mask[outer:size - outer, outer:size - outer] = True
    mask[inner:size - inner, inner:size - inner] = False
    return mask


def test_fill_small_holes_counts_the_hole_in_pixels_not_by_contour_area() -> None:
    mask = np.zeros((40, 40), dtype=np.bool_)
    mask[5:35, 5:35] = True
    mask[15:25, 15:25] = False  # 100 hole pixels in 800 mask pixels = 12.5%
    assert not fill_small_holes(mask, hole_threshold=0.10)[20, 20]
    assert fill_small_holes(mask, hole_threshold=0.15)[20, 20]
    assert int(fill_small_holes(mask, hole_threshold=0.15).sum()) == 900


def test_fill_small_holes_never_fills_background_that_touches_the_border() -> None:
    mask = np.zeros((20, 20), dtype=np.bool_)
    mask[0:10, 0:20] = True
    mask[0:4, 8:12] = False  # a notch open to the border, not a hole
    assert np.array_equal(fill_small_holes(mask, hole_threshold=0.9), mask)


def test_a_donut_polygon_carries_its_hole() -> None:
    shapes = mask_to_polygons(_ring(64, 8, 24))
    assert len(shapes) == 1
    assert len(shapes[0].holes) == 1
    outer_x = shapes[0].outer[:, 0]
    hole_x = shapes[0].holes[0][:, 0]
    assert outer_x.min() < hole_x.min() and hole_x.max() < outer_x.max()
    assert len(shapes[0].holes[0]) >= 3


def test_each_piece_keeps_only_its_own_holes() -> None:
    mask = np.zeros((80, 80), dtype=np.bool_)
    mask[4:36, 4:36] = True
    mask[14:26, 14:26] = False  # hole in piece A
    mask[44:76, 44:76] = True  # solid piece B
    shapes = mask_to_polygons(mask)
    assert sorted(len(shape.holes) for shape in shapes) == [0, 1]
    with_hole = next(shape for shape in shapes if shape.holes)
    assert with_hole.outer[:, 0].max() < 40


def test_an_island_inside_a_hole_is_its_own_outer_ring() -> None:
    mask = _ring(80, 4, 20)  # a ring whose hole spans 20..59
    mask[36:44, 36:44] = True  # island in the middle of the hole
    shapes = mask_to_polygons(mask)
    assert sorted(len(shape.holes) for shape in shapes) == [0, 1]


def test_an_empty_mask_has_no_polygons() -> None:
    assert mask_to_polygons(np.zeros((8, 8), dtype=np.bool_)) == []


def _candidates(*boxes: tuple[int, int, int, int], size: int = 20) -> np.ndarray:
    masks = np.zeros((len(boxes), size, size), dtype=np.bool_)
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        masks[i, y0:y1 + 1, x0:x1 + 1] = True
    return masks


def test_mask_covers_box_needs_every_pixel_and_a_box_inside_the_mask() -> None:
    mask = _candidates((2, 2, 10, 10))[0]
    assert mask_covers_box(mask, (3, 3, 9, 9))
    assert mask_covers_box(mask, (2, 2, 10, 10))
    assert not mask_covers_box(mask, (1, 3, 9, 9))
    assert not mask_covers_box(mask, (3, 3, 11, 9))
    assert not mask_covers_box(mask, (5, 5, 25, 25))
    assert not mask_covers_box(mask, (-1, 3, 9, 9))
    assert not mask_covers_box(mask, (9, 9, 3, 3))


def test_with_a_box_the_highest_scoring_mask_that_covers_all_of_it_wins() -> None:
    masks = _candidates((0, 0, 19, 19), (2, 2, 14, 14), (5, 5, 8, 8))
    scores = np.array([0.5, 0.9, 0.99], dtype=np.float32)
    assert select_mask(masks, scores, box=(4, 4, 10, 10)) == 1  # the 0.99 mask is smaller than the box
    assert select_mask(masks, scores, box=(6, 6, 7, 7)) == 2


def test_a_mask_that_is_only_the_rectangle_does_not_answer_it() -> None:
    masks = _candidates((4, 4, 10, 10), (2, 2, 14, 14))
    scores = np.array([0.99, 0.6], dtype=np.float32)

    assert select_mask(masks, scores, box=(4, 4, 10, 10)) == 1  # the 0.99 mask is exactly the box

    with pytest.raises(NoMatchingMask, match="extends past"):
        select_mask(masks[:1], scores[:1], box=(4, 4, 10, 10))


def test_a_mask_one_pixel_larger_than_the_rectangle_is_enough() -> None:
    masks = _candidates((4, 4, 11, 10))

    assert select_mask(masks, np.array([0.8], dtype=np.float32), box=(4, 4, 10, 10)) == 0


def test_mask_grows_past_box_needs_cover_and_extra_area() -> None:
    from segmentation_server.mask_utils import mask_grows_past_box

    mask = _candidates((2, 2, 10, 10))[0]
    assert mask_grows_past_box(mask, (3, 3, 9, 9))
    assert not mask_grows_past_box(mask, (2, 2, 10, 10))  # the box itself
    assert not mask_grows_past_box(mask, (-10, -10, 10, 10))  # mostly outside the mask


def test_box_coverage_counts_the_share_of_box_pixels_set() -> None:
    mask = _candidates((2, 2, 10, 10))[0]
    assert box_coverage(mask, (3, 3, 9, 9)) == 1.0
    assert box_coverage(mask, (1, 3, 9, 9)) == pytest.approx(56 / 63)  # x 2..9 of the 9 columns are set
    assert box_coverage(mask, (9, 9, 3, 3)) == 0.0
    assert box_coverage(mask, (30, 30, 40, 40)) == 0.0
    # Outside the frame counts as not set: half of this box is off the 32x32 mask.
    assert box_coverage(np.ones((32, 32), dtype=np.bool_), (24, 0, 39, 15)) == pytest.approx(0.5)


def _grid_mask_with_holes(box_size: int, holes: int, margin: int = 4) -> np.ndarray:
    """A mask covering a ``box_size`` square plus ``margin`` all round, with ``holes`` box pixels cleared."""
    size = box_size + 2 * margin + 2
    mask = np.zeros((size, size), dtype=np.bool_)
    mask[1:size - 1, 1:size - 1] = True
    cleared = 0
    for y in range(margin + 1, margin + 1 + box_size):
        for x in range(margin + 1, margin + 1 + box_size):
            if cleared < holes:
                mask[y, x] = False
                cleared += 1
    return mask


def test_a_mask_with_few_holes_inside_the_box_answers_it_and_one_with_many_does_not() -> None:
    from segmentation_server.mask_utils import mask_grows_past_box

    box_size = 20
    box = (5, 5, 5 + box_size - 1, 5 + box_size - 1)
    area = box_size * box_size
    allowed = int(area * (1 - MIN_BOX_COVERAGE))

    assert mask_grows_past_box(_grid_mask_with_holes(box_size, allowed), box)
    assert not mask_grows_past_box(_grid_mask_with_holes(box_size, allowed + 1), box)


def test_select_mask_prefers_the_best_score_among_masks_above_the_coverage_threshold() -> None:
    box_size = 20
    box = (5, 5, 24, 24)
    near_miss = _grid_mask_with_holes(box_size, 10)  # 97.5% covered
    too_holey = _grid_mask_with_holes(box_size, 60)  # 85% covered
    whole = _grid_mask_with_holes(box_size, 0)
    masks = np.stack([too_holey, near_miss, whole])

    scores = np.array([0.99, 0.8, 0.7], dtype=np.float32)
    assert select_mask(masks, scores, box=box) == 1

    scores = np.array([0.99, 0.6, 0.7], dtype=np.float32)
    assert select_mask(masks, scores, box=box) == 2

    with pytest.raises(NoMatchingMask, match="95%"):
        select_mask(masks[:1], scores[:1], box=box)


def test_with_a_box_no_covering_mask_is_no_match_even_if_a_point_is_covered() -> None:
    masks = _candidates((5, 5, 8, 8))
    with pytest.raises(NoMatchingMask, match="starting rectangle"):
        select_mask(masks, np.array([0.9], dtype=np.float32), box=(0, 0, 10, 10), positives=[(6, 6)])


def test_without_a_box_the_mask_covering_the_most_points_wins_then_score() -> None:
    masks = _candidates((0, 0, 9, 9), (0, 0, 19, 19), (0, 0, 19, 19))
    scores = np.array([0.99, 0.5, 0.7], dtype=np.float32)
    points = [(2, 2), (15, 15), (16, 3)]
    assert select_mask(masks, scores, positives=points) == 2  # covers 3; ties go to the higher score
    assert select_mask(masks, scores, positives=[(2, 2)]) == 0  # all three cover one point; best score
    assert count_covered_points(masks[0], points) == 1


def test_without_a_box_a_mask_must_cover_at_least_one_point() -> None:
    masks = _candidates((0, 0, 4, 4))
    with pytest.raises(NoMatchingMask, match="foreground point"):
        select_mask(masks, np.array([0.9], dtype=np.float32), positives=[(15, 15)])
    with pytest.raises(NoMatchingMask):
        select_mask(masks, np.array([0.9], dtype=np.float32), positives=[])


def test_no_candidates_is_no_match() -> None:
    with pytest.raises(NoMatchingMask, match="no candidate"):
        select_mask(np.zeros((0, 4, 4), dtype=np.bool_), np.array([], dtype=np.float32), positives=[(1, 1)])


@settings(max_examples=60, deadline=None)
@given(
    st.lists(st.tuples(st.integers(0, 12), st.integers(0, 12), st.integers(0, 7), st.integers(0, 7)), min_size=1, max_size=5),
    st.lists(st.floats(0.0, 1.0, width=32), min_size=5, max_size=5),
    st.lists(st.tuples(st.integers(0, 19), st.integers(0, 19)), min_size=1, max_size=4),
)
def test_the_chosen_mask_always_satisfies_the_rule_and_is_never_beaten(rects, raw_scores, points) -> None:
    masks = _candidates(*[(x, y, x + w, y + h) for x, y, w, h in rects])
    scores = np.array(raw_scores[: len(rects)], dtype=np.float32)
    try:
        chosen = select_mask(masks, scores, positives=points)
    except NoMatchingMask:
        assert all(count_covered_points(mask, points) == 0 for mask in masks)
        return
    best = max(count_covered_points(mask, points) for mask in masks)
    assert count_covered_points(masks[chosen], points) == best >= 1
    assert all(
        scores[chosen] >= scores[i]
        for i in range(len(masks))
        if count_covered_points(masks[i], points) == best
    )


def test_the_declared_size_is_checked_before_pixels_are_decoded(monkeypatch) -> None:
    monkeypatch.setenv("SEGMENTATION_MAX_IMAGE_PIXELS", "50")
    buf = io.BytesIO()
    Image.new("RGB", (10, 10)).save(buf, format="PNG")
    with pytest.raises(ValueError, match="limit"):
        prepare_image_for_sam2(buf.getvalue())
    with pytest.raises(ValueError):
        ensure_image_within_limit(Image.open(io.BytesIO(buf.getvalue())))
    monkeypatch.setenv("SEGMENTATION_MAX_IMAGE_PIXELS", "100")
    assert prepare_image_for_sam2(buf.getvalue()).shape == (10, 10, 3)


def test_the_pixel_limit_default_and_bad_values(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_MAX_IMAGE_PIXELS", raising=False)
    assert max_image_pixels() == 100_000_000
    monkeypatch.setenv("SEGMENTATION_MAX_IMAGE_PIXELS", "lots")
    assert max_image_pixels() == 100_000_000
