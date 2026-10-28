"""The image cache's two embedding tiers: GPU (capped in real bytes) and host memory (capped with the tiles' bytes),
pulls back on use, thrash warnings, and the idle expiry of shared tiles. No SAM2 or GPU needed."""

from __future__ import annotations

import logging

import pytest

from segmentation_server import image_cache as image_cache_module
from segmentation_server.image_cache import ImageCache

EMBED = 100  # bytes of one fake embedding


class FakeClock:
    def __init__(self, start: float = 1000.0) -> None:
        self.now = start

    def __call__(self) -> float:
        return self.now

    def advance(self, seconds: float) -> None:
        self.now += seconds


class FakePredictor:
    def __init__(self) -> None:
        self.on_gpu = True
        self.host_copy = None


class FakeTiers:
    """Stands in for the SAM2 model: an embedding is EMBED bytes wherever it is."""

    def __init__(self) -> None:
        self.exports = 0
        self.restores = 0

    def gpu_bytes(self, predictor: FakePredictor) -> int:
        return EMBED if predictor.on_gpu else 0

    def export(self, predictor: FakePredictor):
        assert predictor.on_gpu
        self.exports += 1
        return ("copy", id(predictor)), EMBED

    def drop_gpu(self, predictor: FakePredictor, host_copy) -> None:
        predictor.on_gpu = False
        predictor.host_copy = host_copy

    def restore(self, predictor: FakePredictor) -> int:
        assert predictor.host_copy is not None
        self.restores += 1
        predictor.on_gpu = True
        predictor.host_copy = None
        return EMBED


def _key(col: int) -> tuple:
    return ("RC1", 7, "TEM", "grid16", "volume", 1, 0, col)


def _cache(*, gpu_max: int, host_max: int = 10_000, clock=None, idle: float = 0.0, tiers=None, released=None):
    tiers = tiers or FakeTiers()
    return ImageCache(
        max_memory_bytes=host_max,
        ttl_seconds=60,
        create_predictor_func=lambda data, *rest: FakePredictor(),
        release_predictor_func=(released.append if released is not None else None),
        time_fn=clock or FakeClock(),
        embedding_tiers=tiers,
        gpu_max_bytes=gpu_max,
        tile_idle_seconds=idle,
    ), tiers


async def _upload(cache: ImageCache, col: int, clock: FakeClock | None = None) -> int:
    if clock is not None:
        clock.advance(1)
    image_id, _cached = await cache.upload_tile(_key(col), b"0123456789", 1024, 1024)
    return image_id


@pytest.mark.asyncio
async def test_the_least_recently_used_embedding_moves_to_host_memory_past_the_gpu_cap() -> None:
    clock = FakeClock()
    cache, tiers = _cache(gpu_max=2 * EMBED + 50, clock=clock)
    first = await _upload(cache, 0, clock)
    await _upload(cache, 1, clock)
    await _upload(cache, 2, clock)

    stats = await cache.get_stats()
    assert stats["gpu_embedding_bytes"] == 2 * EMBED
    assert stats["host_embedding_bytes"] == EMBED
    assert stats["gpu_resident_images"] == 2 and stats["host_resident_images"] == 1
    assert stats["total_memory_bytes"] == 3 * 10 + EMBED, "host memory counts the tiles' bytes and the moved embedding"
    assert stats["embedding_moves"] == 1 and tiers.exports == 1
    assert cache._cache[first].offloaded, "the oldest went"


@pytest.mark.asyncio
async def test_a_request_pulls_its_embedding_back_and_the_pull_is_logged(caplog) -> None:
    clock = FakeClock()
    cache, tiers = _cache(gpu_max=2 * EMBED, clock=clock)
    first = await _upload(cache, 0, clock)
    await _upload(cache, 1, clock)
    await _upload(cache, 2, clock)
    clock.advance(30)

    with caplog.at_level(logging.INFO, logger="segmentation_server.image_cache"):
        pinned = await cache.get_image_by_tile(_key(0))
    assert pinned is not None
    predictor = pinned[4]
    assert predictor.on_gpu and tiers.restores == 1
    assert "Embedding pulled from host memory: ID=%s RC1 z=7 ds=1 row=0 col=0, idle 32 s" % first in caplog.text
    await cache.release_image(first)

    stats = await cache.get_stats()
    assert stats["embedding_pulls"] == 1
    assert stats["gpu_embedding_bytes"] == 2 * EMBED, "pulling one back moved the next least recently used out"
    assert stats["host_embedding_bytes"] == EMBED


