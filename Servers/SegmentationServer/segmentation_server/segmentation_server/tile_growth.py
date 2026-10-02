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

1. Predict the cell that owns the first foreground click.
2. OR the kept core, and the confident margin pixels, into ``G``.
3. When ``G`` inside a core touches the core edge, or a prediction's margin put pixels in
   a core, predict that cell. Its positive clicks are the foreground clicks in its window
   plus 1 to 3 seeds at the center of each separate piece of ``G`` in its outer 256 px
   margin (``sample_ring_seeds``), so it continues the same object.
4. A cell is predicted again, up to a small cap, when ``G`` has grown inside its window
   and the new pixels are not covered by what it last predicted. That finds the second
   arm of a C or hairpin that comes back through cores already visited.
5. Repeat until nothing is queued, then start from any foreground click still outside
   ``G``.

Aligned tiles the server does not hold are reported in ``GrowthResult.requested`` and the
walk continues without that cell, so the client can upload them and ask again.
"""

from __future__ import annotations

import os
from collections import deque
from dataclasses import dataclass, field
from typing import Callable, Deque, Dict, List, Mapping, Optional, Sequence, Set, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_server.cell_grid import (
    CELL_SIZE,
    CELL_STRIDE,
    CORE_MARGIN,
    CORE_SIZE,
    TILE_SIZE,
    Canvas,
    Cell,
    MaskArray,
    Point,
    TileIndex,
    cell_of_point,
    core_contacts,
    in_window,
    mosaic_to_window,
    neighbor_cells,
    sample_ring_seeds,
    tile_of_point,
    window_origin,
)

__all__ = [
    "TILE_SIZE",
    "Cell",
    "CellPrediction",
    "CellPredict",
    "GrowthCancelled",
    "GrowthResult",
    "PredictUnavailable",
    "TileIndex",
    "grow_segmentation",
    "max_requested_tiles_from_env",
    "tile_of_point",
]

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

# (cell row, cell col, window points y-down, labels) -> mask of the window, logits or None, score
CellPredict = Callable[
    [int, int, Sequence[Point], Sequence[int]],
    Tuple[MaskArray, Optional[NDArray], float],
]


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
    walk. They are None for a cell that came only from remembered cores. The debug dump
    uses them to tell a model stop from a filtered-out piece.
    """

    row: int
    col: int
    core: MaskArray
    score: float
    raw: Optional[MaskArray] = None
    kept: Optional[MaskArray] = None
    logits: Optional[NDArray] = None


@dataclass
class GrowthResult:
    """Fused mosaic and any aligned tiles the caller still needs to upload."""

    mask: MaskArray
    origin_x: int
    origin_y: int
    score: float
    requested: List[TileIndex] = field(default_factory=list)
    cells: Dict[Cell, CellPrediction] = field(default_factory=dict)


@dataclass
class _CellState:
    predictions: int = 0
    seeds_used: Set[Point] = field(default_factory=set)
    last_mask_up: Optional[MaskArray] = None
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
    remembered: Optional[Mapping[Cell, MaskArray]] = None,
    margin_logit_min: Optional[float] = None,
    owner_veto_logit: Optional[float] = None,
) -> GrowthResult:
    """Walk outward from the foreground clicks until ``G`` stops growing.

    ``foreground`` and ``background`` are mosaic points. ``predict`` receives window-local
    Y-down points and may raise ``PredictUnavailable``. ``should_stop`` returning true
    raises ``GrowthCancelled`` before the next SAM2 call. ``remembered`` holds the Y-down
    core masks an earlier call for the same clicks produced. They start ``G``, so the result
    never shrinks between calls, and any of their core edges that was left open (for example
    because a tile was missing) is followed now. ``margin_logit_min`` is the logit a margin
    pixel must reach to be accepted (default: ``SEGMENT_MARGIN_LOGIT`` or 1.5); a prediction
    that returns no logits contributes its core only. ``owner_veto_logit`` (default:
    ``SEGMENT_OWNER_VETO_LOGIT`` or -1.0) is the logit below which a predicted cell vetoes a
    neighbor's margin pixel inside its own core; a prediction with no logits vetoes nothing.
    """
    walk = _Walk(
        [(int(x), int(y)) for x, y in foreground],
        [(int(x), int(y)) for x, y in background],
        predict,
        max_requested,
        max_cells,
        should_stop,
        max_predictions,
        remembered or {},
        margin_logit_min_from_env() if margin_logit_min is None else float(margin_logit_min),
        owner_veto_logit_from_env() if owner_veto_logit is None else float(owner_veto_logit),
    )
    return walk.run()


