"""SegmentTilesStream with geometry prompts: islands, background shapes asked for by cell, and what is refused."""

from __future__ import annotations

import asyncio
from typing import List
from unittest.mock import MagicMock

import grpc
import numpy as np
import pytest

from protocol import (
    background_cells,
    cache as _cache,
    circle,
    context as _context,
    converse,
    grid,
    png as _png,
    polygon,
    put_tile,
    released,
    result as _result,
    servicer as _servicer,
    start,
)
from segmentation_grpc import (
    SegmentTilesStreamRequest,
    Shape,
    TilesAnswer,
    TilesNeeded,
    UploadTileRequest,
    Vector2,
)
from segmentation_server.cell_grid import CELL_SIZE
from segmentation_server.model_manifest import parse_manifest
from test_model_manifest import v4_manifest


class _DiscModel:
    """Answers each window with a disc of radius 60 around the first click (window-local, Y down), no logits.

    Records every call's points and mask prompt, so a test can see which island a prediction was for.
    """

    def __init__(self, manifest=None) -> None:
        if manifest is not None:
            self.manifest = manifest
        self.calls: List[dict] = []

    def _answer(self, points, mask_logits):
        self.calls.append({"points": list(points), "mask_logits": mask_logits})
        yy, xx = np.mgrid[0:CELL_SIZE, 0:CELL_SIZE]
        x, y = points[0]
        return (xx - x) ** 2 + (yy - y) ** 2 <= 60 ** 2, None, 0.9

    def predict_tile(self, _predictor, points, _labels, *_args, mask_logits=None, **_kwargs):
        return self._answer(points, mask_logits)

    def predict_ephemeral(self, _window, points, _labels, *_args, mask_logits=None, **_kwargs):
        return self._answer(points, mask_logits)

    def create_initialized_predictor(self, *_args):
        return object()

    def release_predictor(self, _predictor) -> None:
        pass


def _v6():
    return parse_manifest(v4_manifest(), source="test")


def _object_pixels(segment) -> np.ndarray:
    """The mosaic ``(x, y)`` of every sample of a result above the default threshold (the sample's top-left pixel)."""
    logits = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_size.height, segment.logits_size.width)
    rows, cols = np.nonzero(logits > 1.0)
    scale = int(segment.logits_scale)
    return np.stack([segment.logits_origin.x + cols * scale, segment.logits_origin.y - rows * scale], axis=1)


@pytest.mark.asyncio
async def test_one_disc_gives_one_result_for_island_one() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)

    response = _result(await converse(servicer, start(shapes=[circle(500, 500, 80)], model_id="v4")))

    assert [segment.island for segment in response.segments] == [1]
    centre = _object_pixels(response.segments[0]).mean(axis=0)
    assert np.allclose(centre, (500, 500), atol=6)
    assert model.calls[0]["mask_logits"] is not None and model.calls[0]["mask_logits"].max() > 5.0, (
        "the softened circle is the start window's mask prompt"
    )
    assert released(cache)


@pytest.mark.asyncio
async def test_two_separate_discs_give_two_results_numbered_by_area() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)
    small, large = circle(700, 330, 40), circle(400, 600, 90)

    response = _result(await converse(servicer, start(shapes=[small, large], model_id="v4")))

    assert [segment.island for segment in response.segments] == [1, 2]
    first, second = (_object_pixels(segment).mean(axis=0) for segment in response.segments)
    assert np.allclose(first, (400, 600), atol=6), "island 1 is the larger disc"
    assert np.allclose(second, (700, 330), atol=6)
    assert released(cache)


@pytest.mark.asyncio
async def test_a_click_belongs_to_the_island_it_lies_in() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    model = _DiscModel(_v6())
    servicer = _servicer(cache, model=model)
    small, large = circle(700, 330, 40), circle(400, 600, 90)

    await converse(servicer, start([(705, 320)], shapes=[small, large], model_id="v4"))

    # Window (0, 0) is mosaic 0..1023 both ways: mosaic (x, y) is window (x, 1023 - y).
    first_points = [call["points"] for call in model.calls]
    assert any((705, 1023 - 320) in points for points in first_points), "the click went with its own island"
    assert not any((705, 1023 - 320) in points and (400, 1023 - 600) in points for points in first_points)


