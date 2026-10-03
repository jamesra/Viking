"""Dispatch tests: cached path skips set_image(); inline path does not."""

from __future__ import annotations

from concurrent import futures
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from segmentation_server.image_cache import ImageCache
from segmentation_server.mask_utils import NoMatchingMask
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
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


def _png(width: int, height: int) -> bytes:
    ok, encoded = cv2.imencode(".png", np.zeros((height, width, 3), dtype=np.uint8))
    assert ok
    return encoded.tobytes()


@pytest.mark.asyncio
async def test_dispatch_inline_decodes_once_and_calls_segment_image_with_the_pixels() -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    model.segment_image.return_value = _empty_result()
    servicer = _servicer_with_cache(cache, model)

    await servicer._dispatch_segmentation(
        image_id=0,
        image_data=_png(3, 2),
        width=3,
        height=2,
        coordinates=[(1, 1)],
        labels=[1],
        multimask_output=False,
        context=AsyncMock(),
        empty_message="empty",
    )

    model.segment_image.assert_called_once()
    assert model.segment_image.call_args.kwargs["image_np"].shape == (2, 3, 3)
    model.segment_image_with_predictor.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "width,height,data",
    [(5, 2, _png(3, 2)), (3, 9, _png(3, 2)), (0, 0, _png(3, 2)), (3, 2, b""), (3, 2, b"not an image")],
    ids=["wrong-width", "wrong-height", "no-size", "no-bytes", "garbage"],
)
async def test_inline_image_with_a_wrong_declared_size_or_bad_bytes_is_invalid_argument(
    width: int, height: int, data: bytes
) -> None:
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60)
    model = MagicMock()
    servicer = _servicer_with_cache(cache, model)
    context = AsyncMock()

    response = await servicer._dispatch_segmentation(
        image_id=0,
        image_data=data,
        width=width,
        height=height,
        coordinates=[(1, 1)],
        labels=[1],
        multimask_output=False,
        context=context,
        empty_message="empty",
    )

    context.abort.assert_awaited()
    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert len(response.segments) == 0
    model.segment_image.assert_not_called()


@pytest.mark.asyncio
async def test_a_wrong_size_message_names_both_sizes() -> None:
    servicer = _servicer_with_cache(ImageCache(max_memory_bytes=1024, ttl_seconds=60))
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=0, image_data=_png(3, 2), width=5, height=2, coordinates=[(1, 1)], labels=[1],
        multimask_output=False, context=context, empty_message="empty",
    )

    message = context.abort.await_args.args[1]
    assert "5x2" in message and "3x2" in message


@pytest.mark.asyncio
async def test_no_matching_mask_is_failed_precondition_and_the_cached_image_is_kept() -> None:
    predictor = object()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=lambda _d: predictor)
    image_id = await cache.upload_image(b"png", 2, 2)
    model = MagicMock()
    model.segment_image_with_predictor.side_effect = NoMatchingMask("None of the 3 candidate mask(s) ...")
    servicer = _servicer_with_cache(cache, model)
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=image_id, image_data=b"", width=0, height=0, coordinates=[(1, 1)], labels=[1],
        multimask_output=True, context=context, empty_message="empty",
    )

    code, message = context.abort.await_args.args
    assert code == grpc.StatusCode.FAILED_PRECONDITION
    assert message.startswith("NO_MATCHING_MASK ")
    assert image_id in cache._cache
    assert cache._cache[image_id].in_use == 0


