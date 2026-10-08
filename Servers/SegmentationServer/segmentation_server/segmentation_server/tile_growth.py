"""Grow a segmentation across overlapping cells.

A cell is a 1024 window at a 512 stride (see ``cell_grid``). SAM2 predicts a whole
window. Pieces of the answer that hold no positive click are dropped, then the central 512
core is ORed into the result ``G``, and so is every outer-256 px margin pixel whose SAM2
logit is at least ``DEFAULT_MARGIN_LOGIT_MIN`` (a graded trust: the margin counts only where
the model is clearly sure). Each pixel is stored in the core of the cell that owns it.
Because windows overlap, a confident margin pixel lets a neighbor that sees more of the
object add to the cell centered on it. A neighbor's margin does not override the owner:
once a cell has predicted, core pixels where its own logit is below
``DEFAULT_OWNER_VETO_LOGIT`` are removed from ``G`` if a neighbor put them there, and later
neighbors cannot add them. Pixels the owner itself accepted are never removed.

The walk:

1. Start in every cell the client's prompt touches: each cell whose core holds part of a box
   and each cell that owns a foreground point. Each is prompted with the box (see
   ``GrowthWalk._start_box``) and the clicks in its window. A core the box covers entirely is
   taken as object without a prediction. The request fails only if every start cell is
   rejected and nothing else is found.
2. OR the kept core, and the confident margin pixels, into ``G``.
3. When ``G`` in a core touches an edge it shares with a neighbor, that is a **range**: a
   stretch of set pixels along the edge (``seams.edge_runs``). The neighbor sees the last 256
   px of this core as its own outer margin band. Project from the edge into that band, pixel by
   pixel, until a non-mask pixel; the largest rectangle under those depths with one side on the
   edge is the neighbor's **box prompt**, with a click at its center. The longest range on the
   edge gives the box; the other ranges on that edge add a click each (``seams.seed_for_run``).
   Corners are not followed: a mask has to reach an edge to continue.
4. Ranges already crossed live in a graph whose nodes are cells and whose edges hold the merged
   ranges (``seams.SeamGraph``). A range that overlaps one already crossed is not crossed
   again unless it is at least twice as long or joins two stored ranges, so a mask that loops
   back over a seam, such as the return arm of a hairpin, is crossed where it is new and the
   walk cannot go around forever. A prediction that finds no mask fitting its box claims its
   ranges all the same, so that cell is not tried again until the range changes.
5. Repeat until nothing is queued, then start from any foreground click still outside
   ``G``. A cell started from the client's prompt is prompted with its box and clicks, not an
   edge. A box bigger than a window cannot be a prompt for any one cell, so such a cell is
   given the box's overlap with its core instead.

Aligned tiles the server does not hold are collected by ``GrowthWalk.take_requested`` and the
walk continues without that cell. The walk object stays alive while the caller fetches the
tiles; ``GrowthWalk.resume`` then re-queues only the cells that were waiting, so nothing that
was already predicted is predicted again and ``G`` only ever gains pixels.
"""

from __future__ import annotations

import copy
import logging
import os
from collections import deque
from dataclasses import dataclass, field
from typing import Callable, Deque, Dict, Iterable, Iterator, List, Mapping, Optional, Protocol, Sequence, Set, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.seams import (
    MAX_EXTRA_CLICKS,
    PixelRange,
    RunSeed,
    SIDES,
    SeamGraph,
    Side,
    band_depths,
    box_center,
    edge_clearance_px,
    edge_coordinates,
    edge_runs,
    novel_runs,
    order_runs,
    seed_for_run,
)
from segmentation_server.cell_grid import (
    CELL_SIZE,
    CELL_STRIDE,
    CORE_MARGIN,
    CORE_SIZE,
    TILE_SIZE,
    Box,
    Canvas,
    Cell,
    MaskArray,
    Point,
    TileIndex,
    cell_of_point,
    core_origin,
    cores_under_box,
    in_window,
    mosaic_to_window,
    tile_of_point,
    tiles_for_cell,
)

__all__ = [
    "TILE_SIZE",
    "Cell",
    "CellPrediction",
    "CellPredict",
    "GrowthCancelled",
    "GrowthResult",
    "GrowthWalk",
    "PredictUnavailable",
    "TileIndex",
    "grow_segmentation",
    "max_requested_tiles_from_env",
    "tile_of_point",
    "window_box",
]

logger = logging.getLogger(__name__)

DEFAULT_MAX_REQUESTED_TILES = 8
# A hairpin returns down cells the first arm already crossed, and each of them must be
# predicted again once the returning arm reaches its neighbor. A re-predict reuses the
# cached image embedding, so it costs only the decoder. The per-cell cap and the total
# budget below stop a thin edge that keeps producing fresh seeds from predicting forever.
MAX_PREDICTIONS_PER_CELL = 4
DEFAULT_MAX_PREDICTIONS = 96
# Hard stop for the walk, in distinct cells. 48 cores cover about 3.5k x 3.5k pixels.
DEFAULT_MAX_CELLS = 48
# SAM2 decides a pixel is object when its logit is above 0. A pixel in the outer 256 px
# margin of a window is accepted only when its logit is at least this, so a weak or
# edge-affected margin answer is left to the neighbor cell that sees the pixel centered.
# Core pixels need only the mask. Override with SEGMENT_MARGIN_LOGIT.
DEFAULT_MARGIN_LOGIT_MIN = 1.5
# A neighbor's window ends in a straight line, and SAM2 tends to carry a mask right up to
# the image border, so a margin pixel can be confidently wrong there. The cell whose core
# holds the pixel sees it centered; when that cell has predicted and its logit is below this
# value the pixel is vetoed. Override with SEGMENT_OWNER_VETO_LOGIT.
DEFAULT_OWNER_VETO_LOGIT = -1.0
# A box prompt clipped to less than this on a side says nothing useful about the object.
MIN_BOX_SIDE = 16

