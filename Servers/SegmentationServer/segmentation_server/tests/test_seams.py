"""Seams between cells: the range graph, the seed rectangle geometry, and the walk that uses them."""

from __future__ import annotations

from typing import List, Sequence, Tuple

import numpy as np
import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

from cell_world import World, circle_clicks, ellipse, iou, stroke
from segmentation_server.cell_grid import CORE_MARGIN, CORE_SIZE, Cell, cell_of_point, core_origin, mosaic_to_window
from segmentation_server.seams import (
    MAX_EXTRA_CLICKS,
    MIN_SEED_SIDE,
    SIDES,
    SeamGraph,
    Side,
    band_depths,
    box_center,
    contact_runs,
    edge_clearance_px,
    edge_coordinates,
    edge_key,
    edge_runs,
    inclusive_length,
    largest_rectangle,
    merge_ranges,
    novel_runs,
    order_runs,
    point_at_depth,
    seed_for_run,
)
from segmentation_server.tile_growth import grow_segmentation

PixelRange = Tuple[int, int]
Proposal = Tuple[Cell, Cell, PixelRange]

CAP = CORE_MARGIN - 8


# --- the range graph (restored from the tile-based walk and re-keyed to cells) -----------------


def _grid_edges(rows: int, cols: int) -> List[Tuple[Cell, Cell]]:
    edges: List[Tuple[Cell, Cell]] = []
    for row in range(rows):
        for col in range(cols):
            here = Cell(row, col)
            if col + 1 < cols:
                edges.append((here, Cell(row, col + 1)))
            if row + 1 < rows:
                edges.append((here, Cell(row + 1, col)))
    return edges


def _covered_pixels(ranges: Sequence[PixelRange]) -> int:
    return sum(end - start + 1 for start, end in ranges)


@st.composite
def _border_proposals(draw: st.DrawFn) -> Tuple[int, List[Proposal]]:
    rows = draw(st.integers(min_value=2, max_value=4))
    cols = draw(st.integers(min_value=2, max_value=4))
    border = draw(st.integers(min_value=8, max_value=64))
    edges = _grid_edges(rows, cols)
    count = draw(st.integers(min_value=1, max_value=40))
    proposals: List[Proposal] = []
    for _ in range(count):
        cell, neighbor = draw(st.sampled_from(edges))
        if draw(st.booleans()):
            cell, neighbor = neighbor, cell
        start = draw(st.integers(min_value=0, max_value=border - 1))
        end = draw(st.integers(min_value=start, max_value=border - 1))
        proposals.append((cell, neighbor, (start, end)))
    return border, proposals


def _travel(graph: SeamGraph, cell: Cell, neighbor: Cell, run: PixelRange) -> bool:
    return bool(novel_runs([run], graph.ranges(cell, neighbor)))


def test_one_cut_is_one_edge_whichever_cell_the_walk_arrives_from() -> None:
    assert edge_key(Cell(2, 3), Cell(2, 4)) == edge_key(Cell(2, 4), Cell(2, 3))


def test_overlap_cancels_travel() -> None:
    graph = SeamGraph()
    cell, neighbor = Cell(0, 0), Cell(0, 1)
    graph.claim(cell, neighbor, [(0, 10)])
    assert _travel(graph, cell, neighbor, (0, 10)) is False
    assert _travel(graph, neighbor, cell, (4, 12)) is False


def test_a_doubled_range_is_crossed_once_more() -> None:
    graph = SeamGraph()
    cell, neighbor = Cell(1, 2), Cell(1, 3)
    graph.claim(cell, neighbor, [(0, 7)])
    doubled = (0, 15)
    assert _travel(graph, cell, neighbor, doubled) is True
    graph.claim(cell, neighbor, [doubled])
    assert _travel(graph, cell, neighbor, doubled) is False
    assert _travel(graph, neighbor, cell, (0, 7)) is False
    assert list(graph.ranges(neighbor, cell)) == [(0, 15)]


