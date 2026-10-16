"""Tiled mask prompts: the foreground and background masks of one ``SegmentTilesStream`` request, held tile by tile.

A client that sets ``SegmentTilesRequest.foreground_mask_tile_count`` sends its masks as ``MaskTile`` messages on the
same 1024 grid as the image tiles instead of one large ``Mask``. The server keeps each kind in a sparse
:class:`TiledMask`: a tile that was sent is a boolean array (Y-down like the image tile), the marker ``FULL`` or an empty
tile, and a tile that was not sent is simply absent, so "empty" and "not sent yet" differ.

A :class:`TiledMask` has the interface the growth walk already uses of ``cell_grid.MaskShape`` (``box``, ``role``,
``covers_core``, ``interior_point``, ``window_samples``, ``window_full`` and ``crop_up`` for the canvas pins), so the walk,
the prompt encoders and the canvas treat it exactly like a mask that arrived as one bitmap. ``window_full`` cuts a
window from up to four tiles, the way ``cell_grid.crop_window`` does for image tiles. A scale-4 tile is a 256 x 256 bitmap
that is expanded by nearest neighbour first. Nothing here touches the shared image cache.

:class:`MaskTileSet` is the per-request pair of masks plus the validation of the messages that fill them.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Set, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_server.cell_grid import (
    CELL_SIZE,
    CORE_SIZE,
    LOGIT_SCALE,
    LOGIT_WINDOW,
    TILE_SIZE,
    Box,
    Cell,
    Point,
    TileIndex,
    core_origin,
    tiles_for_cell,
    window_origin,
)

__all__ = [
    "FOREGROUND",
    "BACKGROUND",
    "DEFAULT_MAX_FOREGROUND_MASK_TILES",
    "DEFAULT_MAX_BACKGROUND_MASK_TILES",
    "MASK_TILE_SCALES",
    "SCALED_TILE_SIDE",
    "MaskTileError",
    "MaskTileLimitError",
    "MaskTileSet",
    "TiledMask",
    "parse_mask_tile",
]

FOREGROUND = "foreground"
BACKGROUND = "background"

# Most foreground tiles one request may hold: 256 tiles is a 16 x 16 square of them, 16384 px across at the tiles' downsample.
DEFAULT_MAX_FOREGROUND_MASK_TILES = 256
# Background tiles are asked for one window at a time; this only stops a client from pushing an unbounded number.
DEFAULT_MAX_BACKGROUND_MASK_TILES = 4096

# The scales a MaskTile's png may have (0 means 1). Anything else is INVALID_ARGUMENT.
MASK_TILE_SCALES = (0, 1, 4)
SCALED_TILE_SIDE = TILE_SIZE // 4

_PNG_SIGNATURE = b"\x89PNG"


class MaskTileError(ValueError):
    """A ``MaskTile`` the conversation does not allow: INVALID_ARGUMENT."""


class MaskTileLimitError(Exception):
    """Too many mask tiles for one request: RESOURCE_EXHAUSTED."""


class _Full:
    """Marker: every pixel of the tile is in the mask."""

    def __repr__(self) -> str:
        return "FULL"


FULL = _Full()

# What one received tile holds: None (nothing in it), FULL, or a Y-down bitmap with the scale it was sent at.
_Content = Optional[object]


@dataclass(frozen=True)
class _Tile:
    """One received tile: ``bitmap`` is Y-down and ``scale`` x ``scale`` mosaic pixels per entry; both None for empty/FULL."""

    full: bool = False
    bitmap: Optional[NDArray[np.bool_]] = None
    scale: int = 1

    @property
    def empty(self) -> bool:
        return not self.full and (self.bitmap is None or not self.bitmap.any())

    def pixels(self) -> Optional[NDArray[np.bool_]]:
        """The tile at full resolution, ``TILE_SIZE`` square and Y-down, or None when it is empty (or FULL, which has no array)."""
        if self.bitmap is None or self.full:
            return None
        if self.scale == 1:
            return self.bitmap
        return np.repeat(np.repeat(self.bitmap, self.scale, axis=0), self.scale, axis=1)


_EMPTY_TILE = _Tile()
_FULL_TILE = _Tile(full=True)


def _png_size(data: bytes) -> Optional[Tuple[int, int]]:
    """``(width, height)`` from a PNG's IHDR chunk, or None when ``data`` does not start like a PNG."""
    if len(data) < 24 or data[1:4] != b"PNG" or data[12:16] != b"IHDR":
        return None
    return int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")