class CellPredict(Protocol):
    """What the walk calls to predict one cell.

    ``points`` are window-local Y-down click positions with ``labels`` (1 foreground, 0
    background). ``box`` is passed only when the prompt has one: a window-local Y-down inclusive
    ``(x0, y0, x1, y1)``, and the answer must cover at least 95% of it (see ``mask_utils.select_mask``).
    Returns the chosen mask of the window, its logits or None, and its score. May raise
    ``PredictUnavailable`` (tiles missing) or ``NoMatchingMask`` (no candidate fits the prompt).
    """

    def __call__(
        self,
        row: int,
        col: int,
        points: Sequence[Point],
        labels: Sequence[int],
        box: Optional[Tuple[int, int, int, int]] = None,
    ) -> Tuple[MaskArray, Optional[NDArray], float]: ...


class _Rejected:
    """Marker: the prediction ran but no mask fit its prompt."""


_REJECTED = _Rejected()


class PredictUnavailable(Exception):
    """This cell needs a new SAM2 call and the aligned tiles for its window are not held.

    ``tiles`` names the missing aligned tiles. Growth asks the client for them and
    carries on without this cell.
    """

    def __init__(self, message: str = "", tiles: Sequence[TileIndex] = ()) -> None:
        super().__init__(message)
        self.tiles: Tuple[TileIndex, ...] = tuple(tiles)


class GrowthCancelled(Exception):
    """The walk stopped because the caller asked it to, before the next SAM2 call."""


@dataclass
class CellPrediction:
    """What one cell contributed: its core mask (Y-down, 512x512) and SAM2 score.

    ``raw`` is the whole-window SAM2 answer and ``kept`` is the part of it that held a
    positive click (both Y-down, 1024x1024), from this cell's latest prediction in the
    walk. They are None for a cell that owns pixels but never predicted (its pixels came from
    a neighbor's margin). The debug dump uses them to tell a model stop from a filtered-out piece.
    """

    row: int
    col: int
    core: MaskArray
    score: float
    raw: Optional[MaskArray] = None
    kept: Optional[MaskArray] = None
    logits: Optional[NDArray] = None


@dataclass
class SeamRecord:
    """One prediction the walk made for a cell, and what seeded it.

    ``kind`` is ``"user"`` (the client's clicks and boxes), ``"square"`` (a cell that holds part of
    a box covering whole cores; ``box`` is the box's overlap with the cell's core) or ``"edge"``
    (a range on the edge the ``parent`` cell shares with ``cell``; ``side`` is the parent's side
    facing ``cell``).
    ``runs`` are the ranges, in mosaic coordinates along that edge, the prediction crossed.
    ``box`` is the mosaic box prompt (Y up, inclusive) and ``clicks`` the extra mosaic clicks.
    ``outcome`` is ``"accepted"`` or ``"rejected"`` (no mask fit the prompt, so the cell was
    skipped for these ranges). The debug dump writes these so a seam can be inspected.
    """

    kind: str
    cell: Cell
    parent: Optional[Cell]
    side: Optional[Side]
    runs: List[PixelRange]
    box: Optional[Tuple[int, int, int, int]]
    clicks: List[Point]
    outcome: str


@dataclass
class GrowthResult:
    """Fused mosaic and any aligned tiles the caller still needs to upload."""

    mask: MaskArray
    origin_x: int
    origin_y: int
    score: float
    requested: List[TileIndex] = field(default_factory=list)
    cells: Dict[Cell, CellPrediction] = field(default_factory=dict)
    seams: List[SeamRecord] = field(default_factory=list)
    edges: Dict[Tuple[int, int, int, int], List[PixelRange]] = field(default_factory=dict)


@dataclass
class _Attempt:
    """One prediction the walk is about to make for a cell."""

    kind: str
    parent: Optional[Cell] = None
    side: Optional[Side] = None
    seeds: List[RunSeed] = field(default_factory=list)


@dataclass
class _CellState:
    predictions: int = 0
    last_raw: Optional[MaskArray] = None
    last_kept: Optional[MaskArray] = None
    last_logits: Optional[NDArray] = None
    score: float = 0.0


def max_requested_tiles_from_env() -> int:
    """Read SEGMENT_MAX_REQUESTED_TILES. Missing or invalid values use the default."""
    raw = os.environ.get("SEGMENT_MAX_REQUESTED_TILES")
    if raw is None or raw.strip() == "":
        return DEFAULT_MAX_REQUESTED_TILES
    try:
        return max(0, int(raw))
    except ValueError:
        return DEFAULT_MAX_REQUESTED_TILES


def margin_logit_min_from_env() -> float:
    """Read SEGMENT_MARGIN_LOGIT. Missing or invalid values use the default."""
    raw = os.environ.get("SEGMENT_MARGIN_LOGIT")
    if raw is None or raw.strip() == "":
        return DEFAULT_MARGIN_LOGIT_MIN
    try:
        return float(raw)
    except ValueError:
        return DEFAULT_MARGIN_LOGIT_MIN


def owner_veto_logit_from_env() -> float:
    """Read SEGMENT_OWNER_VETO_LOGIT. Missing or invalid values use the default."""
    raw = os.environ.get("SEGMENT_OWNER_VETO_LOGIT")
    if raw is None or raw.strip() == "":
        return DEFAULT_OWNER_VETO_LOGIT
    try:
        return float(raw)
    except ValueError:
        return DEFAULT_OWNER_VETO_LOGIT


