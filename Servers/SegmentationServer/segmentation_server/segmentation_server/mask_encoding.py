"""
PNG encoding helpers for SAM2 segmentation responses.
"""

import io
from typing import Tuple

import cv2
import numpy as np
from numpy.typing import NDArray
from PIL import Image


# Samples kept outside the boolean mask so the client can interpolate logit zero.
LOGIT_CROP_PAD = 8


def encode_probability_mask_png(logits: NDArray[np.floating]) -> bytes:
    """Encode SAM2 logits as an 8-bit probability PNG.

    Pixel value 128 is the logit-zero contour (probability 0.5). The client
    interpolates that level instead of tracing a thresholded bitmap.
    """
    if logits.size == 0:
        logits = np.zeros((1, 1), dtype=np.float32)

    probability = _stable_sigmoid(logits.astype(np.float32))
    image_u8 = np.clip(np.rint(probability * 255.0), 0, 255).astype(np.uint8)
    image = Image.fromarray(image_u8, mode="L")
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


def padded_logit_crop(
    logits: NDArray[np.floating],
    mask: NDArray[np.bool_],
    pad: int = LOGIT_CROP_PAD,
) -> Tuple[NDArray[np.floating], int, int]:
    """Crop logits to the true-mask bounds plus ``pad``.

    The boolean mask ends at logit zero. The pad keeps samples on the outside
    of that crossing so interpolation is not clamped to the crop edge.
    Returns the crop and its top-left corner in full-image pixels.
    """
    rows, cols = np.where(mask)
    if len(rows) == 0:
        return np.zeros((0, 0), dtype=np.float32), 0, 0

    height, width = logits.shape[:2]
    x0 = max(0, int(np.min(cols)) - pad)
    y0 = max(0, int(np.min(rows)) - pad)
    x1 = min(width, int(np.max(cols)) + 1 + pad)
    y1 = min(height, int(np.max(rows)) + 1 + pad)
    return logits[y0:y1, x0:x1], x0, y0


def _stable_sigmoid(logits: NDArray[np.floating]) -> NDArray[np.float32]:
    """Sigmoid that does not overflow for large positive or negative logits."""
    positive = logits >= 0
    result = np.empty(logits.shape, dtype=np.float32)
    result[positive] = 1.0 / (1.0 + np.exp(-logits[positive]))
    exp_negative = np.exp(logits[~positive])
    result[~positive] = exp_negative / (1.0 + exp_negative)
    return result


def encode_binary_mask_png(mask: NDArray[np.bool_]) -> bytes:
    """Encode a boolean mask as a 1-bit grayscale PNG."""
    if mask.size == 0:
        mask = np.zeros((1, 1), dtype=np.bool_)
    image = Image.fromarray(mask.astype(np.uint8) * 255, mode="L").convert("1")

    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


def encode_labeled_image_png(labeled_image: NDArray[np.uint16], omit: bool) -> bytes:
    """Encode the full-frame labeled image, or return empty bytes when omitted."""
    if omit:
        return b""

    encoded: Tuple[bool, NDArray[np.uint8]] = cv2.imencode(".png", labeled_image)
    return encoded[1].tobytes()


def decode_binary_mask_png(png_bytes: bytes) -> NDArray[np.bool_]:
    """Decode a PNG mask back to a boolean array."""
    image = Image.open(io.BytesIO(png_bytes))
    array = np.array(image)
    if array.ndim == 3:
        array = array[..., 0]
    return array > 0
