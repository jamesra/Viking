"""Encode a finished segmentation as the gRPC response: labeled image, cropped masks, polygons.

This is CPU work that grows with the mask (hole filling, bounds, PNG encoding, contour tracing),
kept apart from the servicer so it can be tested on its own and run on a worker thread.
"""

from __future__ import annotations

from typing import List

import numpy as np
from numpy.typing import NDArray

from segmentation_grpc import Point, Polygon, SegmentationResponse, SegmentResult
from segmentation_server.mask_utils import (
    SegmentInfo,
    encode_png,
    fill_small_holes,
    get_mask_bounds,
    mask_to_polygons,
)


def build_segmentation_response(
    labeled_image: NDArray[np.uint16],
    segments: List[SegmentInfo],
    width: int,
    height: int,
    omit_labeled_image: bool = False,
) -> SegmentationResponse:
    """Encode a labeled PNG (unless omitted) plus each segment's cropped mask and polygons.

    Holes smaller than 3% of a mask's area are filled before it is cropped and traced. Every
    polygon carries the rings of its remaining holes.
    """
    if omit_labeled_image or labeled_image.size == 0:
        response = SegmentationResponse(width=width, height=height)
    else:
        response = SegmentationResponse(
            labeled_image=encode_png(labeled_image),
            width=width,
            height=height,
        )

    for segment in segments:
        if 'mask' in segment:
            mask_bool: NDArray[np.bool_] = fill_small_holes(segment['mask'])
            x, y, mask_width, mask_height = get_mask_bounds(mask_bool)
            if mask_width > 0 and mask_height > 0:
                cropped_mask = mask_bool[y:y + mask_height, x:x + mask_width]
            else:
                cropped_mask = np.zeros((0, 0), dtype=np.bool_)
            mask_bytes = encode_png(cropped_mask.astype(np.uint8) * 255)
            polygons = mask_to_polygons(mask_bool)
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
            x=x,
            y=y,
        )
        for shape in polygons:
            polygon = Polygon()
            for point in shape.outer:
                polygon.points.append(Point(x=int(point[0]), y=int(point[1])))
            for hole in shape.holes:
                hole_ring = polygon.holes.add()
                for point in hole:
                    hole_ring.points.append(Point(x=int(point[0]), y=int(point[1])))
            segment_result.polygons.append(polygon)
        response.segments.append(segment_result)

    return response