def grow_segmentation(
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: CellPredict,
    max_requested: int = DEFAULT_MAX_REQUESTED_TILES,
    max_cells: int = DEFAULT_MAX_CELLS,
    should_stop: Optional[Callable[[], bool]] = None,
    max_predictions: int = DEFAULT_MAX_PREDICTIONS,
    margin_logit_min: Optional[float] = None,
    owner_veto_logit: Optional[float] = None,
    boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
    omit_with_box: Optional[Sequence[Point]] = None,
    assume_boxes: bool = False,
) -> GrowthResult:
    """One pass of ``GrowthWalk``: walk outward from the clicks with the tiles ``predict`` has.

    Cells that need a tile ``predict`` does not hold are skipped and their tiles are listed in
    ``GrowthResult.requested``. Callers that can fetch tiles use ``GrowthWalk`` directly so
    the walk can continue instead of starting over. See ``GrowthWalk`` for the arguments.
    """
    walk = GrowthWalk(
        foreground,
        background,
        predict,
        max_requested=max_requested,
        max_cells=max_cells,
        should_stop=should_stop,
        max_predictions=max_predictions,
        margin_logit_min=margin_logit_min,
        owner_veto_logit=owner_veto_logit,
        boxes=boxes,
        omit_with_box=omit_with_box,
        assume_boxes=assume_boxes,
    )
    walk.advance()
    return walk.result()


