"""Growth over overlapping cells, against a synthetic world with an edge-biased fake SAM2."""

from __future__ import annotations

import cv2
import numpy as np
import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

from segmentation_server.cell_grid import (
    CORE_MARGIN,
    CORE_SIZE,
    Cell,
    TileIndex,
    cell_of_point,
    tile_of_point,
    tiles_for_cell,
)
from segmentation_server.tile_growth import (
    MAX_PREDICTIONS_PER_CELL,
    GrowthCancelled,
    GrowthWalk,
    _gate_margin,
    grow_segmentation,
)
from cell_world import (
    World,
    circle_clicks,
    ellipse,
    iou,
    longest_boundary_run,
    ring,
    stroke,
)


def _grow(world: World, clicks, background=(), **kwargs):
    return grow_segmentation(clicks, list(background), world.predict, **kwargs)


def test_a_blob_inside_one_core_costs_one_predict_and_matches_the_truth() -> None:
    truth = ellipse(2048, 2048, 150, 120, 0.3)
    world = World(truth)

    result = _grow(world, circle_clicks(2048, 2048, 80))

    assert len(world.calls) == 1
    assert iou(world.to_truth_frame(result), truth) == 1.0
    assert result.requested == []


ellipses = st.builds(
    lambda cx, cy, a, b, theta: (cx, cy, a, b, theta),
    st.integers(1900, 2200),
    st.integers(1900, 2200),
    st.integers(200, 600),
    st.integers(200, 600),
    st.floats(0.0, 3.14),
)


@settings(max_examples=12, deadline=None)
@given(shape=ellipses, u1=st.floats(-0.5, 0.5), u2=st.floats(-0.5, 0.5))
def test_a_blob_spanning_cores_is_recovered_from_any_start(shape, u1: float, u2: float) -> None:
    cx, cy, a, b, theta = shape
    truth = ellipse(cx, cy, a, b, theta)
    start_x = cx + np.cos(theta) * u1 * a - np.sin(theta) * u2 * b
    start_y = cy + np.sin(theta) * u1 * a + np.cos(theta) * u2 * b
    world = World(truth)

    result = _grow(world, circle_clicks(int(start_x), int(start_y), 40))

    assert iou(world.to_truth_frame(result), truth) >= 0.985
    assert len(world.calls) <= MAX_PREDICTIONS_PER_CELL * len(result.cells)


@settings(max_examples=12, deadline=None)
@given(shape=ellipses)
def test_a_curved_blob_has_no_straight_cut(shape) -> None:
    cx, cy, a, b, theta = shape
    truth = ellipse(cx, cy, a, b, theta)
    world = World(truth)

    result = _grow(world, circle_clicks(cx, cy, 60))

    assert longest_boundary_run(world.to_truth_frame(result)) <= longest_boundary_run(truth) + 8


class _StingyOwner(World):
    """One cell answers with a mask shrunk by ``shrink`` px; every other cell is exact.

    The pixels it gave up are only weakly negative (-0.5), as for a model that is unsure
    rather than sure, so the owner veto leaves them to the neighbors that see them.
    """

    def __init__(self, truth, stingy: Cell, shrink: int) -> None:
        super().__init__(truth)
        self._stingy = stingy
        self._shrink = shrink

    def predict(self, row, col, points, labels):
        mask, logits, score = super().predict(row, col, points, labels)
        if Cell(row, col) == self._stingy:
            size = 2 * self._shrink + 1
            full = mask
            mask = cv2.erode(mask.astype(np.uint8), np.ones((size, size), np.uint8)) > 0
            logits = np.where(full & ~mask, np.float32(-0.5), self.logits_for(mask))
        return mask, logits, score


def test_margin_pixels_from_a_neighbor_fill_in_what_the_owner_cell_left_out() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    owner = cell_of_point(2560, 2550)
    world = _StingyOwner(truth, owner, shrink=40)

    result = _grow(world, circle_clicks(2560, 2550, 100))

    mask = world.to_truth_frame(result)
    assert iou(mask, truth) >= 0.98
    # The fake SAM2 blanks 6 px at each window border, so the x=2560 join between the two
    # neighbors is the one strip only the stingy owner could have filled.
    assert mask[2405:2695, 2300:2540].all()
    assert mask[2405:2695, 2580:2800].all()


