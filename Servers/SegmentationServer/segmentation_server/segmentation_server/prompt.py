"""The prompt of one SegmentTilesStream as the growth walk reads it: the object as islands, and the background.

The foreground is every shape of ``foreground_feature`` and the FOREGROUND bitmap (painted mask tiles). Each is a
:class:`ForegroundPart`. A geometry shape is *softened* (the model's trust profile for an edge that is not reliable): its
``region`` is the pixels the prompt encoding gives a positive value, which for a circle is its disc at
``radius * circle_diameter_scale``. The bitmap is used as drawn.

The parts together are one object, but an object prompt that falls apart into separate pieces is several objects: each
**island** (:class:`Island`) is segmented on its own, with its own growth walk, and returns its own result. For the
edge-confidence encodings up to v6 (and the legacy flat one) an island is a connected component, 8-connected, of the
union of the parts' regions (:func:`build_islands`); the v7 encoding has its own rule (``depth_trust``). Islands are
numbered 1, 2, ... by decreasing trusted area, here the number of prompt pixels.

The background is what the object is not: the shapes of ``background_feature`` and the BACKGROUND bitmap
(:class:`BackgroundPrompt`). The client sends the shapes that reach the request's tiles with ``start``; when the walk
grows into a cell whose neighbours the server has not been told about, it asks for them (``TilesNeeded.background_cells``)
and waits before it predicts the window there.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Callable, Dict, Iterable, List, Optional, Sequence, Set, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_server.cell_grid import TILE_SIZE, Cell, MaskShape, Point, TileIndex, tiles_for_cell
from segmentation_server.mask_tiles import TiledMaskShape, TileKey
from segmentation_server.shapes import CircleGeometry, Geometry, ShapeRegion, scaled_circle
from segmentation_server import trainer_prompt_encoding as enc

logger = logging.getLogger(__name__)

DEFAULT_MAX_ISLANDS = 16
"""Most islands one request is segmented as. The largest are kept; a bitmap with stray specks would otherwise run a
growth walk per speck."""


class PromptTooLarge(ValueError):
    """The foreground prompt covers more tiles than the server's mask-tile limit allows."""

    def __init__(self, tiles: int, limit: int) -> None:
        super().__init__(f"The foreground prompt covers {tiles} tiles; the server accepts at most {limit}.")
        self.tiles = tiles
        self.limit = limit


def kind_of(geometry: Optional[Geometry]) -> str:
    """``"circle"``, ``"ellipse"``, ``"line"``, ``"polygon"``, or ``"bitmap"`` for None."""
    if geometry is None:
        return "bitmap"
    return {
        "CircleGeometry": "circle",
        "EllipseGeometry": "ellipse",
        "StrokeGeometry": "line",
        "AreaGeometry": "polygon",
    }[type(geometry).__name__]


@dataclass
class ForegroundPart:
    """One piece of the object prompt.

    ``geometry`` is the shape as the client sent it (a circle's TRUE radius), None for the bitmap. ``region`` is the
    pixels the prompt covers for it: a softened circle at its scaled radius, any other shape as drawn, the bitmap's own
    pixels.
    """

    region: MaskShape
    geometry: Optional[Geometry] = None

    @property
    def kind(self) -> str:
        return kind_of(self.geometry)

    @property
    def softened(self) -> bool:
        """True for a geometry shape (its edge is not trusted), False for the bitmap (used as drawn)."""
        return self.geometry is not None


def foreground_parts(
    shapes: Sequence[Geometry], bitmap: Optional[TiledMaskShape], circle_scale: float
) -> List[ForegroundPart]:
    """The object prompt as parts: each shape, softened (a circle's radius times ``circle_scale``), then the bitmap."""
    parts: List[ForegroundPart] = []
    for geometry in shapes:
        softened = scaled_circle(geometry, circle_scale) if isinstance(geometry, CircleGeometry) else geometry
        region = ShapeRegion(softened)
        if not region.empty:
            parts.append(ForegroundPart(region=region, geometry=geometry))
    if bitmap is not None and not bitmap.empty:
        parts.append(ForegroundPart(region=bitmap))
    return parts


