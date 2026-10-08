"""ImageCache unit tests that do not require SAM2 or torch."""

from __future__ import annotations

import pytest

from segmentation_server.cuda_errors import UnrecoverableGpuError
from segmentation_server.image_cache import CacheFullError, ImageCache, PredictorCreationError


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
    await cache.release_image(image_id)


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
    await cache.release_image(second)


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
async def test_unrecoverable_gpu_error_propagates_and_clears_entry() -> None:
    def boom(_image_data: bytes):
        raise UnrecoverableGpuError("CUDA context lost")

    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=boom)
    with pytest.raises(UnrecoverableGpuError):
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
    await cache.release_image(image_id)


@pytest.mark.asyncio
async def test_get_stats_reports_occupancy() -> None:
    cache = ImageCache(max_memory_bytes=100, ttl_seconds=60)
    await cache.upload_image(b"12345", 1, 1)
    stats = await cache.get_stats()
    assert stats["total_images"] == 1
    assert stats["total_memory_bytes"] == 5
    assert stats["max_memory_bytes"] == 100
    assert stats["max_entries"] == 4096


@pytest.mark.asyncio
async def test_get_image_refreshes_ttl() -> None:
    clock = FakeClock()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=10, time_fn=clock)
    image_id = await cache.upload_image(b"x", 1, 1)
    clock.advance(5)
    assert await cache.get_image(image_id) is not None
    await cache.release_image(image_id)
    clock.advance(6)
    assert await cache.get_image(image_id) is not None
    await cache.release_image(image_id)
    clock.advance(11)
    assert await cache.get_image(image_id) is None


@pytest.mark.asyncio
async def test_max_entries_evicts_lru_when_byte_cap_has_room() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, max_entries=2)
    first = await cache.upload_image(b"a", 1, 1)
    second = await cache.upload_image(b"b", 1, 1)
    third = await cache.upload_image(b"c", 1, 1)
    assert await cache.get_image(first) is None
    assert await cache.get_image(second) is not None
    await cache.release_image(second)
    assert await cache.get_image(third) is not None
    await cache.release_image(third)


@pytest.mark.asyncio
async def test_release_predictor_on_delete() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: f"pred-{len(data)}",
        release_predictor_func=released.append,
    )
    image_id = await cache.upload_image(b"ab", 1, 1)
    assert await cache.delete_image(image_id) is True
    assert released == ["pred-2"]


@pytest.mark.asyncio
async def test_release_predictor_on_lru_eviction() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        max_entries=1,
        create_predictor_func=lambda data: data,
        release_predictor_func=released.append,
    )
    await cache.upload_image(b"first", 1, 1)
    await cache.upload_image(b"second", 1, 1)
    assert released == [b"first"]


@pytest.mark.asyncio
async def test_release_predictor_on_ttl_expiry() -> None:
    clock = FakeClock()
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=10,
        time_fn=clock,
        create_predictor_func=lambda data: "pred",
        release_predictor_func=released.append,
    )
    image_id = await cache.upload_image(b"x", 1, 1)
    clock.advance(11)
    assert await cache.get_image(image_id) is None
    assert released == ["pred"]


@pytest.mark.asyncio
async def test_clear_releases_idle_predictors() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: data,
        release_predictor_func=released.append,
    )
    await cache.upload_image(b"one", 1, 1)
    await cache.upload_image(b"two", 1, 1)
    await cache.clear()
    assert released == [b"one", b"two"]
    stats = await cache.get_stats()
    assert stats["total_images"] == 0
    assert stats["total_memory_bytes"] == 0


@pytest.mark.asyncio
async def test_clear_defers_pinned_predictor_reset() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: data,
        release_predictor_func=released.append,
    )
    image_id = await cache.upload_image(b"pinned", 1, 1)
    assert await cache.get_image(image_id) is not None
    await cache.clear()
    assert released == []
    assert await cache.get_image(image_id) is None
    await cache.release_image(image_id)
    assert released == [b"pinned"]


@pytest.mark.asyncio
async def test_pinned_image_not_ttl_expired() -> None:
    clock = FakeClock()
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=10,
        time_fn=clock,
        create_predictor_func=lambda data: "pred",
        release_predictor_func=released.append,
    )
    image_id = await cache.upload_image(b"x", 1, 1)
    assert await cache.get_image(image_id) is not None
    clock.advance(11)
    # Cleanup runs on this get; pinned entry must survive.
    assert await cache.get_image(image_id) is not None
    assert released == []
    await cache.release_image(image_id)
    await cache.release_image(image_id)