def _kind_name(kind: int) -> str:
    from segmentation_grpc.segmentation_pb2 import MASK_KIND_BACKGROUND, MASK_KIND_FOREGROUND

    if kind == MASK_KIND_FOREGROUND:
        return FOREGROUND
    if kind == MASK_KIND_BACKGROUND:
        return BACKGROUND
    raise MaskTileError(f"A MaskTile needs a kind of FOREGROUND or BACKGROUND, got {kind}.")


def parse_mask_tile(tile) -> Tuple[str, TileIndex, _Tile]:
    """One ``MaskTile`` message as ``(kind, tile index, content)``.

    Checks what a single tile can be checked for: the kind, the scale (0, 1 or 4), ``full`` with a png, and a png that is
    1024 x 1024 (scale 0 or 1) or 256 x 256 (scale 4). The coordinate's identity and duplicates are the
    :class:`MaskTileSet`'s to check.

    Raises:
        MaskTileError: The tile is malformed.
    """
    kind = _kind_name(int(tile.kind))
    index = TileIndex(row=int(tile.coord.row), col=int(tile.coord.col))
    scale = int(tile.scale)
    if scale not in MASK_TILE_SCALES:
        raise MaskTileError(f"A MaskTile scale must be 0, 1 or 4, got {scale}.")
    scale = max(scale, 1)
    if tile.full:
        if tile.png:
            raise MaskTileError("A full MaskTile has no png.")
        return kind, index, _FULL_TILE
    if not tile.png:
        return kind, index, _EMPTY_TILE
    side = TILE_SIZE // scale
    declared = _png_size(bytes(tile.png))
    if declared is not None and declared != (side, side):
        raise MaskTileError(
            f"A MaskTile png must be {side}x{side} at scale {scale}, got {declared[0]}x{declared[1]} "
            f"(row={index.row} col={index.col})."
        )
    image = cv2.imdecode(np.frombuffer(tile.png, dtype=np.uint8), cv2.IMREAD_GRAYSCALE)
    if image is None or image.shape != (side, side):
        got = "an undecodable png" if image is None else f"{image.shape[1]}x{image.shape[0]}"
        raise MaskTileError(
            f"A MaskTile png must decode to {side}x{side} at scale {scale}, got {got} (row={index.row} col={index.col})."
        )
    bitmap = image > 0
    if not bitmap.any():
        return kind, index, _EMPTY_TILE
    if bitmap.all():
        return kind, index, _FULL_TILE
    return kind, index, _Tile(bitmap=bitmap, scale=scale)


