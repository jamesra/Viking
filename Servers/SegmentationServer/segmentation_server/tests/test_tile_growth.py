"""Tile growth tests. The predictor is a fake; no SAM2 weights."""

from __future__ import annotations

from typing import List, Sequence, Tuple

import numpy as np

from segmentation_server.mask_utils import mask_to_polygons
from segmentation_server.tile_growth import (
    SEED_INSET_PX,
    TileIndex,
    grow_segmentation,
)

Point = Tuple[int, int]
SIZE = 32


def _interior(size: int = SIZE) -> np.ndarray:
    mask = np.zeros((size, size), dtype=np.bool_)
    mask[10:20, 10:20] = True
    return mask


def _right_edge(size: int = SIZE) -> np.ndarray:
    mask = np.zeros((size, size), dtype=np.bool_)
    mask[2:-2, -2:] = True
    return mask


def test_contained_mask_does_not_request_tiles() -> None:
    calls: List[TileIndex] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(TileIndex(row, col))
        return _interior(), None, 0.9

    result = grow_segmentation(
        uploaded=[TileIndex(1, 2)],
        foreground=[(2 * SIZE + 15, SIZE + 15)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert calls == [TileIndex(1, 2)]
    assert result.requested == []
    assert result.origin_x == 2 * SIZE
    assert result.origin_y == SIZE
    assert result.score == 0.9
    assert len(mask_to_polygons(result.mask)) == 1


def test_right_edge_seeds_uploaded_neighbor() -> None:
    seen: List[Tuple[int, List[Point]]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        seen.append((col, list(points)))
        if col == 0:
            return _right_edge(), None, 0.8
        return _interior(), None, 0.4

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert [col for col, _points in seen] == [0, 1]
    neighbor_points = seen[1][1]
    assert any(x == SEED_INSET_PX for x, _y in neighbor_points)
    assert all(label == 1 for label in [1] * len(neighbor_points))
    assert result.requested == []
    assert result.score == 0.4
    assert np.any(result.mask[:, :SIZE])
    assert np.any(result.mask[:, SIZE:])


def test_missing_neighbor_returns_partial_and_requests_that_cell() -> None:
    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        return _right_edge(), None, 0.7

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert result.requested == [TileIndex(0, 1)]
    assert np.count_nonzero(result.mask) > 0
    assert result.mask.shape == (SIZE, SIZE)


def test_request_cap_stops_extra_directions() -> None:
    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.ones((SIZE, SIZE), dtype=np.bool_)
        return mask, None, 0.5

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        max_requested=2,
        tile_size=SIZE,
    )
    assert len(result.requested) == 2
    assert np.count_nonzero(result.mask) == SIZE * SIZE


def test_two_tiles_fuse_to_one_polygon() -> None:
    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        if col == 0:
            mask[10:22, 20:] = True
            return mask, None, 0.9
        mask[10:22, :12] = True
        return mask, None, 0.3

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(15, 16), (SIZE + 4, 16)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert result.requested == []
    assert result.mask.shape == (SIZE, SIZE * 2)
    assert result.score == 0.3
    assert len(mask_to_polygons(result.mask)) == 1


def test_mosaic_y_up_maps_to_image_y_down() -> None:
    from segmentation_server.tile_growth import mosaic_to_image

    assert mosaic_to_image(0, 0, TileIndex(0, 0), SIZE) == (0, SIZE - 1)
    assert mosaic_to_image(0, SIZE - 1, TileIndex(0, 0), SIZE) == (0, 0)


def test_positive_logits_on_the_edge_request_a_neighbor() -> None:
    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        logits = np.zeros((SIZE, SIZE), dtype=np.float32)
        logits[2:-2, -1] = 1.0
        return mask, logits, 0.2

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert result.requested == [TileIndex(0, 1)]
