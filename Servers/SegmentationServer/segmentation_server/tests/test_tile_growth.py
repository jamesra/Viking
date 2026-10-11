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
from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.tile_growth import (
    MAX_PREDICTIONS_PER_CELL,
    GrowthCancelled,
    GrowthWalk,
    _kept_logits,
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
    # Logits are 4 px samples, so an edge lands within about 2 px of the truth, not exactly on it.
    assert iou(world.to_truth_frame(result), truth) >= 0.99
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

    # An edge off by up to 2 px (half a 4 px sample) flattens the pole of a radius-R curve into a
    # run of up to 4 * sqrt(R) px. A seam cut along a core or window border would be 256 or more.
    allowed = longest_boundary_run(truth) + 4 * int(np.sqrt(max(a, b))) + 8
    assert longest_boundary_run(world.to_truth_frame(result)) <= allowed


class _StingyOwner(World):
    """One cell answers with a mask shrunk by ``shrink`` px; every other cell is exact.

    The pixels it gave up are only weakly negative (-0.5), as for a model that is unsure
    rather than sure, so the owner veto leaves them to the neighbors that see them.
    """

    def __init__(self, truth, stingy: Cell, shrink: int) -> None:
        super().__init__(truth)
        self._stingy = stingy
        self._shrink = shrink

    def predict(self, row, col, points, labels, box=None, mask_logits=None):
        mask, logits, score = super().predict(row, col, points, labels, box=box)
        if Cell(row, col) == self._stingy:
            size = 2 * self._shrink + 1
            full = mask
            mask = cv2.erode(mask.astype(np.uint8), np.ones((size, size), np.uint8)) > 0
            logits = np.where(full & ~mask, np.float32(-0.5), self.logits_for(mask))
        return mask, logits, score


def test_a_neighbor_that_sees_a_pixel_well_inside_its_window_overrides_the_centred_cells_weak_doubt() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    owner = cell_of_point(2560, 2550)
    world = _StingyOwner(truth, owner, shrink=40)

    result = _grow(world, circle_clicks(2560, 2550, 100))

    mask = world.to_truth_frame(result)
    assert iou(mask, truth) >= 0.95
    # The owner gave up a 40 px band along the bar's edges, but only weakly (logit -0.5).
    # Far from the owner's centre, the band is where a neighbor sees it well inside its own
    # window, so the neighbor's confident answer wins ...
    assert mask[2405:2440, 2300:2420].all()
    assert mask[2405:2440, 2700:2800].all()
    # ... and at the owner's centre, the neighbors are at their window borders, where they
    # count for almost nothing: the cell that sees the pixel most centred decides.
    assert mask[2405:2440, 2520:2600].mean() < 0.2
    assert mask[2460:2640, 2300:2800].all()


class _UnsureMargin(_StingyOwner):
    """Like ``_StingyOwner``, but every window's outer 256 px margin has only weak logits."""

    def predict(self, row, col, points, labels, box=None, mask_logits=None):
        mask, logits, score = super().predict(row, col, points, labels, box=box)
        weak = np.full(logits.shape, True)
        weak[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE] = False
        logits = np.where(weak & mask, np.float32(0.5), logits)
        return mask, logits, score


def _bar_world(world_type):
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 2200:3400] = True
    owner = cell_of_point(2560, 2550)
    return truth, world_type(truth, owner, shrink=40)


def test_weak_margin_logits_below_the_iso_do_not_make_a_pixel_object() -> None:
    truth, world = _bar_world(_UnsureMargin)

    at_default = world.to_truth_frame(_grow(world, circle_clicks(2560, 2550, 100)))
    _truth, again = _bar_world(_UnsureMargin)
    lower_iso = again.to_truth_frame(_grow(again, circle_clicks(2560, 2550, 100), logit_iso=0.0))

    assert not at_default[2410, 2400]
    assert at_default.sum() < lower_iso.sum()


def test_a_prediction_without_logits_contributes_only_its_core() -> None:
    class NoLogits(_StingyOwner):
        def predict(self, row, col, points, labels, box=None, mask_logits=None):
            mask, _logits, score = super().predict(row, col, points, labels, box=box)
            return mask, None, score

    truth, world = _bar_world(NoLogits)

    result = _grow(world, circle_clicks(2560, 2550, 100))

    mask = world.to_truth_frame(result)
    assert mask.any()
    assert not mask[2410, 2400]


