"""Seams between cells: where the mask crosses a core edge, and the box that carries it across.

A growth walk moves from a cell P to its neighbor N across the edge their cores share. The edge
is the line where the mask in P's outermost core row (or column) is set; each stretch of set
pixels along it is a **range**. This module keeps two things:

* ``SeamGraph``: an undirected graph whose nodes are cells and whose edges hold the merged
  ranges already crossed. A range that overlaps one already crossed is not crossed again, unless
  it is at least twice as long or it joins two stored ranges. That is what stops a mask that
  loops back over a seam from being predicted forever. (It is the graph the tile-based walk had
  before the cell grid; the cell walk keys it by ``Cell`` and measures ranges in mosaic pixels so
  a range seen from either side compares directly.)
* The **seed rectangle**: neighbor N sees the last 256 px of P's core as its own outer margin
  band. For a range on the edge, project from the edge into that band one pixel at a time until
  a non-mask pixel, which gives a depth for every position along the range; the largest rectangle
  under those depths, with one side on the edge, is the box that tells SAM2 "this much is already
  object, continue it". Everything here is numpy only, so it is tested without a GPU.

Mosaic coordinates are X right, Y up. A core array is Y-up too, so row ``i`` is mosaic
``core_y0 + i`` and column ``j`` is ``core_x0 + j``.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import Enum
from typing import Dict, List, Optional, Sequence, Tuple

import numpy as np
from numpy.typing import NDArray

from segmentation_server.cell_grid import CORE_MARGIN, CORE_SIZE, Cell, MaskArray, Point, core_origin

PixelRange = Tuple[int, int]
EdgeKey = Tuple[int, int, int, int]
Box = Tuple[int, int, int, int]

# A range or a rectangle side shorter than this is noise on a speckled mask edge, not a crossing.
MIN_SEED_SIDE = 16
# At most this many extra point clicks go in one prompt, longest ranges first.
MAX_EXTRA_CLICKS = 24
# The band N sees of P's core is CORE_MARGIN deep. The rectangle stops this far short of the far
# edge of the band, which is the border of N's window: SAM2 treats an image border as an object
# boundary, so a mask can stop short of a box that touches it. Override with SEGMENT_SEED_EDGE_CLEARANCE.
DEFAULT_EDGE_CLEARANCE_PX = 8


def edge_clearance_px() -> int:
    """Read SEGMENT_SEED_EDGE_CLEARANCE. Missing or invalid values use the default."""
    raw = os.environ.get("SEGMENT_SEED_EDGE_CLEARANCE", "").strip()
    if not raw:
        return DEFAULT_EDGE_CLEARANCE_PX
    try:
        return max(0, min(CORE_MARGIN - 1, int(raw)))
    except ValueError:
        return DEFAULT_EDGE_CLEARANCE_PX


class Side(Enum):
    """Which edge of the parent cell's core faces the neighbor, as ``(drow, dcol)`` to that neighbor."""

    EAST = (0, 1)
    NORTH = (1, 0)
    WEST = (0, -1)
    SOUTH = (-1, 0)

    @property
    def step(self) -> Tuple[int, int]:
        return self.value

    @property
    def opposite(self) -> "Side":
        return Side((-self.value[0], -self.value[1]))


SIDES: Tuple[Side, ...] = (Side.EAST, Side.NORTH, Side.WEST, Side.SOUTH)


# --- ranges and the graph ---------------------------------------------------------------------


def edge_key(cell: Cell, neighbor: Cell) -> EdgeKey:
    """One key for the cut, whichever cell the walk arrived from."""
    left = (int(cell.row), int(cell.col))
    right = (int(neighbor.row), int(neighbor.col))
    if right < left:
        left, right = right, left
    return left[0], left[1], right[0], right[1]


def inclusive_length(run: PixelRange) -> int:
    """Pixel count of an inclusive range."""
    start, end = int(run[0]), int(run[1])
    if start > end:
        start, end = end, start
    return end - start + 1


def merge_ranges(runs: Sequence[PixelRange]) -> List[PixelRange]:
    """Sort inclusive ranges and collapse overlaps and ranges that touch."""
    normalized: List[PixelRange] = []
    for start, end in runs:
        start_i, end_i = int(start), int(end)
        if start_i > end_i:
            start_i, end_i = end_i, start_i
        normalized.append((start_i, end_i))
    ordered = sorted(normalized)
    if not ordered:
        return []
    merged: List[PixelRange] = [ordered[0]]
    for start, end in ordered[1:]:
        previous_start, previous_end = merged[-1]
        if start <= previous_end + 1:
            merged[-1] = (previous_start, max(previous_end, end))
        else:
            merged.append((start, end))
    return merged


def ranges_overlap(left: PixelRange, right: PixelRange) -> bool:
    return left[0] <= right[1] and right[0] <= left[1]


