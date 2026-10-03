"""A synthetic mosaic for growth tests.

``World`` holds a ground-truth mask (Y-up, origin at mosaic (0, 0)) and stands in for SAM2:
given a cell and window-local clicks it returns the pieces of the truth inside that window
that contain a positive click. A thin band at the window border is blanked on purpose, to
mimic SAM2 treating an image edge as an object edge. Growth must be correct despite that,
because overlapping windows fill in each other's blanked borders.
"""

from __future__ import annotations

from typing import Iterable, List, Optional, Sequence, Set, Tuple

import cv2
import numpy as np

from segmentation_server.cell_grid import CELL_SIZE, Cell, Point, tiles_for_cell, window_origin
from segmentation_server.mask_utils import NoMatchingMask, count_covered_points, mask_covers_box
from segmentation_server.tile_growth import GrowthResult, PredictUnavailable

DOMAIN = 4096


def ellipse(cx: float, cy: float, a: float, b: float, theta: float) -> np.ndarray:
    """Y-up truth mask of a rotated ellipse, computed only inside its bounding box."""
    truth = np.zeros((DOMAIN, DOMAIN), dtype=bool)
    reach = int(max(a, b)) + 2
    x_lo, x_hi = max(0, int(cx) - reach), min(DOMAIN, int(cx) + reach)
    y_lo, y_hi = max(0, int(cy) - reach), min(DOMAIN, int(cy) + reach)
    xs = (np.arange(x_lo, x_hi, dtype=np.float32) - np.float32(cx))[None, :]
    ys = (np.arange(y_lo, y_hi, dtype=np.float32) - np.float32(cy))[:, None]
    u = xs * np.float32(np.cos(theta)) + ys * np.float32(np.sin(theta))
    v = -xs * np.float32(np.sin(theta)) + ys * np.float32(np.cos(theta))
    truth[y_lo:y_hi, x_lo:x_hi] = (u / np.float32(a)) ** 2 + (v / np.float32(b)) ** 2 <= 1.0
    return truth


def stroke(points: Sequence[Tuple[int, int]], width: int) -> np.ndarray:
    """Y-up truth mask of a thick polyline."""
    canvas = np.zeros((DOMAIN, DOMAIN), dtype=np.uint8)
    cv2.polylines(canvas, [np.array(points, dtype=np.int32)], False, 1, thickness=width)
    return canvas.astype(bool)


def ring(cx: int, cy: int, radius: int, width: int) -> np.ndarray:
    """Y-up truth mask of an annulus."""
    canvas = np.zeros((DOMAIN, DOMAIN), dtype=np.uint8)
    cv2.circle(canvas, (cx, cy), radius, 1, thickness=width)
    return canvas.astype(bool)


def circle_clicks(cx: int, cy: int, radius: int) -> List[Point]:
    """The client's nine foreground clicks: center, a 0.5 radius ring of four rotated 45
    degrees, and a 0.8 radius ring of four on the axes."""
    points = [(cx, cy)]
    for fraction, start in ((0.5, np.pi / 4.0), (0.8, 0.0)):
        for i in range(4):
            angle = start + 2.0 * np.pi * i / 4
            points.append((int(round(cx + radius * fraction * np.cos(angle))),
                           int(round(cy + radius * fraction * np.sin(angle)))))
    return points


