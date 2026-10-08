"""The client's prompt starts the walk in every cell that holds part of its box or any of its clicks."""

from __future__ import annotations

import numpy as np
import pytest

from cell_world import World, ellipse, iou
from segmentation_server.cell_grid import Cell, cell_of_point
from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.tile_growth import GrowthWalk, grow_segmentation


class _RecordingWorld(World):
    def __init__(self, truth) -> None:
        super().__init__(truth)
        self.given = []

    def predict(self, row, col, points, labels, box=None):
        self.given.append((Cell(row, col), list(points), list(labels), box))
        return super().predict(row, col, points, labels, box=box)


def client_prompt(cx: int, cy: int, radius: int):
    """What a modern client sends: the inscribed square, and four points on the axes at 0.95 radius."""
    half = int(radius / np.sqrt(2))
    box = (cx - half, cy - half, cx + half, cy + half)
    reach = int(round(0.95 * radius))
    points = [(cx + reach, cy), (cx, cy + reach), (cx - reach, cy), (cx, cy - reach)]
    return box, points


def _first_call(world, cell):
    return next(call for call in world.given if call[0] == cell)


def test_every_core_the_box_touches_is_started_with_the_whole_box() -> None:
    # A 400 px disc centred on the corner shared by four cores (their boundaries meet at 2304, 2304).
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)

    grow_segmentation(points, [], world.predict, boxes=[box])

    touched = {cell_of_point(x, y) for x in (box[0], box[2]) for y in (box[1], box[3])}
    assert len(touched) == 4
    for cell in touched:
        _cell, _points, _labels, given_box = _first_call(world, cell)
        assert given_box is not None, cell


def _world_with_a_hole_in_the_box():
    truth = ellipse(2304, 2304, 300, 300, 0.0).copy()
    hole = (slice(2290, 2330), slice(2250, 2290))
    truth[hole] = False
    return _RecordingWorld(truth), hole


def test_a_client_box_is_taken_as_object_so_a_hole_inside_it_is_filled() -> None:
    world, hole = _world_with_a_hole_in_the_box()
    box, points = client_prompt(2304, 2304, 300)

    plain = world.to_truth_frame(grow_segmentation(points, [], world.predict, boxes=[box]))
    assumed = world.to_truth_frame(grow_segmentation(points, [], world.predict, boxes=[box], assume_boxes=True))

    assert not plain[hole].any(), "SAM2 leaves the hole out, as it does in the field"
    assert assumed[hole].all()
    assert iou(assumed, ellipse(2304, 2304, 300, 300, 0.0)) >= 0.97


def test_only_boxes_the_client_sent_are_taken_as_object() -> None:
    from types import SimpleNamespace

    from segmentation_server.server import _resolve_boxes

    sent = SimpleNamespace(foreground_boxes=[SimpleNamespace(x_min=10, y_min=20, x_max=110, y_max=140)])
    boxes, omit, client_chose = _resolve_boxes(sent, [(60, 80)])
    assert boxes == [(10, 20, 110, 140)] and omit == [] and client_chose is True

    none_sent = SimpleNamespace(foreground_boxes=[])
    _boxes, _omit, client_chose = _resolve_boxes(none_sent, [(60, 80)])
    assert client_chose is False


def test_a_box_taken_as_object_is_not_an_answer_when_every_start_is_rejected() -> None:
    def refuse(row, col, points, labels, box=None):
        raise NoMatchingMask("nothing fits")

    box, points = client_prompt(2304, 2304, 300)
    walk = GrowthWalk(points, [], refuse, boxes=[box], assume_boxes=True)
    walk.advance()

    with pytest.raises(NoMatchingMask):
        walk.result()


