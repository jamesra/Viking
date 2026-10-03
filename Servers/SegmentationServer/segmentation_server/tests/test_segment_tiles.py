"""SegmentTilesStream: the server owns the growth and asks for tiles as the mask reaches them."""

from __future__ import annotations

import asyncio
from typing import Callable, List, Optional
from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from segmentation_grpc import (
    BoundingBox,
    Point,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    TileCoord,
    TilesAnswer,
    TilesNeeded,
)
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer

_VOLUME_KEY = ("RC2", 1305, "TEM", "SliceToVolume1|", 1)

AnswerFor = Callable[[TilesNeeded], Optional[TilesAnswer]]


def _servicer(cache: ImageCache) -> SegmentationServicer:
    model = MagicMock()
    mask = np.zeros((4, 4), dtype=np.bool_)
    mask[1, 1] = True
    model.predict_tile.return_value = (mask, None, 0.8)
    model.predict_ephemeral.return_value = (mask, None, 0.8)
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


def _context() -> AsyncMock:
    """A gRPC context whose cancelled() is a plain bool, as in grpc.aio, not a coroutine."""
    context = AsyncMock()
    context.cancelled = MagicMock(return_value=False)
    return context


def _tile(row: int, col: int) -> TileCoord:
    return TileCoord(
        volume="RC2",
        section=1305,
        channel="TEM",
        transform="SliceToVolume1|",
        downsample=1,
        row=row,
        col=col,
    )


def _cache() -> ImageCache:
    return ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        max_entries=8,
        create_predictor_func=lambda _data: object(),
    )


def _png() -> bytes:
    ok, encoded = cv2.imencode(".png", np.full((1024, 1024), 90, dtype=np.uint8))
    assert ok
    return encoded.tobytes()


async def _upload(cache: ImageCache, row: int, col: int, data: bytes = b"seed") -> None:
    await cache.upload_tile((*_VOLUME_KEY, row, col), data, 1024, 1024)


def _all_unavailable(needed: TilesNeeded) -> TilesAnswer:
    return TilesAnswer(unavailable=list(needed.tiles))


def _all_ready(needed: TilesNeeded) -> TilesAnswer:
    return TilesAnswer(ready=list(needed.tiles))


async def _converse(
    servicer: SegmentationServicer,
    request: SegmentTilesRequest,
    context: Optional[AsyncMock] = None,
    answer_for: AnswerFor = _all_unavailable,
) -> List[SegmentTilesStreamResponse]:
    """Send ``start``, answer each ``needed`` with ``answer_for`` (None ends the client's stream)."""
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentTilesStreamRequest(start=request))

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates: List[SegmentTilesStreamResponse] = []
    async for update in servicer.SegmentTilesStream(incoming(), context or _context()):
        updates.append(update)
        if update.WhichOneof("update") == "needed":
            answer = answer_for(update.needed)
            await inbox.put(None if answer is None else SegmentTilesStreamRequest(answer=answer))
    return updates


def _result(updates: List[SegmentTilesStreamResponse]):
    assert updates, "the stream produced nothing"
    assert updates[-1].WhichOneof("update") == "result"
    return updates[-1].result


def _needed_tiles(updates: List[SegmentTilesStreamResponse]) -> List[set]:
    return [
        {(tile.row, tile.col) for tile in update.needed.tiles}
        for update in updates
        if update.WhichOneof("update") == "needed"
    ]


@pytest.mark.asyncio
async def test_missing_tile_without_foreground_does_not_abort() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(
        tiles=[_tile(0, 0), _tile(0, 1)],
        foreground=[Point(x=300, y=723)],
    )
    context = _context()

    response = _result(await _converse(servicer, request, context))

    context.abort.assert_not_awaited()
    servicer.model.predict_tile.assert_called_once()
    assert response.segments
    assert cache._cache
    assert all(entry.in_use == 0 for entry in cache._cache.values())


async def _call_sent_to_the_model(**request_fields):
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(
        tiles=[_tile(0, 0)],
        foreground=[Point(x=300, y=723)],
        **request_fields,
    )

    await _converse(servicer, request)

    return servicer.model.predict_tile.call_args


async def _threshold_sent_to_the_model(**request_fields) -> float:
    return (await _call_sent_to_the_model(**request_fields)).args[6]


@pytest.mark.asyncio
async def test_client_boxes_become_the_box_prompt_and_every_click_is_sent() -> None:
    call = await _call_sent_to_the_model(
        foreground_boxes=[BoundingBox(x_min=200, y_min=600, x_max=500, y_max=900)],
    )

    assert call.args[5] is not None
    assert len(call.args[1]) == 1


@pytest.mark.asyncio
async def test_without_client_boxes_a_lone_click_gets_no_box() -> None:
    call = await _call_sent_to_the_model()

    assert call.args[5] is None


