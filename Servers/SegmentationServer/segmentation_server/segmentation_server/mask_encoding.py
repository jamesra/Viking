"""
PNG encoding helpers for SAM2 segmentation responses.
"""

import io
from typing import Tuple

import cv2
import numpy as np
from numpy.typing import NDArray
from PIL import Image


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
