"""What a seam walk keeps of the object.

A straight edge on the result that is not a tile border means a half-tile step cut
the object short. The fakes in ``seam_world`` answer from a ground-truth mask, so a
walk over a single convex shape (every window holds one piece of it) must return
that shape exactly, however many tiles it spans.
"""

from __future__ import annotations

from typing import Tuple

import numpy as np
from hypothesis import given, settings
from hypothesis import strategies as st

from seam_world import SIZE, SeamWorld, blob
from segmentation_server.tile_growth import (
    TilePrediction,
    _continuation_seeds,
    _deep_contact_runs,
    _prompt_index,
    _seam_inset,
    _Side,
    paste_half_mask,
)

TILE = 64


@st.composite
def _single_shapes(draw: st.DrawFn) -> Tuple[int, int, Tuple[str, int, int, int, int]]:
    rows = draw(st.integers(min_value=2, max_value=4))
    cols = draw(st.integers(min_value=2, max_value=4))
    shape = (
        draw(st.sampled_from(["rect", "ellipse"])),
        draw(st.integers(min_value=0, max_value=cols * SIZE)),
        draw(st.integers(min_value=0, max_value=rows * SIZE)),
        draw(st.integers(min_value=6, max_value=cols * SIZE // 2)),
        draw(st.integers(min_value=6, max_value=rows * SIZE // 2)),
    )
    return rows, cols, shape


@given(_single_shapes())
@settings(max_examples=300, deadline=None, derandomize=True)
def test_a_perfect_model_reproduces_a_single_convex_shape(case) -> None:
    rows, cols, shape = case
    truth = blob(rows, cols, [shape])
    if not truth.any():
        return
    world = SeamWorld(truth=truth, rows=rows, cols=cols)
    result = world.walk()
    expected = world.click_component()
    got = world.canvas(result)
    assert int((expected & ~got).sum()) == 0, "pixels of the object are missing"
    assert int((got & ~expected).sum()) == 0, "pixels outside the object were added"


@given(
    st.integers(min_value=0, max_value=2**32 - 1),
    st.floats(min_value=0.5, max_value=0.97),
    st.sampled_from(["right", "left", "top", "bottom"]),
)
@settings(max_examples=200, deadline=None, derandomize=True)
def test_continuation_seeds_lie_inside_the_mask(seed: int, density: float, side: str) -> None:
    """A seed is a positive prompt, so it must be a pixel the half-tile mask already holds."""
    mask = np.random.default_rng(seed).random((TILE, TILE)) < density
    for x, y in _continuation_seeds(mask, side, TILE, 8):
        assert mask[y, x], (side, x, y)


def test_continuation_seeds_skip_a_contact_that_does_not_reach_back_into_the_mask() -> None:
    """Midline contact alone is not enough: the seed is ``inset`` pixels back, and that must be mask too."""
    mask = np.zeros((TILE, TILE), dtype=np.bool_)
    mask[20:40, 10:32] = True
    mask[5:8, 31] = True
    seeds = _continuation_seeds(mask, "right", TILE, 4)
    assert seeds
    assert all(20 <= y < 40 for _x, y in seeds)


def test_a_hole_at_the_middle_of_the_contact_does_not_cancel_the_crossing() -> None:
    inset = _seam_inset(TILE)
    mask = np.ones((TILE, TILE), dtype=np.bool_)
    middle = TILE // 2
    mask[inset, middle - 1: middle + 1] = False
    pred = TilePrediction(row=0, col=0, mask=mask, logits=None, score=0.9)
    runs = _deep_contact_runs(pred, _Side.TOP, TILE)
    assert runs == [(0, TILE - 1)]
    index = _prompt_index(mask, _Side.TOP, runs[0], inset)
    assert index is not None and mask[inset, index]
    assert abs(index - middle) <= 2


def test_a_contact_with_no_mask_behind_it_is_not_a_crossing() -> None:
    mask = np.zeros((TILE, TILE), dtype=np.bool_)
    mask[0, 10:20] = True
    pred = TilePrediction(row=0, col=0, mask=mask, logits=None, score=0.9)
    assert _deep_contact_runs(pred, _Side.TOP, TILE) == []


def test_paste_replaces_near_the_cut_and_keeps_the_tile_farther_back() -> None:
    half = TILE // 2
    reach = half // 2
    source = np.ones((TILE, TILE), dtype=np.bool_)
    neighbor = np.ones((TILE, TILE), dtype=np.bool_)
    synthetic = np.zeros((TILE, TILE), dtype=np.bool_)
    new_source, new_neighbor = paste_half_mask(source, neighbor, synthetic, "right", TILE)
    assert not new_source[:, TILE - reach:].any()
    assert new_source[:, : TILE - reach].all()
    assert not new_neighbor[:, :reach].any()
    assert new_neighbor[:, reach:].all()


def test_paste_adds_what_the_half_tile_found_beyond_the_reach() -> None:
    source = np.zeros((TILE, TILE), dtype=np.bool_)
    synthetic = np.zeros((TILE, TILE), dtype=np.bool_)
    synthetic[:, : TILE // 2] = True
    new_source, _neighbor = paste_half_mask(source, None, synthetic, "right", TILE)
    assert new_source[:, TILE // 2:].all()
    assert not new_source[:, : TILE // 2].any()


@given(st.sampled_from(["right", "left", "top", "bottom"]))
def test_paste_leaves_the_far_half_of_each_tile_alone(side: str) -> None:
    half = TILE // 2
    source = np.ones((TILE, TILE), dtype=np.bool_)
    neighbor = np.ones((TILE, TILE), dtype=np.bool_)
    synthetic = np.zeros((TILE, TILE), dtype=np.bool_)
    new_source, new_neighbor = paste_half_mask(source, neighbor, synthetic, side, TILE)
    far_source = {
        "right": new_source[:, :half],
        "left": new_source[:, half:],
        "top": new_source[half:, :],
        "bottom": new_source[:half, :],
    }[side]
    far_neighbor = {
        "right": new_neighbor[:, half:],
        "left": new_neighbor[:, :half],
        "top": new_neighbor[:half, :],
        "bottom": new_neighbor[half:, :],
    }[side]
    assert far_source.all()
    assert far_neighbor.all()
