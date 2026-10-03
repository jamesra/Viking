"""Inscribed-box prototype: finding circles in the click list and boxing them for SAM2."""

from __future__ import annotations

import math
import random

import numpy as np
from hypothesis import given, settings
from hypothesis import strategies as st

from cell_world import World, circle_clicks, ellipse
from segmentation_server.cell_grid import Cell, mosaic_to_window
from segmentation_server.circle_boxes import find_circles, outer_ring_box_enabled, outer_ring_boxes
from segmentation_server.tile_growth import grow_segmentation, window_box


def _expected(cx: int, cy: int, radius: int):
    """The square inscribed in the circle, one pixel inside."""
    half = int(math.floor(radius / math.sqrt(2.0) - 1.0))
    return cx - half, cy - half, cx + half, cy + half


def _close(box, expected, slack: int = 1) -> bool:
    return all(abs(a - b) <= slack for a, b in zip(box, expected))


def test_a_circle_yields_its_inscribed_square() -> None:
    boxes = outer_ring_boxes(circle_clicks(2048, 2048, 100))

    assert boxes == [(1979, 1979, 2117, 2117)]


@given(st.integers(200, 3800), st.integers(200, 3800), st.integers(20, 300))
@settings(max_examples=60, deadline=None)
def test_the_box_lies_inside_the_circle_and_fills_most_of_it(cx: int, cy: int, radius: int) -> None:
    boxes = outer_ring_boxes(circle_clicks(cx, cy, radius))

    assert len(boxes) == 1
    x0, y0, x1, y1 = boxes[0]
    assert (x0 + x1, y0 + y1) == (2 * cx, 2 * cy)
    half = x1 - cx
    assert half == y1 - cy
    assert math.hypot(half, half) < radius
    assert half >= radius / math.sqrt(2.0) - 3


@given(
    st.integers(200, 3800),
    st.integers(200, 3800),
    st.integers(20, 300),
    st.randoms(use_true_random=False),
)
@settings(max_examples=60, deadline=None)
def test_click_order_does_not_change_the_box(cx: int, cy: int, radius: int, rng: random.Random) -> None:
    clicks = circle_clicks(cx, cy, radius)
    rng.shuffle(clicks)

    boxes = outer_ring_boxes(clicks)

    assert len(boxes) == 1
    assert _close(boxes[0], _expected(cx, cy, radius))


def test_two_circles_give_two_boxes_and_stray_clicks_none() -> None:
    clicks = circle_clicks(1000, 1000, 60) + circle_clicks(3000, 2500, 120) + [(50, 50), (2000, 2000)]

    boxes = outer_ring_boxes(clicks)

    assert sorted(boxes) == sorted([_expected(1000, 1000, 60), _expected(3000, 2500, 120)])


def test_clicks_that_are_not_a_circle_give_no_box() -> None:
    clicks = circle_clicks(2048, 2048, 100)
    outer_only = [clicks[0], *clicks[5:]]
    inner_only = clicks[:5]

    assert outer_ring_boxes(outer_only) == []
    assert outer_ring_boxes(inner_only) == []
    assert outer_ring_boxes([(10, 10)]) == []
    assert outer_ring_boxes([]) == []
    assert outer_ring_boxes(circle_clicks(500, 500, 5)) == []


def test_the_env_switch(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENT_OUTER_RING_BOX", raising=False)
    assert outer_ring_box_enabled()
    for off in ("0", "false", "OFF", "no"):
        monkeypatch.setenv("SEGMENT_OUTER_RING_BOX", off)
        assert not outer_ring_box_enabled()
    monkeypatch.setenv("SEGMENT_OUTER_RING_BOX", "1")
    assert outer_ring_box_enabled()


def test_window_box_is_window_local_y_down() -> None:
    # Cell (3, 3) has its window origin at (1536, 1536).
    box = window_box(Cell(3, 3), [(1984, 1984, 2112, 2112)])

    assert box == (448, 447, 576, 575)  # x - 1536, and 2559 - y


def test_window_box_clips_to_the_window_and_picks_the_largest() -> None:
    cell = Cell(3, 3)
    small = (1600, 1600, 1700, 1700)
    straddling_left = (1400, 1600, 1700, 1900)

    assert window_box(cell, [small, straddling_left]) == window_box(cell, [straddling_left])
    clipped = window_box(cell, [straddling_left])
    assert clipped is not None and clipped[0] == 0


def test_window_box_ignores_boxes_outside_or_too_thin() -> None:
    cell = Cell(3, 3)

    assert window_box(cell, [(0, 0, 100, 100)]) is None
    assert window_box(cell, [(1700, 1700, 1710, 1900)]) is None
    assert window_box(cell, []) is None


class _BoxWorld(World):
    """World that records the box each predict() was given."""

    def __init__(self, truth_up: np.ndarray) -> None:
        super().__init__(truth_up)
        self.boxes: list = []

    def predict(self, row, col, points, labels, box=None):
        self.boxes.append((Cell(row, col), box))
        return super().predict(row, col, points, labels)


def test_growth_hands_the_cell_its_box_only_when_boxes_are_given() -> None:
    truth = ellipse(2048, 2048, 150, 120, 0.3)
    clicks = circle_clicks(2048, 2048, 100)

    with_boxes = _BoxWorld(truth)
    grow_segmentation(clicks, [], with_boxes.predict, boxes=outer_ring_boxes(clicks))
    without = _BoxWorld(truth)
    grow_segmentation(clicks, [], without.predict)

    assert with_boxes.boxes[0] == (Cell(3, 3), (443, 442, 581, 580))
    assert all(box is None for _cell, box in without.boxes)


def test_find_circles_reports_the_ring_clicks_and_not_the_center() -> None:
    clicks = circle_clicks(2048, 2048, 100)

    circles = find_circles(clicks)

    assert len(circles) == 1
    assert set(circles[0].inner_clicks) == set(clicks[1:5])
    assert set(circles[0].outer_clicks) == set(clicks[5:9])
    assert set(circles[0].ring_clicks) == set(clicks[1:9])


def test_a_boxed_cell_is_prompted_with_the_center_only() -> None:
    truth = ellipse(2048, 2048, 150, 120, 0.3)
    clicks = circle_clicks(2048, 2048, 100)
    circles = find_circles(clicks)
    world = _BoxWorld(truth)

    grow_segmentation(
        clicks,
        [],
        world.predict,
        boxes=[c.box for c in circles],
        omit_with_box=[p for c in circles for p in c.ring_clicks],
    )

    cell, points, labels = world.calls[0]
    assert points == [mosaic_to_window(cell, *clicks[0])]
    assert labels == [1]
    assert world.boxes[0][1] == (443, 442, 581, 580)


def test_clicks_are_not_omitted_when_the_window_holds_nothing_else() -> None:
    truth = ellipse(2048, 2048, 150, 120, 0.3)
    clicks = circle_clicks(2048, 2048, 100)
    inner = clicks[1:5]
    world = _BoxWorld(truth)

    grow_segmentation(inner, [], world.predict, boxes=[(1979, 1979, 2117, 2117)], omit_with_box=inner)

    _cell, points, labels = world.calls[0]
    assert len(points) == 4
    assert labels == [1] * 4


def test_without_a_box_every_click_is_sent() -> None:
    truth = ellipse(2048, 2048, 150, 120, 0.3)
    clicks = circle_clicks(2048, 2048, 100)
    world = _BoxWorld(truth)

    grow_segmentation(clicks, [], world.predict, omit_with_box=clicks[1:5])

    assert len(world.calls[0][1]) == 9