def test_reserve_cells_still_start_when_a_box_taken_as_object_is_all_the_canvas_holds() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)
    refused = []

    def refuse_the_first_window(row, col, pts, lbls, box=None):
        if not refused:
            refused.append(Cell(row, col))
            raise NoMatchingMask("the best window finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_window, boxes=[box], assume_boxes=True)

    starts = _user_starts(result)
    assert starts[0].outcome == "rejected" and len(starts) >= 2


def _user_starts(result):
    return [seam for seam in result.seams if seam.kind == "user"]


def test_a_prompt_that_fits_one_window_starts_in_one_window() -> None:
    # Centred on the corner four cores share, but small enough that any of the four windows holds all of it.
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    starts = _user_starts(result)
    assert len(starts) == 1
    _cell, sent, _labels, given_box = world.given[0]
    assert given_box is not None
    assert len(sent) == 4, "the one start window was prompted with the whole prompt"
    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.95


def test_a_prompt_wider_than_any_window_still_starts_in_fewer_windows_than_cells_touched() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    assert 1 <= len(_user_starts(result)) < 4
    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.97


def test_cells_the_best_window_did_not_need_start_when_it_finds_nothing() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 100, 100, 0.0))
    box, points = client_prompt(2304, 2304, 100)
    refused = []

    def refuse_the_first_window(row, col, pts, lbls, box=None):
        if not refused:
            refused.append(Cell(row, col))
            raise NoMatchingMask("the best window finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_window, boxes=[box])

    starts = _user_starts(result)
    assert starts[0].outcome == "rejected" and starts[0].cell == refused[0]
    assert len(starts) >= 2 and starts[1].cell != refused[0]
    assert result.mask.any()


def test_a_click_outside_the_box_starts_its_own_cell() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 700, 700, 0.0))
    box, points = client_prompt(2048, 2048, 300)
    far = (2048, 2048 + 650)
    assert cell_of_point(*far) not in {cell_of_point(box[0], box[1]), cell_of_point(box[2], box[3])}
    assert cell_of_point(*far) != cell_of_point(*points[0]), "not the cell the first click already starts"

    result = grow_segmentation([*points, far], [], world.predict, boxes=[box])

    kinds = [(seam.kind, seam.cell) for seam in result.seams]
    first_edge = next((i for i, (kind, _cell) in enumerate(kinds) if kind == "edge"), len(kinds))
    started_up_front = {cell for kind, cell in kinds[:first_edge] if kind == "user"}
    assert cell_of_point(*far) in started_up_front, "started with the others, not reached later"


def test_the_prompt_is_the_box_and_the_clicks_in_the_window_and_nothing_else() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, points = client_prompt(2048, 2048, 300)

    grow_segmentation(points, [], world.predict, boxes=[box])

    _cell, sent, labels, given_box = world.given[0]
    assert given_box is not None
    assert len(sent) == 4 and set(labels) == {1}, "the four axis points; the box center is not sent as a click"


def test_the_box_center_is_still_a_click_when_the_client_sent_it() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, points = client_prompt(2048, 2048, 300)

    grow_segmentation([(2048, 2048), *points], [], world.predict, boxes=[box])

    assert len(world.given[0][1]) == 5


def test_a_cell_with_no_click_in_its_window_is_prompted_by_the_box_alone() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 300, 300, 0.0))
    box, _points = client_prompt(2048, 2048, 300)

    grow_segmentation([(2048 + 700, 2048)], [], world.predict, boxes=[box])

    _cell, sent, _labels, given_box = next(call for call in world.given if call[3] is not None)
    assert sent == [], "the click is outside this window, so the box is the whole prompt"


def test_the_new_prompt_recovers_a_disc() -> None:
    world = _RecordingWorld(ellipse(2048, 2048, 400, 400, 0.0))
    box, points = client_prompt(2048, 2048, 400)

    result = grow_segmentation(points, [], world.predict, boxes=[box])

    assert iou(world.to_truth_frame(result), world.truth_up) >= 0.97


def test_the_request_fails_only_when_every_start_cell_is_rejected_and_nothing_was_found() -> None:
    def refuse(row, col, points, labels, box=None):
        raise NoMatchingMask("nothing fits")

    box, points = client_prompt(2304, 2304, 300)
    walk = GrowthWalk(points, [], refuse, boxes=[box])
    walk.advance()

    with pytest.raises(NoMatchingMask):
        walk.result()


def test_one_start_cell_succeeding_is_enough() -> None:
    world = _RecordingWorld(ellipse(2304, 2304, 300, 300, 0.0))
    box, points = client_prompt(2304, 2304, 300)
    first = {"seen": False}

    def refuse_the_first_cell(row, col, pts, lbls, box=None):
        if not first["seen"]:
            first["seen"] = True
            raise NoMatchingMask("the first cell finds nothing")
        return world.predict(row, col, pts, lbls, box=box)

    result = grow_segmentation(points, [], refuse_the_first_cell, boxes=[box])

    assert result.mask.any()
    assert result.seams[0].outcome == "rejected"


def test_a_walk_that_is_waiting_for_tiles_has_not_failed() -> None:
    world = World(ellipse(2048, 2048, 300, 300, 0.0), available=[])
    box, points = client_prompt(2048, 2048, 300)
    walk = GrowthWalk(points, [], world.predict, boxes=[box])

    walk.advance()

    assert walk.take_requested(), "the cells need tiles it does not have"
    walk.result()