def test_a_smaller_cell_budget_gives_a_subset_of_a_larger_one() -> None:
    truth = ellipse(2048, 2048, 600, 450, 0.7)
    clicks = circle_clicks(2048, 2048, 100)

    small = World(truth)
    large = World(truth)
    small_mask = small.to_truth_frame(_grow(small, clicks, max_cells=3))
    large_mask = large.to_truth_frame(_grow(large, clicks, max_cells=48))

    assert small_mask.any()
    # More windows can move an edge pixel by one, so "subset" holds up to a vanishing fraction.
    assert int((small_mask & ~large_mask).sum()) <= 0.001 * int(small_mask.sum())
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


def _grow_with_deadline(world: World, clicks, seconds: float = 20.0, **kwargs):
    """Run a growth on a thread so a walk that never ends fails the test instead of hanging it."""
    import threading

    box: dict = {}

    def run() -> None:
        try:
            box["result"] = _grow(world, clicks, **kwargs)
        except BaseException as error:  # noqa: BLE001 - reported to the test below
            box["error"] = error

    worker = threading.Thread(target=run, daemon=True)
    worker.start()
    worker.join(seconds)
    assert not worker.is_alive(), "the growth walk did not terminate"
    if "error" in box:
        raise box["error"]
    return box["result"]


@pytest.mark.parametrize("budget", [{"max_cells": 1}, {"max_predictions": 1}])
def test_a_foreground_click_the_budget_never_reaches_does_not_hang_the_walk(budget) -> None:
    near = ellipse(1500, 1500, 150, 150, 0.0)
    far = ellipse(3000, 3000, 150, 150, 0.0)
    world = World(near | far)
    clicks = circle_clicks(1500, 1500, 60) + circle_clicks(3000, 3000, 60)

    result = _grow_with_deadline(world, clicks, **budget)

    mask = world.to_truth_frame(result)
    assert iou(mask & near, near) >= 0.99
    assert not (mask & far).any()
    assert len(world.calls) == 1


def test_a_walk_that_can_make_no_more_predictions_still_honors_a_cancel() -> None:
    near = ellipse(1500, 1500, 150, 150, 0.0)
    far = ellipse(3000, 3000, 150, 150, 0.0)
    world = World(near | far)
    clicks = circle_clicks(1500, 1500, 60) + circle_clicks(3000, 3000, 60)

    with pytest.raises(GrowthCancelled):
        _grow_with_deadline(
            world, clicks, max_cells=1, should_stop=lambda: len(world.calls) >= 1
        )


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

class _RejectingWorld(World):
    """Answers like World, but raises NoMatchingMask for the cells in ``reject``."""

    def __init__(self, truth_up, reject, **kwargs) -> None:
        super().__init__(truth_up, **kwargs)
        self.reject = set(reject)

    def predict(self, row: int, col: int, points, labels, box=None, mask_logits=None):
        if Cell(row, col) in self.reject:
            self.calls.append((Cell(row, col), list(points), list(labels)))
            raise NoMatchingMask(f"nothing fits cell {row},{col}")
        return super().predict(row, col, points, labels, box=box, mask_logits=mask_logits)


def test_a_no_match_for_the_starting_cell_fails_the_walk() -> None:
    start = cell_of_point(2048, 2048)
    world = _RejectingWorld(ellipse(2048, 2048, 600, 400, 0.0), reject={start})

    with pytest.raises(NoMatchingMask):
        _grow(world, circle_clicks(2048, 2048, 90))

    assert [cell for cell, _p, _l in world.calls] == [start]


