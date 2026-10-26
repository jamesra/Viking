"""GetHeldTiles: which cells a stream would not ask the client for, so a prefetch can skip them."""

from __future__ import annotations

from typing import List, Optional, Set, Tuple

import grpc
import pytest

from segmentation_grpc import CoordinateSpace, GridCell, HeldTilesRequest, TileCoord, TileGrid
from segmentation_server.embedding_store import EmbeddingStore
from segmentation_server.server import MAX_HELD_TILES_CELLS
from segmentation_server.tile_store import TileStore
from test_segment_tiles import _context, _png, _VOLUME_KEY
from test_tile_sourcing import _cache, _servicer

MOSAIC = CoordinateSpace.COORDINATE_SPACE_MOSAIC
VOLUME = CoordinateSpace.COORDINATE_SPACE_VOLUME
CONTEXT_ID = 42


class _ReadableSource:
    """Reads ``cells`` in mosaic space for ``CONTEXT_ID``, as a local context's tileset would."""

    def __init__(self, cells: Set[Tuple[int, int]]) -> None:
        self.cells = cells
        self.fetched: List[Tuple[int, int]] = []

    async def fetch(self, annotation_context_id: int, coord: TileCoord) -> Optional[bytes]:
        self.fetched.append((coord.row, coord.col))
        return None

    async def can_read(self, annotation_context_id: int, coord: TileCoord) -> bool:
        return (
            annotation_context_id == CONTEXT_ID
            and coord.space == MOSAIC
            and (coord.row, coord.col) in self.cells
        )


def _grid(space: int = VOLUME, transform: str = "SliceToVolume1|") -> TileGrid:
    return TileGrid(volume="RC2", section=1305, channel="TEM", transform=transform, downsample=1, space=space)


def _request(cells, space: int = VOLUME, context_id: int = 0, transform: str = "SliceToVolume1|") -> HeldTilesRequest:
    return HeldTilesRequest(
        grid=_grid(space, transform),
        cells=[GridCell(row=row, col=col) for row, col in cells],
        annotation_context_id=context_id,
    )


@pytest.mark.asyncio
async def test_cached_stored_and_disk_cells_are_held_in_request_order(tmp_path) -> None:
    cache = _cache()
    await cache.upload_tile((*_VOLUME_KEY, 0, 0), _png(), 1024, 1024)
    store = TileStore(100_000_000, disk=EmbeddingStore(str(tmp_path), "ab" * 16, max_bytes=None, suffix=".png"))
    store.remember((*_VOLUME_KEY, 0, 1), b"ram")
    store.persist((*_VOLUME_KEY, 0, 2), b"disk")
    servicer = _servicer(cache, None, store)
    context = _context()

    response = await servicer.GetHeldTiles(_request([(0, 2), (5, 5), (0, 0), (0, 1)]), context)

    context.abort.assert_not_awaited()
    assert list(response.held) == [True, False, True, True]
    assert all(entry.in_use == 0 for entry in cache._cache.values())


@pytest.mark.asyncio
async def test_a_mosaic_cell_the_source_reads_is_held_only_for_its_context() -> None:
    source = _ReadableSource({(3, 4)})
    servicer = _servicer(_cache(), source)

    local = await servicer.GetHeldTiles(_request([(3, 4), (3, 5)], MOSAIC, CONTEXT_ID, "|grid.mosaic"), _context())
    no_context = await servicer.GetHeldTiles(_request([(3, 4)], MOSAIC, 0, "|grid.mosaic"), _context())
    volume = await servicer.GetHeldTiles(_request([(3, 4)], VOLUME, CONTEXT_ID), _context())

    assert list(local.held) == [True, False]
    assert list(no_context.held) == [False]
    assert list(volume.held) == [False]
    assert source.fetched == []


@pytest.mark.asyncio
async def test_a_mosaic_cell_and_a_volume_cell_do_not_share_an_answer() -> None:
    cache = _cache()
    await cache.upload_tile((*_VOLUME_KEY, 0, 0), _png(), 1024, 1024)
    servicer = _servicer(cache, None)

    response = await servicer.GetHeldTiles(_request([(0, 0)], MOSAIC), _context())

    assert list(response.held) == [False]


@pytest.mark.asyncio
async def test_asking_does_not_refresh_a_cached_cell() -> None:
    cache = _cache()
    await cache.upload_tile((*_VOLUME_KEY, 0, 0), _png(), 1024, 1024)
    (entry,) = cache._cache.values()
    before = entry.last_access_time

    await _servicer(cache, None).GetHeldTiles(_request([(0, 0)]), _context())

    assert entry.last_access_time == before
    assert entry.in_use == 0


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "request_, detail",
    [
        (_request([(0, 0)], CoordinateSpace.COORDINATE_SPACE_UNSPECIFIED), "MOSAIC or VOLUME"),
        (HeldTilesRequest(grid=TileGrid(volume="RC2", space=VOLUME), cells=[GridCell()]), "downsample"),
        (_request([(0, i) for i in range(MAX_HELD_TILES_CELLS + 1)]), "at most"),
    ],
    ids=["unspecified-space", "no-downsample", "too-many-cells"],
)
async def test_malformed_requests_are_refused(request_: HeldTilesRequest, detail: str) -> None:
    context = _context()

    await _servicer(_cache(), None).GetHeldTiles(request_, context)

    context.abort.assert_awaited_once()
    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.INVALID_ARGUMENT
    assert detail in message


@pytest.mark.asyncio
async def test_the_limit_itself_is_accepted() -> None:
    context = _context()

    response = await _servicer(_cache(), None).GetHeldTiles(
        _request([(0, i) for i in range(MAX_HELD_TILES_CELLS)]), context
    )

    context.abort.assert_not_awaited()
    assert len(response.held) == MAX_HELD_TILES_CELLS
