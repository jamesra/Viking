"""A finished walk as a SegmentResult: native-resolution logits, cropped, and placed by logits_origin."""

from __future__ import annotations

import numpy as np

from segmentation_server.cell_grid import UNSEEN_LOGIT
from segmentation_server.mask_utils import fill_small_holes, get_mask_bounds
from segmentation_server.responses import LOGITS_BORDER, build_island_result, logits_from_mask, logits_origin


def _old_placement(fused: np.ndarray, origin_x: int, origin_y: int, iso: float, scale: int = 4):
    """What the previous protocol sent for the same walk with LOGITS | NO_UPSCALE (responses.py before this change):
    the response's origin and frame height, and the segment's Y-down crop corner in image pixels of that frame."""
    frame = fill_small_holes(fused > iso)
    x, y, _width, _height = get_mask_bounds(frame)
    left, top = max(0, x - LOGITS_BORDER), max(0, y - LOGITS_BORDER)
    return dict(origin_x=origin_x, origin_y=origin_y, height=fused.shape[0], logits_x=left * scale, logits_y=top * scale)


def test_logits_origin_is_the_old_placement_converted_to_one_y_up_pixel() -> None:
    """A walk whose cores span rows 3..5 and columns -2..0: origin (-768, 1792), a 384 x 384 sample grid."""
    origin_x, origin_y = -2 * 512 + 256, 3 * 512 + 256
    fused = np.full((384, 384), UNSEEN_LOGIT, dtype=np.float32)
    fused[40:90, 210:300] = 4.0  # rows 40..89 from the top, columns 210..299
    fused[60, 250] = 9.0  # one marked sample, to follow through the conversion

    segment = build_island_result(2, 0.5, np.zeros((0, 0), dtype=bool), fused, origin_x, origin_y, iso=1.0)

    old = _old_placement(fused, origin_x, origin_y, 1.0)
    assert (segment.logits_origin.x, segment.logits_origin.y) == (
        old["origin_x"] + old["logits_x"],
        old["origin_y"] + old["height"] * 4 - 1 - old["logits_y"],
    )
    # The same numbers by hand: the crop starts two samples before the object.
    assert (segment.logits_origin.x, segment.logits_origin.y) == (-768 + 208 * 4, 1792 + 384 * 4 - 1 - 38 * 4)
    assert segment.island == 2 and segment.logits_scale == 4.0
    assert (segment.logits_size.width, segment.logits_size.height) == (90 + 2 * LOGITS_BORDER, 50 + 2 * LOGITS_BORDER)

    # The marked sample, grid row 60 and column 250, covers mosaic x 232..235 and y 1792 + 1536 - 244 .. -241.
    crop = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_size.height, segment.logits_size.width)
    j, i = np.unravel_index(int(np.argmax(crop)), crop.shape)
    ox, oy, s = segment.logits_origin.x, segment.logits_origin.y, 4
    xs = range(ox + i * s, ox + (i + 1) * s)
    ys = range(oy - (j + 1) * s + 1, oy - j * s + 1)
    assert list(xs) == [-768 + 250 * 4 + k for k in range(4)]
    assert list(ys) == [origin_y + 384 * 4 - (60 + 1) * 4 + k for k in range(4)]


def test_the_conversion_function_alone() -> None:
    assert logits_origin(256, 256, 128, 0, 0) == (256, 256 + 512 - 1)
    assert logits_origin(256, 256, 128, 10, 20) == (296, 767 - 80)


def test_an_island_that_found_nothing_has_no_result() -> None:
    fused = np.full((128, 128), UNSEEN_LOGIT, dtype=np.float32)

    assert build_island_result(1, 0.0, np.zeros((512, 512), dtype=bool), fused, 256, 256, iso=1.0) is None
    assert build_island_result(1, 0.0, np.zeros((0, 0), dtype=bool), None, 0, 0, iso=1.0) is None


def test_a_walk_without_logits_sends_its_mask_at_the_logit_grid() -> None:
    mask = np.zeros((512, 512), dtype=bool)
    mask[100:200, 40:120] = True

    logits = logits_from_mask(mask, iso=1.0)
    segment = build_island_result(1, 0.8, mask, None, 256, 256, iso=1.0)

    assert logits.shape == (128, 128) and int((logits > 1.0).sum()) == 25 * 20
    crop = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_size.height, segment.logits_size.width)
    assert int((crop > 1.0).sum()) == 25 * 20
    assert segment.logits_origin.x == 256 + (10 - LOGITS_BORDER) * 4
    assert segment.logits_origin.y == 256 + 512 - 1 - (25 - LOGITS_BORDER) * 4
