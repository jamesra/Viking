"""TLS is the only transport: credential loading, the startup wait, and a real TLS server."""

from __future__ import annotations

import asyncio
import socket
from unittest.mock import MagicMock

import grpc
import pytest

pytest.importorskip("cryptography")

from segmentation_grpc import ServerStatusRequest, SegmentationServiceStub  # noqa: E402
from segmentation_server import server as server_module  # noqa: E402
from segmentation_server.dev_cert import generate_self_signed  # noqa: E402
from segmentation_server.model_capabilities import SAM2_CAPABILITIES  # noqa: E402
from segmentation_server.server import (  # noqa: E402
    SegmentationServicer,
    TlsConfigurationError,
    load_server_credentials,
    require_tls_pem_paths,
    resolve_tls_pem_paths,
)


@pytest.fixture(scope="module")
def dev_cert(tmp_path_factory):
    folder = tmp_path_factory.mktemp("dev-cert")
    return generate_self_signed(folder / "cert.pem", folder / "key.pem")


def _free_port() -> int:
    with socket.socket(socket.AF_INET6, socket.SOCK_STREAM) as probe:
        probe.bind(("::", 0))
        return probe.getsockname()[1]


def test_resolve_returns_none_for_unset_or_missing_paths(tmp_path, monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    assert resolve_tls_pem_paths() is None
    assert resolve_tls_pem_paths(cert_path="", key_path="") is None
    missing = tmp_path / "missing.pem"
    assert resolve_tls_pem_paths(cert_path=str(missing), key_path=str(missing)) is None


def test_load_credentials_refuses_unset_or_missing_files_instead_of_falling_back(
    tmp_path, monkeypatch
) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    with pytest.raises(TlsConfigurationError):
        load_server_credentials()
    missing = tmp_path / "missing.pem"
    with pytest.raises(TlsConfigurationError):
        load_server_credentials(cert_path=str(missing), key_path=str(missing))


def test_load_credentials_reads_a_real_certificate(dev_cert) -> None:
    cert, key = dev_cert
    assert load_server_credentials(str(cert), str(key)) is not None


def test_unset_paths_fail_at_once_without_waiting(monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    slept: list[float] = []
    with pytest.raises(TlsConfigurationError, match="SSL_CERT_PATH"):
        require_tls_pem_paths(wait_seconds=60, sleep=slept.append)
    assert slept == []


def test_missing_files_are_awaited_until_they_appear(dev_cert, tmp_path) -> None:
    cert, key = dev_cert
    later_cert, later_key = tmp_path / "later-cert.pem", tmp_path / "later-key.pem"
    polls = {"count": 0}

    def sleep(_seconds: float) -> None:
        polls["count"] += 1
        if polls["count"] == 3:
            later_cert.write_bytes(cert.read_bytes())
            later_key.write_bytes(key.read_bytes())

    clock = {"now": 0.0}

    def monotonic() -> float:
        clock["now"] += 1.0
        return clock["now"]

    result = require_tls_pem_paths(
        str(later_cert), str(later_key), wait_seconds=60, poll_seconds=2, sleep=sleep,
        monotonic=monotonic,
    )

    assert result == (str(later_cert), str(later_key))
    assert polls["count"] == 3


def test_missing_files_fail_with_an_actionable_message_after_the_wait(tmp_path) -> None:
    clock = {"now": 0.0}

    def monotonic() -> float:
        clock["now"] += 10.0
        return clock["now"]

    with pytest.raises(TlsConfigurationError, match="dev_cert") as error:
        require_tls_pem_paths(
            str(tmp_path / "a.pem"), str(tmp_path / "b.pem"), wait_seconds=25,
            sleep=lambda _s: None, monotonic=monotonic,
        )
    assert "not found" in str(error.value)


def _servicer_factory(**_ignored):
    model = MagicMock()
    model.capabilities = SAM2_CAPABILITIES
    return SegmentationServicer(model=model, server_start_time=0.0)


@pytest.mark.asyncio
async def test_serve_listens_with_tls_only_and_refuses_plaintext(dev_cert, monkeypatch) -> None:
    cert, key = dev_cert
    port = _free_port()
    monkeypatch.setenv("SSL_CERT_PATH", str(cert))
    monkeypatch.setenv("SSL_KEY_PATH", str(key))
    monkeypatch.setattr(server_module, "SegmentationServicer", _servicer_factory)

    task = asyncio.create_task(server_module.serve(port=port))
    try:
        credentials = grpc.ssl_channel_credentials(root_certificates=cert.read_bytes())
        async with grpc.aio.secure_channel(f"localhost:{port}", credentials) as channel:
            await asyncio.wait_for(channel.channel_ready(), timeout=30)
            status = await SegmentationServiceStub(channel).GetServerStatus(
                ServerStatusRequest(), timeout=10
            )
        assert status.version is not None

        async with grpc.aio.insecure_channel(f"localhost:{port}") as plain:
            with pytest.raises(grpc.aio.AioRpcError) as refused:
                await SegmentationServiceStub(plain).GetServerStatus(
                    ServerStatusRequest(), timeout=5
                )
            assert refused.value.code() == grpc.StatusCode.UNAVAILABLE
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task


@pytest.mark.asyncio
async def test_serve_without_certificates_fails_before_loading_the_model(monkeypatch) -> None:
    monkeypatch.delenv("SSL_CERT_PATH", raising=False)
    monkeypatch.delenv("SSL_KEY_PATH", raising=False)
    built: list[int] = []
    monkeypatch.setattr(
        server_module, "SegmentationServicer", lambda **kw: built.append(1) or _servicer_factory()
    )

    with pytest.raises(TlsConfigurationError):
        await server_module.serve(port=_free_port())

    assert built == []

def test_the_healthcheck_names_the_certificate_host_from_a_lets_encrypt_path(monkeypatch) -> None:
    from segmentation_server.healthcheck import target_name

    monkeypatch.delenv("SEGMENTATION_HEALTHCHECK_HOST", raising=False)
    assert target_name("/etc/letsencrypt/live/segmentation.codepharm.net/fullchain.pem") == "segmentation.codepharm.net"
    assert target_name("C:\\certs\\live\\example.org\\fullchain.pem") == "example.org"
    assert target_name("/tmp/dev-cert/cert.pem") == "localhost"
    assert target_name(None) == "localhost"
    monkeypatch.setenv("SEGMENTATION_HEALTHCHECK_HOST", "override.example")
    assert target_name("/etc/letsencrypt/live/segmentation.codepharm.net/fullchain.pem") == "override.example"


def test_the_healthcheck_port_comes_from_the_environment(monkeypatch) -> None:
    from segmentation_server.healthcheck import tls_port

    monkeypatch.delenv("SEGMENTATION_TLS_PORT", raising=False)
    assert tls_port() == 443
    monkeypatch.setenv("SEGMENTATION_TLS_PORT", "8443")
    assert tls_port() == 8443
    monkeypatch.setenv("SEGMENTATION_TLS_PORT", "x")
    assert tls_port() == 443


@pytest.mark.asyncio
async def test_the_healthcheck_passes_against_a_running_server_and_fails_when_it_is_down(dev_cert, monkeypatch) -> None:
    from segmentation_server.healthcheck import check

    cert, key = dev_cert
    port = _free_port()
    monkeypatch.setenv("SSL_CERT_PATH", str(cert))
    monkeypatch.setenv("SSL_KEY_PATH", str(key))
    monkeypatch.setattr(server_module, "SegmentationServicer", _servicer_factory)
    loop = asyncio.get_running_loop()

    assert await loop.run_in_executor(None, check, port, str(cert), 1.0) is not None  # nothing listening yet

    task = asyncio.create_task(server_module.serve(port=port))
    try:
        for _ in range(100):
            reason = await loop.run_in_executor(None, check, port, str(cert), 2.0)
            if reason is None:
                break
            await asyncio.sleep(0.1)
        assert reason is None
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task
    assert await loop.run_in_executor(None, check, port, str(cert), 1.0) is not None

@pytest.mark.asyncio
async def test_a_segmentation_travels_the_whole_wire_with_polygon_holes(dev_cert, monkeypatch) -> None:
    """UploadImage then SegmentImage over TLS, through the real servicer, cache and response encoder."""
    import cv2
    import numpy as np

    from segmentation_grpc import DeleteImageRequest, Point, SegmentationRequest, UploadImageRequest
    from segmentation_server.mask_utils import combined_mask_to_segments

    cert, key = dev_cert
    port = _free_port()
    monkeypatch.setenv("SSL_CERT_PATH", str(cert))
    monkeypatch.setenv("SSL_KEY_PATH", str(key))

    donut = np.zeros((64, 64), dtype=bool)
    donut[8:56, 8:56] = True
    donut[24:40, 24:40] = False

    def factory(**_ignored):
        model = MagicMock()
        model.capabilities = SAM2_CAPABILITIES
        model.segment_image_with_predictor.side_effect = (
            lambda **_kw: combined_mask_to_segments(donut, 0.9, empty_shape=(64, 64))
        )
        return SegmentationServicer(model=model, server_start_time=0.0)

    monkeypatch.setattr(server_module, "SegmentationServicer", factory)
    ok, encoded = cv2.imencode(".png", np.zeros((64, 64, 3), dtype=np.uint8))
    task = asyncio.create_task(server_module.serve(port=port))
    try:
        credentials = grpc.ssl_channel_credentials(root_certificates=cert.read_bytes())
        async with grpc.aio.secure_channel(f"localhost:{port}", credentials) as channel:
            await asyncio.wait_for(channel.channel_ready(), timeout=30)
            stub = SegmentationServiceStub(channel)
            upload = await stub.UploadImage(
                UploadImageRequest(image_data=encoded.tobytes(), width=64, height=64), timeout=20
            )
            assert upload.image_id > 0

            response = await stub.SegmentImage(
                SegmentationRequest(
                    image_id=upload.image_id, coordinates=[Point(x=10, y=10)], labels=[1], omit_labeled_image=True
                ),
                timeout=20,
            )
            await stub.DeleteImage(DeleteImageRequest(image_id=upload.image_id), timeout=20)

        assert len(response.segments) == 1
        polygon = response.segments[0].polygons[0]
        assert len(polygon.points) >= 4
        assert len(polygon.holes) == 1 and len(polygon.holes[0].points) >= 4
        assert response.width == 64 and response.height == 64
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task

@pytest.mark.asyncio
async def test_the_old_port_flag_is_an_error_not_a_silent_wrong_listener(monkeypatch, capsys) -> None:
    from segmentation_server import __main__ as entry

    monkeypatch.setattr("sys.argv", ["segmentation_server", "--port", "80"])

    with pytest.raises(SystemExit) as stop:
        await entry.main()

    assert stop.value.code == 2
    assert "--tls-port" in capsys.readouterr().err


@pytest.mark.asyncio
async def test_the_tls_port_flag_reaches_serve(monkeypatch) -> None:
    from segmentation_server import __main__ as entry

    seen = {}

    async def fake_serve(**kwargs):
        seen.update(kwargs)

    monkeypatch.setattr(entry, "serve", fake_serve)
    monkeypatch.setattr("sys.argv", ["segmentation_server", "--tls-port", "9443", "--no-compile-image-encoder"])

    await entry.main()

    assert seen["port"] == 9443
    assert seen["compile_image_encoder"] is False
