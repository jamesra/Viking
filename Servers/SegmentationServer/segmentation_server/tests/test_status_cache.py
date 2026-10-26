"""GetServerStatus reuses one answer for a second, so clients polling it every second cost the server a fixed amount."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer


class _Clock:
    def __init__(self) -> None:
        self.now = 100.0

    def __call__(self) -> float:
        return self.now


def _servicer(clock: _Clock, seconds: float = 1.0) -> tuple[SegmentationServicer, MagicMock]:
    model = MagicMock()
    model.compile_status = "ready"
    servicer = SegmentationServicer(
        model=model,
        image_cache=ImageCache(max_memory_bytes=1024, ttl_seconds=60),
        server_start_time=0.0,
        status_cache_seconds=seconds,
        clock=clock,
    )
    return servicer, model


async def _status(servicer: SegmentationServicer):
    return await servicer.GetServerStatus(MagicMock(), AsyncMock())


@pytest.mark.asyncio
async def test_calls_within_a_second_get_the_same_answer() -> None:
    clock = _Clock()
    servicer, _ = _servicer(clock)

    first = await _status(servicer)
    servicer._load.begin()
    clock.now += 0.9
    second = await _status(servicer)

    assert second is first
    assert second.in_flight_requests == 0


@pytest.mark.asyncio
async def test_after_a_second_the_answer_is_rebuilt() -> None:
    clock = _Clock()
    servicer, _ = _servicer(clock)

    first = await _status(servicer)
    servicer._load.begin()
    clock.now += 1.0
    second = await _status(servicer)

    assert second is not first
    assert second.in_flight_requests == 1


@pytest.mark.asyncio
async def test_a_compile_state_change_is_not_hidden_by_the_cache() -> None:
    clock = _Clock()
    servicer, model = _servicer(clock)
    model.compile_status = "warming"

    first = await _status(servicer)
    model.compile_status = "ready"
    clock.now += 0.1
    second = await _status(servicer)

    assert (first.compile_status, second.compile_status) == ("warming", "ready")


@pytest.mark.asyncio
async def test_zero_seconds_turns_the_cache_off() -> None:
    clock = _Clock()
    servicer, _ = _servicer(clock, seconds=0.0)

    first = await _status(servicer)
    second = await _status(servicer)

    assert second is not first


@pytest.mark.asyncio
async def test_a_clock_that_goes_backwards_rebuilds_the_answer() -> None:
    clock = _Clock()
    servicer, _ = _servicer(clock)

    first = await _status(servicer)
    clock.now -= 5.0
    second = await _status(servicer)

    assert second is not first
