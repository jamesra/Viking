"""SegmentTilesStream: the server owns the growth and asks for tiles as the mask reaches them."""

from __future__ import annotations

import asyncio
from typing import List

import grpc
import numpy as np
import pytest

from protocol import (
    VOLUME_KEY,
    all_unavailable,
    background_cells,
    cache as _cache,
    cell,
    context as _context,
    converse,
    grid,
    needed_tiles,
    png as _png,
    put_tile,
    released,
    result as _result,
    servicer as _servicer,
    start,
    uploader,
)
from segmentation_grpc import (
    CoordinateSpace,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    TilesAnswer,
    TilesNeeded,
    UploadTileRequest,
)
from segmentation_server.server import RequestLimits


@pytest.mark.asyncio
async def test_missing_tile_without_foreground_does_not_abort() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    response = _result(await converse(servicer, start([(300, 723)], tiles=[(0, 0), (0, 1)]), context))

    context.abort.assert_not_awaited()
    servicer.model.predict_tile.assert_called_once()
    assert response.segments
    assert released(cache)


async def _call_sent_to_the_model(**request_fields):
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    await converse(servicer, start([(300, 723)], **request_fields))
    return servicer.model.predict_tile.call_args


async def _threshold_sent_to_the_model(**request_fields) -> float:
    return (await _call_sent_to_the_model(**request_fields)).args[6]


@pytest.mark.asyncio
async def test_a_lone_click_gets_no_box_and_one_mask() -> None:
    call = await _call_sent_to_the_model()

    assert call.args[5] is None
    assert call.args[3] is False, "multimask_output is always off"


async def _response_for(**request_fields):
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    return _result(await converse(servicer, start([(300, 723)], **request_fields)))


@pytest.mark.asyncio
async def test_the_response_echoes_the_client_request_id() -> None:
    response = await _response_for(request_id=987654321012)

    assert response.request_id == 987654321012


@pytest.mark.asyncio
async def test_a_request_without_an_id_is_answered_with_zero() -> None:
    response = await _response_for()

    assert response.request_id == 0


@pytest.mark.asyncio
async def test_a_client_that_sends_no_mask_threshold_gets_the_server_default() -> None:
    assert await _threshold_sent_to_the_model() == pytest.approx(1.0)


@pytest.mark.asyncio
async def test_an_explicit_zero_mask_threshold_is_honored() -> None:
    assert await _threshold_sent_to_the_model(mask_threshold=0.0) == 0.0


@pytest.mark.asyncio
async def test_a_sent_mask_threshold_is_forwarded() -> None:
    assert await _threshold_sent_to_the_model(mask_threshold=-1.5) == pytest.approx(-1.5)


@pytest.mark.asyncio
async def test_click_near_a_tile_corner_asks_for_the_neighbor_tiles_and_their_background() -> None:
    """A click at (10, 10) is owned by an offset cell that needs tiles (-1,-1), (-1,0), (0,-1) and (0,0).

    The request's own tile has its background already; the other three are asked for in the same round.
    """
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await converse(servicer, start([(10, 10)]), context)

    context.abort.assert_not_awaited()
    servicer.model.predict_tile.assert_not_called()
    servicer.model.predict_ephemeral.assert_not_called()
    assert {(-1, -1), (-1, 0), (0, -1)} <= needed_tiles(updates)[0]
    assert background_cells(updates)[0] == {(-1, -1), (-1, 0), (0, -1)}
    assert _result(updates) is not None


@pytest.mark.asyncio
async def test_uploaded_tiles_let_the_same_walk_continue_and_finish() -> None:
    """The server asks once, the client uploads the tiles, and the walk predicts without starting over."""
    cache = _cache()
    png = _png()
    await put_tile(cache, 0, 0, png)
    servicer = _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_))
    context = _context()

    updates = await converse(servicer, start([(10, 10)]), context, answer_for=uploader(servicer, png))

    context.abort.assert_not_awaited()
    assert needed_tiles(updates)[0] >= {(-1, -1), (-1, 0), (0, -1)}
    assert servicer.model.predict_ephemeral.call_count >= 1
    assert _result(updates).segments
    assert released(cache)


