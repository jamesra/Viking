"""Grow a segmentation across a fixed 1024 grid.

Tiles are a partition: step equals tile size, anchored at mosaic origin.
Mosaic pixels use X right and Y up (Viking world divided by downsample).
Each tile image is stored Y-down, so image row 0 is the high-Y edge of the cell.
The server flips points into that image before predict().
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import Enum
from typing import Callable, List, Optional, Sequence, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

TILE_SIZE = 1024
BORDER_BAND_PX = 2
SEED_SPACING_PX = 32
SEED_INSET_PX = 8
DEFAULT_MAX_REQUESTED_TILES = 8

Point = Tuple[int, int]
MaskArray = NDArray[np.bool_]
LogitsArray = NDArray[np.float32]

# (row, col, tile-local image points y-down, labels) -> mask, logits or None, score
TilePredict = Callable[
    [int, int, Sequence[Point], Sequence[int]],
    Tuple[MaskArray, Optional[NDArray], float],
]


class _Side(Enum):
    """Image-edge of a tile. TOP is image row 0, which is the high mosaic-Y neighbor."""

    RIGHT = "right"
    LEFT = "left"
    TOP = "top"
    BOTTOM = "bottom"


@dataclass(frozen=True)
class TileIndex:
    """Row increases with mosaic Y (up). Column increases with mosaic X."""

    row: int
    col: int


@dataclass
class TilePrediction:
    row: int
    col: int
    mask: MaskArray
    logits: Optional[LogitsArray]
    score: float


@dataclass
class GrowthResult:
    """Fused mosaic and any cells the caller still needs to upload."""

    mask: MaskArray
    origin_x: int
    origin_y: int
    score: float
    requested: List[TileIndex] = field(default_factory=list)


def max_requested_tiles_from_env() -> int:
    """Read SEGMENT_MAX_REQUESTED_TILES. Missing or invalid values use the default."""
    raw = os.environ.get("SEGMENT_MAX_REQUESTED_TILES")
    if raw is None or raw.strip() == "":
        return DEFAULT_MAX_REQUESTED_TILES
    try:
        return max(0, int(raw))
    except ValueError:
        return DEFAULT_MAX_REQUESTED_TILES


def tile_of_point(x: int, y: int, tile_size: int = TILE_SIZE) -> TileIndex:
    """Cell containing a mosaic point. Y is up."""
    return TileIndex(row=_floor_div(y, tile_size), col=_floor_div(x, tile_size))


def point_in_tile(x: int, y: int, tile: TileIndex, tile_size: int = TILE_SIZE) -> bool:
    return tile.col * tile_size <= x < (tile.col + 1) * tile_size and (
        tile.row * tile_size <= y < (tile.row + 1) * tile_size
    )


def mosaic_to_image(x: int, y: int, tile: TileIndex, tile_size: int = TILE_SIZE) -> Point:
    """Map a mosaic point (Y up) into tile-local image pixels (Y down)."""
    local_x = x - tile.col * tile_size
    local_from_bottom = y - tile.row * tile_size
    image_y = tile_size - 1 - local_from_bottom
    return int(local_x), int(image_y)


def grow_segmentation(
    uploaded: Sequence[TileIndex],
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: TilePredict,
    max_requested: int = DEFAULT_MAX_REQUESTED_TILES,
    tile_size: int = TILE_SIZE,
    border_band: int = BORDER_BAND_PX,
    seed_spacing: int = SEED_SPACING_PX,
    seed_inset: int = SEED_INSET_PX,
) -> GrowthResult:
    """Predict tiles that hold foreground points, then walk outward across borders.

    A neighbor that is already uploaded is seeded along the contact run and predicted.
    A neighbor that is not uploaded is appended to requested, up to max_requested.
    Sides already expanded are not visited again.
    """
    uploaded_set = set(uploaded)
    fg = [(int(x), int(y)) for x, y in foreground]
    bg = [(int(x), int(y)) for x, y in background]

    if not fg:
        return GrowthResult(mask=np.zeros((0, 0), dtype=np.bool_), origin_x=0, origin_y=0, score=0.0)

    initial = []
    seen_initial = set()
    missing_fg: List[TileIndex] = []
    seen_missing = set()
    for x, y in fg:
        tile = tile_of_point(x, y, tile_size)
        if tile in uploaded_set:
            if tile not in seen_initial:
                seen_initial.add(tile)
                initial.append(tile)
        elif tile not in seen_missing:
            seen_missing.add(tile)
            missing_fg.append(tile)

    predicted: dict[TileIndex, TilePrediction] = {}
    expanded: set[Tuple[TileIndex, _Side]] = set()
    requested: List[TileIndex] = []
    requested_set: set[TileIndex] = set()

    def _request(tile: TileIndex) -> None:
        if tile in requested_set or len(requested) >= max_requested:
            return
        requested.append(tile)
        requested_set.add(tile)

    for tile in missing_fg:
        _request(tile)

    work: List[TileIndex] = []
    for tile in initial:
        if _predict_tile(tile, fg, bg, predicted, predict, tile_size):
            work.append(tile)

    while work:
        tile = work.pop(0)
        pred = predicted[tile]
        for side in _Side:
            if (tile, side) in expanded:
                continue
            neighbor = _neighbor(tile, side)
            if neighbor in predicted:
                expanded.add((tile, side))
                continue
            if not _border_contact(pred, side, tile_size, border_band):
                continue
            expanded.add((tile, side))
            if neighbor in uploaded_set:
                if _predict_tile(
                    neighbor,
                    fg,
                    bg,
                    predicted,
                    predict,
                    tile_size,
                    extra_fg=_seed_points(pred, side, tile_size, border_band, seed_spacing, seed_inset),
                ):
                    work.append(neighbor)
            else:
                _request(neighbor)

    return _fuse(predicted, requested, tile_size)


def _predict_tile(
    tile: TileIndex,
    foreground: Sequence[Point],
    background: Sequence[Point],
    predicted: dict[TileIndex, TilePrediction],
    predict: TilePredict,
    tile_size: int,
    extra_fg: Sequence[Point] = (),
) -> bool:
    if tile in predicted:
        return False
    points: List[Point] = []
    labels: List[int] = []
    seen: set[Tuple[int, int, int]] = set()

    def _add(local: Point, label: int) -> None:
        lx, ly = int(local[0]), int(local[1])
        if lx < 0 or ly < 0 or lx >= tile_size or ly >= tile_size:
            return
        key = (lx, ly, label)
        if key in seen:
            return
        seen.add(key)
        points.append((lx, ly))
        labels.append(label)

    for x, y in foreground:
        if point_in_tile(x, y, tile, tile_size):
            _add(mosaic_to_image(x, y, tile, tile_size), 1)
    for x, y in extra_fg:
        _add((x, y), 1)
    for x, y in background:
        if point_in_tile(x, y, tile, tile_size):
            _add(mosaic_to_image(x, y, tile, tile_size), 0)

    if not any(label == 1 for label in labels):
        return False

    mask, logits, score = predict(tile.row, tile.col, points, labels)
    mask_bool = np.asarray(mask, dtype=np.bool_)
    if mask_bool.ndim != 2:
        mask_bool = np.zeros((tile_size, tile_size), dtype=np.bool_)
    predicted[tile] = TilePrediction(
        row=tile.row,
        col=tile.col,
        mask=mask_bool,
        logits=_resize_logits(logits, mask_bool.shape),
        score=float(score),
    )
    return True


def _neighbor(tile: TileIndex, side: _Side) -> TileIndex:
    if side is _Side.RIGHT:
        return TileIndex(tile.row, tile.col + 1)
    if side is _Side.LEFT:
        return TileIndex(tile.row, tile.col - 1)
    if side is _Side.TOP:
        return TileIndex(tile.row + 1, tile.col)
    return TileIndex(tile.row - 1, tile.col)


def _border_contact(pred: TilePrediction, side: _Side, tile_size: int, band: int) -> bool:
    mask = pred.mask
    if mask.size == 0:
        return False
    height, width = mask.shape
    band = max(1, min(band, height, width))
    region = _edge_slice(side, height, width, band)
    if np.any(mask[region]):
        return True
    if pred.logits is None:
        return False
    logits = pred.logits
    if logits.shape != mask.shape:
        return False
    return bool(np.any(logits[region] > 0))


def _edge_slice(side: _Side, height: int, width: int, band: int) -> Tuple[slice, slice]:
    if side is _Side.LEFT:
        return slice(0, height), slice(0, band)
    if side is _Side.RIGHT:
        return slice(0, height), slice(width - band, width)
    if side is _Side.TOP:
        return slice(0, band), slice(0, width)
    return slice(height - band, height), slice(0, width)


def _seed_points(
    pred: TilePrediction,
    side: _Side,
    tile_size: int,
    band: int,
    spacing: int,
    inset: int,
) -> List[Point]:
    """Foreground seeds in the neighbor's image, inset from the shared edge."""
    height, width = pred.mask.shape
    band = max(1, min(band, height, width))
    spacing = max(1, spacing)
    inset = max(0, min(inset, tile_size - 1))
    along = _contact_indices(pred, side, band)
    if not along:
        return []
    samples = _sample_runs(along, spacing)
    seeds: List[Point] = []
    for index in samples:
        if side is _Side.RIGHT:
            seeds.append((inset, int(np.clip(index, 0, tile_size - 1))))
        elif side is _Side.LEFT:
            seeds.append((tile_size - 1 - inset, int(np.clip(index, 0, tile_size - 1))))
        elif side is _Side.TOP:
            seeds.append((int(np.clip(index, 0, tile_size - 1)), tile_size - 1 - inset))
        else:
            seeds.append((int(np.clip(index, 0, tile_size - 1)), inset))
    return seeds


