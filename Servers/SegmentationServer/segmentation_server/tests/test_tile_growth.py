"""Tile growth tests. The predictor is a fake; no SAM2 weights."""

from __future__ import annotations

from typing import List, Sequence, Tuple

import numpy as np

from segmentation_server.mask_utils import mask_to_polygons
from segmentation_server.tile_growth import (
    SEED_INSET_PX,
    SeamGraph,
    TileIndex,
    grow_segmentation,
    paste_half_mask,
    stitch_half_tile,
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


def _reaching_right(size: int = SIZE) -> np.ndarray:
    """Mask that reaches the right edge and contains the half-tile inset point."""
    mask = np.zeros((size, size), dtype=np.bool_)
    mask[2:-2, size // 2 :] = True
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


def test_c_shape_returns_to_the_first_tile() -> None:
    """Two regions on one tile that meet only through the neighbor are both kept.

    The click is in the upper region. The first predict cannot see the lower one.
    Growth enters the neighbor, and the neighbor's far contact seeds the first tile again.
    """
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        if col == 0:
            if any(y < SIZE // 2 for _x, y in points):
                mask[2:8, 8:] = True
            if any(y >= 20 for _x, y in points):
                mask[24:30, 8:] = True
            return mask, None, 0.9
        mask[2:30, :8] = True
        return mask, None, 0.5

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(20, 27)],
        background=[],
        predict=predict,
        tile_size=SIZE,
        seed_spacing=4,
    )
    assert calls == [0, 1, 0]
    assert np.any(result.mask[2:8, 8:SIZE])
    assert np.any(result.mask[24:30, 8:SIZE])
    assert np.any(result.mask[2:30, SIZE:SIZE + 8])
    assert result.requested == []


def test_return_seed_skips_an_edge_the_mask_already_covers() -> None:
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        if col == 0:
            return _right_edge(), None, 0.8
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        # Same rows as _right_edge, so every return seed lands on mask already present.
        mask[2:-2, :2] = True
        return mask, None, 0.4

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        tile_size=SIZE,
    )
    assert calls == [0, 1]


def test_positive_logits_on_the_edge_do_not_request_a_neighbor() -> None:
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
    assert result.requested == []


def test_stitch_right_centers_the_cut() -> None:
    left = np.zeros((8, 8), dtype=np.uint8)
    right = np.zeros((8, 8), dtype=np.uint8)
    left[:, 4:] = 1
    right[:, :4] = 2
    stitched = stitch_half_tile(left, right, "right")
    assert np.all(stitched[:, :4] == 1)
    assert np.all(stitched[:, 4:] == 2)


def test_stitch_top_puts_the_upper_tile_above_the_cut() -> None:
    lower = np.zeros((8, 8), dtype=np.uint8)
    upper = np.zeros((8, 8), dtype=np.uint8)
    lower[:4] = 1
    upper[4:] = 2
    stitched = stitch_half_tile(lower, upper, "top")
    assert np.all(stitched[:4] == 2)
    assert np.all(stitched[4:] == 1)


def test_half_tile_replaces_the_edge_strip() -> None:
    calls: List[int] = []
    seams: List[Tuple[str, List[Point]]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        return _reaching_right(), None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append((side, list(points)))
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[14:18, 8:20] = True
        return mask, None, 0.7

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert calls == [0]
    assert len(seams) == 1
    assert seams[0][0] == "right"
    assert seams[0][1] == [(11, 15)]
    source = result.mask[:, :SIZE]
    assert np.count_nonzero(source[:, -1]) < SIZE // 2
    assert np.any(result.mask[:, SIZE:SIZE + 4])


def test_half_tile_requests_a_neighbor_that_is_not_uploaded() -> None:
    seams: List[str] = []

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(side)
        return np.zeros((SIZE, SIZE), dtype=np.bool_), None, 0.0

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0)],
        foreground=[(8, 16)],
        background=[],
        predict=lambda row, col, points, labels: (_reaching_right(), None, 0.8),
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == []
    assert result.requested == [TileIndex(0, 1)]


def test_shallow_edge_does_not_take_the_half_tile_step() -> None:
    seams: List[str] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[2:-2, -1] = True
        return mask, None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(side)
        return np.ones((SIZE, SIZE), dtype=np.bool_), None, 0.9

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == []
    assert result.requested == []
    assert np.count_nonzero(result.mask) == SIZE - 4