@pytest.mark.asyncio
async def test_pinned_image_not_lru_evicted() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        max_entries=1,
        create_predictor_func=lambda data: data,
        release_predictor_func=released.append,
    )
    first = await cache.upload_image(b"first", 1, 1)
    assert await cache.get_image(first) is not None
    second = await cache.upload_image(b"second", 1, 1)
    # Still pinned: not evicted, and second was still accepted (temporary over-capacity).
    assert await cache.get_image(first) is not None
    assert released == []
    await cache.release_image(first)
    await cache.release_image(first)
    assert await cache.get_image(second) is not None
    await cache.release_image(second)


@pytest.mark.asyncio
async def test_delete_while_pinned_defers_predictor_reset() -> None:
    released: list[object] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: "pred",
        release_predictor_func=released.append,
    )
    image_id = await cache.upload_image(b"x", 1, 1)
    assert await cache.get_image(image_id) is not None
    assert await cache.delete_image(image_id) is True
    assert released == []
    assert await cache.get_image(image_id) is None
    await cache.release_image(image_id)
    assert released == ["pred"]

_TILE_KEY = ("vol", 3, "TEM", "none|rigid", 1, 0, 1)


@pytest.mark.asyncio
async def test_upload_tile_identical_bytes_skips_new_predictor() -> None:
    created: list[bytes] = []

    def create(data: bytes) -> str:
        created.append(data)
        return f"pred-{len(created)}"

    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=create)
    first_id, already = await cache.upload_tile(_TILE_KEY, b"png", 8, 8)
    assert already is False
    second_id, already_again = await cache.upload_tile(_TILE_KEY, b"png", 8, 8)
    assert already_again is True
    assert second_id == first_id
    assert created == [b"png"]
    pinned = await cache.get_image_by_tile(_TILE_KEY)
    assert pinned is not None
    assert pinned[0] == first_id
    await cache.release_image(first_id)


@pytest.mark.asyncio
async def test_upload_tile_coord_key_shared_across_callers() -> None:
    """Same TileCoord from independent callers reuses one encoded entry (cross-client)."""
    created: list[bytes] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: created.append(data) or f"pred-{len(created)}",
    )
    client_a_id, a_hit = await cache.upload_tile(_TILE_KEY, b"shared-png", 8, 8)
    client_b_id, b_hit = await cache.upload_tile(_TILE_KEY, b"shared-png", 8, 8)
    assert a_hit is False
    assert b_hit is True
    assert client_a_id == client_b_id
    assert created == [b"shared-png"]
    other_key = ("vol", 3, "TEM", "none|rigid", 1, 0, 2)
    other_id, other_hit = await cache.upload_tile(other_key, b"shared-png", 8, 8)
    assert other_hit is False
    assert other_id != client_a_id
    assert created == [b"shared-png", b"shared-png"]
    await cache.release_image(client_a_id)
    await cache.release_image(other_id)


@pytest.mark.asyncio
async def test_upload_tile_new_bytes_replace_entry() -> None:
    created: list[bytes] = []
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda data: created.append(data) or data,
    )
    await cache.upload_tile(_TILE_KEY, b"old", 8, 8)
    image_id, already = await cache.upload_tile(_TILE_KEY, b"new", 8, 8)
    assert already is False
    pinned = await cache.get_image_by_tile(_TILE_KEY)
    assert pinned is not None
    assert pinned[0] == image_id
    assert pinned[1] == b"new"
    await cache.release_image(image_id)
    assert created == [b"old", b"new"]


def _tile_key(row: int) -> tuple:
    return ("vol", 1, "TEM", "grid", 1, row, 0)


@pytest.mark.asyncio
async def test_shared_tiles_ignore_entry_cap_and_ttl() -> None:
    clock = FakeClock()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=10, max_entries=1, time_fn=clock)
    first, _hit = await cache.upload_tile(_tile_key(0), b"a", 1, 1)
    second, _hit = await cache.upload_tile(_tile_key(1), b"b", 1, 1)
    clock.advance(30)
    assert await cache.get_image(first) is not None
    await cache.release_image(first)
    assert await cache.get_image(second) is not None
    await cache.release_image(second)


@pytest.mark.asyncio
async def test_entry_cap_does_not_evict_a_shared_tile() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, max_entries=1)
    tile_id, _hit = await cache.upload_tile(_tile_key(0), b"tile", 1, 1)
    first = await cache.upload_image(b"one", 1, 1)
    second = await cache.upload_image(b"two", 1, 1)
    assert await cache.get_image(tile_id) is not None
    await cache.release_image(tile_id)
    assert await cache.get_image(first) is None
    assert await cache.get_image(second) is not None
    await cache.release_image(second)