@pytest.mark.asyncio
async def test_a_tile_is_settled_by_its_upload_alone_before_the_answer_arrives() -> None:
    """The client uploads, then answers only once the server has used the uploads: no tile is ever listed as ready.

    The client runs beside the stream, as a real one does, so the server can act on each upload as it lands. The click
    is in the aligned cell over tile (0, 1), which the request names (so its background is known) but the server lacks.
    """
    cache = _cache()
    png = _png()
    await put_tile(cache, 0, 0, png)
    servicer = _servicer(cache)
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=start([(1324, 723)], tiles=[(0, 0), (0, 1)])))
    predicted_before_answer: List[int] = []

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    async def client(needed: TilesNeeded) -> None:
        for asked in needed.tiles:
            await servicer.UploadTile(UploadTileRequest(grid=grid(), cell=asked, image_data=png), _context())
        for _ in range(200):
            if servicer.model.predict_tile.call_count:
                break
            await asyncio.sleep(0.01)
        predicted_before_answer.append(servicer.model.predict_tile.call_count)
        await inbox.put(SegmentTilesStreamRequest(answer=TilesAnswer()))

    updates: List[SegmentTilesStreamResponse] = []
    clients = []
    async for update in servicer.SegmentTilesStream(incoming(), _context()):
        updates.append(update)
        if update.WhichOneof("update") == "needed":
            clients.append(asyncio.create_task(client(update.needed)))
    await asyncio.gather(*clients)

    assert needed_tiles(updates)[0] == {(0, 1)}
    assert predicted_before_answer[0] >= 1, "the walk waited for the answer instead of the upload"
    assert _result(updates).segments
    assert released(cache)


@pytest.mark.asyncio
async def test_a_tile_listed_unavailable_is_settled_and_growth_does_not_enter_it() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await converse(servicer, start([(10, 10)]), context, answer_for=all_unavailable)

    context.abort.assert_not_awaited()
    servicer.model.predict_ephemeral.assert_not_called()
    assert _result(updates) is not None
    assert released(cache)


@pytest.mark.asyncio
async def test_tiles_the_server_already_holds_are_not_asked_of_the_client() -> None:
    """Tiles another user or an earlier session left in the image cache are pinned at once: no TilesNeeded names them."""
    cache = _cache()
    png = _png()
    held = {(0, 0), (-1, -1), (-1, 0), (0, -1)}
    for row, col in held:
        await put_tile(cache, row, col, png)
    servicer = _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_))
    context = _context()

    updates = await converse(servicer, start([(10, 10)]), context)

    context.abort.assert_not_awaited()
    asked = {tile for tiles in needed_tiles(updates) for tile in tiles}
    assert not asked & held
    assert servicer.model.predict_ephemeral.call_count >= 1
    assert _result(updates).segments
    assert released(cache)


@pytest.mark.asyncio
async def test_a_tile_asked_for_once_is_not_asked_for_again() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)

    updates = await converse(servicer, start([(10, 10)]))

    asked = [tile for tiles in needed_tiles(updates) for tile in tiles]
    assert len(asked) == len(set(asked))


@pytest.mark.asyncio
async def test_a_client_that_ends_its_stream_while_tiles_are_awaited_gets_no_result() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)

    updates = await converse(servicer, start([(10, 10)]), answer_for=lambda _needed: None)

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert released(cache)


@pytest.mark.asyncio
async def test_a_stream_must_begin_with_start() -> None:
    servicer = _servicer(_cache())
    context = _context()

    async def incoming():
        yield SegmentTilesStreamRequest(answer=TilesAnswer())

    updates = [update async for update in servicer.SegmentTilesStream(incoming(), context)]

    assert updates == []
    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_a_message_other_than_an_answer_while_tiles_are_awaited_is_rejected() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = start([(10, 10)])

    async def incoming():
        yield SegmentTilesStreamRequest(start=request)
        yield SegmentTilesStreamRequest(start=request)

    updates = [update async for update in servicer.SegmentTilesStream(incoming(), context)]

    assert all(update.WhichOneof("update") == "needed" for update in updates)
    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert released(cache)