class _Walk:
    """State of one growth walk. Kept as a class so each rule is a small named method."""

    def __init__(
        self,
        foreground: List[Point],
        background: List[Point],
        predict: CellPredict,
        max_requested: int,
        max_cells: int,
        should_stop: Optional[Callable[[], bool]],
        max_predictions: int,
        remembered: Mapping[Cell, MaskArray],
        margin_logit_min: float,
        owner_veto_logit: float,
    ) -> None:
        self._remembered = remembered
        self._margin_logit_min = margin_logit_min
        self._owner_veto_logit = owner_veto_logit
        self._fg = foreground
        self._bg = background
        self._predict = predict
        self._max_requested = max_requested
        self._max_cells = max_cells
        self._max_predictions = max_predictions
        self._predictions = 0
        self._should_stop = should_stop
        self._canvas = Canvas()
        self._states: Dict[Cell, _CellState] = {}
        self._deferred: Set[Cell] = set()
        self._requested: List[TileIndex] = []
        self._queue: Deque[Cell] = deque()
        self._queued: Set[Cell] = set()

    def run(self) -> GrowthResult:
        if not self._fg:
            return self._finish()

        self._enqueue(cell_of_point(*self._fg[0]))
        for cell, core_down in self._remembered.items():
            self._canvas.or_core(cell, np.flipud(np.asarray(core_down, dtype=np.bool_)))
        for cell in self._remembered:
            self._follow_contacts(cell)
        while True:
            while self._queue:
                self._step(self._queue.popleft())

            pending = self._unreached_foreground_owners()
            if not pending:
                break
            for cell in pending:
                self._enqueue(cell)
        return self._finish()

    def _unreached_foreground_owners(self) -> List[Cell]:
        owners: List[Cell] = []
        for x, y in self._fg:
            if self._canvas.contains(x, y):
                continue
            owner = cell_of_point(x, y)
            if owner in self._states or owner in self._deferred or owner in owners:
                continue
            owners.append(owner)
        return owners

    def _enqueue(self, cell: Cell) -> None:
        if cell in self._queued or cell in self._deferred:
            return
        self._queued.add(cell)
        self._queue.append(cell)

    def _request(self, tile: TileIndex) -> None:
        if tile in self._requested or len(self._requested) >= self._max_requested:
            return
        self._requested.append(tile)

    def _step(self, cell: Cell) -> None:
        self._queued.discard(cell)
        if cell in self._deferred or self._predictions >= self._max_predictions:
            return
        state = self._states.get(cell)
        if state is None and len(self._states) >= self._max_cells:
            return
        if state is not None and state.predictions >= MAX_PREDICTIONS_PER_CELL:
            return

        x0, y0 = window_origin(cell)
        seeds = sample_ring_seeds(self._canvas.read(x0, y0, CELL_SIZE, CELL_SIZE), x0, y0)
        if state is not None and not _has_new_seed(state, seeds, x0, y0):
            return

        points, labels, positives = self._prompts(cell, seeds)
        if not positives:
            return

        mask_down = self._call_predict(cell, points, labels)
        if mask_down is None:
            return
        score, raw_down, logits = mask_down
        kept_down = _components_with_positives(raw_down, positives)
        kept_up = np.flipud(kept_down)
        core_up = kept_up[CORE_MARGIN:CORE_MARGIN + CORE_SIZE, CORE_MARGIN:CORE_MARGIN + CORE_SIZE]
        accepted_down = _gate_margin(kept_down, logits, self._margin_logit_min)
        self._canvas.set_veto(cell, _owner_veto(logits, self._owner_veto_logit))
        added = self._canvas.or_window(cell, np.flipud(accepted_down))

        state = self._states.setdefault(cell, _CellState())
        state.predictions += 1
        self._predictions += 1
        state.seeds_used.update(seeds)
        state.last_mask_up = kept_up
        state.last_raw = raw_down
        state.last_kept = kept_down
        state.last_logits = logits
        if core_up.any():
            state.score = score

        self._spread(cell, added)

    def _prompts(
        self, cell: Cell, seeds: Sequence[Point]
    ) -> Tuple[List[Point], List[int], List[Point]]:
        points: List[Point] = []
        labels: List[int] = []
        positives: List[Point] = []
        seen: Set[Tuple[int, int, int]] = set()

        def _add(mosaic: Point, label: int) -> None:
            local = mosaic_to_window(cell, mosaic[0], mosaic[1])
            if not in_window(local) or (local[0], local[1], label) in seen:
                return
            seen.add((local[0], local[1], label))
            points.append(local)
            labels.append(label)
            if label == 1:
                positives.append(local)

        for point in self._fg:
            _add(point, 1)
        for point in seeds:
            _add(point, 1)
        for point in self._bg:
            _add(point, 0)
        return points, labels, positives

    def _call_predict(
        self, cell: Cell, points: Sequence[Point], labels: Sequence[int]
    ) -> Optional[Tuple[float, MaskArray, Optional[NDArray]]]:
        if self._should_stop is not None and self._should_stop():
            raise GrowthCancelled()
        try:
            mask, logits, score = self._predict(cell.row, cell.col, points, labels)
        except PredictUnavailable as unavailable:
            self._deferred.add(cell)
            for tile in unavailable.tiles:
                self._request(tile)
            return None

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
        for d_row, d_col in core_contacts(self._canvas.core(cell)):
            self._enqueue(Cell(cell.row + d_row, cell.col + d_col))

    def _spread(self, cell: Cell, added: Mapping[Cell, int]) -> None:
        """Queue the cells a prediction affects.

        ``added`` maps each core that gained pixels to how many. The prediction's margin
        lands in neighbor cores, so such a core is queued even if its own edge is not
        touched (the object now reaches into it), and every already-predicted cell whose
        window contains the new pixels is queued to see them as seeds. A cell's window
        reaches only into its own core and its eight neighbors' cores.
        """
        self._follow_contacts(cell)
        for owner in added:
            self._follow_contacts(owner)
            if owner != cell:
                self._enqueue(owner)
            for neighbor in neighbor_cells(owner):
                state = self._states.get(neighbor)
                if neighbor != cell and state is not None and state.predictions < MAX_PREDICTIONS_PER_CELL:
                    self._enqueue(neighbor)

    def _finish(self) -> GrowthResult:
        owners = self._canvas.cells()
        if not owners:
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
            core_down = np.flipud(self._canvas.core(cell))
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
        )


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
    margin = np.ones((CELL_SIZE, CELL_SIZE), dtype=np.bool_)
    margin[core, core] = False
    return accepted | (confident & margin)


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


def _has_new_seed(state: _CellState, seeds: Sequence[Point], x0: int, y0: int) -> bool:
    """True when ``G`` offers a seed this cell has not been given and did not already cover."""
    if state.last_mask_up is None:
        return bool(seeds)
    for sx, sy in seeds:
        if (sx, sy) in state.seeds_used:
            continue
        if not state.last_mask_up[sy - y0, sx - x0]:
            return True
    return False


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
    return np.isin(labels, list(keep))