def repeats_search(run: PixelRange, already: Sequence[PixelRange]) -> bool:
    """True when an overlapping range is a new crossing rather than a loop.

    Overlap with a range already crossed cancels travel. The crossing is repeated when ``run`` is
    at least twice as long as the one stored range it overlaps, or when it overlaps two or more
    stored ranges and so joins them into one stretch.
    """
    overlapped = [prior for prior in already if ranges_overlap(run, prior)]
    if len(overlapped) >= 2:
        return True
    if len(overlapped) == 1:
        return inclusive_length(run) >= 2 * inclusive_length(overlapped[0])
    return False


def novel_runs(runs: Sequence[PixelRange], already: Sequence[PixelRange]) -> List[PixelRange]:
    """The runs that still cross: those that miss every stored range, or that ``repeats_search``."""
    return [
        run
        for run in runs
        if not any(ranges_overlap(run, prior) for prior in already) or repeats_search(run, already)
    ]


def contact_runs(indices: Sequence[int]) -> List[PixelRange]:
    """Inclusive start and end of each contiguous stretch of ``indices``."""
    if len(indices) == 0:
        return []
    ordered = sorted({int(i) for i in indices})
    runs: List[PixelRange] = []
    run_start = previous = ordered[0]
    for index in ordered[1:]:
        if index - previous > 1:
            runs.append((run_start, previous))
            run_start = index
        previous = index
    runs.append((run_start, previous))
    return runs


@dataclass
class SeamGraph:
    """Undirected borders between cells. Each edge holds the merged ranges already crossed.

    Ranges are inclusive mosaic coordinates along the shared border (X for a border between
    east-west neighbors, Y for north-south), so a range measured from either cell compares
    directly. One shared cut is one edge whichever side the walk arrives from.
    """

    edges: Dict[EdgeKey, List[PixelRange]] = field(default_factory=dict)

    def ranges(self, cell: Cell, neighbor: Cell) -> Sequence[PixelRange]:
        """Ranges already crossed on the border between these two cells."""
        return self.edges.get(edge_key(cell, neighbor), ())

    def claim(self, cell: Cell, neighbor: Cell, runs: Sequence[PixelRange]) -> None:
        """Merge ``runs`` into the one edge that joins these cells."""
        if not runs:
            return
        key = edge_key(cell, neighbor)
        self.edges[key] = merge_ranges([*self.edges.get(key, ()), *runs])

    def copy(self) -> "SeamGraph":
        return SeamGraph(edges={key: list(runs) for key, runs in self.edges.items()})


# --- contacts and the seed rectangle ----------------------------------------------------------


def _inward(core_up: MaskArray, side: Side) -> NDArray[np.bool_]:
    """The core re-oriented so axis 0 runs along the edge and axis 1 runs inward from it.

    Column 0 is the row or column of the core that touches the neighbor on ``side``.
    """
    if side is Side.EAST:
        return core_up[:, ::-1]
    if side is Side.WEST:
        return core_up
    if side is Side.NORTH:
        return core_up[::-1, :].T
    return core_up.T


def edge_runs(core_up: MaskArray, side: Side, min_length: int = MIN_SEED_SIDE) -> List[PixelRange]:
    """Runs of set pixels along the core's outermost row or column facing ``side``.

    Returned as inclusive local indices along the edge (0 to 511). Only the outermost pixel is
    contact: a band wider than one pixel, or a mask that stopped short of the edge, made a mask
    that never reached the cut look like a crossing. Runs shorter than ``min_length`` are noise.
    """
    if core_up.size == 0:
        return []
    edge = _inward(core_up, side)[:, 0]
    return [run for run in contact_runs(np.flatnonzero(edge)) if inclusive_length(run) >= min_length]


def band_depths(
    core_up: MaskArray, side: Side, depth_cap: int, rows: Optional[Sequence[PixelRange]] = None
) -> NDArray[np.int32]:
    """For each position along the edge, how many set pixels run inward from the edge, up to ``depth_cap``.

    This is the projection "pixel by pixel until a non-mask pixel", for every position at once:
    the depth is the index of the first unset pixel going inward (``depth_cap`` when there is none).
    ``rows`` limits the work to the given inclusive local ranges along the edge; positions outside
    them are left at 0. The walk only needs the depths under the ranges it is about to cross.
    """
    inward = _inward(core_up, side)[:, :depth_cap]
    depths = np.zeros(inward.shape[0], dtype=np.int32)
    spans = [(0, inward.shape[0] - 1)] if rows is None else rows
    for first, last in spans:
        block = inward[first:last + 1]
        unset = ~block
        first_unset = unset.argmax(axis=1)
        depths[first:last + 1] = np.where(unset.any(axis=1), first_unset, block.shape[1])
    return depths


