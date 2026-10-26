"""Depth-relative trust of softened seed shapes and the islands it makes (the edge-confidence-v7 prompt encoding).

Definitions are the trainer's (Sam2Trainer ``docs/depth_relative_trust.md``); section numbers below refer to it.

* Trust profile (section 2): ``w(rho)`` is 1 for ``rho <= a``, a half cosine down to 0 at ``rho = b``, 0 beyond, with
  ``a = full_frac`` and ``b = zero_frac`` of the model's ``circle_annotation_trust``.
* A circle uses ``rho = |p - c| / R`` with its TRUE radius (the depth-relative trust of a disc is its radius-relative
  trust), computed analytically by ``trainer_prompt_encoding.circle_trust``.
* Any other shape uses the local depth coordinate (section 4): ``rho = 1 - d(p) / D_l(p)``, ``d`` the distance from the
  shape's edge and ``D_l`` the local thickness radius, the radius of the largest inscribed disc that contains ``p``
  (Hildebrand and Ruegsegger 1997), found by painting each disc centre's radius into its disc and keeping the maximum.
* Islands (section 5): the trusted core ``K = {rho <= a}`` split into 8-connected components (components smaller than
  ``min_core_px`` are dropped with their zone); each pixel of the falling zone ``{a < rho < b}`` joins the core nearest
  to it along paths inside the prompt.

A painted bitmap is not softened: its pixels are all core, and the encoder gives them the v6 edge confidence.
"""

from __future__ import annotations

import math
from typing import List, Sequence, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray
from scipy import ndimage

from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import TILE_SIZE
from segmentation_server.prompt import ForegroundPart, Island, PromptTooLarge, _count_tiles, _part_tiles
from segmentation_server.shapes import CircleGeometry, rasterize

DEFAULT_MIN_CORE_PX = 16
"""A trusted core smaller than this many pixels is outline noise or a spur, not an island (``A_min``)."""

DEFAULT_MAX_DENSE_PIXELS = 64 * 1024 * 1024
"""Largest box, in pixels, the v7 islands are worked out over in one array."""


def trust_weight(rho: NDArray, trust: enc.CircleAnnotationTrust) -> NDArray[np.float32]:
    """``w(rho)``: 1 inside ``full_frac``, a half cosine down to 0 at ``zero_frac``, 0 beyond."""
    a, b = float(trust.full_frac), float(trust.zero_frac)
    rho = np.asarray(rho, dtype=np.float32)
    phase = (rho - np.float32(a)) * np.float32(math.pi / (b - a))
    taper = np.float32(0.5) * (np.float32(1.0) + np.cos(np.clip(phase, 0.0, math.pi)))
    return np.where(rho <= a, np.float32(1.0), np.where(rho < b, taper, np.float32(0.0))).astype(np.float32)


def edge_depth(mask: NDArray[np.bool_]) -> NDArray[np.float32]:
    """``d``: distance from each pixel of ``mask`` to its edge (pixel centre to the pixel boundary), 0 outside.

    The same measure as the v6 edge confidence (``trainer_prompt_encoding.edge_distance``), but the array border is an
    edge: the caller pads the shape's own box, so a shape is never cut by it.
    """
    mask = np.asarray(mask, dtype=bool)
    if not mask.any():
        return np.zeros(mask.shape, dtype=np.float32)
    padded = np.pad(mask, 1)
    depth = np.maximum(ndimage.distance_transform_edt(padded) - 0.5, 0.0)[1:-1, 1:-1]
    return (depth * mask).astype(np.float32)


def local_thickness(depth: NDArray[np.float32]) -> NDArray[np.float32]:
    """``D_l``: for each pixel, the radius of the largest inscribed disc ``B(c, d(c))`` that contains it (0 outside).

    Painting from every centre gives the answer, and so does painting from any set of centres that contains the medial
    axis. A centre whose disc lies inside a neighbour's (``d(n) >= d(c) + |n - c|``) adds nothing, so only the rest are
    painted: the ridge of the distance map.
    """
    height, width = depth.shape
    out = np.zeros_like(depth, dtype=np.float32)
    keep = depth > 0
    if not keep.any():
        return out
    padded = np.pad(depth, 1)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            step = math.sqrt(2.0) if dy and dx else 1.0
            neighbour = padded[1 + dy:1 + dy + height, 1 + dx:1 + dx + width]
            keep &= ~(neighbour >= depth + np.float32(step) - np.float32(1e-6))
    ys, xs = np.nonzero(keep)
    radii = depth[ys, xs]
    for y, x, r in zip(ys.tolist(), xs.tolist(), radii.tolist()):
        reach = int(math.floor(r))
        y0, y1 = max(0, y - reach), min(height, y + reach + 1)
        x0, x1 = max(0, x - reach), min(width, x + reach + 1)
        oy = np.arange(y0, y1, dtype=np.float32)[:, None] - y
        ox = np.arange(x0, x1, dtype=np.float32)[None, :] - x
        inside = (oy * oy + ox * ox) <= np.float32(r * r)
        block = out[y0:y1, x0:x1]
        np.maximum(block, np.where(inside, np.float32(r), np.float32(0.0)), out=block)
    return out


