"""Grow a segmentation across overlapping cells.

A cell is a 1024 window at a 512 stride (see ``cell_grid``). SAM2 predicts a whole
window. Pieces of the answer that hold no positive click are dropped, then the window's SAM2
logits (native 256x256, four pixels per sample) are blended into the result ``G``. Each pixel is
stored in the core of the cell that owns it, as a weighted mean of the logits every window that
saw it predicted, the weight falling to nothing at a window's border (``cell_grid.window_feather``).
``G`` is where that fused logit is above the request's ``mask_threshold``. Because windows overlap,
the cell that sees a pixel most centred decides it, and a neighbor's unsure answer near its own
window edge weighs little, so there is no margin gate and no owner veto: a pixel can be added or
removed as more windows report. A prediction that returns no logits can only say object / not
object, so its core is added outright.

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
was already predicted is predicted again and ``G`` only ever gains pixels. A window whose
background (the neighbouring annotations) the caller has not been told about is set aside the
same way, and its tiles are listed by ``GrowthWalk.take_requested_background``.
"""

from __future__ import annotations

import logging
import os
from collections import deque
from dataclasses import dataclass, field
from typing import Any, Callable, Deque, Dict, Iterable, Iterator, List, Mapping, Optional, Protocol, Sequence, Set, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_server.mask_utils import DEFAULT_MASK_THRESHOLD, NoMatchingMask
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
    LOGIT_SCALE,
    LOGIT_WINDOW,
    TILE_SIZE,
    Box,
    Canvas,
    Cell,
    MaskArray,
    MaskShape,
    Point,
    TileIndex,
    cell_of_point,
    core_origin,
    MAX_BOX_CELLS,
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
# A box prompt clipped to less than this on a side says nothing useful about the object.
MIN_BOX_SIDE = 16
# Logit a mask prompt gives the pixels it covers: confidently object, well inside SAM2's mask_input range.
MASK_PROMPT_LOGIT = 8.0
# Logit a background rectangle gives where nothing positive is known: it only says "most likely background".
BACKGROUND_RECT_LOGIT = 2.0
# A frontier is tried once per square of this many mosaic pixels, so one that stays uncovered cannot loop.
FRONTIER_TRY_PX = 64
# How far from the object it found, in logit samples (4 px each), a prediction's "not object"
# still counts as evidence. See ``_kept_logits``.
SUPPORT_RADIUS = 16