class Island(TiledMaskShape):
    """One island of the object prompt: its pixels (a complete :class:`TiledMaskShape`), the parts it holds, its number
    and trusted area.

    ``trust`` is set by encodings that weigh pixels by depth (v7): ``(x0, y0, values_up)``, the island's
    ``SEED_LOGIT``-free trust ``w`` in [0, 1] over the mosaic rectangle starting at ``(x0, y0)``, Y up, 0 outside the
    island and for bitmap pixels.
    """

    def __init__(self, number: int, area: float, parts: Sequence[ForegroundPart]) -> None:
        super().__init__(role="seed", complete=True)
        object.__setattr__(self, "number", int(number))
        object.__setattr__(self, "area", float(area))
        object.__setattr__(self, "parts", list(parts))
        object.__setattr__(self, "trust", None)

    def contains(self, x: int, y: int) -> bool:
        """True when mosaic pixel ``(x, y)`` is in the island."""
        return bool(self.region(x, y, x, y)[0, 0])

    def renumber(self, number: int) -> None:
        object.__setattr__(self, "number", int(number))

    def set_trust(self, x0: int, y0: int, values_up: NDArray[np.float32]) -> None:
        object.__setattr__(self, "trust", (int(x0), int(y0), values_up))

    def trust_window(self, cell: Cell) -> Optional[NDArray[np.float32]]:
        """The island's trust over ``cell``'s window, ``CELL_SIZE`` square, Y-down; None without a trust map."""
        if self.trust is None:
            return None
        from segmentation_server.cell_grid import CELL_SIZE, window_origin

        x0, y0, values = self.trust
        wx0, wy0 = window_origin(cell)
        out = np.zeros((CELL_SIZE, CELL_SIZE), dtype=np.float32)
        xa, xb = max(x0, wx0), min(x0 + values.shape[1], wx0 + CELL_SIZE)
        ya, yb = max(y0, wy0), min(y0 + values.shape[0], wy0 + CELL_SIZE)
        if xa >= xb or ya >= yb:
            return out
        # Window row 0 is the highest mosaic y of the window.
        out[wy0 + CELL_SIZE - yb:wy0 + CELL_SIZE - ya, xa - wx0:xb - wx0] = np.flipud(
            values[ya - y0:yb - y0, xa - x0:xb - x0]
        )
        return out


