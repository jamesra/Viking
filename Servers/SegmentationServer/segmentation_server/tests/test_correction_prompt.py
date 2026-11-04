"""Outlines to correct (``correction_feature``) and the one click a mask prompt is paired with, as the model was trained.

The trainer pairs every mask prompt with exactly one click, at the mask's deepest pixel, and gives an existing outline
as a mask to correct (``CORRECTION_LOGIT``), never as a seed. These tests hold the server to that.
"""

from __future__ import annotations

from typing import List

import grpc
import numpy as np
import pytest

from protocol import (
    cache as _cache,
    circle,
    context as _context,
    converse,
    polygon,
    put_tile,
    released,
    result as _result,
    servicer as _servicer,
    start,
)
from segmentation_grpc import Feature
from segmentation_server import trainer_prompt_encoding as enc
from segmentation_server.cell_grid import LOGIT_SCALE
from segmentation_server.mask_tiles import TiledMaskShape
from segmentation_server.result_store import PROMPT_SCHEME, prompt_hash
from test_shape_prompts_stream import _DiscModel, _object_pixels, _v6

# A 400 px square (pixels 100..499) with a thin tail to the right: its centroid (x 350) is not its deepest point
# (x 299.5), so the two rules for "the click" give different pixels.
SQUARE_WITH_TAIL = [(100, 100), (500, 100), (500, 280), (1000, 280), (1000, 320), (500, 320), (500, 500), (100, 500)]


def _window(x: float, y: float) -> tuple:
    """Window (0, 0) is mosaic 0..1023 both ways: mosaic (x, y) is window (x, 1023 - y)."""
    return int(x), int(1023 - y)


def _start_points(model: _DiscModel) -> List[tuple]:
    return list(model.calls[0]["points"])


def _value_at(prompt: np.ndarray, x: int, y: int) -> float:
    col, row = x // LOGIT_SCALE, (1023 - y) // LOGIT_SCALE
    return float(prompt[row, col])


def test_the_deepest_point_is_the_pixel_farthest_from_the_edge_not_the_one_nearest_the_centroid() -> None:
    shape = TiledMaskShape(complete=True)
    pixels = np.zeros((1024, 1024), dtype=bool)
    pixels[100:500, 100:500] = True  # Y-up rows: y 100..499
    pixels[280:320, 500:1000] = True

    shape.settle((0, 0), pixels)

    x, y = shape.deepest_point()
    assert abs(x - 299.5) <= 0.5 and abs(y - 299.5) <= 0.5
    assert shape.interior_point()[0] > 340, "the centroid rule lands elsewhere, which is why it is not used"


@pytest.mark.asyncio
async def test_a_polygon_without_a_click_gets_one_click_at_its_deepest_pixel() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)

    await converse(servicer, start(shapes=[polygon(SQUARE_WITH_TAIL)], model_id="v4"))

    points = _start_points(model)
    assert len(points) == 1, "one click, as the trainer pairs a mask prompt with one"
    x, y = points[0]
    assert abs(x - 299.5) <= 1 and abs((1023 - y) - 299.5) <= 1


@pytest.mark.asyncio
async def test_a_polygon_with_the_clients_click_reaches_the_model_with_that_click_alone() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)

    await converse(servicer, start([(250, 250)], shapes=[polygon(SQUARE_WITH_TAIL)], model_id="v4"))

    assert _start_points(model) == [_window(250, 250)], "no centre click is added beside the client's"


@pytest.mark.asyncio
async def test_a_circle_reaches_the_model_with_one_click() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)

    await converse(servicer, start([(500, 500)], shapes=[circle(500, 500, 80)], model_id="v4"))

    assert _start_points(model) == [_window(500, 500)]


@pytest.mark.asyncio
async def test_an_outline_to_correct_is_encoded_as_a_correction_not_a_seed() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)
    outline = Feature(shapes=[polygon(SQUARE_WITH_TAIL)])

    await converse(servicer, start(correction_feature=outline, model_id="v4"))

    prompt = model.calls[0]["mask_logits"]
    assert prompt is not None
    assert _value_at(prompt, 300, 300) == pytest.approx(enc.CORRECTION_LOGIT, abs=0.2)
    assert len(_start_points(model)) == 1


@pytest.mark.asyncio
async def test_an_outline_to_correct_is_not_taken_as_object() -> None:
    """The model answers a disc of radius 60 around the click; a seed's pixels would be pinned into the answer, an
    outline to correct's are not."""
    outline = polygon(SQUARE_WITH_TAIL)

    async def answer(**fields) -> np.ndarray:
        cache = _cache()
        await put_tile(cache, 0, 0)
        updates = await converse(_servicer(cache, model=_DiscModel(_v6())), start(model_id="v4", **fields))
        assert released(cache)
        return _object_pixels(_result(updates).segments[0])

    corrected = await answer(correction_feature=Feature(shapes=[outline]))
    seeded = await answer(shapes=[outline])

    assert corrected[:, 0].max() < 300 + 70, "only the model's disc, not the outline's tail"
    assert seeded[:, 0].max() > 900, "a seed's pixels are pinned"


@pytest.mark.asyncio
async def test_foreground_shapes_and_an_outline_to_correct_together_are_invalid_argument() -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(
        servicer,
        start(shapes=[circle(300, 300, 50)], correction_feature=Feature(shapes=[polygon(SQUARE_WITH_TAIL)])),
        context,
    )

    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and "not both" in message
    servicer.model.predict_tile.assert_not_called()


def test_an_outline_to_correct_is_part_of_the_prompt_hash_under_the_v2_scheme() -> None:
    """Rows from before outlines to correct existed (prompt-v1) never match, and a seed never shares a row with the
    same outline sent as a correction."""
    outline = polygon(SQUARE_WITH_TAIL)
    seeded = start(shapes=[outline])
    corrected = start(correction_feature=Feature(shapes=[outline]))

    assert PROMPT_SCHEME == "prompt-v2"
    assert prompt_hash(seeded, "") != prompt_hash(corrected, "")
    assert prompt_hash(corrected, "") == prompt_hash(start(correction_feature=Feature(shapes=[outline])), "")