class CellPredict(Protocol):
    """What the walk calls to predict one cell.

    ``points`` are window-local Y-down click positions with ``labels`` (1 foreground, 0
    background). ``box`` is passed only when the prompt has one: a window-local Y-down inclusive
    ``(x0, y0, x1, y1)``, and the answer must have a pixel inside it (see ``mask_utils.select_mask``).
    ``mask_logits`` is passed only for a continuation window: a ``LOGIT_WINDOW`` square, Y-down map of
    what the neighboring windows already found, 0 where nothing is known, for SAM2's ``mask_input``.
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
        mask_logits: Optional[NDArray] = None,
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
    logits: Optional[NDArray] = None
    """Fused logits over the same extent as ``mask`` at ``1 / LOGIT_SCALE`` of its size (Y-down), or
    None when no prediction returned logits. Samples nobody predicted read ``UNSEEN_LOGIT``; boxes
    taken as object read at least ``logit_iso + 1``."""


@dataclass
class _Attempt:
    """One prediction the walk is about to make for a cell."""

    kind: str
    parent: Optional[Cell] = None
    side: Optional[Side] = None
    seeds: List[RunSeed] = field(default_factory=list)
    points: List[Point] = field(default_factory=list)


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


def grow_segmentation(
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: CellPredict,
    max_requested: int = DEFAULT_MAX_REQUESTED_TILES,
    max_cells: int = DEFAULT_MAX_CELLS,
    should_stop: Optional[Callable[[], bool]] = None,
    max_predictions: int = DEFAULT_MAX_PREDICTIONS,
    logit_iso: float = DEFAULT_MASK_THRESHOLD,
    boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
    masks: Optional[Sequence[MaskShape | Tuple[int, int, int, int]]] = None,
    hint_boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
    background_masks: Optional[Sequence[MaskShape | Tuple[int, int, int, int]]] = None,
    background_rects: Optional[Sequence[Tuple[int, int, int, int]]] = None,
    prompt_encoder: Optional[Any] = None,
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
        logit_iso=logit_iso,
        boxes=boxes,
        masks=masks,
        hint_boxes=hint_boxes,
        background_masks=background_masks,
        background_rects=background_rects,
        prompt_encoder=prompt_encoder,
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
    raises ``GrowthCancelled`` before the next SAM2 call. ``logit_iso`` is the logit a fused
    pixel must exceed to be object, normally the same ``mask_threshold`` the predictions were
    thresholded with; a prediction that returns no logits contributes its core only.
    ``max_requested`` caps how many tiles one ``advance`` asks for.
    ``boxes`` are mosaic ``(x_min, y_min, x_max, y_max)`` SAM2 box prompts, Y up. A cell
    whose window overlaps one passes the largest clipped box to ``predict`` as the keyword
    ``box`` (window-local, Y-down, XYXY); with no boxes ``predict`` is called without it.
    A box is never taken as object: anything that is object is a mask.
    ``masks`` are rectangle-shaped mask prompts (same frame as ``boxes``): "everything inside is
    object, and the mask grows beyond it". They start cells and cover cores exactly as boxes do and
    are always taken as object, but a cell's first prediction is given the rectangle as SAM2's
    ``mask_input`` (with the rectangle's center as a click) instead of as a box.
    ``hint_boxes`` are boxes in the "most likely inside" sense: they start cells and are sent as boxes
    like ``boxes`` and, like them, are never taken as object.
    ``background_masks`` are rectangles that are all background: every window's mask prompt is
    strongly negative there and they are cleared from the answer. ``background_rects`` are
    rectangles the background is most likely inside: a weak negative in the mask prompt only.
    ``prompt_encoder`` (see ``prompt_encodings``) builds the mask prompt of a start window that has a client mask,
    background included, as the model was trained on it. None keeps the legacy flat prompt below. Windows the walk
    grows into are prompted with their neighbours' fused logits whichever encoder this is.
    ``background_masks`` may be a list the caller keeps adding to (background shapes arrive as the walk asks for
    them); it is read, not copied. ``background_coverage`` names, for a cell, the tiles of its window whose background
    the caller has not been told about yet; the walk waits for those (:meth:`take_requested_background`) before it
    predicts the window.
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
        logit_iso: float = DEFAULT_MASK_THRESHOLD,
        boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
        masks: Optional[Sequence[MaskShape | Tuple[int, int, int, int]]] = None,
        hint_boxes: Optional[Sequence[Tuple[int, int, int, int]]] = None,
        background_masks: Optional[Sequence[MaskShape | Tuple[int, int, int, int]]] = None,
        background_rects: Optional[Sequence[Tuple[int, int, int, int]]] = None,
        prompt_encoder: Optional[Any] = None,
        holds_image: Optional[Callable[[TileIndex], bool]] = None,
        background_coverage: Optional[Callable[[Cell], Sequence[TileIndex]]] = None,
    ) -> None:
        self._prompt_encoder = prompt_encoder
        self._background_coverage = background_coverage
        # Whether the caller already holds a tile's image. A cell that waits for background mask tiles also asks for the
        # image tiles it lacks in the same round, so the client is not asked twice for one window.
        self._holds_image = holds_image
        self._pending_masks: List[TileIndex] = []
        self._masks_asked: Set[TileIndex] = set()
        # Cores the mask prompts touch without covering whole: each must be predicted for the walk to be complete at
        # the mask's edge. Used to say so when a budget stops the walk first.
        self._mask_boundary: Set[Cell] = set()
        self._started = False
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
        self._mask_shapes: Dict[Box, MaskShape] = {}
        for item in masks or ():
            shape = item if isinstance(item, MaskShape) else MaskShape(tuple(int(v) for v in item))  # type: ignore[arg-type]
            self._mask_shapes[shape.box] = shape
        self._masks: Set[Box] = set(self._mask_shapes)
        # Masks follow boxes in this list so start-cell choice and core coverage treat both alike;
        # ``_masks`` says which entries are prompted as masks.
        self._boxes = [(int(a), int(b), int(c), int(d)) for a, b, c, d in boxes or ()]
        self._hints: Set[Box] = {(int(a), int(b), int(c), int(d)) for a, b, c, d in hint_boxes or ()}
        self._boxes += sorted(self._hints.difference(self._boxes))
        self._boxes += sorted(self._masks.difference(self._boxes))
        if isinstance(background_masks, list) and all(isinstance(item, MaskShape) for item in background_masks):
            # The caller's own list: background shapes it adds later are seen.
            self._background_masks: List[MaskShape] = background_masks
        else:
            self._background_masks = [
                item if isinstance(item, MaskShape) else MaskShape(tuple(int(v) for v in item))  # type: ignore[arg-type]
                for item in background_masks or ()
            ]
        self._background_rects = [(int(a), int(b), int(c), int(d)) for a, b, c, d in background_rects or ()]
        self._background_patches: Dict[Cell, Optional[Tuple[Optional[NDArray], Optional[NDArray]]]] = {}
        self._encoded_backgrounds: Dict[Cell, Optional[NDArray]] = {}
        self._logit_iso = float(logit_iso)
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
        # Object that ends at area no window has covered: the points to continue from, by the cell that
        # centers on them, and the places already tried so the same stretch is not queued twice.
        self._frontiers: Dict[Cell, List[Point]] = {}
        self._frontier_tried: Set[Tuple[Cell, int, int]] = set()
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

    def take_requested_background(self) -> List[TileIndex]:
        """Tiles whose background the walk needs and has not been told about, since the last call, in the order needed.

        The caller finds out what background reaches them (adding any shapes to the background list), records them as
        known so ``background_coverage`` stops naming them, then calls :meth:`resume`.
        """
        taken = self._pending_masks
        self._pending_masks = []
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
        self._warn_if_budget_left_mask_edge_unpredicted()
        return self._finish()

    def _warn_if_budget_left_mask_edge_unpredicted(self) -> None:
        """Log when ``max_cells`` or ``max_predictions`` stopped the walk with mask-boundary cores never predicted.

        The mask's own pixels are still returned, but the object's edge beyond those cores is not found, so the answer
        stops at the mask there.
        """
        if not self._mask_boundary:
            return
        limit = None
        if self._predictions >= self._max_predictions:
            limit = f"max_predictions={self._max_predictions}"
        elif len(self._states) >= self._max_cells:
            limit = f"max_cells={self._max_cells}"
        if limit is None:
            return
        left = [cell for cell in self._mask_boundary if cell not in self._states and cell not in self._assumed]
        if left:
            logger.warning(
                "SegmentTiles stopped at %s with %d of %d mask-boundary cores unpredicted; the answer stops at the mask there",
                limit, len(left), len(self._mask_boundary),
            )

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
            split = cores_under_box(box, None if box in self._masks else MAX_BOX_CELLS)
            if split is None:
                logger.warning("Box %s spans too many cells to be a prompt; only its clicks start the walk", box)
                continue
            full, partial = split
            touched = list(partial)
            if box in self._masks:
                shape = self._mask_shapes[box]
                for cell in full:
                    if cell in self._assumed:
                        continue
                    if not shape.covers_core(cell):
                        # A bitmap that leaves part of this core out: predict the core, then pin the mask's pixels.
                        touched.append(cell)
                        continue
                    self._assumed.add(cell)
                    added = self._canvas.assume_core(cell)
                    if added:
                        self._spread(cell, {cell: added})
            else:
                # A box is a hint, not a claim about every pixel, so a core it covers is predicted like any other.
                touched = list(full) + touched
            box_cells[box] = touched
            if box in self._masks:
                self._mask_boundary.update(touched)
            for cell in touched:
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

    def _start_box(self, cell: Cell) -> Optional[Tuple[Box, Optional[MaskShape]]]:
        """The mosaic rectangle this cell's first prediction is prompted with and the mask it came from, or None.

        A cell is prompted with a box only when the box reaches its core. When the whole box lies
        inside the window (clear of the border) the box is used as it is. When it does not, which
        is a box bigger than the window, only its overlap with the core is used: that rectangle is
        inside the object and at least 256 px from the window border, where a mask can cover it.
        The largest such rectangle wins when several boxes reach the cell. The shape is the mask prompt
        the winner came from (or is the overlap of), None for a box.
        """
        core_x, core_y = core_origin(cell)
        clearance = edge_clearance_px()
        best: Optional[Box] = None
        best_shape: Optional[MaskShape] = None
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
                best, best_shape = chosen, self._mask_shapes.get(box)
        return None if best is None else (best, best_shape)

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
        pending = self._frontiers.pop(cell, [])
        if pending:
            yield _Attempt(kind="frontier", points=pending[: 1 + MAX_EXTRA_CLICKS])

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
            start = self._start_box(cell)
            mosaic_box = start[0] if start is not None else None
            mask_shape = start[1] if start is not None else None
            masked = mask_shape is not None
            box = window_box(cell, [mosaic_box]) if mosaic_box is not None and not masked else None
            points, labels, positives = self._start_prompts(
                cell, mosaic_box, send_center=masked,
                center=mask_shape.interior_point() if mask_shape is not None else None,
            )
            clicks: List[Point] = []
        elif attempt.kind == "frontier":
            # The object ran into area no window had covered here. This is the cell centered on that spot:
            # a click on the object where it meets the uncovered area, the user's own clicks that fall in
            # this window, and the mask prompt. No box.
            mosaic_box = None
            box = None
            clicks = list(attempt.points)
            points, labels, positives = self._build_prompts(cell, set(), extra_positives=clicks)
        else:
            primary = attempt.seeds[0]
            # The seed rectangle is recorded (and tells the walk what the window has already seen), but it
            # is not sent to SAM2: a box reaching toward the window border makes SAM2 stop at the border,
            # which is the straight cut a continuation exists to avoid. The window gets the clicks and
            # the mask prompt instead.
            mosaic_box = primary.box
            box = None
            clicks = [seed.click for seed in attempt.seeds[: 1 + MAX_EXTRA_CLICKS]]
            points, labels, positives = self._build_prompts(cell, set(), extra_positives=clicks)
        runs = [seed.coordinates for seed in attempt.seeds]

        if not positives:
            if attempt.kind == "user":
                self._user_attempted.add(cell)
            return True

        missing = self._missing_background(cell)
        if missing:
            self._wait_for_background(cell, missing)
            return False

        # A continuation window is told what the windows around it have found, so it continues that
        # object instead of choosing one from its prompt alone. A start window has nothing to be told.
        mask_logits = self._canvas.window_logits(cell) if attempt.kind in ("edge", "frontier") else None
        if attempt.kind == "user" and masked and self._prompt_encoder is not None:
            # The model's own encoding builds the whole start prompt, background included.
            mask_logits = self._prompt_encoder.start_window(cell, mask_shape, self._background_masks)
        else:
            if attempt.kind == "user" and masked:
                mask_logits = mask_prompt_logits(cell, mask_shape)
            mask_logits = self._with_background(cell, mask_logits)
        outcome = self._call_predict(cell, points, labels, box, mask_logits)
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
        if raw_down.any() and not kept_down.any():
            # The prediction exists but holds none of the foreground clicks, so nothing of it is used. Say so,
            # because the client only sees an empty result.
            on_mask = sum(1 for x, y in positives if raw_down[y, x])
            logger.info(
                "SegmentTiles cell row=%s col=%s kind=%s: prediction kept nothing: mask %d px, "
                "%d of %d foreground click(s) on it, %d background point(s) sent",
                cell.row, cell.col, attempt.kind, int(raw_down.sum()), on_mask, len(positives),
                int(sum(1 for label in labels if label == 0)),
            )
        kept_up = np.flipud(kept_down)
        core_up = kept_up[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE]
        if logits is None:
            added = self._or_own_core(cell, core_up)
        else:
            native, ignored = _kept_logits(logits, kept_down, self._logit_iso)
            added = self._canvas.blend_window(
                cell,
                np.flipud(native),
                self._logit_iso,
                ignore_up=np.flipud(ignored),
                reach_up=kept_up,
            )

        state = self._states.setdefault(cell, _CellState())
        state.predictions += 1
        state.last_raw = raw_down
        state.last_kept = kept_down
        state.last_logits = logits
        if core_up.any():
            state.score = score

        self._spread(cell, added)
        self._queue_frontiers(cell)
        return True

    def _queue_frontiers(self, cell: Cell) -> None:
        """Queue the cells that must look where the object runs into area nobody has covered.

        A prediction stops at its window's border, and a neighbor window that was silent there (it did not
        see this object) leaves the area unseen. An object edge that is really a window border is invisible
        to the edge-following above, because it sits inside a core, not on its edge. So after each
        prediction the canvas is asked where the object meets unseen samples, and the cell whose core
        holds each such spot, whose window is centered on it, is queued with a click there. A spot is tried
        once, by 64 px cell of the mosaic, so a border that stays uncovered does not loop.
        """
        for x, y, _logit in self._canvas.frontier_points(cell, self._logit_iso):
            target = cell_of_point(x, y)
            key = (target, x // FRONTIER_TRY_PX, y // FRONTIER_TRY_PX)
            if key in self._frontier_tried or target in self._blocked:
                continue
            self._frontier_tried.add(key)
            self._frontiers.setdefault(target, []).append((x, y))
            logger.info(
                "SegmentTiles frontier at (%s, %s): the object meets uncovered area, continuing in cell row=%s col=%s",
                x,
                y,
                target.row,
                target.col,
            )
            self._enqueue(target)

    def _or_own_core(self, cell: Cell, core_up: MaskArray) -> Dict[Cell, int]:
        """Add a prediction that has no logits: only its core, which is all it can vouch for."""
        new = self._canvas.or_core(cell, core_up)
        return {cell: new} if new else {}

    def _with_background(self, cell: Cell, mask_logits: Optional[NDArray]) -> Optional[NDArray]:
        """``mask_logits`` with the client's background rectangles and masks painted into ``cell``'s window.

        A background mask is ``-MASK_PROMPT_LOGIT`` wherever it covers, or, for a model with a prompt encoder, the
        encoder's own negative (scaled by distance from the mask's edge, as the model was trained); a background
        rectangle is only ``-BACKGROUND_RECT_LOGIT`` and only where nothing positive is already known, because it says
        the background is likely there, not certain. The input is not modified. Returns it unchanged when no
        background shape reaches the window.
        """
        patch = self._background_patch(cell)
        if patch is None:
            return mask_logits
        weak, strong = patch
        out = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=np.float32) if mask_logits is None else mask_logits.copy()
        if weak is not None:
            free = weak & (out <= 0)
            out[free] = np.minimum(out[free], -BACKGROUND_RECT_LOGIT)
        if self._prompt_encoder is not None:
            encoded = self._encoded_background(cell)
            if encoded is not None:
                negative = encoded < 0
                out[negative] = encoded[negative]
        elif strong is not None:
            out[strong] = -MASK_PROMPT_LOGIT
        return out

    def _encoded_background(self, cell: Cell) -> Optional[NDArray]:
        """The prompt encoder's negatives for the client's background masks in ``cell``'s window, or None when none reach it.

        A ``LOGIT_WINDOW`` square of values <= 0. Worked out once per cell, since the encoding includes a distance transform.
        """
        if cell not in self._encoded_backgrounds:
            self._encoded_backgrounds[cell] = (
                self._prompt_encoder.background_logits(cell, self._background_masks) if self._prompt_encoder is not None else None
            )
        return self._encoded_backgrounds[cell]

    def _background_patch(self, cell: Cell) -> Optional[Tuple[Optional[NDArray], Optional[NDArray]]]:
        """Where the background rectangles (weak) and masks (strong) fall in ``cell``'s window, or None when neither does.

        Both are ``LOGIT_WINDOW`` square boolean maps, None when that kind misses the window. They depend only on the
        request, so each cell's is worked out once and reused by every attempt on it.
        """
        if cell in self._background_patches:
            return self._background_patches[cell]
        weak: Optional[NDArray] = None
        for rect in self._background_rects:
            samples = MaskShape(rect).window_samples(cell)
            if samples is not None:
                weak = samples if weak is None else weak | samples
        strong: Optional[NDArray] = None
        for mask in self._background_masks:
            samples = mask.window_samples(cell)
            if samples is not None:
                strong = samples if strong is None else strong | samples
        patch = None if weak is None and strong is None else (weak, strong)
        self._background_patches[cell] = patch
        return patch

    def _start_prompts(
        self, cell: Cell, mosaic_box: Optional[Box], send_center: bool = False,
        center: Optional[Point] = None,
    ) -> Tuple[List[Point], List[int], List[Point]]:
        """Window-local points, labels and positives for a cell the client's prompt started.

        SAM2 gets the box and the client's clicks that fall in the window. The box's center is also a
        positive for the answer filter, so a piece covering the box is never dropped for holding no click
        and the window may hold none; it is not sent to SAM2 unless the client sent it itself or
        ``send_center`` is set, which a mask-prompted start uses so SAM2 has a click to pair with
        the mask.
        """
        filter_only: Set[Point] = set()
        extra: List[Point] = []
        if mosaic_box is not None:
            center = center if center is not None else box_center(mosaic_box)
            if center not in self._fg_set:
                if not send_center:
                    filter_only.add(center)
                extra.append(center)
        return self._build_prompts(cell, filter_only, extra_positives=extra)

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

    def _missing_background(self, cell: Cell) -> List[TileIndex]:
        """The tiles of ``cell``'s window whose background the walk has not been told about."""
        missing: List[TileIndex] = []
        if self._background_coverage is not None:
            missing.extend(self._background_coverage(cell))
        for shape in self._background_masks:
            for tile in shape.unknown_tiles(cell):
                if tile not in missing:
                    missing.append(tile)
        return missing

    def _wait_for_background(self, cell: Cell, missing: Sequence[TileIndex]) -> None:
        """Defer ``cell`` until the caller has settled the background of the tiles ``missing`` (see :meth:`take_requested_background`).

        The window's image tiles the caller lacks are asked for in the same round.
        """
        self._deferred.add(cell)
        for tile in missing:
            if tile not in self._masks_asked:
                self._masks_asked.add(tile)
                self._pending_masks.append(tile)
        if self._holds_image is not None:
            for tile in tiles_for_cell(cell):
                if not self._holds_image(tile):
                    self._request(tile)

    def _call_predict(
        self,
        cell: Cell,
        points: Sequence[Point],
        labels: Sequence[int],
        box: Optional[Tuple[int, int, int, int]] = None,
        mask_logits: Optional[NDArray] = None,
    ):
        """One SAM2 call. Returns ``(score, mask, logits)``, ``_REJECTED``, or None when the cell is deferred.

        A rejection (no mask fits the prompt) is remembered in ``_last_rejection`` so a caller that
        started the walk in this cell can report it if nothing else is found.
        """
        self._check_stop()
        try:
            extra = {} if mask_logits is None else {"mask_logits": mask_logits}
            if box is None:
                mask, logits, score = self._predict(cell.row, cell.col, points, labels, **extra)
            else:
                mask, logits, score = self._predict(cell.row, cell.col, points, labels, box=box, **extra)
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
        if not self._canvas.decided_cells():
            return GrowthResult(
                mask=np.zeros((0, 0), dtype=np.bool_),
                origin_x=0,
                origin_y=0,
                score=0.0,
                requested=list(self._requested),
            )

        # Masks taken as object are added to a copy, only now. Taken as object while the walk
        # runs they would stand in for predictions (edge crossings, a found answer), and a
        # request whose every start was rejected would return the mask it was given.
        canvas = self._canvas
        if self._masks or self._background_masks:
            canvas = self._canvas.fork_for_pins()
            for box in self._boxes:
                if cores_under_box(box, None if box in self._masks else MAX_BOX_CELLS) is None:
                    continue
                if box in self._masks:
                    canvas.assume_mask(self._mask_shapes[box])
            for shape in self._background_masks:
                if not shape.empty and cores_under_box(shape.box, None if shape.unbounded else MAX_BOX_CELLS) is not None:
                    canvas.deny_mask(shape)
        owners = canvas.decided_cells()
        if not owners:
            # A background mask can clear everything the walk found.
            return GrowthResult(
                mask=np.zeros((0, 0), dtype=np.bool_),
                origin_x=0,
                origin_y=0,
                score=0.0,
                requested=list(self._requested),
            )

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
            core_down = np.flipud(canvas.decided_core(cell))
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
        logits = None
        if canvas.has_logits():
            logits = canvas.fused_mosaic(min_row, max_row, min_col, max_col, self._logit_iso + 1.0)
        return GrowthResult(
            mask=mosaic,
            origin_x=min_col * CELL_STRIDE + CORE_MARGIN,
            origin_y=min_row * CELL_STRIDE + CORE_MARGIN,
            score=min(scores) if scores else 0.0,
            requested=list(self._requested),
            cells=cells,
            seams=list(self._seams),
            edges={key: list(runs) for key, runs in self._graph.edges.items()},
            logits=logits,
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


def mask_prompt_logits(cell: Cell, shape: MaskShape) -> NDArray[np.float32]:
    """A mask prompt as SAM2 ``mask_input`` for ``cell``'s window.

    ``LOGIT_WINDOW`` square, Y-down like :meth:`Canvas.window_logits`: ``MASK_PROMPT_LOGIT`` inside the
    rectangle and 0 (no information) elsewhere, because a mask prompt says what is object and nothing
    about the rest. The rectangle is clipped to the window.
    """
    out = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=np.float32)
    samples = shape.window_samples(cell)
    if samples is not None:
        out[samples] = MASK_PROMPT_LOGIT
    return out


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


