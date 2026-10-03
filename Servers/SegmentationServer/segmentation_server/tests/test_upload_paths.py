"""UploadImage and UploadTile at the servicer: validation, status codes, and what is left behind."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from segmentation_grpc import TileCoord, UploadImageRequest, UploadTileRequest
from segmentation_server.image_cache import ImageCache
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.server import SegmentationServicer


def _png(width: int, height: int) -> bytes:
    ok, encoded = cv2.imencode(".png", np.zeros((height, width, 3), dtype=np.uint8))
    assert ok
    return encoded.tobytes()


def _servicer() -> SegmentationServicer:
    model = MagicMock()
    model.capabilities = SAM2_CAPABILITIES
    cache = ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        create_predictor_func=lambda *args: object(),
    )
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


def _tile(**changes) -> UploadTileRequest:
    fields = dict(
        coord=TileCoord(volume="v", section=1, channel="c", transform="t", downsample=1, row=0, col=0),
        image_data=_png(1024, 1024),
        width=1024,
        height=1024,
    )
    fields.update(changes)
    return UploadTileRequest(**fields)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "request_fields,fragment",
    [
        (dict(image_data=b"", width=3, height=2), "non-empty image_data"),
        (dict(image_data=_png(3, 2), width=0, height=2), "positive width and height"),
        (dict(image_data=_png(3, 2), width=3, height=0), "positive width and height"),
        (dict(image_data=b"garbage", width=3, height=2), "Could not decode"),
        (dict(image_data=_png(3, 2), width=4, height=2), "do not match decoded image 3x2"),
    ],
    ids=["no-bytes", "no-width", "no-height", "garbage", "size-mismatch"],
)
async def test_upload_image_rejects_bad_input_without_touching_the_cache(request_fields, fragment) -> None:
    servicer = _servicer()
    context = AsyncMock()

    response = await servicer.UploadImage(UploadImageRequest(**request_fields), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert fragment in context.abort.await_args.args[1]
    assert response.image_id == 0
    assert (await servicer.image_cache.get_stats())["total_images"] == 0
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_upload_image_caches_the_image_and_returns_an_id() -> None:
    servicer = _servicer()

    response = await servicer.UploadImage(UploadImageRequest(image_data=_png(3, 2), width=3, height=2), AsyncMock())

    assert response.image_id > 0
    pinned = await servicer.image_cache.get_image(response.image_id)
    assert pinned is not None and (pinned[1], pinned[2]) == (3, 2)
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "changes,fragment",
    [
        (dict(width=512, height=512, image_data=_png(512, 512)), "1024x1024"),
        (dict(image_data=b""), "non-empty image_data"),
        (dict(image_data=b"garbage"), "Could not decode"),
        (dict(image_data=_png(1024, 1023)), "do not match decoded image"),
    ],
    ids=["wrong-tile-size", "no-bytes", "garbage", "size-mismatch"],
)
async def test_upload_tile_rejects_bad_input_without_touching_the_cache(changes, fragment) -> None:
    servicer = _servicer()
    context = AsyncMock()

    await servicer.UploadTile(_tile(**changes), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert fragment in context.abort.await_args.args[1]
    assert (await servicer.image_cache.get_stats())["total_images"] == 0


@pytest.mark.asyncio
async def test_upload_tile_needs_a_positive_downsample() -> None:
    servicer = _servicer()
    context = AsyncMock()
    request = _tile(coord=TileCoord(volume="v", section=1, channel="c", transform="t", downsample=0, row=0, col=0))

    await servicer.UploadTile(request, context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert "downsample" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_upload_tile_reports_a_repeat_of_the_same_bytes_as_already_cached() -> None:
    servicer = _servicer()
    context = AsyncMock()

    first = await servicer.UploadTile(_tile(), context)
    second = await servicer.UploadTile(_tile(), context)

    assert first.already_cached is False
    assert second.already_cached is True
    context.abort.assert_not_awaited()
    assert (await servicer.image_cache.get_stats())["total_images"] == 1
    assert servicer._load.snapshot()[0] == 0


@pytest.mark.asyncio
async def test_upload_tile_with_new_bytes_replaces_the_cached_tile() -> None:
    servicer = _servicer()
    await servicer.UploadTile(_tile(), AsyncMock())
    other = np.full((1024, 1024, 3), 7, dtype=np.uint8)
    ok, encoded = cv2.imencode(".png", other)

    response = await servicer.UploadTile(_tile(image_data=encoded.tobytes()), AsyncMock())

    assert response.already_cached is False
    assert (await servicer.image_cache.get_stats())["total_images"] == 1