def test_joining_two_ranges_is_crossed_once_more() -> None:
    graph = SeamGraph()
    cell, neighbor = Cell(2, 0), Cell(3, 0)
    graph.claim(cell, neighbor, [(0, 5), (20, 30)])
    bridge = (4, 22)
    assert _travel(graph, cell, neighbor, bridge) is True
    graph.claim(cell, neighbor, [bridge])
    assert _travel(graph, cell, neighbor, bridge) is False
    assert list(graph.ranges(cell, neighbor)) == [(0, 30)]


def test_a_disjoint_range_the_other_arm_of_a_c_is_still_crossed() -> None:
    graph = SeamGraph()
    cell, neighbor = Cell(0, 0), Cell(0, 1)
    graph.claim(cell, neighbor, [(0, 20)])
    assert _travel(graph, cell, neighbor, (40, 60)) is True


def test_one_pixel_extensions_wait_until_the_range_doubles() -> None:
    graph = SeamGraph()
    cell, neighbor = Cell(0, 0), Cell(0, 1)
    travels = []
    for end in range(10, 40):
        run = (0, end)
        if _travel(graph, cell, neighbor, run):
            graph.claim(cell, neighbor, [run])
            travels.append(run)
    assert travels == [(0, 10), (0, 21)]


def test_dropping_a_cell_forgets_only_its_edges() -> None:
    graph = SeamGraph()
    graph.claim(Cell(0, 0), Cell(0, 1), [(0, 5)])
    graph.claim(Cell(0, 1), Cell(0, 2), [(0, 5)])
    graph.drop_cell(Cell(0, 0))
    assert list(graph.ranges(Cell(0, 0), Cell(0, 1))) == []
    assert list(graph.ranges(Cell(0, 1), Cell(0, 2))) == [(0, 5)]


def test_the_copy_does_not_alias_the_original() -> None:
    graph = SeamGraph()
    graph.claim(Cell(0, 0), Cell(0, 1), [(0, 5)])
    clone = graph.copy()
    clone.claim(Cell(0, 0), Cell(0, 1), [(10, 20)])
    assert list(graph.ranges(Cell(0, 0), Cell(0, 1))) == [(0, 5)]


@given(_border_proposals())
@settings(max_examples=100, deadline=None, derandomize=True)
def test_random_ranges_on_a_cell_network_do_not_cycle(case: Tuple[int, List[Proposal]]) -> None:
    border, proposals = case
    graph = SeamGraph()
    edge_count = len({edge_key(cell, neighbor) for cell, neighbor, _run in proposals})
    accept_limit = max(1, edge_count) * border
    accepts = 0
    while True:
        progressed = False
        for cell, neighbor, run in proposals:
            already = graph.ranges(cell, neighbor)
            if not _travel(graph, cell, neighbor, run):
                continue
            overlapped = [prior for prior in already if prior[0] <= run[1] and run[0] <= prior[1]]
            if overlapped:
                doubled = len(overlapped) == 1 and inclusive_length(run) >= 2 * inclusive_length(overlapped[0])
                assert doubled or len(overlapped) >= 2
            before = _covered_pixels(already)
            graph.claim(cell, neighbor, [run])
            assert _covered_pixels(graph.ranges(cell, neighbor)) > before
            progressed = True
            accepts += 1
            assert accepts <= accept_limit
        if not progressed:
            break
    for cell, neighbor, run in proposals:
        assert _travel(graph, cell, neighbor, run) is False


@given(st.lists(st.tuples(st.integers(0, 60), st.integers(0, 60)), max_size=12))
def test_merged_ranges_are_sorted_disjoint_and_cover_the_same_pixels(raw) -> None:
    merged = merge_ranges(raw)
    covered = {i for a, b in raw for i in range(min(a, b), max(a, b) + 1)}
    assert {i for a, b in merged for i in range(a, b + 1)} == covered
    assert all(a <= b for a, b in merged)
    assert all(merged[i][1] + 1 < merged[i + 1][0] for i in range(len(merged) - 1))


