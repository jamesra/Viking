"""Property checks for border ranges on a synthetic tile network.

Overlap with a range already searched cancels travel to the neighbor. Travel
is allowed again when the new range is at least twice as long as the one it
overlaps, or when it overlaps two stored ranges and joins them. Feeding the
same random ranges back through that rule must stop.
"""

from __future__ import annotations

from typing import List, Sequence, Tuple

from hypothesis import given, settings
from hypothesis import strategies as st

from segmentation_server.tile_growth import SeamGraph, TileIndex, _novel_runs

PixelRange = Tuple[int, int]
Proposal = Tuple[TileIndex, TileIndex, PixelRange]


def _grid_edges(rows: int, cols: int) -> List[Tuple[TileIndex, TileIndex]]:
    edges: List[Tuple[TileIndex, TileIndex]] = []
    for row in range(rows):
        for col in range(cols):
            here = TileIndex(row, col)
            if col + 1 < cols:
                edges.append((here, TileIndex(row, col + 1)))
            if row + 1 < rows:
                edges.append((here, TileIndex(row + 1, col)))
    return edges


def _covered_pixels(ranges: Sequence[PixelRange]) -> int:
    return sum(end - start + 1 for start, end in ranges)


def _undirected_edge(tile: TileIndex, neighbor: TileIndex) -> Tuple[TileIndex, TileIndex]:
    left, right = (tile.row, tile.col), (neighbor.row, neighbor.col)
    if right < left:
        return neighbor, tile
    return tile, neighbor


@st.composite
def _border_proposals(draw: st.DrawFn) -> Tuple[int, List[Proposal]]:
    """Random inclusive ranges on the borders of a small tile grid."""
    rows = draw(st.integers(min_value=2, max_value=4))
    cols = draw(st.integers(min_value=2, max_value=4))
    border = draw(st.integers(min_value=8, max_value=64))
    edges = _grid_edges(rows, cols)
    count = draw(st.integers(min_value=1, max_value=40))
    proposals: List[Proposal] = []
    for _ in range(count):
        tile, neighbor = draw(st.sampled_from(edges))
        if draw(st.booleans()):
            tile, neighbor = neighbor, tile
        start = draw(st.integers(min_value=0, max_value=border - 1))
        end = draw(st.integers(min_value=start, max_value=border - 1))
        proposals.append((tile, neighbor, (start, end)))
    return border, proposals


def _travel(graph: SeamGraph, tile: TileIndex, neighbor: TileIndex, run: PixelRange) -> bool:
    return bool(_novel_runs([run], graph.ranges(tile, neighbor)))


def test_overlap_cancels_travel() -> None:
    graph = SeamGraph()
    tile, neighbor = TileIndex(0, 0), TileIndex(0, 1)
    graph.claim(tile, neighbor, [(0, 10)])
    assert _travel(graph, tile, neighbor, (0, 10)) is False
    assert _travel(graph, neighbor, tile, (4, 12)) is False


def test_a_doubled_range_is_searched_once_more() -> None:
    graph = SeamGraph()
    tile, neighbor = TileIndex(1, 2), TileIndex(1, 3)
    graph.claim(tile, neighbor, [(0, 7)])
    doubled = (0, 15)
    assert _travel(graph, tile, neighbor, doubled) is True
    graph.claim(tile, neighbor, [doubled])
    assert _travel(graph, tile, neighbor, doubled) is False
    assert _travel(graph, neighbor, tile, (0, 7)) is False
    assert graph.ranges(neighbor, tile) == [(0, 15)]


def test_joining_two_ranges_is_searched_once_more() -> None:
    graph = SeamGraph()
    tile, neighbor = TileIndex(2, 0), TileIndex(3, 0)
    graph.claim(tile, neighbor, [(0, 5), (20, 30)])
    bridge = (4, 22)
    assert _travel(graph, tile, neighbor, bridge) is True
    graph.claim(tile, neighbor, [bridge])
    assert _travel(graph, tile, neighbor, bridge) is False
    assert graph.ranges(tile, neighbor) == [(0, 30)]


def test_one_pixel_extensions_wait_until_the_range_doubles() -> None:
    """Growing a searched border by one pixel does not cross again until it doubles."""
    graph = SeamGraph()
    tile, neighbor = TileIndex(0, 0), TileIndex(0, 1)
    travels = []
    for end in range(10, 40):
        run = (0, end)
        if _travel(graph, tile, neighbor, run):
            graph.claim(tile, neighbor, [run])
            travels.append(run)
    # (0, 10) is 11 pixels. The next search is (0, 21), 22 pixels, then it stops.
    assert travels == [(0, 10), (0, 21)]


@given(_border_proposals())
@settings(max_examples=100, deadline=None, derandomize=True)
def test_random_ranges_on_a_tile_network_do_not_cycle(case: Tuple[int, List[Proposal]]) -> None:
    border, proposals = case
    graph = SeamGraph()
    edge_count = len({_undirected_edge(tile, neighbor) for tile, neighbor, _run in proposals})
    # Every accepted search covers at least one new pixel, so this bound is
    # the whole border of every distinct cut in the proposal list.
    accept_limit = max(1, edge_count) * border
    accepts = 0
    while True:
        progressed = False
        for tile, neighbor, run in proposals:
            already = graph.ranges(tile, neighbor)
            if not _travel(graph, tile, neighbor, run):
                continue
            overlapped = [
                prior for prior in already if prior[0] <= run[1] and run[0] <= prior[1]
            ]
            if overlapped:
                doubled = len(overlapped) == 1 and (run[1] - run[0] + 1) >= 2 * (
                    overlapped[0][1] - overlapped[0][0] + 1
                )
                merged = len(overlapped) >= 2
                assert doubled or merged
            before = _covered_pixels(already)
            graph.claim(tile, neighbor, [run])
            assert _covered_pixels(graph.ranges(tile, neighbor)) > before
            progressed = True
            accepts += 1
            assert accepts <= accept_limit
        if not progressed:
            break
    for tile, neighbor, run in proposals:
        assert _travel(graph, tile, neighbor, run) is False
