"""ImageCache unit tests that do not require SAM2 or torch."""

from __future__ import annotations

import pytest

from segmentation_server.image_cache import ImageCache, PredictorCreationError


class FakeClock:
    def __init__(self, start: float = 1000.0) -> None:
        self.now = start

    def __call__(self) -> float:
        return self.now

    def advance(self, seconds: float) -> None:
        self.now += seconds


@pytest.mark.asyncio
async def test_upload_assigns_sequential_ids() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    first = await cache.upload_image(b"aaa", 1, 1)
    second = await cache.upload_image(b"bbb", 1, 1)
    assert first == 1
    assert second == 2


@pytest.mark.asyncio
async def test_get_image_returns_payload_and_refreshes_entry() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    image_id = await cache.upload_image(b"png-bytes", 8, 4)
    result = await cache.get_image(image_id)
    assert result is not None
    data, width, height, predictor, lock = result
    assert data == b"png-bytes"
    assert width == 8
    assert height == 4
    assert predictor is None
    assert lock is not None


@pytest.mark.asyncio
async def test_delete_missing_returns_false() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    assert await cache.delete_image(99) is False


@pytest.mark.asyncio
async def test_lru_evicts_oldest_when_memory_exceeded() -> None:
    cache = ImageCache(max_memory_bytes=5, ttl_seconds=60)
    first = await cache.upload_image(b"aaaa", 1, 1)
    second = await cache.upload_image(b"bbbb", 1, 1)
    assert await cache.get_image(first) is None
    remaining = await cache.get_image(second)
    assert remaining is not None
    assert remaining[0] == b"bbbb"


@pytest.mark.asyncio
async def test_ttl_expires_unused_entries() -> None:
    clock = FakeClock()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=10, time_fn=clock)
    image_id = await cache.upload_image(b"x", 1, 1)
    clock.advance(11)
    assert await cache.get_image(image_id) is None


@pytest.mark.asyncio
async def test_failed_predictor_does_not_leave_cache_entry() -> None:
    def boom(_image_data: bytes):
        raise RuntimeError("cuda oom")

    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=boom)
    with pytest.raises(PredictorCreationError):
        await cache.upload_image(b"x", 1, 1)
    assert await cache.get_image(1) is None


@pytest.mark.asyncio
async def test_successful_predictor_is_stored() -> None:
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: f"pred-{len(data)}",
    )
    image_id = await cache.upload_image(b"abcd", 2, 2)
    result = await cache.get_image(image_id)
    assert result is not None
    assert result[3] == "pred-4"


@pytest.mark.asyncio
async def test_get_stats_reports_occupancy() -> None:
    cache = ImageCache(max_memory_bytes=100, ttl_seconds=60)
    await cache.upload_image(b"12345", 1, 1)
    stats = await cache.get_stats()
    assert stats["total_images"] == 1
    assert stats["total_memory_bytes"] == 5
    assert stats["max_memory_bytes"] == 100