def test_contact_runs_split_on_gaps() -> None:
    assert contact_runs([]) == []
    assert contact_runs([3, 4, 5, 9, 10, 20]) == [(3, 5), (9, 10), (20, 20)]
    assert contact_runs(np.array([7, 5, 6])) == [(5, 7)]


# --- the histogram rectangle -------------------------------------------------------------------


def _brute_force_area(heights: Sequence[int]) -> int:
    best = 0
    for first in range(len(heights)):
        floor = heights[first]
        for last in range(first, len(heights)):
            floor = min(floor, heights[last])
            best = max(best, floor * (last - first + 1))
    return best


@given(st.lists(st.integers(0, 12), min_size=1, max_size=16))
def test_the_largest_rectangle_is_the_largest_and_fits_under_the_histogram(heights) -> None:
    found = largest_rectangle(heights)
    expected = _brute_force_area(heights)
    if expected == 0:
        assert found is None
        return
    first, last, height = found
    assert height * (last - first + 1) == expected
    assert all(h >= height for h in heights[first:last + 1])


def test_the_largest_rectangle_prefers_the_earlier_start_on_a_tie() -> None:
    assert largest_rectangle([4, 4, 0, 4, 4]) == (0, 1, 4)


def test_a_tapering_arm_gets_a_deep_narrow_rectangle_not_one_spanning_the_whole_range() -> None:
    depths = [10, 20, 40, 80, 120, 160, 200, 200, 160, 120, 80, 40, 20, 10]
    first, last, height = largest_rectangle(depths)
    assert height * (last - first + 1) > min(depths) * len(depths)
    assert (first, last) == (3, 10) or height * (last - first + 1) >= 80 * 8


# --- band geometry on all four sides -----------------------------------------------------------


def _slab(side: Side, along: Tuple[int, int], depth: int) -> np.ndarray:
    """A core (Y-up, rows are Y) holding a rectangle of ``depth`` pixels on ``side``'s edge."""
    core = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    lo, hi = along
    if side is Side.EAST:
        core[lo:hi + 1, CORE_SIZE - depth:] = True
    elif side is Side.WEST:
        core[lo:hi + 1, :depth] = True
    elif side is Side.NORTH:
        core[CORE_SIZE - depth:, lo:hi + 1] = True
    else:
        core[:depth, lo:hi + 1] = True
    return core


@pytest.mark.parametrize("side", SIDES, ids=lambda s: s.name)
def test_a_slab_on_each_edge_is_found_as_one_run_with_the_right_depth(side: Side) -> None:
    core = _slab(side, (100, 299), 180)

    assert edge_runs(core, side) == [(100, 299)]
    depths = band_depths(core, side, CAP)
    assert set(depths[100:300].tolist()) == {180}
    assert int(depths[:100].sum()) == 0 and int(depths[300:].sum()) == 0
    for other in SIDES:
        if other is not side:
            assert edge_runs(core, other) == [] or other.opposite is side and False


@pytest.mark.parametrize("side", SIDES, ids=lambda s: s.name)
def test_the_seed_box_is_the_slab_exactly_on_each_side(side: Side) -> None:
    parent = Cell(4, 5)
    core = _slab(side, (100, 299), 180)
    run = edge_runs(core, side)[0]
    seed = seed_for_run(core, parent, side, run)

    x0, y0 = core_origin(parent)
    ox, oy = np.nonzero(core)[1], np.nonzero(core)[0]
    assert seed.box == (x0 + int(ox.min()), y0 + int(oy.min()), x0 + int(ox.max()), y0 + int(oy.max()))
    assert seed.click == box_center(seed.box)
    assert seed.coordinates == edge_coordinates(parent, side, run)
    cx, cy = seed.click
    assert core[cy - y0, cx - x0]