def largest_rectangle(depths: Sequence[int]) -> Optional[Tuple[int, int, int]]:
    """Largest-area rectangle standing on the baseline under a histogram.

    ``depths[i]`` is the height at position ``i``. Returns ``(first, last, height)`` with
    ``first`` and ``last`` inclusive indices into ``depths``, or None when every height is 0.
    Ties go to the larger area's earlier start, so the answer is deterministic.
    """
    best: Optional[Tuple[int, int, int]] = None
    best_area = 0
    stack: List[Tuple[int, int]] = []  # (start index, height), heights strictly increasing
    padded = [int(d) for d in depths] + [0]
    for index, height in enumerate(padded):
        start = index
        while stack and stack[-1][1] >= height:
            start, top = stack.pop()
            area = top * (index - start)
            if top > 0 and (area > best_area or (area == best_area and best is not None and start < best[0])):
                best, best_area = (start, index - 1, top), area
        stack.append((start, height))
    return best


def seed_box(parent: Cell, side: Side, first: int, last: int, height: int) -> Box:
    """Mosaic box ``(x_min, y_min, x_max, y_max)``, Y up and inclusive, for a rectangle in the band.

    ``first`` and ``last`` are local indices along the parent core's edge on ``side``; the
    rectangle stands on that edge and reaches ``height`` pixels into the core.
    """
    core_x0, core_y0 = core_origin(parent)
    last_index = CORE_SIZE - 1
    if side is Side.EAST:
        return core_x0 + last_index - height + 1, core_y0 + first, core_x0 + last_index, core_y0 + last
    if side is Side.WEST:
        return core_x0, core_y0 + first, core_x0 + height - 1, core_y0 + last
    if side is Side.NORTH:
        return core_x0 + first, core_y0 + last_index - height + 1, core_x0 + last, core_y0 + last_index
    return core_x0 + first, core_y0, core_x0 + last, core_y0 + height - 1


def point_at_depth(parent: Cell, side: Side, along: int, depth: int) -> Point:
    """Mosaic point ``depth`` pixels in from the parent core's edge on ``side``, at local index ``along``."""
    core_x0, core_y0 = core_origin(parent)
    last_index = CORE_SIZE - 1
    if side is Side.EAST:
        return core_x0 + last_index - depth, core_y0 + along
    if side is Side.WEST:
        return core_x0 + depth, core_y0 + along
    if side is Side.NORTH:
        return core_x0 + along, core_y0 + last_index - depth
    return core_x0 + along, core_y0 + depth


def edge_coordinates(parent: Cell, side: Side, run: PixelRange) -> PixelRange:
    """A run's local indices as mosaic coordinates along the edge (X for north/south, Y for east/west)."""
    core_x0, core_y0 = core_origin(parent)
    base = core_x0 if side in (Side.NORTH, Side.SOUTH) else core_y0
    return base + run[0], base + run[1]


def box_center(box: Box) -> Point:
    """Integer mosaic center of an inclusive box."""
    return (box[0] + box[2]) // 2, (box[1] + box[3]) // 2


@dataclass(frozen=True)
class RunSeed:
    """What one range on an edge contributes to the prompt for the neighbor."""

    run: PixelRange
    coordinates: PixelRange
    box: Optional[Box]
    click: Point

    @property
    def length(self) -> int:
        return inclusive_length(self.run)


def seed_for_run(
    core_up: MaskArray,
    parent: Cell,
    side: Side,
    run: PixelRange,
    depths: Optional[NDArray[np.int32]] = None,
    min_side: int = MIN_SEED_SIDE,
    clearance: Optional[int] = None,
) -> RunSeed:
    """The box and click one range gives the neighbor.

    The box is the largest rectangle on the edge under the range's depths; it is None when that
    rectangle is under ``min_side`` on either side. The click is the box center, or, with no
    usable box, the middle of the range a little way in from the edge. Depth is capped short of
    the far edge of the band by ``clearance`` pixels.
    """
    cap = CORE_MARGIN - (edge_clearance_px() if clearance is None else clearance)
    heights = band_depths(core_up, side, cap) if depths is None else depths
    window = heights[run[0]:run[1] + 1]
    found = largest_rectangle(window)
    box: Optional[Box] = None
    if found is not None:
        first, last, height = found
        if (last - first + 1) >= min_side and height >= min_side:
            box = seed_box(parent, side, run[0] + first, run[0] + last, height)
    coordinates = edge_coordinates(parent, side, run)
    if box is not None:
        return RunSeed(run, coordinates, box, box_center(box))
    middle = (run[0] + run[1]) // 2
    return RunSeed(run, coordinates, None, point_at_depth(parent, side, middle, int(heights[middle]) // 2))


def order_runs(runs: Sequence[PixelRange]) -> List[PixelRange]:
    """Longest first, then lowest start: the order seeds are chosen in, so the result is deterministic."""
    return sorted(runs, key=lambda run: (-inclusive_length(run), run[0]))
