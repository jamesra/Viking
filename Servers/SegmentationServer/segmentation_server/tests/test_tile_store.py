"""The tile store: uploaded tiles the server can rebuild after the image cache drops them, without asking a client."""

from __future__ import annotations

import pytest

from segmentation_grpc import Feature, Point, SegmentTilesRequest, UploadTileRequest
from segmentation_server.cell_grid import TILE_SIZE
from segmentation_server.embedding_store import EmbeddingStore
from segmentation_server.image_cache import ImageCache
from segmentation_server.tile_store import TileStore, open_tile_store
from test_segment_tiles import _all_unavailable, _context, _converse, _needed_tiles, _png, _result, _servicer, _tile


def _key(row: int = 0, col: int = 0):
    return ("RC2", 1305, "TEM", "SliceToVolume1|", "volume", 1, row, col)


def test_memory_drops_the_least_recently_used_tile_over_the_byte_cap() -> None:
    store = TileStore(ram_bytes=10)
    store.remember(_key(0), b"aaaa")
    store.remember(_key(1), b"bbbb")
    assert store.recall(_key(0)) == b"aaaa"
    store.remember(_key(2), b"cccc")

    assert store.recall(_key(1)) is None
    assert store.recall(_key(0)) == b"aaaa"
    assert store.recall(_key(2)) == b"cccc"
    assert store.held_bytes == 8


def test_a_tile_bigger_than_the_memory_cap_is_not_kept() -> None:
    store = TileStore(ram_bytes=3)
    store.remember(_key(), b"abcd")
    assert store.recall(_key()) is None
    assert store.held_bytes == 0


def test_a_tile_on_disk_survives_a_new_store(tmp_path) -> None:
    def disk() -> EmbeddingStore:
        return EmbeddingStore(str(tmp_path), "ab" * 16, max_bytes=None, suffix=".png")

    first = TileStore(ram_bytes=1024, disk=disk())
    first.remember(_key(), b"png-bytes")
    first.persist(_key(), b"png-bytes")

    second = TileStore(ram_bytes=1024, disk=disk())
    assert second.recall(_key()) is None
    assert second.load(_key()) == b"png-bytes"
    assert second.recall(_key()) == b"png-bytes"

    second.forget(_key())
    assert second.load(_key()) is None
    assert list(tmp_path.rglob("*.png")) == []


def test_tile_files_do_not_count_as_embeddings_under_one_root(tmp_path) -> None:
    tiles = EmbeddingStore(str(tmp_path / "tiles"), "ab" * 16, max_bytes=None, suffix=".png")
    TileStore(ram_bytes=0, disk=tiles).persist(_key(), b"x" * 100)

    embeddings = EmbeddingStore(str(tmp_path), "cd" * 16, max_bytes=None)

    assert embeddings.total_bytes == 0
    assert list(tmp_path.rglob("*.png"))


def test_the_disk_store_lives_under_the_embedding_cache_root(monkeypatch, tmp_path) -> None:
    monkeypatch.setenv("SEGMENTATION_EMBEDDING_CACHE", str(tmp_path))
    monkeypatch.setenv("SEGMENTATION_TILE_RAM_BYTES", "0")
    store = open_tile_store()
    store.persist(_key(), b"png-bytes")

    assert store.has_disk
    assert list((tmp_path / "tiles").rglob("*.png"))
    assert store.load(_key()) == b"png-bytes"


def test_without_an_embedding_cache_the_store_is_memory_only(monkeypatch) -> None:
    monkeypatch.delenv("SEGMENTATION_EMBEDDING_CACHE", raising=False)
    assert not open_tile_store().has_disk


def _upload(row: int, col: int, data: bytes) -> UploadTileRequest:
    return UploadTileRequest(coord=_tile(row, col), image_data=data, width=TILE_SIZE, height=TILE_SIZE)


@pytest.mark.asyncio
async def test_a_tile_the_image_cache_dropped_is_rebuilt_without_asking_the_client() -> None:
    created = []
    cache = ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        create_predictor_func=lambda *_args: created.append(1) or object(),
    )
    servicer = _servicer(cache)
    png = _png()
    for row, col in ((0, 0), (-1, -1), (-1, 0), (0, -1)):
        await servicer.UploadTile(_upload(row, col, png), _context())
    # GPU pressure, or a restart of the image cache: every embedding and its bytes are gone from it.
    await cache.clear()
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground_feature=Feature(points=[Point(x=10, y=10)]))
    context = _context()

    updates = await _converse(servicer, request, context, answer_for=_all_unavailable)

    context.abort.assert_not_awaited()
    asked = {tile for tiles in _needed_tiles(updates) for tile in tiles}
    assert not asked & {(0, 0), (-1, -1), (-1, 0), (0, -1)}
    assert _result(updates) is not None
    assert all(entry.in_use == 0 for entry in cache._cache.values())
    rebuilt = await cache.get_image_by_tile(_key(0, 0))
    assert rebuilt is not None and rebuilt[1] == png
    await cache.release_image(rebuilt[0])


@pytest.mark.asyncio
async def test_a_stored_tile_the_image_cache_cannot_take_is_used_from_its_pixels() -> None:
    cache = ImageCache(max_memory_bytes=1, ttl_seconds=60, create_predictor_func=lambda *_args: object())
    store = TileStore(ram_bytes=100_000_000)
    for row, col in ((0, 0), (-1, -1), (-1, 0), (0, -1)):
        store.remember(_key(row, col), _png())
    servicer = _servicer(cache)
    servicer._tile_store = store
    request = SegmentTilesRequest(tiles=[_tile(0, 0)], foreground_feature=Feature(points=[Point(x=10, y=10)]))
    context = _context()

    updates = await _converse(servicer, request, context, answer_for=_all_unavailable)

    context.abort.assert_not_awaited()
    asked = {tile for tiles in _needed_tiles(updates) for tile in tiles}
    assert not asked & {(0, 0), (-1, -1), (-1, 0), (0, -1)}
    assert servicer.model.predict_ephemeral.call_count >= 1
    assert _result(updates) is not None