class _UnsureMargin(_StingyOwner):
    """Like ``_StingyOwner``, but every window's outer 256 px margin has only weak logits."""

    def predict(self, row, col, points, labels):
        mask, logits, score = super().predict(row, col, points, labels)
        weak = np.full(logits.shape, True)
        weak[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE] = False
        logits = np.where(weak & mask, np.float32(0.5), logits)
        return mask, logits, score


def _bar_world(world_type):
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    owner = cell_of_point(2560, 2550)
    return truth, world_type(truth, owner, shrink=40)


def test_margin_pixels_below_the_logit_gate_are_left_to_the_cell_that_owns_them() -> None:
    truth, world = _bar_world(_UnsureMargin)

    gated = world.to_truth_frame(_grow(world, circle_clicks(2560, 2550, 100)))
    _truth, again = _bar_world(_UnsureMargin)
    open_gate = again.to_truth_frame(_grow(again, circle_clicks(2560, 2550, 100), margin_logit_min=0.0))

    assert not gated[2410, 2400]
    assert open_gate[2410:2690, 2300:2540].all()
    assert gated.sum() < open_gate.sum()


def test_a_prediction_without_logits_contributes_only_its_core() -> None:
    class NoLogits(_StingyOwner):
        def predict(self, row, col, points, labels):
            mask, _logits, score = super().predict(row, col, points, labels)
            return mask, None, score

    truth, world = _bar_world(NoLogits)

    result = _grow(world, circle_clicks(2560, 2550, 100))

    mask = world.to_truth_frame(result)
    assert mask.any()
    assert not mask[2410, 2400]


class _LeakyNeighbors(World):
    """Every cell except ``owner`` adds a confident strip along y 2700..2760 to its mask.

    The strip is joined to the truth bar, so it survives the component filter, and it sits
    in ``owner``'s core where the owner (exact) is sure it is not object.
    """

    def __init__(self, truth, owner: Cell) -> None:
        super().__init__(truth)
        self._owner = owner

    def predict(self, row, col, points, labels):
        mask, logits, score = super().predict(row, col, points, labels)
        if Cell(row, col) != self._owner and mask.any():
            x0, y0 = col * 512, row * 512
            top = 1023 - (2759 - y0)
            bottom = 1023 - (2700 - y0)
            if 0 <= top and bottom < 1024:
                mask = mask.copy()
                mask[top:bottom + 1, :] = True
                logits = np.where(mask, np.float32(6.0), logits)
        return mask, logits, score


@pytest.mark.parametrize("start_x", [2560, 2320])
def test_an_owner_cell_that_is_sure_vetoes_a_neighbors_confident_margin(start_x: int) -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    owner = cell_of_point(2560, 2550)
    world = _LeakyNeighbors(truth, owner)

    result = _grow(world, circle_clicks(start_x, 2550, 80))

    mask = world.to_truth_frame(result)
    assert owner in result.cells
    assert not mask[2705:2760, 2310:2810].any()
    assert mask[2410:2690, 2310:2540].all()
    assert mask[2410:2690, 2540:2810].sum() >= 0.95 * 280 * 270


def test_owner_veto_never_removes_what_the_owner_accepted_itself() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    world = World(truth)

    result = _grow(world, circle_clicks(2560, 2550, 100), owner_veto_logit=100.0)

    assert iou(world.to_truth_frame(result), truth) >= 0.98


def test_gate_margin_keeps_the_core_and_only_confident_margin() -> None:
    kept = np.ones((1024, 1024), dtype=bool)
    logits = np.full((1024, 1024), 0.5, dtype=np.float32)
    logits[:, :100] = 3.0

    accepted = _gate_margin(kept, logits, 1.5)

    assert accepted[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE].all()
    assert accepted[:, :100].all()
    assert not accepted[:, 100:CORE_MARGIN].any()
    assert not accepted[:CORE_MARGIN, CORE_MARGIN:].any()
    assert int(accepted.sum()) == CORE_SIZE * CORE_SIZE + 100 * 1024


def test_gate_margin_upsamples_low_resolution_logits_and_drops_margin_without_them() -> None:
    kept = np.ones((1024, 1024), dtype=bool)
    low = np.full((256, 256), -2.0, dtype=np.float32)
    low[:, :32] = 4.0

    accepted = _gate_margin(kept, low, 1.5)

    assert accepted[:, :100].all()
    assert not accepted[:, 200:CORE_MARGIN].any()
    assert int(_gate_margin(kept, None, 1.5).sum()) == CORE_SIZE * CORE_SIZE


