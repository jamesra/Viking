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
# Outermost pixel of a tile. A wider band, or a positive logit short of that
# pixel, treated a circle that never reached the cut as a crossing.
BORDER_BAND_PX = 1
SEED_SPACING_PX = 32
# On the shared edge. An inset on a single tile landed on the membrane that runs
# along the cut, and SAM2 followed that membrane instead of the cell that crossed.
# Seam crossing uses a half-tile instead of this inset. See seam_predict.
SEED_INSET_PX = 0
# Into the already-segmented half of a synthetic seam image. The point is then
# in the interior of that image, with pixels on both sides of the cut.
SEAM_PROMPT_INSET_PX = 32
DEFAULT_MAX_REQUESTED_TILES = 8
# One return trip per side is enough for a C that comes back onto this tile.
# The cap stops a thin edge that never fills from predicting again without bound.
MAX_RESEEDS_PER_TILE = 4

Point = Tuple[int, int]
MaskArray = NDArray[np.bool_]
LogitsArray = NDArray[np.float32]

# (row, col, tile-local image points y-down, labels) -> mask, logits or None, score
TilePredict = Callable[
    [int, int, Sequence[Point], Sequence[int]],
    Tuple[MaskArray, Optional[NDArray], float],
]
# (src_row, src_col, dst_row, dst_col, side, synthetic-image points, labels)
# -> mask of the half-tile, logits or None, score
SeamPredict = Callable[
    [int, int, int, int, str, Sequence[Point], Sequence[int]],
    Tuple[MaskArray, Optional[NDArray], float],
]


class PredictUnavailable(Exception):
    """This cell needs a new SAM2 call and no predictor is pinned.

    Growth asks the client to upload the cell again. A mask already stored for
    the cell stays in the composite; only the new call is skipped.
    """


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


# Canonical (row_a, col_a, row_b, col_b) with the first cell ordered before the second.
EdgeKey = Tuple[int, int, int, int]
PixelRange = Tuple[int, int]


@dataclass
class SeamGraph:
    """Undirected borders between tiles. Each edge holds the pixel ranges already resolved.

    One shared cut is one edge, whether the walk arrives from the left or the right.
    The ranges are inclusive indexes along that border. Overlapping or touching
    ranges collapse into one interval, so a field-sized mask becomes
    ``(0, tile_size - 1)``. A later contact that overlaps an interval does not
    travel to the neighbor again, unless that contact is at least twice as long
    as the stored range or it joins two stored ranges into one. A stretch that
    does not overlap is still crossed. The growth session keeps this graph so
    the next call for the same clicks starts from the ranges already crossed.
    """

    edges: dict[EdgeKey, List[PixelRange]] = field(default_factory=dict)

    def ranges(self, tile: TileIndex, neighbor: TileIndex) -> Sequence[PixelRange]:
        """Intervals already resolved on the border between these two cells."""
        return self.edges.get(_edge_key(tile, neighbor), ())

    def claim(self, tile: TileIndex, neighbor: TileIndex, runs: Sequence[PixelRange]) -> None:
        """Merge ``runs`` into the one edge that joins these cells."""
        if not runs:
            return
        key = _edge_key(tile, neighbor)
        self.edges[key] = _merge_ranges([*self.edges.get(key, ()), *runs])

    def copy(self) -> "SeamGraph":
        """A graph the caller can mutate without aliasing this one."""
        return SeamGraph(edges={key: list(runs) for key, runs in self.edges.items()})

    def drop_tile(self, row: int, col: int) -> None:
        """Drop every edge that touches this cell. Used when its bytes are replaced."""
        cell = (int(row), int(col))
        stale = [
            key
            for key in self.edges
            if (key[0], key[1]) == cell or (key[2], key[3]) == cell
        ]
        for key in stale:
            del self.edges[key]


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
    # Per-cell masks after reseeds. The growth store copies these for the next call.
    tiles: dict[TileIndex, TilePrediction] = field(default_factory=dict)


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


