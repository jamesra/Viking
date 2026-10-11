"""Encode a finished segmentation as the gRPC response: labeled image, cropped masks, polygons, logits.

This is CPU work that grows with the mask (hole filling, bounds, PNG encoding, contour tracing),
kept apart from the servicer so it can be tested on its own and run on a worker thread.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional, Tuple

import cv2
import numpy as np
from numpy.typing import NDArray

from segmentation_grpc import Point, Polygon, SegmentationResponse, SegmentResult
from segmentation_grpc.segmentation_pb2 import (
    RESPONSE_CONTENT_LOGITS,
    RESPONSE_CONTENT_MASKS,
    RESPONSE_CONTENT_NO_UPSCALE,
    RESPONSE_CONTENT_POLYGONS,
)
from segmentation_server.mask_utils import (
    SegmentInfo,
    encode_png,
    fill_small_holes,
    get_mask_bounds,
    mask_to_polygons,
)

# Background samples kept around the mask in a logits crop so the client's contour closes on the
# object's own edge instead of the grid border.
LOGITS_BORDER = 2

_KNOWN_FLAGS = (
    RESPONSE_CONTENT_MASKS | RESPONSE_CONTENT_POLYGONS | RESPONSE_CONTENT_LOGITS | RESPONSE_CONTENT_NO_UPSCALE
)


@dataclass(frozen=True)
class ResponseOptions:
    """What a client asked to get back, decoded from the ``ResponseContent`` flags of a request.

    ``masks`` and ``polygons`` choose the payloads built from the mask; ``logits`` adds the fused
    SAM2 logits. ``no_upscale`` is a modifier: the caller must hand over masks and logits at SAM2's
    native resolution and :func:`build_segmentation_response` is told the factor, so the client
    upscales after it has contoured. It is not a payload by itself.
    """

    masks: bool = True
    polygons: bool = True
    logits: bool = False
    no_upscale: bool = False

    @classmethod
    def from_flags(cls, flags: int) -> "ResponseOptions":
        """Decode request flags. Zero is the answer clients got before the flags existed: masks and polygons.

        Raises:
            ValueError: A bit outside ``ResponseContent`` is set, or only the ``NO_UPSCALE``
                modifier is set (it asks for nothing).
        """
        flags = int(flags)
        unknown = flags & ~_KNOWN_FLAGS
        if unknown:
            raise ValueError(f"Unknown response_content bits: {unknown:#x}")
        if flags == 0:
            return cls()
        options = cls(
            masks=bool(flags & RESPONSE_CONTENT_MASKS),
            polygons=bool(flags & RESPONSE_CONTENT_POLYGONS),
            logits=bool(flags & RESPONSE_CONTENT_LOGITS),
            no_upscale=bool(flags & RESPONSE_CONTENT_NO_UPSCALE),
        )
        if not (options.masks or options.polygons or options.logits):
            raise ValueError("response_content asks for NO_UPSCALE but for no masks, polygons or logits")
        return options


def choose_frame(
    mask: NDArray[np.bool_],
    fused_logits: Optional[NDArray],
    options: ResponseOptions,
    iso: float,
    logit_scale: int,
) -> Tuple[NDArray[np.bool_], Optional[NDArray], int]:
    """The mask, logits and pixel scale a finished walk is encoded in.

    ``mask`` is the walk's full-resolution result and ``fused_logits`` its fused logit grid at
    ``1 / logit_scale`` of that size (None when no prediction returned logits). With
    ``options.no_upscale`` and logits available the frame is the logit grid itself: the mask is
    ``fused_logits > iso`` and the scale is ``logit_scale``, so nothing is upscaled and the
    client does it after contouring. Otherwise the frame is the full-resolution mask, any
    requested logits are upsampled bilinearly to it, and the scale is 1. ``no_upscale`` cannot be
    honored without logits (the predictions had none), and the answer then falls back to the
    full-resolution mask.
    """
    if fused_logits is None or mask.ndim != 2:
        return mask, None, 1
    if options.no_upscale:
        return fused_logits > iso, fused_logits, logit_scale
    if options.logits:
        upscaled = cv2.resize(fused_logits, (mask.shape[1], mask.shape[0]), interpolation=cv2.INTER_LINEAR)
        return mask, upscaled, 1
    return mask, None, 1


def build_segmentation_response(
    labeled_image: NDArray[np.uint16],
    segments: List[SegmentInfo],
    width: int,
    height: int,
    omit_labeled_image: bool = False,
    options: Optional[ResponseOptions] = None,
    mask_scale: int = 1,
    logits: Optional[NDArray] = None,
) -> SegmentationResponse:
    """Encode a labeled PNG (unless omitted) plus each segment's cropped mask, polygons and logits.

    Holes smaller than 3% of a mask's area are filled before it is cropped and traced. Every
    polygon carries the rings of its remaining holes. ``options`` (default: masks and polygons)
    skips the payloads the client did not ask for.

    ``mask_scale`` is the number of image pixels per pixel of ``labeled_image``, the masks and
    ``logits`` (1 normally, ``LOGIT_SCALE`` for a no-upscale answer). ``SegmentResult.x``/``y`` and
    polygon points are always in image pixels, so they are multiplied by it, and ``width`` and
    ``height`` are the dimensions of the reduced frame. ``logits`` is a Y-down float grid in that
    same frame; when ``options.logits`` is set it is cropped to the first segment's bounds plus
    ``LOGITS_BORDER`` samples and sent as little-endian float16 with its origin and scale.
    """
    options = options if options is not None else ResponseOptions()
    if omit_labeled_image or labeled_image.size == 0:
        response = SegmentationResponse(width=width, height=height)
    else:
        response = SegmentationResponse(
            labeled_image=encode_png(labeled_image),
            width=width,
            height=height,
        )

    logits_pending = options.logits and logits is not None
    for segment in segments:
        if 'mask' in segment:
            mask_bool: NDArray[np.bool_] = fill_small_holes(segment['mask'])
            x, y, mask_width, mask_height = get_mask_bounds(mask_bool)
            if options.masks:
                if mask_width > 0 and mask_height > 0:
                    cropped_mask = mask_bool[y:y + mask_height, x:x + mask_width]
                else:
                    cropped_mask = np.zeros((0, 0), dtype=np.bool_)
                mask_bytes = encode_png(cropped_mask.astype(np.uint8) * 255)
            else:
                mask_bytes = b''
            polygons = mask_to_polygons(mask_bool) if options.polygons else []
        else:
            mask_bytes = b''
            x, y, mask_width, mask_height = (
                segment.get('x', 0),
                segment.get('y', 0),
                segment.get('width', 0),
                segment.get('height', 0),
            )
            polygons = []

        segment_result = SegmentResult(
            index=segment['index'],
            score=segment['score'],
            mask=mask_bytes,
            x=x * mask_scale,
            y=y * mask_scale,
        )
        for shape in polygons:
            polygon = Polygon()
            for point in shape.outer:
                polygon.points.append(Point(x=int(point[0]) * mask_scale, y=int(point[1]) * mask_scale))
            for hole in shape.holes:
                hole_ring = polygon.holes.add()
                for point in hole:
                    hole_ring.points.append(Point(x=int(point[0]) * mask_scale, y=int(point[1]) * mask_scale))
            segment_result.polygons.append(polygon)
        if logits_pending and mask_width > 0 and mask_height > 0:
            _attach_logits(segment_result, logits, x, y, mask_width, mask_height, mask_scale)
            logits_pending = False
        response.segments.append(segment_result)

    return response


def _attach_logits(
    segment_result: SegmentResult,
    logits: NDArray,
    x: int,
    y: int,
    mask_width: int,
    mask_height: int,
    mask_scale: int,
) -> None:
    """Crop ``logits`` to a mask's bounds plus a border and store it on ``segment_result`` as float16.

    ``x``, ``y``, ``mask_width`` and ``mask_height`` are the mask's bounds in the same (reduced)
    frame as ``logits``; ``mask_scale`` converts that frame to image pixels.
    """
    rows, cols = logits.shape
    left = max(0, x - LOGITS_BORDER)
    top = max(0, y - LOGITS_BORDER)
    right = min(cols, x + mask_width + LOGITS_BORDER)
    bottom = min(rows, y + mask_height + LOGITS_BORDER)
    crop = np.ascontiguousarray(logits[top:bottom, left:right], dtype='<f2')
    segment_result.logits = crop.tobytes()
    segment_result.logits_width = int(crop.shape[1])
    segment_result.logits_height = int(crop.shape[0])
    segment_result.logits_scale = float(mask_scale)
    segment_result.logits_x = left * mask_scale
    segment_result.logits_y = top * mask_scale