class GrowthWalk:
    """One growth walk that can pause for missing tiles and carry on.

    ``advance`` walks outward from the foreground clicks until ``G`` stops growing with the
    tiles ``predict`` has. A cell whose tiles are missing is set aside and its tiles are
    queued for ``take_requested``. Once the caller has fetched them, ``resume`` re-queues the
    cells that were waiting and ``advance`` continues from where the walk stopped. A walk
    that is done has no new requested tiles after ``advance``.

    ``foreground`` and ``background`` are mosaic points. ``predict`` receives window-local
    Y-down points and may raise ``PredictUnavailable``. ``should_stop`` returning true
    raises ``GrowthCancelled`` before the next SAM2 call. ``margin_logit_min`` is the logit
    a margin pixel must reach to be accepted (default: ``SEGMENT_MARGIN_LOGIT`` or 1.5); a
    prediction that returns no logits contributes its core only. ``owner_veto_logit``
    (default: ``SEGMENT_OWNER_VETO_LOGIT`` or -1.0) is the logit below which a predicted
    cell vetoes a neighbor's margin pixel inside its own core; a prediction with no logits
    vetoes nothing. ``max_requested`` caps how many tiles one ``advance`` asks for.
    ``boxes`` are mosaic ``(x_min, y_min, x_max, y_max)`` SAM2 box prompts, Y up. A cell
    whose window overlaps one passes the largest clipped box to ``predict`` as the keyword
    ``box`` (window-local, Y-down, XYXY); with no boxes ``predict`` is called without it.
    ``omit_with_box`` lists foreground mosaic points that are left out of the SAM2 prompt in
    a cell that is sent a box (they still count as positives when filtering the answer).
    ``assume_boxes`` takes every pixel of every box as object, predicted or not: for boxes a
    client chose and placed inside the structure, so what SAM2 leaves out of one (an organelle,
    a pale compartment) is not left as a hole. Boxes the server inferred are not assumed.
    """

    def __init__(
        self,
        foreground: Sequence[Point],
        background: Sequence[Point],
        predict: CellPredict,
        max_requested: int = DEFAULT_MAX_REQUESTED_TILES,
        max_cells: int = DEFAULT_MAX_CELLS,
        should_stop: Optional[Callable[[], bool]] = None,
        max_predictions: int = DEFAULT_MAX_PREDICTIONS,
        margin_logit_min: Optional[float] = None,
        owner_veto_logit: Optional[float] = None,
        boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
        omit_with_box: Optional[Sequence[Point]] = None,
        assume_boxes: bool = False,
    ) -> None:
        self._started = False
        self._assume_boxes = assume_boxes
        # Cells the client's prompt starts the walk in, and the first answer that no mask fit one
        # of them. The request fails with that answer only if nothing at all was found.
        self._start_rejection: Optional[NoMatchingMask] = None
        self._last_rejection: Optional[NoMatchingMask] = None
        self._user_cells: Set[Cell] = set()
        self._user_attempted: Set[Cell] = set()
        # Cells the prompt touches that the best-fit start windows did not need. They start only
        # if those windows find nothing at all (see ``_seed_start_cells``).
        self._reserve_cells: List[Cell] = []
        self._graph = SeamGraph()
        self._seams: List[SeamRecord] = []
        self._pending: List[TileIndex] = []
        self._asked: Set[TileIndex] = set()
        self._unavailable: Set[TileIndex] = set()
        self._boxes = [(int(a), int(b), int(c), int(d)) for a, b, c, d in boxes or ()]
        self._omit_with_box = {(int(x), int(y)) for x, y in omit_with_box or ()}
        self._margin_logit_min = (
            margin_logit_min_from_env() if margin_logit_min is None else float(margin_logit_min)
        )
        self._owner_veto_logit = (
            owner_veto_logit_from_env() if owner_veto_logit is None else float(owner_veto_logit)
        )
        self._fg = [(int(x), int(y)) for x, y in foreground]
        self._bg = [(int(x), int(y)) for x, y in background]
        self._predict = predict
        self._max_requested = max_requested
        self._max_cells = max_cells
        self._max_predictions = max_predictions
        self._predictions = 0
        self._should_stop = should_stop
        self._canvas = Canvas()
        self._states: Dict[Cell, _CellState] = {}
        self._deferred: Set[Cell] = set()
        self._blocked: Set[Cell] = set()
        # Cores a box covers entirely: taken as object, never predicted.
        self._assumed: Set[Cell] = set()
        self._fg_set = set(self._fg)
        self._attempted_owners: Set[Cell] = set()
        self._requested: List[TileIndex] = []
        self._queue: Deque[Cell] = deque()
        self._queued: Set[Cell] = set()

    def advance(self) -> None:
        """Walk until nothing is queued. Call again after ``resume``."""
        if not self._started:
            self._started = True
            if not self._fg:
                return
            self._seed_start_cells()
        while True:
            while self._queue:
                self._check_stop()
                self._step(self._queue.popleft())

            self._check_stop()
            if self._reserve_cells and not self._deferred and not self._canvas.cells():
                reserve, self._reserve_cells = self._reserve_cells, []
                for cell in reserve:
                    self._user_cells.add(cell)
                    self._enqueue(cell)
                continue
            pending = self._unreached_foreground_owners()
            if not pending:
                break
            for cell in pending:
                # A cell the budgets or a failed step leave without a state would otherwise be
                # found unreached again and queued forever. One attempt per owner is enough
                # because the budgets only ever shrink, and a deferred cell is re-queued by
                # ``resume`` instead.
                self._attempted_owners.add(cell)
                self._user_cells.add(cell)
                self._enqueue(cell)

    @property
    def predictions(self) -> int:
        """SAM2 calls made so far."""
        return self._predictions

    def take_requested(self) -> List[TileIndex]:
        """Tiles the walk asked for since the last call, in the order they were needed."""
        taken = self._pending
        self._pending = []
        return taken

    def resume(self, unavailable: Iterable[TileIndex] = ()) -> None:
        """Re-queue the cells that were waiting for tiles.

        ``unavailable`` names tiles the caller could not supply. A cell that needs one of them
        (now or from an earlier call) is not tried again, so the walk cannot ask for the same
        tile forever. Tiles that were supplied need no mention: the cell's next ``predict``
        finds them.
        """
        self._unavailable.update(unavailable)
        waiting = [
            cell for cell in self._deferred
            if not any(tile in self._unavailable for tile in tiles_for_cell(cell))
        ]
        for cell in waiting:
            self._deferred.discard(cell)
        for cell in waiting:
            self._enqueue(cell)

    def result(self) -> GrowthResult:
        """The fused mosaic of ``G`` so far, and every tile that was asked for.

        Raises:
            NoMatchingMask: The prompt started the walk in one or more cells, no mask fit any of
                them, and nothing else was found. A start cell that fails while others succeed is
                only logged.
        """
        if self._start_rejection is not None and not self._canvas.cells() and not self._deferred:
            raise self._start_rejection
        return self._finish()

    def _seed_start_cells(self) -> None:
        """Start the walk in the fewest windows that between them see the whole prompt.

        The prompt is every box and every foreground point. The candidates are the cells the
        prompt touches: each whose core holds part of a box, and each that owns a point. A core a
        box covers entirely is taken as object without a prediction (and without needing its
        tiles). Of the candidates, the one whose window holds the most of the prompt is started
        first (a box counts as held only when the whole box sits clear of the window border and
        reaches the core, so the prediction is prompted with all of it); ties go to the window
        with the most room around what it holds. That repeats for whatever is still unseen, so a
        prompt that fits one window starts in one window, and a prompt that straddles a boundary
        starts in as few windows as can see all of it. Edge crossings carry the walk outward from
        there as usual.

        A box too large for any window is read through the cores it touches, each of which starts
        with that box's overlap. Candidates that were not needed wait in ``_reserve_cells`` and
        start only when nothing at all was found, so a rejected best window does not end the
        request while another window could have answered.
        """
        order: List[Cell] = []
        seen: Set[Cell] = set()
        box_cells: Dict[Box, List[Cell]] = {}

        def start(cell: Cell) -> None:
            if cell not in seen:
                seen.add(cell)
                order.append(cell)

        for box in self._boxes:
            split = cores_under_box(box)
            if split is None:
                logger.warning("Box %s spans too many cells to be a prompt; only its clicks start the walk", box)
                continue
            full, partial = split
            for cell in full:
                if cell in self._assumed:
                    continue
                self._assumed.add(cell)
                added = self._canvas.assume_core(cell)
                if added:
                    self._spread(cell, {cell: added})
            box_cells[box] = list(partial)
            for cell in partial:
                start(cell)
        for point in self._fg:
            start(cell_of_point(*point))

        candidates = [cell for cell in order if cell not in self._assumed]
        items: List[Tuple[str, Tuple[int, ...]]] = [("box", box) for box in box_cells]
        items += [
            ("point", point) for point in dict.fromkeys(self._fg)
            if cell_of_point(*point) not in self._assumed
        ]
        unseen = set(range(len(items)))
        chosen: List[Cell] = []
        while unseen:
            best_key: Optional[Tuple[int, int, int, int]] = None
            best_cell: Optional[Cell] = None
            best_held: List[int] = []
            for cell in candidates:
                if cell in chosen:
                    continue
                held = []
                room = CELL_SIZE
                for index in unseen:
                    margin = self._item_margin(cell, items[index])
                    if margin is not None:
                        held.append(index)
                        room = min(room, margin)
                if not held:
                    continue
                key = (len(held), room, -cell.row, -cell.col)
                if best_key is None or key > best_key:
                    best_key, best_cell, best_held = key, cell, held
            if best_cell is None:
                break
            chosen.append(best_cell)
            unseen.difference_update(best_held)

        started = list(chosen)
        for index in sorted(unseen):
            kind, item = items[index]
            if kind == "box":
                for cell in box_cells[item]:
                    if cell not in started and cell not in self._assumed:
                        started.append(cell)
        self._reserve_cells = [cell for cell in candidates if cell not in started]
        for cell in started:
            self._user_cells.add(cell)
            self._enqueue(cell)

    def _item_margin(self, cell: Cell, item: Tuple[str, Tuple[int, ...]]) -> Optional[int]:
        """How far a start item sits inside ``cell``'s window, or None when the window cannot hold it.

        A point is held when it is inside the window; a box only when the whole box reaches the
        cell's core and sits at least ``edge_clearance_px`` from the window border, because that
        is the only case ``_start_box`` prompts with the whole box. The margin is the distance
        from the border to the nearest edge of the item, so the larger it is the more context
        the prediction has all round.
        """
        kind, value = item
        if kind == "point":
            x, y = mosaic_to_window(cell, value[0], value[1])
            if not in_window((x, y)):
                return None
            return min(x, y, CELL_SIZE - 1 - x, CELL_SIZE - 1 - y)
        box = (value[0], value[1], value[2], value[3])
        core_x, core_y = core_origin(cell)
        if box[2] < core_x or box[3] < core_y or box[0] > core_x + CORE_SIZE - 1 or box[1] > core_y + CORE_SIZE - 1:
            return None
        margin = _box_window_margin(cell, box)
        return margin if margin >= edge_clearance_px() else None

    def _start_box(self, cell: Cell) -> Optional[Box]:
        """The mosaic box this cell's first prediction is prompted with, or None.

        A cell is prompted with a box only when the box reaches its core. When the whole box lies
        inside the window (clear of the border) the box is used as it is. When it does not, which
        is a box bigger than the window, only its overlap with the core is used: that rectangle is
        inside the object and at least 256 px from the window border, where a mask can cover it.
        The largest such rectangle wins when several boxes reach the cell.
        """
        core_x, core_y = core_origin(cell)
        clearance = edge_clearance_px()
        best: Optional[Box] = None
        for box in self._boxes:
            x0, y0, x1, y1 = box
            overlap = (max(x0, core_x), max(y0, core_y), min(x1, core_x + CORE_SIZE - 1), min(y1, core_y + CORE_SIZE - 1))
            if overlap[0] > overlap[2] or overlap[1] > overlap[3]:
                continue
            fits = _box_window_margin(cell, box) >= clearance
            chosen = box if fits else overlap
            if chosen[2] - chosen[0] + 1 < MIN_BOX_SIDE or chosen[3] - chosen[1] + 1 < MIN_BOX_SIDE:
                continue
            if best is None or _box_area(chosen) > _box_area(best):
                best = chosen
        return best

    def _unreached_foreground_owners(self) -> List[Cell]:
        owners: List[Cell] = []
        for x, y in self._fg:
            if self._canvas.contains(x, y):
                continue
            owner = cell_of_point(x, y)
            if (
                owner in self._states
                or owner in self._deferred
                or owner in self._attempted_owners
                or owner in owners
            ):
                continue
            owners.append(owner)
        return owners

    def _enqueue(self, cell: Cell) -> None:
        if cell in self._queued or cell in self._deferred or cell in self._blocked:
            return
        self._queued.add(cell)
        self._queue.append(cell)

    def _request(self, tile: TileIndex) -> None:
        if tile in self._asked or len(self._pending) >= self._max_requested:
            return
        self._asked.add(tile)
        self._pending.append(tile)
        self._requested.append(tile)

    def _step(self, cell: Cell) -> None:
        """Make every prediction this cell is owed: the user's prompt, then one per edge with new ranges."""
        self._queued.discard(cell)
        if cell in self._deferred:
            return
        for attempt in self._attempts(cell):
            state = self._states.get(cell)
            if self._predictions >= self._max_predictions:
                return
            if state is None and len(self._states) >= self._max_cells:
                # The budget only shrinks, so this cell will never be predicted. Remember it so
                # a neighbor's spread does not queue it again just to find that out.
                self._blocked.add(cell)
                return
            if state is not None and state.predictions >= MAX_PREDICTIONS_PER_CELL:
                return
            if not self._run_attempt(cell, attempt):
                return

    def _attempts(self, cell: Cell) -> Iterator[_Attempt]:
        """The predictions this cell is owed, one at a time, each read from the canvas as it is now.

        A core taken as object by a box (``_seed_start_cells``) is owed nothing. Otherwise first
        the client's own prompt, if the box or a click started this cell and it has not run. Then,
        for each side in a fixed order, the edge that the neighbor on that side shares with this
        cell, when its set pixels hold a range that has not been crossed.
        """
        if cell in self._assumed:
            return
        if cell in self._user_cells and cell not in self._user_attempted:
            yield _Attempt(kind="user")
        for side in SIDES:
            parent = Cell(cell.row - side.step[0], cell.col - side.step[1])
            seeds = self._edge_seeds(parent, side, cell)
            if seeds:
                yield _Attempt(kind="edge", parent=parent, side=side, seeds=seeds)

    def _edge_seeds(self, parent: Cell, side: Side, cell: Cell) -> List[RunSeed]:
        """Seeds for the ranges on ``parent``'s edge toward ``cell`` that have not been crossed.

        Longest range first, so the first seed is the one whose rectangle becomes the box. A
        range shorter than ``MIN_SEED_SIDE`` is noise and is neither crossed nor claimed.
        """
        core = self._canvas.core(parent)
        local_runs = edge_runs(core, side)
        if not local_runs:
            return []
        by_coordinates = {edge_coordinates(parent, side, run): run for run in local_runs}
        fresh = novel_runs(sorted(by_coordinates), self._graph.ranges(parent, cell))
        if not fresh:
            return []
        ordered = order_runs([by_coordinates[coordinates] for coordinates in fresh])
        depths = band_depths(core, side, CORE_MARGIN - edge_clearance_px(), rows=ordered)
        seeds = [seed_for_run(core, parent, side, run, depths=depths) for run in ordered]
        return [seed for seed in seeds if not self._already_seen_by(cell, seed)]

    def _already_seen_by(self, cell: Cell, seed: RunSeed) -> bool:
        """True when ``cell``'s latest prediction already covers this range's seed.

        A prediction leaves its confident margin pixels in the neighbors' cores, so a neighbor's
        core edge can show a range that is only this cell's own answer coming back. Crossing it
        would predict the cell again for nothing, and claiming it would block the forward
        crossing the walk still has to make. A range the cell has not seen (the return arm of a
        hairpin) is not covered and goes through. The seed's box, or its click when it has no
        box, is what is tested.
        """
        state = self._states.get(cell)
        if state is None or state.last_kept is None:
            return False
        if seed.box is not None:
            # The prediction was given this box clipped to the window, so the test uses the same clip.
            clipped = clip_box_to_window(cell, seed.box)
            if clipped is None:
                return False
            left, top, right, bottom = clipped
        else:
            left, top = right, bottom = mosaic_to_window(cell, seed.click[0], seed.click[1])
            if left < 0 or top < 0 or right >= CELL_SIZE or bottom >= CELL_SIZE:
                return False
        return bool(state.last_kept[top:bottom + 1, left:right + 1].all())

    def _run_attempt(self, cell: Cell, attempt: _Attempt) -> bool:
        """Predict ``cell`` for one attempt and fold the answer into ``G``. False when the cell is deferred."""
        if attempt.kind == "user":
            mosaic_box = self._start_box(cell)
            box = window_box(cell, [mosaic_box]) if mosaic_box is not None else None
            points, labels, positives = self._start_prompts(cell, mosaic_box, boxed=box is not None)
            clicks: List[Point] = []
        else:
            primary = attempt.seeds[0]
            mosaic_box = primary.box
            box = window_box(cell, [mosaic_box]) if mosaic_box is not None else None
            clicks = [seed.click for seed in attempt.seeds[: 1 + MAX_EXTRA_CLICKS]]
            points, labels, positives = self._build_prompts(cell, set(), extra_positives=clicks)
        runs = [seed.coordinates for seed in attempt.seeds]

        if not positives:
            if attempt.kind == "user":
                self._user_attempted.add(cell)
            return True

        outcome = self._call_predict(cell, points, labels, box)
        if outcome is None:
            return False
        if attempt.kind == "user":
            self._user_attempted.add(cell)
            if outcome is _REJECTED and self._start_rejection is None:
                self._start_rejection = self._last_rejection
        elif attempt.parent is not None:
            self._graph.claim(attempt.parent, cell, runs)

        record = SeamRecord(
            kind=attempt.kind,
            cell=cell,
            parent=attempt.parent,
            side=attempt.side,
            runs=runs,
            box=mosaic_box,
            clicks=clicks,
            outcome="rejected" if outcome is _REJECTED else "accepted",
        )
        self._seams.append(record)
        if outcome is _REJECTED:
            return True

        score, raw_down, logits = outcome
        kept_down = _components_with_positives(raw_down, positives)
        kept_up = np.flipud(kept_down)
        core_up = kept_up[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE]
        accepted_down = _gate_margin(kept_down, logits, self._margin_logit_min)
        self._canvas.set_veto(cell, _owner_veto(logits, self._owner_veto_logit))
        added = self._canvas.or_window(cell, np.flipud(accepted_down))

        state = self._states.setdefault(cell, _CellState())
        state.predictions += 1
        state.last_raw = raw_down
        state.last_kept = kept_down
        state.last_logits = logits
        if core_up.any():
            state.score = score

        self._spread(cell, added)
        return True

    def _start_prompts(
        self, cell: Cell, mosaic_box: Optional[Box], boxed: bool
    ) -> Tuple[List[Point], List[int], List[Point]]:
        """Window-local points, labels and positives for a cell the client's prompt started.

        SAM2 gets the box and the client's clicks that fall in the window. A click the client sent
        for the legacy nine-click circle can be listed in ``omit_with_box``: it is left out of the
        prompt but still counts as a positive when filtering the answer. The box's center is also a
        positive for that filter, so a piece covering the box is never dropped for holding no click
        and the window may hold none; it is not sent to SAM2 unless the client sent it itself.
        """
        legacy_omit = set(self._omit_with_box) if boxed else set()
        filter_only: Set[Point] = set()
        extra: List[Point] = []
        if mosaic_box is not None:
            center = box_center(mosaic_box)
            if center not in self._fg_set:
                filter_only.add(center)
                extra.append(center)
        points, labels, positives = self._build_prompts(cell, legacy_omit | filter_only, extra_positives=extra)
        # Legacy nine-click prompt: if the window holds nothing but omitted clicks, send them.
        if legacy_omit and not any(label == 1 for label in labels) and positives:
            points, labels, positives = self._build_prompts(cell, filter_only, extra_positives=extra)
        return points, labels, positives

    def _build_prompts(
        self, cell: Cell, omit: Set[Point], extra_positives: Sequence[Point] = ()
    ) -> Tuple[List[Point], List[int], List[Point]]:
        """Window-local prompt points and labels, and every positive click in the window.

        Clicks in ``omit`` are left out of ``points`` and ``labels`` but still returned in
        ``positives``, which is what filters the answer to the pieces the user clicked.
        ``extra_positives`` are mosaic clicks the walk added (a seed rectangle's center).
        """
        points: List[Point] = []
        labels: List[int] = []
        positives: List[Point] = []
        seen: Set[Tuple[int, int, int]] = set()

        def _add(mosaic: Point, label: int) -> None:
            local = mosaic_to_window(cell, mosaic[0], mosaic[1])
            if not in_window(local) or (local[0], local[1], label) in seen:
                return
            seen.add((local[0], local[1], label))
            if label == 1:
                positives.append(local)
            if label == 1 and (int(mosaic[0]), int(mosaic[1])) in omit:
                return
            points.append(local)
            labels.append(label)

        for point in self._fg:
            _add(point, 1)
        for point in extra_positives:
            _add(point, 1)
        for point in self._bg:
            _add(point, 0)
        return points, labels, positives

    def _check_stop(self) -> None:
        """Raise ``GrowthCancelled`` when the caller asked to stop.

        Checked before every step as well as before every SAM2 call, so a walk that is making
        no predictions (budgets spent, nothing left to try) still honors a cancel.
        """
        if self._should_stop is not None and self._should_stop():
            raise GrowthCancelled()

    def _call_predict(
        self,
        cell: Cell,
        points: Sequence[Point],
        labels: Sequence[int],
        box: Optional[Tuple[int, int, int, int]] = None,
    ):
        """One SAM2 call. Returns ``(score, mask, logits)``, ``_REJECTED``, or None when the cell is deferred.

        A rejection (no mask fits the prompt) is remembered in ``_last_rejection`` so a caller that
        started the walk in this cell can report it if nothing else is found.
        """
        self._check_stop()
        try:
            if box is None:
                mask, logits, score = self._predict(cell.row, cell.col, points, labels)
            else:
                mask, logits, score = self._predict(cell.row, cell.col, points, labels, box=box)
        except PredictUnavailable as unavailable:
            self._deferred.add(cell)
            for tile in unavailable.tiles:
                self._request(tile)
            return None
        except NoMatchingMask as rejected:
            self._predictions += 1
            self._last_rejection = rejected
            logger.warning(
                "SegmentTiles cell row=%s col=%s skipped: %s", cell.row, cell.col, rejected
            )
            return _REJECTED
        self._predictions += 1

        mask_bool = np.asarray(mask, dtype=np.bool_)
        if mask_bool.ndim != 2:
            mask_bool = np.zeros((CELL_SIZE, CELL_SIZE), dtype=np.bool_)
        elif mask_bool.shape != (CELL_SIZE, CELL_SIZE):
            mask_bool = cv2.resize(
                mask_bool.astype(np.uint8),
                (CELL_SIZE, CELL_SIZE),
                interpolation=cv2.INTER_NEAREST,
            ).astype(np.bool_)
        return float(score), mask_bool, _window_logits(logits)

    def _follow_contacts(self, cell: Cell) -> None:
        """Queue the neighbor across each edge where this cell's core holds a range not yet crossed.

        Corners are not followed. A range already crossed would only make the neighbor compute
        that there is nothing to do, so it is not queued.
        """
        core = self._canvas.core(cell)
        for side in SIDES:
            neighbor = Cell(cell.row + side.step[0], cell.col + side.step[1])
            if self._has_fresh_range(cell, core, side, neighbor):
                self._enqueue(neighbor)

    def _has_fresh_range(self, cell: Cell, core: MaskArray, side: Side, neighbor: Cell) -> bool:
        """True when ``core``'s edge toward ``neighbor`` holds a range that has not been crossed.

        A cheaper test than :meth:`_edge_seeds`, which it must never be stricter than: it skips
        the depth and rectangle work, so a True here can still end with no seed.
        """
        runs = edge_runs(core, side)
        if not runs:
            return False
        coordinates = sorted(edge_coordinates(cell, side, run) for run in runs)
        return bool(novel_runs(coordinates, self._graph.ranges(cell, neighbor)))

    def _spread(self, cell: Cell, added: Mapping[Cell, int]) -> None:
        """Queue the cells a prediction affects.

        ``added`` maps each core that gained pixels to how many. A core that gained pixels may
        now touch an edge, so each is followed, and it is queued itself because its neighbors'
        edges toward it may have changed. A queued cell predicts only if one of its incoming
        edges holds a range that has not been crossed, so queueing is cheap.
        """
        self._follow_contacts(cell)
        for owner in added:
            self._follow_contacts(owner)
            if owner != cell:
                self._enqueue(owner)

    def _finish(self) -> GrowthResult:
        if not self._canvas.cells():
            return GrowthResult(
                mask=np.zeros((0, 0), dtype=np.bool_),
                origin_x=0,
                origin_y=0,
                score=0.0,
                requested=list(self._requested),
            )

        # Boxes taken as object are added to a copy, only now. Taken as object while the walk
        # runs they would stand in for predictions (edge crossings, a found answer), and a
        # request whose every start was rejected would return the rectangle it was given.
        canvas = self._canvas
        if self._assume_boxes:
            canvas = copy.deepcopy(self._canvas)
            for box in self._boxes:
                if cores_under_box(box) is not None:
                    canvas.assume_box(box)
        owners = canvas.cells()

        min_row = min(cell.row for cell in owners)
        max_row = max(cell.row for cell in owners)
        min_col = min(cell.col for cell in owners)
        max_col = max(cell.col for cell in owners)
        mosaic = np.zeros(
            ((max_row - min_row + 1) * CORE_SIZE, (max_col - min_col + 1) * CORE_SIZE),
            dtype=np.bool_,
        )
        cells: Dict[Cell, CellPrediction] = {}
        for cell in owners:
            core_down = np.flipud(canvas.core(cell))
            top = (max_row - cell.row) * CORE_SIZE
            left = (cell.col - min_col) * CORE_SIZE
            mosaic[top:top + CORE_SIZE, left:left + CORE_SIZE] = core_down
            state = self._states.get(cell)
            cells[cell] = CellPrediction(
                row=cell.row,
                col=cell.col,
                core=core_down,
                score=0.0 if state is None else state.score,
                raw=None if state is None else state.last_raw,
                kept=None if state is None else state.last_kept,
                logits=None if state is None else state.last_logits,
            )

        scores = [pred.score for pred in cells.values() if pred.score > 0.0]
        return GrowthResult(
            mask=mosaic,
            origin_x=min_col * CELL_STRIDE + CORE_MARGIN,
            origin_y=min_row * CELL_STRIDE + CORE_MARGIN,
            score=min(scores) if scores else 0.0,
            requested=list(self._requested),
            cells=cells,
            seams=list(self._seams),
            edges={key: list(runs) for key, runs in self._graph.edges.items()},
        )