@pytest.mark.parametrize("side", SIDES, ids=lambda s: s.name)
def test_the_box_stays_inside_the_neighbors_window_and_clear_of_its_border(side: Side) -> None:
    parent = Cell(4, 5)
    neighbor = Cell(parent.row + side.step[0], parent.col + side.step[1])
    core = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)  # a field-sized mask: depth hits the cap
    run = edge_runs(core, side)[0]
    seed = seed_for_run(core, parent, side, run)

    assert seed.box is not None
    left, top = mosaic_to_window(neighbor, seed.box[0], seed.box[3])
    right, bottom = mosaic_to_window(neighbor, seed.box[2], seed.box[1])
    clearance = edge_clearance_px()
    assert min(left, top) >= clearance
    assert max(right, bottom) <= 1023 - clearance
    depth_axis = (right - left + 1) if side in (Side.EAST, Side.WEST) else (bottom - top + 1)
    assert depth_axis == CORE_MARGIN - clearance


def test_clearance_comes_from_the_environment(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENT_SEED_EDGE_CLEARANCE", raising=False)
    assert edge_clearance_px() == 8
    monkeypatch.setenv("SEGMENT_SEED_EDGE_CLEARANCE", "20")
    assert edge_clearance_px() == 20
    monkeypatch.setenv("SEGMENT_SEED_EDGE_CLEARANCE", "none")
    assert edge_clearance_px() == 8


def test_a_mask_that_stops_one_pixel_short_of_the_edge_is_not_a_crossing() -> None:
    core = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    core[100:300, 300:CORE_SIZE - 1] = True
    assert edge_runs(core, Side.EAST) == []


def test_runs_shorter_than_the_minimum_are_ignored_as_noise() -> None:
    core = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    core[10:10 + MIN_SEED_SIDE - 1, 400:] = True
    core[200:260, 400:] = True
    assert edge_runs(core, Side.EAST) == [(200, 259)]
    assert edge_runs(core, Side.EAST, min_length=1) == [(10, 10 + MIN_SEED_SIDE - 2), (200, 259)]


def test_a_range_whose_rectangle_is_too_shallow_gives_a_click_and_no_box() -> None:
    parent = Cell(2, 2)
    core = _slab(Side.EAST, (100, 199), MIN_SEED_SIDE - 1)
    run = edge_runs(core, Side.EAST)[0]

    seed = seed_for_run(core, parent, Side.EAST, run)

    assert seed.box is None
    x0, y0 = core_origin(parent)
    cx, cy = seed.click
    assert core[cy - y0, cx - x0]


def test_a_range_whose_rectangle_is_too_narrow_gives_a_click_and_no_box() -> None:
    core = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    core[100:100 + MIN_SEED_SIDE, 300:] = True  # exactly the minimum along the edge
    core[100:100 + MIN_SEED_SIDE, :] = False
    core[100:100 + MIN_SEED_SIDE, 300:] = True
    assert seed_for_run(core, Cell(0, 0), Side.EAST, edge_runs(core, Side.EAST)[0]).box is not None
    thin = np.zeros_like(core)
    thin[100:100 + MIN_SEED_SIDE, 300:] = True
    thin[100 + MIN_SEED_SIDE - 1, :] = False
    thin[100:100 + MIN_SEED_SIDE - 1, 300:] = True
    runs = edge_runs(thin, Side.EAST, min_length=1)
    assert seed_for_run(thin, Cell(0, 0), Side.EAST, runs[0], min_side=MIN_SEED_SIDE).box is None


def test_a_hole_in_the_range_splits_it_and_each_part_is_its_own_run() -> None:
    core = _slab(Side.NORTH, (50, 449), 200)
    core[:, 250:262] = False
    assert edge_runs(core, Side.NORTH) == [(50, 249), (262, 449)]


def test_order_runs_is_longest_first_then_lowest_start() -> None:
    assert order_runs([(10, 29), (100, 139), (40, 59), (0, 19)]) == [(100, 139), (0, 19), (10, 29), (40, 59)]


def test_seed_geometry_is_deterministic() -> None:
    core = _slab(Side.EAST, (100, 299), 150)
    run = edge_runs(core, Side.EAST)[0]
    assert seed_for_run(core, Cell(1, 1), Side.EAST, run) == seed_for_run(core, Cell(1, 1), Side.EAST, run)


def test_point_at_depth_lies_that_far_in_from_the_edge_on_every_side() -> None:
    parent = Cell(3, 3)
    x0, y0 = core_origin(parent)
    assert point_at_depth(parent, Side.EAST, 10, 5) == (x0 + 511 - 5, y0 + 10)
    assert point_at_depth(parent, Side.WEST, 10, 5) == (x0 + 5, y0 + 10)
    assert point_at_depth(parent, Side.NORTH, 10, 5) == (x0 + 10, y0 + 511 - 5)
    assert point_at_depth(parent, Side.SOUTH, 10, 5) == (x0 + 10, y0 + 5)


def test_the_extra_click_cap_is_the_old_seed_cap() -> None:
    assert MAX_EXTRA_CLICKS == 24


def test_seed_box_agrees_with_band_depth_for_a_random_staircase() -> None:
    rng = np.random.default_rng(3)
    core = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
    for row in range(60, 200):
        core[row, CORE_SIZE - int(rng.integers(20, 200)):] = True
    run = edge_runs(core, Side.EAST)[0]
    seed = seed_for_run(core, Cell(0, 0), Side.EAST, run)
    x0, y0 = core_origin(Cell(0, 0))
    bx0, by0, bx1, by1 = seed.box
    assert core[by0 - y0:by1 - y0 + 1, bx0 - x0:bx1 - x0 + 1].all()
    assert bx1 - x0 == CORE_SIZE - 1


# --- the walk ----------------------------------------------------------------------------------


class _RecordingWorld(World):
    """World that records the box each cell was given."""

    def __init__(self, truth_up, **kwargs) -> None:
        super().__init__(truth_up, **kwargs)
        self.given = []

    def predict(self, row, col, points, labels, box=None):
        self.given.append((Cell(row, col), list(points), list(labels), box))
        return super().predict(row, col, points, labels, box=box)


def test_every_continuation_is_given_a_box_that_lies_inside_the_object_and_its_center_click() -> None:
    truth = ellipse(2048, 2048, 650, 300, 0.0)
    world = _RecordingWorld(truth)

    result = grow_segmentation(circle_clicks(2048, 2048, 90), [], world.predict)

    edge_seams = [seam for seam in result.seams if seam.kind == "edge" and seam.outcome == "accepted"]
    boxed = [seam for seam in edge_seams if seam.box is not None]
    assert boxed
    for seam in boxed:
        x0, y0, x1, y1 = seam.box
        assert truth[y0:y1 + 1, x0:x1 + 1].all()
        assert seam.clicks[0] == box_center(seam.box)
    for seam in edge_seams:
        # With or without a box, the first click is always on the object.
        assert truth[seam.clicks[0][1], seam.clicks[0][0]]
    assert iou(world.to_truth_frame(result), truth) >= 0.97


def test_the_starting_cell_is_prompted_by_the_clicks_and_never_by_an_edge() -> None:
    truth = ellipse(2048, 2048, 650, 300, 0.0)
    world = _RecordingWorld(truth)

    result = grow_segmentation(circle_clicks(2048, 2048, 90), [], world.predict)

    first = result.seams[0]
    assert first.kind == "user" and first.parent is None and first.box is None
    start = cell_of_point(2048, 2048)
    assert world.given[0][0] == start and world.given[0][3] is None
    assert len(world.given[0][1]) == 9


def test_the_original_boxes_go_only_to_cells_the_user_started() -> None:
    truth = ellipse(2048, 2048, 650, 300, 0.0)
    world = _RecordingWorld(truth)
    start_box = [(2000, 2000, 2096, 2096)]

    result = grow_segmentation(circle_clicks(2048, 2048, 90), [], world.predict, boxes=start_box)

    user_boxes = [box for cell, _p, _l, box in world.given[:1]]
    assert user_boxes[0] is not None
    for (cell, _points, _labels, box), seam in zip(world.given[1:], result.seams[1:]):
        assert seam.kind == "edge"
        assert box is None or box != world.given[0][3]


def test_a_mask_with_a_crossing_on_every_edge_calls_each_edge_once() -> None:
    truth = ellipse(2048, 2048, 500, 500, 0.0)
    world = _RecordingWorld(truth)

    result = grow_segmentation(circle_clicks(2048, 2048, 100), [], world.predict)

    keys = [(seam.cell, seam.parent) for seam in result.seams if seam.kind == "edge"]
    assert len(keys) == len(set(keys)), "an edge was crossed twice for the same range"


def test_two_arms_crossing_one_edge_give_one_box_and_one_extra_click() -> None:
    left, right, bottom = 1500, 1700, 1400
    truth = stroke([(left, 2900), (left, bottom), (right, bottom), (right, 2900)], 70)
    world = _RecordingWorld(truth)

    # Start on the connector: the starting core holds both arms, so its north edge shows two ranges.
    result = grow_segmentation(circle_clicks(1600, bottom, 25), [], world.predict)

    crossings = [seam for seam in result.seams if seam.kind == "edge" and len(seam.runs) == 2]
    assert crossings, "no edge saw both arms"
    seam = crossings[0]
    assert seam.side is Side.NORTH
    assert seam.box is not None
    assert len(seam.clicks) == 2
    assert seam.clicks[0] == box_center(seam.box)
    assert seam.box[0] == 1465  # the lower-starting of two equally long ranges is the boxed one
    assert seam.clicks[1][0] in range(1665, 1736)
    attempts_into_that_cell = [s for s in result.seams if s.cell == seam.cell and s.parent == seam.parent]
    assert len(attempts_into_that_cell) == 1, "two ranges on one edge must be one prediction"


def test_a_speckled_edge_does_not_start_predictions() -> None:
    truth = ellipse(1900, 2048, 150, 150, 0.0)
    specks = np.zeros_like(truth)
    for y in range(1960, 2140, 9):
        specks[y:y + 4, 2050:2054] = True  # 4 px slivers, each far under the minimum range
    world = _RecordingWorld(truth | specks)

    result = grow_segmentation(circle_clicks(1900, 2048, 60), [], world.predict)

    assert all(seam.kind == "user" or inclusive_length(seam.runs[0]) >= MIN_SEED_SIDE for seam in result.seams)


def test_a_cell_is_never_predicted_again_for_a_range_it_already_covers() -> None:
    truth = ellipse(2048, 2048, 150, 150, 0.0)  # fits inside one core
    world = _RecordingWorld(truth)

    result = grow_segmentation(circle_clicks(2048, 2048, 60), [], world.predict)

    assert len(world.given) == 1
    assert result.edges == {}


def test_the_result_carries_the_graph_and_the_dump_records_it(tmp_path, monkeypatch) -> None:
    import json

    from segmentation_server import debug_dump

    monkeypatch.setenv(debug_dump.ENV_ENABLE, "1")
    truth = ellipse(2048, 2048, 650, 300, 0.0)
    world = _RecordingWorld(truth)
    result = grow_segmentation(circle_clicks(2048, 2048, 90), [], world.predict)
    assert result.edges

    path = debug_dump.dump_growth(
        "seam0001",
        fused_mask=result.mask,
        origin=(result.origin_x, result.origin_y),
        cells=result.cells,
        uploaded=[],
        requested=[],
        foreground=[],
        background=[],
        score=result.score,
        directory=tmp_path,
        seams=result.seams,
        edges=result.edges,
    )

    with np.load(path) as data:
        meta = json.loads(str(data["meta"]))
    assert len(meta["seams"]) == len(result.seams)
    assert meta["seams"][0]["kind"] == "user"
    edge = next(seam for seam in meta["seams"] if seam["kind"] == "edge")
    assert edge["side"] in {"EAST", "NORTH", "WEST", "SOUTH"} and edge["box"] is not None
    assert len(meta["edges"]) == len(result.edges)
    assert all(entry["runs"] for entry in meta["edges"])
