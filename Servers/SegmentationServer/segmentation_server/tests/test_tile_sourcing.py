"""SegmentTilesStream reads tiles of a local annotation context itself instead of asking the client for them."""

from __future__ import annotations

import asyncio
from typing import Dict, List, Optional, Set, Tuple

import numpy as np
import pytest

from segmentation_grpc import Feature, Point, SegmentTilesRequest, TileCoord
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer
from segmentation_server.tile_store import TileStore
from test_segment_tiles import _all_unavailable, _context, _converse, _needed_tiles, _png, _result, _tile, _VOLUME_KEY

CONTEXT_ID = 42

# A click at (10, 10) is owned by an offset cell cropped from these four tiles.
CORNER = {(0, 0), (-1, -1), (-1, 0), (0, -1)}


class _FakeSource:
    """Serves ``tiles`` for ``CONTEXT_ID`` and records every fetch."""

    def __init__(self, tiles: Set[Tuple[int, int]], data: Optional[bytes] = None, delay: float = 0.0) -> None:
        self.tiles = tiles
        self.data = data if data is not None else _png()
        self.delay = delay
        self.fetched: List[Tuple[int, int, int]] = []

    async def fetch(self, annotation_context_id: int, coord: TileCoord) -> Optional[bytes]:
        self.fetched.append((annotation_context_id, coord.row, coord.col))
        if self.delay:
            await asyncio.sleep(self.delay)
        if annotation_context_id != CONTEXT_ID or (coord.row, coord.col) not in self.tiles:
            return None
        return self.data


def _cache() -> ImageCache:
    return ImageCache(max_memory_bytes=100_000_000, ttl_seconds=60, create_predictor_func=lambda *_args: object())


def _servicer(cache: ImageCache, source, store: Optional[TileStore] = None) -> SegmentationServicer:
    from unittest.mock import MagicMock

    model = MagicMock()
    mask = np.ones((4, 4), dtype=np.bool_)
    model.predict_tile.return_value = (mask, None, 0.8)
    model.predict_ephemeral.return_value = (mask, None, 0.8)
    return SegmentationServicer(
        model=model,
        image_cache=cache,
        server_start_time=0.0,
        tile_store=store if store is not None else TileStore(100_000_000),
        tile_source=source,
    )


def _request(context_id: int = CONTEXT_ID) -> SegmentTilesRequest:
    return SegmentTilesRequest(
        tiles=[_tile(0, 0)], foreground_feature=Feature(points=[Point(x=10, y=10)]), annotation_context_id=context_id
    )


def _asked(updates) -> Set[Tuple[int, int]]:
    return {tile for tiles in _needed_tiles(updates) for tile in tiles}


@pytest.mark.asyncio
async def test_a_tile_the_source_reads_is_never_asked_of_the_client_and_lands_in_both_caches() -> None:
    cache = _cache()
    store = TileStore(100_000_000)
    source = _FakeSource(CORNER)
    servicer = _servicer(cache, source, store)
    context = _context()

    updates = await _converse(servicer, _request(), context, answer_for=_all_unavailable)

    context.abort.assert_not_awaited()
    assert not _asked(updates) & CORNER
    assert {(row, col) for _id, row, col in source.fetched} >= CORNER
    for row, col in CORNER:
        assert cache._coord_index.get((*_VOLUME_KEY, row, col)) is not None
        assert store.recall((*_VOLUME_KEY, row, col)) is not None
    assert _result(updates).segments
    assert all(entry.in_use == 0 for entry in cache._cache.values())


@pytest.mark.asyncio
async def test_a_tile_the_server_already_holds_is_not_read_from_the_source() -> None:
    cache = _cache()
    png = _png()
    await cache.upload_tile((*_VOLUME_KEY, 0, 0), png, 1024, 1024)
    store = TileStore(100_000_000)
    store.remember((*_VOLUME_KEY, -1, -1), png)
    source = _FakeSource(CORNER)

    await _converse(_servicer(cache, source, store), _request(), answer_for=_all_unavailable)

    fetched = {(row, col) for _id, row, col in source.fetched}
    assert (0, 0) not in fetched
    assert (-1, -1) not in fetched


@pytest.mark.asyncio
async def test_what_the_source_cannot_read_is_asked_of_the_client() -> None:
    cache = _cache()
    source = _FakeSource({(0, 0)})

    updates = await _converse(_servicer(cache, source), _request(), answer_for=_all_unavailable)

    assert (0, 0) not in _asked(updates)
    assert {(-1, -1), (-1, 0), (0, -1)} <= _asked(updates)


@pytest.mark.asyncio
async def test_a_start_tile_is_read_in_the_first_round() -> None:
    cache = _cache()
    source = _FakeSource({(0, 0)})
    servicer = _servicer(cache, source)
    servicer.model.predict_tile.return_value = (np.zeros((4, 4), dtype=np.bool_), None, 0.8)
    request = _request()
    request.foreground_feature.points[0].x = 512
    request.foreground_feature.points[0].y = 512

    updates = await _converse(servicer, request, answer_for=_all_unavailable)

    assert (CONTEXT_ID, 0, 0) in source.fetched
    assert (0, 0) not in _asked(updates)
    servicer.model.predict_tile.assert_called()


@pytest.mark.asyncio
@pytest.mark.parametrize("context_id", [0, 7])
async def test_without_a_local_context_nothing_is_read(context_id: int) -> None:
    cache = _cache()
    source = _FakeSource(CORNER)

    updates = await _converse(_servicer(cache, source), _request(context_id), answer_for=_all_unavailable)

    assert CORNER <= _asked(updates)
    if context_id == 0:
        assert not source.fetched


@pytest.mark.asyncio
async def test_a_failing_source_falls_back_to_the_client() -> None:
    class _Broken:
        async def fetch(self, annotation_context_id: int, coord: TileCoord) -> Optional[bytes]:
            raise OSError("storage unreachable")

    updates = await _converse(_servicer(_cache(), _Broken()), _request(), answer_for=_all_unavailable)

    assert CORNER <= _asked(updates)


@pytest.mark.asyncio
async def test_two_streams_read_a_shared_tile_once() -> None:
    cache = _cache()
    source = _FakeSource(CORNER, delay=0.05)
    servicer = _servicer(cache, source)

    await asyncio.gather(
        _converse(servicer, _request(), answer_for=_all_unavailable),
        _converse(servicer, _request(), answer_for=_all_unavailable),
    )

    reads: Dict[Tuple[int, int], int] = {}
    for _id, row, col in source.fetched:
        reads[(row, col)] = reads.get((row, col), 0) + 1
    assert all(count == 1 for count in reads.values()), reads
    assert all(entry.in_use == 0 for entry in cache._cache.values())
