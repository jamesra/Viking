"""Growth memory replays earlier cells from the cache and pins the tiles they need."""

from __future__ import annotations

from typing import Sequence, Tuple

import numpy as np
import pytest

from cell_world import World, circle_clicks, ellipse, iou
from segmentation_server.cell_grid import Cell, TileIndex, cell_of_point, tiles_for_cell
from segmentation_server.growth_memory import GrowthMemory, grow_remembering, prompt_key
from segmentation_server.tile_growth import GrowthCancelled

Point = Tuple[int, int]
CLICKS = circle_clicks(2048, 2048, 90)
EVERYTHING = {(r, c) for r in range(5) for c in range(5)}


def _session(foreground: Sequence[Point], background: Sequence[Point] = ()) -> tuple:
    return prompt_key("RC2", 1, "TEM", "grid", 1, False, foreground, background)


def _window_mask() -> np.ndarray:
    mask = np.zeros((1024, 1024), dtype=np.bool_)
    mask[400:600, 400:600] = True
    return mask


def test_prompt_key_ignores_point_order() -> None:
    assert _session([(1, 2), (3, 4)]) == _session([(3, 4), (1, 2)])
    assert _session([(1, 2)]) != _session([(1, 2)], [(5, 5)])


def test_repeating_the_same_clicks_settles_to_no_new_sam2_work() -> None:
    world = World(ellipse(2048, 2048, 500, 400, 0.4))
    memory = GrowthMemory()
    session = _session(CLICKS)

    first = grow_remembering(memory, session, CLICKS, [], world.predict)
    assert first.predicted == len(world.calls) > 1

    previous = first
    for _attempt in range(4):
        again = grow_remembering(memory, session, CLICKS, [], world.predict)
        assert not _lost(previous.result, again.result)
        if again.predicted == 0:
            break
        previous = again

    assert again.predicted == 0
    assert again.reused >= 1
    assert iou(world.to_truth_frame(again.result), world.to_truth_frame(first.result)) >= 0.99


def test_a_resumed_walk_keeps_what_it_found_and_completes_it() -> None:
    truth = ellipse(2560, 2560, 500, 400, 0.4)
    clicks = circle_clicks(2560, 2560, 90)
    owner = cell_of_point(2560, 2560)
    assert [(t.row, t.col) for t in tiles_for_cell(owner)] == [(2, 2)]
    memory = GrowthMemory()
    session = _session(clicks)

    partial_world = World(truth, available={(2, 2)})
    partial = grow_remembering(memory, session, clicks, [], partial_world.predict)

    assert partial.result.requested
    assert partial.predicted >= 1

    full_world = World(truth, available=EVERYTHING)
    complete = grow_remembering(memory, session, clicks, [], full_world.predict)

    assert not _lost(partial.result, complete.result)
    assert complete.result.requested == []
    assert iou(full_world.to_truth_frame(complete.result), truth) >= 0.985


