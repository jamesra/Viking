"""Two SegmentTilesStream calls that need the same tile fetch it once."""

from __future__ import annotations

import asyncio
import time
from typing import List, Optional

import grpc
import numpy as np
import pytest

from segmentation_grpc import (
    Point,
    SegmentTilesRequest,
    SegmentTilesStreamRequest,
    SegmentTilesStreamResponse,
    TileCoord,
    TilesAnswer,
    UploadTileRequest,
)
from segmentation_server.cell_grid import TILE_SIZE
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer
from test_segment_tiles import _context, _png, _servicer, _tile, _upload

NEIGHBOURS = {(-1, -1), (-1, 0), (0, -1)}


def _request() -> SegmentTilesRequest:
    """A click at (10, 10), owned by an offset cell that needs the three neighbours of tile (0, 0)."""
    return SegmentTilesRequest(tiles=[_tile(0, 0)], foreground=[Point(x=10, y=10)])


class _Client:
    """One SegmentTilesStream conversation driven by hand, so a test can interleave two of them."""

    def __init__(self, servicer: SegmentationServicer, request: SegmentTilesRequest) -> None:
        self.context = _context()
        self.updates: List[SegmentTilesStreamResponse] = []
        self.needed: asyncio.Queue = asyncio.Queue()
        self._inbox: asyncio.Queue = asyncio.Queue()
        self._inbox.put_nowait(SegmentTilesStreamRequest(start=request))
        self.task = asyncio.create_task(self._run(servicer))

    async def _incoming(self):
        while True:
            item = await self._inbox.get()
            if item is None:
                return
            yield item

    async def _run(self, servicer: SegmentationServicer) -> None:
        async for update in servicer.SegmentTilesStream(self._incoming(), self.context):
            self.updates.append(update)
            if update.WhichOneof("update") == "needed":
                self.needed.put_nowait({(tile.row, tile.col) for tile in update.needed.tiles})

    async def next_needed(self, timeout: float = 5.0) -> set:
        return await asyncio.wait_for(self.needed.get(), timeout)

    async def answer(self, *, ready=(), unavailable=()) -> None:
        coords = lambda tiles: [_tile(row, col) for row, col in tiles]
        await self._inbox.put(
            SegmentTilesStreamRequest(answer=TilesAnswer(ready=coords(ready), unavailable=coords(unavailable)))
        )

    async def hang_up(self) -> None:
        await self._inbox.put(None)

    async def finish(self, timeout: float = 10.0) -> List[SegmentTilesStreamResponse]:
        await asyncio.wait_for(self.task, timeout)
        return self.updates

    def kinds(self) -> List[str]:
        return [update.WhichOneof("update") for update in self.updates]


async def _client_uploads(servicer: SegmentationServicer, tiles) -> None:
    """What a client does for a ``needed``: send each tile through UploadTile."""
    for row, col in tiles:
        await servicer.UploadTile(
            UploadTileRequest(coord=_tile(row, col), image_data=_png(), width=TILE_SIZE, height=TILE_SIZE),
            _context(),
        )


async def _wait_until(condition, timeout: float = 5.0) -> None:
    deadline = time.monotonic() + timeout
    while not condition():
        assert time.monotonic() < deadline, "condition was not met in time"
        await asyncio.sleep(0.01)


def _key(row: int, col: int):
    return ("RC2", 1305, "TEM", "SliceToVolume1|", 1, row, col)


async def _two_streams_wanting_the_neighbours(servicer: SegmentationServicer):
    """Start A, wait for its ``needed``, then start B and wait until B is queued behind A's request."""
    a = _Client(servicer, _request())
    assert (await a.next_needed()) >= NEIGHBOURS
    b = _Client(servicer, _request())
    await _wait_until(lambda: all(servicer._tile_requests.waiter_count(_key(*t)) == 1 for t in NEIGHBOURS))
    return a, b


def _servicer_with_tile_zero() -> SegmentationServicer:
    """UploadTile passes the decoded pixels to the predictor factory, which test_segment_tiles' cache does not accept."""
    cache = ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        max_entries=8,
        create_predictor_func=lambda *_args, **_kwargs: object(),
    )
    servicer = _servicer(cache)
    servicer.model.predict_ephemeral.return_value = (np.ones((4, 4), dtype=np.bool_), None, 0.8)
    return servicer


