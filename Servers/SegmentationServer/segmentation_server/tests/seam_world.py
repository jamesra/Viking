"""A synthetic mosaic for seam walk tests.

A ground-truth mask is spread over a small grid of tiles. The fake predictors
return the component of that mask under the prompts, cropped to a tile or to a
half-tile stitched across a border, the way SAM2 would for a perfect model.
Dilating the answer by a few pixels per call imitates the run-to-run wobble of
the real model, which is what makes a tile's mask grow between visits.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Sequence, Tuple

import cv2
import numpy as np

from segmentation_server.tile_growth import (
    GrowthResult,
    SeamGraph,
    TileIndex,
    grow_segmentation,
    stitch_half_tile,
)

Point = Tuple[int, int]
EdgeKey = Tuple[int, int, int, int]

SIZE = 32


def edge_key(a: TileIndex, b: TileIndex) -> EdgeKey:
    """Undirected identity of the border between two cells."""
    left, right = (a.row, a.col), (b.row, b.col)
    if right < left:
        left, right = right, left
    return left[0], left[1], right[0], right[1]


@dataclass
class SeamWorld:
    """Ground truth plus the fakes that answer for it, and a log of seam calls."""

    truth: np.ndarray
    rows: int
    cols: int
    wobble: Tuple[int, ...] = (0,)
    seam_calls: List[Tuple[EdgeKey, str]] = field(default_factory=list)
    seam_prompts: List[Tuple[EdgeKey, str, Tuple[Point, ...], Tuple[int, ...]]] = field(default_factory=list)
    predict_calls: int = 0
    _wobble_index: int = 0

    def tile_truth(self, row: int, col: int) -> np.ndarray:
        """Image-oriented crop for one cell. Row grows with mosaic Y, so it is flipped."""
        top = (self.rows - 1 - row) * SIZE
        left = col * SIZE
        return self.truth[top : top + SIZE, left : left + SIZE].copy()

    def cells(self) -> List[TileIndex]:
        return [TileIndex(r, c) for r in range(self.rows) for c in range(self.cols)]

    def click(self) -> Point:
        """A mosaic point inside the truth. The deepest pixel, so it is a real click."""
        distance = cv2.distanceTransform(self.truth.astype(np.uint8), cv2.DIST_L2, 3)
        image_y, image_x = np.unravel_index(int(np.argmax(distance)), distance.shape)
        mosaic_y = (self.rows * SIZE - 1) - int(image_y)
        return int(image_x), int(mosaic_y)

    def predict(self, row: int, col: int, points: Sequence[Point], labels: Sequence[int]):
        self.predict_calls += 1
        return self._component(self.tile_truth(row, col), points, labels), None, 0.9

    def seam_predict(
        self,
        src_row: int,
        src_col: int,
        dst_row: int,
        dst_col: int,
        side: str,
        points: Sequence[Point],
        labels: Sequence[int],
    ):
        key = edge_key(TileIndex(src_row, src_col), TileIndex(dst_row, dst_col))
        self.seam_calls.append((key, side))
        self.seam_prompts.append(
            (key, side, tuple((int(x), int(y)) for x, y in points), tuple(int(v) for v in labels))
        )
        stitched = stitch_half_tile(
            self.tile_truth(src_row, src_col), self.tile_truth(dst_row, dst_col), side
        )
        mask = self._component(stitched, points, labels)
        grow = self.wobble[self._wobble_index % len(self.wobble)]
        self._wobble_index += 1
        if grow > 0:
            kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * grow + 1, 2 * grow + 1))
            mask = cv2.dilate(mask.astype(np.uint8), kernel).astype(np.bool_)
        return mask, None, 0.8

    @staticmethod
    def _component(image: np.ndarray, points: Sequence[Point], labels: Sequence[int]) -> np.ndarray:
        count, labeled = cv2.connectedComponents(image.astype(np.uint8), connectivity=8)
        del count
        keep = set()
        for (x, y), label in zip(points, labels):
            if label != 1:
                continue
            if 0 <= y < image.shape[0] and 0 <= x < image.shape[1] and labeled[y, x] > 0:
                keep.add(int(labeled[y, x]))
        if not keep:
            return np.zeros(image.shape, dtype=np.bool_)
        return np.isin(labeled, list(keep))

    def walk(self, graph: SeamGraph | None = None) -> GrowthResult:
        """One SegmentTiles call with every cell uploaded and one click."""
        return grow_segmentation(
            uploaded=self.cells(),
            foreground=[self.click()],
            background=[],
            predict=self.predict,
            seam_predict=self.seam_predict,
            seam_graph=graph,
            tile_size=SIZE,
        )

    def click_component(self) -> np.ndarray:
        """The truth component under the click, in the image-oriented world canvas."""
        x, y = self.click()
        image_y = (self.rows * SIZE - 1) - y
        _count, labeled = cv2.connectedComponents(self.truth.astype(np.uint8), connectivity=8)
        return labeled == labeled[image_y, x]

    def canvas(self, result: GrowthResult) -> np.ndarray:
        """A walk result placed on the world canvas, image-oriented like ``truth``."""
        full = np.zeros((self.rows * SIZE, self.cols * SIZE), dtype=np.bool_)
        mask = result.mask
        if mask.size == 0:
            return full
        max_row = result.origin_y // SIZE + mask.shape[0] // SIZE - 1
        top = (self.rows - 1 - max_row) * SIZE
        left = result.origin_x
        full[top : top + mask.shape[0], left : left + mask.shape[1]] = mask
        return full

    def crossings_by_edge(self) -> Dict[EdgeKey, int]:
        counts: Dict[EdgeKey, int] = {}
        for key, _side in self.seam_calls:
            counts[key] = counts.get(key, 0) + 1
        return counts

    def repeated_prompts(self) -> List[Tuple[EdgeKey, str, Tuple[Point, ...], Tuple[int, ...]]]:
        """Seam predicts that asked exactly what an earlier call already asked."""
        seen = set()
        repeats = []
        for item in self.seam_prompts:
            if item in seen:
                repeats.append(item)
            seen.add(item)
        return repeats


def blob(rows: int, cols: int, shapes: Sequence[Tuple[str, int, int, int, int]]) -> np.ndarray:
    """Union of rectangles and ellipses (kind, cx, cy, rx, ry) in global image pixels."""
    truth = np.zeros((rows * SIZE, cols * SIZE), dtype=np.uint8)
    for kind, cx, cy, rx, ry in shapes:
        if kind == "rect":
            cv2.rectangle(truth, (cx - rx, cy - ry), (cx + rx, cy + ry), 1, thickness=-1)
        else:
            cv2.ellipse(truth, (cx, cy), (max(1, rx), max(1, ry)), 0, 0, 360, 1, thickness=-1)
    return truth.astype(np.bool_)