@pytest.mark.asyncio
async def test_embeddings_in_use_stay_on_the_gpu_over_the_cap() -> None:
    clock = FakeClock()
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock)
    first = await _upload(cache, 0, clock)
    second = await _upload(cache, 1, clock)  # moves the first out
    pinned_first = await cache.get_image(first)  # pulls it back, moves the second out
    pinned_second = await cache.get_image(second)  # pulls the second back: both pinned
    assert pinned_first[3].on_gpu and pinned_second[3].on_gpu
    stats = await cache.get_stats()
    assert stats["gpu_embedding_bytes"] == 2 * EMBED, "nothing idle to move while both are in use"
    await cache.release_image(first)
    await cache.release_image(second)
    await _upload(cache, 2, clock)
    stats = await cache.get_stats()
    assert stats["gpu_embedding_bytes"] == EMBED, "once released, the cap applies again"


@pytest.mark.asyncio
async def test_host_memory_cap_drops_the_least_recently_used_entry() -> None:
    clock = FakeClock()
    released: list = []
    cache, _tiers = _cache(gpu_max=EMBED, host_max=2 * EMBED + 50, clock=clock, released=released)
    first = await _upload(cache, 0, clock)
    await _upload(cache, 1, clock)
    await _upload(cache, 2, clock)  # host: 3 tiles (30) + 2 moved embeddings (200) = 230 <= 250
    assert first in cache._cache
    await _upload(cache, 3, clock)  # a third moved embedding would make 340: the oldest entry goes

    assert first not in cache._cache
    stats = await cache.get_stats()
    assert stats["total_memory_bytes"] <= 2 * EMBED + 50
    assert stats["host_embedding_bytes"] == 2 * EMBED and stats["gpu_embedding_bytes"] == EMBED
    assert len(released) == 1, "the dropped entry's predictor was released"


@pytest.mark.asyncio
async def test_a_tile_idle_for_an_hour_leaves_both_tiers() -> None:
    clock = FakeClock()
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock, idle=3600)
    await _upload(cache, 0, clock)
    second = await _upload(cache, 1, clock)
    clock.advance(1800)
    pinned = await cache.get_image(second)  # used half an hour later: stays
    await cache.release_image(second)
    clock.advance(1801)

    assert await cache.get_image_by_tile(_key(0)) is None, "unused for over an hour"
    stats = await cache.get_stats()
    assert stats["total_images"] == 1
    assert stats["host_embedding_bytes"] == 0 and stats["gpu_embedding_bytes"] == EMBED
    assert pinned is not None


@pytest.mark.asyncio
async def test_without_an_idle_limit_tiles_stay() -> None:
    clock = FakeClock()
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock, idle=0)
    await _upload(cache, 0, clock)
    clock.advance(10 * 3600)
    pinned = await cache.get_image_by_tile(_key(0))
    assert pinned is not None
    await cache.release_image(pinned[0])


@pytest.mark.asyncio
async def test_a_burst_of_pulls_is_reported_as_thrashing(caplog, monkeypatch) -> None:
    monkeypatch.setattr(image_cache_module, "THRASH_PULLS", 3)
    clock = FakeClock()
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock)
    first = await _upload(cache, 0, clock)
    second = await _upload(cache, 1, clock)
    with caplog.at_level(logging.INFO, logger="segmentation_server.image_cache"):
        for _ in range(2):  # each use pulls one back and moves the other out
            for image_id in (first, second):
                assert await cache.get_image(image_id) is not None
                await cache.release_image(image_id)
    assert "GPU tile cache is thrashing: 3 embeddings pulled from host memory" in caplog.text
    assert caplog.text.count("thrashing") == 1, "warned once per window"


@pytest.mark.asyncio
async def test_deleting_a_moved_entry_clears_its_host_bytes() -> None:
    clock = FakeClock()
    released: list = []
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock, released=released)
    first = await _upload(cache, 0, clock)
    await _upload(cache, 1, clock)
    await cache.clear()
    stats = await cache.get_stats()
    assert stats["host_embedding_bytes"] == 0 and stats["gpu_embedding_bytes"] == 0
    assert stats["total_memory_bytes"] == 0
    assert len(released) == 2 and first not in cache._cache


@pytest.mark.asyncio
async def test_a_failed_pull_drops_the_entry_as_if_it_was_not_cached() -> None:
    class BrokenRestore(FakeTiers):
        def restore(self, predictor):
            raise RuntimeError("copy failed")

    clock = FakeClock()
    cache, _tiers = _cache(gpu_max=EMBED, clock=clock, tiers=BrokenRestore())
    first = await _upload(cache, 0, clock)
    await _upload(cache, 1, clock)
    assert await cache.get_image(first) is None
    assert first not in cache._cache
    stats = await cache.get_stats()
    assert stats["host_embedding_bytes"] == 0
