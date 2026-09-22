"""CPU-only mask and image helpers used by the gRPC segmentation server.

These functions are independent of SAM2/torch so they can be unit-tested without
a GPU or the model weights.
"""

from __future__ import annotations

import io
from dataclasses import dataclass
from typing import Callable, List, Optional, Sequence, Tuple, TypedDict

import cv2
import numpy as np
from numpy.typing import NDArray
from PIL import Image

MaskArray = NDArray[np.bool_]
LabeledImage = NDArray[np.uint16]
PolygonArray = NDArray[np.int32]
Point = Tuple[int, int]
ExtraPredict = Callable[
    [Sequence[Point], Sequence[int]],
    Tuple[NDArray[np.bool_], NDArray[np.float32]],
]


class SegmentInfo(TypedDict):
    index: int
    score: float
    mask: MaskArray
    x: int
    y: int
    width: int
    height: int


@dataclass(frozen=True)
class UnionMaskStats:
    """Cheap counters from a union pass; all O(n_masks + n_clicks) plus one count_nonzero."""

    n_initial_masks: int = 0
    n_kept_initial: int = 0
    n_extra_predicts: int = 0
    n_extra_kept: int = 0
    n_positives: int = 0
    n_negatives: int = 0
    n_positives_oob: int = 0
    n_uncovered: int = 0
    area: int = 0
    score: float = 0.0

    @property
    def n_merged(self) -> int:
        return self.n_kept_initial + self.n_extra_kept

    def log_line(self, width: int, height: int) -> str:
        return (
            f"segment {width}x{height} "
            f"fg={self.n_positives} bg={self.n_negatives} "
            f"initial={self.n_initial_masks} kept={self.n_kept_initial} "
            f"extra={self.n_extra_predicts} extra_kept={self.n_extra_kept} "
            f"merged={self.n_merged} area={self.area} "
            f"uncovered={self.n_uncovered}/{self.n_positives} "
            f"oob={self.n_positives_oob} score={self.score:.3f}"
        )


def prepare_image_for_sam2(image_data: bytes) -> NDArray[np.uint8]:
    """Decode image bytes to an RGB numpy array for SAM2.

    Args:
        image_data: Encoded image bytes (PNG, JPEG, etc.).

    Returns:
        Array of shape (H, W, 3) in RGB order.

    Raises:
        OSError: If the bytes are not a readable image.
        ValueError: If the decoded image cannot be converted to 3-channel RGB.
    """
    try:
        image = Image.open(io.BytesIO(image_data))
    except (OSError, IOError) as e:
        error_msg = str(e).lower()
        if any(keyword in error_msg for keyword in ['truncated', 'corrupted', 'invalid', 'cannot identify image file']):
            raise OSError(f"Image data is corrupted or truncated: {e}") from e
        raise

    if image.mode != 'RGB':
        image = image.convert('RGB')

    image_np: NDArray[np.uint8] = np.array(image)

    if len(image_np.shape) == 3 and image_np.shape[2] == 4:
        image_np = image_np[:, :, :3]
    elif len(image_np.shape) == 3 and image_np.shape[2] == 1:
        image_np = np.repeat(image_np, 3, axis=2)
    elif len(image_np.shape) == 2:
        image_np = np.repeat(image_np[:, :, np.newaxis], 3, axis=2)

    if len(image_np.shape) != 3 or image_np.shape[2] != 3:
        raise ValueError(f"Expected RGB image with 3 channels, got shape: {image_np.shape}")

    return image_np


def encode_png(array: NDArray) -> bytes:
    """Encode an array as PNG bytes.

    Raises:
        ValueError: If OpenCV cannot encode the array.
    """
    ok, encoded = cv2.imencode('.png', array)
    if not ok:
        raise ValueError("Failed to encode image as PNG")
    return encoded.tobytes()