def _contact_indices(pred: TilePrediction, side: _Side, band: int) -> List[int]:
    mask = pred.mask
    height, width = mask.shape
    logits = pred.logits
    indices: List[int] = []
    if side in (_Side.LEFT, _Side.RIGHT):
        cols = range(band) if side is _Side.LEFT else range(width - band, width)
        for image_y in range(height):
            hit = any(bool(mask[image_y, col]) for col in cols)
            if not hit and logits is not None and logits.shape == mask.shape:
                hit = any(float(logits[image_y, col]) > 0 for col in cols)
            if hit:
                indices.append(image_y)
        return indices
    rows = range(band) if side is _Side.TOP else range(height - band, height)
    for image_x in range(width):
        hit = any(bool(mask[row, image_x]) for row in rows)
        if not hit and logits is not None and logits.shape == mask.shape:
            hit = any(float(logits[row, image_x]) > 0 for row in rows)
        if hit:
            indices.append(image_x)
    return indices


def _sample_runs(indices: Sequence[int], spacing: int) -> List[int]:
    if not indices:
        return []
    ordered = sorted(set(indices))
    samples: List[int] = []
    run_start = ordered[0]
    previous = ordered[0]
    for index in ordered[1:]:
        if index - previous > 1:
            samples.extend(_sample_run(run_start, previous, spacing))
            run_start = index
        previous = index
    samples.extend(_sample_run(run_start, previous, spacing))
    return samples


