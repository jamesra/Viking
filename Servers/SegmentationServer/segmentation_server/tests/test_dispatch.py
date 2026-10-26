"""Upload and status handling outside the segmentation stream: decode once, refuse what the cache cannot hold."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from protocol import cell, grid
from segmentation_grpc import UploadTileRequest
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer


def _servicer_with_cache(cache: ImageCache, model: MagicMock | None = None) -> SegmentationServicer:
    return SegmentationServicer(model=model if model is not None else MagicMock(), image_cache=cache, server_start_time=0.0)


def _tile_request() -> UploadTileRequest:
    ok, encoded = cv2.imencode(".png", np.zeros((1024, 1024, 3), dtype=np.uint8))
    assert ok
    return UploadTileRequest(grid=grid(volume="v", section=1, channel="c", transform="t"), cell=cell(0, 0),
                             image_data=encoded.tobytes())


@pytest.mark.asyncio
async def test_status_message_includes_compile_state() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    model.device = "cuda"
    model.compile_status = "warming"
    servicer = _servicer_with_cache(cache, model)

    response = await servicer.GetServerStatus(MagicMock(), AsyncMock())

    assert "compile=warming" in response.message


@pytest.mark.asyncio
async def test_an_upload_tile_the_cache_cannot_hold_is_resource_exhausted() -> None:
    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60)
    servicer = _servicer_with_cache(cache)
    context = AsyncMock()

    response = await servicer.UploadTile(_tile_request(), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert response.already_cached is False


@pytest.mark.asyncio
async def test_an_uploaded_tile_is_decoded_once_and_the_pixels_reach_the_predictor_and_the_disk_touch(monkeypatch) -> None:
    from segmentation_server import server as server_module

    decodes: list[int] = []
    real = server_module.prepare_image_for_sam2
    monkeypatch.setattr(server_module, "prepare_image_for_sam2", lambda data: decodes.append(1) or real(data))
    seen: list[tuple] = []
    cache = ImageCache(
        max_memory_bytes=100_000_000, ttl_seconds=60,
        create_predictor_func=lambda *args: seen.append(args) or object(),
    )
    model = MagicMock()
    servicer = _servicer_with_cache(cache, model)
    request = _tile_request()

    await servicer.UploadTile(request, AsyncMock())
    await servicer.UploadTile(request, AsyncMock())  # identical bytes: a cache hit

    assert decodes == [1, 1]  # one decode per upload call, none for the predictor, none for the touch
    assert seen[0][1].shape == (1024, 1024, 3)
    model.note_disk_embedding_used.assert_called_once()
    assert model.note_disk_embedding_used.call_args.args[1].shape == (1024, 1024, 3)
