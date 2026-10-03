"""CPU-only mask and image helpers used by the gRPC segmentation server.

These functions are independent of SAM2/torch so they can be unit-tested without
a GPU or the model weights.
"""

from __future__ import annotations

import io
import os
from typing import List, NamedTuple, Optional, Sequence, Tuple, TypedDict

import cv2
import numpy as np
from numpy.typing import NDArray
from PIL import Image

MaskArray = NDArray[np.bool_]
LabeledImage = NDArray[np.uint16]
PolygonArray = NDArray[np.int32]
Point = Tuple[int, int]

# Logit a pixel must exceed to be object when a client sends no mask_threshold. SAM2's own default
# is 0; 1.0 trims the over-segmentation seen on RC2 (0.5 still ran large). Clients that do send a
# value, including an explicit 0, override it.
DEFAULT_MASK_THRESHOLD = 1.0
class SegmentInfo(TypedDict):
    index: int
    score: float
    mask: MaskArray
    x: int
    y: int
    width: int
    height: int


DEFAULT_MAX_IMAGE_PIXELS = 100_000_000
_MAX_IMAGE_PIXELS_ENV = "SEGMENTATION_MAX_IMAGE_PIXELS"


def max_image_pixels() -> int:
    """Most pixels one decoded image may hold (SEGMENTATION_MAX_IMAGE_PIXELS, default 100 million).

    A few kilobytes of PNG can declare a canvas of billions of pixels. The size is read from the
    header, before the decoder allocates anything, so an upload cannot make the process allocate
    gigabytes. The largest image the server expects is a screen-sized viewport.
    """
    raw = os.environ.get(_MAX_IMAGE_PIXELS_ENV, "").strip()
    if not raw:
        return DEFAULT_MAX_IMAGE_PIXELS
    try:
        return max(1, int(raw))
    except ValueError:
        return DEFAULT_MAX_IMAGE_PIXELS


def ensure_image_within_limit(image: Image.Image) -> None:
    """Refuse an opened image whose declared size is over :func:`max_image_pixels`.

    Raises:
        ValueError: The width times height exceeds the limit. Nothing has been decoded yet.
    """
    width, height = image.size
    limit = max_image_pixels()
    if width * height > limit:
        raise ValueError(
            f"Image is {width}x{height} ({width * height} pixels); the limit is {limit} pixels."
        )


def prepare_image_for_sam2(image_data: bytes) -> NDArray[np.uint8]:
    """Decode image bytes to an RGB numpy array for SAM2.

    Args:
        image_data: Encoded image bytes (PNG, JPEG, etc.).

    Returns:
        Array of shape (H, W, 3) in RGB order.

    Raises:
        OSError: If the bytes are not a readable image.
        ValueError: If the image is over the pixel limit, or the decoded image cannot be
            converted to 3-channel RGB.
    """
    try:
        image = Image.open(io.BytesIO(image_data))
    except (OSError, IOError) as e:
        error_msg = str(e).lower()
        if any(keyword in error_msg for keyword in ['truncated', 'corrupted', 'invalid', 'cannot identify image file']):
            raise OSError(f"Image data is corrupted or truncated: {e}") from e
        raise
    ensure_image_within_limit(image)

    if image.mode != 'RGB':
        image = image.convert('RGB')

    image_np: NDArray[np.uint8] = np.array(image)
    if image_np.ndim != 3 or image_np.shape[2] != 3:
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


class PolygonShape(NamedTuple):
    """One connected piece of a mask: its outer ring and the rings of the holes inside it.

    Rings are ``(N, 2)`` int32 arrays of ``(x, y)`` pixel coordinates in mask order (Y down).
    ``holes`` is empty for a solid piece.
    """

    outer: PolygonArray
    holes: List[PolygonArray]


def _simplify_ring(contour: NDArray) -> Optional[PolygonArray]:
    """Douglas-Peucker simplify one contour at 0.5% of its length; None when under 3 points remain."""
    epsilon = 0.005 * cv2.arcLength(contour, True)
    approx = cv2.approxPolyDP(contour, epsilon, True)
    ring = approx.reshape(-1, 2)
    return ring if len(ring) >= 3 else None


def mask_to_polygons(mask: MaskArray) -> List[PolygonShape]:
    """Convert a boolean mask to simplified polygons, each with its interior (hole) rings.

    Uses the contour hierarchy: a boundary with no parent is an outer ring, and the boundaries
    nested directly inside it are its holes. An island inside a hole is a new outer ring of its
    own. A ring that simplifies to fewer than 3 points is dropped, and so are the holes of a
    dropped outer ring.
    """
    mask_uint8 = mask.astype(np.uint8) * 255
    contours, hierarchy = cv2.findContours(mask_uint8, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_SIMPLE)
    if hierarchy is None:
        return []

    shapes: List[PolygonShape] = []
    outer_slot: dict[int, int] = {}
    for index, contour in enumerate(contours):
        if int(hierarchy[0][index][3]) >= 0:
            continue
        ring = _simplify_ring(contour)
        if ring is None:
            continue
        outer_slot[index] = len(shapes)
        shapes.append(PolygonShape(ring, []))

    for index, contour in enumerate(contours):
        parent = int(hierarchy[0][index][3])
        if parent < 0 or parent not in outer_slot:
            continue
        ring = _simplify_ring(contour)
        if ring is not None:
            shapes[outer_slot[parent]].holes.append(ring)

    return shapes