def _sample_run(start: int, end: int, spacing: int) -> List[int]:
    return list(range(start, end + 1, spacing))


def _resize_logits(logits: Optional[NDArray], shape: Tuple[int, int]) -> Optional[LogitsArray]:
    if logits is None:
        return None
    arr = np.asarray(logits, dtype=np.float32)
    if arr.ndim == 3:
        arr = arr[0]
    if arr.ndim != 2:
        return None
    if arr.shape == shape:
        return arr
    resized = cv2.resize(arr, (shape[1], shape[0]), interpolation=cv2.INTER_LINEAR)
    return resized.astype(np.float32, copy=False)


def _fuse(
    predicted: dict[TileIndex, TilePrediction],
    requested: List[TileIndex],
    tile_size: int,
) -> GrowthResult:
    if not predicted:
        return GrowthResult(
            mask=np.zeros((0, 0), dtype=np.bool_),
            origin_x=0,
            origin_y=0,
            score=0.0,
            requested=requested,
        )
    rows = [tile.row for tile in predicted]
    cols = [tile.col for tile in predicted]
    min_row, max_row = min(rows), max(rows)
    min_col, max_col = min(cols), max(cols)
    height = (max_row - min_row + 1) * tile_size
    width = (max_col - min_col + 1) * tile_size
    mosaic = np.zeros((height, width), dtype=np.bool_)
    scores = []
    for tile, pred in predicted.items():
        x0 = (tile.col - min_col) * tile_size
        y0 = (max_row - tile.row) * tile_size
        tile_mask = pred.mask
        if tile_mask.shape != (tile_size, tile_size):
            tile_mask = cv2.resize(
                tile_mask.astype(np.uint8),
                (tile_size, tile_size),
                interpolation=cv2.INTER_NEAREST,
            ).astype(np.bool_)
        view = mosaic[y0:y0 + tile_size, x0:x0 + tile_size]
        view[:] = np.maximum(view, tile_mask)
        if np.any(tile_mask):
            scores.append(pred.score)
    return GrowthResult(
        mask=mosaic,
        origin_x=min_col * tile_size,
        origin_y=min_row * tile_size,
        score=min(scores) if scores else 0.0,
        requested=requested,
    )


def _floor_div(value: int, size: int) -> int:
    if size <= 0:
        raise ValueError("tile size must be positive")
    if value >= 0:
        return value // size
    return -((-value + size - 1) // size)
