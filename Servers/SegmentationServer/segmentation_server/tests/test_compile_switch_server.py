"""The servicer side of the eager-to-compiled switch: flush, status fields."""

from __future__ import annotations

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from segmentation_grpc import ServerStatusRequest
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.server import SegmentationServicer


class _Model:
    """Reports an encoder generation the way SegmentationModel does."""

    device = "cuda"
    capabilities = SAM2_CAPABILITIES
    compile_status = "warming"

    def __init__(self) -> None:
        self.generation = "eager"
        self.callback = None
        self.created = 0
        self.released = []

    @property
    def encoder_generation(self) -> str:
        return self.generation

    @staticmethod
    def predictor_generation(predictor):
        return getattr(predictor, "generation", None)

    def set_on_compiled_ready(self, callback) -> None:
        self.callback = callback

    def create_initialized_predictor(self, _data: bytes):
        self.created += 1
        return SimpleNamespace(generation=self.generation)

    def release_predictor(self, predictor) -> None:
        self.released.append(predictor)

    def swap(self) -> None:
        self.generation = "compiled"
        self.compile_status = "ready"
        self.callback()


def _build(model: _Model) -> SegmentationServicer:
    return SegmentationServicer(model=model, server_start_time=0.0)


@pytest.mark.asyncio
async def test_swap_flushes_eager_entries_but_keeps_ones_made_after_it() -> None:
    model = _Model()
    servicer = _build(model)
    cache = servicer.image_cache
    before = await cache.upload_image(b"a", 1, 1)

    await asyncio.get_running_loop().run_in_executor(None, model.swap)
    for _ in range(50):
        if await cache.get_image(before) is None:
            break
        await cache.release_image(before)
        await asyncio.sleep(0.01)
    after = await cache.upload_image(b"b", 1, 1)

    assert await cache.get_image(before) is None
    kept = await cache.get_image(after)
    assert kept is not None and kept[3].generation == "compiled"


@pytest.mark.asyncio
async def test_status_reports_the_encoder_generation_and_compile_state() -> None:
    model = _Model()
    servicer = _build(model)

    first = await servicer.GetServerStatus(ServerStatusRequest(), AsyncMock())
    await asyncio.get_running_loop().run_in_executor(None, model.swap)
    second = await servicer.GetServerStatus(ServerStatusRequest(), AsyncMock())

    assert (first.encoder_generation, first.compile_status) == ("eager", "warming")
    assert (second.encoder_generation, second.compile_status) == ("compiled", "ready")


@pytest.mark.asyncio
async def test_a_model_without_generations_leaves_the_fields_empty_and_flushes_nothing() -> None:
    from unittest.mock import MagicMock

    model = MagicMock()
    model.capabilities = SAM2_CAPABILITIES
    servicer = SegmentationServicer(model=model, server_start_time=0.0)

    status = await servicer.GetServerStatus(ServerStatusRequest(), AsyncMock())

    assert status.encoder_generation == ""
    assert status.compile_status == "off"
    assert await servicer.image_cache.flush_stale() == 0

