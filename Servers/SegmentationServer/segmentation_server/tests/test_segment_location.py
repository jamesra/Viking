"""SegmentLocation: the server builds the prompt and answers the walk's background itself; tiles go to the client only
when it provides images."""

from __future__ import annotations

import asyncio
from typing import Callable, List, Optional

import grpc
import numpy as np
import pytest

from protocol import (
    cache as _cache,
    context as _context,
    png as _png,
    put_tile,
    servicer as _servicer,
    start as _start,
    circle,
)
from segmentation_grpc import (
    GridCell,
    LocationStart,
    SegmentLocationRequest,
    SegmentLocationResponse,
    TilesAnswer,
    UploadTileRequest,
)
from segmentation_grpc import prompt_input_pb2 as prompt_pb
from segmentation_server.location_source import LocationError, PreparedLocation


class _FakeLocations:
    """Prepares the test grid's corner click as the location's request and gives one circle per background round."""

    def __init__(self, readable: bool = True, error: Optional[LocationError] = None) -> None:
        self.readable = readable
        self.error = error
        self.background_rounds: List[List[GridCell]] = []

    async def prepare(self, start: LocationStart) -> PreparedLocation:
        if self.error is not None:
            raise self.error
        return PreparedLocation(
            request=_start([(512, 512)], annotation_context_id=start.annotation_context_id),
            prompt_input=prompt_pb.PromptInput(volume="RC2", section=1305, tileset="TEM"),
            readable=self.readable,
        )

    async def background(self, prepared: PreparedLocation, cells, sent_ids: List[int]):
        self.background_rounds.append(list(cells))
        sent_ids.append(len(sent_ids) + 1)
        return [circle(5000.0, 5000.0, 10.0)]


Client = Callable[[SegmentLocationResponse, "asyncio.Queue"], object]


async def _talk(servicer, start: LocationStart, context, client: Optional[Client] = None, close: bool = True):
    """Run one call. ``client`` sees each response and may put messages (None ends the client's half)."""
    inbox: asyncio.Queue = asyncio.Queue()
    await inbox.put(SegmentLocationRequest(start=start))
    if close and client is None:
        await inbox.put(None)

    async def incoming():
        while True:
            item = await inbox.get()
            if item is None:
                return
            yield item

    updates: List[SegmentLocationResponse] = []
    async for update in servicer.SegmentLocation(incoming(), context):
        updates.append(update)
        if client is not None:
            outcome = client(update, inbox)
            if asyncio.iscoroutine(outcome):
                await outcome
    return updates


def _of(updates, kind: str):
    return [getattr(u, kind) for u in updates if u.WhichOneof("update") == kind]


def _location(**fields) -> LocationStart:
    return LocationStart(annotation_context_id=42, location_id=7, **fields)


async def _servicer_with(locations, **kwargs):
    cache = _cache()
    await put_tile(cache, 0, 0)
    return _servicer(cache, mask=np.ones((4, 4), dtype=np.bool_), location_prompts=locations, **kwargs)


@pytest.mark.asyncio
async def test_a_client_without_images_is_never_asked_and_gets_a_result() -> None:
    locations = _FakeLocations(readable=True)
    servicer = await _servicer_with(locations)
    context = _context()

    updates = await _talk(servicer, _location(), context)

    context.abort.assert_not_awaited()
    assert not _of(updates, "needed")
    assert _of(updates, "progress")[0].stage == "reading annotations"
    assert any(p.stage == "growing" and p.round >= 1 for p in _of(updates, "progress"))
    [result] = _of(updates, "result")
    assert result.segments and result.request_id == 0
    assert locations.background_rounds, "the walk's background cells were answered by the provider"


@pytest.mark.asyncio
async def test_a_client_that_provides_images_is_asked_for_tiles_only_and_its_uploads_finish_the_walk() -> None:
    locations = _FakeLocations(readable=False)
    servicer = await _servicer_with(locations)
    context = _context()
    png = _png()

    async def client(update: SegmentLocationResponse, inbox: asyncio.Queue) -> None:
        if update.WhichOneof("update") == "needed":
            for asked in update.needed.tiles:
                await inbox.put(SegmentLocationRequest(tile=UploadTileRequest(cell=asked, image_data=png)))
            await inbox.put(SegmentLocationRequest(answer=TilesAnswer()))
        elif update.WhichOneof("update") == "result":
            await inbox.put(None)

    updates = await _talk(servicer, _location(provides_images=True), context, client)

    context.abort.assert_not_awaited()
    needed = _of(updates, "needed")
    assert needed and all(not n.background_cells for n in needed)
    assert needed[0].tiles
    assert _of(updates, "result")[0].segments


