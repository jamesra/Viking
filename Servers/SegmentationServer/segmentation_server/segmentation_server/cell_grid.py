"""Cell grid for mask growth.

Mosaic pixels use X right and Y up (Viking world divided by downsample). Images and
SAM2 masks are stored Y-down, so image row 0 is the high-Y edge. Anything named ``_up``
is Y-up mosaic order; ``_down`` or a window point is Y-down image order.

The client uploads aligned 1024 **tiles** (the upload and cache unit). Segmentation uses
**cells**: 1024x1024 windows cropped from those tiles at a 512 stride, so neighbors
overlap 50%. Each cell owns its central 512x512 **core**. Cores partition the plane, so
every pixel has exactly one owner cell. A prediction's whole window, outer 256 px
margin included, is blended into the owning cores' fused logits (``Canvas.blend_window``): each
sample is weighted by how far it is from the window border, so the cell that sees a pixel most
centred decides it. The last 256 px of a core is also the outer margin band of the cell across
the edge, which is where the next cell's box prompt comes from (see ``seams``).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Mapping, Optional, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

TILE_SIZE = 1024
CELL_SIZE = 1024
CELL_STRIDE = 512
CORE_SIZE = 512
CORE_MARGIN = (CELL_SIZE - CORE_SIZE) // 2

# SAM2's mask decoder emits logits at 1/4 of the 1024 input: 256x256 per window, so each core
# owns 128x128 of them that no other cell's core shares.
LOGIT_SCALE = 4
LOGIT_WINDOW = CELL_SIZE // LOGIT_SCALE
LOGIT_CORE = CORE_SIZE // LOGIT_SCALE
LOGIT_CORE_MARGIN = CORE_MARGIN // LOGIT_SCALE
# Fused logit of a sample no prediction has covered. Far below any iso level a client sends.
UNSEEN_LOGIT = -20.0
# A window's logit only counts as reaching a pixel (see ``Canvas``) where its feather weight is at
# least this, which keeps the unreliable outer 64 px of a window from steering the walk.
REACH_MIN_WEIGHT = 0.25
# A fused sample counts as seen above this total weight. Above float residue, below any feather weight.
SEEN_MIN_WEIGHT = 1e-4
# An object sample next to unseen ones is a frontier only when this many connected samples (16 px) do.
FRONTIER_MIN_SAMPLES = 4
# At most this many frontiers are reported per call, largest first.
FRONTIER_MAX_POINTS = 6

Point = Tuple[int, int]
MaskArray = NDArray[np.bool_]


@dataclass(frozen=True)
class TileIndex:
    """An aligned 1024 upload tile. Row increases with mosaic Y (up), column with X."""

    row: int
    col: int


@dataclass(frozen=True)
class Cell:
    """A 1024 window at a 512 stride. Its window origin is ``(col * 512, row * 512)``."""

    row: int
    col: int


def tile_of_point(x: int, y: int) -> TileIndex:
    """Aligned upload tile containing a mosaic point."""
    return TileIndex(row=y // TILE_SIZE, col=x // TILE_SIZE)


def cell_of_point(x: int, y: int) -> Cell:
    """Cell whose core contains a mosaic point."""
    return Cell(row=(y - CORE_MARGIN) // CELL_STRIDE, col=(x - CORE_MARGIN) // CELL_STRIDE)


Box = Tuple[int, int, int, int]

# A box this large across is not a prompt for one window. The split below is skipped past this.
MAX_BOX_CELLS = 64


def cores_under_box(box: Box) -> Optional[Tuple[List[Cell], dict]]:
    """Split the cores a mosaic box touches into those it covers entirely and those it covers in part.

    ``box`` is ``(x_min, y_min, x_max, y_max)``, Y up, inclusive. Returns ``(full, partial)``:
    ``full`` lists the cells whose whole 512x512 core lies inside the box, and ``partial`` maps
    every other touched cell to the box's overlap with that core (same form as ``box``, inside the
    core, so at least 256 px from the cell's window border). None when the box spans more than
    ``MAX_BOX_CELLS`` cores, which no longer describes a seed.
    """
    x0, y0, x1, y1 = (int(v) for v in box)
    x0, x1 = min(x0, x1), max(x0, x1)
    y0, y1 = min(y0, y1), max(y0, y1)
    first = cell_of_point(x0, y0)
    last = cell_of_point(x1, y1)
    if (last.row - first.row + 1) * (last.col - first.col + 1) > MAX_BOX_CELLS:
        return None
    full: List[Cell] = []
    partial: dict = {}
    for row in range(first.row, last.row + 1):
        for col in range(first.col, last.col + 1):
            cell = Cell(row, col)
            bx, by = core_origin(cell)
            xa, xb = max(x0, bx), min(x1, bx + CORE_SIZE - 1)
            ya, yb = max(y0, by), min(y1, by + CORE_SIZE - 1)
            if xa > xb or ya > yb:
                continue
            if xa == bx and xb == bx + CORE_SIZE - 1 and ya == by and yb == by + CORE_SIZE - 1:
                full.append(cell)
            else:
                partial[cell] = (xa, ya, xb, yb)
    return full, partial


def window_origin(cell: Cell) -> Point:
    """Mosaic point of the window's low-X, low-Y corner."""
    return cell.col * CELL_STRIDE, cell.row * CELL_STRIDE