@pytest.mark.asyncio
async def test_shared_tile_client_delete_is_ignored_until_memory_is_needed() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, max_entries=1)
    tile_id, _hit = await cache.upload_tile(_tile_key(0), b"tile", 1, 1)
    assert await cache.delete_image(tile_id) is True
    pinned = await cache.get_image_by_tile(_tile_key(0))
    assert pinned is not None
    await cache.release_image(pinned[0])

    pressure = {"on": False}
    pressured = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        gpu_under_pressure=lambda: pressure["on"] is True,
    )
    tile_id, _hit = await pressured.upload_tile(_tile_key(0), b"tile", 1, 1)
    pressure["on"] = True
    assert await pressured.delete_image(tile_id) is True
    assert await pressured.get_image_by_tile(_tile_key(0)) is None


@pytest.mark.asyncio
async def test_shared_tiles_evict_when_byte_cap_or_gpu_is_hit() -> None:
    cache = ImageCache(max_memory_bytes=2, ttl_seconds=60, max_entries=8)
    first, _hit = await cache.upload_tile(_tile_key(0), b"ab", 1, 1)
    _second, _hit = await cache.upload_tile(_tile_key(1), b"cd", 1, 1)
    assert await cache.get_image(first) is None

    pressure = {"on": False}
    gpu_cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        max_entries=8,
        gpu_under_pressure=lambda: pressure["on"] is True,
    )
    kept, _hit = await gpu_cache.upload_tile(_tile_key(0), b"a", 1, 1)
    pressure["on"] = True
    _new, _hit = await gpu_cache.upload_tile(_tile_key(1), b"b", 1, 1)
    assert await gpu_cache.get_image(kept) is None


@pytest.mark.asyncio
async def test_arbitrary_uploads_also_yield_to_gpu_pressure() -> None:
    """A large entry cap must not be what protects the GPU: ad-hoc uploads yield to pressure too."""
    pressure = {"on": False}
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        max_entries=4096,
        gpu_under_pressure=lambda: pressure["on"] is True,
    )
    kept = await cache.upload_image(b"a", 1, 1)
    pressure["on"] = True
    await cache.upload_image(b"b", 1, 1)
    assert await cache.get_image(kept) is None


def test_defaults_hold_thousands_of_entries() -> None:
    from segmentation_server.image_cache import DEFAULT_MAX_ENTRIES, DEFAULT_MAX_MEMORY_BYTES

    assert DEFAULT_MAX_ENTRIES >= 4096
    assert DEFAULT_MAX_MEMORY_BYTES >= 16 * 1024**3


class _Gen:
    """A stand-in predictor that carries an encoder generation."""

    def __init__(self, generation):
        self.generation = generation
        self.reset = False


def _generation_cache(state, **kwargs):
    released: list[_Gen] = []

    def create(_data: bytes):
        return _Gen(state["generation"])

    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=create,
        release_predictor_func=released.append,
        predictor_generation=lambda predictor: predictor.generation,
        current_generation=lambda: state["generation"],
        **kwargs,
    )
    return cache, released


@pytest.mark.asyncio
async def test_flush_stale_drops_only_entries_made_with_the_replaced_encoder() -> None:
    state = {"generation": "eager"}
    cache, released = _generation_cache(state)
    old_a = await cache.upload_image(b"a", 1, 1)
    old_b = await cache.upload_image(b"b", 1, 1)
    state["generation"] = "compiled"
    fresh = await cache.upload_image(b"c", 1, 1)

    dropped = await cache.flush_stale()

    assert dropped == 2
    assert await cache.get_image(old_a) is None
    assert await cache.get_image(old_b) is None
    kept = await cache.get_image(fresh)
    assert kept is not None and kept[3].generation == "compiled"
    assert [p.generation for p in released] == ["eager", "eager"]


@pytest.mark.asyncio
async def test_flush_stale_retires_a_pinned_stale_entry_after_its_last_release() -> None:
    state = {"generation": "eager"}
    cache, released = _generation_cache(state)
    image_id = await cache.upload_image(b"a", 1, 1)
    pinned = await cache.get_image(image_id)
    state["generation"] = "compiled"

    await cache.flush_stale()

    assert released == []
    assert pinned[3].generation == "eager"
    await cache.release_image(image_id)
    assert [p.generation for p in released] == ["eager"]


@pytest.mark.asyncio
async def test_flush_stale_keeps_entries_that_report_no_generation_or_have_no_predictor() -> None:
    state = {"generation": "eager"}
    cache, _released = _generation_cache(state)
    unknown = await cache.upload_image(b"a", 1, 1)
    cached = cache._cache[unknown]
    cached.predictor.generation = None
    state["generation"] = "compiled"

    assert await cache.flush_stale() == 0
    assert await cache.get_image(unknown) is not None


@pytest.mark.asyncio
async def test_flush_stale_is_a_noop_without_generation_functions() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    await cache.upload_image(b"a", 1, 1)
    assert await cache.flush_stale() == 0


