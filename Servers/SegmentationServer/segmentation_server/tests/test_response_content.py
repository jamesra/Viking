"""Response content flags: what a client asks for is what the response holds, and no-upscale stays small."""

from __future__ import annotations

import cv2
import numpy as np
import pytest

from segmentation_grpc.segmentation_pb2 import (
    RESPONSE_CONTENT_LOGITS,
    RESPONSE_CONTENT_MASKS,
    RESPONSE_CONTENT_NO_UPSCALE,
    RESPONSE_CONTENT_POLYGONS,
)
from segmentation_server.cell_grid import LOGIT_SCALE
from segmentation_server.mask_utils import combined_mask_to_segments
from segmentation_server.responses import (
    LOGITS_BORDER,
    ResponseOptions,
    build_segmentation_response,
    choose_frame,
)

ALL = RESPONSE_CONTENT_MASKS | RESPONSE_CONTENT_POLYGONS | RESPONSE_CONTENT_LOGITS


def _square(size: int = 40, at: int = 10, total: int = 64) -> np.ndarray:
    mask = np.zeros((total, total), dtype=np.bool_)
    mask[at:at + size, at:at + size] = True
    return mask


def _build(mask: np.ndarray, options: ResponseOptions, scale: int = 1, logits=None):
    labeled, segments = combined_mask_to_segments(mask, 0.9, empty_shape=mask.shape)
    return build_segmentation_response(
        labeled, segments, mask.shape[1], mask.shape[0], options=options, mask_scale=scale, logits=logits
    )


def test_zero_flags_keep_the_answer_clients_got_before_the_flags_existed() -> None:
    assert ResponseOptions.from_flags(0) == ResponseOptions(masks=True, polygons=True, logits=False, no_upscale=False)


@pytest.mark.parametrize(
    ("flags", "expected"),
    [
        (RESPONSE_CONTENT_MASKS, ResponseOptions(True, False, False, False)),
        (RESPONSE_CONTENT_POLYGONS, ResponseOptions(False, True, False, False)),
        (RESPONSE_CONTENT_LOGITS, ResponseOptions(False, False, True, False)),
        (RESPONSE_CONTENT_LOGITS | RESPONSE_CONTENT_NO_UPSCALE, ResponseOptions(False, False, True, True)),
        (ALL | RESPONSE_CONTENT_NO_UPSCALE, ResponseOptions(True, True, True, True)),
    ],
)
def test_flags_decode_to_the_payloads_they_name(flags: int, expected: ResponseOptions) -> None:
    assert ResponseOptions.from_flags(flags) == expected


def test_unknown_bits_and_a_modifier_that_asks_for_nothing_are_refused() -> None:
    with pytest.raises(ValueError, match="Unknown"):
        ResponseOptions.from_flags(16)
    with pytest.raises(ValueError, match="NO_UPSCALE"):
        ResponseOptions.from_flags(RESPONSE_CONTENT_NO_UPSCALE)


def test_a_payload_that_was_not_asked_for_is_left_empty() -> None:
    mask = _square()

    masks_only = _build(mask, ResponseOptions(True, False, False)).segments[0]
    polygons_only = _build(mask, ResponseOptions(False, True, False)).segments[0]

    assert masks_only.mask and len(masks_only.polygons) == 0
    assert not polygons_only.mask and len(polygons_only.polygons) == 1
    assert not masks_only.logits and not polygons_only.logits


def test_logits_are_cropped_to_the_mask_plus_a_border_and_round_trip_as_float16() -> None:
    mask = _square(size=20, at=20, total=64)
    logits = np.linspace(-8, 8, 64 * 64, dtype=np.float32).reshape(64, 64)

    segment = _build(mask, ResponseOptions(False, False, True), logits=logits).segments[0]

    border = LOGITS_BORDER
    assert (segment.logits_x, segment.logits_y) == (20 - border, 20 - border)
    assert (segment.logits_width, segment.logits_height) == (20 + 2 * border, 20 + 2 * border)
    assert segment.logits_scale == 1.0
    decoded = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_height, segment.logits_width)
    expected = logits[20 - border:20 + 20 + border, 20 - border:20 + 20 + border]
    assert np.allclose(decoded.astype(np.float32), expected, atol=0.01)


def test_logits_are_not_sent_unless_asked_for() -> None:
    mask = _square()

    segment = _build(mask, ResponseOptions(True, True, False), logits=np.zeros((64, 64), np.float32)).segments[0]

    assert not segment.logits and segment.logits_width == 0


def test_a_reduced_frame_reports_positions_and_polygons_in_image_pixels() -> None:
    mask = _square(size=10, at=5, total=32)
    logits = np.zeros((32, 32), dtype=np.float32)

    segment = _build(mask, ResponseOptions(True, True, True, True), scale=LOGIT_SCALE, logits=logits).segments[0]

    assert (segment.x, segment.y) == (5 * LOGIT_SCALE, 5 * LOGIT_SCALE)
    assert segment.logits_scale == float(LOGIT_SCALE)
    assert segment.logits_x == (5 - LOGITS_BORDER) * LOGIT_SCALE
    xs = [point.x for point in segment.polygons[0].points]
    assert max(xs) <= 32 * LOGIT_SCALE and all(x % LOGIT_SCALE == 0 for x in xs)


def test_no_upscale_gives_the_native_grid_and_the_client_scale() -> None:
    mask = np.zeros((16 * LOGIT_SCALE, 16 * LOGIT_SCALE), dtype=np.bool_)
    logits = np.full((16, 16), -5.0, dtype=np.float32)
    logits[4:10, 4:10] = 5.0
    options = ResponseOptions(True, False, True, True)

    frame_mask, frame_logits, scale = choose_frame(mask, logits, options, iso=1.0, logit_scale=LOGIT_SCALE)

    assert scale == LOGIT_SCALE
    assert frame_mask.shape == logits.shape and int(frame_mask.sum()) == 36
    assert frame_logits is logits


def test_without_no_upscale_the_logits_are_upsampled_to_the_mask_frame() -> None:
    mask = np.zeros((16 * LOGIT_SCALE, 16 * LOGIT_SCALE), dtype=np.bool_)
    logits = np.arange(16 * 16, dtype=np.float32).reshape(16, 16)

    frame_mask, frame_logits, scale = choose_frame(
        mask, logits, ResponseOptions(True, True, True, False), iso=1.0, logit_scale=LOGIT_SCALE
    )

    assert scale == 1 and frame_mask is mask
    assert frame_logits.shape == mask.shape
    assert np.allclose(frame_logits, cv2.resize(logits, mask.shape[::-1], interpolation=cv2.INTER_LINEAR))


def test_default_options_leave_the_frame_alone_and_no_upscale_without_logits_falls_back() -> None:
    mask = _square()
    options = ResponseOptions(True, True, True, True)

    assert choose_frame(mask, np.zeros((16, 16), np.float32), ResponseOptions(), 1.0, LOGIT_SCALE)[1:] == (None, 1)
    fallback = choose_frame(mask, None, options, 1.0, LOGIT_SCALE)
    assert fallback[0] is mask and fallback[1] is None and fallback[2] == 1
