"""Mask prompts sent as tiles, so a mask can be any size.

Image tiles are 1024x1024 on a grid anchored at the volume origin. A client sends a mask on the same grid
(``MaskTile``): a tile is wholly mask (``full``), carries a 1-bit PNG, or is explicitly empty. The server keeps only
the tiles that hold mask and cuts each cell window from them on demand, so a mask 4000 px across costs the tiles it
touches and no more.

There are two kinds, a painted bitmap of what the object is (FOREGROUND, used as drawn) and of what it is not
(BACKGROUND). Every tile of both is sent right after ``start`` (``SegmentTilesRequest.mask_tile_count`` counts them), so
each bitmap is complete once they have arrived: a tile not sent is empty.
"""

from __future__ import annotations

from typing import Any, List, Optional, Tuple, Union

import cv2
import numpy as np

from segmentation_server.cell_grid import (
    LOGIT_SCALE,
    TILE_SIZE,
    Cell,
    MaskArray,
    MaskShape,
    Point,
    TileIndex,
    tiles_for_cell,
)

# A scale-4 tile is the 256x256 grid SAM2 takes as ``mask_input``: one sample per 4x4 pixels.
QUARTER_TILE = TILE_SIZE // LOGIT_SCALE

FOREGROUND = 1
BACKGROUND = 2

TileKey = Tuple[int, int]
# True for a wholly-mask tile, else a TILE_SIZE square of Y-up booleans.
TilePixels = Union[bool, MaskArray]


class MaskTileError(ValueError):
    """A mask tile the server cannot use. The message names the tile and what is wrong."""


def png_size(data: bytes) -> Optional[Tuple[int, int]]:
    """``(width, height)`` from a PNG's IHDR chunk, or None when ``data`` does not start like a PNG.

    Read before decoding so a tiny file that declares a huge image is refused without allocating it.
    """
    if len(data) < 24 or data[1:4] != b"PNG" or data[12:16] != b"IHDR":
        return None
    return int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")


def decode_mask_tile(tile: Any) -> Optional[TilePixels]:
    """One ``MaskTile`` message as pixels: True when full, a Y-up bool array, or None when it holds no mask.

    A scale-4 PNG is expanded to full resolution here, by nearest neighbour, so a window is cut from one kind of data.

    Raises:
        MaskTileError: ``full`` and ``png`` are both set, the scale is not 0, 1 or 4, or the PNG is not the size the
            scale calls for.
    """
    where = f"row {tile.cell.row} col {tile.cell.col}"
    if tile.full:
        if tile.png:
            raise MaskTileError(f"Mask tile {where} sets both full and png.")
        return True
    if not tile.png:
        return None
    if tile.scale in (0, 1):
        side = TILE_SIZE
    elif tile.scale == LOGIT_SCALE:
        side = QUARTER_TILE
    else:
        raise MaskTileError(f"Mask tile {where} has scale {tile.scale}; only 0, 1 and {LOGIT_SCALE} are accepted.")
    declared = png_size(tile.png)
    if declared is not None and declared != (side, side):
        raise MaskTileError(
            f"Mask tile {where} must be a {side}x{side} PNG at scale {tile.scale}, got {declared[0]}x{declared[1]}."
        )
    image = cv2.imdecode(np.frombuffer(tile.png, dtype=np.uint8), cv2.IMREAD_GRAYSCALE)
    if image is None or image.shape != (side, side):
        got = "not a PNG" if image is None else f"{image.shape[1]}x{image.shape[0]}"
        raise MaskTileError(f"Mask tile {where} must be a {side}x{side} PNG at scale {tile.scale}, got {got}.")
    pixels = np.flipud(image > 0)  # tiles are Y-down on the wire, Y-up here
    if side != TILE_SIZE:
        pixels = np.repeat(np.repeat(pixels, LOGIT_SCALE, axis=0), LOGIT_SCALE, axis=1)
    return pixels if pixels.any() else None


