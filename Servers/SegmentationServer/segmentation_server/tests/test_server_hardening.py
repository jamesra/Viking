"""Round-2 hardening: stream cap, quiet errors, omit_labeled_image, single-flight uploads, lifecycle."""

from __future__ import annotations

import asyncio
import os
import time
from concurrent import futures
from unittest.mock import AsyncMock, MagicMock

import cv2
import grpc
import numpy as np
import pytest

from segmentation_grpc import (
    DeleteImageRequest,
    MultiSegmentationRequest,
    Point,
    SegmentationRequest,
    UploadImageRequest,
    UploadTileRequest,
)
from segmentation_server import server as server_module
from segmentation_server.image_cache import ImageCache
from segmentation_server.model_capabilities import SAM2_CAPABILITIES
from segmentation_server.server import PortBindError, RequestLimits, SegmentationServicer
from test_segment_tiles import _context, _png as _tile_png, _tile
from test_tile_sharing import NEIGHBOURS, _Client, _request, _servicer_with_tile_zero


def _small_png(width: int = 3, height: int = 2) -> bytes:
    ok, encoded = cv2.imencode(".png", np.zeros((height, width, 3), dtype=np.uint8))
    assert ok
    return encoded.tobytes()


def _servicer(**cache_args) -> SegmentationServicer:
    model = MagicMock()
    model.capabilities = SAM2_CAPABILITIES
    model.segment_image_with_predictor.return_value = (np.zeros((2, 2), dtype=np.uint16), [])
    cache = ImageCache(
        max_memory_bytes=100_000_000,
        ttl_seconds=60,
        create_predictor_func=lambda *_a, **_k: object(),
        **cache_args,
    )
    return SegmentationServicer(model=model, image_cache=cache, server_start_time=0.0)


# --- concurrent stream cap ---------------------------------------------------------------------


