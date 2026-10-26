"""Encode a finished growth walk as one ``SegmentResult``: SAM2's fused logits at native resolution, and where they sit.

This is CPU work that grows with the mask (hole filling, bounds), kept apart from the servicer so it can be tested on
its own and run on a worker thread.

Placement. The walk's fused logits are a Y-down grid, ``LOGIT_SCALE`` mosaic pixels per sample, whose bottom-left pixel
is the walk's ``(origin_x, origin_y)`` (Y up) and whose height is ``rows * LOGIT_SCALE`` pixels. Grid row ``r`` covers
mosaic pixels ``y`` from ``origin_y + H - (r + 1) * s`` to ``origin_y + H - r * s - 1`` (``H`` the grid's height in
pixels, ``s`` the scale) and column ``c`` covers ``x`` from ``origin_x + c * s`` to ``origin_x + (c + 1) * s - 1``. The
result is a crop of that grid starting at row ``top`` and column ``left``, so its sample ``(0, 0)`` has its top-left
pixel at ``x = origin_x + left * s`` and ``y = origin_y + H - 1 - top * s``: that pixel is ``SegmentResult.logits_origin``
(see segmentation.proto). The protocol before this one sent the same numbers as the response's
``origin_x``/``origin_y`` and the segment's Y-down ``logits_x = left * s`` and ``logits_y = top * s`` relative to the
grid's top-left corner; :func:`logits_origin` is that conversion.
"""

from __future__ import annotations

from typing import Optional, Tuple

import numpy as np
from numpy.typing import NDArray

from segmentation_grpc import IntPoint, IntSize, SegmentResult
from segmentation_server.cell_grid import LOGIT_SCALE, UNSEEN_LOGIT
from segmentation_server.mask_utils import fill_small_holes, get_mask_bounds

# Background samples kept around the mask in a logits crop so the client's contour closes on the
# object's own edge instead of the grid border.
LOGITS_BORDER = 2


def logits_origin(
    origin_x: int, origin_y: int, grid_rows: int, left: int, top: int, scale: int = LOGIT_SCALE
) -> Tuple[int, int]:
    """The mosaic pixel (X right, Y up) at the top-left corner of a logits crop's sample ``(0, 0)``.

    ``(origin_x, origin_y)`` is the bottom-left pixel of the walk's fused grid, ``grid_rows`` its height in samples,
    and the crop starts at sample row ``top`` (counted down from the grid's top) and column ``left``.
    """
    return origin_x + left * scale, origin_y + grid_rows * scale - 1 - top * scale


def logits_from_mask(mask: NDArray[np.bool_], iso: float, scale: int = LOGIT_SCALE) -> NDArray[np.float32]:
    """Logits for a walk whose predictions carried none: ``iso + 1`` where at least half of a sample's pixels are set,
    ``UNSEEN_LOGIT`` elsewhere, so the client's contour at ``iso`` is the mask at the logit grid's resolution."""
    rows, cols = mask.shape[0] // scale, mask.shape[1] // scale
    covered = mask[:rows * scale, :cols * scale].reshape(rows, scale, cols, scale).mean(axis=(1, 3)) >= 0.5
    return np.where(covered, np.float32(iso + 1.0), np.float32(UNSEEN_LOGIT)).astype(np.float32)


def build_island_result(
    island: int,
    score: float,
    mask: NDArray[np.bool_],
    fused_logits: Optional[NDArray],
    origin_x: int,
    origin_y: int,
    iso: float,
    scale: int = LOGIT_SCALE,
) -> Optional[SegmentResult]:
    """One island's result, or None when the walk found nothing above ``iso``.

    ``mask`` is the walk's full-resolution answer (Y-down) and ``fused_logits`` its fused grid at ``1 / scale`` of that
    size (None when no prediction returned logits; they are then made from the mask). The object is
    ``fused_logits > iso``; the logits are cropped to its bounds (small holes filled first, as the client contours them)
    plus ``LOGITS_BORDER`` samples, and sent as little-endian float16, rows from the top.
    """
    if fused_logits is None:
        if mask.ndim != 2 or not mask.any():
            return None
        fused_logits = logits_from_mask(mask, iso, scale)
    logits = np.asarray(fused_logits, dtype=np.float32)
    if logits.ndim != 2 or logits.size == 0:
        return None
    frame = fill_small_holes(logits > iso)
    x, y, width, height = get_mask_bounds(frame)
    if width == 0 or height == 0:
        return None
    rows, cols = logits.shape
    left = max(0, x - LOGITS_BORDER)
    top = max(0, y - LOGITS_BORDER)
    right = min(cols, x + width + LOGITS_BORDER)
    bottom = min(rows, y + height + LOGITS_BORDER)
    crop = np.ascontiguousarray(logits[top:bottom, left:right], dtype='<f2')
    origin = logits_origin(origin_x, origin_y, rows, left, top, scale)
    return SegmentResult(
        island=int(island),
        score=float(score),
        logits=crop.tobytes(),
        logits_size=IntSize(width=int(crop.shape[1]), height=int(crop.shape[0])),
        logits_origin=IntPoint(x=int(origin[0]), y=int(origin[1])),
        logits_scale=float(scale),
    )