class TiledMaskShape(MaskShape):
    """A :class:`MaskShape` whose pixels live in sparse 1024 tiles instead of one bitmap.

    It is filled in as tiles arrive (:meth:`settle`). ``complete`` says whether a tile it has not been told about is
    empty (every tile the mask touches was sent) or unknown (:meth:`unknown_tiles` lists it, and the walk waits for it).
    A tile that is unknown or empty reads as no mask.

    ``box`` is the tight box of the pixels settled so far, and is only meaningful when :attr:`empty` is False.
    """

    # MaskShape is a frozen dataclass, so state is set through object.__setattr__; the tile dicts are mutated in place.
    def __init__(self, role: str = "seed", complete: bool = False) -> None:
        object.__setattr__(self, "bitmap_up", None)
        object.__setattr__(self, "role", role)
        object.__setattr__(self, "_complete", complete)
        object.__setattr__(self, "_pixels", {})  # (row, col) -> TilePixels, tiles that hold mask
        object.__setattr__(self, "_extent", {})  # (row, col) -> tight (x0, y0, x1, y1) of that tile's mask
        object.__setattr__(self, "_known", set())  # (row, col) of every tile settled, empty or not

    def __eq__(self, other: object) -> bool:
        return self is other

    __hash__ = object.__hash__

    def with_role(self, role: str) -> "TiledMaskShape":
        """This same mask (not a copy) read as ``role``. The tiles are shared."""
        object.__setattr__(self, "role", role)
        return self

    def mark_complete(self) -> None:
        """Every tile that holds mask has been settled: a tile not settled is empty from now on."""
        object.__setattr__(self, "_complete", True)

    def settle(self, key: TileKey, pixels: Optional[TilePixels]) -> None:
        """Record what is in tile ``key``: True (all mask), a Y-up bool square, or None (no mask)."""
        self._known.add(key)
        if pixels is None or (pixels is not True and not pixels.any()):
            self._pixels.pop(key, None)
            self._extent.pop(key, None)
            return
        row, col = key
        tx, ty = col * TILE_SIZE, row * TILE_SIZE
        if pixels is True:
            local = (0, 0, TILE_SIZE - 1, TILE_SIZE - 1)
        else:
            columns = np.flatnonzero(pixels.any(axis=0))
            rows = np.flatnonzero(pixels.any(axis=1))
            local = (int(columns[0]), int(rows[0]), int(columns[-1]), int(rows[-1]))
        self._pixels[key] = pixels
        self._extent[key] = (tx + local[0], ty + local[1], tx + local[2], ty + local[3])

    def is_known(self, key: TileKey) -> bool:
        return key in self._known

    def tiles(self) -> List[Tuple[TileKey, TilePixels]]:
        """The tiles that hold mask: ``((row, col), pixels)``, pixels True for a full tile or a Y-up bool square."""
        return list(self._pixels.items())

    def unknown_tiles(self, cell: Cell) -> List[TileIndex]:
        if self._complete:
            return []
        return [tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in self._known]

    @property
    def unbounded(self) -> bool:
        return True

    @property
    def empty(self) -> bool:
        return not self._pixels

    @property
    def box(self):  # type: ignore[override]
        if not self._extent:
            return (0, 0, -1, -1)
        extents = list(self._extent.values())
        return (
            min(e[0] for e in extents), min(e[1] for e in extents),
            max(e[2] for e in extents), max(e[3] for e in extents),
        )

    @property
    def filled(self) -> bool:
        return False

    def region(self, xa: int, ya: int, xb: int, yb: int) -> MaskArray:
        out = np.zeros((yb - ya + 1, xb - xa + 1), dtype=np.bool_)
        for row in range(ya // TILE_SIZE, yb // TILE_SIZE + 1):
            for col in range(xa // TILE_SIZE, xb // TILE_SIZE + 1):
                pixels = self._pixels.get((row, col))
                if pixels is None:
                    continue
                tx, ty = col * TILE_SIZE, row * TILE_SIZE
                ca, cb = max(xa, tx), min(xb, tx + TILE_SIZE - 1)
                ra, rb = max(ya, ty), min(yb, ty + TILE_SIZE - 1)
                target = out[ra - ya:rb - ya + 1, ca - xa:cb - xa + 1]
                if pixels is True:
                    target[...] = True
                else:
                    target[...] = pixels[ra - ty:rb - ty + 1, ca - tx:cb - tx + 1]
        return out

    def interior_point(self) -> Point:
        """The set pixel nearest the centroid of the set pixels, found tile by tile so no full-size array is built."""
        total = 0
        sum_x = sum_y = 0.0
        for (row, col), pixels in self._pixels.items():
            tx, ty = col * TILE_SIZE, row * TILE_SIZE
            if pixels is True:
                count = TILE_SIZE * TILE_SIZE
                sum_x += count * (tx + (TILE_SIZE - 1) / 2)
                sum_y += count * (ty + (TILE_SIZE - 1) / 2)
            else:
                ys, xs = np.nonzero(pixels)
                count = xs.size
                sum_x += float(xs.sum()) + count * tx
                sum_y += float(ys.sum()) + count * ty
            total += count
        if total == 0:
            return 0, 0
        cx, cy = sum_x / total, sum_y / total
        best_distance = None
        best: Point = (0, 0)
        for (row, col), pixels in self._pixels.items():
            tx, ty = col * TILE_SIZE, row * TILE_SIZE
            if pixels is True:
                # The tile's pixel nearest the centroid is the centroid clamped into the tile.
                x = int(min(max(round(cx), tx), tx + TILE_SIZE - 1))
                y = int(min(max(round(cy), ty), ty + TILE_SIZE - 1))
            else:
                ys, xs = np.nonzero(pixels)
                nearest = int(np.argmin((xs + tx - cx) ** 2 + (ys + ty - cy) ** 2))
                x, y = int(xs[nearest]) + tx, int(ys[nearest]) + ty
            distance = (x - cx) ** 2 + (y - cy) ** 2
            if best_distance is None or distance < best_distance:
                best_distance, best = distance, (x, y)
        return best


class MaskTileCollector:
    """One request's mask tiles: the foreground and background bitmaps, with their duplicate and count rules.

    ``announced`` is ``SegmentTilesRequest.mask_tile_count``, how many tiles of either kind follow ``start`` (0 for a
    request with no bitmap). Both bitmaps are complete once that many have arrived.
    """

    def __init__(self, announced: int = 0) -> None:
        self.announced = int(announced)
        self.foreground = TiledMaskShape(complete=False)
        self.background = TiledMaskShape(complete=False)
        self.received = 0

    @property
    def done(self) -> bool:
        return self.received >= self.announced

    def add(self, message: Any) -> int:
        """Take one ``MaskTiles`` message. Returns how many tiles it held (all new: a repeat is an error).

        Raises:
            MaskTileError: The kind is unset, a (cell, kind) repeats, more tiles arrive than were announced, or a tile
                is not usable (see :func:`decode_mask_tile`).
        """
        for tile in message.tiles:
            kind = int(tile.kind)
            if kind not in (FOREGROUND, BACKGROUND):
                raise MaskTileError(f"Mask tile row {tile.cell.row} col {tile.cell.col} has no kind.")
            key = (int(tile.cell.row), int(tile.cell.col))
            shape = self.foreground if kind == FOREGROUND else self.background
            if shape.is_known(key):
                raise MaskTileError(f"Mask tile row {key[0]} col {key[1]} was sent twice for the same kind.")
            if self.received >= self.announced:
                raise MaskTileError(f"More mask tiles were sent than the {self.announced} announced.")
            shape.settle(key, decode_mask_tile(tile))
            self.received += 1
        return len(message.tiles)

    def foreground_shape(self) -> Optional[TiledMaskShape]:
        """The foreground bitmap, complete, or None when it holds no pixel."""
        self.foreground.mark_complete()
        return None if self.foreground.empty else self.foreground

    def background_shape(self) -> Optional[TiledMaskShape]:
        """The background bitmap, complete, or None when it holds no pixel."""
        self.background.mark_complete()
        return None if self.background.empty else self.background
