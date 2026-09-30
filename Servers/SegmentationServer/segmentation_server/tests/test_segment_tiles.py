"""SegmentTiles must not fail the RPC when an extra listed cell was evicted."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import grpc
import numpy as np
import pytest

from segmentation_grpc import Point, SegmentTilesRequest, TileCoord
from segmentation_server.image_cache import ImageCache
from segmentation_server.mask_utils import fill_small_holes, get_mask_bounds, mask_to_polygons
from segmentation_server.server import SegmentationServicer


def _servicer(cache: ImageCache) -> SegmentationServicer:
    model = MagicMock()
    mask = np.zeros((4, 4), dtype=np.bool_)
    mask[1, 1] = True
    model.predict_tile_union.return_value = (mask, None, 0.8)
    model.fill_small_holes.side_effect = fill_small_holes
    model.get_mask_bounds.side_effect = get_mask_bounds
    model.mask_to_polygons.side_effect = mask_to_polygons
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


def _tile(row: int, col: int) -> TileCoord:
    return TileCoord(
        volume="RC2",
        section=1305,
        channel="TEM",
        transform="SliceToVolume1|",
        downsample=1,
        row=row,
        col=col,
    )


@pytest.mark.asyncio
async def test_missing_tile_without_foreground_does_not_abort() -> None:
    predictor = object()
    cache = ImageCache(
        max_memory_bytes=10_000_000,
        ttl_seconds=60,
        max_entries=8,
        create_predictor_func=lambda _data: predictor,
    )
    await cache.upload_tile(("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 0), b"seed", 1024, 1024)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(
        tiles=[_tile(0, 0), _tile(0, 1)],
        foreground=[Point(x=10, y=10)],
    )
    context = AsyncMock()

    response = await servicer.SegmentTiles(request, context)

    context.abort.assert_not_awaited()
    servicer.model.predict_tile_union.assert_called_once()
    assert response.segments
    assert cache._cache
    assert all(entry.in_use == 0 for entry in cache._cache.values())


@pytest.mark.asyncio
async def test_missing_foreground_tile_is_not_found() -> None:
    predictor = object()
    cache = ImageCache(
        max_memory_bytes=10_000_000,
        ttl_seconds=60,
        max_entries=8,
        create_predictor_func=lambda _data: predictor,
    )
    await cache.upload_tile(("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 0), b"other", 1024, 1024)
    servicer = _servicer(cache)
    request = SegmentTilesRequest(
        tiles=[_tile(0, 0), _tile(0, 1)],
        foreground=[Point(x=1034, y=10)],
    )
    context = AsyncMock()

    await servicer.SegmentTiles(request, context)

    context.abort.assert_awaited()
    assert context.abort.await_args.args[0] == grpc.StatusCode.NOT_FOUND
    assert "row=0 col=1" in context.abort.await_args.args[1]
    servicer.model.predict_tile_union.assert_not_called()
    assert all(entry.in_use == 0 for entry in cache._cache.values())