@pytest.mark.asyncio
async def test_an_answer_with_no_tiles_needed_outstanding_is_rejected() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = start([(10, 10)])

    async def incoming():
        yield SegmentTilesStreamRequest(start=request)
        yield SegmentTilesStreamRequest(answer=TilesAnswer())
        yield SegmentTilesStreamRequest(answer=TilesAnswer())
        await asyncio.sleep(5)

    updates = await asyncio.wait_for(_collect(servicer.SegmentTilesStream(incoming(), context)), timeout=10)

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and "no TilesNeeded" in message


@pytest.mark.asyncio
async def test_a_start_tile_the_server_lacks_is_asked_for_instead_of_failing_the_call() -> None:
    """A client names the cells under its clicks without uploading them; the server asks for the ones it lacks."""
    cache = _cache()
    await put_tile(cache, 0, 0, b"other")
    servicer = _servicer(cache)
    context = _context()

    updates = await converse(servicer, start([(1034, 10)], tiles=[(0, 0), (0, 1)]), context)

    context.abort.assert_not_awaited()
    assert (0, 1) in needed_tiles(updates)[0]
    assert (0, 0) not in needed_tiles(updates)[0]
    assert _result(updates) is not None
    assert released(cache)


@pytest.mark.asyncio
async def test_a_client_that_goes_quiet_after_tiles_are_requested_times_out_and_releases_its_pins() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._tile_answer_timeout = 0.2
    context = _context()
    request = start([(10, 10)])

    async def incoming():
        yield SegmentTilesStreamRequest(start=request)
        await asyncio.sleep(30)

    updates = await asyncio.wait_for(_collect(servicer.SegmentTilesStream(incoming(), context)), timeout=10)

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert context.abort.await_args.args[0] == grpc.StatusCode.DEADLINE_EXCEEDED
    assert released(cache)
    assert servicer._load.snapshot()[0] == 0


async def _collect(stream) -> List[SegmentTilesStreamResponse]:
    return [update async for update in stream]


def test_the_answer_timeout_comes_from_the_environment(monkeypatch) -> None:
    from segmentation_server.server import tile_answer_timeout_seconds

    monkeypatch.delenv("SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS", raising=False)
    assert tile_answer_timeout_seconds() == 60.0
    monkeypatch.setenv("SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS", "5")
    assert tile_answer_timeout_seconds() == 5.0
    monkeypatch.setenv("SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS", "soon")
    assert tile_answer_timeout_seconds() == 60.0


@pytest.mark.asyncio
async def test_a_client_cancel_ends_the_stream_with_no_result_and_no_status(monkeypatch) -> None:
    from segmentation_server import server as server_module
    from segmentation_server.tile_growth import GrowthCancelled, GrowthWalk

    class CancelledWalk(GrowthWalk):
        def advance(self) -> None:
            raise GrowthCancelled()

    monkeypatch.setattr(server_module, "GrowthWalk", CancelledWalk)
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()

    updates = await converse(servicer, start([(300, 723)]), context)

    assert updates == []
    context.abort.assert_not_awaited()
    assert released(cache)
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_no_matching_mask_for_the_starting_cell_fails_the_stream_with_failed_precondition() -> None:
    from segmentation_server.mask_utils import NoMatchingMask

    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    servicer.model.predict_tile.side_effect = NoMatchingMask("None of the 3 candidate mask(s) covers any ...")
    context = _context()

    updates = await converse(servicer, start([(300, 723)]), context)

    # The start window's tile and background came with the request, so nothing is asked before the prediction.
    assert updates == []
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.FAILED_PRECONDITION
    assert message.startswith("NO_MATCHING_MASK ")
    assert released(cache)
    assert servicer._load.snapshot()[0] == 0