@pytest.mark.asyncio
async def test_a_client_box_with_no_area_is_ignored() -> None:
    call = await _call_sent_to_the_model(
        foreground_boxes=[BoundingBox(x_min=200, y_min=600, x_max=200, y_max=900)],
    )

    assert call.args[5] is None


async def _response_for(**request_fields):
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=300, y=723)], **request_fields)
    return _result(await _converse(servicer, request))


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
async def test_click_near_a_tile_corner_asks_for_the_neighbor_tiles_instead_of_aborting() -> None:
    """A click at (10, 10) is owned by an offset cell that needs tiles (-1,-1), (-1,0), (0,-1) and (0,0)."""
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])
    context = _context()

    updates = await _converse(servicer, request, context)

    context.abort.assert_not_awaited()
    servicer.model.predict_tile.assert_not_called()
    servicer.model.predict_ephemeral.assert_not_called()
    assert {(-1, -1), (-1, 0), (0, -1)} <= _needed_tiles(updates)[0]
    assert _result(updates) is not None


@pytest.mark.asyncio
async def test_uploaded_tiles_let_the_same_walk_continue_and_finish() -> None:
    """The server asks once, the client supplies the tiles, and the walk predicts without starting over."""
    cache = _cache()
    png = _png()
    for row, col in ((0, 0), (-1, -1), (-1, 0), (0, -1)):
        await _upload(cache, row, col, png)
    servicer = _servicer(cache)
    servicer.model.predict_ephemeral.return_value = (np.ones((4, 4), dtype=np.bool_), None, 0.8)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])
    context = _context()

    updates = await _converse(servicer, request, context, answer_for=_all_ready)

    context.abort.assert_not_awaited()
    assert _needed_tiles(updates)[0] >= {(-1, -1), (-1, 0), (0, -1)}
    assert servicer.model.predict_ephemeral.call_count >= 1
    assert _result(updates).segments
    assert all(entry.in_use == 0 for entry in cache._cache.values())


@pytest.mark.asyncio
async def test_a_tile_reported_ready_but_not_cached_does_not_stall_the_walk() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])

    updates = await _converse(servicer, request, answer_for=_all_ready)

    assert _result(updates) is not None
    servicer.model.predict_ephemeral.assert_not_called()


@pytest.mark.asyncio
async def test_a_tile_asked_for_once_is_not_asked_for_again() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])

    updates = await _converse(servicer, request, answer_for=_all_unavailable)

    asked = [tile for tiles in _needed_tiles(updates) for tile in tiles]
    assert len(asked) == len(set(asked))


@pytest.mark.asyncio
async def test_a_client_that_ends_its_stream_while_tiles_are_awaited_gets_no_result() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])

    updates = await _converse(servicer, request, answer_for=lambda _needed: None)

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert all(entry.in_use == 0 for entry in cache._cache.values())


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
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])

    async def incoming():
        yield SegmentTilesStreamRequest(start=request)
        yield SegmentTilesStreamRequest(start=request)

    updates = [update async for update in servicer.SegmentTilesStream(incoming(), context)]

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert all(entry.in_use == 0 for entry in cache._cache.values())


@pytest.mark.asyncio
async def test_missing_foreground_tile_is_not_found() -> None:
    cache = _cache()
    await _upload(cache, 0, 0, b"other")
    servicer = _servicer(cache)
    request = SegmentTilesRequest(
        tiles=[_tile(0, 0), _tile(0, 1)],
        foreground=[Point(x=1034, y=10)],
    )
    context = _context()

    await _converse(servicer, request, context)

    context.abort.assert_awaited()
    assert context.abort.await_args.args[0] == grpc.StatusCode.NOT_FOUND
    assert "row=0 col=1" in context.abort.await_args.args[1]
    servicer.model.predict_tile.assert_not_called()
    assert all(entry.in_use == 0 for entry in cache._cache.values())

@pytest.mark.asyncio
async def test_a_client_that_goes_quiet_after_tiles_are_requested_times_out_and_releases_its_pins() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._tile_answer_timeout = 0.2
    context = _context()
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])

    async def incoming():
        yield SegmentTilesStreamRequest(start=request)
        await asyncio.sleep(30)

    updates = await asyncio.wait_for(
        _collect(servicer.SegmentTilesStream(incoming(), context)), timeout=10
    )

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert context.abort.await_args.args[0] == grpc.StatusCode.DEADLINE_EXCEEDED
    assert all(entry.in_use == 0 for entry in cache._cache.values())
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
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    context = _context()
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=300, y=723)])

    updates = await _converse(servicer, request, context)

    assert updates == []
    context.abort.assert_not_awaited()
    assert all(entry.in_use == 0 for entry in cache._cache.values())
    assert servicer._load.snapshot()[0] == 0