def test_a_smaller_cell_budget_gives_a_subset_of_a_larger_one() -> None:
    truth = ellipse(2048, 2048, 600, 450, 0.7)
    clicks = circle_clicks(2048, 2048, 100)

    small = World(truth)
    large = World(truth)
    small_mask = small.to_truth_frame(_grow(small, clicks, max_cells=3))
    large_mask = large.to_truth_frame(_grow(large, clicks, max_cells=48))

    assert small_mask.any()
    assert not (small_mask & ~large_mask).any()
    assert large_mask.sum() > small_mask.sum()


def test_the_cell_budget_bounds_the_walk() -> None:
    world = World(ellipse(2048, 2048, 900, 900, 0.0))

    result = _grow(world, circle_clicks(2048, 2048, 100), max_cells=4)

    assert len({cell for cell, _points, _labels in world.calls}) <= 4
    assert len(world.calls) <= 4 * MAX_PREDICTIONS_PER_CELL
    assert result.mask.any()


def test_the_prediction_budget_bounds_the_walk() -> None:
    world = World(ellipse(2048, 2048, 900, 900, 0.0))

    _grow(world, circle_clicks(2048, 2048, 100), max_predictions=5)

    assert len(world.calls) <= 5


def test_a_hairpin_whose_second_arm_returns_through_visited_cores_is_found() -> None:
    top = 1300
    left_arm, right_arm = 1500, 1700
    points = [(left_arm, 2900), (left_arm, top), (right_arm, top), (right_arm, 2900)]
    truth = stroke(points, 70)
    world = World(truth)

    result = _grow(world, circle_clicks(left_arm, 2850, 25))

    assert iou(world.to_truth_frame(result), truth) >= 0.97


def test_a_closed_ring_is_completed() -> None:
    truth = ring(2048, 2048, 700, 90)
    world = World(truth)

    result = _grow(world, circle_clicks(2048 + 700, 2048, 30))

    assert iou(world.to_truth_frame(result), truth) >= 0.97


def test_a_foreground_click_on_a_separate_object_is_still_segmented() -> None:
    near = ellipse(1500, 1500, 150, 150, 0.0)
    far = ellipse(3000, 3000, 150, 150, 0.0)
    world = World(near | far)

    result = _grow(world, circle_clicks(1500, 1500, 60) + circle_clicks(3000, 3000, 60))

    assert iou(world.to_truth_frame(result), near | far) >= 0.99


def test_background_clicks_reach_only_the_windows_that_contain_them() -> None:
    world = World(ellipse(2048, 2048, 150, 150, 0.0))
    inside = (2048 + 100, 2048)
    far_away = (3900, 3900)

    _grow(world, circle_clicks(2048, 2048, 60), background=[inside, far_away])

    _cell, points, labels = world.calls[0]
    assert labels.count(0) == 1
    assert len(points) == len(labels)


def test_missing_tiles_are_requested_then_the_walk_completes_after_upload() -> None:
    truth = ellipse(2048, 2048, 500, 400, 0.4)
    clicks = circle_clicks(2048, 2048, 90)
    owner = cell_of_point(2048, 2048)
    needed = {(t.row, t.col) for t in tiles_for_cell(owner)}
    held = {(tile_of_point(2048, 2048).row, tile_of_point(2048, 2048).col)}
    assert len(needed) == 4

    first = World(truth, available=held)
    partial = _grow(first, clicks)

    assert first.calls == []
    assert partial.mask.size == 0
    assert set(map(lambda t: (t.row, t.col), partial.requested)) == needed - held

    everything = {(r, c) for r in range(4) for c in range(4)}
    second = World(truth, available=everything)
    complete = _grow(second, clicks)

    assert iou(second.to_truth_frame(complete), truth) >= 0.985
    assert complete.requested == []


def _staged_walk(world: World, clicks, give):
    """Run one GrowthWalk, giving it the tiles it asks for between ``advance`` calls.

    ``give`` maps a requested tile to True (supplied) or False (unavailable). Returns the
    walk and the truth-frame mask after each ``advance``.
    """
    walk = GrowthWalk(clicks, [], world.predict)
    frames = []
    asked = []
    while True:
        walk.advance()
        frames.append(world.to_truth_frame(walk.result()))
        needed = walk.take_requested()
        if not needed:
            return walk, frames, asked
        asked.extend(needed)
        unavailable = []
        for tile in needed:
            if give(tile):
                world.available.add((tile.row, tile.col))
            else:
                unavailable.append(tile)
        walk.resume(unavailable)