def test_a_no_match_for_a_continuation_cell_skips_that_cell_and_the_walk_carries_on(caplog) -> None:
    truth = ellipse(2048, 2048, 700, 300, 0.0)
    start = cell_of_point(2048, 2048)
    skipped = Cell(start.row, start.col + 1)
    world = _RejectingWorld(truth, reject={skipped})

    with caplog.at_level("WARNING"):
        result = _grow_with_deadline(world, circle_clicks(2048, 2048, 90))

    assert result.mask.any()
    assert any(f"row={skipped.row} col={skipped.col} skipped" in r.getMessage() for r in caplog.records)
    rejected = [seam for seam in result.seams if seam.outcome == "rejected"]
    assert rejected and all(seam.cell == skipped for seam in rejected)
    # Each edge into the skipped cell is tried once; a rejected range is claimed, not retried.
    assert len({(seam.parent, seam.side) for seam in rejected}) == len(rejected)
    assert len(rejected) <= 4


def test_rejected_predictions_count_against_the_prediction_budget() -> None:
    truth = ellipse(2048, 2048, 900, 900, 0.0)
    start = cell_of_point(2048, 2048)
    world = _RejectingWorld(truth, reject={Cell(start.row + dr, start.col + dc) for dr in (-1, 0, 1) for dc in (-1, 0, 1)} - {start})

    _grow_with_deadline(world, circle_clicks(2048, 2048, 100), max_predictions=3)

    assert len(world.calls) <= 3


def test_a_prediction_is_silent_away_from_the_object_it_found_and_in_pieces_nobody_clicked() -> None:
    logits = np.full((256, 256), -5.0, dtype=np.float32)
    logits[100:120, 100:120] = 5.0  # clicked and kept
    logits[200:210, 200:210] = 5.0  # confident, but a piece with no click
    kept = np.zeros((1024, 1024), dtype=bool)
    kept[400:480, 400:480] = True

    native, silent = _kept_logits(logits, kept, iso=1.0)

    assert native.shape == (256, 256)
    assert not silent[100:120, 100:120].any()
    assert not silent[96:124, 96:124].any(), "background right beside the object still votes"
    assert silent[200:210, 200:210].all()
    assert silent[0:20, 0:20].all(), "background far from the object says nothing"


def test_the_walk_returns_the_fused_logits_over_the_same_extent_as_the_mask() -> None:
    truth = ellipse(2048, 2048, 300, 250, 0.4)
    world = World(truth)

    result = _grow(world, circle_clicks(2048, 2048, 100))

    assert result.logits is not None
    assert result.logits.shape == (result.mask.shape[0] // 4, result.mask.shape[1] // 4)
    decided = result.logits > 1.0
    coarse = result.mask.reshape(decided.shape[0], 4, decided.shape[1], 4).mean(axis=(1, 3)) >= 0.5
    assert int((decided ^ coarse).sum()) <= 0.01 * int(coarse.sum())
    assert result.logits.min() >= -20.0


def test_without_logits_from_any_prediction_the_result_has_none() -> None:
    class NoLogits(World):
        def predict(self, row, col, points, labels, box=None, mask_logits=None):
            mask, _logits, score = super().predict(row, col, points, labels, box=box)
            return mask, None, score

    world = NoLogits(ellipse(2048, 2048, 150, 120, 0.3))

    result = _grow(world, circle_clicks(2048, 2048, 80))

    assert result.mask.any()
    assert result.logits is None


def test_a_continuation_cell_is_given_what_its_neighbors_found_as_a_mask_prompt() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 1500:3500] = True
    world = World(truth)

    result = _grow(world, circle_clicks(2048, 2550, 100))

    assert iou(world.to_truth_frame(result), truth) >= 0.95
    prompts = [prompt for prompt in world.mask_prompts if prompt is not None]
    assert world.mask_prompts[0] is None, "the cell the user's clicks start in has nothing to be told"
    assert len(prompts) >= 2, "every continuation cell gets a prompt"
    for prompt in prompts:
        assert prompt.shape == (256, 256) and prompt.dtype == np.float32
        assert prompt.max() > 1.0, "it holds the neighbor's object logits ..."
        assert (prompt == 0.0).mean() > 0.2, "... and is neutral (0) wherever nothing has been predicted"
        assert prompt.min() > -19.0, "unseen is never passed as a confident background"


