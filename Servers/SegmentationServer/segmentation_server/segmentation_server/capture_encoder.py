"""Encodes a cell the server assembled the way the Viking client encodes a capture it uploads.

A cell the server reads itself and one a client uploads under the same key should look alike, so this is a port of
the greyscale path of the client's SegmentationCaptureEncoder (Clients/Viking/WebAnnotation/UI/Commands/
Segmentation): the 1st and 99th percentiles of the histogram are stretched to 0 and 255 with integer arithmetic, and
the result is an 8-bit greyscale PNG.
"""

from __future__ import annotations

import math
from typing import Tuple

import cv2
import numpy as np
from numpy.typing import NDArray


def _percentile(histogram: NDArray[np.int64], target: int) -> int:
    """The first value whose cumulative count reaches ``target``."""
    return int(np.searchsorted(np.cumsum(histogram), target))


def stretch_range(luma: NDArray[np.uint8]) -> Tuple[int, int]:
    """The (lo, hi) the client stretches to 0..255: the 1st and 99th percentiles, or the occupied range when those meet."""
    if luma.size == 0:
        return 0, 255
    histogram = np.bincount(luma.ravel(), minlength=256)
    low_count = max(1, math.ceil(luma.size * (1 / 100.0)))
    high_count = max(low_count, math.ceil(luma.size * (99 / 100.0)))
    lo = _percentile(histogram, low_count)
    hi = _percentile(histogram, high_count)
    if hi <= lo:
        occupied = np.flatnonzero(histogram)
        lo, hi = int(occupied[0]), int(occupied[-1])
    return lo, hi


def stretch(luma: NDArray[np.uint8]) -> NDArray[np.uint8]:
    lo, hi = stretch_range(luma)
    if hi <= lo:
        return luma.copy()
    scaled = (luma.astype(np.int32) - lo) * 255 // (hi - lo)
    return np.clip(scaled, 0, 255).astype(np.uint8)


def encode_capture(luma: NDArray[np.uint8]) -> bytes:
    """The stretched cell as an 8-bit greyscale PNG (fast compression, as the client writes it)."""
    ok, encoded = cv2.imencode(".png", stretch(luma), [cv2.IMWRITE_PNG_COMPRESSION, 1])
    if not ok:
        raise ValueError("PNG encoding failed")
    return encoded.tobytes()