@pytest.mark.asyncio
async def test_a_shape_point_in_a_prompt_is_invalid_argument() -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(servicer, start(shapes=[Shape(point=Vector2(x=5, y=5))]), context)

    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and "Shape.point" in message
    servicer.model.predict_tile.assert_not_called()


@pytest.mark.asyncio
async def test_a_background_shape_point_in_an_answer_is_invalid_argument() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await converse(
        servicer, start([(10, 10)]), context,
        answer_for=lambda needed: TilesAnswer(unavailable=list(needed.tiles), background_shapes=[Shape(point=Vector2())]),
    )

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and "Shape.point" in message
    assert released(cache)


@pytest.mark.asyncio
async def test_background_shapes_are_asked_for_by_cell_and_used_once_answered() -> None:
    """The click's window reaches three cells beyond the request's tile. The server asks for their background with
    the image tiles, the client answers with a neighbour's polygon, and the prediction there is told about it and
    the result leaves it out. Cells already answered are not asked again."""
    cache = _cache()
    await put_tile(cache, 0, 0, _png())
    model = MagicMock(spec=["predict_tile", "predict_ephemeral", "create_initialized_predictor", "release_predictor"])
    model.predict_ephemeral.return_value = (np.ones((CELL_SIZE, CELL_SIZE), dtype=bool), None, 0.9)
    model.predict_tile.return_value = (np.ones((CELL_SIZE, CELL_SIZE), dtype=bool), None, 0.9)
    servicer = _servicer(cache, model=model)
    png = _png()
    neighbour = polygon([(-300, -300), (-100, -300), (-100, -100), (-300, -100)])
    answered: List[TilesNeeded] = []

    async def client(needed: TilesNeeded) -> TilesAnswer:
        answered.append(needed)
        for asked in needed.tiles:
            await servicer.UploadTile(UploadTileRequest(grid=grid(), cell=asked, image_data=png), _context())
        shapes = [neighbour] if any((c.row, c.col) == (-1, -1) for c in needed.background_cells) else []
        return TilesAnswer(background_shapes=shapes)

    updates = await converse(servicer, start([(10, 10)]), answer_for=client)

    asked = background_cells(updates)
    assert asked[0] == {(-1, -1), (-1, 0), (0, -1)}
    flattened = [cell for cells in asked for cell in cells]
    assert len(flattened) == len(set(flattened)), "a cell's background is asked for once"
    assert (0, 0) not in flattened, "the request's own tile came with its background"
    # The first window predicted is cell (-1, -1), mosaic -512..511 both ways: mosaic (x, y) is window
    # (x + 512, 511 - y), so the neighbour's middle (-200, -200) is window pixel (312, 711).
    first = model.predict_ephemeral.call_args_list[0].kwargs["mask_logits"]
    assert first is not None and first[711 // 4, 312 // 4] <= -8.0, "the neighbour is negative"
    response = _result(updates)
    pixels = _object_pixels(response.segments[0])
    inside = (pixels[:, 0] >= -296) & (pixels[:, 0] <= -104) & (pixels[:, 1] >= -296) & (pixels[:, 1] <= -104)
    assert not inside.any(), "the neighbour's pixels are not object"
    assert released(cache)


@pytest.mark.asyncio
async def test_without_a_background_answer_the_window_is_not_predicted() -> None:
    """The window waits for its background: a client that uploads but never answers gets DEADLINE_EXCEEDED."""
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_))
    servicer._tile_answer_timeout = 0.3
    context = _context()
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=start([(10, 10)])))

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates = []
    async for update in servicer.SegmentTilesStream(incoming(), context):
        updates.append(update)
        for asked in update.needed.tiles:
            await servicer.UploadTile(UploadTileRequest(grid=grid(), cell=asked, image_data=_png()), _context())

    assert context.abort.await_args.args[0] == grpc.StatusCode.DEADLINE_EXCEEDED
    servicer.model.predict_ephemeral.assert_not_called()
    assert released(cache)