class World:
    """Ground truth plus a SAM2 stand-in. ``calls`` records every prediction."""

    def __init__(
        self,
        truth_up: np.ndarray,
        edge_px: int = 6,
        available: Optional[Iterable[Tuple[int, int]]] = None,
    ) -> None:
        self.truth_up = truth_up
        self.edge_px = edge_px
        self.available: Optional[Set[Tuple[int, int]]] = None if available is None else set(available)
        self.calls: List[Tuple[Cell, List[Point], List[int]]] = []

    def predict(self, row: int, col: int, points: Sequence[Point], labels: Sequence[int], box=None):
        """Stand in for the server's per-cell predict, selection rule included.

        With a ``box`` (window-local, Y-down, inclusive) the answer must cover all of it; without
        one it must cover at least one foreground click. Otherwise ``NoMatchingMask``, exactly as
        ``SegmentationModel.predict_tile`` raises it.
        """
        cell = Cell(row, col)
        if self.available is not None:
            missing = [t for t in tiles_for_cell(cell) if (t.row, t.col) not in self.available]
            if missing:
                raise PredictUnavailable("tiles not held", missing)

        self.calls.append((cell, list(points), list(labels)))
        window = np.flipud(self._window_up(cell))
        edge = self.edge_px
        if edge:
            window[:edge] = False
            window[-edge:] = False
            window[:, :edge] = False
            window[:, -edge:] = False

        count, pieces = cv2.connectedComponents(window.astype(np.uint8), connectivity=8)
        keep = {int(pieces[y, x]) for (x, y), label in zip(points, labels) if label == 1 and pieces[y, x] > 0}
        mask = np.isin(pieces, list(keep)) if keep else np.zeros_like(window)
        positives = [(x, y) for (x, y), label in zip(points, labels) if label == 1]
        if box is not None:
            if not mask_covers_box(mask, box):
                raise NoMatchingMask(f"mask does not cover box {tuple(box)} in cell {row},{col}")
        elif positives and count_covered_points(mask, positives) == 0:
            raise NoMatchingMask(f"mask covers none of the {len(positives)} click(s) in cell {row},{col}")
        return mask, self.logits_for(mask), 0.9

    @staticmethod
    def logits_for(mask: np.ndarray, confidence: float = 6.0) -> np.ndarray:
        """A full-window logit map that is ``+confidence`` on the mask and ``-confidence`` off it."""
        return np.where(mask, np.float32(confidence), np.float32(-confidence)).astype(np.float32)

    def _window_up(self, cell: Cell) -> np.ndarray:
        x0, y0 = window_origin(cell)
        out = np.zeros((CELL_SIZE, CELL_SIZE), dtype=bool)
        xa, xb = max(x0, 0), min(x0 + CELL_SIZE, DOMAIN)
        ya, yb = max(y0, 0), min(y0 + CELL_SIZE, DOMAIN)
        if xa < xb and ya < yb:
            out[ya - y0:yb - y0, xa - x0:xb - x0] = self.truth_up[ya:yb, xa:xb]
        return out

    def to_truth_frame(self, result: GrowthResult) -> np.ndarray:
        """The fused result placed back into the truth array's Y-up frame."""
        out = np.zeros_like(self.truth_up)
        if result.mask.size == 0:
            return out
        height, width = result.mask.shape
        y_hi = min(DOMAIN, result.origin_y + height)
        x_hi = min(DOMAIN, result.origin_x + width)
        up = np.flipud(result.mask)
        out[result.origin_y:y_hi, result.origin_x:x_hi] = up[:y_hi - result.origin_y, :x_hi - result.origin_x]
        return out


def iou(a: np.ndarray, b: np.ndarray) -> float:
    union = int(np.count_nonzero(a | b))
    return 1.0 if union == 0 else int(np.count_nonzero(a & b)) / union


def longest_boundary_run(mask: np.ndarray) -> int:
    """Longest straight run of boundary pixels along one row or one column."""
    ys, xs = np.nonzero(mask)
    if len(ys) == 0:
        return 0
    box = mask[max(ys.min() - 1, 0):ys.max() + 2, max(xs.min() - 1, 0):xs.max() + 2]
    padded = np.pad(box, 1)
    edges = (
        padded[1:-1, 1:-1] & ~padded[:-2, 1:-1],
        padded[1:-1, 1:-1] & ~padded[2:, 1:-1],
        (padded[1:-1, 1:-1] & ~padded[1:-1, :-2]).T,
        (padded[1:-1, 1:-1] & ~padded[1:-1, 2:]).T,
    )
    return max(_longest_run(lines) for lines in edges)


def _longest_run(lines: np.ndarray) -> int:
    best = 0
    for line in lines:
        if not line.any():
            continue
        padded = np.concatenate(([0], line.astype(np.int8), [0]))
        change = np.diff(padded)
        starts = np.nonzero(change == 1)[0]
        ends = np.nonzero(change == -1)[0]
        best = max(best, int((ends - starts).max()))
    return best