def test_a_walk_continues_after_each_batch_of_tiles_and_never_loses_pixels() -> None:
    truth = ellipse(2048, 2048, 500, 400, 0.4)
    clicks = circle_clicks(2048, 2048, 90)
    world = World(truth, available=set())

    walk, frames, asked = _staged_walk(world, clicks, lambda _tile: True)

    assert len(frames) > 1
    for before, after in zip(frames, frames[1:]):
        assert not (before & ~after).any()
    assert iou(frames[-1], truth) >= 0.985
    assert len(asked) == len(set(asked))


def test_a_walk_in_stages_matches_a_walk_that_had_every_tile() -> None:
    truth = ellipse(2048, 2048, 500, 400, 0.4)
    clicks = circle_clicks(2048, 2048, 90)
    everything = {(r, c) for r in range(-1, 6) for c in range(-1, 6)}
    full_world = World(truth, available=everything)
    full = _grow(full_world, clicks)

    _walk, frames, _asked = _staged_walk(World(truth, available=set()), clicks, lambda _tile: True)

    assert iou(frames[-1], full_world.to_truth_frame(full)) >= 0.99


def test_a_tile_the_client_cannot_supply_is_not_asked_for_again() -> None:
    truth = ellipse(2560, 2560, 600, 400, 0.0)
    everything = {(r, c) for r in range(5) for c in range(5)}
    world = World(truth, available=everything - {(2, 3)})

    _walk, _frames, asked = _staged_walk(world, circle_clicks(2560, 2560, 90), lambda _tile: False)

    assert len(asked) == len(set(asked))
    assert (2, 3) in {(tile.row, tile.col) for tile in asked}


def test_resume_without_waiting_cells_changes_nothing() -> None:
    world = World(ellipse(2048, 2048, 150, 120, 0.3))
    walk = GrowthWalk(circle_clicks(2048, 2048, 80), [], world.predict)
    walk.advance()
    calls = len(world.calls)

    walk.resume()
    walk.advance()

    assert len(world.calls) == calls
    assert walk.take_requested() == []


def test_requested_tiles_are_capped() -> None:
    world = World(ellipse(2048, 2048, 300, 300, 0.0), available=set())

    result = _grow(world, circle_clicks(2048, 2048, 60), max_requested=2)

    assert len(result.requested) == 2
    assert all(isinstance(tile, TileIndex) for tile in result.requested)


def test_a_cell_that_cannot_be_predicted_does_not_stop_the_other_cells() -> None:
    truth = ellipse(2560, 2560, 600, 400, 0.0)
    everything = {(r, c) for r in range(5) for c in range(5)}
    world = World(truth, available=everything - {(2, 3)})

    result = _grow(world, circle_clicks(2560, 2560, 90))

    assert result.mask.any()
    assert {(t.row, t.col) for t in result.requested} == {(2, 3)}
    assert iou(world.to_truth_frame(result), truth) < 1.0

def test_should_stop_cancels_before_the_next_predict() -> None:
    world = World(ellipse(2048, 2048, 600, 400, 0.0))

    with pytest.raises(GrowthCancelled):
        _grow(world, circle_clicks(2048, 2048, 90), should_stop=lambda: True)

    assert world.calls == []


def test_no_foreground_is_an_empty_result() -> None:
    world = World(ellipse(2048, 2048, 100, 100, 0.0))

    result = _grow(world, [])

    assert result.mask.size == 0
    assert result.requested == []


def test_the_fused_mosaic_origin_and_size_follow_the_cores() -> None:
    truth = ellipse(2048, 2048, 150, 150, 0.0)
    world = World(truth)

    result = _grow(world, circle_clicks(2048, 2048, 60))

    owner = cell_of_point(2048, 2048)
    assert result.mask.shape == (CORE_SIZE, CORE_SIZE)
    assert (result.origin_x, result.origin_y) == (
        owner.col * 512 + CORE_MARGIN,
        owner.row * 512 + CORE_MARGIN,
    )
    assert set(result.cells) == {owner}
    assert result.cells[owner].core.shape == (CORE_SIZE, CORE_SIZE)
