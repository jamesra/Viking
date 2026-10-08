"""Cell grid for mask growth.

Mosaic pixels use X right and Y up (Viking world divided by downsample). Images and
SAM2 masks are stored Y-down, so image row 0 is the high-Y edge. Anything named ``_up``
is Y-up mosaic order; ``_down`` or a window point is Y-down image order.

The client uploads aligned 1024 **tiles** (the upload and cache unit). Segmentation uses
**cells**: 1024x1024 windows cropped from those tiles at a 512 stride, so neighbors
overlap 50%. Each cell owns its central 512x512 **core**. Cores partition the plane, so
every pixel has exactly one owner cell. A prediction's whole window, outer 256 px
margin included, is ORed into the owning cores. Only an owner's own veto removes a
neighbor's margin pixel (``Canvas.set_veto``). The
last 256 px of a core is also the outer margin band of the cell across the edge, which is where
the next cell's box prompt comes from (see ``seams``).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Mapping, Optional, Tuple

import numpy as np
from numpy.typing import NDArray

TILE_SIZE = 1024
CELL_SIZE = 1024
CELL_STRIDE = 512
CORE_SIZE = 512
CORE_MARGIN = (CELL_SIZE - CORE_SIZE) // 2

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


class Canvas:
    """The result mask ``G`` as one 512x512 Y-up block per owning cell.

    ``or_core`` and ``or_window`` set bits. The one way a pixel is removed is ``set_veto``:
    a cell that has predicted and is confident a pixel in its own core is not object
    overrides a neighbor's margin pixel there. Pixels the owner itself wrote are never
    removed, so a re-prediction cannot erase the owner's earlier answer.
    """

    def __init__(self) -> None:
        self._blocks: dict[Cell, MaskArray] = {}
        self._owned: dict[Cell, MaskArray] = {}
        self._veto: dict[Cell, MaskArray] = {}

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
        """OR a core-sized Y-up mask into the cell's block. Returns how many pixels were new."""
        incoming = np.asarray(core_up, dtype=np.bool_)
        if incoming.shape != (CORE_SIZE, CORE_SIZE):
            raise ValueError(f"core must be {CORE_SIZE}x{CORE_SIZE}, got {incoming.shape}")

        block = self._blocks.get(cell)
        if block is None:
            if not incoming.any():
                return 0
            self._blocks[cell] = incoming.copy()
            return int(incoming.sum())

        added = int(np.count_nonzero(incoming & ~block))
        if added:
            block |= incoming
        return added

    def assume_core(self, cell: Cell) -> int:
        """Mark the whole core as object without a prediction. Returns how many pixels were new.

        Used for a core that lies entirely inside a box the user drew around the object. The
        pixels count as the owner's own, so no neighbor's veto can take them back.
        """
        block = self._blocks.get(cell)
        if block is None:
            block = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
            self._blocks[cell] = block
        new = CORE_SIZE * CORE_SIZE - int(np.count_nonzero(block))
        block[:] = True
        self._owned[cell] = np.ones((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
        return new

    def assume_box(self, box: Box) -> dict[Cell, int]:
        """Mark every pixel of a mosaic box as object without a prediction.

        ``box`` is ``(x_min, y_min, x_max, y_max)``, Y up, inclusive. Used for the rectangle a
        client sent as the object's starting prompt: the user put it inside the structure, so
        whatever SAM2 leaves out of it (an organelle, a pale compartment) is still the
        structure. The pixels count as each owner's own, so no veto removes them. Returns the
        number of new pixels per cell, omitting cells that gained nothing.
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
                owned = self._owned.get(cell)
                if owned is None:
                    owned = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
                    self._owned[cell] = owned
                owned[piece] = True
                if new:
                    added[cell] = new
        return added

    def set_veto(self, cell: Cell, veto_up: Optional[MaskArray]) -> int:
        """Record where ``cell``'s own prediction says its core is not object.

        ``veto_up`` is core-sized and Y-up (None clears the veto). It replaces the cell's
        previous veto. Pixels set in it are removed from the cell's block unless the cell
        wrote them itself, and later ``or_window`` calls from other cells skip them.
        Returns how many pixels were removed.
        """
        if veto_up is None:
            self._veto.pop(cell, None)
            return 0
        veto = np.asarray(veto_up, dtype=np.bool_)
        if veto.shape != (CORE_SIZE, CORE_SIZE):
            raise ValueError(f"veto must be {CORE_SIZE}x{CORE_SIZE}, got {veto.shape}")
        self._veto[cell] = veto.copy()

        block = self._blocks.get(cell)
        if block is None:
            return 0
        owned = self._owned.get(cell)
        doomed = block & veto
        if owned is not None:
            doomed &= ~owned
        removed = int(np.count_nonzero(doomed))
        if removed:
            block &= ~doomed
        return removed

    def or_window(self, cell: Cell, window_up: MaskArray) -> dict[Cell, int]:
        """OR a whole window-sized Y-up mask, margin included, into the cores it overlaps.

        A window overlaps up to nine cores. Each piece lands in the core that owns those
        pixels, so the canvas keeps one block per owning cell. Pixels a neighbor has vetoed
        (``set_veto``) are skipped, and the writing cell's own core is recorded as owned.
        Returns the number of new pixels per cell, omitting cells that gained nothing.
        """
        incoming = np.asarray(window_up, dtype=np.bool_)
        if incoming.shape != (CELL_SIZE, CELL_SIZE):
            raise ValueError(f"window must be {CELL_SIZE}x{CELL_SIZE}, got {incoming.shape}")

        added: dict[Cell, int] = {}
        if not incoming.any():
            return added

        x0, y0 = window_origin(cell)
        first = cell_of_point(x0, y0)
        last = cell_of_point(x0 + CELL_SIZE - 1, y0 + CELL_SIZE - 1)
        for row in range(first.row, last.row + 1):
            for col in range(first.col, last.col + 1):
                owner = Cell(row, col)
                bx, by = core_origin(owner)
                xa, xb = max(x0, bx), min(x0 + CELL_SIZE, bx + CORE_SIZE)
                ya, yb = max(y0, by), min(y0 + CELL_SIZE, by + CORE_SIZE)
                if xa >= xb or ya >= yb:
                    continue
                piece = incoming[ya - y0:yb - y0, xa - x0:xb - x0]
                if owner != cell:
                    veto = self._veto.get(owner)
                    if veto is not None:
                        piece = piece & ~veto[ya - by:yb - by, xa - bx:xb - bx]
                if not piece.any():
                    continue
                block = self._blocks.get(owner)
                if block is None:
                    block = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
                    self._blocks[owner] = block
                if owner == cell:
                    owned = self._owned.get(owner)
                    if owned is None:
                        owned = np.zeros((CORE_SIZE, CORE_SIZE), dtype=np.bool_)
                        self._owned[owner] = owned
                    owned[ya - by:yb - by, xa - bx:xb - bx] |= piece
                target = block[ya - by:yb - by, xa - bx:xb - bx]
                new = int(np.count_nonzero(piece & ~target))
                if new:
                    target |= piece
                    added[owner] = new
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