def test_a_later_call_never_shrinks_the_mask_even_if_sam2_answers_smaller() -> None:
    truth = ellipse(2048, 2048, 500, 400, 0.4)
    memory = GrowthMemory()
    session = _session(CLICKS)
    generous = World(truth)
    first = grow_remembering(memory, session, CLICKS, [], generous.predict)

    class Stingy(World):
        """Answers with only the left half of what the truth holds in each window."""

        def predict(self, row, col, points, labels):
            mask, logits, score = super().predict(row, col, points, labels)
            mask[:, mask.shape[1] // 2:] = False
            return mask, logits, score

    second = grow_remembering(memory, session, CLICKS + [(2049, 2049)], [], Stingy(truth).predict)
    third = grow_remembering(memory, session, CLICKS, [], Stingy(truth).predict)

    assert not (first.result.mask & ~third.result.mask).any()
    assert third.result.mask.sum() >= first.result.mask.sum()
    assert second.result.mask.shape[0] > 0


def test_a_remembered_open_edge_is_followed_once_the_tiles_arrive() -> None:
    truth = ellipse(2560, 2560, 500, 400, 0.4)
    clicks = circle_clicks(2560, 2560, 90)
    memory = GrowthMemory()
    session = _session(clicks)

    partial = grow_remembering(memory, session, clicks, [], World(truth, available={(2, 2)}).predict)
    full_world = World(truth, available=EVERYTHING)
    complete = grow_remembering(memory, session, clicks, [], full_world.predict)

    assert partial.result.mask.any()
    assert not _lost(partial.result, complete.result)
    assert iou(full_world.to_truth_frame(complete.result), truth) >= 0.985


def test_every_cell_the_margin_reached_is_remembered_and_replays_without_loss() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    clicks = circle_clicks(2560, 2550, 100)
    world = World(truth)
    memory = GrowthMemory()
    session = _session(clicks)

    first = grow_remembering(memory, session, clicks, [], world.predict)

    assert len(first.result.cells) >= 3
    assert set(memory.cores(session)) == set(first.result.cells)

    again = grow_remembering(memory, session, clicks, [], world.predict)
    assert not _lost(first.result, again.result)


def _lost(before, after) -> bool:
    """True if any pixel of ``before`` is missing from ``after``, comparing in mosaic coordinates."""
    height, width = before.mask.shape
    for y, x in zip(*np.nonzero(before.mask)):
        up_y = before.origin_y + (height - 1 - int(y))
        up_x = before.origin_x + int(x)
        ay = after.mask.shape[0] - 1 - (up_y - after.origin_y)
        ax = up_x - after.origin_x
        if not (0 <= ay < after.mask.shape[0] and 0 <= ax < after.mask.shape[1] and after.mask[ay, ax]):
            return True
    return False


def test_a_different_background_click_is_a_different_session() -> None:
    world = World(ellipse(2048, 2048, 150, 150, 0.0))
    memory = GrowthMemory()

    grow_remembering(memory, _session(CLICKS), CLICKS, [], world.predict)
    other = grow_remembering(memory, _session(CLICKS, [(2400, 2400)]), CLICKS, [(2400, 2400)], world.predict)

    assert other.reused == 0
    assert other.predicted >= 1


def test_visited_cells_name_the_aligned_tiles_they_need() -> None:
    world = World(ellipse(2048, 2048, 500, 400, 0.4))
    memory = GrowthMemory()
    session = _session(CLICKS)

    outcome = grow_remembering(memory, session, CLICKS, [], world.predict)

    needed = {tile for cell in outcome.result.cells for tile in tiles_for_cell(cell)}
    assert set(memory.tile_indexes(session)) == needed
    assert memory.tile_indexes(_session([(9, 9)])) == []


def test_held_tiles_are_remembered_until_the_tile_is_replaced() -> None:
    memory = GrowthMemory()
    session = _session([(1, 1)])
    memory.remember_held(session, [TileIndex(1, 1), TileIndex(2, 2)])
    memory.remember_held(session, [TileIndex(2, 2), TileIndex(3, 3)])

    assert set(memory.tile_indexes(session)) == {TileIndex(1, 1), TileIndex(2, 2), TileIndex(3, 3)}

    memory.invalidate_tile("RC2", 1, "TEM", "grid", 1, 2, 2)

    assert set(memory.tile_indexes(session)) == {TileIndex(1, 1), TileIndex(3, 3)}


def test_cached_arrays_are_copies() -> None:
    memory = GrowthMemory()
    session = _session([(5, 5)])
    mask = _window_mask()
    memory.store_predict(session, 1, 1, [(5, 5)], [1], mask, None, 0.9)

    mask[:] = False
    first = memory.lookup(session, 1, 1, [(5, 5)], [1])
    assert first is not None
    first[0][:] = False
    second = memory.lookup(session, 1, 1, [(5, 5)], [1])

    assert second is not None and second[0].sum() == 200 * 200
    assert memory.lookup(session, 1, 1, [(5, 5)], [0]) is None
    assert memory.lookup(session, 1, 2, [(5, 5)], [1]) is None


def test_the_least_recently_used_session_is_evicted_over_the_cap() -> None:
    one_mask = _window_mask().nbytes
    memory = GrowthMemory(max_bytes=one_mask * 2)
    old, newer, newest = _session([(1, 1)]), _session([(2, 2)]), _session([(3, 3)])

    for session in (old, newer):
        memory.store_predict(session, 0, 0, [(1, 1)], [1], _window_mask(), None, 0.5)
    assert memory.lookup(old, 0, 0, [(1, 1)], [1]) is not None
    memory.store_predict(newest, 0, 0, [(1, 1)], [1], _window_mask(), None, 0.5)

    assert memory.lookup(old, 0, 0, [(1, 1)], [1]) is not None
    assert memory.lookup(newer, 0, 0, [(1, 1)], [1]) is None
    assert memory.lookup(newest, 0, 0, [(1, 1)], [1]) is not None


def test_one_session_larger_than_the_cap_is_dropped() -> None:
    memory = GrowthMemory(max_bytes=_window_mask().nbytes // 2)
    session = _session([(1, 1)])

    memory.store_predict(session, 0, 0, [(1, 1)], [1], _window_mask(), None, 0.5)

    assert memory.lookup(session, 0, 0, [(1, 1)], [1]) is None


def test_invalidating_a_tile_drops_only_results_built_from_it() -> None:
    memory = GrowthMemory()
    session = _session([(1, 1)])
    uses_tile = Cell(row=5, col=5)
    other = Cell(row=10, col=10)
    tile: TileIndex = tiles_for_cell(uses_tile)[0]
    assert tile not in tiles_for_cell(other)
    for cell in (uses_tile, other):
        memory.store_predict(session, cell.row, cell.col, [(1, 1)], [1], _window_mask(), None, 0.5)
    core = np.zeros((512, 512), dtype=np.bool_)
    core[10:20, 10:20] = True
    memory.remember_cores(session, {uses_tile: core, other: core})

    memory.invalidate_tile("RC2", 1, "TEM", "grid", 1, tile.row, tile.col)

    assert memory.lookup(session, uses_tile.row, uses_tile.col, [(1, 1)], [1]) is None
    assert memory.lookup(session, other.row, other.col, [(1, 1)], [1]) is not None
    assert set(memory.tile_indexes(session)) == set(tiles_for_cell(other))
    assert set(memory.cores(session)) == {other}


def test_invalidating_another_volume_leaves_the_session_alone() -> None:
    memory = GrowthMemory()
    session = _session([(1, 1)])
    memory.store_predict(session, 5, 5, [(1, 1)], [1], _window_mask(), None, 0.5)
    tile = tiles_for_cell(Cell(5, 5))[0]

    memory.invalidate_tile("OTHER", 1, "TEM", "grid", 1, tile.row, tile.col)

    assert memory.lookup(session, 5, 5, [(1, 1)], [1]) is not None


def test_cancellation_propagates_and_keeps_finished_cells() -> None:
    world = World(ellipse(2048, 2048, 500, 400, 0.4))
    memory = GrowthMemory()
    session = _session(CLICKS)

    with pytest.raises(GrowthCancelled):
        grow_remembering(
            memory, session, CLICKS, [], world.predict, should_stop=lambda: len(world.calls) >= 1
        )

    assert len(world.calls) == 1
    first = world.calls[0]
    assert memory.lookup(session, first[0].row, first[0].col, first[1], first[2]) is not None