@pytest.mark.asyncio
async def test_a_predictor_built_across_a_swap_is_built_again_on_the_new_encoder() -> None:
    state = {"generation": "eager"}
    builds: list[str] = []
    released: list[_Gen] = []

    def create(_data: bytes):
        builds.append(state["generation"])
        predictor = _Gen(state["generation"])
        if len(builds) == 1:
            state["generation"] = "compiled"
        return predictor

    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=create,
        release_predictor_func=released.append,
        predictor_generation=lambda predictor: predictor.generation,
        current_generation=lambda: state["generation"],
    )

    image_id = await cache.upload_image(b"a", 1, 1)

    assert builds == ["eager", "compiled"]
    assert [p.generation for p in released] == ["eager"]
    pinned = await cache.get_image(image_id)
    assert pinned[3].generation == "compiled"

@pytest.mark.asyncio
async def test_bytes_of_a_retiring_entry_still_count_toward_the_cap() -> None:
    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60, create_predictor_func=lambda d: d)
    held = await cache.upload_image(b"12345678", 1, 1)
    assert await cache.get_image(held) is not None  # a request pins it
    assert await cache.delete_image(held) is True  # deleted while pinned: retiring

    stats = await cache.get_stats()
    assert stats["total_images"] == 0
    assert stats["retiring_images"] == 1
    assert stats["retiring_bytes"] == 8
    assert stats["total_memory_bytes"] == 8  # the bytes are still in memory

    with pytest.raises(CacheFullError):
        await cache.upload_image(b"abcd", 1, 1)  # 8 + 4 > 10, and nothing idle can be evicted

    await cache.release_image(held)
    stats = await cache.get_stats()
    assert stats["retiring_images"] == 0 and stats["total_memory_bytes"] == 0
    assert await cache.upload_image(b"abcd", 1, 1) > 0


@pytest.mark.asyncio
async def test_an_upload_that_cannot_fit_because_everything_is_pinned_is_refused() -> None:
    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60, create_predictor_func=lambda d: d)
    first = await cache.upload_image(b"123456", 1, 1)
    assert await cache.get_image(first) is not None

    with pytest.raises(CacheFullError, match="byte cap"):
        await cache.upload_image(b"abcdef", 1, 1)

    assert list(cache._cache) == [first]
    await cache.release_image(first)
    second = await cache.upload_image(b"abcdef", 1, 1)  # now the idle one can be evicted
    assert first not in cache._cache and second in cache._cache


@pytest.mark.asyncio
async def test_an_image_bigger_than_the_whole_cap_is_refused() -> None:
    cache = ImageCache(max_memory_bytes=4, ttl_seconds=60)
    with pytest.raises(CacheFullError):
        await cache.upload_image(b"too big", 1, 1)
    assert (await cache.get_stats())["total_images"] == 0


@pytest.mark.asyncio
async def test_a_refused_upload_does_not_count_its_bytes() -> None:
    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60)
    kept = await cache.upload_image(b"12345", 1, 1)
    assert await cache.get_image(kept) is not None
    with pytest.raises(CacheFullError):
        await cache.upload_image(b"123456", 1, 1)
    assert (await cache.get_stats())["total_memory_bytes"] == 5


@pytest.mark.asyncio
async def test_concurrent_uploads_never_hold_more_than_the_cap() -> None:
    import asyncio

    cache = ImageCache(max_memory_bytes=40, ttl_seconds=60, create_predictor_func=lambda d: d)

    async def upload(n: int) -> None:
        try:
            await cache.upload_image(bytes([n]) * 10, 1, 1)
        except (CacheFullError, PredictorCreationError):
            pass  # refused, or evicted by a later upload before its predictor was built

    await asyncio.gather(*(upload(n) for n in range(30)))

    stats = await cache.get_stats()
    assert stats["total_memory_bytes"] <= 40
    assert stats["total_memory_bytes"] == sum(c.size_bytes for c in cache._cache.values())

@pytest.mark.asyncio
async def test_a_decoded_image_goes_to_the_predictor_factory_and_is_not_kept() -> None:
    seen: list[tuple] = []
    decoded = object()

    def factory(*args):
        seen.append(args)
        return "predictor"

    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=factory)
    image_id = await cache.upload_image(b"png", 2, 2, decoded=decoded)
    await cache.upload_image(b"png2", 2, 2)

    assert seen[0] == (b"png", decoded)
    assert seen[1] == (b"png2",)
    cached = cache._cache[image_id]
    assert decoded not in vars(cached).values()


@pytest.mark.asyncio
async def test_a_decoded_tile_reaches_the_predictor_factory() -> None:
    seen: list[tuple] = []
    decoded = object()
    cache = ImageCache(
        max_memory_bytes=1024, ttl_seconds=60,
        create_predictor_func=lambda *args: seen.append(args) or "predictor",
    )

    await cache.upload_tile(_TILE_KEY, b"tile", 1024, 1024, decoded=decoded)

    assert seen == [(b"tile", decoded)]