def _box_window_margin(cell: Cell, box: Box) -> int:
    """Distance from the nearest edge of a mosaic box to ``cell``'s window border, in pixels.

    Negative when the box reaches outside the window.
    """
    left, top = mosaic_to_window(cell, box[0], box[3])
    right, bottom = mosaic_to_window(cell, box[2], box[1])
    return min(left, top, CELL_SIZE - 1 - right, CELL_SIZE - 1 - bottom)


def _box_area(box: Box) -> int:
    """Pixels in an inclusive ``(x_min, y_min, x_max, y_max)`` box."""
    return (box[2] - box[0] + 1) * (box[3] - box[1] + 1)


def window_box(
    cell: Cell, boxes: Sequence[Tuple[int, int, int, int]]
) -> Optional[Tuple[int, int, int, int]]:
    """The box prompt for one cell: the largest mosaic box clipped to its window.

    Returned as window-local Y-down ``(x0, y0, x1, y1)``. SAM2 takes one box per prompt, so
    when several circles overlap the window the one with the most area inside it wins. A
    box clipped below ``MIN_BOX_SIDE`` px on either side is ignored.
    """
    best: Optional[Tuple[int, int, int, int]] = None
    best_area = 0
    for box in boxes:
        clipped = clip_box_to_window(cell, box)
        if clipped is None:
            continue
        left, top, right, bottom = clipped
        # Corners are inclusive pixels, so a side spans (right - left + 1) pixels.
        if right - left + 1 < MIN_BOX_SIDE or bottom - top + 1 < MIN_BOX_SIDE:
            continue
        area = (right - left + 1) * (bottom - top + 1)
        # Equal areas go to the smaller corner tuple, so the answer does not depend on the order
        # the boxes were sent in.
        if area > best_area or (area == best_area and best is not None and clipped < best):
            best, best_area = clipped, area
    return best