def test_full_edge_contact_prompts_one_point_per_run() -> None:
    seams: List[List[Point]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[2:8, SIZE // 2 :] = True
        mask[20:28, SIZE // 2 :] = True
        return mask, None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(list(points))
        return np.zeros((SIZE, SIZE), dtype=np.bool_), None, 0.0

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == [[(11, 4), (11, 23)]]


def test_full_frame_seam_mask_fills_the_range_and_does_not_return() -> None:
    seams: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        return _reaching_right(), None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(1)
        return np.ones((SIZE, SIZE), dtype=np.bool_), None, 0.9

    result = grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == [1]
    assert result.mask.shape[1] == SIZE * 2
    assert np.count_nonzero(result.mask[:, SIZE - 1]) == SIZE


def test_overlapping_contact_does_not_search_the_original_tile() -> None:
    seams: List[List[Point]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[2:8, SIZE // 2 :] = True
        return mask, None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(list(points))
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[4, 11:18] = True
        return mask, None, 0.6

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == [[(11, 4)]]


def test_disjoint_contact_searches_the_return() -> None:
    seams: List[List[Point]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[2:8, SIZE // 2 :] = True
        return mask, None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append((side, list(points)))
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        if len(seams) == 1:
            mask[4, 11:24] = True
            mask[4:23, 23] = True
            mask[22, 16:24] = True
            mask[20:25, 16] = True
        else:
            mask[22, 18:22] = True
        return mask, None, 0.6

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert [side for side, _points in seams] == ["right", "left"]
    assert seams[1][1][0] == (20, 22)


def test_one_cut_is_one_undirected_edge() -> None:
    graph = SeamGraph()
    graph.claim(TileIndex(0, 0), TileIndex(0, 1), [(2, 8)])
    graph.claim(TileIndex(0, 1), TileIndex(0, 0), [(6, 12), (20, 22)])
    assert list(graph.edges) == [(0, 0, 0, 1)]
    assert graph.ranges(TileIndex(0, 1), TileIndex(0, 0)) == [(2, 12), (20, 22)]


def test_full_edge_paste_records_the_whole_border() -> None:
    graph = SeamGraph()

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        return np.ones((SIZE, SIZE), dtype=np.bool_), None, 0.9

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=lambda row, col, points, labels: (_reaching_right(), None, 0.8),
        seam_predict=seam_predict,
        seam_graph=graph,
        tile_size=SIZE,
    )
    assert list(graph.edges) == [(0, 0, 0, 1)]
    assert graph.ranges(TileIndex(0, 0), TileIndex(0, 1)) == [(0, SIZE - 1)]


def test_remembered_range_skips_overlap_and_crosses_the_disjoint_run() -> None:
    graph = SeamGraph()
    graph.claim(TileIndex(0, 0), TileIndex(0, 1), [(2, 8)])
    seams: List[List[Point]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[2:9, SIZE // 2 :] = True
        mask[20:28, SIZE // 2 :] = True
        return mask, None, 0.8

    def seam_predict(
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        seams.append(list(points))
        return np.zeros((SIZE, SIZE), dtype=np.bool_), None, 0.0

    grow_segmentation(
        uploaded=[TileIndex(0, 0), TileIndex(0, 1)],
        foreground=[(8, 16)],
        background=[],
        predict=predict,
        seam_predict=seam_predict,
        seam_graph=graph,
        tile_size=SIZE,
    )
    assert seams == [[(11, 23)]]
    assert list(graph.edges) == [(0, 0, 0, 1)]
    assert graph.ranges(TileIndex(0, 0), TileIndex(0, 1)) == [(2, 8), (20, 27)]


def test_paste_right_drops_the_edge_strip() -> None:
    source = _right_edge()
    synthetic = np.zeros((SIZE, SIZE), dtype=np.bool_)
    synthetic[14:18, 12:20] = True
    pasted, neighbor = paste_half_mask(source, None, synthetic, "right", SIZE)
    assert np.count_nonzero(pasted[:, -1]) == 4
    assert np.any(neighbor[:, :4])