@pytest.mark.asyncio
async def test_no_matching_mask_on_the_inline_path_is_the_same_status() -> None:
    model = MagicMock()
    model.segment_image.side_effect = NoMatchingMask("nothing fits")
    servicer = _servicer_with_cache(ImageCache(max_memory_bytes=1024, ttl_seconds=60), model)
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=0, image_data=_png(2, 2), width=2, height=2, coordinates=[(1, 1)], labels=[1],
        multimask_output=False, context=context, empty_message="empty",
    )

    assert context.abort.await_args.args[0] == grpc.StatusCode.FAILED_PRECONDITION


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "error,evicted",
    [
        (RuntimeError("An image must be set with .set_image(...) before mask prediction."), True),
        (RuntimeError("CUDA out of memory"), False),
        (ValueError("bad prompt"), False),
    ],
    ids=["embedding-gone", "transient-gpu", "bad-prompt"],
)
async def test_only_a_lost_embedding_evicts_the_cached_image(error: Exception, evicted: bool) -> None:
    predictor = object()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=lambda _d: predictor)
    image_id = await cache.upload_image(b"png", 2, 2)
    model = MagicMock()
    model.segment_image_with_predictor.side_effect = error
    servicer = _servicer_with_cache(cache, model)
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=image_id, image_data=b"", width=0, height=0, coordinates=[(1, 1)], labels=[1],
        multimask_output=False, context=context, empty_message="empty",
    )

    assert context.abort.await_args.args[0] == grpc.StatusCode.INTERNAL
    assert (image_id not in cache._cache) is evicted
    assert str(error) not in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_a_cancelled_rpc_does_not_start_predict_after_it_wins_the_predictor_lock() -> None:
    """The job was already running on a worker, blocked on the predictor lock, when the call dropped."""
    import threading

    predictor_lock = threading.Lock()
    model = MagicMock()
    servicer = _servicer_with_cache(ImageCache(max_memory_bytes=1024, ttl_seconds=60), model)
    cancelled = threading.Event()
    predictor_lock.acquire()
    outcome: list = []

    def job() -> None:
        try:
            servicer._segment_with_locked_predictor(
                object(), predictor_lock, [(1, 1)], [1], False, (2, 2), cancelled
            )
        except Exception as error:  # noqa: BLE001
            outcome.append(error)

    worker = threading.Thread(target=job)
    worker.start()
    cancelled.set()
    predictor_lock.release()
    worker.join(5)

    assert len(outcome) == 1 and type(outcome[0]).__name__ == "RpcCancelled"
    model.segment_image_with_predictor.assert_not_called()


@pytest.mark.asyncio
async def test_cancelling_the_handler_marks_the_running_job_cancelled_and_releases_the_pin() -> None:
    import asyncio
    import threading

    predictor = object()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=lambda _d: predictor)
    image_id = await cache.upload_image(b"png", 2, 2)
    release = threading.Event()
    started = threading.Event()
    seen: list[bool] = []
    model = MagicMock()

    def slow_segment(**_kwargs):
        started.set()
        release.wait(5)
        return _empty_result()

    model.segment_image_with_predictor.side_effect = slow_segment
    servicer = _servicer_with_cache(cache, model)
    servicer.inference_executor = futures.ThreadPoolExecutor(max_workers=1)
    original = servicer._segment_with_locked_predictor

    def spy(*args):
        seen.append(args[-1].is_set())
        return original(*args)

    servicer._segment_with_locked_predictor = spy
    task = asyncio.create_task(
        servicer._dispatch_segmentation(
            image_id=image_id, image_data=b"", width=0, height=0, coordinates=[(1, 1)], labels=[1],
            multimask_output=False, context=AsyncMock(), empty_message="empty",
        )
    )
    while not started.is_set():
        await asyncio.sleep(0.01)
    task.cancel()
    with pytest.raises(asyncio.CancelledError):
        await task
    release.set()
    servicer.inference_executor.shutdown(wait=True)

    assert seen == [False]
    assert cache._cache[image_id].in_use == 0


@pytest.mark.asyncio
async def test_the_response_is_built_off_the_event_loop_thread() -> None:
    import threading

    model = MagicMock()
    model.segment_image.return_value = _empty_result()
    servicer = _servicer_with_cache(ImageCache(max_memory_bytes=1024, ttl_seconds=60), model)
    threads: list[int] = []
    original = servicer._build_segmentation_response

    def spy(*args, **kwargs):
        threads.append(threading.get_ident())
        return original(*args, **kwargs)

    servicer._build_segmentation_response = spy

    await servicer._dispatch_segmentation(
        image_id=0, image_data=_png(2, 2), width=2, height=2, coordinates=[(1, 1)], labels=[1],
        multimask_output=False, context=AsyncMock(), empty_message="empty",
    )

    assert threads and threads[0] != threading.get_ident()


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
    model.capabilities = SAM2_CAPABILITIES
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

