"""Dispatch tests: cached path skips set_image(); inline path does not."""

from __future__ import annotations

from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import grpc
import numpy as np
import pytest

from segmentation_server.image_cache import ImageCache
from segmentation_server.server import SegmentationServicer


def _empty_result() -> tuple[np.ndarray, list]:
    return np.zeros((2, 2), dtype=np.uint16), []


def _servicer_with_cache(cache: ImageCache, model: MagicMock | None = None) -> SegmentationServicer:
    if model is None:
        model = MagicMock()
        model.segment_image_with_predictor.return_value = _empty_result()
        model.segment_image.return_value = _empty_result()
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


@pytest.mark.asyncio
async def test_dispatch_cached_skips_inline_set_image() -> None:
    predictor = object()
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda _data: predictor,
    )
    image_id = await cache.upload_image(b"png", 2, 2)
    model = MagicMock()
    model.segment_image_with_predictor.return_value = _empty_result()
    model.segment_image.return_value = _empty_result()
    servicer = _servicer_with_cache(cache, model)

    response = await servicer._dispatch_segmentation(
        image_id=image_id,
        image_data=b"ignored",
        width=2,
        height=2,
        coordinates=[(1, 1)],
        labels=[1],
        multimask_output=False,
        context=AsyncMock(),
        empty_message="empty",
    )

    assert response.width == 2
    assert response.height == 2
    model.segment_image_with_predictor.assert_called_once()
    model.segment_image.assert_not_called()
    call_kwargs = model.segment_image_with_predictor.call_args.kwargs
    assert call_kwargs["predictor"] is predictor
    # Pin from get_image must be released after the RPC finishes.
    assert cache._cache[image_id].in_use == 0


@pytest.mark.asyncio
async def test_dispatch_inline_calls_segment_image() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    model.segment_image.return_value = _empty_result()
    servicer = _servicer_with_cache(cache, model)

    await servicer._dispatch_segmentation(
        image_id=0,
        image_data=b"png-bytes",
        width=2,
        height=2,
        coordinates=[(1, 1)],
        labels=[1],
        multimask_output=False,
        context=AsyncMock(),
        empty_message="empty",
    )

    model.segment_image.assert_called_once()
    model.segment_image_with_predictor.assert_not_called()


@pytest.mark.asyncio
async def test_dispatch_missing_id_aborts_not_found() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    servicer = _servicer_with_cache(cache)
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=99,
        image_data=b"",
        width=0,
        height=0,
        coordinates=[(1, 1)],
        labels=[1],
        multimask_output=False,
        context=context,
        empty_message="empty",
    )

    context.abort.assert_awaited()
    status = context.abort.await_args.args[0]
    assert status == grpc.StatusCode.NOT_FOUND
    servicer.model.segment_image_with_predictor.assert_not_called()
    servicer.model.segment_image.assert_not_called()


@pytest.mark.asyncio
async def test_status_message_includes_compile_state() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    model.device = "cuda"
    model.compile_status = "warming"
    servicer = _servicer_with_cache(cache, model)

    response = await servicer.GetServerStatus(MagicMock(), AsyncMock())

    assert "compile=warming" in response.message


def _point(x: int, y: int) -> SimpleNamespace:
    return SimpleNamespace(x=x, y=y)


def _set_request(image_id: int, foreground: list[tuple[int, int]], background: list[tuple[int, int]]):
    return SimpleNamespace(
        image_id=image_id,
        foreground=[_point(x, y) for x, y in foreground],
        background=[_point(x, y) for x, y in background],
        multimask_output=False,
    )


async def _stream(*items):
    for item in items:
        yield item


@pytest.mark.asyncio
async def test_segment_image_sets_yields_one_response_per_set() -> None:
    predictor = object()
    cache = ImageCache(
        max_memory_bytes=1024,
        ttl_seconds=60,
        create_predictor_func=lambda _data: predictor,
    )
    image_id = await cache.upload_image(b"png", 2, 2)
    model = MagicMock()
    model.segment_image_with_predictor.return_value = _empty_result()
    servicer = _servicer_with_cache(cache, model)

    responses = []
    async for response in servicer.SegmentImageSets(
        _stream(
            _set_request(image_id, [(1, 1)], [(0, 0)]),
            _set_request(0, [(2, 2)], []),
        ),
        AsyncMock(),
    ):
        responses.append(response)

    assert len(responses) == 2
    assert model.segment_image_with_predictor.call_count == 2
    first_labels = model.segment_image_with_predictor.call_args_list[0].kwargs["labels"]
    second_labels = model.segment_image_with_predictor.call_args_list[1].kwargs["labels"]
    assert first_labels == [1, 0]
    assert second_labels == [1]


@pytest.mark.asyncio
async def test_segment_image_sets_requires_image_id() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    servicer = _servicer_with_cache(cache)
    context = AsyncMock()

    responses = [
        response
        async for response in servicer.SegmentImageSets(
            _stream(_set_request(0, [(1, 1)], [])),
            context,
        )
    ]

    assert responses == []
    context.abort.assert_awaited()
    servicer.model.segment_image_with_predictor.assert_not_called()