def clip_box_to_window(
    cell: Cell, box: Tuple[int, int, int, int]
) -> Optional[Tuple[int, int, int, int]]:
    """A mosaic box ``(x_min, y_min, x_max, y_max)`` (Y up) as window-local Y-down corners clipped to the window.

    None when nothing of the box lies inside the window. Corners are inclusive pixels.
    """
    x_min, y_min, x_max, y_max = box
    left, top = mosaic_to_window(cell, x_min, y_max)
    right, bottom = mosaic_to_window(cell, x_max, y_min)
    left, top = max(left, 0), max(top, 0)
    right, bottom = min(right, CELL_SIZE - 1), min(bottom, CELL_SIZE - 1)
    if right < left or bottom < top:
        return None
    return left, top, right, bottom


def _window_logits(logits: Optional[NDArray]) -> Optional[NDArray]:
    """The predictor's logits as one 2-D float array (any size, Y-down), or None if unusable.

    SAM2 returns low-resolution logits (typically 256x256) for the best mask, sometimes with
    a leading mask axis. A multi-mask stack is reduced to its first entry, which is the
    highest-scoring mask after the server's sort.
    """
    if logits is None:
        return None
    array = np.asarray(logits)
    while array.ndim > 2 and array.shape[0] >= 1:
        array = array[0]
    if array.ndim != 2 or array.size == 0:
        return None
    return array.astype(np.float32, copy=False)