def shape_trust(
    part: ForegroundPart, x0: int, y0: int, width: int, height: int, trust: enc.CircleAnnotationTrust
) -> Tuple[NDArray[np.float32], NDArray[np.bool_]]:
    """A softened shape's trust ``w`` and its core ``{rho <= a}`` over the mosaic rectangle at ``(x0, y0)``, Y up."""
    geometry = part.geometry
    if isinstance(geometry, CircleGeometry):
        # circle_trust works in a Y-down image with pixel (x, y) centred at (x + 0.5, y + 0.5). A Y-up array is the same
        # rectangle with rows counted upward, so the centre's row coordinate is measured from y0 the same way.
        claimed, weight = enc.circle_trust(
            (height, width), geometry.cx - x0, geometry.cy - y0, geometry.radius, trust
        )
        dx = np.arange(width, dtype=np.float32)[None, :] + np.float32(0.5) - np.float32(geometry.cx - x0)
        dy = np.arange(height, dtype=np.float32)[:, None] + np.float32(0.5) - np.float32(geometry.cy - y0)
        core = (dx * dx + dy * dy) <= np.float32(trust.full_frac * geometry.radius) ** 2
        return weight * claimed, core & claimed
    mask = rasterize(geometry, x0, y0, width, height)
    depth = edge_depth(mask)
    thickness = local_thickness(depth)
    rho = np.ones(mask.shape, dtype=np.float32)
    inside = mask & (thickness > 0)
    rho[inside] = np.float32(1.0) - depth[inside] / thickness[inside]
    weight = trust_weight(rho, trust) * mask
    return weight.astype(np.float32), inside & (rho <= np.float32(trust.full_frac))


def _grow_into(labels: NDArray[np.int32], support: NDArray[np.bool_]) -> NDArray[np.int32]:
    """Give each unlabelled ``support`` pixel the label of the nearest labelled pixel along 8-connected paths inside
    ``support`` (a breadth-first front, one pixel per step; a tie takes the larger label)."""
    labels = labels.copy()
    kernel = np.ones((3, 3), dtype=np.uint8)
    while True:
        free = support & (labels == 0)
        if not free.any():
            return labels
        grown = cv2.dilate(labels.astype(np.float32), kernel).astype(np.int32)
        take = free & (grown > 0)
        if not take.any():
            return labels
        labels[take] = grown[take]


def build_trust_islands(
    parts: Sequence[ForegroundPart],
    trust: enc.CircleAnnotationTrust,
    max_tiles: int,
    max_islands: int,
    min_core_px: int = DEFAULT_MIN_CORE_PX,
    max_dense_pixels: int = DEFAULT_MAX_DENSE_PIXELS,
) -> List[Island]:
    """The v7 islands of the object prompt (section 5), numbered by decreasing core area, each with its trust map.

    Raises:
        PromptTooLarge: The parts cover more than ``max_tiles`` tiles, or their box is larger than ``max_dense_pixels``.
    """
    if not parts:
        return []
    tile_count = _count_tiles(parts)
    if tile_count > max_tiles:
        raise PromptTooLarge(tile_count, max_tiles)
    boxes = [part.region.box for part in parts]
    # One pixel of margin, so a shape's edge is never the array's border.
    x0 = min(box[0] for box in boxes) - 1
    y0 = min(box[1] for box in boxes) - 1
    x1 = max(box[2] for box in boxes) + 1
    y1 = max(box[3] for box in boxes) + 1
    width, height = x1 - x0 + 1, y1 - y0 + 1
    if width * height > max_dense_pixels:
        raise PromptTooLarge(tile_count, max_tiles)

    weight = np.zeros((height, width), dtype=np.float32)
    core = np.zeros((height, width), dtype=np.bool_)
    support = np.zeros((height, width), dtype=np.bool_)
    part_support: List[NDArray[np.bool_]] = []
    for part in parts:
        if part.softened:
            w, part_core = shape_trust(part, x0, y0, width, height, trust)
            np.maximum(weight, w, out=weight)
            core |= part_core
            mine = w > 0
        else:
            mine = np.zeros((height, width), dtype=np.bool_)
            for (row, col), pixels in _part_tiles(part):
                tx, ty = col * TILE_SIZE, row * TILE_SIZE
                xa, xb = max(tx, x0), min(tx + TILE_SIZE, x0 + width)
                ya, yb = max(ty, y0), min(ty + TILE_SIZE, y0 + height)
                if xa < xb and ya < yb:
                    mine[ya - y0:yb - y0, xa - x0:xb - x0] |= pixels[ya - ty:yb - ty, xa - tx:xb - tx]
            core |= mine
        support |= mine
        part_support.append(mine)

    count, labels, stats, _ = cv2.connectedComponentsWithStats(core.astype(np.uint8), connectivity=8)
    kept = [label for label in range(1, count) if stats[label, cv2.CC_STAT_AREA] >= min_core_px]
    kept.sort(key=lambda label: (-int(stats[label, cv2.CC_STAT_AREA]), label))
    kept = kept[:max_islands]
    remap = np.zeros(count, dtype=np.int32)
    for number, label in enumerate(kept, start=1):
        remap[label] = number
    labels = _grow_into(remap[labels], support)

    islands: List[Island] = []
    for number, label in enumerate(kept, start=1):
        pixels = labels == number
        held = [part for part, mine in zip(parts, part_support) if (mine & pixels).any()]
        island = Island(number, float(stats[label, cv2.CC_STAT_AREA]), held)
        for row in range(y0 // TILE_SIZE, (y0 + height - 1) // TILE_SIZE + 1):
            for col in range(x0 // TILE_SIZE, (x0 + width - 1) // TILE_SIZE + 1):
                tx, ty = col * TILE_SIZE, row * TILE_SIZE
                tile = np.zeros((TILE_SIZE, TILE_SIZE), dtype=np.bool_)
                xa, xb = max(tx, x0), min(tx + TILE_SIZE, x0 + width)
                ya, yb = max(ty, y0), min(ty + TILE_SIZE, y0 + height)
                tile[ya - ty:yb - ty, xa - tx:xb - tx] = pixels[ya - y0:yb - y0, xa - x0:xb - x0]
                if tile.any():
                    island.settle((row, col), tile)
        island.set_trust(x0, y0, np.where(pixels, weight, np.float32(0.0)).astype(np.float32))
        islands.append(island)
    return islands

