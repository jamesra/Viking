"""Mask helper tests that require numpy, OpenCV, and Pillow only."""

from __future__ import annotations

import io

import numpy as np
import pytest
from PIL import Image

from segmentation_server.mask_utils import (
    cleanup_mask,
    encode_png,
    get_mask_bounds,
    mask_to_polygons,
    prepare_image_for_sam2,
    process_masks,
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