@pytest.mark.asyncio
async def test_a_second_stream_waits_for_the_first_streams_upload_and_asks_its_client_for_nothing() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    a, b = await _two_streams_wanting_the_neighbours(servicer)

    asked_of_b: list = []

    async def serve(client: _Client, record: list) -> None:
        """Upload and report ready for every later ``needed`` the client is sent."""
        while not client.task.done():
            try:
                tiles = await asyncio.wait_for(client.needed.get(), 0.2)
            except asyncio.TimeoutError:
                continue
            record.append(tiles)
            await _client_uploads(servicer, tiles)
            await client.answer(ready=tiles)

    await _client_uploads(servicer, NEIGHBOURS)
    await a.answer(ready=NEIGHBOURS)
    servers = [asyncio.create_task(serve(a, [])), asyncio.create_task(serve(b, asked_of_b))]
    try:
        a_updates = await a.finish()
        b_updates = await b.finish()
    finally:
        for server in servers:
            server.cancel()

    assert not any(NEIGHBOURS & tiles for tiles in asked_of_b), (
        "B must be served the neighbours from A's upload without its own client being asked for them"
    )
    assert a.kinds()[-1] == "result" and b.kinds()[-1] == "result" and b_updates[-1].result.segments
    assert len(servicer._tile_requests) == 0
    assert all(entry.in_use == 0 for entry in servicer.image_cache._cache.values())
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_a_stream_waits_only_as_long_as_the_share_timeout_then_asks_its_own_client() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_share_timeout = 0.2
    a = _Client(servicer, _request())
    await a.next_needed()
    b = _Client(servicer, _request())

    asked_of_b = await b.next_needed(timeout=5)

    assert asked_of_b >= NEIGHBOURS
    await b.answer(unavailable=asked_of_b)
    await a.answer(unavailable=NEIGHBOURS)
    await b.finish()
    await a.finish()
    assert len(servicer._tile_requests) == 0


@pytest.mark.asyncio
async def test_a_stream_asks_its_own_client_at_once_when_the_first_stream_reports_the_tile_unavailable() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_share_timeout = 60.0
    a, b = await _two_streams_wanting_the_neighbours(servicer)

    started = time.monotonic()
    await a.answer(unavailable=NEIGHBOURS)
    asked_of_b = await b.next_needed(timeout=5)

    assert time.monotonic() - started < 5, "B must not sit out the 60s share timeout"
    assert asked_of_b >= NEIGHBOURS
    await b.answer(unavailable=asked_of_b)
    await a.finish()
    await b.finish()


@pytest.mark.asyncio
async def test_a_stream_asks_its_own_client_at_once_when_the_first_stream_goes_away() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_share_timeout = 60.0
    a, b = await _two_streams_wanting_the_neighbours(servicer)

    await a.hang_up()
    await a.finish()
    asked_of_b = await b.next_needed(timeout=5)

    assert asked_of_b >= NEIGHBOURS
    await b.answer(unavailable=asked_of_b)
    await b.finish()
    assert len(servicer._tile_requests) == 0


@pytest.mark.asyncio
async def test_a_borrowed_tile_that_left_the_cache_before_it_could_be_pinned_is_asked_for_instead() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_share_timeout = 60.0
    a, b = await _two_streams_wanting_the_neighbours(servicer)

    servicer._tile_requests.arrived(_key(-1, -1))
    asked_of_b = await b.next_needed(timeout=5)

    assert asked_of_b == {(-1, -1)}
    await b.answer(unavailable=asked_of_b)
    await a.answer(unavailable=NEIGHBOURS)
    await a.finish()
    still_borrowed = await b.next_needed(timeout=5)
    assert still_borrowed == NEIGHBOURS - {(-1, -1)}
    await b.answer(unavailable=still_borrowed)
    await b.finish()


@pytest.mark.asyncio
async def test_with_sharing_off_every_stream_asks_for_every_tile() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_share_timeout = 0.0
    a = _Client(servicer, _request())
    b = _Client(servicer, _request())

    assert (await a.next_needed()) >= NEIGHBOURS
    assert (await b.next_needed()) >= NEIGHBOURS
    await a.answer(unavailable=NEIGHBOURS)
    await b.answer(unavailable=NEIGHBOURS)
    await a.finish()
    await b.finish()
    assert len(servicer._tile_requests) == 0


@pytest.mark.asyncio
async def test_a_client_that_keeps_sending_answers_that_settle_nothing_still_times_out() -> None:
    servicer = _servicer_with_tile_zero()
    await _upload(servicer.image_cache, 0, 0, _png())
    servicer._tile_answer_timeout = 0.3
    client = _Client(servicer, _request())
    await client.next_needed()

    async def chatter() -> None:
        while not client.task.done():
            await client.answer()
            await asyncio.sleep(0.05)

    chatting = asyncio.create_task(chatter())
    try:
        await client.finish(timeout=5)
    finally:
        chatting.cancel()

    assert client.context.abort.await_args.args[0] == grpc.StatusCode.DEADLINE_EXCEEDED
    assert all(entry.in_use == 0 for entry in servicer.image_cache._cache.values())
    assert len(servicer._tile_requests) == 0


def test_the_share_timeout_comes_from_the_environment(monkeypatch) -> None:
    from segmentation_server.server import tile_share_timeout_seconds

    monkeypatch.delenv("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", raising=False)
    assert tile_share_timeout_seconds() == 5.0
    monkeypatch.setenv("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", "12")
    assert tile_share_timeout_seconds() == 12.0
    monkeypatch.setenv("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", "0")
    assert tile_share_timeout_seconds() == 0.0
    monkeypatch.setenv("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", "-3")
    assert tile_share_timeout_seconds() == 0.0
    monkeypatch.setenv("SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS", "soon")
    assert tile_share_timeout_seconds() == 5.0