@pytest.mark.asyncio
async def test_no_matching_mask_for_the_starting_cell_fails_the_stream_with_failed_precondition() -> None:
    from segmentation_server.mask_utils import NoMatchingMask

    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer.model.predict_tile.side_effect = NoMatchingMask("None of the 3 candidate mask(s) covers any ...")
    context = _context()
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=300, y=723)])

    updates = await _converse(servicer, request, context)

    assert updates == []
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.FAILED_PRECONDITION
    assert message.startswith("NO_MATCHING_MASK ")
    assert all(entry.in_use == 0 for entry in cache._cache.values())
    assert servicer._load.snapshot()[0] == 0

def _limited(**limits):
    from segmentation_server.server import RequestLimits

    servicer = _servicer(_cache())
    servicer._limits = RequestLimits(**limits)
    return servicer


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "limits,request_fields,named",
    [
        ({"max_tiles": 2}, {"tiles": [_tile(0, 0), _tile(0, 1), _tile(1, 0)], "foreground": [Point(x=1, y=1)]}, "SEGMENTATION_MAX_TILES"),
        ({"max_points": 3}, {"tiles": [_tile(0, 0)], "foreground": [Point(x=i, y=i) for i in range(2)], "background": [Point(x=9, y=9), Point(x=8, y=8)]}, "SEGMENTATION_MAX_POINTS"),
        ({"max_boxes": 1}, {"tiles": [_tile(0, 0)], "foreground": [Point(x=1, y=1)], "foreground_boxes": [BoundingBox(x_min=0, y_min=0, x_max=5, y_max=5)] * 2}, "SEGMENTATION_MAX_BOXES"),
    ],
    ids=["tiles", "points", "boxes"],
)
async def test_a_request_over_a_size_limit_is_resource_exhausted_before_any_work(limits, request_fields, named) -> None:
    servicer = _limited(**limits)
    context = _context()

    updates = await _converse(servicer, SegmentTilesRequest(**request_fields), context)

    assert updates == []
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert named in message
    servicer.model.predict_tile.assert_not_called()
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_a_request_at_the_limit_is_served() -> None:
    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    from segmentation_server.server import RequestLimits

    servicer._limits = RequestLimits(max_tiles=1, max_points=1, max_boxes=1)
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=300, y=723)])

    assert _result(await _converse(servicer, request)) is not None


@pytest.mark.asyncio
async def test_an_answer_listing_more_tiles_than_the_limit_is_rejected() -> None:
    from segmentation_server.server import RequestLimits

    cache = _cache()
    await _upload(cache, 0, 0)
    servicer = _servicer(cache)
    servicer._limits = RequestLimits(max_answer_tiles=2)
    context = _context()

    updates = await _converse(
        servicer,
        SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)]),
        context,
        answer_for=lambda _needed: TilesAnswer(ready=[_tile(7, c) for c in range(5)]),
    )

    assert [update.WhichOneof("update") for update in updates] == ["needed"]
    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert "SEGMENTATION_MAX_ANSWER_TILES" in context.abort.await_args.args[1]
    assert all(entry.in_use == 0 for entry in cache._cache.values())


def test_request_limits_come_from_the_environment(monkeypatch) -> None:
    from segmentation_server.server import RequestLimits

    for name in ("TILES", "POINTS", "BOXES", "SETS", "ANSWER_TILES"):
        monkeypatch.delenv(f"SEGMENTATION_MAX_{name}", raising=False)
    assert RequestLimits.from_env() == RequestLimits()
    monkeypatch.setenv("SEGMENTATION_MAX_TILES", "7")
    monkeypatch.setenv("SEGMENTATION_MAX_POINTS", "many")
    monkeypatch.setenv("SEGMENTATION_MAX_BOXES", "0")
    limits = RequestLimits.from_env()
    assert limits.max_tiles == 7
    assert limits.max_points == RequestLimits().max_points
    assert limits.max_boxes == 1

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
    await _upload(cache, 0, 0, _png())
    servicer = _servicer(cache)
    servicer.model.predict_tile.return_value = (np.zeros((4, 4), dtype=np.bool_), None, 0.8)  # nothing to continue into
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=300, y=723)])

    assert _result(await _converse(servicer, request)) is not None
    assert calls == []


@pytest.mark.asyncio
async def test_an_offset_cell_decodes_each_tile_it_is_cropped_from_once() -> None:
    from segmentation_server import server as server_module

    cache = _cache()
    png = _png()
    for row, col in ((0, 0), (-1, -1), (-1, 0), (0, -1)):
        await _upload(cache, row, col, png)
    servicer = _servicer(cache)
    servicer.model.predict_ephemeral.return_value = (np.ones((4, 4), dtype=np.bool_), None, 0.8)
    seen: list = []
    original = server_module.TileImages.__getitem__

    def spy(self, key):
        seen.append(key)
        return original(self, key)

    server_module.TileImages.__getitem__ = spy
    try:
        await _converse(
            servicer,
            SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)]),
            answer_for=_all_ready,
        )
    finally:
        server_module.TileImages.__getitem__ = original

    assert servicer.model.predict_ephemeral.call_count >= 1
    assert seen, "the offset cell was never cropped"