class TiledMask:
    """One kind of a request's mask prompt, tile by tile, with the interface of ``cell_grid.MaskShape``.

    ``role`` is what a foreground mask is (``"seed"`` or ``"correction"``). Tiles are added with :meth:`put`; a tile that has
    never been put is *missing*, one put empty is *received* and holds nothing. Coordinates are mosaic pixels (X right, Y up);
    a tile's arrays are Y-down like the image tile, window arrays too, and ``crop_up`` is Y-up like ``MaskShape.bitmap_up``.
    """

    is_rectangle = False

    def __init__(self, kind: str = FOREGROUND, role: str = "seed") -> None:
        self.kind = kind
        self.role = role
        self._tiles: Dict[Tuple[int, int], _Tile] = {}
        self._version = 0
        self._cached_box: Optional[Tuple[int, Optional[Box]]] = None

    # -- filling -----------------------------------------------------------------------------------------------

    def put(self, index: TileIndex, tile: _Tile) -> None:
        """Hold ``tile`` for ``index``, replacing what was there."""
        self._tiles[(index.row, index.col)] = tile
        self._version += 1

    def mark_unavailable(self, index: TileIndex) -> None:
        """Take ``index`` as received and empty: the client could not supply it, so it has no neighbours."""
        if (index.row, index.col) not in self._tiles:
            self.put(index, _EMPTY_TILE)

    def received(self, index: TileIndex) -> bool:
        return (index.row, index.col) in self._tiles

    @property
    def tile_count(self) -> int:
        """Tiles received, empty ones included."""
        return len(self._tiles)

    def missing_for_cell(self, cell: Cell) -> List[TileIndex]:
        """The tiles ``cell``'s window covers that were never received, in the order the window lists them."""
        return [tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in self._tiles]

    # -- shape --------------------------------------------------------------------------------------------------

    @property
    def is_empty(self) -> bool:
        """True when no received tile holds a pixel."""
        return self.box is None

    @property
    def box(self) -> Optional[Box]:
        """The tightest inclusive ``(x_min, y_min, x_max, y_max)`` around every set pixel (a scale-4 block counts whole), or None."""
        if self._cached_box is not None and self._cached_box[0] == self._version:
            return self._cached_box[1]
        x0 = y0 = None
        x1 = y1 = None

        def widen(left: int, bottom: int, right: int, top: int) -> None:
            nonlocal x0, y0, x1, y1
            x0 = left if x0 is None else min(x0, left)
            y0 = bottom if y0 is None else min(y0, bottom)
            x1 = right if x1 is None else max(x1, right)
            y1 = top if y1 is None else max(y1, top)

        for (row, col), tile in self._tiles.items():
            left, bottom = col * TILE_SIZE, row * TILE_SIZE
            if tile.full:
                widen(left, bottom, left + TILE_SIZE - 1, bottom + TILE_SIZE - 1)
            elif tile.bitmap is not None:
                rows = np.nonzero(tile.bitmap.any(axis=1))[0]
                cols = np.nonzero(tile.bitmap.any(axis=0))[0]
                if rows.size == 0:
                    continue
                scale = tile.scale
                top = bottom + TILE_SIZE - 1
                widen(
                    left + int(cols[0]) * scale,
                    top - (int(rows[-1]) * scale + scale - 1),
                    left + int(cols[-1]) * scale + scale - 1,
                    top - int(rows[0]) * scale,
                )
        box: Optional[Box] = None if x0 is None else (int(x0), int(y0), int(x1), int(y1))  # type: ignore[arg-type]
        self._cached_box = (self._version, box)
        return box

    def crop_up(self, xa: int, ya: int, xb: int, yb: int) -> NDArray[np.bool_]:
        """The mask over the inclusive mosaic rectangle as ``(yb - ya + 1, xb - xa + 1)``, Y-up (row 0 is ``ya``).

        Pixels in tiles that were never received are False.
        """
        out = np.zeros((yb - ya + 1, xb - xa + 1), dtype=np.bool_)
        for row in range(ya // TILE_SIZE, yb // TILE_SIZE + 1):
            for col in range(xa // TILE_SIZE, xb // TILE_SIZE + 1):
                tile = self._tiles.get((row, col))
                if tile is None or tile.empty:
                    continue
                left, bottom = col * TILE_SIZE, row * TILE_SIZE
                ta, tb = max(xa, left), min(xb, left + TILE_SIZE - 1)
                sa, sb = max(ya, bottom), min(yb, bottom + TILE_SIZE - 1)
                target = (slice(sa - ya, sb - ya + 1), slice(ta - xa, tb - xa + 1))
                if tile.full:
                    out[target] = True
                    continue
                pixels = tile.pixels()
                top = bottom + TILE_SIZE - 1
                # Tile row r is mosaic y = top - r, so mosaic rows sa..sb are tile rows top-sb..top-sa, reversed for Y-up.
                out[target] = pixels[top - sb:top - sa + 1, ta - left:tb - left + 1][::-1]
        return out

    def covers_core(self, cell: Cell) -> bool:
        """True when every pixel of ``cell``'s core is in the mask."""
        bx, by = core_origin(cell)
        for tile in tiles_overlapping(bx, by, bx + CORE_SIZE - 1, by + CORE_SIZE - 1):
            held = self._tiles.get((tile.row, tile.col))
            if held is None or held.empty:
                return False
        return bool(self.crop_up(bx, by, bx + CORE_SIZE - 1, by + CORE_SIZE - 1).all())

    def window_full(self, cell: Cell) -> Optional[NDArray[np.bool_]]:
        """The mask over ``cell``'s window at full resolution, ``CELL_SIZE`` square (Y-down), or None when none of it is set."""
        wx0, wy0 = window_origin(cell)
        if not any(
            (tile.row, tile.col) in self._tiles and not self._tiles[(tile.row, tile.col)].empty
            for tile in tiles_for_cell(cell)
        ):
            return None
        pixels = np.flipud(self.crop_up(wx0, wy0, wx0 + CELL_SIZE - 1, wy0 + CELL_SIZE - 1))
        return pixels if pixels.any() else None

    def window_samples(self, cell: Cell) -> Optional[NDArray[np.bool_]]:
        """The mask over ``cell``'s window as a ``LOGIT_WINDOW`` square (Y-down): a sample is set when any of its pixels are."""
        pixels = self.window_full(cell)
        if pixels is None:
            return None
        samples = pixels.reshape(LOGIT_WINDOW, LOGIT_SCALE, LOGIT_WINDOW, LOGIT_SCALE).any(axis=(1, 3))
        return samples if samples.any() else None

    def interior_point(self) -> Point:
        """A mosaic pixel in the mask, the set pixel nearest the centroid of the set pixels.

        Ties go to the lowest y, then the lowest x, as they do for a ``MaskShape`` that holds the same pixels as one bitmap.
        Falls back to the box centre of an empty mask (``(0, 0)`` when there is no box).
        """
        box = self.box
        if box is None:
            return 0, 0
        count = 0
        sum_x = sum_y = 0.0
        sources: List[Tuple[Tuple[int, int], _Tile]] = []
        for key, tile in self._tiles.items():
            if tile.empty:
                continue
            row, col = key
            left, bottom = col * TILE_SIZE, row * TILE_SIZE
            if tile.full:
                n = TILE_SIZE * TILE_SIZE
                count += n
                sum_x += n * (left + (TILE_SIZE - 1) / 2.0)
                sum_y += n * (bottom + (TILE_SIZE - 1) / 2.0)
            else:
                pixels = tile.pixels()
                n = int(pixels.sum())
                count += n
                columns = np.arange(TILE_SIZE, dtype=np.float64) + left
                rows_up = (bottom + TILE_SIZE - 1) - np.arange(TILE_SIZE, dtype=np.float64)
                sum_x += float((pixels.sum(axis=0) * columns).sum())
                sum_y += float((pixels.sum(axis=1) * rows_up).sum())
            sources.append((key, tile))
        if count == 0:
            return (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
        centre_x, centre_y = sum_x / count, sum_y / count

        def rectangle_distance(key: Tuple[int, int]) -> float:
            row, col = key
            left, bottom = col * TILE_SIZE, row * TILE_SIZE
            dx = max(left - centre_x, 0.0, centre_x - (left + TILE_SIZE - 1))
            dy = max(bottom - centre_y, 0.0, centre_y - (bottom + TILE_SIZE - 1))
            return dx * dx + dy * dy

        best: Optional[Tuple[float, int, int]] = None
        for key, tile in sorted(sources, key=lambda item: rectangle_distance(item[0])):
            if best is not None and rectangle_distance(key) > best[0]:
                break
            row, col = key
            left, bottom = col * TILE_SIZE, row * TILE_SIZE
            if tile.full:
                x = int(np.clip(round(centre_x), left, left + TILE_SIZE - 1))
                y = int(np.clip(round(centre_y), bottom, bottom + TILE_SIZE - 1))
                candidates = [(x, y)]
            else:
                pixels = tile.pixels()
                rows_down, cols = np.nonzero(pixels)
                xs = cols + left
                ys = (bottom + TILE_SIZE - 1) - rows_down
                distance = (xs - centre_x) ** 2 + (ys - centre_y) ** 2
                nearest = float(distance.min())
                tied = np.nonzero(distance == nearest)[0]
                pick = min(tied, key=lambda i: (int(ys[i]), int(xs[i])))
                candidates = [(int(xs[pick]), int(ys[pick]))]
            for x, y in candidates:
                d = (x - centre_x) ** 2 + (y - centre_y) ** 2
                if best is None or (d, y, x) < best:
                    best = (d, y, x)
        assert best is not None
        return best[2], best[1]


def tiles_overlapping(xa: int, ya: int, xb: int, yb: int) -> List[TileIndex]:
    """The upload tiles an inclusive mosaic rectangle touches."""
    return [
        TileIndex(row=row, col=col)
        for row in range(ya // TILE_SIZE, yb // TILE_SIZE + 1)
        for col in range(xa // TILE_SIZE, xb // TILE_SIZE + 1)
    ]


class MaskTileSet:
    """A request's two tiled masks, and the checks on the messages that fill them.

    ``identity`` is any object with ``volume``, ``section``, ``channel``, ``transform`` and ``downsample`` (the request's
    ``TileCoord``): a mask tile must name the same ones. ``announced_foreground`` is the request's
    ``foreground_mask_tile_count``.
    """

    def __init__(
        self,
        identity,
        announced_foreground: int,
        max_foreground: int = DEFAULT_MAX_FOREGROUND_MASK_TILES,
        max_background: int = DEFAULT_MAX_BACKGROUND_MASK_TILES,
        role: str = "seed",
    ) -> None:
        self._identity = identity
        self.announced_foreground = int(announced_foreground)
        self._max_foreground = max_foreground
        self._max_background = max_background
        self.foreground = TiledMask(FOREGROUND, role)
        self.background = TiledMask(BACKGROUND)
        self._seen: Set[Tuple[str, int, int]] = set()

    @property
    def foreground_complete(self) -> bool:
        """True once as many foreground tiles have arrived as the request announced."""
        return self.foreground.tile_count >= self.announced_foreground

    def ingest(self, tiles: Iterable) -> List[Tuple[str, TileIndex]]:
        """Validate and hold the ``MaskTile`` messages of one ``MaskTiles``. Returns ``(kind, index)`` for each accepted tile.

        Raises:
            MaskTileError: A tile is malformed, names another downsample (or volume, section, channel or transform), repeats a
                ``(coord, kind)`` already received, or is a foreground tile beyond the announced count.
            MaskTileLimitError: More foreground or background tiles than the server holds.
        """
        accepted: List[Tuple[str, TileIndex]] = []
        for tile in tiles:
            kind, index, content = parse_mask_tile(tile)
            self._check_identity(tile.coord)
            key = (kind, index.row, index.col)
            if key in self._seen:
                raise MaskTileError(f"A {kind} MaskTile for row={index.row} col={index.col} was sent twice.")
            if kind == FOREGROUND:
                if self.foreground.tile_count >= self.announced_foreground:
                    raise MaskTileError(
                        f"More FOREGROUND MaskTiles than the {self.announced_foreground} announced "
                        "(foreground_mask_tile_count)."
                    )
                if self.foreground.tile_count >= self._max_foreground:
                    raise MaskTileLimitError(
                        f"More than {self._max_foreground} foreground mask tiles "
                        "(SEGMENTATION_MAX_FOREGROUND_MASK_TILES)."
                    )
                self.foreground.put(index, content)
            else:
                if self.background.tile_count >= self._max_background:
                    raise MaskTileLimitError(
                        f"More than {self._max_background} background mask tiles "
                        "(SEGMENTATION_MAX_BACKGROUND_MASK_TILES)."
                    )
                self.background.put(index, content)
            self._seen.add(key)
            accepted.append((kind, index))
        return accepted

    def _check_identity(self, coord) -> None:
        identity = self._identity
        if (
            coord.volume != identity.volume
            or coord.section != identity.section
            or coord.channel != identity.channel
            or coord.transform != identity.transform
            or coord.downsample != identity.downsample
        ):
            raise MaskTileError(
                "A MaskTile must share volume, section, channel, transform and downsample with the request's tiles "
                f"(got downsample {coord.downsample}, expected {identity.downsample})."
            )

    def mark_background_unavailable(self, index: TileIndex) -> None:
        """A background tile the client could not supply: received, empty, no neighbours."""
        self.background.mark_unavailable(index)
        self._seen.add((BACKGROUND, index.row, index.col))