def test_a_continuation_that_stops_short_of_its_box_is_still_taken_so_the_mask_is_not_cut() -> None:
    class StopsShort(World):
        """Like SAM2 beside an image border: the answer leaves out the strip of the window nearest its border."""

        def predict(self, row, col, points, labels, box=None, mask_logits=None):
            mask, logits, score = super().predict(row, col, points, labels, box=box, mask_logits=mask_logits)
            mask = mask.copy()
            mask[:, :40] = False
            mask[:, -40:] = False
            return mask, np.where(mask, np.float32(6.0), np.float32(-6.0)), score

    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 1500:3500] = True
    world = StopsShort(truth)

    result = _grow(world, circle_clicks(2048, 2550, 100))

    mask = world.to_truth_frame(result)
    xs = np.nonzero(mask.any(axis=0))[0]
    assert xs.min() <= 1560 and xs.max() >= 3440
    assert mask[2450:2650, 1600:3400].mean() > 0.98


def test_continuation_cells_are_prompted_without_a_box() -> None:
    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2400:2700, 1500:3500] = True
    seen = []

    class Boxes(World):
        def predict(self, row, col, points, labels, box=None, mask_logits=None):
            seen.append((box, mask_logits))
            return super().predict(row, col, points, labels, box=box, mask_logits=mask_logits)

    world = Boxes(truth)

    result = _grow(world, circle_clicks(2048, 2550, 100))

    continuations = [box for box, mask in seen if mask is not None]
    assert len(continuations) >= 2
    assert all(box is None for box in continuations), "a continuation gets clicks and the mask prompt, never a box"
    assert iou(world.to_truth_frame(result), truth) >= 0.95


def test_an_object_that_runs_to_a_window_border_inside_a_core_is_followed_by_the_cell_centered_there() -> None:
    from segmentation_server.cell_grid import LOGIT_WINDOW

    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2830:3170, 2000:2300] = True      # runs past the north border (y = 3072) of cell (4, 3)'s window
    world = World(truth)
    walk = GrowthWalk([(500, 500)], [], world.predict)   # the user's click is elsewhere and starts nothing here
    south = Cell(4, 3)
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[(2830 - 2048) // 4:, (2000 - 1536) // 4:(2300 - 1536) // 4] = 6.0   # Y-up: the bar to the window's top
    walk._canvas.blend_window(south, logits, walk._logit_iso)

    walk._queue_frontiers(south)
    walk.advance()
    result = walk.result()

    target = Cell(5, 3)   # the core that holds the frontier, whose window is centered on it
    decoy = Cell(0, 0)    # the unrelated click's own start cell, which finds nothing
    called = [(index, cell) for index, (cell, _p, _l) in enumerate(world.calls) if cell != decoy]
    assert [cell for _index, cell in called] == [target]
    prompt = world.mask_prompts[called[0][0]]
    assert prompt is not None and prompt.max() > 1.0
    assert [(s.kind, s.cell) for s in result.seams if s.kind == "frontier"] == [("frontier", target)]
    mask = world.to_truth_frame(result)
    assert mask[3080:3160, 2050:2250].mean() > 0.95, "the object is followed past the border the first window stopped at"


def test_a_frontier_spot_is_tried_once_and_never_inside_a_blocked_cell() -> None:
    from segmentation_server.cell_grid import LOGIT_WINDOW

    truth = np.zeros_like(ellipse(2048, 2048, 1, 1, 0.0))
    truth[2830:3170, 2000:2300] = True
    world = World(truth)
    walk = GrowthWalk([(500, 500)], [], world.predict)
    south = Cell(4, 3)
    logits = np.full((LOGIT_WINDOW, LOGIT_WINDOW), -6.0, dtype=np.float32)
    logits[(2830 - 2048) // 4:, (2000 - 1536) // 4:(2300 - 1536) // 4] = 6.0
    walk._canvas.blend_window(south, logits, walk._logit_iso)

    walk._queue_frontiers(south)
    queued = {cell: list(points) for cell, points in walk._frontiers.items()}
    walk._queue_frontiers(south)

    assert queued == {cell: list(points) for cell, points in walk._frontiers.items()}, "the same spot is not queued twice"

    blocked = GrowthWalk([(500, 500)], [], world.predict)
    blocked._blocked.add(Cell(5, 3))
    blocked._canvas.blend_window(south, logits, blocked._logit_iso)
    blocked._queue_frontiers(south)
    assert Cell(5, 3) not in blocked._frontiers