class GrowthCancelled(Exception):
    """The walk stopped because the caller asked it to, before the next SAM2 call."""


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
    remembered: Optional[dict[TileIndex, TilePrediction]] = None,
    should_stop: Optional[Callable[[], bool]] = None,
    seam_predict: Optional[SeamPredict] = None,
    seam_graph: Optional[SeamGraph] = None,
) -> GrowthResult:
    """Predict tiles that hold foreground points, then walk outward across borders.

    When ``seam_predict`` is set, a border is crossed by building a half-tile
    centered on the cut and prompting inside the already-segmented half. The
    two halves of that mask replace the seam side of each tile, so the polygon
    is not the tile edge. Without ``seam_predict``, an uploaded neighbor is
    seeded on the shared edge and predicted as its own image.
    A neighbor that is not uploaded is appended to requested, up to max_requested.
    A tile is predicted again when a neighbor's mask comes back across an edge this
    tile does not already cover, and the new mask is OR'd in. That finds a second
    region on the first tile that meets the click only through the neighbor.
    Sides already expanded are not visited again until a reseed grows that tile.

    ``remembered`` tiles are fused even when this call did not upload them. Their
    borders are walked. ``predict`` may raise ``PredictUnavailable`` when a new
    SAM2 call has no pinned predictor; that cell is requested and any mask already
    in ``remembered`` stays in the composite. ``should_stop`` returning true raises
    ``GrowthCancelled`` before the next SAM2 call and between walk steps.

    ``seam_graph`` is the borders already crossed for this click set. The walk
    mutates it. A contact that overlaps a stored range is not searched. Pass the
    session's graph so the next call starts from those ranges. When omitted, the
    ranges live only for this call.
    """
    uploaded_set = set(uploaded)
    fg = [(int(x), int(y)) for x, y in foreground]
    bg = [(int(x), int(y)) for x, y in background]

    if not fg:
        return GrowthResult(mask=np.zeros((0, 0), dtype=np.bool_), origin_x=0, origin_y=0, score=0.0)

    def guarded_predict(row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        if should_stop is not None and should_stop():
            raise GrowthCancelled()
        return predict(row, col, points, labels)

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
    queued: set[TileIndex] = set()
    tried_seeds: dict[TileIndex, set[Point]] = {}
    reseed_counts: dict[TileIndex, int] = {}
    # One undirected edge per cut. An overlapping range is the same cut, so the
    # walk does not search the original tile again. A C that comes back on a
    # stretch that does not overlap is a new range and is still crossed.
    graph = seam_graph if seam_graph is not None else SeamGraph()
    if remembered:
        for tile, pred in remembered.items():
            predicted[tile] = _clone_prediction(pred)
            _enqueue(work, queued, tile)
    for tile in initial:
        if tile in predicted:
            _enqueue(work, queued, tile)
            continue
        try:
            ready = _predict_tile(tile, fg, bg, predicted, guarded_predict, tile_size)
        except PredictUnavailable:
            _request(tile)
            continue
        if ready:
            _enqueue(work, queued, tile)

    while work:
        if should_stop is not None and should_stop():
            raise GrowthCancelled()
        tile = work.pop(0)
        queued.discard(tile)
        pred = predicted[tile]
        for side in _Side:
            if (tile, side) in expanded:
                continue
            if seam_predict is not None:
                if not _deep_contact_runs(pred, side, tile_size):
                    continue
            elif not _border_contact(pred, side, tile_size, border_band):
                continue
            expanded.add((tile, side))
            neighbor = _neighbor(tile, side)
            if seam_predict is not None:
                if neighbor not in uploaded_set:
                    _request(neighbor)
                    continue
                try:
                    _cross_seam(
                        tile,
                        side,
                        neighbor,
                        pred,
                        fg,
                        bg,
                        predicted,
                        expanded,
                        graph,
                        work,
                        queued,
                        guarded_predict,
                        seam_predict,
                        tile_size,
                        border_band,
                        seed_spacing,
                        should_stop,
                    )
                except PredictUnavailable:
                    _request(neighbor)
                continue
            seeds = _seed_points(pred, side, tile_size, border_band, seed_spacing, seed_inset)
            if neighbor in predicted:
                try:
                    grew = _reseed_predicted_tile(
                        neighbor,
                        fg,
                        bg,
                        predicted,
                        guarded_predict,
                        tile_size,
                        seeds,
                        _opposite(side),
                        border_band,
                        tried_seeds,
                        reseed_counts,
                    )
                except PredictUnavailable:
                    _request(neighbor)
                    continue
                if grew:
                    _drop_expanded(expanded, neighbor)
                    _enqueue(work, queued, neighbor)
                continue
            if neighbor in uploaded_set:
                try:
                    ready = _predict_tile(
                        neighbor,
                        fg,
                        bg,
                        predicted,
                        guarded_predict,
                        tile_size,
                        extra_fg=seeds,
                    )
                except PredictUnavailable:
                    _request(neighbor)
                    continue
                if ready:
                    _enqueue(work, queued, neighbor)
            else:
                _request(neighbor)

    result = _fuse(predicted, requested, tile_size)
    result.tiles = predicted
    return result


def _clone_prediction(pred: TilePrediction) -> TilePrediction:
    """Copy arrays so a reseed cannot mutate a mask the caller still holds."""
    logits = None if pred.logits is None else np.array(pred.logits, copy=True)
    return TilePrediction(
        row=int(pred.row),
        col=int(pred.col),
        mask=np.array(pred.mask, copy=True),
        logits=logits,
        score=float(pred.score),
    )


def _enqueue(work: List[TileIndex], queued: set[TileIndex], tile: TileIndex) -> None:
    if tile in queued:
        return
    work.append(tile)
    queued.add(tile)


def _drop_expanded(expanded: set[Tuple[TileIndex, _Side]], tile: TileIndex) -> None:
    """Let a tile walk its borders again after its mask gains pixels."""
    stale = [item for item in expanded if item[0] == tile]
    for item in stale:
        expanded.discard(item)


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
    points, labels = _prompt_points(tile, foreground, background, tile_size, extra_fg)
    if not any(label == 1 for label in labels):
        return False

    mask_bool, logits, score = _call_predict(tile, predict, points, labels, tile_size)
    predicted[tile] = TilePrediction(
        row=tile.row,
        col=tile.col,
        mask=mask_bool,
        logits=logits,
        score=score,
    )
    return True


def _reseed_predicted_tile(
    tile: TileIndex,
    foreground: Sequence[Point],
    background: Sequence[Point],
    predicted: dict[TileIndex, TilePrediction],
    predict: TilePredict,
    tile_size: int,
    extra_fg: Sequence[Point],
    facing_side: _Side,
    border_band: int,
    tried_seeds: dict[TileIndex, set[Point]],
    reseed_counts: dict[TileIndex, int],
) -> bool:
    """OR a second predict into a tile when neighbor seeds land on an empty edge.

    The first predict only sees the click, so a second region that leaves this tile
    and comes back is missing until those return seeds are applied. Seeds already
    sent, or whose edge sample is already inside the mask, are not sent again.
    """
    existing = predicted[tile]
    if reseed_counts.get(tile, 0) >= MAX_RESEEDS_PER_TILE:
        return False
    already = tried_seeds.setdefault(tile, set())
    fresh = [
        point
        for point in extra_fg
        if point not in already and not _edge_index_covered(existing, facing_side, point, border_band)
    ]
    if not fresh:
        return False
    already.update(fresh)
    reseed_counts[tile] = reseed_counts.get(tile, 0) + 1
    points, labels = _prompt_points(tile, foreground, background, tile_size, fresh)
    if not any(label == 1 for label in labels):
        return False
    mask_bool, logits, score = _call_predict(tile, predict, points, labels, tile_size)
    grew = bool(np.any(mask_bool & ~existing.mask))
    existing.mask = existing.mask | mask_bool
    existing.logits = _merge_logits(existing.logits, logits)
    if grew:
        existing.score = min(existing.score, score)
    return grew


def _prompt_points(
    tile: TileIndex,
    foreground: Sequence[Point],
    background: Sequence[Point],
    tile_size: int,
    extra_fg: Sequence[Point],
) -> Tuple[List[Point], List[int]]:
    """Tile-local prompts: clicks that fall in this cell, plus border seeds."""
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
    return points, labels


def _call_predict(
    tile: TileIndex,
    predict: TilePredict,
    points: Sequence[Point],
    labels: Sequence[int],
    tile_size: int,
) -> Tuple[MaskArray, Optional[LogitsArray], float]:
    mask, logits, score = predict(tile.row, tile.col, points, labels)
    mask_bool = np.asarray(mask, dtype=np.bool_)
    if mask_bool.ndim != 2:
        mask_bool = np.zeros((tile_size, tile_size), dtype=np.bool_)
    return mask_bool, _resize_logits(logits, mask_bool.shape), float(score)


def _merge_logits(
    existing: Optional[LogitsArray],
    new: Optional[LogitsArray],
) -> Optional[LogitsArray]:
    if new is None:
        return existing
    if existing is None or existing.shape != new.shape:
        return new
    return np.maximum(existing, new)


def _opposite(side: _Side) -> _Side:
    if side is _Side.RIGHT:
        return _Side.LEFT
    if side is _Side.LEFT:
        return _Side.RIGHT
    if side is _Side.TOP:
        return _Side.BOTTOM
    return _Side.TOP


def _edge_index_covered(pred: TilePrediction, facing: _Side, seed: Point, band: int) -> bool:
    """True when this tile's mask already touches the shared edge at the seed."""
    mask = pred.mask
    if mask.size == 0:
        return False
    height, width = mask.shape
    band = max(1, min(band, height, width))
    x, y = int(seed[0]), int(seed[1])
    if facing in (_Side.LEFT, _Side.RIGHT):
        row = int(np.clip(y, 0, height - 1))
        cols = range(band) if facing is _Side.LEFT else range(width - band, width)
        return any(bool(mask[row, col]) for col in cols)
    col = int(np.clip(x, 0, width - 1))
    rows = range(band) if facing is _Side.TOP else range(height - band, height)
    return any(bool(mask[row, col]) for row in rows)


def stitch_half_tile(source: NDArray, neighbor: NDArray, side: str) -> NDArray:
    """Image centered on the shared edge of two Y-down tiles.

    ``side`` is the source edge that touches ``neighbor`` (``right``, ``left``,
    ``top``, ``bottom``). TOP is image row 0. The cut lies on the center line.
    The source's near half stays on the source side of that line, so a prompt
    inset into the source is an interior point with pixels on both sides of the cut.
    """
    if side not in ("right", "left", "top", "bottom"):
        raise ValueError(f"unknown seam side {side}")
    half = source.shape[0] // 2
    stitched = np.empty_like(source)
    if side == "right":
        stitched[:, :half] = source[:, half: half * 2]
        stitched[:, half:] = neighbor[:, :half]
    elif side == "left":
        stitched[:, :half] = neighbor[:, half: half * 2]
        stitched[:, half:] = source[:, :half]
    elif side == "top":
        stitched[:half] = neighbor[half: half * 2]
        stitched[half:] = source[:half]
    else:
        stitched[:half] = source[half: half * 2]
        stitched[half:] = neighbor[:half]
    return stitched


def paste_half_mask(
    source_mask: MaskArray,
    neighbor_mask: Optional[MaskArray],
    synthetic: MaskArray,
    side: str,
    tile_size: int,
) -> Tuple[MaskArray, MaskArray]:
    """Write a half-tile mask back onto the seam half of each tile.

    Each pixel goes to whichever prediction has it nearer the center of its own image.
    The half-tile image is centered on the cut, so within ``tile_size // 4`` of the cut
    its answer replaces the tile's, and an edge-following strip on the cut does not
    survive. Farther from the cut the tile's own full-tile prediction is the better
    informed one, so its pixels are kept and the half-tile answer is added to them. The
    half-tile answer holds only the piece connected to its prompts inside that window;
    replacing the whole half deleted pieces that join the object outside the window and
    left a straight cut on the tile midline.
    The half of each tile that was outside the synthetic image is left as it was.
    """
    half = tile_size // 2
    reach = half // 2
    source = np.array(source_mask, dtype=np.bool_, copy=True)
    if neighbor_mask is None:
        neighbor = np.zeros((tile_size, tile_size), dtype=np.bool_)
    else:
        neighbor = np.array(neighbor_mask, dtype=np.bool_, copy=True)
    seam = np.asarray(synthetic, dtype=np.bool_)
    if side == "right":
        source[:, half:] = _merge_half(source[:, half:], seam[:, :half], "right", reach)
        neighbor[:, :half] = _merge_half(neighbor[:, :half], seam[:, half:], "left", reach)
    elif side == "left":
        source[:, :half] = _merge_half(source[:, :half], seam[:, half:], "left", reach)
        neighbor[:, half:] = _merge_half(neighbor[:, half:], seam[:, :half], "right", reach)
    elif side == "top":
        source[:half, :] = _merge_half(source[:half, :], seam[half:, :], "top", reach)
        neighbor[half:, :] = _merge_half(neighbor[half:, :], seam[:half, :], "bottom", reach)
    elif side == "bottom":
        source[half:, :] = _merge_half(source[half:, :], seam[:half, :], "bottom", reach)
        neighbor[:half, :] = _merge_half(neighbor[:half, :], seam[half:, :], "top", reach)
    else:
        raise ValueError(f"unknown seam side {side}")
    return source, neighbor


def _merge_half(original: MaskArray, seam: MaskArray, cut_edge: str, reach: int) -> MaskArray:
    """One tile half: the half-tile answer within ``reach`` of the cut, the union beyond it.

    ``cut_edge`` names the edge of this half that touches the cut.
    """
    merged = original | seam
    if reach <= 0:
        return merged
    if cut_edge == "right":
        merged[:, -reach:] = seam[:, -reach:]
    elif cut_edge == "left":
        merged[:, :reach] = seam[:, :reach]
    elif cut_edge == "bottom":
        merged[-reach:, :] = seam[-reach:, :]
    else:
        merged[:reach, :] = seam[:reach, :]
    return merged


def _cross_seam(
    tile: TileIndex,
    side: _Side,
    neighbor: TileIndex,
    pred: TilePrediction,
    foreground: Sequence[Point],
    background: Sequence[Point],
    predicted: dict[TileIndex, TilePrediction],
    expanded: set[Tuple[TileIndex, _Side]],
    seam_graph: SeamGraph,
    work: List[TileIndex],
    queued: set[TileIndex],
    predict: TilePredict,
    seam_predict: SeamPredict,
    tile_size: int,
    border_band: int,
    seed_spacing: int,
    should_stop: Optional[Callable[[], bool]],
) -> None:
    """Replace both seam halves from one predict centered on the cut.

    A contact range that overlaps one already stored on that undirected edge
    does not travel to the neighbor. The search is repeated when the contact
    is at least twice as long as that stored range, or when it meets two stored
    ranges and joins them. A disjoint range, the other arm of a C, is still crossed.
    """
    if should_stop is not None and should_stop():
        raise GrowthCancelled()
    novel = _novel_runs(
        _deep_contact_runs(pred, side, tile_size),
        seam_graph.ranges(tile, neighbor),
    )
    if not novel:
        return
    inset = _seam_inset(tile_size)
    along = [
        index
        for index in (_prompt_index(pred.mask, side, run, inset) for run in novel)
        if index is not None
    ]
    points, labels = _seam_prompts(
        tile,
        neighbor,
        side,
        foreground,
        background,
        tile_size,
        along,
    )
    if not any(label == 1 for label in labels):
        seam_graph.claim(tile, neighbor, novel)
        return
    synthetic, _logits, score = seam_predict(
        tile.row, tile.col, neighbor.row, neighbor.col, side.value, points, labels
    )
    synthetic_bool = np.asarray(synthetic, dtype=np.bool_)
    if synthetic_bool.shape[:2] != (tile_size, tile_size):
        synthetic_bool = cv2.resize(
            synthetic_bool.astype(np.uint8),
            (tile_size, tile_size),
            interpolation=cv2.INTER_NEAREST,
        ).astype(np.bool_)
    kept = _seam_component(synthetic_bool, points, labels)
    if kept is None:
        seam_graph.claim(tile, neighbor, novel)
        return
    existing = predicted.get(neighbor)
    source_mask, neighbor_mask = paste_half_mask(
        pred.mask,
        None if existing is None else existing.mask,
        kept,
        side.value,
        tile_size,
    )
    if existing is None:
        neighbor_mask, score = _fill_far_half(
            neighbor,
            neighbor_mask,
            side,
            foreground,
            background,
            predict,
            tile_size,
            seed_spacing,
            score,
        )
    grew_source = bool(np.any(source_mask != pred.mask))
    pred.mask = source_mask
    pred.logits = _zero_logit_half(pred.logits, side.value, tile_size)
    filled = _contact_runs(_contact_indices(pred, side, border_band))
    seam_graph.claim(tile, neighbor, [*novel, *filled])
    if grew_source:
        pred.score = min(pred.score, float(score))
        _drop_expanded(expanded, tile)
        expanded.add((tile, side))
        _enqueue(work, queued, tile)
    if existing is None:
        predicted[neighbor] = TilePrediction(
            row=neighbor.row,
            col=neighbor.col,
            mask=neighbor_mask,
            logits=None,
            score=float(score),
        )
        if np.any(neighbor_mask):
            _enqueue(work, queued, neighbor)
        return
    grew_neighbor = bool(np.any(neighbor_mask != existing.mask))
    existing.mask = neighbor_mask
    existing.logits = _zero_logit_half(existing.logits, _opposite(side).value, tile_size)
    if grew_neighbor:
        existing.score = min(existing.score, float(score))
        _drop_expanded(expanded, neighbor)
        expanded.add((neighbor, _opposite(side)))
        _enqueue(work, queued, neighbor)


def _fill_far_half(
    neighbor: TileIndex,
    neighbor_mask: MaskArray,
    side: _Side,
    foreground: Sequence[Point],
    background: Sequence[Point],
    predict: TilePredict,
    tile_size: int,
    seed_spacing: int,
    score: float,
) -> Tuple[MaskArray, float]:
    """Predict the neighbor's far half from interior points when the seam mask reaches the midline.

    The full-tile result is kept only on that far half. The seam half stays the
    half-tile mask, so a border-hugging full-tile mask cannot paint the cut back in.
    """
    seeds = _continuation_seeds(neighbor_mask, side.value, tile_size, seed_spacing)
    if not seeds:
        return neighbor_mask, score
    points, labels = _prompt_points(neighbor, foreground, background, tile_size, seeds)
    if not any(label == 1 for label in labels):
        return neighbor_mask, score
    try:
        full_mask, _logits, full_score = _call_predict(neighbor, predict, points, labels, tile_size)
    except PredictUnavailable:
        return neighbor_mask, score
    return _keep_far_half(full_mask, neighbor_mask, side.value, tile_size), min(float(score), float(full_score))


def _seam_prompts(
    tile: TileIndex,
    neighbor: TileIndex,
    side: _Side,
    foreground: Sequence[Point],
    background: Sequence[Point],
    tile_size: int,
    along: Sequence[int],
) -> Tuple[List[Point], List[int]]:
    """One point at each contact midpoint, plus clicks inside the half-tile.

    A point every 32 px along the cut made SAM2 treat the edge as the object and
    return a mask that filled most of the tile. ``along`` is only the runs that
    do not overlap a cut already crossed.
    """
    points: List[Point] = []
    labels: List[int] = []
    seen: set[Tuple[int, int, int]] = set()
    inset = _seam_inset(tile_size)

    def _add(local: Optional[Point], label: int) -> None:
        if local is None:
            return
        lx, ly = int(local[0]), int(local[1])
        if lx < 0 or ly < 0 or lx >= tile_size or ly >= tile_size:
            return
        key = (lx, ly, label)
        if key in seen:
            return
        seen.add(key)
        points.append((lx, ly))
        labels.append(label)

    for index in along:
        _add(_seam_prompt_xy(side, index, tile_size, inset), 1)
    _add_clicks(_add, tile, neighbor, side, foreground, 1, tile_size, inset)
    _add_clicks(_add, tile, neighbor, side, background, 0, tile_size, inset)
    return points, labels


def _add_clicks(
    add: Callable[[Optional[Point], int], None],
    tile: TileIndex,
    neighbor: TileIndex,
    side: _Side,
    clicks: Sequence[Point],
    label: int,
    tile_size: int,
    inset: int,
) -> None:
    for x, y in clicks:
        if point_in_tile(x, y, tile, tile_size):
            mapped = _map_local_to_synth(mosaic_to_image(x, y, tile, tile_size), "source", side, tile_size)
            if mapped is not None and _synth_interior(mapped, tile_size, inset):
                add(mapped, label)
        elif point_in_tile(x, y, neighbor, tile_size):
            mapped = _map_local_to_synth(
                mosaic_to_image(x, y, neighbor, tile_size), "neighbor", side, tile_size
            )
            if mapped is not None and _synth_interior(mapped, tile_size, inset):
                add(mapped, label)


def _synth_interior(point: Point, tile_size: int, inset: int) -> bool:
    """False for points on the half-tile's own border. Those are midlines of the real tiles."""
    x, y = int(point[0]), int(point[1])
    return inset <= x < tile_size - inset and inset <= y < tile_size - inset


def _seam_inset(tile_size: int) -> int:
    half = max(1, tile_size // 2)
    room = max(1, half // 4)
    return max(1, min(SEAM_PROMPT_INSET_PX, room, half - 1))


def _seam_prompt_xy(side: _Side, index: int, tile_size: int, inset: int) -> Point:
    """Synthetic-image point inset into the source half, off the cut."""
    half = tile_size // 2
    inset = max(0, min(inset, half - 1))
    index = int(np.clip(index, 0, tile_size - 1))
    if side is _Side.RIGHT:
        return (half - 1 - inset, index)
    if side is _Side.LEFT:
        return (half + inset, index)
    if side is _Side.TOP:
        return (index, half + inset)
    return (index, half - 1 - inset)


def _map_local_to_synth(local: Point, role: str, side: _Side, tile_size: int) -> Optional[Point]:
    """Map a tile-local pixel into the half-tile, or None when it lies outside that window."""
    x, y = int(local[0]), int(local[1])
    half = tile_size // 2
    if side is _Side.RIGHT:
        if role == "source" and x >= half:
            return (x - half, y)
        if role == "neighbor" and x < half:
            return (x + half, y)
    elif side is _Side.LEFT:
        if role == "neighbor" and x >= half:
            return (x - half, y)
        if role == "source" and x < half:
            return (x + half, y)
    elif side is _Side.TOP:
        if role == "neighbor" and y >= half:
            return (x, y - half)
        if role == "source" and y < half:
            return (x, y + half)
    elif role == "source" and y >= half:
        return (x, y - half)
    elif role == "neighbor" and y < half:
        return (x, y + half)
    return None


def _continuation_seeds(mask: MaskArray, side: str, tile_size: int, spacing: int) -> List[Point]:
    """Points inside the seam mask, ``inset`` pixels before the tile midline, for the far-half predict.

    The half-tile window ends at the neighbor's midline, so a mask that reaches it is
    cut straight there. The full-tile predict that continues it needs positive points
    the object certainly contains. A point placed past the midline is a guess: where the
    boundary slants or curves it lands on the background, the predict returns nothing
    on the far half, and the cut stays as a straight line. A row qualifies only when the
    mask is set from the midline back to the seed.
    """
    half = tile_size // 2
    inset = _seam_inset(tile_size)
    if side == "right":
        deep = np.all(mask[:, half - 1 - inset: half], axis=1)
        hit = [int(index) for index in np.flatnonzero(deep)]
        return [(half - 1 - inset, y) for y in _sample_runs(hit, spacing)]
    if side == "left":
        deep = np.all(mask[:, half: half + inset + 1], axis=1)
        hit = [int(index) for index in np.flatnonzero(deep)]
        return [(half + inset, y) for y in _sample_runs(hit, spacing)]
    if side == "top":
        deep = np.all(mask[half: half + inset + 1, :], axis=0)
        hit = [int(index) for index in np.flatnonzero(deep)]
        return [(x, half + inset) for x in _sample_runs(hit, spacing)]
    deep = np.all(mask[half - 1 - inset: half, :], axis=0)
    hit = [int(index) for index in np.flatnonzero(deep)]
    return [(x, half - 1 - inset) for x in _sample_runs(hit, spacing)]


def _keep_far_half(full_mask: MaskArray, seam_mask: MaskArray, side: str, tile_size: int) -> MaskArray:
    half = tile_size // 2
    combined = np.array(seam_mask, dtype=np.bool_, copy=True)
    full = np.asarray(full_mask, dtype=np.bool_)
    if side == "right":
        combined[:, half:] = full[:, half:]
    elif side == "left":
        combined[:, :half] = full[:, :half]
    elif side == "top":
        combined[:half, :] = full[:half, :]
    else:
        combined[half:, :] = full[half:, :]
    return combined


def _zero_logit_half(
    logits: Optional[LogitsArray],
    side: str,
    tile_size: int,
) -> Optional[LogitsArray]:
    """Drop logits on the replaced half so a stale edge score cannot walk the cut."""
    if logits is None:
        return None
    cleared = np.array(logits, dtype=np.float32, copy=True)
    if cleared.shape[0] < tile_size or cleared.shape[1] < tile_size:
        return None
    half = tile_size // 2
    if side == "right":
        cleared[:, half:] = 0
    elif side == "left":
        cleared[:, :half] = 0
    elif side == "top":
        cleared[:half, :] = 0
    else:
        cleared[half:, :] = 0
    return cleared


def _neighbor(tile: TileIndex, side: _Side) -> TileIndex:
    if side is _Side.RIGHT:
        return TileIndex(tile.row, tile.col + 1)
    if side is _Side.LEFT:
        return TileIndex(tile.row, tile.col - 1)
    if side is _Side.TOP:
        return TileIndex(tile.row + 1, tile.col)
    return TileIndex(tile.row - 1, tile.col)


def _border_contact(pred: TilePrediction, side: _Side, tile_size: int, band: int) -> bool:
    """True when the binary mask occupies the outermost pixels of this side.

    Logits are ignored. A bilinear upsample of the 256px SAM map is positive
    past the mask, and that used to start a half-tile for a circle that never
    reached the cut.
    """
    mask = pred.mask
    if mask.size == 0:
        return False
    height, width = mask.shape
    band = max(1, min(band, height, width))
    region = _edge_slice(side, height, width, band)
    return bool(np.any(mask[region]))


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
    """Foreground seeds on the neighbor's pixel that touches the shared edge."""
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
    """Indexes along the side where the binary mask touches the outer band.

    The index is an image row for a vertical side and an image column for a
    horizontal side. Logits do not count.
    """
    mask = pred.mask
    height, width = mask.shape
    indices: List[int] = []
    if side in (_Side.LEFT, _Side.RIGHT):
        cols = range(band) if side is _Side.LEFT else range(width - band, width)
        for image_y in range(height):
            if any(bool(mask[image_y, col]) for col in cols):
                indices.append(image_y)
        return indices
    rows = range(band) if side is _Side.TOP else range(height - band, height)
    for image_x in range(width):
        if any(bool(mask[row, image_x]) for row in rows):
            indices.append(image_x)
    return indices


def _inset_in_mask(mask: MaskArray, side: _Side, index: int, inset: int) -> bool:
    """True when the half-tile prompt pixel lies inside the source mask.

    The prompt sits ``inset`` pixels in from the cut. A circle that only touches
    the outermost pixel does not contain that point, so the crossing is skipped
    instead of asking SAM about the tissue outside the circle.
    """
    height, width = mask.shape[:2]
    index = int(index)
    if side in (_Side.LEFT, _Side.RIGHT):
        if index < 0 or index >= height:
            return False
        x = inset if side is _Side.LEFT else width - 1 - inset
        return 0 <= x < width and bool(mask[index, x])
    if index < 0 or index >= width:
        return False
    y = inset if side is _Side.TOP else height - 1 - inset
    return 0 <= y < height and bool(mask[y, index])


def _prompt_index(mask: MaskArray, side: _Side, run: PixelRange, inset: int) -> Optional[int]:
    """Index along the edge, inside ``run``, whose inset prompt pixel is in the mask and nearest the run's middle.

    A hole or dent exactly at the middle of a long contact must not cancel the crossing,
    so any qualifying pixel of the run will do. None when no pixel of the run qualifies.
    """
    start, end = sorted((int(run[0]), int(run[1])))
    middle = (start + end) // 2
    height, width = mask.shape[:2]
    if side in (_Side.LEFT, _Side.RIGHT):
        x = inset if side is _Side.LEFT else width - 1 - inset
        if not 0 <= x < width:
            return None
        start, end = max(0, start), min(height - 1, end)
        column = mask[start: end + 1, x]
    else:
        y = inset if side is _Side.TOP else height - 1 - inset
        if not 0 <= y < height:
            return None
        start, end = max(0, start), min(width - 1, end)
        column = mask[y, start: end + 1]
    hits = np.flatnonzero(column)
    if hits.size == 0:
        return None
    indices = hits + start
    return int(indices[np.argmin(np.abs(indices - middle))])


def _deep_contact_runs(pred: TilePrediction, side: _Side, tile_size: int) -> List[Tuple[int, int]]:
    """Edge runs that still have a prompt pixel inside the mask, ``inset`` pixels in from the edge.

    Contact is the outermost pixel only. A run that reaches that pixel but has no
    inset pixel inside the mask is not a crossing.
    """
    inset = _seam_inset(tile_size)
    runs = _contact_runs(_contact_indices(pred, side, 1))
    return [run for run in runs if _prompt_index(pred.mask, side, run, inset) is not None]


def _seam_component(
    mask: MaskArray,
    points: Sequence[Point],
    labels: Sequence[int],
) -> Optional[MaskArray]:
    """Connected component that contains a foreground prompt, or None.

    A field-sized mask is kept when it covers the prompt. Pasting it fills the
    contact range, and any later touch on that same stretch overlaps, so the
    walk does not search the original tile again. A miss on the prompt is not
    pasted. Each disjoint stretch is still crossed on its own.
    """
    height, width = mask.shape[:2]
    if height == 0 or width == 0:
        return None
    prompts = [
        (int(point[0]), int(point[1]))
        for point, label in zip(points, labels)
        if int(label) == 1
    ]
    binary = np.asarray(mask, dtype=np.uint8)
    if binary.ndim != 2:
        return None
    _count, labeled = cv2.connectedComponents(binary, connectivity=8)
    kept_ids: set[int] = set()
    for x, y in prompts:
        if x < 0 or y < 0 or x >= width or y >= height:
            continue
        label_id = int(labeled[y, x])
        if label_id > 0:
            kept_ids.add(label_id)
    if not kept_ids:
        return None
    return np.isin(labeled, list(kept_ids))


def _contact_runs(indices: Sequence[int]) -> List[Tuple[int, int]]:
    """Inclusive start and end of each contiguous stretch along an edge."""
    if not indices:
        return []
    ordered = sorted(set(indices))
    runs: List[Tuple[int, int]] = []
    run_start = ordered[0]
    previous = ordered[0]
    for index in ordered[1:]:
        if index - previous > 1:
            runs.append((run_start, previous))
            run_start = index
        previous = index
    runs.append((run_start, previous))
    return runs


def _inclusive_length(run: PixelRange) -> int:
    """Pixel count of an inclusive border range."""
    start, end = int(run[0]), int(run[1])
    if start > end:
        start, end = end, start
    return end - start + 1


def _repeats_search(run: PixelRange, already: Sequence[PixelRange]) -> bool:
    """True when an overlapping contact is a new search rather than a cycle.

    Overlap with a previous search cancels travel. The search is repeated when
    ``run`` is at least twice as long as the single stored range it overlaps, or
    when it overlaps two or more stored ranges and so joins them into one cut.
    """
    overlapped = [prior for prior in already if _ranges_overlap(run, prior)]
    if len(overlapped) >= 2:
        return True
    if len(overlapped) == 1:
        return _inclusive_length(run) >= 2 * _inclusive_length(overlapped[0])
    return False


def _novel_runs(
    runs: Sequence[Tuple[int, int]],
    already: Sequence[Tuple[int, int]],
) -> List[Tuple[int, int]]:
    """Runs that still travel to the neighbor.

    A run that misses every stored range is a new stretch and is crossed once.
    A run that overlaps a previous search stays home, unless
    ``_repeats_search`` says that range doubled or merged two stored ranges.
    """
    return [
        run
        for run in runs
        if not any(_ranges_overlap(run, prior) for prior in already) or _repeats_search(run, already)
    ]


def _ranges_overlap(left: Tuple[int, int], right: Tuple[int, int]) -> bool:
    return left[0] <= right[1] and right[0] <= left[1]


def _edge_key(tile: TileIndex, neighbor: TileIndex) -> EdgeKey:
    """One key for the cut, independent of which cell the walk arrived from."""
    left = (int(tile.row), int(tile.col))
    right = (int(neighbor.row), int(neighbor.col))
    if right < left:
        left, right = right, left
    return left[0], left[1], right[0], right[1]


def _merge_ranges(runs: Sequence[PixelRange]) -> List[PixelRange]:
    """Sort inclusive ranges and collapse overlaps and ranges that touch."""
    normalized = []
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
        prev_start, prev_end = merged[-1]
        if start <= prev_end + 1:
            merged[-1] = (prev_start, max(prev_end, end))
        else:
            merged.append((start, end))
    return merged


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