def mask_to_polygons(mask: MaskArray) -> List[PolygonArray]:
    """Convert a boolean mask to simplified external-contour polygons."""
    mask_uint8 = mask.astype(np.uint8) * 255
    contours, _ = cv2.findContours(mask_uint8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    polygons: List[PolygonArray] = []
    for contour in contours:
        epsilon = 0.005 * cv2.arcLength(contour, True)
        approx = cv2.approxPolyDP(contour, epsilon, True)
        polygon = approx.reshape(-1, 2)
        if len(polygon) >= 3:
            polygons.append(polygon)

    return polygons


def get_mask_bounds(mask: MaskArray) -> Tuple[int, int, int, int]:
    """Return (x, y, width, height) of the True region, or zeros if empty."""
    rows, cols = np.where(mask)
    if len(rows) == 0:
        return 0, 0, 0, 0

    min_row, max_row = np.min(rows), np.max(rows)
    min_col, max_col = np.min(cols), np.max(cols)
    return int(min_col), int(min_row), int(max_col - min_col + 1), int(max_row - min_row + 1)


def fill_small_holes(mask: MaskArray, hole_threshold: float = 0.03) -> MaskArray:
    """Fill interior holes smaller than hole_threshold of the True area.

    Does not drop disconnected components. Background between blobs is connected
    to the image border, so it is not treated as a hole.
    """
    if not np.any(mask):
        return mask

    mask_uint8 = mask.astype(np.uint8) * 255
    holes_contours, holes_hierarchy = cv2.findContours(
        ~mask_uint8,
        cv2.RETR_CCOMP,
        cv2.CHAIN_APPROX_SIMPLE,
    )
    if holes_hierarchy is None:
        return mask

    filled = mask_uint8.copy()
    total_mask_area = float(np.sum(mask))
    for i, contour in enumerate(holes_contours):
        if holes_hierarchy[0][i][3] >= 0:
            hole_area = cv2.contourArea(contour)
            if hole_area < (total_mask_area * hole_threshold):
                cv2.fillPoly(filled, [contour], 255)
    return (filled > 0).astype(np.bool_)


def cleanup_mask(mask: MaskArray, hole_threshold: float = 0.03) -> MaskArray:
    """Keep the largest connected component, then fill small holes.

    Do not use this after a positive-covering union; it would throw away extra blobs.
    """
    mask_uint8 = mask.astype(np.uint8) * 255
    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask_uint8, connectivity=8)

    if num_labels <= 1:
        return mask

    largest_component_idx = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    largest = (labels == largest_component_idx).astype(np.bool_)
    return fill_small_holes(largest, hole_threshold=hole_threshold)


def as_bool_mask_batch(masks: NDArray) -> NDArray[np.bool_]:
    """Normalize SAM2 predict() output to (N, H, W) bool."""
    arr = np.asarray(masks)
    if arr.ndim == 2:
        return np.expand_dims(arr, 0).astype(np.bool_, copy=False)
    if arr.ndim >= 3:
        return arr.astype(np.bool_, copy=False)
    return np.zeros((0, 0, 0), dtype=np.bool_)


def mask_contains_xy(mask: MaskArray, x: int, y: int) -> bool:
    """True when (x, y) is inside the mask and the pixel is set."""
    height, width = mask.shape[-2], mask.shape[-1]
    if x < 0 or y < 0 or x >= width or y >= height:
        return False
    return bool(mask[y, x])


def _labeled_points(
    coordinates: Sequence[Point],
    labels: Sequence[int],
    want_positive: bool,
) -> List[Point]:
    if want_positive:
        return [coord for coord, label in zip(coordinates, labels) if int(label) == 1]
    return [coord for coord, label in zip(coordinates, labels) if int(label) != 1]


def mask_covers_any_positive(mask: MaskArray, positives: Sequence[Point]) -> bool:
    return any(mask_contains_xy(mask, x, y) for x, y in positives)