@pytest.mark.asyncio
async def test_an_upload_the_cache_cannot_hold_is_resource_exhausted() -> None:
    from segmentation_grpc import UploadImageRequest
    from segmentation_server.image_cache import CacheFullError

    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60)
    servicer = _servicer_with_cache(cache)
    context = AsyncMock()
    data = _png(3, 2)

    with pytest.raises(CacheFullError):
        await servicer.UploadImage(UploadImageRequest(image_data=data, width=3, height=2), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED


@pytest.mark.asyncio
async def test_an_upload_tile_the_cache_cannot_hold_is_resource_exhausted() -> None:
    from segmentation_grpc import TileCoord, UploadTileRequest

    cache = ImageCache(max_memory_bytes=10, ttl_seconds=60)
    servicer = _servicer_with_cache(cache)
    context = AsyncMock()
    ok, encoded = cv2.imencode(".png", np.zeros((1024, 1024, 3), dtype=np.uint8))
    assert ok
    request = UploadTileRequest(
        coord=TileCoord(volume="v", section=1, channel="c", transform="t", downsample=1, row=0, col=0),
        image_data=encoded.tobytes(),
        width=1024,
        height=1024,
    )

    response = await servicer.UploadTile(request, context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert response.already_cached is False

@pytest.mark.asyncio
async def test_too_many_points_on_a_segment_request_is_resource_exhausted() -> None:
    from segmentation_server.server import RequestLimits

    servicer = _servicer_with_cache(ImageCache(max_memory_bytes=1024, ttl_seconds=60))
    servicer._limits = RequestLimits(max_points=3)
    context = AsyncMock()

    await servicer._dispatch_segmentation(
        image_id=0, image_data=_png(2, 2), width=2, height=2,
        coordinates=[(0, 0), (1, 1), (0, 1), (1, 0)], labels=[1, 1, 1, 1],
        multimask_output=False, context=context, empty_message="empty",
    )

    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_POINTS" in context.abort.await_args.args[1]
    servicer.model.segment_image.assert_not_called()


@pytest.mark.asyncio
async def test_a_stream_of_more_prompt_sets_than_the_limit_is_cut_off() -> None:
    from segmentation_server.server import RequestLimits

    predictor = object()
    cache = ImageCache(max_memory_bytes=1024, ttl_seconds=60, create_predictor_func=lambda _d: predictor)
    image_id = await cache.upload_image(b"png", 2, 2)
    model = MagicMock()
    model.segment_image_with_predictor.return_value = _empty_result()
    servicer = _servicer_with_cache(cache, model)
    servicer._limits = RequestLimits(max_sets=2)
    context = AsyncMock()

    responses = [
        response
        async for response in servicer.SegmentImageSets(
            _stream(*[_set_request(image_id, [(1, 1)], []) for _ in range(5)]), context
        )
    ]

    assert len(responses) == 2
    assert context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_SETS" in context.abort.await_args.args[1]
    assert model.segment_image_with_predictor.call_count == 2

@pytest.mark.asyncio
async def test_an_uploaded_image_is_decoded_once_and_the_pixels_reach_the_predictor(monkeypatch) -> None:
    from segmentation_grpc import UploadImageRequest
    from segmentation_server import server as server_module

    decodes: list[int] = []
    real = server_module.prepare_image_for_sam2
    monkeypatch.setattr(server_module, "prepare_image_for_sam2", lambda data: decodes.append(1) or real(data))
    seen: list[tuple] = []
    cache = ImageCache(
        max_memory_bytes=10_000_000, ttl_seconds=60,
        create_predictor_func=lambda *args: seen.append(args) or object(),
    )
    servicer = _servicer_with_cache(cache)

    response = await servicer.UploadImage(UploadImageRequest(image_data=_png(3, 2), width=3, height=2), AsyncMock())

    assert response.image_id > 0
    assert decodes == [1]
    data, pixels = seen[0]
    assert pixels.shape == (2, 3, 3)


@pytest.mark.asyncio
async def test_an_uploaded_tile_is_decoded_once_and_the_pixels_reach_the_predictor_and_the_disk_touch(monkeypatch) -> None:
    from segmentation_grpc import TileCoord, UploadTileRequest
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
    ok, encoded = cv2.imencode(".png", np.zeros((1024, 1024, 3), dtype=np.uint8))
    request = UploadTileRequest(
        coord=TileCoord(volume="v", section=1, channel="c", transform="t", downsample=1, row=0, col=0),
        image_data=encoded.tobytes(), width=1024, height=1024,
    )

    await servicer.UploadTile(request, AsyncMock())
    await servicer.UploadTile(request, AsyncMock())  # identical bytes: a cache hit

    assert decodes == [1, 1]  # one decode per upload call, none for the predictor, none for the touch
    assert seen[0][1].shape == (1024, 1024, 3)
    model.note_disk_embedding_used.assert_called_once()
    assert model.note_disk_embedding_used.call_args.args[1].shape == (1024, 1024, 3)