def _limited(**limits):
    servicer = _servicer(_cache())
    servicer._limits = RequestLimits(**limits)
    return servicer


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "limits,request_fields,named",
    [
        ({"max_tiles": 2}, dict(points=[(1, 1)], tiles=[(0, 0), (0, 1), (1, 0)]), "SEGMENTATION_MAX_TILES"),
        ({"max_points": 1}, dict(points=[(1, 1), (2, 2)]), "SEGMENTATION_MAX_POINTS"),
        ({"max_points": 1}, dict(points=[(1, 1)], background_points=[(9, 9), (8, 8)]), "SEGMENTATION_MAX_POINTS"),
        ({"max_mask_tiles": 1}, dict(points=[(1, 1)], mask_tile_count=2), "SEGMENTATION_MAX_MASK_TILES"),
    ],
    ids=["tiles", "foreground-points", "background-points", "mask-tiles"],
)
async def test_a_request_over_a_size_limit_is_resource_exhausted_before_any_work(limits, request_fields, named) -> None:
    servicer = _limited(**limits)
    context = _context()

    updates = await converse(servicer, start(**request_fields), context)

    assert updates == []
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert named in message
    servicer.model.predict_tile.assert_not_called()
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_more_shapes_than_the_limit_is_resource_exhausted() -> None:
    from protocol import circle

    servicer = _limited(max_shapes=2)
    context = _context()

    await converse(servicer, start(shapes=[circle(100, 100, 10)] * 3), context)

    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.RESOURCE_EXHAUSTED and "SEGMENTATION_MAX_SHAPES" in message


@pytest.mark.asyncio
async def test_a_request_at_the_limit_is_served() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._limits = RequestLimits(max_tiles=1, max_points=1, max_shapes=1)

    assert _result(await converse(servicer, start([(300, 723)]))) is not None


@pytest.mark.asyncio
async def test_an_answer_listing_more_tiles_than_the_limit_is_resource_exhausted() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._limits = RequestLimits(max_answer_tiles=2)
    context = _context()

    updates = await converse(
        servicer,
        start([(10, 10)]),
        context,
        answer_for=lambda _needed: TilesAnswer(unavailable=[cell(7, c) for c in range(5)]),
    )

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_ANSWER_TILES" in context.abort.await_args.args[1]
    assert released(cache)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "grid_fields,message",
    [
        (dict(space=CoordinateSpace.COORDINATE_SPACE_UNSPECIFIED), "MOSAIC or VOLUME"),
        (dict(downsample=0), "positive downsample"),
    ],
    ids=["unspecified-space", "zero-downsample"],
)
async def test_a_grid_without_a_space_or_a_downsample_is_invalid_argument(grid_fields, message) -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(servicer, start([(1, 1)], grid=grid(**grid_fields)), context)

    code, text = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and message in text