def _native_logits(logits: NDArray) -> NDArray:
    """Logits resampled to the native ``LOGIT_WINDOW`` square the canvas blends (a no-op when already that size)."""
    if logits.shape == (LOGIT_WINDOW, LOGIT_WINDOW):
        return logits
    shrinking = logits.shape[0] > LOGIT_WINDOW or logits.shape[1] > LOGIT_WINDOW
    return cv2.resize(
        logits,
        (LOGIT_WINDOW, LOGIT_WINDOW),
        interpolation=cv2.INTER_AREA if shrinking else cv2.INTER_LINEAR,
    )


def _kept_logits(logits: NDArray, kept_down: MaskArray, iso: float) -> Tuple[NDArray, NDArray]:
    """A prediction's logits at native size, and which samples it has no evidence about.

    ``kept_down`` is the full-resolution Y-down answer that held a positive click. A native
    sample is kept when at least half of its ``LOGIT_SCALE`` square is. A prediction answers for
    the object its clicks picked, so it speaks only within ``SUPPORT_RADIUS`` samples of that
    object: farther away, "not object" says nothing about a separate piece of the same structure
    (the other arm of a hairpin, which this window may not see joined to the first). A connected
    piece the model calls object (above ``iso``) that holds no kept sample is a piece nobody clicked
    and is silent too; a piece that touches the kept object is part of it, whatever its edge samples
    say about the full-resolution mask. Silent samples are the second array. The canvas gives them
    weight 0, so a sample only silent predictions cover stays unseen, which is not the same as
    background: the walk continues from an object that runs into it.
    """
    native = _native_logits(logits)
    side = CELL_SIZE // LOGIT_SCALE
    kept_low = kept_down.reshape(side, LOGIT_SCALE, side, LOGIT_SCALE).mean(axis=(1, 3)) >= 0.5
    size = 2 * SUPPORT_RADIUS + 1
    support = cv2.dilate(kept_low.astype(np.uint8), np.ones((size, size), np.uint8)) > 0
    above = native > iso
    count, labels = cv2.connectedComponents(above.astype(np.uint8), connectivity=8)
    touching_kept = np.unique(labels[kept_low & above])
    clicked_piece = np.isin(labels, touching_kept[touching_kept > 0])
    return native, ~support | (above & ~clicked_piece)


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