def _part_tiles(part: ForegroundPart) -> Iterable[Tuple[TileKey, NDArray[np.bool_]]]:
    """The part's pixels tile by tile: ``((row, col), Y-up bool square)``, only tiles it covers."""
    region = part.region
    if isinstance(region, TiledMaskShape) and not isinstance(region, Island):
        for key, pixels in region.tiles():
            yield key, (np.ones((TILE_SIZE, TILE_SIZE), dtype=np.bool_) if pixels is True else pixels)
        return
    x0, y0, x1, y1 = region.box
    for row in range(y0 // TILE_SIZE, y1 // TILE_SIZE + 1):
        for col in range(x0 // TILE_SIZE, x1 // TILE_SIZE + 1):
            tx, ty = col * TILE_SIZE, row * TILE_SIZE
            pixels = region.region(tx, ty, tx + TILE_SIZE - 1, ty + TILE_SIZE - 1)
            if pixels.any():
                yield (row, col), pixels


def _count_tiles(parts: Sequence[ForegroundPart]) -> int:
    keys: Set[TileKey] = set()
    for part in parts:
        region = part.region
        if isinstance(region, TiledMaskShape):
            keys.update(key for key, _pixels in region.tiles())
            continue
        x0, y0, x1, y1 = region.box
        keys.update(
            (row, col)
            for row in range(y0 // TILE_SIZE, y1 // TILE_SIZE + 1)
            for col in range(x0 // TILE_SIZE, x1 // TILE_SIZE + 1)
        )
    return len(keys)


class _UnionFind:
    def __init__(self, size: int) -> None:
        self.parent = list(range(size))

    def find(self, item: int) -> int:
        parent = self.parent
        while parent[item] != item:
            parent[item] = parent[parent[item]]
            item = parent[item]
        return item

    def union(self, a: int, b: int) -> None:
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.parent[max(ra, rb)] = min(ra, rb)


def _join_pairs(sets: _UnionFind, left: NDArray, right: NDArray) -> None:
    """Union every label pair where both sides are set (the two arrays are pixels that touch)."""
    both = (left > 0) & (right > 0)
    if not both.any():
        return
    for a, b in set(zip(left[both].tolist(), right[both].tolist())):
        sets.union(a, b)


def label_tiles(union: Dict[TileKey, NDArray[np.bool_]]) -> Tuple[Dict[TileKey, NDArray[np.int32]], NDArray[np.int64]]:
    """8-connected components of a mask stored as 1024 tiles, joined across tile edges and corners.

    Returns each tile's labels (0 for no mask) and ``roots``, which maps a label to its component's id (the smallest
    label in it), so ``roots[labels[key]]`` is the component of every pixel.
    """
    labels: Dict[TileKey, NDArray[np.int32]] = {}
    offset = 0
    for key in sorted(union):
        count, local = cv2.connectedComponents(union[key].astype(np.uint8), connectivity=8)
        local = local.astype(np.int32)
        local[local > 0] += offset
        labels[key] = local
        offset += count - 1
    sets = _UnionFind(offset + 1)
    for (row, col), here in labels.items():
        right = labels.get((row, col + 1))
        if right is not None:
            # Y-up tiles: column -1 of this tile touches column 0 of the next, one row up, level or down.
            a, b = here[:, -1], right[:, 0]
            _join_pairs(sets, a, b)
            _join_pairs(sets, a[:-1], b[1:])
            _join_pairs(sets, a[1:], b[:-1])
        above = labels.get((row + 1, col))
        if above is not None:
            a, b = here[-1, :], above[0, :]
            _join_pairs(sets, a, b)
            _join_pairs(sets, a[:-1], b[1:])
            _join_pairs(sets, a[1:], b[:-1])
        up_right = labels.get((row + 1, col + 1))
        if up_right is not None:
            _join_pairs(sets, here[-1:, -1], up_right[:1, 0])
        up_left = labels.get((row + 1, col - 1))
        if up_left is not None:
            _join_pairs(sets, here[-1:, 0], up_left[:1, -1])
    roots = np.asarray([sets.find(label) for label in range(offset + 1)], dtype=np.int64)
    return labels, roots


def build_islands(
    parts: Sequence[ForegroundPart],
    max_tiles: int,
    max_islands: int = DEFAULT_MAX_ISLANDS,
) -> List[Island]:
    """The islands of the object prompt: 8-connected components of the union of the parts' regions.

    Numbered 1, 2, ... by decreasing area (pixels), ties by position. Past ``max_islands`` the smallest are dropped,
    with a warning.

    Raises:
        PromptTooLarge: The parts cover more than ``max_tiles`` tiles.
    """
    if not parts:
        return []
    tile_count = _count_tiles(parts)
    if tile_count > max_tiles:
        raise PromptTooLarge(tile_count, max_tiles)
    union: Dict[TileKey, NDArray[np.bool_]] = {}
    for part in parts:
        for key, pixels in _part_tiles(part):
            held = union.get(key)
            union[key] = pixels.copy() if held is None else (held | pixels)
    if not union:
        return []
    labels, roots = label_tiles(union)
    area = np.zeros(len(roots), dtype=np.int64)
    for local in labels.values():
        area += np.bincount(roots[local].ravel(), minlength=len(roots))
    area[0] = 0
    components = [int(root) for root in np.flatnonzero(area)]
    components.sort(key=lambda root: (-int(area[root]), root))
    if len(components) > max_islands:
        logger.warning(
            "The object prompt has %d islands; segmenting the %d largest (the smallest dropped has %d pixels)",
            len(components), max_islands, int(area[components[max_islands]]),
        )
        components = components[:max_islands]
    # Which components each part reaches, so an island knows the parts it is made of.
    reaches: List[Set[int]] = []
    for part in parts:
        touched: Set[int] = set()
        for key, pixels in _part_tiles(part):
            touched.update(np.unique(roots[labels[key][pixels]]).tolist())
        reaches.append(touched)
    islands: List[Island] = []
    for number, root in enumerate(components, start=1):
        island = Island(number, float(area[root]), [part for part, touched in zip(parts, reaches) if root in touched])
        for key, local in labels.items():
            pixels = roots[local] == root
            if pixels.any():
                island.settle(key, pixels)
        islands.append(island)
    return islands


def assign_clicks(islands: Sequence[Island], clicks: Sequence[Point]) -> List[List[Point]]:
    """The foreground clicks of each island, in island order: a click goes to the island it lies in, else island 1."""
    assigned: List[List[Point]] = [[] for _ in islands]
    if not islands:
        return assigned
    for click in clicks:
        for index, island in enumerate(islands):
            if island.contains(click[0], click[1]):
                assigned[index].append(click)
                break
        else:
            assigned[0].append(click)
    return assigned


class BackgroundShape(ShapeRegion):
    """A shape of ``background_feature``. Its region is what it claims as not object: a polygon, line or ellipse as
    drawn; a circle out to ``zero_frac`` of its TRUE radius, where its radius trust falls to nothing. ``circle`` keeps
    the true circle for the radius-trust encoding."""

    def __init__(self, geometry: Geometry, trust: enc.CircleAnnotationTrust = enc.DEFAULT_CIRCLE_TRUST) -> None:
        circle = geometry if isinstance(geometry, CircleGeometry) else None
        super().__init__(scaled_circle(circle, trust.zero_frac) if circle is not None else geometry, role="background")
        object.__setattr__(self, "circle", circle)


class BackgroundPrompt:
    """What the object is not: the BACKGROUND bitmap and the background shapes, and which tiles they are known for.

    ``regions`` is a live list (bitmap first, then shapes in arrival order) the growth walk reads as its background
    masks. A tile is *covered* when the client has been asked for the shapes that reach it: the request's ``tiles`` up
    front, then every cell the server asked for with ``TilesNeeded.background_cells`` once that ``TilesNeeded`` is
    answered. The bitmap is complete from the start, so only shapes are asked for.
    """

    def __init__(
        self,
        covered: Iterable[TileKey] = (),
        bitmap: Optional[TiledMaskShape] = None,
        trust: enc.CircleAnnotationTrust = enc.DEFAULT_CIRCLE_TRUST,
    ) -> None:
        self._trust = trust
        self._covered: Set[TileKey] = set(covered)
        self._asked: Set[TileKey] = set()
        self.regions: List[MaskShape] = []
        if bitmap is not None and not bitmap.empty:
            self.regions.append(bitmap)
        self.shape_count = 0

    def add_shapes(self, geometries: Iterable[Geometry]) -> int:
        """Add background shapes; returns how many were added (a shape covering no pixel is skipped)."""
        added = 0
        for geometry in geometries:
            region = BackgroundShape(geometry, self._trust)
            if region.empty:
                continue
            self.regions.append(region)
            added += 1
        self.shape_count += added
        return added

    def is_covered(self, key: TileKey) -> bool:
        return key in self._covered

    def ask(self, keys: Iterable[TileKey]) -> List[TileKey]:
        """Note that the client is being asked for these cells' shapes. Returns the ones not asked before."""
        fresh = [key for key in keys if key not in self._covered and key not in self._asked]
        self._asked.update(fresh)
        return fresh

    def cover(self, keys: Iterable[TileKey]) -> None:
        """The client has answered for these cells: every shape that reaches them has been sent."""
        for key in keys:
            self._covered.add(key)
            self._asked.discard(key)

    def unknown_tiles(self, cell: Cell) -> List[TileIndex]:
        """The tiles of ``cell``'s window whose background shapes the server has not been told about yet."""
        return [tile for tile in tiles_for_cell(cell) if (tile.row, tile.col) not in self._covered]


CoverageCheck = Callable[[Cell], List[TileIndex]]