def get_mask_bounds(mask: MaskArray) -> Tuple[int, int, int, int]:
    """Return (x, y, width, height) of the True region, or zeros if empty."""
    rows, cols = np.where(mask)
    if len(rows) == 0:
        return 0, 0, 0, 0

    min_row, max_row = np.min(rows), np.max(rows)
    min_col, max_col = np.min(cols), np.max(cols)
    return int(min_col), int(min_row), int(max_col - min_col + 1), int(max_row - min_row + 1)


def fill_small_holes(mask: MaskArray, hole_threshold: float = 0.03) -> MaskArray:
    """Fill interior holes smaller than ``hole_threshold`` of the True area, in pixels.

    A hole is a piece of background that does not touch the image border, so the background
    between blobs is never one. Disconnected components are kept. Background is joined with
    4-connectivity, the dual of the 8-connected foreground the polygon extraction traces.
    """
    if not np.any(mask):
        return mask

    background = np.logical_not(mask).astype(np.uint8)
    count, labels, stats, _centroids = cv2.connectedComponentsWithStats(background, connectivity=4)
    if count <= 1:
        return mask

    on_border = np.unique(np.concatenate((labels[0, :], labels[-1, :], labels[:, 0], labels[:, -1])))
    limit = float(np.count_nonzero(mask)) * hole_threshold
    fill = np.zeros(count, dtype=np.bool_)
    for label in range(1, count):
        if label in on_border:
            continue
        if stats[label, cv2.CC_STAT_AREA] < limit:
            fill[label] = True
    if not fill.any():
        return mask
    return np.logical_or(mask, fill[labels])


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


class NoMatchingMask(Exception):
    """No candidate mask fits the prompt, so there is no honest answer to return.

    The server reports it as ``FAILED_PRECONDITION`` with a ``NO_MATCHING_MASK`` prefix and logs
    it. It is never papered over with an empty or best-effort mask.
    """


def mask_covers_box(mask: MaskArray, box: Sequence[int]) -> bool:
    """True when every pixel of the inclusive ``(x0, y0, x1, y1)`` box is set in ``mask``.

    The box is in the mask's own pixel frame (Y down). A box that reaches outside the mask can
    never be covered, because the pixels outside it are not set.
    """
    x0, y0, x1, y1 = (int(value) for value in box)
    if x1 < x0 or y1 < y0:
        return False
    height, width = mask.shape[-2], mask.shape[-1]
    if x0 < 0 or y0 < 0 or x1 >= width or y1 >= height:
        return False
    return bool(mask[y0:y1 + 1, x0:x1 + 1].all())


def count_covered_points(mask: MaskArray, points: Sequence[Point]) -> int:
    """How many of ``points`` fall on a set pixel of ``mask``."""
    return sum(1 for x, y in points if mask_contains_xy(mask, x, y))


def select_mask(
    masks: NDArray,
    scores: NDArray,
    box: Optional[Sequence[int]] = None,
    positives: Sequence[Point] = (),
) -> int:
    """Index of the one candidate mask that answers the prompt. The same rule for every RPC.

    With a ``box`` (the starting rectangle): of the masks that cover every pixel of it, the one
    with the highest score. Auto-segmentation masks grow past their rectangle, so a mask that
    does not cover it is not an answer to it.

    Without a box: the mask that covers the most ``positives`` (foreground points), ties broken by
    score. A mask must cover at least one.

    Nothing is combined: the chosen mask is returned as SAM2 made it, and nothing is searched for
    when no candidate qualifies.

    Raises:
        NoMatchingMask: No candidate satisfies the rule.
    """
    batch = as_bool_mask_batch(masks)
    score_vec = np.asarray(scores, dtype=np.float32).reshape(-1)
    count = min(int(batch.shape[0]) if batch.ndim == 3 else 0, int(score_vec.shape[0]))
    if count == 0:
        raise NoMatchingMask("SAM2 returned no candidate masks.")

    if box is not None:
        eligible = [i for i in range(count) if mask_covers_box(batch[i], box)]
        if not eligible:
            raise NoMatchingMask(
                f"None of the {count} candidate mask(s) covers the whole starting rectangle {tuple(int(v) for v in box)}."
            )
        return max(eligible, key=lambda i: (float(score_vec[i]), -i))

    covered = [count_covered_points(batch[i], positives) for i in range(count)]
    best = max(covered)
    if best == 0:
        raise NoMatchingMask(
            f"None of the {count} candidate mask(s) covers any of the {len(positives)} foreground point(s)."
        )
    return max((i for i in range(count) if covered[i] == best), key=lambda i: (float(score_vec[i]), -i))


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
    """Paint non-overlapping labels (1..N) from masks sorted by caller, skipping empty masks.

    Labels are dense: the k-th non-empty mask is label k + 1 and segment ``index`` k, however many
    empty masks came before it, so ``index + 1`` is always the pixel value in the labeled image.
    """
    if masks.size == 0 or len(masks.shape) < 3:
        empty_image: LabeledImage = np.zeros(empty_shape, dtype=np.uint16)
        return empty_image, []

    mask_height, mask_width = masks.shape[1], masks.shape[2]
    labeled_image: LabeledImage = np.zeros((mask_height, mask_width), dtype=np.uint16)
    segments: List[SegmentInfo] = []

    for i, mask in enumerate(masks):
        if not np.any(mask):
            continue

        slot = len(segments)
        label_id = slot + 1
        unlabeled_pixels = np.logical_and(mask, labeled_image == 0)
        labeled_image[unlabeled_pixels] = label_id

        x, y, width, height = get_mask_bounds(mask)
        segments.append({
            'index': slot,
            'score': float(scores[i]),
            'mask': mask,
            'x': x,
            'y': y,
            'width': width,
            'height': height
        })

    return labeled_image, segments
