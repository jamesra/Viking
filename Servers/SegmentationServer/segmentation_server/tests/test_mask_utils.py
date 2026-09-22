"""Mask helper tests that require numpy, OpenCV, and Pillow only."""

from __future__ import annotations

import io

import numpy as np
import pytest
from PIL import Image

from segmentation_server.mask_utils import (
    cleanup_mask,
    combined_mask_to_segments,
    encode_png,
    fill_small_holes,
    get_mask_bounds,
    mask_contains_xy,
    mask_to_polygons,
    prepare_image_for_sam2,
    process_masks,
    union_masks_covering_positives,
)


def test_get_mask_bounds_empty() -> None:
    mask = np.zeros((4, 4), dtype=np.bool_)
    assert get_mask_bounds(mask) == (0, 0, 0, 0)


def test_get_mask_bounds_region() -> None:
    mask = np.zeros((10, 10), dtype=np.bool_)
    mask[2:5, 3:7] = True
    assert get_mask_bounds(mask) == (3, 2, 4, 3)


def test_cleanup_mask_keeps_largest_component() -> None:
    mask = np.zeros((20, 20), dtype=np.bool_)
    mask[1:3, 1:3] = True
    mask[8:18, 8:18] = True
    cleaned = cleanup_mask(mask)
    assert int(np.sum(cleaned)) == 100
    assert not cleaned[1, 1]


def test_mask_to_polygons_returns_closed_shape() -> None:
    mask = np.zeros((32, 32), dtype=np.bool_)
    mask[8:24, 8:24] = True
    polygons = mask_to_polygons(mask)
    assert len(polygons) >= 1
    assert polygons[0].shape[1] == 2
    assert len(polygons[0]) >= 3


def test_process_masks_empty_uses_provided_shape() -> None:
    masks = np.zeros((0, 0, 0), dtype=np.bool_)
    scores = np.array([], dtype=np.float32)
    labeled, segments = process_masks(masks, scores, empty_shape=(6, 8))
    assert labeled.shape == (6, 8)
    assert segments == []


def test_process_masks_skips_empty_and_labels_from_one() -> None:
    masks = np.zeros((2, 4, 4), dtype=np.bool_)
    masks[1, 1:3, 1:3] = True
    scores = np.array([0.1, 0.9], dtype=np.float32)
    labeled, segments = process_masks(masks, scores)
    assert labeled.dtype == np.uint16
    assert int(labeled.max()) == 2
    assert len(segments) == 1
    assert segments[0]["index"] == 1
    assert segments[0]["score"] == pytest.approx(0.9)


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


def test_union_or_keeps_covering_masks_and_drops_misses() -> None:
    masks = np.zeros((3, 10, 10), dtype=np.bool_)
    masks[0, 1:4, 1:4] = True
    masks[1, 6:9, 6:9] = True
    masks[2, 0:2, 8:10] = True
    scores = np.array([0.9, 0.8, 0.1], dtype=np.float32)
    union, stats = union_masks_covering_positives(
        masks,
        scores,
        coordinates=[(2, 2), (7, 7)],
        labels=[1, 1],
    )
    assert union[2, 2]
    assert union[7, 7]
    assert not union[0, 9]
    assert stats.score == pytest.approx(0.9)
    assert stats.n_initial_masks == 3
    assert stats.n_kept_initial == 2
    assert stats.n_merged == 2
    assert stats.n_uncovered == 0
    labeled, segments = combined_mask_to_segments(union, stats.score, empty_shape=(10, 10))
    assert len(segments) == 1
    assert int(labeled.max()) == 1
    assert segments[0]["width"] == 8
    assert segments[0]["height"] == 8


def test_union_skips_extra_predict_when_positives_already_covered() -> None:
    masks = np.zeros((1, 8, 8), dtype=np.bool_)
    masks[0, 1:6, 1:6] = True
    calls: list[tuple] = []

    def extra_predict(coords, labels):
        calls.append((list(coords), list(labels)))
        raise AssertionError("extra_predict should not run")

    union, _stats = union_masks_covering_positives(
        masks,
        np.array([0.7], dtype=np.float32),
        coordinates=[(2, 2), (4, 4), (0, 0)],
        labels=[1, 1, 0],
        extra_predict=extra_predict,
    )
    assert union[2, 2] and union[4, 4]
    assert calls == []