@pytest.mark.asyncio
async def test_a_stream_over_the_concurrent_limit_is_refused_and_the_count_returns_to_zero() -> None:
    servicer = _servicer_with_tile_zero()
    await servicer.image_cache.upload_tile(("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 0), _tile_png(), 1024, 1024)
    servicer._limits = RequestLimits(max_concurrent_streams=1)
    first = _Client(servicer, _request())
    await first.next_needed()

    second = _Client(servicer, _request())
    await second.finish()

    assert second.updates == []
    assert second.context.abort.await_args.args[0] == grpc.StatusCode.RESOURCE_EXHAUSTED
    assert "SEGMENTATION_MAX_CONCURRENT_STREAMS" in second.context.abort.await_args.args[1]
    assert servicer._active_streams == 1

    await first.answer(unavailable=NEIGHBOURS)
    await first.finish()
    assert servicer._active_streams == 0


def test_the_stream_limit_comes_from_the_environment(monkeypatch) -> None:
    monkeypatch.setenv("SEGMENTATION_MAX_CONCURRENT_STREAMS", "3")
    assert RequestLimits.from_env().max_concurrent_streams == 3
    monkeypatch.setenv("SEGMENTATION_MAX_CONCURRENT_STREAMS", "many")
    assert RequestLimits.from_env().max_concurrent_streams == RequestLimits().max_concurrent_streams


# --- errors do not carry internals to the client -----------------------------------------------

SECRET = "secret-internal-detail"


@pytest.mark.asyncio
async def test_upload_image_failure_does_not_echo_the_exception() -> None:
    servicer = _servicer()
    servicer.image_cache.upload_image = AsyncMock(side_effect=RuntimeError(SECRET))
    context = AsyncMock()

    with pytest.raises(RuntimeError):
        await servicer.UploadImage(UploadImageRequest(image_data=_small_png(), width=3, height=2), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INTERNAL
    assert SECRET not in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_upload_tile_failure_does_not_echo_the_exception() -> None:
    servicer = _servicer()
    servicer.image_cache.upload_tile = AsyncMock(side_effect=RuntimeError(SECRET))
    context = AsyncMock()
    request = UploadTileRequest(coord=_tile(0, 0), image_data=_tile_png(), width=1024, height=1024)

    await servicer.UploadTile(request, context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INTERNAL
    assert SECRET not in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_delete_image_failure_does_not_echo_the_exception() -> None:
    servicer = _servicer()
    servicer.image_cache.delete_image = AsyncMock(side_effect=RuntimeError(SECRET))
    context = AsyncMock()

    await servicer.DeleteImage(DeleteImageRequest(image_id=1), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INTERNAL
    assert SECRET not in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_a_decode_failure_names_the_problem_without_the_decoders_own_text(monkeypatch) -> None:
    servicer = _servicer()
    monkeypatch.setattr(
        server_module, "prepare_image_for_sam2", MagicMock(side_effect=OSError(SECRET))
    )
    context = AsyncMock()

    await servicer.UploadImage(UploadImageRequest(image_data=b"x", width=3, height=2), context)

    assert context.abort.await_args.args[0] == grpc.StatusCode.INVALID_ARGUMENT
    assert SECRET not in context.abort.await_args.args[1]
    assert "decode" in context.abort.await_args.args[1]


@pytest.mark.asyncio
async def test_a_tile_stream_failure_does_not_echo_the_exception(monkeypatch) -> None:
    servicer = _servicer_with_tile_zero()
    await servicer.image_cache.upload_tile(("RC2", 1305, "TEM", "SliceToVolume1|", 1, 0, 0), _tile_png(), 1024, 1024)

    class Failing(server_module.GrowthWalk):
        def advance(self) -> None:
            raise RuntimeError(SECRET)

    monkeypatch.setattr(server_module, "GrowthWalk", Failing)
    client = _Client(servicer, _request())
    await client.finish()

    assert client.context.abort.await_args.args[0] == grpc.StatusCode.INTERNAL
    assert SECRET not in client.context.abort.await_args.args[1]


# --- omit_labeled_image on the unary calls -----------------------------------------------------


@pytest.mark.asyncio
@pytest.mark.parametrize("omit", [False, True])
async def test_segment_image_honours_omit_labeled_image(omit: bool) -> None:
    servicer = _servicer()
    image_id = await servicer.image_cache.upload_image(b"png", 2, 2)

    response = await servicer.SegmentImage(
        SegmentationRequest(
            image_id=image_id, coordinates=[Point(x=1, y=1)], labels=[1], omit_labeled_image=omit
        ),
        AsyncMock(),
    )

    assert bool(response.labeled_image) is (not omit)


@pytest.mark.asyncio
@pytest.mark.parametrize("omit", [False, True])
async def test_multi_segment_image_honours_omit_labeled_image(omit: bool) -> None:
    servicer = _servicer()
    image_id = await servicer.image_cache.upload_image(b"png", 2, 2)

    response = await servicer.MultiSegmentImage(
        MultiSegmentationRequest(
            image_id=image_id, foreground_points={1: Point(x=1, y=1)}, omit_labeled_image=omit
        ),
        AsyncMock(),
    )

    assert bool(response.labeled_image) is (not omit)


# --- single-flight tile uploads ----------------------------------------------------------------

KEY = ("v", 1, "c", "t", 1, 0, 0)


def _slow_cache(builds: list) -> ImageCache:
    def build(*_args, **_kwargs):
        builds.append(1)
        time.sleep(0.15)
        return object()

    return ImageCache(max_memory_bytes=10_000, ttl_seconds=60, create_predictor_func=build)


@pytest.mark.asyncio
async def test_two_uploads_of_the_same_tile_at_once_encode_it_once() -> None:
    builds: list = []
    cache = _slow_cache(builds)

    first, second = await asyncio.gather(
        cache.upload_tile(KEY, b"same", 1, 1), cache.upload_tile(KEY, b"same", 1, 1)
    )

    assert len(builds) == 1
    assert sorted([first[1], second[1]]) == [False, True]
    assert first[0] == second[0]
    assert len(cache._cache) == 1
    assert cache._tile_upload_locks == {}


@pytest.mark.asyncio
async def test_two_uploads_of_one_tile_with_different_bytes_leave_one_entry() -> None:
    builds: list = []
    cache = _slow_cache(builds)

    await asyncio.gather(cache.upload_tile(KEY, b"aaaa", 1, 1), cache.upload_tile(KEY, b"bbbb", 1, 1))

    assert len(cache._cache) == 1, "the first upload's entry must not be orphaned"
    assert len(cache._coord_index) == 1
    pinned = await cache.get_image_by_tile(KEY)
    assert pinned is not None and pinned[1] in (b"aaaa", b"bbbb")
    assert cache._tile_upload_locks == {}


@pytest.mark.asyncio
async def test_uploads_of_different_tiles_do_not_wait_for_each_other() -> None:
    builds: list = []
    cache = _slow_cache(builds)
    other = ("v", 1, "c", "t", 1, 0, 1)

    started = time.monotonic()
    await asyncio.gather(cache.upload_tile(KEY, b"aaaa", 1, 1), cache.upload_tile(other, b"bbbb", 1, 1))

    assert len(builds) == 2
    assert time.monotonic() - started < 0.29, "the two encodes should overlap"


# --- lifecycle ---------------------------------------------------------------------------------


class _RecordingExecutor(futures.ThreadPoolExecutor):
    created: list = []

    def __init__(self, *args, **kwargs) -> None:
        super().__init__(*args, **kwargs)
        self.was_shut_down = False
        _RecordingExecutor.created.append(self)

    def shutdown(self, *args, **kwargs):
        self.was_shut_down = True
        return super().shutdown(*args, **kwargs)


def test_create_server_stops_its_pools_when_the_servicer_cannot_be_built(monkeypatch) -> None:
    _RecordingExecutor.created = []
    monkeypatch.setattr(server_module.futures, "ThreadPoolExecutor", _RecordingExecutor)
    monkeypatch.setattr(server_module, "SegmentationServicer", MagicMock(side_effect=RuntimeError("model load")))
    monkeypatch.setattr(server_module.grpc.aio, "server", MagicMock())

    with pytest.raises(RuntimeError, match="model load"):
        server_module.create_server(port=1, credentials=None)

    assert len(_RecordingExecutor.created) == 2
    assert all(pool.was_shut_down for pool in _RecordingExecutor.created)


def test_create_server_reports_a_port_it_cannot_bind_and_stops_its_pools(monkeypatch) -> None:
    _RecordingExecutor.created = []
    servicer = MagicMock()
    fake_server = MagicMock()
    fake_server.add_secure_port.return_value = 0
    monkeypatch.setattr(server_module.futures, "ThreadPoolExecutor", _RecordingExecutor)
    monkeypatch.setattr(server_module, "SegmentationServicer", MagicMock(return_value=servicer))
    monkeypatch.setattr(server_module, "add_SegmentationServiceServicer_to_server", MagicMock())
    monkeypatch.setattr(server_module.grpc.aio, "server", MagicMock(return_value=fake_server))

    with pytest.raises(PortBindError):
        server_module.create_server(port=1, credentials=None)

    servicer.close.assert_called_once()
    assert all(pool.was_shut_down for pool in _RecordingExecutor.created)


def test_a_port_that_cannot_be_bound_exits_with_status_2(monkeypatch) -> None:
    from segmentation_server import __main__ as entry

    async def failing_main() -> None:
        raise PortBindError("Could not bind the TLS gRPC port [::]:443")

    monkeypatch.setattr(entry, "main", failing_main)

    with pytest.raises(SystemExit) as stop:
        entry.run()

    assert stop.value.code == 2


@pytest.mark.asyncio
async def test_a_failed_stub_generation_exits_non_zero(monkeypatch) -> None:
    from segmentation_server import __main__ as entry

    monkeypatch.setattr(entry, "generate_grpc_code", lambda _force: False)
    monkeypatch.setattr("sys.argv", ["segmentation_server", "--generate-grpc"])

    with pytest.raises(SystemExit) as stop:
        await entry.main()

    assert stop.value.code == 1


@pytest.mark.asyncio
async def test_a_stop_signal_that_arrives_while_the_model_loads_prevents_the_server_starting(monkeypatch) -> None:
    loop = asyncio.get_running_loop()
    handlers: dict = {}
    fake_server = MagicMock()
    fake_server.start = AsyncMock()
    fake_server.stop = AsyncMock()
    inference = futures.ThreadPoolExecutor(max_workers=1)
    grpc_pool = futures.ThreadPoolExecutor(max_workers=1)
    servicer = MagicMock()

    def loading_the_model(**_kwargs):
        # The event loop is blocked while the model loads, so the signal is delivered afterwards.
        loop.call_soon(handlers["stop"])
        return server_module.ServerParts(fake_server, servicer, inference, grpc_pool, "[::]:1")

    monkeypatch.setattr(server_module, "require_tls_pem_paths", lambda: ("cert", "key"))
    monkeypatch.setattr(server_module, "load_server_credentials", lambda *_paths: object())
    monkeypatch.setattr(server_module, "create_server", loading_the_model)
    monkeypatch.setattr(
        server_module, "install_stop_handlers", lambda _loop, request_stop: handlers.update(stop=request_stop)
    )

    await asyncio.wait_for(server_module.serve(port=1), timeout=10)

    fake_server.start.assert_not_awaited()
    servicer.close.assert_called_once()


# --- key file permissions ----------------------------------------------------------------------


@pytest.mark.skipif(os.name == "nt", reason="POSIX file modes")
def test_the_development_key_is_readable_only_by_its_owner(tmp_path) -> None:
    pytest.importorskip("cryptography")
    from segmentation_server.dev_cert import generate_self_signed

    _cert, key = generate_self_signed(tmp_path / "cert.pem", tmp_path / "key.pem")

    assert (key.stat().st_mode & 0o077) == 0