@pytest.mark.asyncio
async def test_a_request_without_tiles_is_invalid_argument() -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(servicer, start([(1, 1)], tiles=[]), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_a_request_with_nothing_in_the_foreground_is_invalid_argument() -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(servicer, start(background_points=[(5, 5)]), context)

    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT and "foreground" in message


@pytest.mark.asyncio
async def test_a_click_that_is_not_a_finite_number_is_invalid_argument() -> None:
    servicer = _servicer(_cache())
    context = _context()

    await converse(servicer, start([(float("nan"), 3)]), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT


def test_request_limits_come_from_the_environment(monkeypatch) -> None:
    for name in ("TILES", "POINTS", "SHAPES", "ANSWER_TILES", "MASK_TILES", "ISLANDS", "CONCURRENT_STREAMS"):
        monkeypatch.delenv(f"SEGMENTATION_MAX_{name}", raising=False)
    assert RequestLimits.from_env() == RequestLimits()
    monkeypatch.setenv("SEGMENTATION_MAX_TILES", "7")
    monkeypatch.setenv("SEGMENTATION_MAX_POINTS", "many")
    monkeypatch.setenv("SEGMENTATION_MAX_SHAPES", "0")
    monkeypatch.setenv("SEGMENTATION_MAX_ISLANDS", "3")
    limits = RequestLimits.from_env()
    assert limits.max_tiles == 7
    assert limits.max_points == RequestLimits().max_points
    assert limits.max_shapes == 1
    assert limits.max_islands == 3


def test_tile_images_decode_only_when_a_window_is_cropped_from_them(monkeypatch) -> None:
    from segmentation_server import server as server_module

    calls: list[int] = []
    real = server_module.prepare_image_for_sam2
    monkeypatch.setattr(server_module, "prepare_image_for_sam2", lambda data: calls.append(1) or real(data))
    images = server_module.TileImages()
    images.add(0, 0, _png())
    images.add(0, 1, _png())

    assert (0, 0) in images and (5, 5) not in images and len(images) == 2
    assert calls == []
    assert images.get((0, 0)).shape == (1024, 1024, 3)
    assert images[(0, 0)] is images[(0, 0)]
    assert calls == [1]
    assert images.get((9, 9)) is None
    with pytest.raises(KeyError):
        images[(9, 9)]


def test_a_tile_that_cannot_be_decoded_is_forgotten_so_growth_asks_for_it_again() -> None:
    from segmentation_server.server import TileImages

    images = TileImages()
    images.add(2, 3, b"not an image")

    assert images.get((2, 3)) is None
    assert (2, 3) not in images


@pytest.mark.asyncio
async def test_a_walk_over_aligned_cells_never_decodes_a_tile(monkeypatch) -> None:
    from segmentation_server import server as server_module

    calls: list[int] = []
    real = server_module.prepare_image_for_sam2
    monkeypatch.setattr(server_module, "prepare_image_for_sam2", lambda data: calls.append(1) or real(data))
    cache = _cache()
    await put_tile(cache, 0, 0, _png())
    servicer = _servicer(cache, mask=np.zeros((4, 4), dtype=np.bool_))  # nothing to continue into

    assert _result(await converse(servicer, start([(300, 723)]))) is not None
    assert calls == []


@pytest.mark.asyncio
async def test_an_offset_cell_decodes_each_tile_it_is_cropped_from_once() -> None:
    from segmentation_server import server as server_module

    cache = _cache()
    png = _png()
    for row, col in ((0, 0), (-1, -1), (-1, 0), (0, -1)):
        await put_tile(cache, row, col, png)
    servicer = _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_))
    seen: list = []
    original = server_module.TileImages.__getitem__

    def spy(self, key):
        seen.append(key)
        return original(self, key)

    server_module.TileImages.__getitem__ = spy
    try:
        await converse(servicer, start([(10, 10)]))
    finally:
        server_module.TileImages.__getitem__ = original

    assert servicer.model.predict_ephemeral.call_count >= 1
    assert seen, "the offset cell was never cropped"


def _square_logits():
    """A model answer: a 224 px square around window pixel (512, 512), with SAM2's native 256x256 logits."""
    mask = np.zeros((1024, 1024), dtype=np.bool_)
    mask[400:624, 400:624] = True
    logits = np.full((256, 256), -5.0, dtype=np.float32)
    logits[100:156, 100:156] = 5.0
    return mask, logits


@pytest.mark.asyncio
async def test_the_result_is_logits_at_the_native_grid_with_their_placement() -> None:
    cache = _cache()
    await put_tile(cache, 0, 0)
    mask, logits = _square_logits()
    servicer = _servicer(cache, mask=mask, logits=logits)

    response = _result(await converse(servicer, start([(512, 512)])))

    (segment,) = response.segments
    assert segment.island == 1
    assert segment.logits_scale == 4.0
    decoded = np.frombuffer(segment.logits, dtype="<f2").reshape(segment.logits_size.height, segment.logits_size.width)
    inside = decoded > 1.0
    assert 40 <= int(inside.sum()) ** 0.5 <= 60, "a 224 px square is 56 samples across"
    assert float(decoded.min()) <= -5.0 + 0.01
    # Window (0, 0)'s rows 400..623 (Y down) are mosaic y 400..623 (the window spans y 0..1023, flipped):
    # the square covers x 400..623 and y 1023-623..1023-400 = 400..623. The crop keeps two samples of border.
    rows, cols = np.nonzero(inside)
    top_pixel_y = segment.logits_origin.y - int(rows.min()) * 4
    left_pixel_x = segment.logits_origin.x + int(cols.min()) * 4
    assert left_pixel_x == 400
    assert top_pixel_y == 623