def test_union_extra_predict_uses_uncovered_click_and_shared_negatives() -> None:
    masks = np.zeros((1, 12, 12), dtype=np.bool_)
    masks[0, 1:4, 1:4] = True
    extra_mask = np.zeros((12, 12), dtype=np.bool_)
    extra_mask[8:11, 8:11] = True
    calls: list[tuple] = []

    def extra_predict(coords, labels):
        calls.append((list(coords), list(labels)))
        return extra_mask[np.newaxis, ...], np.array([0.55], dtype=np.float32)

    union, stats = union_masks_covering_positives(
        masks,
        np.array([0.4], dtype=np.float32),
        coordinates=[(2, 2), (9, 9), (0, 0)],
        labels=[1, 1, 0],
        extra_predict=extra_predict,
    )
    assert calls == [([(9, 9), (0, 0)], [1, 0])]
    assert union[2, 2] and union[9, 9]
    assert stats.score == pytest.approx(0.55)
    assert stats.n_kept_initial == 1
    assert stats.n_extra_predicts == 1
    assert stats.n_extra_kept == 1
    assert stats.n_merged == 2
    assert stats.n_uncovered == 0
    assert stats.area == 18


def test_union_drops_extra_mask_that_misses_positives() -> None:
    masks = np.zeros((1, 8, 8), dtype=np.bool_)
    extra_mask = np.zeros((1, 8, 8), dtype=np.bool_)
    extra_mask[0, 6:8, 6:8] = True

    def extra_predict(_coords, _labels):
        return extra_mask, np.array([0.99], dtype=np.float32)

    union, stats = union_masks_covering_positives(
        masks,
        np.array([0.2], dtype=np.float32),
        coordinates=[(1, 1)],
        labels=[1],
        extra_predict=extra_predict,
    )
    assert not np.any(union)
    assert stats.score == pytest.approx(0.0)
    assert stats.n_kept_initial == 0
    assert stats.n_extra_predicts == 1
    assert stats.n_extra_kept == 0
    assert stats.n_uncovered == 1
    assert stats.area == 0


def test_union_second_uncovered_click_skipped_if_first_extra_covers_it() -> None:
    masks = np.zeros((1, 16, 16), dtype=np.bool_)
    extra_mask = np.zeros((16, 16), dtype=np.bool_)
    extra_mask[4:12, 4:12] = True
    calls: list[tuple] = []

    def extra_predict(coords, labels):
        calls.append((list(coords), list(labels)))
        return extra_mask[np.newaxis, ...], np.array([0.6], dtype=np.float32)

    union, stats = union_masks_covering_positives(
        masks,
        np.array([0.1], dtype=np.float32),
        coordinates=[(5, 5), (10, 10), (0, 1)],
        labels=[1, 1, 0],
        extra_predict=extra_predict,
    )
    assert len(calls) == 1
    assert calls[0][0][0] == (5, 5)
    assert union[5, 5] and union[10, 10]
    assert stats.n_extra_predicts == 1
    assert stats.n_uncovered == 0


def test_union_stats_count_out_of_bounds_positives() -> None:
    masks = np.zeros((1, 4, 4), dtype=np.bool_)
    masks[0, 1:3, 1:3] = True
    union, stats = union_masks_covering_positives(
        masks,
        np.array([0.5], dtype=np.float32),
        coordinates=[(1, 1), (9, 9)],
        labels=[1, 1],
    )
    assert union[1, 1]
    assert stats.n_positives == 2
    assert stats.n_positives_oob == 1
    assert stats.n_uncovered == 0
    line = stats.log_line(4, 4)
    assert "oob=1" in line
    assert "merged=1" in line
    assert "area=4" in line