def _make_margin_mask() -> MaskArray:
    """True on the outer 256 px band of a window, False on its core. Built once; it never changes."""
    mask = np.ones((CELL_SIZE, CELL_SIZE), dtype=np.bool_)
    mask[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE] = False
    mask.setflags(write=False)
    return mask


_MARGIN_MASK = _make_margin_mask()


def _gate_margin(kept_down: MaskArray, logits: Optional[NDArray], margin_logit_min: float) -> MaskArray:
    """The part of a kept window mask to write: its core, plus margin pixels the model is sure of.

    The core (central 512) is accepted as the mask says. A pixel in the outer 256 px margin
    is accepted only when its logit, upsampled to window size, is at least
    ``margin_logit_min``. Without logits the margin is dropped, so the walk degrades to the
    old core-only rule rather than trusting an unmeasured margin.
    """
    accepted = np.zeros_like(kept_down)
    core = slice(CORE_MARGIN, CORE_MARGIN + CORE_SIZE)
    accepted[core, core] = kept_down[core, core]
    if logits is None or not kept_down.any():
        return accepted

    confident = kept_down & (_full_res(logits) >= margin_logit_min)
    return accepted | (confident & _MARGIN_MASK)


def _full_res(logits: NDArray) -> NDArray:
    """Logits bilinearly upsampled to window size (a no-op when already 1024x1024)."""
    if logits.shape == (CELL_SIZE, CELL_SIZE):
        return logits
    return cv2.resize(logits, (CELL_SIZE, CELL_SIZE), interpolation=cv2.INTER_LINEAR)