@pytest.mark.asyncio
async def test_a_half_close_settles_the_tiles_still_awaited_as_unavailable() -> None:
    servicer = await _servicer_with(_FakeLocations(readable=False))
    context = _context()

    def client(update: SegmentLocationResponse, inbox: asyncio.Queue) -> None:
        if update.WhichOneof("update") == "needed":
            inbox.put_nowait(None)

    updates = await _talk(servicer, _location(provides_images=True), context, client)

    context.abort.assert_not_awaited()
    assert len(_of(updates, "needed")) == 1
    assert len(_of(updates, "result")) == 1


@pytest.mark.asyncio
async def test_a_tile_for_a_readable_section_is_failed_precondition() -> None:
    servicer = await _servicer_with(_FakeLocations(readable=True))
    context = _context()
    gate = asyncio.Event()

    async def client(update: SegmentLocationResponse, inbox: asyncio.Queue) -> None:
        if not gate.is_set():
            gate.set()
            await inbox.put(SegmentLocationRequest(tile=UploadTileRequest(cell=GridCell(row=0, col=0), image_data=_png())))
            await inbox.put(None)

    updates = await _talk(servicer, _location(provides_images=True), context, client)

    assert context.abort.await_args.args[0] == grpc.StatusCode.FAILED_PRECONDITION
    assert not _of(updates, "result")


@pytest.mark.asyncio
async def test_an_unreadable_section_without_images_is_failed_precondition() -> None:
    servicer = await _servicer_with(_FakeLocations(readable=False))
    context = _context()

    await _talk(servicer, _location(), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.FAILED_PRECONDITION


@pytest.mark.asyncio
async def test_an_answer_with_no_tiles_needed_open_is_invalid_argument() -> None:
    servicer = await _servicer_with(_FakeLocations(readable=False))
    context = _context()
    sent = asyncio.Event()

    async def client(update: SegmentLocationResponse, inbox: asyncio.Queue) -> None:
        if not sent.is_set():
            sent.set()
            await inbox.put(SegmentLocationRequest(answer=TilesAnswer()))
            await inbox.put(SegmentLocationRequest(answer=TilesAnswer()))
            await inbox.put(None)

    await _talk(servicer, _location(provides_images=True), context, client)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT


@pytest.mark.asyncio
async def test_a_location_the_source_cannot_build_ends_with_its_status() -> None:
    error = LocationError(grpc.StatusCode.NOT_FOUND, "RC2 has no location 7.")
    servicer = await _servicer_with(_FakeLocations(error=error))
    context = _context()

    await _talk(servicer, _location(), context)

    context.abort.assert_awaited_with(grpc.StatusCode.NOT_FOUND, "RC2 has no location 7.")


@pytest.mark.asyncio
async def test_without_a_prompt_provider_the_call_is_unimplemented() -> None:
    cache = _cache()
    servicer = _servicer(cache)
    context = _context()

    await _talk(servicer, _location(), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.UNIMPLEMENTED


@pytest.mark.asyncio
async def test_a_call_must_begin_with_start() -> None:
    servicer = await _servicer_with(_FakeLocations())
    context = _context()

    async def incoming():
        yield SegmentLocationRequest(answer=TilesAnswer())

    async for _ in servicer.SegmentLocation(incoming(), context):
        pass

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT


class _ContextSource:
    """Two assigned contexts: one whose VikingXML names tilesets, one whose VikingXML cannot be read."""

    async def context_volumes(self):
        from segmentation_server.identity_client import LocalContext
        from segmentation_server.viking_volume import Tileset, TilesetLevel, VikingVolume, VolumeSection

        tileset = Tileset("TEM", "TEM", 256, 256, "", ".png", "03d", {1: TilesetLevel(1, 1, 1)})
        readable = VikingVolume("RC2", "http://h/RC2", {1: VolumeSection(1, "0001", {"TEM": tileset})})
        return [(LocalContext(7, "RC2", "http://h/RC2.xml"), readable), (LocalContext(9, "RPC1", "http://h/RPC1.xml"), None)]

    async def fetch(self, annotation_context_id, coord):
        return None

    async def can_read(self, annotation_context_id, coord):
        return False


@pytest.mark.asyncio
async def test_the_status_lists_each_assigned_volume_and_whether_its_images_are_read_here() -> None:
    from segmentation_grpc import ServerStatusRequest

    servicer = _servicer(_cache(), tile_source=_ContextSource())

    status = await servicer.GetServerStatus(ServerStatusRequest(), _context())

    assert [(v.annotation_context_id, v.name, v.readable) for v in status.readable_volumes] == [
        (7, "RC2", True), (9, "RPC1", False),
    ]
    # A client that reads the same VikingXML itself picks its context by the URL, and its filter from the tilesets.
    assert [(v.viking_xml, list(v.tilesets)) for v in status.readable_volumes] == [
        ("http://h/RC2.xml", ["TEM"]), ("http://h/RPC1.xml", []),
    ]