def union_masks_covering_positives(
    initial_masks: NDArray,
    initial_scores: NDArray,
    coordinates: Sequence[Point],
    labels: Sequence[int],
    extra_predict: Optional[ExtraPredict] = None,
    empty_shape: Tuple[int, int] = (0, 0),
) -> Tuple[MaskArray, UnionMaskStats]:
    """OR full-frame masks that contain at least one positive click.

    Starts from the all-points predict. For each positive still uncovered,
    extra_predict is called with that click plus the shared negatives.
    Extra masks are kept only when they contain a positive pixel.
    """
    masks = as_bool_mask_batch(initial_masks)
    scores = np.asarray(initial_scores, dtype=np.float32).reshape(-1)
    positives = _labeled_points(coordinates, labels, want_positive=True)
    negatives = _labeled_points(coordinates, labels, want_positive=False)

    if masks.size == 0 or masks.ndim < 3 or masks.shape[0] == 0:
        height, width = empty_shape
        n_oob = sum(1 for x, y in positives if x < 0 or y < 0 or x >= width or y >= height)
        stats = UnionMaskStats(
            n_initial_masks=0,
            n_positives=len(positives),
            n_negatives=len(negatives),
            n_positives_oob=n_oob,
            n_uncovered=max(0, len(positives) - n_oob),
        )
        return np.zeros(empty_shape, dtype=np.bool_), stats

    shape = (int(masks.shape[1]), int(masks.shape[2]))
    height, width = shape
    n_oob = sum(1 for x, y in positives if x < 0 or y < 0 or x >= width or y >= height)

    union = np.zeros(shape, dtype=np.bool_)
    best_score = 0.0
    n_kept_initial = 0
    for mask, score in zip(masks, scores):
        if positives and not mask_covers_any_positive(mask, positives):
            continue
        if not positives and not np.any(mask):
            continue
        union |= mask
        best_score = max(best_score, float(score))
        n_kept_initial += 1

    n_extra_predicts = 0
    n_extra_kept = 0
    if extra_predict is not None and positives:
        for click in positives:
            if mask_contains_xy(union, click[0], click[1]):
                continue
            n_extra_predicts += 1
            extra_coords: List[Point] = [click, *negatives]
            extra_labels: List[int] = [1, *[0] * len(negatives)]
            extra_masks, extra_scores = extra_predict(extra_coords, extra_labels)
            extra_batch = as_bool_mask_batch(extra_masks)
            extra_score_vec = np.asarray(extra_scores, dtype=np.float32).reshape(-1)
            for extra_mask, extra_score in zip(extra_batch, extra_score_vec):
                if not mask_covers_any_positive(extra_mask, positives):
                    continue
                union |= extra_mask
                best_score = max(best_score, float(extra_score))
                n_extra_kept += 1

    n_uncovered = sum(
        1 for x, y in positives
        if 0 <= x < width and 0 <= y < height and not mask_contains_xy(union, x, y)
    )
    stats = UnionMaskStats(
        n_initial_masks=int(masks.shape[0]),
        n_kept_initial=n_kept_initial,
        n_extra_predicts=n_extra_predicts,
        n_extra_kept=n_extra_kept,
        n_positives=len(positives),
        n_negatives=len(negatives),
        n_positives_oob=n_oob,
        n_uncovered=n_uncovered,
        area=int(np.count_nonzero(union)),
        score=best_score,
    )
    return union, stats


def combined_mask_to_segments(
    union: MaskArray,
    score: float,
    empty_shape: Tuple[int, int] = (0, 0),
) -> Tuple[LabeledImage, List[SegmentInfo]]:
    """Wrap a single full-frame union mask as one SegmentInfo."""
    if union.size == 0 or union.ndim != 2:
        return process_masks(
            np.zeros((0, *empty_shape), dtype=np.bool_),
            np.array([], dtype=np.float32),
            empty_shape,
        )
    return process_masks(
        np.expand_dims(union, 0),
        np.array([score], dtype=np.float32),
        empty_shape,
    )


def process_masks(
    masks: NDArray[np.bool_],
    scores: NDArray[np.float32],
    empty_shape: Tuple[int, int] = (0, 0)
) -> Tuple[LabeledImage, List[SegmentInfo]]:
    """Paint non-overlapping labels (1..N) from masks sorted by caller, skipping empty masks."""
    if masks.size == 0 or len(masks.shape) < 3:
        empty_image: LabeledImage = np.zeros(empty_shape, dtype=np.uint16)
        return empty_image, []

    mask_height, mask_width = masks.shape[1], masks.shape[2]
    labeled_image: LabeledImage = np.zeros((mask_height, mask_width), dtype=np.uint16)
    segments: List[SegmentInfo] = []

    for i, mask in enumerate(masks):
        if not np.any(mask):
            continue

        label_id = i + 1
        unlabeled_pixels = np.logical_and(mask, labeled_image == 0)
        labeled_image[unlabeled_pixels] = label_id

        x, y, width, height = get_mask_bounds(mask)
        segments.append({
            'index': i,
            'score': float(scores[i]),
            'mask': mask,
            'x': x,
            'y': y,
            'width': width,
            'height': height
        })

    return labeled_image, segments
