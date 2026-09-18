"""CPU-only mask and image helpers used by the gRPC segmentation server.

These functions are independent of SAM2/torch so they can be unit-tested without
a GPU or the model weights.
"""

from __future__ import annotations

import io
from typing import List, Tuple, TypedDict

import cv2
import numpy as np
from numpy.typing import NDArray
from PIL import Image

MaskArray = NDArray[np.bool_]
LabeledImage = NDArray[np.uint16]
PolygonArray = NDArray[np.int32]
Point = Tuple[int, int]


class SegmentInfo(TypedDict):
    index: int
    score: float
    mask: MaskArray
    x: int
    y: int
    width: int
    height: int


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


def cleanup_mask(mask: MaskArray, hole_threshold: float = 0.03) -> MaskArray:
    """Keep the largest connected component and fill holes smaller than hole_threshold of its area."""
    mask_uint8 = mask.astype(np.uint8) * 255
    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask_uint8, connectivity=8)

    if num_labels <= 1:
        return mask

    largest_component_idx = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    cleaned_mask = (labels == largest_component_idx).astype(np.bool_)
    cleaned_mask_uint8 = cleaned_mask.astype(np.uint8) * 255

    mask_copy = cleaned_mask_uint8.copy()
    holes_contours, holes_hierarchy = cv2.findContours(
        ~cleaned_mask_uint8,
        cv2.RETR_CCOMP,
        cv2.CHAIN_APPROX_SIMPLE
    )

    if holes_hierarchy is not None:
        total_mask_area = np.sum(cleaned_mask)
        for i, contour in enumerate(holes_contours):
            if holes_hierarchy[0][i][3] >= 0:
                hole_area = cv2.contourArea(contour)
                if hole_area < (total_mask_area * hole_threshold):
                    cv2.fillPoly(mask_copy, [contour], 255)
        cleaned_mask = (mask_copy > 0).astype(np.bool_)

    return cleaned_mask


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
