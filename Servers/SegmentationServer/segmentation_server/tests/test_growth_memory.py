"""Growth memory keeps omitted cells in the composite and skips repeated SAM2 calls."""

from __future__ import annotations

from typing import List, Sequence, Tuple

import numpy as np

from segmentation_server.growth_memory import (
    MASK_MEMORY_CAP_BYTES,
    GrowthMemory,
    grow_remembering,
    prompt_key,
)
from segmentation_server.tile_growth import GrowthCancelled, PredictUnavailable, SeamGraph, TileIndex, TilePrediction

Point = Tuple[int, int]
SIZE = 32


def _session(foreground: Sequence[Point], background: Sequence[Point] = ()) -> tuple:
    return prompt_key("RC2", 1, "TEM", "grid", 1, False, foreground, background)


def _left_edge() -> np.ndarray:
    mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
    mask[2:-2, :2] = True
    return mask


def _right_fill() -> np.ndarray:
    mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
    mask[2:-2, -10:] = True
    return mask


def _right_edge() -> np.ndarray:
    mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
    mask[2:-2, -2:] = True
    return mask


def _reaching_right() -> np.ndarray:
    """Reaches the right edge and contains the half-tile inset point."""
    mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
    mask[2:-2, SIZE // 2 :] = True
    return mask


def _interior() -> np.ndarray:
    mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
    mask[10:20, 10:20] = True
    return mask


def test_omitted_cell_stays_in_the_composite() -> None:
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        if col == 1:
            return _left_edge(), None, 0.9
        return _right_fill(), None, 0.4

    memory = GrowthMemory()
    session = _session([(SIZE + 8, 16)])
    click = TileIndex(0, 1)
    left = TileIndex(0, 0)
    first = grow_remembering(
        memory,
        session,
        [click, left],
        [(SIZE + 8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [1, 0]
    assert first.result.requested == []

    second = grow_remembering(
        memory,
        session,
        [click],
        [(SIZE + 8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [1, 0]
    assert second.predicted == 0
    assert left not in second.result.requested
    assert second.result.mask.shape == (SIZE, SIZE * 2)
    assert np.any(second.result.mask[:, :SIZE])
    assert np.any(second.result.mask[:, SIZE:SIZE + 2])


def test_new_neighbor_is_predicted_once() -> None:
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        if col == 0:
            return _right_edge(), None, 0.8
        return _interior(), None, 0.4

    memory = GrowthMemory()
    session = _session([(8, 16)])
    first = grow_remembering(
        memory,
        session,
        [TileIndex(0, 0)],
        [(8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [0]
    assert first.result.requested == [TileIndex(0, 1)]

    second = grow_remembering(
        memory,
        session,
        [TileIndex(0, 0), TileIndex(0, 1)],
        [(8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [0, 1]
    assert second.predicted == 1
    assert second.reused == 0
    assert np.any(second.result.mask[:, SIZE:])


def test_different_click_does_not_reuse() -> None:
    calls: List[Point] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(points[0])
        return _interior(), None, 0.9

    memory = GrowthMemory()
    grow_remembering(
        memory,
        _session([(8, 16)]),
        [TileIndex(0, 0)],
        [(8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    grow_remembering(
        memory,
        _session([(10, 16)]),
        [TileIndex(0, 0)],
        [(10, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert len(calls) == 2


def test_prompt_order_is_the_same_session() -> None:
    foreground = [(8, 16), (12, 18)]
    background = [(4, 4), (6, 6)]
    forward = prompt_key("RC2", 1, "TEM", "grid", 1, False, foreground, background)
    backward = prompt_key("RC2", 1, "TEM", "grid", 1, False, list(reversed(foreground)), list(reversed(background)))
    assert forward == backward
    other_click = prompt_key("RC2", 1, "TEM", "grid", 1, False, [(9, 16)], background)
    assert other_click != forward


def test_repeated_reseed_hits_the_cache() -> None:
    calls: List[Tuple[int, List[Point], List[int]]] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append((col, list(points), list(labels)))
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        if col == 0:
            if any(y < SIZE // 2 for _x, y in points):
                mask[2:8, 8:] = True
            if any(y >= 20 for _x, y in points):
                mask[24:30, 8:] = True
            return mask, None, 0.9
        mask[2:30, :8] = True
        return mask, None, 0.5

    memory = GrowthMemory()
    session = _session([(20, 27)])
    uploaded = [TileIndex(0, 0), TileIndex(0, 1)]
    first = grow_remembering(
        memory,
        session,
        uploaded,
        [(20, 27)],
        [],
        predict,
        tile_size=SIZE,
        seed_spacing=4,
    )
    assert [col for col, _points, _labels in calls] == [0, 1, 0]
    assert np.any(first.result.mask[24:30, 8:SIZE])

    _col, reseed_points, reseed_labels = calls[2]
    cached, _logits, _score = memory.lookup(session, 0, 0, reseed_points, reseed_labels)
    original = bool(cached[0, 0])
    cached[0, 0] = not original
    again, _, _ = memory.lookup(session, 0, 0, reseed_points, reseed_labels)
    assert bool(again[0, 0]) is original

    memory.remember_tiles(session, {})
    second = grow_remembering(
        memory,
        session,
        uploaded,
        [(20, 27)],
        [],
        predict,
        tile_size=SIZE,
        seed_spacing=4,
    )
    assert [col for col, _points, _labels in calls] == [0, 1, 0]
    assert second.reused == 3
    assert second.predicted == 0
    assert np.any(second.result.mask[2:8, 8:SIZE])
    assert np.any(second.result.mask[24:30, 8:SIZE])
    assert np.any(second.result.mask[2:30, SIZE:SIZE + 8])


def test_over_cap_drops_the_least_recently_used_session() -> None:
    assert MASK_MEMORY_CAP_BYTES == 2 * 1024 * 1024 * 1024
    memory = GrowthMemory(max_bytes=1500)

    def store(mark: int) -> tuple:
        key = _session([(mark, 0)])
        mask = np.zeros(600, dtype=np.bool_)
        memory.remember_tiles(key, {TileIndex(0, 0): TilePrediction(0, 0, mask, None, 1.0)})
        return key

    oldest = store(1)
    middle = store(2)
    assert memory.tiles(oldest)
    newest = store(3)
    assert memory.tiles(middle) == {}
    assert TileIndex(0, 0) in memory.tiles(oldest)
    assert TileIndex(0, 0) in memory.tiles(newest)


def test_predict_cache_counts_toward_the_cap() -> None:
    memory = GrowthMemory(max_bytes=1000)
    older = _session([(1, 0)])
    newer = _session([(2, 0)])
    mask = np.zeros(800, dtype=np.bool_)
    memory.store_predict(older, 0, 0, [(1, 1)], [1], mask, None, 0.2)
    memory.remember_tiles(newer, {TileIndex(0, 0): TilePrediction(0, 0, mask, None, 0.2)})
    assert memory.lookup(older, 0, 0, [(1, 1)], [1]) is None
    assert TileIndex(0, 0) in memory.tiles(newer)


def test_session_larger_than_the_cap_is_dropped() -> None:
    memory = GrowthMemory(max_bytes=100)
    key = _session([(1, 0)])
    mask = np.zeros(500, dtype=np.bool_)
    memory.remember_tiles(key, {TileIndex(0, 0): TilePrediction(0, 0, mask, None, 1.0)})
    assert memory.tiles(key) == {}


def test_replaced_tile_bytes_drop_its_mask() -> None:
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        if col == 1:
            return _left_edge(), None, 0.9
        return _right_fill(), None, 0.4

    memory = GrowthMemory()
    foreground = [(SIZE + 8, 16)]
    session = _session(foreground)
    grow_remembering(
        memory,
        session,
        [TileIndex(0, 1), TileIndex(0, 0)],
        foreground,
        [],
        predict,
        tile_size=SIZE,
    )
    memory.invalidate_tile("RC2", 1, "TEM", "grid", 1, 0, 0)
    omitted = grow_remembering(
        memory,
        session,
        [TileIndex(0, 1)],
        foreground,
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [1, 0]
    assert omitted.result.mask.shape[1] == SIZE
    assert TileIndex(0, 0) in omitted.result.requested

    both = grow_remembering(
        memory,
        session,
        [TileIndex(0, 1), TileIndex(0, 0)],
        foreground,
        [],
        predict,
        tile_size=SIZE,
    )
    assert calls == [1, 0, 0]
    assert both.predicted == 1
    assert np.any(both.result.mask[:, :SIZE])


def test_missing_predictor_requests_the_cell_and_keeps_the_mask() -> None:
    memory = GrowthMemory()
    session = _session([(8, 16)])
    memory.remember_tiles(
        session,
        {TileIndex(0, 0): TilePrediction(0, 0, _right_edge(), None, 0.8)},
    )

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        raise PredictUnavailable("embedding evicted")

    outcome = grow_remembering(
        memory,
        session,
        [TileIndex(0, 0), TileIndex(0, 1)],
        [(8, 16)],
        [],
        predict,
        tile_size=SIZE,
    )
    assert outcome.predicted == 0
    assert TileIndex(0, 1) in outcome.result.requested
    assert np.any(outcome.result.mask)


def test_cancelled_walk_does_not_call_sam() -> None:
    calls: List[int] = []

    def predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        calls.append(col)
        return _right_fill(), None, 0.5

    try:
        grow_remembering(
            GrowthMemory(),
            _session([(8, 16)]),
            [TileIndex(0, 0)],
            [(8, 16)],
            [],
            predict,
            should_stop=lambda: True,
            tile_size=SIZE,
        )
    except GrowthCancelled:
        assert calls == []
        return

    raise AssertionError("expected GrowthCancelled before SAM2")


def test_half_tile_is_not_encoded_twice() -> None:
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
        mask = np.zeros((SIZE, SIZE), dtype=np.bool_)
        mask[14:18, 8:20] = True
        return mask, None, 0.6

    memory = GrowthMemory()
    session = _session([(8, 16)])
    grow_remembering(
        memory,
        session,
        [TileIndex(0, 0), TileIndex(0, 1)],
        [(8, 16)],
        [],
        predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    # The pasted contact is stored on the seam graph, so the next call does not
    # search this cut again.
    second = grow_remembering(
        memory,
        session,
        [TileIndex(0, 0), TileIndex(0, 1)],
        [(8, 16)],
        [],
        predict,
        seam_predict=seam_predict,
        tile_size=SIZE,
    )
    assert seams == [1]
    assert second.predicted == 0
    assert len(memory.seam_graph(session).edges) == 1


def test_second_call_keeps_a_filled_border_and_does_not_reseam_it() -> None:
    seams: List[int] = []

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

    memory = GrowthMemory()
    session = _session([(8, 16)])
    for _call in range(2):
        grow_remembering(
            memory,
            session,
            [TileIndex(0, 0), TileIndex(0, 1)],
            [(8, 16)],
            [],
            lambda row, col, points, labels: (_reaching_right(), None, 0.8),
            seam_predict=seam_predict,
            tile_size=SIZE,
        )
    graph = memory.seam_graph(session)
    assert isinstance(graph, SeamGraph)
    assert seams == [1]
    assert list(graph.edges) == [(0, 0, 0, 1)]
    assert graph.ranges(TileIndex(0, 0), TileIndex(0, 1)) == [(0, SIZE - 1)]