def core_origin(cell: Cell) -> Point:
    """Mosaic point of the core's low-X, low-Y corner."""
    x0, y0 = window_origin(cell)
    return x0 + CORE_MARGIN, y0 + CORE_MARGIN


def mosaic_to_window(cell: Cell, x: int, y: int) -> Point:
    """Window image pixel (Y-down) of a mosaic point. May fall outside ``[0, CELL_SIZE)``."""
    x0, y0 = window_origin(cell)
    return x - x0, y0 + CELL_SIZE - 1 - y


def in_window(point: Point) -> bool:
    """True when a window pixel lies inside the 1024 image."""
    return 0 <= point[0] < CELL_SIZE and 0 <= point[1] < CELL_SIZE


def neighbor_cells(cell: Cell) -> List[Cell]:
    """The eight cells whose cores touch this core, in a fixed order."""
    return [
        Cell(cell.row + dr, cell.col + dc)
        for dr in (1, 0, -1)
        for dc in (-1, 0, 1)
        if (dr, dc) != (0, 0)
    ]


def tiles_for_cell(cell: Cell) -> List[TileIndex]:
    """Aligned tiles a cell's window is cropped from: one, two or four."""
    x0, y0 = window_origin(cell)
    cols = range(x0 // TILE_SIZE, (x0 + CELL_SIZE - 1) // TILE_SIZE + 1)
    rows = range(y0 // TILE_SIZE, (y0 + CELL_SIZE - 1) // TILE_SIZE + 1)
    return [TileIndex(row=row, col=col) for row in rows for col in cols]


def is_aligned(cell: Cell) -> bool:
    """True when the window equals one uploaded tile, so its pinned predictor applies."""
    return cell.row % 2 == 0 and cell.col % 2 == 0


def crop_window(cell: Cell, tiles: Mapping[Tuple[int, int], NDArray]) -> Optional[NDArray]:
    """Y-down window image for a cell, or None when a needed tile is absent or not 1024.

    ``tiles`` maps ``(row, col)`` to a Y-down tile image. Cropping across a tile join
    needs no seam logic because every window is just a different rectangle of the mosaic.
    """
    needed = tiles_for_cell(cell)
    reference: Optional[NDArray] = None
    for tile in needed:
        image = tiles.get((tile.row, tile.col))
        if image is None or image.shape[0] != TILE_SIZE or image.shape[1] != TILE_SIZE:
            return None
        reference = image

    if reference is None:
        return None

    x0, y0 = window_origin(cell)
    out = np.zeros((CELL_SIZE, CELL_SIZE) + reference.shape[2:], dtype=reference.dtype)
    for tile in needed:
        image = tiles[(tile.row, tile.col)]
        xa = max(x0, tile.col * TILE_SIZE)
        xb = min(x0 + CELL_SIZE, (tile.col + 1) * TILE_SIZE)
        ya = max(y0, tile.row * TILE_SIZE)
        yb = min(y0 + CELL_SIZE, (tile.row + 1) * TILE_SIZE)
        out[y0 + CELL_SIZE - yb:y0 + CELL_SIZE - ya, xa - x0:xb - x0] = image[
            tile.row * TILE_SIZE + TILE_SIZE - yb:tile.row * TILE_SIZE + TILE_SIZE - ya,
            xa - tile.col * TILE_SIZE:xb - tile.col * TILE_SIZE,
        ]
    return out


@dataclass(frozen=True)
class MaskShape:
    """A mask prompt in mosaic space: a rectangle, optionally narrowed to a shape inside it.

    ``box`` is ``(x_min, y_min, x_max, y_max)``, Y up, inclusive. ``bitmap_up`` is None for a filled
    rectangle; otherwise a boolean array of shape ``(y_max - y_min + 1, x_max - x_min + 1)`` in Y-up
    order (row 0 is ``y_min``), true where the pixel is in the mask.
    """

    box: Box
    bitmap_up: Optional[MaskArray] = None

    def covers_core(self, cell: "Cell") -> bool:
        """True when every pixel of ``cell``'s core is in the mask."""
        x0, y0, x1, y1 = self.box
        bx, by = core_origin(cell)
        if x0 > bx or y0 > by or x1 < bx + CORE_SIZE - 1 or y1 < by + CORE_SIZE - 1:
            return False
        if self.bitmap_up is None:
            return True
        return bool(self.bitmap_up[by - y0:by - y0 + CORE_SIZE, bx - x0:bx - x0 + CORE_SIZE].all())

    def interior_point(self) -> Point:
        """A mosaic pixel that is in the mask, near its centre: the box centre for a filled rectangle.

        For a bitmap it is the set pixel nearest the centroid of the set pixels, so a ring or a crescent still
        yields a click that is on the mask.
        """
        x0, y0, x1, y1 = self.box
        if self.bitmap_up is None:
            return (x0 + x1) // 2, (y0 + y1) // 2
        rows, cols = np.nonzero(self.bitmap_up)
        if rows.size == 0:
            return (x0 + x1) // 2, (y0 + y1) // 2
        nearest = int(np.argmin((rows - rows.mean()) ** 2 + (cols - cols.mean()) ** 2))
        return x0 + int(cols[nearest]), y0 + int(rows[nearest])

    def window_samples(self, cell: "Cell") -> Optional[NDArray[np.bool_]]:
        """The mask over ``cell``'s window as a ``LOGIT_WINDOW`` square (Y-down), or None when it misses the window.

        A sample is set when any of the ``LOGIT_SCALE`` squared pixels it covers is in the mask.
        """
        x0, y0, x1, y1 = self.box
        wx0, wy0 = window_origin(cell)
        xa, xb = max(x0, wx0), min(x1, wx0 + CELL_SIZE - 1)
        ya, yb = max(y0, wy0), min(y1, wy0 + CELL_SIZE - 1)
        if xa > xb or ya > yb:
            return None
        # Window row 0 is the highest mosaic y of the window, so rows run in the opposite direction.
        rows = slice(wy0 + CELL_SIZE - 1 - yb, wy0 + CELL_SIZE - 1 - ya + 1)
        cols = slice(xa - wx0, xb - wx0 + 1)
        if self.bitmap_up is None:
            # A filled rectangle sets every sample it touches: no need to build the pixel grid.
            samples = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=np.bool_)
            samples[rows.start // LOGIT_SCALE:(rows.stop - 1) // LOGIT_SCALE + 1,
                    cols.start // LOGIT_SCALE:(cols.stop - 1) // LOGIT_SCALE + 1] = True
            return samples
        pixels = np.zeros((CELL_SIZE, CELL_SIZE), dtype=np.bool_)
        pixels[rows, cols] = np.flipud(self.bitmap_up[ya - y0:yb - y0 + 1, xa - x0:xb - x0 + 1])
        samples = pixels.reshape(LOGIT_WINDOW, LOGIT_SCALE, LOGIT_WINDOW, LOGIT_SCALE).any(axis=(1, 3))
        return samples if samples.any() else None


class Canvas:
    """The result mask ``G`` as one 512x512 Y-up block per owning cell, fused from SAM2 logits.

    A prediction that carries logits goes in with :meth:`blend_window`. Logits live at SAM2's
    native resolution (``LOGIT_SCALE`` image pixels per sample), summed per owning core with a
    feather weight that is 0 at the prediction's window border and 1 across its core, so the
    cell that sees a sample most centred dominates and overlapping windows blend continuously.
    A block is the fused logit, upsampled bilinearly (as SAM2 itself does) and thresholded at
    ``iso``. A neighbor's unsure answer near its own window edge weighs little. A prediction without logits can only say object / not object, so
    :meth:`or_core` adds its core to the block outright. :meth:`assume_core` and
    :meth:`assume_box` pin pixels as object regardless of the logits.

    Two views of the same data. The **decided** mask (``decided_core``) is what the fused logits
    say and is the answer. The **control** mask (``core``, ``cells``, ``contains``, ``read``) is
    every pixel any one window answered as object (exactly, at full resolution, away from the
    window's border) plus pins and predictions without logits, wherever the fused logits stand: a
    window with more context may know of object that a context-blind neighbor denies, and the walk
    has to visit the edge where they disagree to find out. It only decides where to predict next.

    Each predicting cell has one contribution: a new prediction by the same cell replaces its
    earlier one instead of adding to it, so a re-prediction with more context (the return arm of
    a hairpin) is not averaged against the same cell's earlier, context-blind answer.

    Blocks are Y-up. Logit grids handed in or out of this class are Y-up too unless a method
    says otherwise.
    """

    def __init__(self) -> None:
        self._blocks: dict[Cell, MaskArray] = {}
        self._legacy: dict[Cell, MaskArray] = {}
        self._pinned: dict[Cell, MaskArray] = {}
        self._sum: dict[Cell, NDArray] = {}
        self._weight: dict[Cell, NDArray] = {}
        self._windows: dict[Cell, Tuple[NDArray, NDArray]] = {}
        self._reach: dict[Cell, MaskArray] = {}
        self._decided: dict[Cell, MaskArray] = {}
        # fused_block results, read only; an owner's entry is dropped whenever a window changes its sums.
        self._fused: dict[Cell, NDArray] = {}

    def fork_for_pins(self) -> "Canvas":
        """A canvas that can take :meth:`assume_core`, :meth:`assume_box`, :meth:`assume_mask` and :meth:`deny_mask` without touching this one.

        Only the blocks, decided cores and pins those methods write are copied. The fused logits, the per-window
        contributions and the reach masks (about 1.5 MB per predicted window, which a ``copy.deepcopy`` would copy
        too) are shared, so the fork is for reading them (:meth:`decided_cells`, :meth:`decided_core`,
        :meth:`fused_mosaic`, :meth:`has_logits`) and must not be given another prediction.
        """
        fork = Canvas.__new__(Canvas)
        fork._blocks = {cell: block.copy() for cell, block in self._blocks.items()}
        fork._pinned = {cell: block.copy() for cell, block in self._pinned.items()}
        fork._decided = {cell: block.copy() for cell, block in self._decided.items()}
        fork._legacy = self._legacy
        fork._sum = self._sum
        fork._weight = self._weight
        fork._windows = self._windows
        fork._reach = self._reach
        fork._fused = {}
        return fork

    def cells(self) -> List[Cell]:
        """Cells that own at least one set pixel, in insertion order."""
        return [cell for cell, block in self._blocks.items() if block.any()]

    def core(self, cell: Cell) -> MaskArray:
        """A copy of the cell's core, Y-up. All False when the cell owns nothing yet."""
        block = self._blocks.get(cell)
        if block is None:
            return np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        return block.copy()

    def or_core(self, cell: Cell, core_up: MaskArray) -> int:
        """OR a core-sized Y-up mask into the cell's block. Returns how many pixels were new.

        For a prediction that has no logits: those pixels stay set however the logits of other
        predictions blend.
        """
        incoming = np.asarray(core_up, dtype=np.bool_)
        if incoming.shape != (CORE_SIZE, CORE_SIZE):
            raise ValueError(f"core must be {CORE_SIZE}x{CORE_SIZE}, got {incoming.shape}")

        block = self._blocks.get(cell)
        if block is None:
            if not incoming.any():
                return 0
            self._blocks[cell] = incoming.copy()
            self._decided[cell] = incoming.copy()
            self._legacy[cell] = incoming.copy()
            return int(incoming.sum())

        legacy = self._legacy.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))
        legacy |= incoming
        decided = self._decided.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))
        decided |= incoming
        added = int(np.count_nonzero(incoming & ~block))
        if added:
            block |= incoming
        return added

    def assume_core(self, cell: Cell) -> int:
        """Mark the whole core as object without a prediction. Returns how many pixels were new.

        Used for a core that lies entirely inside a box the user drew around the object. The
        pixels are pinned, so no later logits take them back.
        """
        block = self._blocks.get(cell)
        if block is None:
            block = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
            self._blocks[cell] = block
        new = CORE_SIZE * CORE_SIZE - int(np.count_nonzero(block))
        block[:] = True
        self._decided[cell] = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        self._pinned[cell] = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        return new

    def assume_box(self, box: Box) -> dict[Cell, int]:
        """Mark every pixel of a mosaic box as object without a prediction.

        ``box`` is ``(x_min, y_min, x_max, y_max)``, Y up, inclusive. Used for the rectangle a
        client sent as the object's starting prompt: the user put it inside the structure, so
        whatever SAM2 leaves out of it (an organelle, a pale compartment) is still the
        structure. The pixels are pinned. Returns the number of new pixels per cell, omitting
        cells that gained nothing.
        """
        x0, y0, x1, y1 = (int(v) for v in box)
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        added: dict[Cell, int] = {}
        first = cell_of_point(x0, y0)
        last = cell_of_point(x1, y1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                cell = Cell(row, col)
                bx, by = core_origin(cell)
                xa, xb = max(x0, bx), min(x1, bx + CORE_SIZE - 1)
                ya, yb = max(y0, by), min(y1, by + CORE_SIZE - 1)
                if xa > xb or ya > yb:
                    continue
                block = self._blocks.get(cell)
                if block is None:
                    block = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
                    self._blocks[cell] = block
                piece = (slice(ya - by, yb - by + 1), slice(xa - bx, xb - bx + 1))
                new = (yb - ya + 1) * (xb - xa + 1) - int(np.count_nonzero(block[piece]))
                block[piece] = True
                self._decided.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))[piece] = True
                pinned = self._pinned.get(cell)
                if pinned is None:
                    pinned = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
                    self._pinned[cell] = pinned
                pinned[piece] = True
                if new:
                    added[cell] = new
        return added

    def deny_box(self, box: Box) -> None:
        """Mark every pixel of a mosaic box as not object, whatever was predicted or pinned there.

        ``box`` is ``(x_min, y_min, x_max, y_max)``, Y up, inclusive. Used for a background mask the
        client sent ("everything inside is background"), applied to the finished answer only, after
        :meth:`assume_box`, so a background mask wins where it overlaps a foreground one. Cells with
        no pixels yet are left alone.
        """
        x0, y0, x1, y1 = (int(v) for v in box)
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        first = cell_of_point(x0, y0)
        last = cell_of_point(x1, y1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                cell = Cell(row, col)
                bx, by = core_origin(cell)
                xa, xb = max(x0, bx), min(x1, bx + CORE_SIZE - 1)
                ya, yb = max(y0, by), min(y1, by + CORE_SIZE - 1)
                if xa > xb or ya > yb:
                    continue
                piece = (slice(ya - by, yb - by + 1), slice(xa - bx, xb - bx + 1))
                for store in (self._blocks, self._decided, self._pinned):
                    held = store.get(cell)
                    if held is not None:
                        held[piece] = False

    def assume_mask(self, shape: MaskShape) -> dict[Cell, int]:
        """Like :meth:`assume_box` for a :class:`MaskShape`: only the pixels inside the shape are marked object."""
        if shape.bitmap_up is None:
            return self.assume_box(shape.box)
        x0, y0, x1, y1 = shape.box
        added: dict[Cell, int] = {}
        first = cell_of_point(x0, y0)
        last = cell_of_point(x1, y1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                cell = Cell(row, col)
                bx, by = core_origin(cell)
                xa, xb = max(x0, bx), min(x1, bx + CORE_SIZE - 1)
                ya, yb = max(y0, by), min(y1, by + CORE_SIZE - 1)
                if xa > xb or ya > yb:
                    continue
                piece = (slice(ya - by, yb - by + 1), slice(xa - bx, xb - bx + 1))
                inside = shape.bitmap_up[ya - y0:yb - y0 + 1, xa - x0:xb - x0 + 1]
                if not inside.any():
                    continue
                block = self._blocks.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))
                new = int(np.count_nonzero(inside & ~block[piece]))
                block[piece] |= inside
                self._decided.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))[piece] |= inside
                self._pinned.setdefault(cell, np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_))[piece] |= inside
                if new:
                    added[cell] = new
        return added

    def deny_mask(self, shape: MaskShape) -> None:
        """Like :meth:`deny_box` for a :class:`MaskShape`: only the pixels inside the shape are cleared."""
        if shape.bitmap_up is None:
            self.deny_box(shape.box)
            return
        x0, y0, x1, y1 = shape.box
        first = cell_of_point(x0, y0)
        last = cell_of_point(x1, y1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                cell = Cell(row, col)
                bx, by = core_origin(cell)
                xa, xb = max(x0, bx), min(x1, bx + CORE_SIZE - 1)
                ya, yb = max(y0, by), min(y1, by + CORE_SIZE - 1)
                if xa > xb or ya > yb:
                    continue
                piece = (slice(ya - by, yb - by + 1), slice(xa - bx, xb - bx + 1))
                inside = shape.bitmap_up[ya - y0:yb - y0 + 1, xa - x0:xb - x0 + 1]
                for store in (self._blocks, self._decided, self._pinned):
                    held = store.get(cell)
                    if held is not None:
                        held[piece] &= ~inside

    def blend_window(
        self,
        cell: Cell,
        logits_up: NDArray,
        iso: float,
        ignore_up: Optional[NDArray] = None,
        reach_up: Optional[MaskArray] = None,
    ) -> dict[Cell, int]:
        """Fold one prediction's logits into the fused logits and refresh the cores it touches.

        ``logits_up`` is the whole window at SAM2's native resolution, ``LOGIT_WINDOW`` x
        ``LOGIT_WINDOW`` float samples, Y-up. Each sample adds ``logit * weight`` to the core that
        owns it, where the weight is the feather of :func:`window_feather`. Every core the window
        overlaps (up to nine) is then recomputed as ``fused logit > iso``, so a block can lose
        pixels as well as gain them. Returns how many pixels each core gained, omitting cores
        that gained none; the caller uses it to decide which cells to look at next.

        ``ignore_up`` (same shape, bool) marks samples this prediction has no evidence about, such
        as a piece of its answer that held no clicked point, or background far from the object it found.
        They get weight 0: they neither add to the object nor count against it, and a sample that only
        silent predictions cover stays **unseen**, which is not the same as "looked and found background".
        The walk reads an object next to unseen samples as unfinished (:meth:`frontier_points`).

        ``reach_up`` is the full-resolution (``CELL_SIZE`` square, Y-up) mask of what this prediction
        is sure is object, for the control view; the walk compares prompts against that exact mask,
        so it should be the prediction's own answer. It is limited to where the feather weight is at
        least ``REACH_MIN_WEIGHT``. Left out, it is taken from the logits above ``iso``.
        """
        incoming = np.asarray(logits_up, dtype=np.float32)
        if incoming.shape != (LOGIT_WINDOW, LOGIT_WINDOW):
            raise ValueError(f"logits must be {LOGIT_WINDOW}x{LOGIT_WINDOW}, got {incoming.shape}")

        weights = window_feather()
        if ignore_up is not None:
            ignored = np.asarray(ignore_up, dtype=np.bool_)
            if ignored.shape != incoming.shape:
                raise ValueError(f"ignore must be {LOGIT_WINDOW}x{LOGIT_WINDOW}, got {ignored.shape}")
            weights = np.where(ignored, np.float32(0.0), weights)

        previous = self._windows.get(cell)
        if previous is not None:
            self._accumulate(cell, previous[0], previous[1], -1.0)
        weighted = incoming * weights
        self._windows[cell] = (weighted, weights)
        touched = self._accumulate(cell, weighted, weights, 1.0)

        if reach_up is None:
            full = cv2.resize(incoming, (CELL_SIZE, CELL_SIZE), interpolation=cv2.INTER_LINEAR) > iso
        else:
            full = np.asarray(reach_up, dtype=np.bool_)
            if full.shape != (CELL_SIZE, CELL_SIZE):
                raise ValueError(f"reach must be {CELL_SIZE}x{CELL_SIZE}, got {full.shape}")
        self._reach[cell] = full & reach_pixels()

        added: dict[Cell, int] = {}
        for owner in touched:
            new = self._refresh(owner, iso)
            if new:
                added[owner] = new
        return added

    def decided_cells(self) -> List[Cell]:
        """Cells whose decided mask holds at least one pixel, in insertion order."""
        return [cell for cell, block in self._decided.items() if block.any()]

    def decided_core(self, cell: Cell) -> MaskArray:
        """A copy of the cell's decided core (fused logits above the iso, plus pins), Y-up."""
        block = self._decided.get(cell)
        if block is None:
            return np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        return block.copy()

    def _reach_block(self, owner: Cell) -> Optional[MaskArray]:
        """OR of every window's sure pixels inside ``owner``'s core, or None when no window reaches it."""
        ox, oy = core_origin(owner)
        out: Optional[MaskArray] = None
        for cell, reach in self._reach.items():
            if abs(cell.row - owner.row) > 1 or abs(cell.col - owner.col) > 1:
                continue
            wx0, wy0 = window_origin(cell)
            xa, xb = max(wx0, ox), min(wx0 + CELL_SIZE, ox + CORE_SIZE)
            ya, yb = max(wy0, oy), min(wy0 + CELL_SIZE, oy + CORE_SIZE)
            if xa >= xb or ya >= yb:
                continue
            piece = reach[ya - wy0:yb - wy0, xa - wx0:xb - wx0]
            if not piece.any():
                continue
            if out is None:
                out = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
            out[ya - oy:yb - oy, xa - ox:xb - ox] |= piece
        return out

    def _accumulate(self, cell: Cell, weighted: NDArray, weights: NDArray, sign: float) -> List[Cell]:
        """Add (``sign`` 1) or take back (``sign`` -1) one window's contribution to the cores it overlaps."""
        # A window starts half a window (LOGIT_WINDOW // 2 samples) per cell step, like its pixels.
        wx0, wy0 = cell.col * (LOGIT_WINDOW // 2), cell.row * (LOGIT_WINDOW // 2)
        touched: List[Cell] = []
        for row in range(cell.row - 1, cell.row + 2):
            for col in range(cell.col - 1, cell.col + 2):
                owner = Cell(row, col)
                ox = col * (LOGIT_WINDOW // 2) + LOGIT_CORE_MARGIN
                oy = row * (LOGIT_WINDOW // 2) + LOGIT_CORE_MARGIN
                xa, xb = max(wx0, ox), min(wx0 + LOGIT_WINDOW, ox + LOGIT_CORE)
                ya, yb = max(wy0, oy), min(wy0 + LOGIT_WINDOW, oy + LOGIT_CORE)
                if xa >= xb or ya >= yb:
                    continue
                total = self._sum.get(owner)
                if total is None:
                    total = np.zeros((LOGIT_CORE, LOGIT_CORE), dtype=np.float32)
                    self._sum[owner] = total
                    self._weight[owner] = np.zeros((LOGIT_CORE, LOGIT_CORE), dtype=np.float32)
                src = (slice(ya - wy0, yb - wy0), slice(xa - wx0, xb - wx0))
                dst = (slice(ya - oy, yb - oy), slice(xa - ox, xb - ox))
                total[dst] += np.float32(sign) * weighted[src]
                self._weight[owner][dst] += np.float32(sign) * weights[src]
                self._fused.pop(owner, None)
                touched.append(owner)
        return touched

    def fused_block(self, cell: Cell) -> NDArray:
        """The cell's fused logits, ``LOGIT_CORE`` square, Y-up; ``UNSEEN_LOGIT`` where nothing predicted.

        The array is cached until a window changes the cell's sums, and is read only: callers that need to change it
        copy it (stacking, slicing into :func:`numpy.where` and assigning into another array all do).
        """
        cached = self._fused.get(cell)
        if cached is not None:
            return cached
        total = self._sum.get(cell)
        if total is None:
            return _unseen_block()
        out = np.full((LOGIT_CORE, LOGIT_CORE), UNSEEN_LOGIT, dtype=np.float32)
        weight = self._weight[cell]
        # Taking a contribution back leaves float residue instead of an exact zero.
        seen = weight > SEEN_MIN_WEIGHT
        out[seen] = total[seen] / weight[seen]
        out.flags.writeable = False
        self._fused[cell] = out
        return out

    def window_logits(self, cell: Cell) -> Optional[NDArray]:
        """What the canvas knows about ``cell``'s window as a SAM2 mask prompt, or None when it knows nothing.

        ``LOGIT_WINDOW`` square, Y-down like the image the window is cropped to. Each sample is the
        fused logit of the core that owns it, and 0 (neutral, "no information") where no prediction
        has covered it, so the prompt says what the neighboring windows found and nothing about the
        rest. Predictions that already include this cell's own earlier answer are not removed from it.
        """
        out = np.zeros((LOGIT_WINDOW, LOGIT_WINDOW), dtype=np.float32)
        known = False
        wx0, wy0 = cell.col * (LOGIT_WINDOW // 2), cell.row * (LOGIT_WINDOW // 2)
        for row in range(cell.row - 1, cell.row + 2):
            for col in range(cell.col - 1, cell.col + 2):
                owner = Cell(row, col)
                if owner not in self._sum:
                    continue
                ox = col * (LOGIT_WINDOW // 2) + LOGIT_CORE_MARGIN
                oy = row * (LOGIT_WINDOW // 2) + LOGIT_CORE_MARGIN
                xa, xb = max(wx0, ox), min(wx0 + LOGIT_WINDOW, ox + LOGIT_CORE)
                ya, yb = max(wy0, oy), min(wy0 + LOGIT_WINDOW, oy + LOGIT_CORE)
                if xa >= xb or ya >= yb:
                    continue
                block = self.fused_block(owner)[ya - oy:yb - oy, xa - ox:xb - ox]
                seen = block > UNSEEN_LOGIT + 0.5
                if not seen.any():
                    continue
                known = True
                out[ya - wy0:yb - wy0, xa - wx0:xb - wx0] = np.where(seen, block, np.float32(0.0))
        return np.flipud(out) if known else None

    def frontier_points(self, cell: Cell, iso: float) -> List[Tuple[int, int, float]]:
        """Where the object, near ``cell``, meets area that no window has covered.

        Each result is ``(x, y, logit)``: a mosaic pixel and the fused logit of the strongest object
        sample in one connected stretch of at least ``FRONTIER_MIN_SAMPLES`` samples that are above
        ``iso`` and touch an unseen sample (8-connected), largest stretches first, at most
        ``FRONTIER_MAX_POINTS``. Unseen is "no window said anything here" (a window border, or an area
        every prediction was silent about), not "background", so an object that runs into it has not been
        followed to its end. The nine cores a window can overlap are examined.
        """
        span = 5
        row0, col0 = cell.row - 2, cell.col - 2
        grid = np.vstack([
            np.hstack([self.fused_block(Cell(row0 + i, col0 + j)) for j in range(span)])
            for i in range(span)
        ])
        inside = grid > iso
        unseen = grid <= UNSEEN_LOGIT + 0.5
        touching = cv2.dilate(unseen.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
        frontier = (inside & touching).astype(np.uint8)
        # Only the middle 3x3 cores are examined; the outer ring is there so its neighbors read correctly.
        frontier[:LOGIT_CORE, :] = 0
        frontier[-LOGIT_CORE:, :] = 0
        frontier[:, :LOGIT_CORE] = 0
        frontier[:, -LOGIT_CORE:] = 0
        count, labels, stats, _ = cv2.connectedComponentsWithStats(frontier, connectivity=8)
        order = sorted(range(1, count), key=lambda i: -int(stats[i, cv2.CC_STAT_AREA]))
        points: List[Tuple[int, int, float]] = []
        for index in order:
            if stats[index, cv2.CC_STAT_AREA] < FRONTIER_MIN_SAMPLES or len(points) >= FRONTIER_MAX_POINTS:
                break
            ys, xs = np.nonzero(labels == index)
            best = int(np.argmax(grid[ys, xs]))
            sample_x = col0 * LOGIT_CORE + LOGIT_CORE_MARGIN + int(xs[best])
            sample_y = row0 * LOGIT_CORE + LOGIT_CORE_MARGIN + int(ys[best])
            points.append((
                int((sample_x + 0.5) * LOGIT_SCALE),
                int((sample_y + 0.5) * LOGIT_SCALE),
                float(grid[ys[best], xs[best]]),
            ))
        return points

    def has_logits(self) -> bool:
        """True when at least one prediction with logits has been blended in."""
        return bool(self._sum)

    def fused_mosaic(self, min_row: int, max_row: int, min_col: int, max_col: int, pin_level: float) -> NDArray:
        """Fused logits for a rectangle of cores, Y-down, ``LOGIT_CORE`` samples per core.

        The layout matches the mask the walk returns for the same rows and columns, at
        ``1 / LOGIT_SCALE`` of its size. Pinned pixels (``assume_*``) read at least ``pin_level``
        so a threshold at or below it keeps them.
        """
        rows, cols = max_row - min_row + 1, max_col - min_col + 1
        out = np.full((rows * LOGIT_CORE, cols * LOGIT_CORE), UNSEEN_LOGIT, dtype=np.float32)
        for row in range(min_row, max_row + 1):
            for col in range(min_col, max_col + 1):
                cell = Cell(row, col)
                block = self.fused_block(cell)
                pinned = self._pinned.get(cell)
                if pinned is not None:
                    low = pinned.reshape(LOGIT_CORE, LOGIT_SCALE, LOGIT_CORE, LOGIT_SCALE).mean(axis=(1, 3)) >= 0.5
                    block = np.where(low, np.maximum(block, np.float32(pin_level)), block)
                top = (max_row - row) * LOGIT_CORE
                left = (col - min_col) * LOGIT_CORE
                out[top:top + LOGIT_CORE, left:left + LOGIT_CORE] = np.flipud(block)
        return out

    def _refresh(self, cell: Cell, iso: float) -> int:
        """Recompute a core as ``fused logit > iso`` plus what predictions without logits and pins set.

        The fused logits are upsampled with a one-sample border from the neighboring cores, so a
        pixel next to a core boundary sees both sides. Returns how many pixels are newly set.
        """
        rows = []
        for dr in (-1, 0, 1):
            rows.append(np.hstack([self.fused_block(Cell(cell.row + dr, cell.col + dc)) for dc in (-1, 0, 1)]))
        grid = np.vstack(rows)
        halo = 1
        low = LOGIT_CORE - halo
        patch = grid[low:low + LOGIT_CORE + 2 * halo, low:low + LOGIT_CORE + 2 * halo]
        size = patch.shape[0] * LOGIT_SCALE
        up = cv2.resize(patch, (size, size), interpolation=cv2.INTER_LINEAR)
        start = halo * LOGIT_SCALE
        inside = up[start:start + CORE_SIZE, start:start + CORE_SIZE] > iso

        for extra in (self._legacy.get(cell), self._pinned.get(cell)):
            if extra is not None:
                inside = inside | extra
        self._decided[cell] = inside
        control = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        for part in (self._reach_block(cell), self._legacy.get(cell), self._pinned.get(cell)):
            if part is not None:
                control |= part
        old = self._blocks.get(cell)
        if old is None:
            if not control.any():
                return 0
            self._blocks[cell] = control
            return int(control.sum())
        added = int(np.count_nonzero(control & ~old))
        self._blocks[cell] = control
        return added

    def contains(self, x: int, y: int) -> bool:
        """True when the mosaic point is set."""
        cell = cell_of_point(x, y)
        block = self._blocks.get(cell)
        if block is None:
            return False
        ox, oy = core_origin(cell)
        return bool(block[y - oy, x - ox])

    def read(self, x0: int, y0: int, width: int, height: int) -> MaskArray:
        """Y-up rectangle of the canvas with its low corner at ``(x0, y0)``."""
        out = np.zeros((height, width), dtype=np.bool_)
        first = cell_of_point(x0, y0)
        last = cell_of_point(x0 + width - 1, y0 + height - 1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                cell = Cell(row, col)
                block = self._blocks.get(cell)
                if block is None:
                    continue
                bx, by = core_origin(cell)
                xa, xb = max(x0, bx), min(x0 + width, bx + CORE_SIZE)
                ya, yb = max(y0, by), min(y0 + height, by + CORE_SIZE)
                if xa >= xb or ya >= yb:
                    continue
                out[ya - y0:yb - y0, xa - x0:xb - x0] |= block[ya - by:yb - by, xa - bx:xb - bx]
        return out


_UNSEEN_BLOCK: Optional[NDArray] = None


def _unseen_block() -> NDArray:
    """One shared, read-only ``LOGIT_CORE`` square of ``UNSEEN_LOGIT``, for cells nothing has predicted."""
    global _UNSEEN_BLOCK
    if _UNSEEN_BLOCK is None:
        block = np.full((LOGIT_CORE, LOGIT_CORE), UNSEEN_LOGIT, dtype=np.float32)
        block.flags.writeable = False
        _UNSEEN_BLOCK = block
    return _UNSEEN_BLOCK


_FEATHER: Optional[NDArray] = None
_REACH_PIXELS: Optional[MaskArray] = None


def reach_pixels() -> MaskArray:
    """Pixels of a ``CELL_SIZE`` window a prediction may steer the walk with (feather weight at least ``REACH_MIN_WEIGHT``)."""
    global _REACH_PIXELS
    if _REACH_PIXELS is None:
        pixels = np.kron(window_feather() >= REACH_MIN_WEIGHT, np.ones((LOGIT_SCALE, LOGIT_SCALE), dtype=np.bool_))
        pixels.setflags(write=False)
        _REACH_PIXELS = pixels
    return _REACH_PIXELS


def window_feather() -> NDArray:
    """Weight of each sample of a prediction window: ``LOGIT_WINDOW`` square, read only.

    The weight is the distance to the nearest window border, in samples, over the width of the
    margin band, capped at 1: close to 0 at the border (a window's edge is where SAM2 is least
    reliable) and 1 across the core. It is never exactly 0, so a sample that only one window has
    seen is still defined.
    """
    global _FEATHER
    if _FEATHER is None:
        index = np.arange(LOGIT_WINDOW, dtype=np.float32)
        edge = np.minimum(index, LOGIT_WINDOW - 1 - index)
        ramp = np.minimum((edge + 0.5) / LOGIT_CORE_MARGIN, 1.0).astype(np.float32)
        feather = np.minimum.outer(ramp, ramp)
        feather.setflags(write=False)
        _FEATHER = feather
    return _FEATHER
