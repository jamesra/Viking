"""UploadTile at the servicer: validation, status codes, and what is left behind."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from protocol import cell, grid
from segmentation_grpc import CoordinateSpace, UploadTileRequest
from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer


def _png(width: int, height: int) -> bytes:
    ok, encoded = cv2.imencode(".png", np.zeros((height, width, 3), dtype=np.uint8))
    assert ok
    return encoded.tobytes()


def _servicer() -> SegmentationServicer:
    model = MagicMock()
    cache = ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        create_predictor_func=lambda *args: object(),
    )
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


def _tile(**changes) -> UploadTileRequest:
    fields = dict(
        grid=grid(volume="v", section=1, channel="c", transform="t"),
        cell=cell(0, 0),
        image_data=_png(1024, 1024),
    )
    fields.update(changes)
    return UploadTileRequest(**fields)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "changes,fragment",
    [
        (dict(image_data=_png(512, 512)), "1024x1024 image, got 512x512"),
        (dict(image_data=b""), "non-empty image_data"),
        (dict(image_data=b"garbage"), "Could not decode"),
        (dict(image_data=_png(1024, 1023)), "got 1024x1023"),
    ],
    ids=["wrong-tile-size", "no-bytes", "garbage", "one-row-short"],
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
    request = _tile(grid=grid(downsample=0))

    await servicer.UploadTile(request, context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert "downsample" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_upload_tile_needs_a_mosaic_or_volume_space() -> None:
    servicer = _servicer()
    context = AsyncMock()

    await servicer.UploadTile(_tile(grid=grid(space=CoordinateSpace.COORDINATE_SPACE_UNSPECIFIED)), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert "MOSAIC or VOLUME" in context.abort.await_args.args[1]


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
async def test_upload_tile_with_other_bytes_keeps_the_cached_tile() -> None:
    servicer = _servicer()
    first = _tile()
    await servicer.UploadTile(first, AsyncMock())
    other = np.full((1024, 1024, 3), 7, dtype=np.uint8)
    ok, encoded = cv2.imencode(".png", other)

    response = await servicer.UploadTile(_tile(image_data=encoded.tobytes()), AsyncMock())

    assert response.already_cached is True
    assert (await servicer.image_cache.get_stats())["total_images"] == 1
    key = ("v", 1, "c", "t", "volume", 1, 0, 0)
    pinned = await servicer.image_cache.get_image_by_tile(key)
    assert pinned is not None and pinned[1] == first.image_data
    await servicer.image_cache.release_image(pinned[0])