def _owner_veto(logits: Optional[NDArray], owner_veto_logit: float) -> Optional[MaskArray]:
    """Core-sized Y-up mask of the core pixels this prediction is sure are not object.

    None when the prediction has no logits, which clears any earlier veto for the cell.
    """
    if logits is None:
        return None
    core = slice(CORE_MARGIN, CORE_MARGIN + CORE_SIZE)
    veto_down = _full_res(logits)[core, core] < owner_veto_logit
    return np.flipud(veto_down)


def _components_with_positives(mask_down: MaskArray, positives: Sequence[Point]) -> MaskArray:
    """The connected pieces of a fresh SAM2 mask that hold a positive click.

    This is applied to one prediction before it enters ``G``. A piece with no click is
    something SAM2 added on its own, and it will be reached from the neighbor that owns
    its pixels if it belongs to the object.
    """
    if not mask_down.any() or not positives:
        return np.zeros_like(mask_down)
    count, labels = cv2.connectedComponents(mask_down.astype(np.uint8), connectivity=8)
    keep = {int(labels[y, x]) for x, y in positives if labels[y, x] > 0}
    if not keep:
        return np.zeros_like(mask_down)
    if count == 2:
        return mask_down
    # A lookup table indexed by label is much cheaper than np.isin over a million pixels.
    wanted = np.zeros(count, dtype=np.bool_)
    wanted[list(keep)] = True
    return wanted[labels]
